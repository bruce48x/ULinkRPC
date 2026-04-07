using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ULinkRPC.CodeGen;

internal static partial class ContractParser
{
    private const string RpcServiceAttributeName = "RpcService";
    private const string RpcCallbackAttributeName = "RpcCallback";
    private const string RpcMethodAttributeName = "RpcMethod";
    private const string RpcPushAttributeName = "RpcPush";
    private const string ValueTaskTypeName = "ValueTask";
    private const string ValueTaskNamespace = "System.Threading.Tasks";
    private const string AnalysisCompilationName = "ULinkRPC.Contracts.Analysis";

    public static List<RpcServiceInfo> FindRpcServicesFromSource(string contractsPath)
    {
        var trees = LoadSyntaxTrees(contractsPath);
        var compilation = CreateContractsCompilation(trees);

        var sourceFiles = new List<SourceFileInfo>();
        var services = new List<RpcServiceInfo>();

        foreach (var tree in trees)
        {
            var sourceFile = ParseSourceFile(compilation, tree);
            sourceFiles.Add(sourceFile);
            services.AddRange(sourceFile.Services);
        }

        ValidateNoDuplicateServiceIds(services);
        ResolveCallbacks(services, sourceFiles);

        return services;
    }

    private static List<SyntaxTree> LoadSyntaxTrees(string contractsPath)
    {
        var files = Directory.GetFiles(contractsPath, "*.cs", SearchOption.AllDirectories);
        return files
            .Select(file => CSharpSyntaxTree.ParseText(File.ReadAllText(file), path: file))
            .ToList();
    }

