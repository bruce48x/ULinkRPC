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

        if (options.ShowVersion)
        {
            Console.WriteLine(GetVersion());
            return 0;
        }

        var transport = options.Transport ?? StarterCli.PromptTransport();
        var serializer = options.Serializer ?? StarterCli.PromptSerializer();
        var clientEngine = options.ClientEngine ?? StarterCli.PromptClientEngine();

        var rootPath = Path.GetFullPath(Path.Combine(options.OutputDir, options.ProjectName));
        var versions = NuGetVersionResolver.ResolveVersions(transport, serializer);
        var generator = new StarterTemplateGenerator(ProcessRunner.RunDotNet, ProcessRunner.RunGit);

        try
        {
            StarterOutputManager.GenerateIntoTargetDirectory(
                rootPath,
                stagingRootPath => generator.GenerateTemplate(stagingRootPath, options.ProjectName, clientEngine, transport, serializer, versions));
        }
        catch (InvalidOperationException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        Console.WriteLine($"Created ULinkRPC starter template at: {rootPath}");
        Console.WriteLine("Next steps:");
        Console.WriteLine($"  1) cd \"{rootPath}\"");
        Console.WriteLine("  2) dotnet run --project \"Server/Server/Server.csproj\"");
        Console.WriteLine(clientEngine.IsUnityCompatible()
            ? $"  3) Open \"Client\" with {clientEngine.GetStarterClientLabel()}."
            : "  3) Open \"Client\" with Godot 4.6 and build the C# solution.");

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
