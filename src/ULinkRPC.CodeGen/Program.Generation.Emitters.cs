using System.Text;

namespace ULinkRPC.CodeGen;

internal static partial class Program
{
    private static (string? Client, string? Binder) GenerateCode(
        RpcServiceInfo svc,
        string runtimeNamespace,
        string clientRuntimeUsing,
        string serverRuntimeUsing)
    {
        var ifaceName = svc.InterfaceName;
        var clientTypeName = GetClientTypeName(ifaceName);
        var contractUsings = ExcludeUsingDirectives(
            GetContractUsingDirectives(svc),
            "System",
            "System.Threading",
            "System.Threading.Tasks",
            clientRuntimeUsing);

        var clientBody = new StringBuilder();
        clientBody.Append("using System;\nusing System.Threading;\nusing System.Threading.Tasks;\n")
            .Append(FormatUsingBlock(contractUsings))
            .Append("using ")
            .Append(clientRuntimeUsing)
            .Append(";\n\nnamespace ")
            .Append(runtimeNamespace)
            .Append("\n{\n");
        clientBody.Append("    public sealed class ").Append(clientTypeName).Append(" : ").Append(ifaceName).Append("\n    {\n");
        clientBody.Append("        private const int ServiceId = ").Append(svc.ServiceId).Append(";\n");
        foreach (var m in svc.Methods)
        {
            var argType = GetRequestPayloadType(m);
            var retType = m.IsVoid ? "RpcVoid" : m.RetTypeName!;
            clientBody.Append("        private static readonly RpcMethod<").Append(argType).Append(", ")
                .Append(retType).Append("> ").Append(GetClientMethodFieldName(m.Name))
                .Append(" = new(ServiceId, ").Append(m.MethodId).Append(");\n");
        }
        clientBody.Append('\n');
        clientBody.Append("        private readonly IRpcClient _client;\n\n");
        clientBody.Append("        public ").Append(clientTypeName).Append("(IRpcClient client) { _client = client; }\n\n");

        foreach (var m in svc.Methods)
        {
            var retType = m.IsVoid ? "RpcVoid" : m.RetTypeName!;
            var argVal = GetRequestPayloadValue(m);
            var methodParamSig = GetMethodParameterSignature(m);
            var sig = $"{m.Name}({methodParamSig})";
            var sigWithCt = string.IsNullOrEmpty(methodParamSig)
                ? $"{m.Name}(CancellationToken ct)"
                : $"{m.Name}({methodParamSig}, CancellationToken ct)";
            if (m.IsVoid)
            {
                clientBody.Append("        public async ValueTask ").Append(sig).Append("\n        {\n");
                clientBody.Append("            await ").Append(m.Name).Append("(")
                    .Append(GetForwardArguments(m.Parameters, includeCt: false)).Append(");\n        }\n\n");
                clientBody.Append("        public async ValueTask ").Append(sigWithCt).Append("\n        {\n");
                clientBody.Append("            await _client.CallAsync(").Append(GetClientMethodFieldName(m.Name))
                    .Append(", ").Append(argVal).Append(", ct);\n        }\n\n");
            }
            else
            {
                clientBody.Append("        public ValueTask<").Append(retType).Append("> ").Append(sig).Append("\n        {\n");
                clientBody.Append("            return ").Append(m.Name).Append("(")
                    .Append(GetForwardArguments(m.Parameters, includeCt: false)).Append(");\n        }\n\n");
                clientBody.Append("        public ValueTask<").Append(retType).Append("> ").Append(sigWithCt).Append("\n        {\n");
                clientBody.Append("            return _client.CallAsync(").Append(GetClientMethodFieldName(m.Name))
                    .Append(", ").Append(argVal).Append(", ct);\n        }\n\n");
            }
        }
        clientBody.Append("    }\n\n");
        clientBody.Append("    public static class ").Append(GetClientExtensionTypeName(ifaceName)).Append("\n    {\n");
        clientBody.Append("        public static ").Append(ifaceName).Append(" ").Append(GetClientFactoryMethodName(ifaceName))
            .Append("(this IRpcClient client)\n        {\n");
        clientBody.Append("            if (client is null) throw new ArgumentNullException(nameof(client));\n");
        clientBody.Append("            return new ").Append(clientTypeName).Append("(client);\n");
        clientBody.Append("        }\n");
        clientBody.Append("    }\n}\n");

        return (clientBody.ToString(), GenerateBinderCode(svc, runtimeNamespace, DefaultCoreRuntimeUsing, serverRuntimeUsing));
    }

