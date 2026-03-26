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

        var transport = options.Transport ?? StarterCli.PromptTransport();
        var serializer = options.Serializer ?? StarterCli.PromptSerializer();

        var rootPath = Path.GetFullPath(Path.Combine(options.OutputDir, options.ProjectName));
        var versions = NuGetVersionResolver.ResolveVersions(transport, serializer);
        var generator = new StarterTemplateGenerator(ProcessRunner.RunDotNet, ProcessRunner.RunGit);

        try
        {
            StarterOutputManager.GenerateIntoTargetDirectory(
                rootPath,
                stagingRootPath => generator.GenerateTemplate(stagingRootPath, options.ProjectName, transport, serializer, versions));
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
        Console.WriteLine("  3) Open \"Client\" with Unity 2022 LTS.");

        return 0;
    }
}
