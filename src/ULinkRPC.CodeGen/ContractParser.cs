using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ULinkRPC.CodeGen;

internal static class ContractParser
{
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

        foreach (var svc in services)
        {
            if (string.IsNullOrEmpty(svc.CallbackInterfaceName) ||
                string.IsNullOrEmpty(svc.CallbackInterfaceFullName))
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

    private static Dictionary<string, CallbackInterfaceInfo> ParseCallbackInterfaces(
        CompilationUnitSyntax root, SemanticModel semanticModel)
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
            callbacks.TryAdd(
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

            var retType = isVoid ? null : parsedReturnType;

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

    #region Attribute helpers

    private static bool TryGetAttributeIntValue(
        SyntaxList<AttributeListSyntax> attributeLists,
        SemanticModel semanticModel,
        string attributeBaseName,
        out int value)
    {
        foreach (var attribute in attributeLists.SelectMany(list => list.Attributes))
        {
            if (!IsMatchingAttribute(attribute, semanticModel, attributeBaseName))
                continue;

            if (TryExtractAttributeIntArgument(attribute, semanticModel, out var intValue))
            {
                value = intValue;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static bool IsMatchingAttribute(
        AttributeSyntax attribute, SemanticModel semanticModel, string attributeBaseName)
    {
        foreach (var symbol in GetAttributeCandidateSymbols(attribute, semanticModel))
        {
            var containingType = symbol.ContainingType;
            if (containingType == null) continue;

            var typeName = containingType.Name;
            if (string.Equals(typeName, attributeBaseName, StringComparison.Ordinal) ||
                string.Equals(typeName, $"{attributeBaseName}Attribute", StringComparison.Ordinal))
                return true;
        }

        return IsAttributeName(attribute.Name, attributeBaseName);
    }

    private static IEnumerable<IMethodSymbol> GetAttributeCandidateSymbols(
        AttributeSyntax attribute, SemanticModel semanticModel)
    {
        var symbolInfo = semanticModel.GetSymbolInfo(attribute);
        if (symbolInfo.Symbol is IMethodSymbol methodSymbol)
            yield return methodSymbol;

        foreach (var candidate in symbolInfo.CandidateSymbols.OfType<IMethodSymbol>())
            yield return candidate;
    }

    private static bool TryExtractAttributeIntArgument(
        AttributeSyntax attribute, SemanticModel semanticModel, out int value)
    {
        value = default;
        var arguments = attribute.ArgumentList?.Arguments;
        if (arguments == null || arguments.Value.Count == 0)
            return false;

        foreach (var argument in arguments.Value)
        {
            if (!IsNamedAttributeArgument(argument)) continue;
            if (TryGetIntConstant(argument.Expression, semanticModel, out value))
                return true;
        }

        foreach (var argument in arguments.Value)
        {
            if (IsNamedAttributeArgument(argument)) continue;
            if (TryGetIntConstant(argument.Expression, semanticModel, out value))
                return true;
        }

        return false;
    }

    private static bool IsNamedAttributeArgument(AttributeArgumentSyntax argument) =>
        argument.NameColon != null || argument.NameEquals != null;

    private static bool TryGetIntConstant(ExpressionSyntax expression, SemanticModel semanticModel, out int value)
    {
        value = default;
        var constant = semanticModel.GetConstantValue(expression);
        if (!constant.HasValue || constant.Value == null)
            return false;

        switch (constant.Value)
        {
            case int intValue:
                value = intValue;
                return true;
            case long longValue when longValue is >= int.MinValue and <= int.MaxValue:
                value = (int)longValue;
                return true;
            case short shortValue:
                value = shortValue;
                return true;
            case byte byteValue:
                value = byteValue;
                return true;
            default:
                return false;
        }
    }

    private static bool IsAttributeName(NameSyntax attributeName, string attributeBaseName)
    {
        var simpleName = GetRightMostName(attributeName);
        return string.Equals(simpleName, attributeBaseName, StringComparison.Ordinal) ||
               string.Equals(simpleName, $"{attributeBaseName}Attribute", StringComparison.Ordinal);
    }

    private static string GetRightMostName(NameSyntax nameSyntax) => nameSyntax switch
    {
        IdentifierNameSyntax id => id.Identifier.ValueText,
        GenericNameSyntax generic => generic.Identifier.ValueText,
        QualifiedNameSyntax qualified => GetRightMostName(qualified.Right),
        AliasQualifiedNameSyntax aliasQualified => GetRightMostName(aliasQualified.Name),
        _ => nameSyntax.ToString()
    };

    #endregion

    #region Type helpers

    private static INamedTypeSymbol? TryGetServiceCallbackType(INamedTypeSymbol serviceInterface)
    {
        foreach (var baseInterface in serviceInterface.AllInterfaces)
        {
            if (!string.Equals(baseInterface.Name, "IRpcService", StringComparison.Ordinal))
                continue;
            if (baseInterface.TypeArguments.Length < 2)
                continue;
            return baseInterface.TypeArguments[1] as INamedTypeSymbol;
        }
        return null;
    }

    private static bool TryGetServiceCallbackTypeFromSyntax(
        InterfaceDeclarationSyntax iface,
        SemanticModel semanticModel,
        out string? callbackName,
        out string? callbackFullName)
    {
        callbackName = null;
        callbackFullName = null;
        if (iface.BaseList == null)
            return false;

        foreach (var baseType in iface.BaseList.Types)
        {
            if (!TryGetIrpcServiceGeneric(baseType.Type, out var generic) ||
                generic.TypeArgumentList.Arguments.Count < 2)
                continue;

            var callbackTypeSyntax = generic.TypeArgumentList.Arguments[1];
            callbackName = GetTypeSimpleName(callbackTypeSyntax);
            callbackFullName = GetTypeFullNameFromSyntax(callbackTypeSyntax, semanticModel) ?? callbackName;
            return true;
        }
        return false;
    }

    private static bool TryGetIrpcServiceGeneric(TypeSyntax typeSyntax, out GenericNameSyntax generic)
    {
        switch (typeSyntax)
        {
            case GenericNameSyntax genericName when genericName.Identifier.ValueText == "IRpcService":
                generic = genericName;
                return true;
            case QualifiedNameSyntax qualified:
                return TryGetIrpcServiceGeneric(qualified.Right, out generic);
            case AliasQualifiedNameSyntax aliasQualified:
                return TryGetIrpcServiceGeneric(aliasQualified.Name, out generic);
            default:
                generic = null!;
                return false;
        }
    }

    private static string GetTypeSimpleName(TypeSyntax typeSyntax) => typeSyntax switch
    {
        IdentifierNameSyntax id => id.Identifier.ValueText,
        GenericNameSyntax generic => generic.Identifier.ValueText,
        QualifiedNameSyntax qualified => GetTypeSimpleName(qualified.Right),
        AliasQualifiedNameSyntax aliasQualified => GetTypeSimpleName(aliasQualified.Name),
        NullableTypeSyntax nullable => GetTypeSimpleName(nullable.ElementType),
        _ => typeSyntax.ToString()
    };

    private static string? GetTypeFullNameFromSyntax(TypeSyntax typeSyntax, SemanticModel semanticModel)
    {
        if (semanticModel.GetTypeInfo(typeSyntax).Type is INamedTypeSymbol typeSymbol)
            return GetTypeFullName(typeSymbol);
        return null;
    }

    private static bool TryParseRpcReturnType(
        IMethodSymbol methodSymbol,
        TypeSyntax returnTypeSyntax,
        out string? returnType,
        out bool isVoid)
    {
        returnType = null;
        isVoid = false;

        if (methodSymbol.ReturnType is not INamedTypeSymbol returnTypeSymbol)
            return false;

        var returnTypeNamespace = returnTypeSymbol.ContainingNamespace?.ToDisplayString();
        if (!string.Equals(returnTypeSymbol.Name, "ValueTask", StringComparison.Ordinal) ||
            !string.Equals(returnTypeNamespace, "System.Threading.Tasks", StringComparison.Ordinal))
            return false;

        if (returnTypeSymbol.TypeArguments.Length == 0)
        {
            isVoid = true;
            return true;
        }

        if (returnTypeSymbol.TypeArguments.Length != 1)
            return false;

        returnType = TryGetValueTaskGenericType(returnTypeSyntax);
        return !string.IsNullOrWhiteSpace(returnType);
    }

    private static string GetTypeFullName(INamedTypeSymbol symbol) =>
        symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);

    private static void AddTypeNamespaces(ISet<string> target, ITypeSymbol? typeSymbol)
    {
        if (typeSymbol == null) return;

        switch (typeSymbol)
        {
            case IArrayTypeSymbol arrayType:
                AddTypeNamespaces(target, arrayType.ElementType);
                return;
            case IPointerTypeSymbol pointerType:
                AddTypeNamespaces(target, pointerType.PointedAtType);
                return;
            case INamedTypeSymbol namedType:
                AddNamespace(target, namedType.ContainingNamespace);
                foreach (var typeArgument in namedType.TypeArguments)
                    AddTypeNamespaces(target, typeArgument);
                if (namedType.IsTupleType)
                    foreach (var tupleElement in namedType.TupleElements)
                        AddTypeNamespaces(target, tupleElement.Type);
                return;
        }
    }

    private static void AddNamespace(ISet<string> target, INamespaceSymbol? namespaceSymbol)
    {
        if (namespaceSymbol == null || namespaceSymbol.IsGlobalNamespace) return;
        var ns = namespaceSymbol.ToDisplayString();
        if (!string.IsNullOrWhiteSpace(ns))
            target.Add(ns);
    }

    private static string? TryGetValueTaskGenericType(TypeSyntax returnType)
    {
        if (returnType is GenericNameSyntax generic &&
            string.Equals(generic.Identifier.ValueText, "ValueTask", StringComparison.Ordinal) &&
            generic.TypeArgumentList.Arguments.Count == 1)
            return generic.TypeArgumentList.Arguments[0].ToString();

        if (returnType is QualifiedNameSyntax qualified)
            return TryGetValueTaskGenericType(qualified.Right);

        if (returnType is AliasQualifiedNameSyntax aliasQualified)
            return TryGetValueTaskGenericType(aliasQualified.Name);

        return null;
    }

    #endregion
}