    private static string GenerateBinderCode(RpcServiceInfo svc, string ns, string coreRuntimeUsing, string serverRuntimeUsing)
    {
        var ifaceName = svc.InterfaceName;
        var binderTypeName = GetBinderTypeName(ifaceName);
        var contractUsings = ExcludeUsingDirectives(
            GetContractUsingDirectives(svc),
            "System",
            "System.Threading.Tasks",
            coreRuntimeUsing,
            serverRuntimeUsing);
        var binderSb = new StringBuilder();
        binderSb.Append("using System;\nusing System.Threading.Tasks;\n")
            .Append(FormatUsingBlock(contractUsings))
            .Append("using ")
            .Append(coreRuntimeUsing)
            .Append(";\nusing ")
            .Append(serverRuntimeUsing)
            .Append(";\n\nnamespace ")
            .Append(ns)
            .Append("\n{\n");
        binderSb.Append("    public static class ").Append(binderTypeName).Append("\n    {\n");
        binderSb.Append("        private const int ServiceId = ").Append(svc.ServiceId).Append(";\n\n");
        binderSb.Append(GenerateDelegateBindOverload(svc));
        if (svc.HasCallback)
            binderSb.Append(GenerateCallbackFactoryBindOverload(svc));
        binderSb.Append("        public static void Bind(RpcServer server, ").Append(ifaceName).Append(" impl)\n        {\n");

        foreach (var m in svc.Methods)
        {
            var argType = GetRequestPayloadType(m);
            binderSb.Append("            server.Register(ServiceId, ").Append(m.MethodId).Append(", async (req, ct) =>\n            {\n");
            if (m.Parameters.Count == 1)
            {
                binderSb.Append("                var arg1 = server.Serializer.Deserialize<").Append(argType).Append(">(req.Payload.AsSpan())!;\n");
            }
            else if (m.Parameters.Count > 1)
            {
                binderSb.Append("                var (").Append(GetDeconstructVariableList(m.Parameters.Count)).Append(") = server.Serializer.Deserialize<")
                    .Append(argType).Append(">(req.Payload.AsSpan())!;\n");
            }
            if (m.IsVoid)
            {
                binderSb.Append("                await impl.").Append(m.Name).Append("(").Append(GetInvokeArguments(m.Parameters.Count)).Append(");\n");
                binderSb.Append("                return new RpcResponseEnvelope { RequestId = req.RequestId, Status = RpcStatus.Ok, Payload = Array.Empty<byte>() };\n");
            }
            else
            {
                binderSb.Append("                var resp = await impl.").Append(m.Name).Append("(").Append(GetInvokeArguments(m.Parameters.Count)).Append(");\n");
                binderSb.Append("                return new RpcResponseEnvelope { RequestId = req.RequestId, Status = RpcStatus.Ok, Payload = server.Serializer.Serialize(resp) };\n");
            }
            binderSb.Append("            });\n\n");
        }
        binderSb.Append("        }\n    }\n}\n");

        return binderSb.ToString();
    }

    private static string GenerateAllServicesBinder(List<RpcServiceInfo> services, string ns, string runtimeUsing)
    {
        var contractUsings = ExcludeUsingDirectives(GetContractUsingDirectives(services), runtimeUsing);
        var sb = new StringBuilder();
        sb.Append(FormatUsingBlock(contractUsings))
            .Append("using ")
            .Append(runtimeUsing)
            .Append(";\n\nnamespace ")
            .Append(ns)
            .Append("\n{\n");
        sb.Append("    public static class AllServicesBinder\n    {\n");
        sb.Append("        public static void BindAll(RpcServer server");
        foreach (var svc in services)
            sb.Append(", ").Append(svc.InterfaceName).Append(" ").Append(GetServiceParamName(svc.InterfaceName));

        sb.Append(")\n        {\n");
        foreach (var svc in services)
        {
            sb.Append("            ").Append(GetBinderTypeName(svc.InterfaceName))
                .Append(".Bind(server, ").Append(GetServiceParamName(svc.InterfaceName)).Append(");\n");
        }
        sb.Append("        }\n    }\n}\n");
        return sb.ToString();
    }

