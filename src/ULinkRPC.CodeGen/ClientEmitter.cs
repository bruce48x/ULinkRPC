namespace ULinkRPC.CodeGen;

internal static class ClientEmitter
{
    public static string GenerateClient(RpcServiceInfo svc, string ns, string coreRuntimeUsing)
    {
        var ifaceName = svc.InterfaceName;
        var clientTypeName = NamingHelper.GetClientTypeName(ifaceName);
        var contractUsings = NamingHelper.ExcludeUsingDirectives(
            NamingHelper.GetContractUsingDirectives(svc),
            "System", "System.Threading", "System.Threading.Tasks", coreRuntimeUsing);

        var w = new CodeWriter();
        w.WriteUsings("System", "System.Threading", "System.Threading.Tasks");
        w.WriteUsings(contractUsings);
        w.Line($"using {coreRuntimeUsing};");
        w.Line();
        w.OpenBlock($"namespace {ns}");

        w.OpenBlock($"public sealed class {clientTypeName} : {ifaceName}");
        w.Line($"private const int ServiceId = {svc.ServiceId};");

        foreach (var m in svc.Methods)
        {
            var argType = NamingHelper.GetRequestPayloadType(m);
            var retType = m.IsVoid ? "RpcVoid" : m.RetTypeName!;
            var fieldName = NamingHelper.GetClientMethodFieldName(m.Name);
            w.Line($"private static readonly RpcMethod<{argType}, {retType}> {fieldName} = new(ServiceId, {m.MethodId});");
        }

        w.Line();
        w.Line("private readonly IRpcClient _client;");
        w.Line();
        w.Line($"public {clientTypeName}(IRpcClient client) {{ _client = client; }}");
        w.Line();

        foreach (var m in svc.Methods)
        {
            var argVal = NamingHelper.GetRequestPayloadValue(m);
            var paramSig = NamingHelper.GetMethodParameterSignature(m.Parameters);
            var sig = $"{m.Name}({paramSig})";
            var sigWithCt = string.IsNullOrEmpty(paramSig)
                ? $"{m.Name}(CancellationToken ct)"
                : $"{m.Name}({paramSig}, CancellationToken ct)";
            var fwdArgs = NamingHelper.GetForwardArguments(m.Parameters, includeCt: false);
            var fieldName = NamingHelper.GetClientMethodFieldName(m.Name);

            if (m.IsVoid)
            {
                w.OpenBlock($"public async ValueTask {sig}");
                w.Line($"await {m.Name}({fwdArgs});");
                w.CloseBlock();
                w.Line();
                w.OpenBlock($"public async ValueTask {sigWithCt}");
                w.Line($"await _client.CallAsync({fieldName}, {argVal}, ct);");
                w.CloseBlock();
            }
            else
            {
                var retType = m.RetTypeName!;
                w.OpenBlock($"public ValueTask<{retType}> {sig}");
                w.Line($"return {m.Name}({fwdArgs});");
                w.CloseBlock();
                w.Line();
                w.OpenBlock($"public ValueTask<{retType}> {sigWithCt}");
                w.Line($"return _client.CallAsync({fieldName}, {argVal}, ct);");
                w.CloseBlock();
            }
            w.Line();
        }

        w.CloseBlock();
        w.Line();

        var extTypeName = NamingHelper.GetClientExtensionTypeName(ifaceName);
        var factoryName = NamingHelper.GetClientFactoryMethodName(ifaceName);
        w.OpenBlock($"public static class {extTypeName}");
        w.OpenBlock($"public static {ifaceName} {factoryName}(this IRpcClient client)");
        w.Line("if (client is null) throw new ArgumentNullException(nameof(client));");
        w.Line($"return new {clientTypeName}(client);");
        w.CloseBlock();
        w.CloseBlock();

        w.CloseBlock();
        return w.ToString();
    }

    public static string GenerateCallbackBinder(
        RpcServiceInfo svc, string ns, string coreRuntimeUsing)
    {
        var cbName = svc.CallbackInterfaceName!;
        var binderTypeName = NamingHelper.GetCallbackBinderTypeName(cbName);
        var contractUsings = NamingHelper.ExcludeUsingDirectives(
            NamingHelper.GetContractUsingDirectives(svc), "System", coreRuntimeUsing);

        var w = new CodeWriter();
        w.Line("using System;");
        w.WriteUsings(contractUsings);
        w.Line($"using {coreRuntimeUsing};");
        w.Line();
        w.OpenBlock($"namespace {ns}");

        w.OpenBlock($"public static class {binderTypeName}");
        w.Line($"private const int ServiceId = {svc.ServiceId};");
        w.Line();

        foreach (var m in svc.CallbackMethods)
        {
            var argType = NamingHelper.GetCallbackPayloadType(m);
            var fieldName = NamingHelper.GetCallbackMethodFieldName(m.Name);
            w.Line($"private static readonly RpcPushMethod<{argType}> {fieldName} = new(ServiceId, {m.MethodId});");
        }

        w.Line();
        w.OpenBlock($"public static void Bind(IRpcClient client, {cbName} receiver)");

        foreach (var m in svc.CallbackMethods)
        {
            var fieldName = NamingHelper.GetCallbackMethodFieldName(m.Name);
            w.OpenBlock($"client.RegisterPushHandler({fieldName}, (arg) =>");

            if (m.Parameters.Count == 1)
            {
                w.Line($"receiver.{m.Name}(arg);");
            }
            else if (m.Parameters.Count > 1)
            {
                var deconstructVars = NamingHelper.GetDeconstructVariableList(m.Parameters.Count);
                var invokeArgs = NamingHelper.GetInvokeArguments(m.Parameters.Count);
                w.Line($"var ({deconstructVars}) = arg;");
                w.Line($"receiver.{m.Name}({invokeArgs});");
            }
            else
            {
                w.Line($"receiver.{m.Name}();");
            }

            w.CloseBlock(");");
            w.Line();
        }

        w.CloseBlock();
        w.CloseBlock();
        w.CloseBlock();
        return w.ToString();
    }
}
