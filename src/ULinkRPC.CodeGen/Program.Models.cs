namespace ULinkRPC.CodeGen;

internal static partial class Program
{
    private sealed class RpcServiceInfo
    {
        public string InterfaceName { get; }
        public string InterfaceFullName { get; }
        public int ServiceId { get; }
        public List<RpcMethodInfo> Methods { get; }
        public List<string> UsingDirectives { get; }
        public string? CallbackInterfaceName { get; set; }
        public string? CallbackInterfaceFullName { get; set; }
        public List<RpcCallbackMethodInfo> CallbackMethods { get; set; } = new();

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
            UsingDirectives = BuildUsingDirectives(interfaceFullName, usingDirectives);
        }

        public void AddUsingDirectives(IEnumerable<string> usingDirectives)
        {
            foreach (var directive in usingDirectives)
            {
                if (!UsingDirectives.Contains(directive, StringComparer.Ordinal))
                    UsingDirectives.Add(directive);
            }
        }

        public bool HasCallback => !string.IsNullOrEmpty(CallbackInterfaceName) && CallbackMethods.Count > 0;

        private static List<string> BuildUsingDirectives(string interfaceFullName, IEnumerable<string> usingDirectives)
        {
            var allUsings = new List<string>();
            foreach (var directive in usingDirectives)
            {
                if (!allUsings.Contains(directive, StringComparer.Ordinal))
                    allUsings.Add(directive);
            }

            var contractNamespace = GetNamespaceFromFullName(interfaceFullName);
            if (!string.IsNullOrWhiteSpace(contractNamespace) &&
                !allUsings.Contains(contractNamespace, StringComparer.Ordinal))
            {
                allUsings.Add(contractNamespace);
            }

            return allUsings;
        }
    }

    private sealed class SourceFileInfo
    {
        public IReadOnlyList<RpcServiceInfo> Services { get; }
        public IReadOnlyDictionary<string, CallbackInterfaceInfo> CallbackInterfaces { get; }

        public SourceFileInfo(
            IReadOnlyList<RpcServiceInfo> services,
            IReadOnlyDictionary<string, CallbackInterfaceInfo> callbackInterfaces)
        {
            Services = services;
            CallbackInterfaces = callbackInterfaces;
        }
    }

    private sealed class CallbackInterfaceInfo
    {
        public string Name { get; }
        public string FullName { get; }
        public IReadOnlyList<string> RequiredNamespaces { get; }
        public List<RpcCallbackMethodInfo> Methods { get; }

        public CallbackInterfaceInfo(
            string name,
            string fullName,
            List<RpcCallbackMethodInfo> methods,
            IReadOnlyList<string> requiredNamespaces)
        {
            Name = name;
            FullName = fullName;
            Methods = methods;
            RequiredNamespaces = requiredNamespaces;
        }
    }

    private sealed class RpcMethodInfo
    {
        public string Name { get; }
        public int MethodId { get; }
        public List<RpcParameterInfo> Parameters { get; }
        public string? RetTypeName { get; }
        public bool IsVoid { get; }

        public RpcMethodInfo(string name, int methodId, List<RpcParameterInfo> parameters, string? retTypeName, bool isVoid)
        {
            Name = name;
            MethodId = methodId;
            Parameters = parameters;
            RetTypeName = retTypeName;
            IsVoid = isVoid;
        }
    }

    private sealed class RpcCallbackMethodInfo
    {
        public string Name { get; }
        public int MethodId { get; }
        public List<RpcParameterInfo> Parameters { get; }

        public RpcCallbackMethodInfo(string name, int methodId, List<RpcParameterInfo> parameters)
        {
            Name = name;
            MethodId = methodId;
            Parameters = parameters;
        }
    }

    private sealed class RpcParameterInfo
    {
        public string TypeName { get; }
        public string Name { get; }

        public RpcParameterInfo(string typeName, string name)
        {
            TypeName = typeName;
            Name = name;
        }
    }

    private sealed class FacadeGroupInfo
    {
        public string GroupName { get; }
        public List<FacadeMemberInfo> Members { get; }

        public FacadeGroupInfo(string groupName, List<FacadeMemberInfo> members)
        {
            GroupName = groupName;
            Members = members;
        }
    }

    private sealed class FacadeMemberInfo
    {
        public RpcServiceInfo Service { get; }
        public string PropertyName { get; }

        public FacadeMemberInfo(RpcServiceInfo service, string propertyName)
        {
            Service = service;
            PropertyName = propertyName;
        }
    }

    private sealed record RawOptions(
        string ContractsPath,
        string OutputPath,
        string UnityNamespace,
        string ServerOutputPath,
        string ServerNamespace,
        OutputMode Mode)
    {
        public static RawOptions Empty { get; } = new(
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            OutputMode.Unknown);
    }

    private sealed record ResolvedOptions(
        string ContractsPath,
        string OutputPath,
        string UnityNamespace,
        string ServerOutputPath,
        string ServerNamespace,
        OutputMode Mode)
    {
        public static ResolvedOptions Empty { get; } = new(
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            OutputMode.Unknown);
    }

    private enum OutputMode
    {
        Unknown,
        Unity,
        Server
    }
}
