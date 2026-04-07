using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ULinkRPC.CodeGen;

internal static partial class ContractParser
{
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
                    BuildDtoTypeErrorMessage(
                        "RPC request parameter",
                        methodSymbol.Parameters[0].Name,
                        methodSymbol.Parameters[0].Type)));
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
                    BuildDtoTypeErrorMessage(
                        "RPC response type",
                        null,
                        resultType)));
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
                    BuildDtoTypeErrorMessage(
                        "RPC push parameter",
                        methodSymbol.Parameters[0].Name,
                        methodSymbol.Parameters[0].Type)));
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

    private static string BuildInterfaceContractError(
        InterfaceDeclarationSyntax iface,
        string reason)
    {
        var location = iface.Identifier.GetLocation().GetLineSpan();
        var line = location.StartLinePosition.Line + 1;
        return $"{reason} Offending contract: {iface.Identifier.ValueText} in {location.Path}:{line}.";
    }

    private static string BuildDtoTypeErrorMessage(string subject, string? memberName, ITypeSymbol type)
    {
        var displayName = type.ToDisplayString();
        var subjectText = memberName is null
            ? subject
            : $"{subject} '{memberName}'";

        if (IsCollectionLikeRootType(type))
        {
            return $"{subjectText} must be a DTO object type, not '{displayName}'. Collection-like payload roots are not allowed because they are hard to evolve compatibly; wrap the collection in a DTO and add the list as a property.";
        }

        return $"{subjectText} must be a DTO object type, not '{displayName}'. Use a user-defined DTO class/record as the payload root.";
    }

    private static bool IsCollectionLikeRootType(ITypeSymbol type)
    {
        if (type is IArrayTypeSymbol)
            return true;

        if (type is not INamedTypeSymbol namedType)
            return false;

        if (namedType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
            return IsCollectionLikeRootType(namedType.TypeArguments[0]);

        if (namedType.SpecialType == SpecialType.System_String)
            return false;

        var namespaceName = namedType.ContainingNamespace?.ToDisplayString() ?? string.Empty;
        if (!namespaceName.StartsWith("System.Collections", StringComparison.Ordinal))
            return false;

        return namedType.AllInterfaces.Any(i =>
            i.OriginalDefinition.SpecialType == SpecialType.System_Collections_IEnumerable ||
            i.ToDisplayString() == "System.Collections.Generic.IEnumerable<T>");
    }
}