    private static string GenerateCallbackProxyCode(RpcServiceInfo svc, string ns, string coreRuntimeUsing, string serverRuntimeUsing)
    {
        var cbName = svc.CallbackInterfaceName!;
        var proxyTypeName = GetCallbackProxyTypeName(cbName);
        var contractUsings = ExcludeUsingDirectives(
            GetContractUsingDirectives(svc),
            "System",
            coreRuntimeUsing,
            serverRuntimeUsing);
        var sb = new StringBuilder();
        sb.Append("using System;\n")
            .Append(FormatUsingBlock(contractUsings))
            .Append("using ").Append(coreRuntimeUsing).Append(";\n")
            .Append("using ").Append(serverRuntimeUsing).Append(";\n\n")
            .Append("namespace ").Append(ns).Append("\n{\n");
        sb.Append("    public sealed class ").Append(proxyTypeName).Append(" : ").Append(cbName).Append("\n    {\n");
        sb.Append("        private const int ServiceId = ").Append(svc.ServiceId).Append(";\n");
        sb.Append("        private readonly RpcServer _server;\n\n");
        sb.Append("        public ").Append(proxyTypeName).Append("(RpcServer server) { _server = server; }\n\n");

        foreach (var m in svc.CallbackMethods)
        {
            var paramSig = GetMethodParameterSignature(m.Parameters);
            var argType = GetCallbackPayloadType(m);
            var argVal = GetCallbackPayloadValue(m);
            sb.Append("        public void ").Append(m.Name).Append("(").Append(paramSig).Append(")\n        {\n");
            sb.Append("            _server.PushAsync<").Append(argType).Append(">(ServiceId, ").Append(m.MethodId)
                .Append(", ").Append(argVal).Append(").AsTask().Wait();\n");
            sb.Append("        }\n\n");
        }

        sb.Append("    }\n}\n");
        return sb.ToString();
    }

    private static string GenerateCallbackBinderCode(RpcServiceInfo svc, string ns, string coreRuntimeUsing, string clientRuntimeUsing)
    {
        var cbName = svc.CallbackInterfaceName!;
        var binderTypeName = GetCallbackBinderTypeName(cbName);
        var contractUsings = ExcludeUsingDirectives(GetContractUsingDirectives(svc), "System", coreRuntimeUsing);
        var sb = new StringBuilder();
        sb.Append("using System;\n")
            .Append(FormatUsingBlock(contractUsings))
            .Append("using ").Append(coreRuntimeUsing).Append(";\n\n")
            .Append("namespace ").Append(ns).Append("\n{\n");
        sb.Append("    public static class ").Append(binderTypeName).Append("\n    {\n");
        sb.Append("        private const int ServiceId = ").Append(svc.ServiceId).Append(";\n\n");
        foreach (var m in svc.CallbackMethods)
        {
            var argType = GetCallbackPayloadType(m);
            sb.Append("        private static readonly RpcPushMethod<").Append(argType).Append("> ")
                .Append(GetCallbackMethodFieldName(m.Name)).Append(" = new(ServiceId, ").Append(m.MethodId).Append(");\n");
        }
        sb.Append('\n');
        sb.Append("        public static void Bind(IRpcClient client, ").Append(cbName)
            .Append(" receiver)\n        {\n");

        foreach (var m in svc.CallbackMethods)
        {
            sb.Append("            client.RegisterPushHandler(").Append(GetCallbackMethodFieldName(m.Name))
                .Append(", (arg) =>\n            {\n");
            if (m.Parameters.Count == 1)
            {
                sb.Append("                receiver.").Append(m.Name).Append("(arg);\n");
            }
            else if (m.Parameters.Count > 1)
            {
                sb.Append("                var (").Append(GetDeconstructVariableList(m.Parameters.Count))
                    .Append(") = arg;\n");
                sb.Append("                receiver.").Append(m.Name).Append("(")
                    .Append(GetInvokeArguments(m.Parameters.Count)).Append(");\n");
            }
            else
            {
                sb.Append("                receiver.").Append(m.Name).Append("();\n");
            }
            sb.Append("            });\n\n");
        }

        sb.Append("        }\n    }\n}\n");
        return sb.ToString();
    }

