namespace ULinkRPC.Starter;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (!StarterCli.TryParseArgs(args, out var options, out var error))
        {
            Console.Error.WriteLine(error);
            StarterCli.PrintUsage();
            return 1;
        }

        if (options.ShowHelp)
        {
            StarterCli.PrintUsage();
            return 0;
        }

        if (options.ShowVersion)
        {
            Console.WriteLine(GetVersion());
            return 0;
        }

        try
        {
            return options.Command switch
            {
                StarterCommandKind.New => RunNewCommand(options.NewCommand!),
                StarterCommandKind.CodeGen => RunCodeGenCommand(options.CodeGenCommand!),
                _ => throw new ArgumentOutOfRangeException()
            };
        }
        catch (InvalidOperationException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static int RunNewCommand(StarterNewCommandOptions options)
    {
        var transport = options.Transport ?? StarterCli.PromptTransport();
        var serializer = options.Serializer ?? StarterCli.PromptSerializer();
        var clientEngine = options.ClientEngine ?? StarterCli.PromptClientEngine();
        var nuGetForUnitySource = options.NuGetForUnitySource ?? clientEngine.GetDefaultNuGetForUnitySource();

        var rootPath = Path.GetFullPath(Path.Combine(options.OutputDir, options.ProjectName));
        var versions = NuGetVersionResolver.ResolveVersions(transport, serializer);
        var generator = new StarterTemplateGenerator(ProcessRunner.RunDotNet, ProcessRunner.RunGit);

        StarterOutputManager.GenerateIntoTargetDirectory(
            rootPath,
            stagingRootPath => generator.GenerateTemplate(stagingRootPath, options.ProjectName, clientEngine, transport, serializer, nuGetForUnitySource, versions));

        Console.WriteLine($"Created ULinkRPC project at: {rootPath}");
        if (options.NoNextSteps)
        {
            return 0;
        }

        Console.WriteLine("Next steps:");
        Console.WriteLine($"  1) cd \"{rootPath}\"");
        Console.WriteLine("  2) dotnet run --project \"Server/Server/Server.csproj\"");
        Console.WriteLine(clientEngine switch
        {
            ClientEngineKind.Unity or ClientEngineKind.UnityCn or ClientEngineKind.Tuanjie =>
                $"  3) Open \"Client\" with {clientEngine.GetStarterClientLabel()}.",
            ClientEngineKind.Godot => "  3) Open \"Client\" with Godot 4.6 and build the C# solution.",
            ClientEngineKind.Stride3D => "  3) Run \"dotnet run --project Client/Client.csproj\" to start the Stride3D client.",
            _ => throw new ArgumentOutOfRangeException(nameof(clientEngine), clientEngine, null)
        });
        Console.WriteLine("  4) After changing Shared contracts, run `ulinkrpc-starter codegen` from the project root.");
        return 0;
    }

    private static int RunCodeGenCommand(StarterCodeGenCommandOptions options)
    {
        var startPath = Path.GetFullPath(options.ProjectRoot);
        if (!StarterWorkspace.TryResolveProjectContext(startPath, out var context, out var error))
        {
            Console.Error.WriteLine(error);
            return 1;
        }

        var projectTool = new StarterProjectTool(ProcessRunner.RunDotNet);
        projectTool.RunCodeGen(context, options.NoRestore);
        Console.WriteLine($"Regenerated server and client code for: {context.RootPath}");
        return 0;
    }

    private static string GetVersion()
    {
        var assembly = typeof(Program).Assembly;
        var informationalVersion = assembly
            .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), inherit: false)
            .OfType<System.Reflection.AssemblyInformationalVersionAttribute>()
            .FirstOrDefault()?
            .InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            var plusIndex = informationalVersion.IndexOf('+');
            return plusIndex >= 0
                ? informationalVersion[..plusIndex]
                : informationalVersion;
        }

        var assemblyVersion = assembly.GetName().Version;
        if (assemblyVersion is null)
        {
            return "unknown";
        }

        return assemblyVersion.Revision == 0
            ? assemblyVersion.ToString(3)
            : assemblyVersion.ToString();
    }
}
