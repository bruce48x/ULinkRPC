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
        var files = Directory.GetFiles(contractsPath, "*.cs", SearchOption.AllDirectories);
        var trees = files
            .Select(file => CSharpSyntaxTree.ParseText(File.ReadAllText(file), path: file))
            .ToList();
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

        foreach (var svc in services)
        {
            if (string.IsNullOrEmpty(svc.CallbackInterfaceName) ||
                string.IsNullOrEmpty(svc.CallbackInterfaceFullName))
                continue;

            foreach (var sourceFile in sourceFiles)
            {
                if (!sourceFile.CallbackInterfaces.TryGetValue(svc.CallbackInterfaceFullName, out var callbackInfo))
                    continue;

                if (!string.Equals(callbackInfo.ServiceFullName, svc.InterfaceFullName, StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        $"Callback interface '{callbackInfo.FullName}' is associated with '{callbackInfo.ServiceFullName}', but service '{svc.InterfaceFullName}' declared it as its callback.");

                svc.CallbackMethods = callbackInfo.Methods;
                svc.AddUsingDirectives(callbackInfo.RequiredNamespaces);
                break;
            }

            if (svc.HasCallback)
                continue;

            throw new InvalidOperationException(
                $"Callback interface '{svc.CallbackInterfaceFullName}' declared by service '{svc.InterfaceFullName}' was not found or is missing a valid [RpcCallback] contract.");
        }

        return services;
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
                continue;

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
                continue;

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
                continue;

            var parameters = ParseParameters(method.ParameterList.Parameters);
            ValidateSingleDtoCallbackParameter(method, iface, methodSymbol);
            foreach (var parameterSymbol in methodSymbol.Parameters)
                AddTypeNamespaces(requiredNamespaces, parameterSymbol.Type);

            methods.Add(new RpcCallbackMethodInfo(method.Identifier.ValueText, methodId, parameters));
        }

        ValidateNoDuplicateCallbackMethodIds(methods, iface.Identifier.ValueText);
        return methods;
    }

    private static List<RpcParameterInfo> ParseParameters(SeparatedSyntaxList<ParameterSyntax> parameters)
    {
        var result = new List<RpcParameterInfo>();
        for (var i = 0; i < parameters.Count; i++)
        {
            var parameter = parameters[i];
            var typeName = parameter.Type?.ToString() ?? "object";
            var name = parameter.Identifier.ValueText;
            if (string.IsNullOrWhiteSpace(name))
                name = $"arg{i + 1}";
            result.Add(new RpcParameterInfo(typeName, name));
        }
        return result;
    }

    #region Validation

    private static void ValidateNoDuplicateServiceIds(List<RpcServiceInfo> services)
    {
        var seen = new Dictionary<int, string>();
        foreach (var svc in services)
        {
            if (seen.TryGetValue(svc.ServiceId, out var existingName))
                throw new InvalidOperationException(
                    $"Duplicate ServiceId {svc.ServiceId} found on '{existingName}' and '{svc.InterfaceName}'. Each [RpcService] must have a unique id.");
            seen[svc.ServiceId] = svc.InterfaceName;
        }
    }

    private static void ValidateNoDuplicateMethodIds(List<RpcMethodInfo> methods, string interfaceName)
    {
        var seen = new Dictionary<int, string>();
        foreach (var m in methods)
        {
            if (seen.TryGetValue(m.MethodId, out var existingName))
                throw new InvalidOperationException(
                    $"Duplicate MethodId {m.MethodId} found on '{existingName}' and '{m.Name}' in {interfaceName}. Each [RpcMethod] within a service must have a unique id.");
            seen[m.MethodId] = m.Name;
        }
    }

    private static void ValidateNoDuplicateCallbackMethodIds(List<RpcCallbackMethodInfo> methods, string interfaceName)
    {
        var seen = new Dictionary<int, string>();
        foreach (var m in methods)
        {
            if (seen.TryGetValue(m.MethodId, out var existingName))
                throw new InvalidOperationException(
                    $"Duplicate MethodId {m.MethodId} found on '{existingName}' and '{m.Name}' in {interfaceName}. Each [RpcMethod] within a callback interface must have a unique id.");
            seen[m.MethodId] = m.Name;
        }
    }

    private static void ValidateSingleDtoRequestParameter(
        MethodDeclarationSyntax method,
        InterfaceDeclarationSyntax iface,
        IMethodSymbol methodSymbol)
    {
        if (methodSymbol.Parameters.Length != 1)
            throw new InvalidOperationException(
                BuildContractShapeError(
                    method,
                    iface,
                    methodSymbol,
                    "RPC methods must declare exactly one request DTO parameter."));

        if (!IsDtoContractType(methodSymbol.Parameters[0].Type))
            throw new InvalidOperationException(
                BuildContractShapeError(
                    method,
                    iface,
                    methodSymbol,
                    $"RPC request parameter '{methodSymbol.Parameters[0].Name}' must be a DTO type, not '{methodSymbol.Parameters[0].Type.ToDisplayString()}'."));
    }

    private static void ValidateDtoResponseType(
        MethodDeclarationSyntax method,
        InterfaceDeclarationSyntax iface,
        IMethodSymbol methodSymbol)
    {
        if (methodSymbol.ReturnType is not INamedTypeSymbol returnTypeSymbol ||
            returnTypeSymbol.TypeArguments.Length != 1)
            return;

        var resultType = returnTypeSymbol.TypeArguments[0];
        if (!IsDtoContractType(resultType))
            throw new InvalidOperationException(
                BuildContractShapeError(
                    method,
                    iface,
                    methodSymbol,
                    $"RPC response type must be a DTO type, not '{resultType.ToDisplayString()}'."));
    }

    private static void ValidateSingleDtoCallbackParameter(
        MethodDeclarationSyntax method,
        InterfaceDeclarationSyntax iface,
        IMethodSymbol methodSymbol)
    {
        if (methodSymbol.Parameters.Length != 1)
            throw new InvalidOperationException(
                BuildContractShapeError(
                    method,
                    iface,
                    methodSymbol,
                    "RPC callback methods must declare exactly one push DTO parameter."));

        if (!IsDtoContractType(methodSymbol.Parameters[0].Type))
            throw new InvalidOperationException(
                BuildContractShapeError(
                    method,
                    iface,
                    methodSymbol,
                    $"RPC push parameter '{methodSymbol.Parameters[0].Name}' must be a DTO type, not '{methodSymbol.Parameters[0].Type.ToDisplayString()}'."));
    }

    private static bool IsDtoContractType(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol nullableType &&
            nullableType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
        {
            type = nullableType.TypeArguments[0];
        }

        if (type is not INamedTypeSymbol namedType)
            return false;

        if (namedType.IsTupleType)
            return false;

        if (namedType.SpecialType != SpecialType.None ||
            namedType.TypeKind is TypeKind.Enum or TypeKind.Delegate or TypeKind.Interface)
            return false;

        var ns = namedType.ContainingNamespace?.ToDisplayString() ?? string.Empty;
        return !ns.StartsWith("System", StringComparison.Ordinal);
    }

    private static string BuildContractShapeError(
        MethodDeclarationSyntax method,
        InterfaceDeclarationSyntax iface,
        IMethodSymbol methodSymbol,
        string reason)
    {
        var location = method.GetLocation().GetLineSpan();
        var line = location.StartLinePosition.Line + 1;
        return $"{reason} Offending member: {iface.Identifier.ValueText}.{methodSymbol.Name} in {location.Path}:{line}.";
    }

    #endregion
}
