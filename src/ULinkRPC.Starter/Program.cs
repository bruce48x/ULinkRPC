using System.Text.Json;
using System.Diagnostics;
using System.Xml.Linq;

namespace ULinkRPC.Starter;

internal static class Program
{
    private static readonly HttpClient Http = new();

    private static async Task<int> Main(string[] args)
    {
        if (!TryParseArgs(args, out var projectName, out var outputDir, out var transport, out var serializer, out var error))
        {
            Console.Error.WriteLine(error);
            PrintUsage();
            return 1;
        }

        transport ??= PromptTransport();
        serializer ??= PromptSerializer();

        var rootPath = Path.GetFullPath(Path.Combine(outputDir, projectName));
        if (Directory.Exists(rootPath) && Directory.EnumerateFileSystemEntries(rootPath).Any())
        {
            Console.Error.WriteLine($"Target directory already exists and is not empty: {rootPath}");
            return 1;
        }

        Directory.CreateDirectory(rootPath);

        var versions = await ResolveVersionsAsync(transport.Value, serializer.Value);
        new StarterTemplateGenerator(RunDotNet, RunGit)
            .GenerateTemplate(rootPath, projectName, transport.Value, serializer.Value, versions);

        Console.WriteLine($"Created ULinkRPC starter template at: {rootPath}");
        Console.WriteLine("Next steps:");
        Console.WriteLine($"  1) cd \"{rootPath}\"");
        Console.WriteLine("  2) dotnet run --project \"Server/Server/Server.csproj\"");
        Console.WriteLine("  3) Open \"Client\" with Unity 2022 LTS.");

        return 0;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage: ulinkrpc-starter [--name MyGame] [--output ./out] [--transport tcp|websocket|kcp] [--serializer json|memorypack]");
    }

    private static bool TryParseArgs(
        string[] args,
        out string projectName,
        out string outputDir,
        out TransportKind? transport,
        out SerializerKind? serializer,
        out string error)
    {
        projectName = "ULinkApp";
        outputDir = Directory.GetCurrentDirectory();
        transport = null;
        serializer = null;
        error = string.Empty;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg is "--name" && i + 1 < args.Length)
            {
                projectName = args[++i];
                continue;
            }

            if (arg is "--output" && i + 1 < args.Length)
            {
                outputDir = args[++i];
                continue;
            }

            if (arg is "--transport" && i + 1 < args.Length)
            {
                if (!TryParseTransport(args[++i], out var parsed))
                {
                    error = "Invalid --transport value.";
                    return false;
                }

                transport = parsed;
                continue;
            }

            if (arg is "--serializer" && i + 1 < args.Length)
            {
                if (!TryParseSerializer(args[++i], out var parsed))
                {
                    error = "Invalid --serializer value.";
                    return false;
                }

                serializer = parsed;
                continue;
            }

