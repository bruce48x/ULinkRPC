using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ULinkRPC.CodeGen;

internal static partial class Program
{
    private static List<RpcServiceInfo> FindRpcServicesFromSource(string contractsPath)
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

        foreach (var svc in services)
        {
            if (string.IsNullOrEmpty(svc.CallbackInterfaceName) || string.IsNullOrEmpty(svc.CallbackInterfaceFullName))
                continue;

            foreach (var sourceFile in sourceFiles)
            {
                if (!sourceFile.CallbackInterfaces.TryGetValue(svc.CallbackInterfaceFullName, out var callbackInfo))
                    continue;

                svc.CallbackMethods = callbackInfo.Methods;
                svc.AddUsingDirectives(callbackInfo.RequiredNamespaces);
                break;
            }
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

        return CSharpCompilation.Create(
            "ULinkRPC.Contracts.Analysis",
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

            if (!TryGetAttributeIntValue(iface.AttributeLists, semanticModel, "RpcService", out var serviceId))
                continue;

            var methods = ParseServiceMethods(iface, semanticModel, out var requiredNamespaces);
            if (methods.Count == 0)
                continue;

            AddNamespace(requiredNamespaces, ifaceSymbol.ContainingNamespace);
            var fullName = GetTypeFullName(ifaceSymbol);
            var callbackType = TryGetServiceCallbackType(ifaceSymbol);
            var callbackName = callbackType?.Name;
            var callbackFullName = callbackType != null ? GetTypeFullName(callbackType) : null;
            if (callbackType == null)
                TryGetServiceCallbackTypeFromSyntax(iface, semanticModel, out callbackName, out callbackFullName);

            var service = new RpcServiceInfo(ifaceSymbol.Name, fullName, serviceId, methods, requiredNamespaces.ToList())
            {
                CallbackInterfaceName = callbackName,
                CallbackInterfaceFullName = callbackFullName
            };
            services.Add(service);
        }

        return services;
    }

    private static Dictionary<string, CallbackInterfaceInfo> ParseCallbackInterfaces(CompilationUnitSyntax root, SemanticModel semanticModel)
    {
        var callbacks = new Dictionary<string, CallbackInterfaceInfo>(StringComparer.Ordinal);
        foreach (var iface in root.DescendantNodes().OfType<InterfaceDeclarationSyntax>())
        {
            if (semanticModel.GetDeclaredSymbol(iface) is not INamedTypeSymbol ifaceSymbol)
                continue;

            if (TryGetAttributeIntValue(iface.AttributeLists, semanticModel, "RpcService", out _))
                continue;

            var methods = ParseCallbackMethods(iface, semanticModel, out var requiredNamespaces);
            if (methods.Count == 0)
                continue;

            AddNamespace(requiredNamespaces, ifaceSymbol.ContainingNamespace);

            var callbackFullName = GetTypeFullName(ifaceSymbol);
            if (callbacks.ContainsKey(callbackFullName))
                continue;

            callbacks.Add(
                callbackFullName,
                new CallbackInterfaceInfo(ifaceSymbol.Name, callbackFullName, methods, requiredNamespaces.ToList()));
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

            if (!TryGetAttributeIntValue(method.AttributeLists, semanticModel, "RpcMethod", out var methodId))
                continue;

            var parameters = ParseParameters(method.ParameterList.Parameters);
            foreach (var parameterSymbol in methodSymbol.Parameters)
                AddTypeNamespaces(requiredNamespaces, parameterSymbol.Type);

            if (!TryParseRpcReturnType(methodSymbol, method.ReturnType, out var parsedReturnType, out var isVoid))
            {
                var location = method.GetLocation().GetLineSpan();
                var line = location.StartLinePosition.Line + 1;
                throw new InvalidOperationException(
                    $"Unsupported return type '{method.ReturnType}' on {iface.Identifier.ValueText}.{method.Identifier.ValueText} in {location.Path}:{line}. RPC methods must return ValueTask or ValueTask<T>.");
            }

            var retType = isVoid
                ? null
                : parsedReturnType;

            if (methodSymbol.ReturnType is INamedTypeSymbol returnTypeSymbol && returnTypeSymbol.TypeArguments.Length == 1)
                AddTypeNamespaces(requiredNamespaces, returnTypeSymbol.TypeArguments[0]);

            methods.Add(new RpcMethodInfo(method.Identifier.ValueText, methodId, parameters, retType, isVoid));
        }

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

            if (!TryGetAttributeIntValue(method.AttributeLists, semanticModel, "RpcMethod", out var methodId))
                continue;

            if (!methodSymbol.ReturnsVoid)
                continue;

            var parameters = ParseParameters(method.ParameterList.Parameters);
            foreach (var parameterSymbol in methodSymbol.Parameters)
                AddTypeNamespaces(requiredNamespaces, parameterSymbol.Type);

            methods.Add(new RpcCallbackMethodInfo(method.Identifier.ValueText, methodId, parameters));
        }

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

}