    private static string GenerateDelegateBindOverload(RpcServiceInfo svc)
    {
        var sb = new StringBuilder();
        var delegateParameters = svc.Methods
            .Select(m => $"{GetDelegateType(m)} {GetHandlerParameterName(m.Name)}")
            .ToList();

        sb.Append("        public static void Bind(RpcServer server");
        if (delegateParameters.Count > 0)
            sb.Append(", ").Append(string.Join(", ", delegateParameters));

        sb.Append(")\n        {\n");
        sb.Append("            Bind(server, new DelegateImpl(")
            .Append(string.Join(", ", svc.Methods.Select(m => GetHandlerParameterName(m.Name))))
            .Append("));\n");
        sb.Append("        }\n\n");
        sb.Append("        private sealed class DelegateImpl : ").Append(svc.InterfaceName).Append("\n        {\n");

        foreach (var method in svc.Methods)
        {
            sb.Append("            private readonly ")
                .Append(GetDelegateType(method))
                .Append(" ")
                .Append(GetHandlerFieldName(method.Name))
                .Append(";\n");
        }

        if (svc.Methods.Count > 0)
            sb.Append('\n');

        sb.Append("            public DelegateImpl(")
            .Append(string.Join(", ", svc.Methods.Select(m => $"{GetDelegateType(m)} {GetHandlerParameterName(m.Name)}")))
            .Append(")\n            {\n");

        foreach (var method in svc.Methods)
        {
            var handlerParam = GetHandlerParameterName(method.Name);
            var handlerField = GetHandlerFieldName(method.Name);
            sb.Append("                ")
                .Append(handlerField)
                .Append(" = ")
                .Append(handlerParam)
                .Append(" ?? throw new ArgumentNullException(nameof(")
                .Append(handlerParam)
                .Append("));\n");
        }
        sb.Append("            }\n\n");

        foreach (var method in svc.Methods)
        {
            var methodSig = $"{method.Name}({GetMethodParameterSignature(method)})";
            var invokeArgs = string.Join(", ", method.Parameters.Select(p => p.Name));
            sb.Append("            public ")
                .Append(GetInterfaceReturnType(method))
                .Append(" ")
                .Append(methodSig)
                .Append("\n            {\n");
            sb.Append("                return ")
                .Append(GetHandlerFieldName(method.Name))
                .Append("(")
                .Append(invokeArgs)
                .Append(");\n");
            sb.Append("            }\n\n");
        }

        sb.Append("        }\n\n");
        return sb.ToString();
    }

    private static string GenerateCallbackFactoryBindOverload(RpcServiceInfo svc)
    {
        var callbackInterfaceName = svc.CallbackInterfaceName!;
        var callbackProxyTypeName = GetCallbackProxyTypeName(callbackInterfaceName);
        var sb = new StringBuilder();
        sb.Append("        public static void Bind(RpcServer server, Func<")
            .Append(callbackInterfaceName)
            .Append(", ")
            .Append(svc.InterfaceName)
            .Append("> implFactory)\n        {\n");
        sb.Append("            if (implFactory is null) throw new ArgumentNullException(nameof(implFactory));\n");
        sb.Append("            var callback = new ")
            .Append(callbackProxyTypeName)
            .Append("(server);\n");
        sb.Append("            var impl = implFactory(callback) ?? throw new InvalidOperationException(\"Service implementation factory returned null.\");\n");
        sb.Append("            Bind(server, impl);\n");
        sb.Append("        }\n\n");
        return sb.ToString();
    }
}