    private static CSharpCompilation CreateContractsCompilation(IReadOnlyList<SyntaxTree> trees)
    {
        var references = new List<MetadataReference>();
        var trustedAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        if (!string.IsNullOrWhiteSpace(trustedAssemblies))
        {
            foreach (var path in trustedAssemblies.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
                references.Add(MetadataReference.CreateFromFile(path));
        }

        if (references.Count == 0)
            Console.Error.WriteLine("Warning: No platform assembly references found. Type resolution may be incomplete.");

        return CSharpCompilation.Create(
            AnalysisCompilationName,
            trees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static SourceFileInfo ParseSourceFile(CSharpCompilation compilation, SyntaxTree tree)
    {
        var semanticModel = compilation.GetSemanticModel(tree, ignoreAccessibility: true);
        var root = tree.GetCompilationUnitRoot();
        var services = ParseServices(root, semanticModel);
        var callbackInterfaces = ParseCallbackInterfaces(root, semanticModel);
        return new SourceFileInfo(services, callbackInterfaces);
    }

    private static void ResolveCallbacks(
        IReadOnlyList<RpcServiceInfo> services,
        IReadOnlyList<SourceFileInfo> sourceFiles)
    {
        foreach (var service in services)
        {
            if (!RequiresCallbackResolution(service))
                continue;

            if (TryBindCallback(service, sourceFiles))
                continue;

            throw new InvalidOperationException(
                $"Callback interface '{service.CallbackInterfaceFullName}' declared by service '{service.InterfaceFullName}' was not found or is missing a valid [RpcCallback] contract.");
        }
    }

    private static bool RequiresCallbackResolution(RpcServiceInfo service)
    {
        return !string.IsNullOrEmpty(service.CallbackInterfaceName) &&
               !string.IsNullOrEmpty(service.CallbackInterfaceFullName);
    }

    private static bool TryBindCallback(RpcServiceInfo service, IReadOnlyList<SourceFileInfo> sourceFiles)
    {
        foreach (var sourceFile in sourceFiles)
        {
            if (!sourceFile.CallbackInterfaces.TryGetValue(service.CallbackInterfaceFullName!, out var callbackInfo))
                continue;

            if (!string.Equals(callbackInfo.ServiceFullName, service.InterfaceFullName, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"Callback interface '{callbackInfo.FullName}' is associated with '{callbackInfo.ServiceFullName}', but service '{service.InterfaceFullName}' declared it as its callback.");

            service.CallbackMethods = callbackInfo.Methods;
            service.AddUsingDirectives(callbackInfo.RequiredNamespaces);
            return true;
        }

        return false;
    }

    private static List<RpcServiceInfo> ParseServices(CompilationUnitSyntax root, SemanticModel semanticModel)
    {
        var services = new List<RpcServiceInfo>();
        foreach (var iface in root.DescendantNodes().OfType<InterfaceDeclarationSyntax>())
        {
            if (semanticModel.GetDeclaredSymbol(iface) is not INamedTypeSymbol ifaceSymbol)
                continue;

            if (!TryGetAttributeIntValue(iface.AttributeLists, semanticModel, RpcServiceAttributeName, out var serviceId))
                continue;

            var methods = ParseServiceMethods(iface, semanticModel, out var requiredNamespaces);
            if (methods.Count == 0)
                throw new InvalidOperationException(
                    BuildInterfaceContractError(
                        iface,
                        "RPC services must declare at least one [RpcMethod] contract."));

            AddNamespace(requiredNamespaces, ifaceSymbol.ContainingNamespace);
            var fullName = GetTypeFullName(ifaceSymbol);
            TryGetAttributeTypeValue(
                iface.AttributeLists,
                semanticModel,
                RpcServiceAttributeName,
                out var callbackName,
                out var callbackFullName);

            var service = new RpcServiceInfo(ifaceSymbol.Name, fullName, serviceId, methods, requiredNamespaces.ToList())
            {
                CallbackInterfaceName = callbackName,
                CallbackInterfaceFullName = callbackFullName
            };
            services.Add(service);
        }

        return services;
    }

    private static Dictionary<string, CallbackInterfaceInfo> ParseCallbackInterfaces(
        CompilationUnitSyntax root, SemanticModel semanticModel)
    {
        var callbacks = new Dictionary<string, CallbackInterfaceInfo>(StringComparer.Ordinal);
        foreach (var iface in root.DescendantNodes().OfType<InterfaceDeclarationSyntax>())
        {
            if (semanticModel.GetDeclaredSymbol(iface) is not INamedTypeSymbol ifaceSymbol)
                continue;

            if (!TryGetAttributeTypeValue(
                    iface.AttributeLists,
                    semanticModel,
                    RpcCallbackAttributeName,
                    out _,
                    out var serviceFullName))
                continue;

            var methods = ParseCallbackMethods(iface, semanticModel, out var requiredNamespaces);
            if (methods.Count == 0)
                throw new InvalidOperationException(
                    BuildInterfaceContractError(
                        iface,
                        "RPC callback interfaces must declare at least one valid [RpcPush] contract."));

            AddNamespace(requiredNamespaces, ifaceSymbol.ContainingNamespace);
            var callbackFullName = GetTypeFullName(ifaceSymbol);
            callbacks.TryAdd(
                callbackFullName,
                new CallbackInterfaceInfo(
                    ifaceSymbol.Name,
                    callbackFullName,
                    serviceFullName!,
                    methods,
                    requiredNamespaces.ToList()));
        }

        return callbacks;
    }

    private static List<RpcMethodInfo> ParseServiceMethods(
        InterfaceDeclarationSyntax iface,
        SemanticModel semanticModel,
        out HashSet<string> requiredNamespaces)
    {
        requiredNamespaces = new HashSet<string>(StringComparer.Ordinal);
        var methods = new List<RpcMethodInfo>();
        foreach (var method in iface.Members.OfType<MethodDeclarationSyntax>())
        {
            if (semanticModel.GetDeclaredSymbol(method) is not IMethodSymbol methodSymbol)
                continue;

            if (!TryGetAttributeIntValue(method.AttributeLists, semanticModel, RpcMethodAttributeName, out var methodId))
                continue;

            var parameters = ParseParameters(method.ParameterList.Parameters);
            ValidateSingleDtoRequestParameter(method, iface, methodSymbol);
            foreach (var parameterSymbol in methodSymbol.Parameters)
                AddTypeNamespaces(requiredNamespaces, parameterSymbol.Type);

            if (!TryParseRpcReturnType(methodSymbol, method.ReturnType, out var parsedReturnType, out var isVoid))
            {
                var location = method.GetLocation().GetLineSpan();
                var line = location.StartLinePosition.Line + 1;
                throw new InvalidOperationException(
                    $"Unsupported return type '{method.ReturnType}' on {iface.Identifier.ValueText}.{method.Identifier.ValueText} in {location.Path}:{line}. RPC methods must return ValueTask or ValueTask<T>.");
            }

            var retType = isVoid ? null : parsedReturnType;
            if (!isVoid)
                ValidateDtoResponseType(method, iface, methodSymbol);

            if (methodSymbol.ReturnType is INamedTypeSymbol returnTypeSymbol && returnTypeSymbol.TypeArguments.Length == 1)
                AddTypeNamespaces(requiredNamespaces, returnTypeSymbol.TypeArguments[0]);

            methods.Add(new RpcMethodInfo(method.Identifier.ValueText, methodId, parameters, retType, isVoid));
        }

        ValidateNoDuplicateMethodIds(methods, iface.Identifier.ValueText);
        return methods;
    }

    private static List<RpcCallbackMethodInfo> ParseCallbackMethods(
        InterfaceDeclarationSyntax iface,
        SemanticModel semanticModel,
        out HashSet<string> requiredNamespaces)
    {
        requiredNamespaces = new HashSet<string>(StringComparer.Ordinal);
        var methods = new List<RpcCallbackMethodInfo>();
        foreach (var method in iface.Members.OfType<MethodDeclarationSyntax>())
        {
            if (semanticModel.GetDeclaredSymbol(method) is not IMethodSymbol methodSymbol)
                continue;

            if (!TryGetAttributeIntValue(method.AttributeLists, semanticModel, RpcPushAttributeName, out var methodId))
                continue;

            if (!methodSymbol.ReturnsVoid)
                throw new InvalidOperationException(
                    BuildContractShapeError(
                        method,
                        iface,
                        methodSymbol,
                        "RPC callback methods must return void."));

            var parameters = ParseParameters(method.ParameterList.Parameters);
            ValidateSingleDtoCallbackParameter(method, iface, methodSymbol);
            foreach (var parameterSymbol in methodSymbol.Parameters)
                AddTypeNamespaces(requiredNamespaces, parameterSymbol.Type);

            methods.Add(new RpcCallbackMethodInfo(method.Identifier.ValueText, methodId, parameters));
        }

        ValidateNoDuplicateCallbackMethodIds(methods, iface.Identifier.ValueText);
        return methods;
    }

}
