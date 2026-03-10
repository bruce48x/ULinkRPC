using System.Text;
using System.Text.RegularExpressions;

namespace ULinkRPC.CodeGen;

internal static class NamingHelper
{
    private const string DefaultFallbackServerNamespace = "ULinkRPC.Server.Generated";
    private const string ContractsNamespaceSuffix = ".Contracts";

    public static string GetServiceTypeName(string ifaceName)
    {
        if (ifaceName.Length > 1 && ifaceName[0] == 'I' && char.IsUpper(ifaceName[1]))
            return ifaceName[1..];
        return ifaceName;
    }

    public static string GetClientTypeName(string ifaceName) =>
        $"{GetServiceTypeName(ifaceName)}Client";

    public static string GetBinderTypeName(string ifaceName) =>
        $"{GetServiceTypeName(ifaceName)}Binder";

    public static string GetCallbackProxyTypeName(string callbackInterfaceName) =>
        $"{GetServiceTypeName(callbackInterfaceName)}Proxy";

    public static string GetCallbackBinderTypeName(string callbackInterfaceName) =>
        $"{GetServiceTypeName(callbackInterfaceName)}Binder";

    public static string GetClientExtensionTypeName(string ifaceName) =>
        $"{GetServiceTypeName(ifaceName)}ClientExtensions";

    public static string GetClientFactoryMethodName(string ifaceName) =>
        $"Create{GetServiceTypeName(ifaceName)}";

    public static string GetClientConnectionTypeName() => "RpcConnection";

    public static string GetCallbackReceiverParamName(string callbackInterfaceName) =>
        ToCamelCase(GetServiceTypeName(callbackInterfaceName));

    public static string GetClientMethodFieldName(string methodName) =>
        $"{ToCamelCase(methodName)}RpcMethod";

    public static string GetCallbackMethodFieldName(string methodName) =>
        $"{ToCamelCase(methodName)}PushMethod";

    public static string GetHandlerParameterName(string methodName) =>
        $"{ToCamelCase(methodName)}Handler";

    public static string GetHandlerFieldName(string methodName) =>
        $"_{ToCamelCase(methodName)}Handler";

    public static string GetServiceParamName(string ifaceName)
    {
        var baseName = GetServiceTypeName(ifaceName);
        return baseName.Length == 0 ? "service" : char.ToLowerInvariant(baseName[0]) + baseName[1..];
    }

    public static string GetServiceFactoryParamName(string ifaceName) =>
        $"{GetServiceParamName(ifaceName)}Factory";

    public static string ToCamelCase(string value) =>
        string.IsNullOrEmpty(value) ? "method" : char.ToLowerInvariant(value[0]) + value[1..];

    public static string GetMethodParameterSignature(IReadOnlyList<RpcParameterInfo> parameters) =>
        parameters.Count == 0
            ? string.Empty
            : string.Join(", ", parameters.Select(p => $"{p.TypeName} {p.Name}"));

    public static string GetInterfaceReturnType(RpcMethodInfo method) =>
        method.IsVoid ? "ValueTask" : $"ValueTask<{method.RetTypeName}>";

    public static string GetDelegateType(RpcMethodInfo method)
    {
        var genericArgs = method.Parameters
            .Select(p => p.TypeName)
            .Append(GetInterfaceReturnType(method));
        return $"Func<{string.Join(", ", genericArgs)}>";
    }

    #region Payload types (unified)

    public static string GetRequestPayloadType(RpcMethodInfo method) =>
        GetPayloadType(method.Parameters);

    public static string GetRequestPayloadValue(RpcMethodInfo method) =>
        GetPayloadValue(method.Parameters, "default");

    public static string GetCallbackPayloadType(RpcCallbackMethodInfo method) =>
        GetPayloadType(method.Parameters);

    public static string GetCallbackPayloadValue(RpcCallbackMethodInfo method) =>
        GetPayloadValue(method.Parameters, "default!");

    private static string GetPayloadType(IReadOnlyList<RpcParameterInfo> parameters) =>
        parameters.Count switch
        {
            0 => "RpcVoid",
            1 => parameters[0].TypeName,
            _ => $"({string.Join(", ", parameters.Select(p => p.TypeName))})"
        };

    private static string GetPayloadValue(IReadOnlyList<RpcParameterInfo> parameters, string zeroParamDefault) =>
        parameters.Count switch
        {
            0 => zeroParamDefault,
            1 => parameters[0].Name,
            _ => $"({string.Join(", ", parameters.Select(p => p.Name))})"
        };

    #endregion

    #region Arguments

    public static string GetDeconstructVariableList(int parameterCount) =>
        string.Join(", ", Enumerable.Range(1, parameterCount).Select(i => $"arg{i}"));

    public static string GetInvokeArguments(int parameterCount) =>
        parameterCount == 0
            ? string.Empty
            : string.Join(", ", Enumerable.Range(1, parameterCount).Select(i => $"arg{i}"));

    public static string GetForwardArguments(IReadOnlyList<RpcParameterInfo> parameters, bool includeCt)
    {
        var args = parameters.Select(p => p.Name).ToList();
        args.Add(includeCt ? "ct" : "CancellationToken.None");
        return string.Join(", ", args);
    }

    #endregion

    #region Using directives

    public static string GetNamespaceFromFullName(string fullName)
    {
        var lastDot = fullName.LastIndexOf('.');
        return lastDot > 0 ? fullName[..lastDot] : string.Empty;
    }

    public static IReadOnlyList<string> GetContractUsingDirectives(RpcServiceInfo svc) =>
        svc.UsingDirectives.Distinct(StringComparer.Ordinal).ToList();

    public static IReadOnlyList<string> GetContractUsingDirectives(IEnumerable<RpcServiceInfo> services) =>
        services.SelectMany(s => s.UsingDirectives).Distinct(StringComparer.Ordinal).ToList();

    public static IReadOnlyList<string> ExcludeUsingDirectives(
        IEnumerable<string> usingDirectives, params string[] excluded)
    {
        var excludedSet = new HashSet<string>(excluded, StringComparer.Ordinal);
        return usingDirectives.Where(d => !excludedSet.Contains(d)).ToList();
    }

    #endregion

    #region Namespace defaults

    public static string GetDefaultServerNamespace(List<RpcServiceInfo> services)
    {
        var first = services.FirstOrDefault();
        if (first == null)
            return DefaultFallbackServerNamespace;

        var baseNs = GetNamespaceFromFullName(first.InterfaceFullName);
        if (baseNs.EndsWith(ContractsNamespaceSuffix, StringComparison.Ordinal))
            baseNs = baseNs[..^ContractsNamespaceSuffix.Length];

        return string.IsNullOrWhiteSpace(baseNs)
            ? DefaultFallbackServerNamespace
            : $"{baseNs}.Server.Generated";
    }

    #endregion

    #region Identifier conversion

    public static string ToPascalIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "Default";

        var parts = Regex.Split(value, "[^A-Za-z0-9]+")
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToList();

        if (parts.Count == 0)
            return "Default";

        var sb = new StringBuilder();
        foreach (var part in parts)
        {
            var token = part.Trim();
            if (token.Length == 0) continue;
            sb.Append(char.ToUpperInvariant(token[0]));
            if (token.Length > 1)
                sb.Append(token[1..]);
        }

        if (sb.Length == 0) return "Default";
        if (char.IsDigit(sb[0])) sb.Insert(0, '_');
        return sb.ToString();
    }

    #endregion
}
