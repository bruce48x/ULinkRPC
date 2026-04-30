namespace ULinkRPC.Starter;

internal sealed class StarterProjectTool(Action<string, string> runDotNet)
{
    public void RunCodeGen(StarterProjectContext context, bool noRestore)
    {
        if (!noRestore)
            runDotNet(context.RootPath, "tool restore");

        runDotNet(
            context.ServerAppPath,
            $"tool run ulinkrpc-codegen -- --contracts \"{context.SharedPath}\" --mode server --server-output \"Generated\" --server-namespace \"Server.Generated\"");

        runDotNet(
            context.ClientPath,
            $"tool run ulinkrpc-codegen -- --contracts \"{context.SharedPath}\" --mode {context.ClientCodeGenMode} --output \"{context.ClientCodeGenOutput}\" --namespace \"Rpc.Generated\"");
    }
}
