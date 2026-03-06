namespace ULinkRPC.CodeGen;

internal sealed class RpcServiceInfo
{
    public string InterfaceName { get; }
    public string InterfaceFullName { get; }
    public int ServiceId { get; }
    public List<RpcMethodInfo> Methods { get; }
    public List<string> UsingDirectives { get; }
    public string? CallbackInterfaceName { get; init; }
    public string? CallbackInterfaceFullName { get; init; }
    public List<RpcCallbackMethodInfo> CallbackMethods { get; set; } = [];

    private readonly HashSet<string> _usingSet;

    public bool HasCallback => !string.IsNullOrEmpty(CallbackInterfaceName) && CallbackMethods.Count > 0;

    public RpcServiceInfo(
        string interfaceName,
        string interfaceFullName,
        int serviceId,
        List<RpcMethodInfo> methods,
        IReadOnlyList<string> usingDirectives)
    {
        InterfaceName = interfaceName;
        InterfaceFullName = interfaceFullName;
        ServiceId = serviceId;
        Methods = methods;
        _usingSet = new HashSet<string>(usingDirectives, StringComparer.Ordinal);
        UsingDirectives = new List<string>(usingDirectives);

        var lastDot = interfaceFullName.LastIndexOf('.');
        var contractNs = lastDot > 0 ? interfaceFullName[..lastDot] : string.Empty;
        if (!string.IsNullOrWhiteSpace(contractNs))
            AddUsingDirective(contractNs);
    }

    public void AddUsingDirectives(IEnumerable<string> directives)
    {
        foreach (var d in directives)
            AddUsingDirective(d);
    }

    private void AddUsingDirective(string directive)
    {
        if (_usingSet.Add(directive))
            UsingDirectives.Add(directive);
    }
}

internal sealed class SourceFileInfo(
    IReadOnlyList<RpcServiceInfo> services,
    IReadOnlyDictionary<string, CallbackInterfaceInfo> callbackInterfaces)
{
    public IReadOnlyList<RpcServiceInfo> Services { get; } = services;
    public IReadOnlyDictionary<string, CallbackInterfaceInfo> CallbackInterfaces { get; } = callbackInterfaces;
}

internal sealed class CallbackInterfaceInfo(
    string name,
    string fullName,
    List<RpcCallbackMethodInfo> methods,
    IReadOnlyList<string> requiredNamespaces)
{
    public string Name { get; } = name;
    public string FullName { get; } = fullName;
    public List<RpcCallbackMethodInfo> Methods { get; } = methods;
    public IReadOnlyList<string> RequiredNamespaces { get; } = requiredNamespaces;
}

internal sealed class RpcMethodInfo(
    string name,
    int methodId,
    List<RpcParameterInfo> parameters,
    string? retTypeName,
    bool isVoid)
{
    public string Name { get; } = name;
    public int MethodId { get; } = methodId;
    public List<RpcParameterInfo> Parameters { get; } = parameters;
    public string? RetTypeName { get; } = retTypeName;
    public bool IsVoid { get; } = isVoid;
}

internal sealed class RpcCallbackMethodInfo(
    string name,
    int methodId,
    List<RpcParameterInfo> parameters)
{
    public string Name { get; } = name;
    public int MethodId { get; } = methodId;
    public List<RpcParameterInfo> Parameters { get; } = parameters;
}

internal sealed class RpcParameterInfo(string typeName, string name)
{
    public string TypeName { get; } = typeName;
    public string Name { get; } = name;
}

internal sealed class FacadeGroupInfo(string groupName, List<FacadeMemberInfo> members)
{
    public string GroupName { get; } = groupName;
    public List<FacadeMemberInfo> Members { get; } = members;
}

internal sealed class FacadeMemberInfo(RpcServiceInfo service, string propertyName)
{
    public RpcServiceInfo Service { get; } = service;
    public string PropertyName { get; } = propertyName;
}

internal sealed record RawOptions(
    string ContractsPath,
    string OutputPath,
    string UnityNamespace,
    string ServerOutputPath,
    string ServerNamespace,
    OutputMode Mode)
{
    public static RawOptions Empty { get; } = new(
        string.Empty, string.Empty, string.Empty,
        string.Empty, string.Empty, OutputMode.Unknown);
}

internal sealed record ResolvedOptions(
    string ContractsPath,
    string OutputPath,
    string UnityNamespace,
    string ServerOutputPath,
    string ServerNamespace,
    OutputMode Mode)
{
    public static ResolvedOptions Empty { get; } = new(
        string.Empty, string.Empty, string.Empty,
        string.Empty, string.Empty, OutputMode.Unknown);
}

internal enum OutputMode
{
    Unknown,
    Unity,
    Server
}