            error = $"Unknown or incomplete option: {arg}";
            return false;
        }

        if (string.IsNullOrWhiteSpace(projectName))
        {
            error = "--name cannot be empty.";
            return false;
        }

        return true;
    }

    private static bool TryParseTransport(string raw, out TransportKind transport)
    {
        var normalized = raw.Trim().ToLowerInvariant();
        switch (normalized)
        {
            case "tcp": transport = TransportKind.Tcp; return true;
            case "websocket":
            case "ws": transport = TransportKind.WebSocket; return true;
            case "kcp": transport = TransportKind.Kcp; return true;
            default: transport = default; return false;
        }
    }

    private static bool TryParseSerializer(string raw, out SerializerKind serializer)
    {
        var normalized = raw.Trim().ToLowerInvariant();
        switch (normalized)
        {
            case "json": serializer = SerializerKind.Json; return true;
            case "memorypack": serializer = SerializerKind.MemoryPack; return true;
            default: serializer = default; return false;
        }
    }

    private static TransportKind PromptTransport()
    {
        Console.WriteLine("Select transport:");
        Console.WriteLine("  1) TCP");
        Console.WriteLine("  2) WebSocket");
        Console.WriteLine("  3) KCP");
        while (true)
        {
            Console.Write("> ");
            var line = Console.ReadLine()?.Trim();
            switch (line)
            {
                case "1": return TransportKind.Tcp;
                case "2": return TransportKind.WebSocket;
                case "3": return TransportKind.Kcp;
            }

            Console.WriteLine("Please enter 1-3.");
        }
    }

    private static SerializerKind PromptSerializer()
    {
        Console.WriteLine("Select serializer:");
        Console.WriteLine("  1) JSON");
        Console.WriteLine("  2) MemoryPack");
        while (true)
        {
            Console.Write("> ");
            var line = Console.ReadLine()?.Trim();
            switch (line)
            {
                case "1": return SerializerKind.Json;
                case "2": return SerializerKind.MemoryPack;
            }

            Console.WriteLine("Please enter 1-2.");
        }
    }

    private static async Task<ResolvedVersions> ResolveVersionsAsync(TransportKind transport, SerializerKind serializer)
    {
        var transportPackage = GetTransportPackage(transport);
        var serializerPackage = GetSerializerPackage(serializer);

        var coreVersion = await ResolveLatestStableVersionAsync("ULinkRPC.Core");
        var serverVersion = await ResolveLatestStableVersionAsync("ULinkRPC.Server");
        var clientVersion = await ResolveLatestStableVersionAsync("ULinkRPC.Client");
        var transportVersion = await ResolveLatestStableVersionAsync(transportPackage);
        var serializerVersion = await ResolveLatestStableVersionAsync(serializerPackage);
        var codeGenVersion = await ResolveLatestStableVersionAsync("ULinkRPC.CodeGen");
        var serializerRuntime = default(string);
        var serializerRuntimeCore = default(string);

        if (serializer is SerializerKind.MemoryPack)
        {
            serializerRuntime = await ResolveDependencyVersionAsync(serializerPackage, serializerVersion, ".NETStandard2.1", "MemoryPack");
            serializerRuntimeCore = await ResolveDependencyVersionAsync("MemoryPack", serializerRuntime, ".NETStandard2.1", "MemoryPack.Core");
        }

        return new ResolvedVersions(
            coreVersion,
            serverVersion,
            clientVersion,
            transportVersion,
            serializerVersion,
            codeGenVersion,
            serializerRuntime,
            serializerRuntimeCore);
    }

    private static async Task<string> ResolveLatestStableVersionAsync(string packageId)
    {
        var url = $"https://api.nuget.org/v3-flatcontainer/{packageId.ToLowerInvariant()}/index.json";
        using var stream = await Http.GetStreamAsync(url);
        using var doc = await JsonDocument.ParseAsync(stream);
        var versions = doc.RootElement.GetProperty("versions").EnumerateArray()
            .Select(v => v.GetString())
            .Where(v => !string.IsNullOrWhiteSpace(v) && !v!.Contains('-', StringComparison.Ordinal))
            .ToList();

        if (versions.Count == 0)
        {
            throw new InvalidOperationException($"No stable NuGet versions found for package '{packageId}'.");
        }

        return versions[^1]!;
    }

    private static async Task<string> ResolveDependencyVersionAsync(string packageId, string packageVersion, string targetFramework, string dependencyId)
    {
        var nuspec = await LoadNuSpecAsync(packageId, packageVersion);
        var dependency = nuspec
            .Descendants()
            .FirstOrDefault(element =>
                element.Name.LocalName == "group" &&
                string.Equals((string?)element.Attribute("targetFramework"), targetFramework, StringComparison.OrdinalIgnoreCase))
            ?.Elements()
            .FirstOrDefault(element =>
                element.Name.LocalName == "dependency" &&
                string.Equals((string?)element.Attribute("id"), dependencyId, StringComparison.OrdinalIgnoreCase));

        var version = (string?)dependency?.Attribute("version");
        if (string.IsNullOrWhiteSpace(version))
        {
            throw new InvalidOperationException(
                $"Unable to resolve dependency '{dependencyId}' from package '{packageId}' {packageVersion} for target framework '{targetFramework}'.");
        }

        return version;
    }

    private static async Task<XDocument> LoadNuSpecAsync(string packageId, string packageVersion)
    {
        var lowerId = packageId.ToLowerInvariant();
        var lowerVersion = packageVersion.ToLowerInvariant();
        var url = $"https://api.nuget.org/v3-flatcontainer/{lowerId}/{lowerVersion}/{lowerId}.nuspec";
        using var stream = await Http.GetStreamAsync(url);
        return XDocument.Load(stream);
    }

    private static string GetTransportPackage(TransportKind transport) => transport switch
    {
        TransportKind.Tcp => "ULinkRPC.Transport.Tcp",
        TransportKind.WebSocket => "ULinkRPC.Transport.WebSocket",
        TransportKind.Kcp => "ULinkRPC.Transport.Kcp",
        _ => throw new ArgumentOutOfRangeException(nameof(transport), transport, null)
    };

    private static string GetSerializerPackage(SerializerKind serializer) => serializer switch
    {
        SerializerKind.Json => "ULinkRPC.Serializer.Json",
        SerializerKind.MemoryPack => "ULinkRPC.Serializer.MemoryPack",
        _ => throw new ArgumentOutOfRangeException(nameof(serializer), serializer, null)
    };

    private static void RunDotNet(string workingDirectory, string arguments)
    {
        RunProcess("dotnet", workingDirectory, arguments);
    }

    private static void RunGit(string workingDirectory, string arguments)
    {
        RunProcess("git", workingDirectory, arguments);
    }

    private static void RunProcess(string fileName, string workingDirectory, string arguments)
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        });

        if (process is null)
        {
            throw new InvalidOperationException($"Failed to start '{fileName} {arguments}'.");
        }

        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Command failed: {fileName} {arguments}{Environment.NewLine}{stdout}{stderr}".TrimEnd());
    }

}
