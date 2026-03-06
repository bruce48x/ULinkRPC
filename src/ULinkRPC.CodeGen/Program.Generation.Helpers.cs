namespace ULinkRPC.CodeGen;

internal static partial class Program
{
    private static string GetCallbackPayloadType(RpcCallbackMethodInfo method)
    {
        if (method.Parameters.Count == 0)
            return "RpcVoid";
        if (method.Parameters.Count == 1)
            return method.Parameters[0].TypeName;
        return $"({string.Join(", ", method.Parameters.Select(p => p.TypeName))})";
    }

    private static string GetCallbackPayloadValue(RpcCallbackMethodInfo method)
    {
        if (method.Parameters.Count == 0)
            return "default!";
        if (method.Parameters.Count == 1)
            return method.Parameters[0].Name;
        return $"({string.Join(", ", method.Parameters.Select(p => p.Name))})";
    }

    private static string GetCallbackProxyTypeName(string callbackInterfaceName)
    {
        return $"{GetServiceTypeName(callbackInterfaceName)}Proxy";
    }

    private static string GetCallbackBinderTypeName(string callbackInterfaceName)
    {
        return $"{GetServiceTypeName(callbackInterfaceName)}Binder";
    }

    private static string GetClientMethodFieldName(string methodName)
    {
        return $"{GetCamelCaseName(methodName)}RpcMethod";
    }

    private static string GetCallbackMethodFieldName(string methodName)
    {
        return $"{GetCamelCaseName(methodName)}PushMethod";
    }

    private static string GetClientExtensionTypeName(string ifaceName)
    {
        return $"{GetServiceTypeName(ifaceName)}ClientExtensions";
    }

    private static string GetClientFactoryMethodName(string ifaceName)
    {
        return $"Create{GetServiceTypeName(ifaceName)}";
    }

    private static string GetMethodParameterSignature(List<RpcParameterInfo> parameters)
    {
        if (parameters.Count == 0)
            return string.Empty;
        return string.Join(", ", parameters.Select(p => $"{p.TypeName} {p.Name}"));
    }

    private static string GetServiceParamName(string ifaceName)
    {
        var baseName = GetServiceTypeName(ifaceName);
        if (baseName.Length == 0)
            return "service";
        return char.ToLowerInvariant(baseName[0]) + baseName.Substring(1);
    }

    private static string GetMethodParameterSignature(RpcMethodInfo method)
    {
        if (method.Parameters.Count == 0)
            return string.Empty;

        return string.Join(", ", method.Parameters.Select(p => $"{p.TypeName} {p.Name}"));
    }

    private static string GetInterfaceReturnType(RpcMethodInfo method)
    {
        if (method.IsVoid)
            return "ValueTask";

        return $"ValueTask<{method.RetTypeName}>";
    }

    private static string GetDelegateType(RpcMethodInfo method)
    {
        var genericArgs = new List<string>();
        genericArgs.AddRange(method.Parameters.Select(p => p.TypeName));
        genericArgs.Add(GetInterfaceReturnType(method));
        return $"Func<{string.Join(", ", genericArgs)}>";
    }

    private static string GetHandlerParameterName(string methodName)
    {
        return $"{GetCamelCaseName(methodName)}Handler";
    }

    private static string GetHandlerFieldName(string methodName)
    {
        return $"_{GetCamelCaseName(methodName)}Handler";
    }

    private static string GetCamelCaseName(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "method";

        return char.ToLowerInvariant(value[0]) + value.Substring(1);
    }

    private static string GetRequestPayloadType(RpcMethodInfo method)
    {
        if (method.Parameters.Count == 0)
            return "RpcVoid";

        if (method.Parameters.Count == 1)
            return method.Parameters[0].TypeName;

        return $"({string.Join(", ", method.Parameters.Select(p => p.TypeName))})";
    }

    private static string GetRequestPayloadValue(RpcMethodInfo method)
    {
        if (method.Parameters.Count == 0)
            return "default";

        if (method.Parameters.Count == 1)
            return method.Parameters[0].Name;

        return $"({string.Join(", ", method.Parameters.Select(p => p.Name))})";
    }

    private static string GetDeconstructVariableList(int parameterCount)
    {
        return string.Join(", ", Enumerable.Range(1, parameterCount).Select(i => $"arg{i}"));
    }

    private static string GetInvokeArguments(int parameterCount)
    {
        if (parameterCount == 0)
            return string.Empty;

        return string.Join(", ", Enumerable.Range(1, parameterCount).Select(i => $"arg{i}"));
    }

    private static string GetForwardArguments(IReadOnlyList<RpcParameterInfo> parameters, bool includeCt)
    {
        var args = new List<string>();
        if (parameters.Count > 0)
            args.AddRange(parameters.Select(p => p.Name));

        args.Add(includeCt ? "ct" : "CancellationToken.None");
        return string.Join(", ", args);
    }

    private static IReadOnlyList<string> GetContractUsingDirectives(RpcServiceInfo svc)
    {
        return svc.UsingDirectives
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static IReadOnlyList<string> GetContractUsingDirectives(IEnumerable<RpcServiceInfo> services)
    {
        return services
            .SelectMany(svc => svc.UsingDirectives)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static string GetNamespaceFromFullName(string fullName)
    {
        var lastDot = fullName.LastIndexOf('.');
        return lastDot > 0 ? fullName.Substring(0, lastDot) : string.Empty;
    }

    private static string FormatUsingBlock(IEnumerable<string> namespaces)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var ns in namespaces)
            sb.Append("using ").Append(ns).Append(";\n");
        return sb.ToString();
    }

    private static IReadOnlyList<string> ExcludeUsingDirectives(IEnumerable<string> usingDirectives, params string[] excluded)
    {
        var excludedSet = new HashSet<string>(excluded, StringComparer.Ordinal);
        return usingDirectives
            .Where(directive => !excludedSet.Contains(directive))
            .ToList();
    }

    private static string GetDefaultServerNamespace(List<RpcServiceInfo> services)
    {
        var first = services.FirstOrDefault();
        if (first == null)
            return "ULinkRPC.Server.Generated";

        var ns = first.InterfaceFullName;
        var lastDot = ns.LastIndexOf('.');
        var baseNs = lastDot > 0 ? ns.Substring(0, lastDot) : ns;
        if (baseNs.EndsWith(".Contracts", StringComparison.Ordinal))
            baseNs = baseNs.Substring(0, baseNs.Length - ".Contracts".Length);

        if (string.IsNullOrWhiteSpace(baseNs))
            return "ULinkRPC.Server.Generated";

        return $"{baseNs}.Server.Generated";
    }

    private static string GetBinderTypeName(string ifaceName)
    {
        return $"{GetServiceTypeName(ifaceName)}Binder";
    }

    private static string GetClientTypeName(string ifaceName)
    {
        return $"{GetServiceTypeName(ifaceName)}Client";
    }

    private static string GetServiceTypeName(string ifaceName)
    {
        if (ifaceName.Length > 1 && ifaceName[0] == 'I' && char.IsUpper(ifaceName[1]))
            return ifaceName.Substring(1);

        return ifaceName;
    }
}
