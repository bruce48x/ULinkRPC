namespace ULinkRPC.CodeGen;

internal static partial class FacadeEmitter
{
    private static void EmitCallbackTypes(
        CodeWriter w,
        IReadOnlyList<CallbackBindingInfo> callbacks,
        string generatedNamespace)
    {
        w.OpenBlock("public sealed class RpcCallbackBindings");
        foreach (var callback in callbacks)
        {
            var callbackType = callback.Service.CallbackInterfaceName!;
            var callbackField = GetCallbackFieldName(callbackType);
            var receiverName = NamingHelper.GetCallbackReceiverParamName(callbackType);
            w.Line($"private {callbackType}? {callbackField};");
            w.OpenBlock($"public void Add({callbackType} {receiverName})");
            w.Line($"if ({receiverName} is null) throw new ArgumentNullException(nameof({receiverName}));");
            w.OpenBlock($"if ({callbackField} is not null)");
            w.Line($"throw new InvalidOperationException(\"Callback receiver for '{callbackType}' is already registered.\");");
            w.CloseBlock();
            w.Line($"{callbackField} = {receiverName};");
            w.CloseBlock();
            w.Line();
        }

        w.OpenBlock("internal void Bind(IRpcClient client)");
        w.Line("if (client is null) throw new ArgumentNullException(nameof(client));");
        foreach (var callback in callbacks)
        {
            var callbackType = callback.Service.CallbackInterfaceName!;
            var callbackField = GetCallbackFieldName(callbackType);
            w.OpenBlock($"if ({callbackField} is not null)");
            w.Line($"global::{generatedNamespace}.{NamingHelper.GetCallbackBinderTypeName(callbackType)}.Bind(client, {callbackField});");
            w.CloseBlock();
        }
        w.CloseBlock();
        w.CloseBlock();
        w.Line();

        foreach (var callback in callbacks)
        {
            var callbackType = callback.Service.CallbackInterfaceName!;
            w.OpenBlock($"public abstract class {GetCallbackBaseTypeName(callbackType)} : {callbackType}");
            foreach (var method in callback.Service.CallbackMethods.OrderBy(static method => method.MethodId))
            {
                w.Line();
                w.OpenBlock($"public virtual void {method.Name}({GetCallbackMethodParameterSignature(method.Parameters)})");
                w.CloseBlock();
            }
            w.CloseBlock();
            w.Line();
        }
    }
}
