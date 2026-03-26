namespace ULinkRPC.Starter;

internal static class Program
{
    private static async Task<int> Main(string[] args)
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
        if (Directory.Exists(rootPath) && Directory.EnumerateFileSystemEntries(rootPath).Any())
        {
            Console.Error.WriteLine($"Target directory already exists and is not empty: {rootPath}");
            return 1;
        }

        Directory.CreateDirectory(rootPath);

        var versions = await NuGetVersionResolver.ResolveVersionsAsync(transport, serializer);
        new StarterTemplateGenerator(ProcessRunner.RunDotNet, ProcessRunner.RunGit)
            .GenerateTemplate(rootPath, options.ProjectName, transport, serializer, versions);

        Console.WriteLine($"Created ULinkRPC starter template at: {rootPath}");
        Console.WriteLine("Next steps:");
        Console.WriteLine($"  1) cd \"{rootPath}\"");
        Console.WriteLine("  2) dotnet run --project \"Server/Server/Server.csproj\"");
        Console.WriteLine("  3) Open \"Client\" with Unity 2022 LTS.");

        return 0;
    }
}
