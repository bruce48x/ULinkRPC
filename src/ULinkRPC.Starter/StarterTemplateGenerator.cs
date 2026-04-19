namespace ULinkRPC.Starter;

internal sealed class StarterTemplateGenerator(Action<string, string> runDotNet, Action<string, string> runGit)
{
    public void GenerateTemplate(string rootPath, string projectName, ClientEngineKind clientEngine, TransportKind transport, SerializerKind serializer, ResolvedVersions versions)
    {
        var context = CreateContext(rootPath, projectName, clientEngine, transport, serializer, versions);

        GenerateGitIgnore(context.Paths.RootPath);
        StarterSharedTemplate.Generate(context);
        StarterServerTemplate.Generate(context);
        GenerateSolution(context.Paths.ServerRootPath);
        GenerateClientTemplate(context);
        GenerateCodeGenToolManifest(context);
        RunCodeGen(context);
        InitializeGit(context.Paths.RootPath);
    }

    private static StarterTemplateContext CreateContext(
        string rootPath,
        string projectName,
        ClientEngineKind clientEngine,
        TransportKind transport,
        SerializerKind serializer,
        ResolvedVersions versions)
    {
        var paths = new StarterPaths(
            rootPath,
            Path.Combine(rootPath, "Shared"),
            Path.Combine(rootPath, "Server"),
            Path.Combine(rootPath, "Server", "Server"),
            Path.Combine(rootPath, "Client"));

        Directory.CreateDirectory(paths.SharedPath);
        Directory.CreateDirectory(paths.ServerRootPath);
        Directory.CreateDirectory(paths.ServerAppPath);
        Directory.CreateDirectory(paths.ClientPath);

        return new StarterTemplateContext(
            projectName,
            MakeCompanyId(projectName),
            clientEngine,
            transport,
            serializer,
            versions,
            paths);
    }

    private static void GenerateClientTemplate(StarterTemplateContext context)
    {
        switch (context.ClientEngine)
        {
            case ClientEngineKind.Unity:
                StarterUnityTemplate.Generate(context);
                return;
            case ClientEngineKind.Godot:
                StarterGodotTemplate.Generate(context);
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(context.ClientEngine), context.ClientEngine, null);
        }
    }

    private void InitializeGit(string rootPath)
    {
        runGit(rootPath, "init");
    }

    private void GenerateSolution(string serverPath)
    {
        var solutionPath = Path.Combine(serverPath, "Server.slnx");
        runDotNet(serverPath, "new sln -n \"Server\" --format slnx");
        runDotNet(serverPath, $"sln \"{solutionPath}\" add \"..{Path.DirectorySeparatorChar}Shared{Path.DirectorySeparatorChar}Shared.csproj\"");
        runDotNet(serverPath, $"sln \"{solutionPath}\" add \"Server{Path.DirectorySeparatorChar}Server.csproj\"");
    }

    private static string MakeCompanyId(string projectName)
    {
        var filtered = new string(projectName.Where(char.IsLetterOrDigit).ToArray());
        return string.IsNullOrWhiteSpace(filtered) ? "ulinkrpc.sample" : $"ulinkrpc.{filtered.ToLowerInvariant()}";
    }

    private void GenerateCodeGenToolManifest(StarterTemplateContext context)
    {
        runDotNet(context.Paths.RootPath, "new tool-manifest");
        runDotNet(context.Paths.RootPath, $"tool install ULinkRPC.CodeGen --version {context.Versions.CodeGen}");
    }

    private void RunCodeGen(StarterTemplateContext context)
    {
        runDotNet(
            context.Paths.ServerAppPath,
            $"tool run ulinkrpc-codegen -- --contracts \"{context.Paths.SharedPath}\" --mode server --server-output \"Generated\" --server-namespace \"Server.Generated\"");

        runDotNet(
            context.Paths.ClientPath,
            context.ClientEngine == ClientEngineKind.Unity
                ? $"tool run ulinkrpc-codegen -- --contracts \"{context.Paths.SharedPath}\" --mode unity --output \"Assets{Path.DirectorySeparatorChar}Scripts{Path.DirectorySeparatorChar}Rpc{Path.DirectorySeparatorChar}Generated\" --namespace \"Rpc.Generated\""
                : $"tool run ulinkrpc-codegen -- --contracts \"{context.Paths.SharedPath}\" --mode godot --output \"Scripts{Path.DirectorySeparatorChar}Rpc{Path.DirectorySeparatorChar}Generated\" --namespace \"Rpc.Generated\"");
    }

    private static void GenerateGitIgnore(string rootPath)
    {
        var gitIgnore = """
# OS / Editor
.DS_Store
Thumbs.db
.idea/
.vs/
*.suo
*.user
*.userprefs
*.DotSettings.user

# .NET build outputs
**/bin/
**/obj/
/_artifacts/
/Client/.godot/

# Unity generated folders
/Client/[Ll]ibrary/
/Client/[Tt]emp/
/Client/[Ll]ogs/
/Client/[Uu]ser[Ss]ettings/
/Client/[Oo]bj/
/Client/[Bb]uild/
/Client/[Bb]uilds/
/Client/[Mm]emoryCaptures/
/Client/[Rr]ecordings/

# Unity generated project/IDE files
/Client/*.csproj
/Client/*.sln
/Client/*.slnx
/Client/*.unityproj
/Client/*.pidb
/Client/*.booproj
/Client/*.svd
/Client/*.pdb
/Client/*.mdb
/Client/*.opendb
/Client/*.VC.db

# NuGetForUnity restored packages
/Client/Assets/Packages/

# Godot generated files
/Client/.mono/
/Client/export_presets.cfg
/Client/*.csproj
/Client/*.sln

# Logs
*.log
""";

        StarterFileWriter.Write(Path.Combine(rootPath, ".gitignore"), gitIgnore);
    }
}
