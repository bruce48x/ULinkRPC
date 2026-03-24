using System.Text.Json;
using ULinkRPC.Starter;
using Xunit;

namespace ULinkRPC.Starter.Tests;

public sealed class StarterTemplateGeneratorTests
{
    private static readonly ResolvedVersions Versions = new("1.2.3", "2.3.4", "3.4.5", "4.5.6");

    [Fact]
    public void GenerateTemplate_CreatesSharedLayout_ForUnityCompatibilityRules()
    {
        var root = CreateTempRoot();
        try
        {
            var generator = new StarterTemplateGenerator(CreateFakeDotNetRunner(), CreateFakeGitRunner());

            generator.GenerateTemplate(root, "My Game!@#$", TransportKind.Kcp, SerializerKind.MemoryPack, Versions);

            var sharedProps = File.ReadAllText(Path.Combine(root, "Shared", "Directory.Build.props"));
            var sharedCsproj = File.ReadAllText(Path.Combine(root, "Shared", "Shared.csproj"));
            var sharedAsmdef = File.ReadAllText(Path.Combine(root, "Shared", "Shared.asmdef"));
            var sharedDtos = File.ReadAllText(Path.Combine(root, "Shared", "Interfaces", "SharedDtos.cs"));
            var gitIgnore = File.ReadAllText(Path.Combine(root, ".gitignore"));

            Assert.Contains("<LangVersion>9.0</LangVersion>", sharedCsproj);
            Assert.Contains("<ImplicitUsings>disable</ImplicitUsings>", sharedCsproj);
            Assert.Contains("<RootNamespace>Shared</RootNamespace>", sharedCsproj);
            Assert.Contains(@"<MSBuildProjectExtensionsPath>..\_artifacts\Shared\obj\</MSBuildProjectExtensionsPath>", sharedProps);
            Assert.Contains(@"<BaseIntermediateOutputPath>..\_artifacts\Shared\obj\</BaseIntermediateOutputPath>", sharedProps);
            Assert.Contains(@"<BaseOutputPath>..\_artifacts\Shared\bin\</BaseOutputPath>", sharedProps);
            Assert.Contains("\"rootNamespace\": \"Shared\"", sharedAsmdef);
            Assert.DoesNotContain("My Game", sharedDtos, StringComparison.Ordinal);
            Assert.Contains("namespace Shared.Interfaces", sharedDtos);
            Assert.DoesNotContain("namespace Shared.Interfaces;", sharedDtos, StringComparison.Ordinal);
            Assert.DoesNotContain("DateTimeOffset", sharedDtos, StringComparison.Ordinal);
            Assert.Contains("public string ServerTimeUtc { get; set; } = string.Empty;", sharedDtos);
            Assert.True(File.Exists(Path.Combine(root, "Shared", "package.json")));
            Assert.False(File.Exists(Path.Combine(root, "Shared", "UnityPackage", "package.json")));
            Assert.False(File.Exists(Path.Combine(root, "Shared", "UnityPackage", "SharedDtos.cs")));
            Assert.Contains("**/bin/", gitIgnore);
            Assert.Contains("/Client/[Ll]ibrary/", gitIgnore);
            Assert.Contains("/Client/Assets/Packages/", gitIgnore);
            Assert.Contains("/_artifacts/", gitIgnore);
            Assert.Contains(".vs/", gitIgnore);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void GenerateTemplate_CreatesSolutionAndAddsSharedAndServerProjects()
    {
        var root = CreateTempRoot();
        try
        {
            var commands = new List<string>();
            var gitCommands = new List<string>();
            var generator = new StarterTemplateGenerator(CreateFakeDotNetRunner(commands), CreateFakeGitRunner(gitCommands));

            generator.GenerateTemplate(root, "Starter-App", TransportKind.Tcp, SerializerKind.Json, Versions);

            var slnxPath = Path.Combine(root, "Server", "Server.slnx");
            var slnx = File.ReadAllText(slnxPath);

            Assert.Contains("new sln -n \"Server\"", commands);
            Assert.Contains($"sln \"{slnxPath}\" add \"..{Path.DirectorySeparatorChar}Shared{Path.DirectorySeparatorChar}Shared.csproj\"", commands);
            Assert.Contains($"sln \"{slnxPath}\" add \"Server{Path.DirectorySeparatorChar}Server.csproj\"", commands);
            Assert.Contains("<Project Path=\"../Shared/Shared.csproj\" />", slnx);
            Assert.Contains("<Project Path=\"Server/Server.csproj\" />", slnx);
            Assert.True(File.Exists(Path.Combine(root, "Server", "Server", "Server.csproj")));
            Assert.Contains("init", gitCommands);
            Assert.True(Directory.Exists(Path.Combine(root, ".git")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void GenerateTemplate_CreatesClientFiles_WithSharedReferenceAndPinnedUnityVersion()
    {
        var root = CreateTempRoot();
        try
        {
            var generator = new StarterTemplateGenerator(CreateFakeDotNetRunner(), CreateFakeGitRunner());

            generator.GenerateTemplate(root, "Bad Project Name %$#", TransportKind.WebSocket, SerializerKind.Json, Versions);

            var manifestJson = File.ReadAllText(Path.Combine(root, "Client", "Packages", "manifest.json"));
            using var manifest = JsonDocument.Parse(manifestJson);
            var sharedDependency = manifest.RootElement
                .GetProperty("dependencies")
                .GetProperty("com.ulinkrpc.badprojectname.shared")
                .GetString();

            var serverProgram = File.ReadAllText(Path.Combine(root, "Server", "Server", "Program.cs"));
            var serverCsproj = File.ReadAllText(Path.Combine(root, "Server", "Server", "Server.csproj"));
            var packagesConfig = File.ReadAllText(Path.Combine(root, "Client", "Assets", "packages.config"));
            var projectVersion = File.ReadAllText(Path.Combine(root, "Client", "ProjectSettings", "ProjectVersion.txt"));

            Assert.Equal("file:../../Shared", sharedDependency);
            Assert.Contains("using Shared.Interfaces;", serverProgram);
            Assert.DoesNotContain("Bad Project Name", serverProgram, StringComparison.Ordinal);
            Assert.DoesNotContain("DateTimeOffset", serverProgram, StringComparison.Ordinal);
            Assert.Contains("using ULinkRPC.Server;", serverProgram);
            Assert.Contains("using ULinkRPC.Serializer.Json;", serverProgram);
            Assert.Contains("using ULinkRPC.Transport.WebSocket;", serverProgram);
            Assert.Contains("var args = Environment.GetCommandLineArgs().Skip(1).ToArray();", serverProgram);
            Assert.Contains("await RpcServerHostBuilder.Create()", serverProgram);
            Assert.Contains(".UseCommandLine(args)", serverProgram);
            Assert.Contains(".UseJson()", serverProgram);
            Assert.Contains(".UseWebSocket(defaultPort: 20000, path: \"/ws\")", serverProgram);
            Assert.Contains(".RunAsync();", serverProgram);
            Assert.Contains("<RootNamespace>Server</RootNamespace>", serverCsproj);
            Assert.Contains("<ProjectReference Include=\"..\\..\\Shared\\Shared.csproj\" />", serverCsproj);
            Assert.Contains("<package id=\"ULinkRPC.Core\" version=\"1.2.3\" />", packagesConfig);
            Assert.Contains("<package id=\"ULinkRPC.Transport.WebSocket\" version=\"3.4.5\" manuallyInstalled=\"true\" />", packagesConfig);
            Assert.Contains("<package id=\"ULinkRPC.Serializer.Json\" version=\"4.5.6\" manuallyInstalled=\"true\" />", packagesConfig);
            Assert.Contains("m_EditorVersion: 2022.3.62f3c1", projectVersion);
            Assert.Contains("m_EditorVersionWithRevision: 2022.3.62f3c1 (1623fc0bbb97)", projectVersion);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void GenerateTemplate_CreatesServerProgram_WithTransportAndSerializerSpecificBuilderChain()
    {
        var root = CreateTempRoot();
        try
        {
            var generator = new StarterTemplateGenerator(CreateFakeDotNetRunner(), CreateFakeGitRunner());

            generator.GenerateTemplate(root, "Builder-Test", TransportKind.Tcp, SerializerKind.MemoryPack, Versions);

            var serverProgram = File.ReadAllText(Path.Combine(root, "Server", "Server", "Program.cs"));

            Assert.Contains("using ULinkRPC.Serializer.MemoryPack;", serverProgram);
            Assert.Contains("using ULinkRPC.Transport.Tcp;", serverProgram);
            Assert.Contains(".UseMemoryPack()", serverProgram);
            Assert.Contains(".UseTcp(defaultPort: 20000)", serverProgram);
            Assert.DoesNotContain(".UseJson()", serverProgram, StringComparison.Ordinal);
            Assert.DoesNotContain(".UseWebSocket(", serverProgram, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }


    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), $"ulinkrpc_starter_{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        return root;
    }

    private static Action<string, string> CreateFakeDotNetRunner(List<string>? commands = null)
    {
        return (workingDirectory, arguments) =>
        {
            commands?.Add(arguments);

            if (arguments.StartsWith("new sln -n ", StringComparison.Ordinal))
            {
                var solutionName = arguments["new sln -n ".Length..].Trim().Trim('"');
                var slnxPath = Path.Combine(workingDirectory, $"{solutionName}.slnx");
                File.WriteAllText(slnxPath, "<Solution>\n</Solution>\n");
                return;
            }

            if (arguments.StartsWith("sln ", StringComparison.Ordinal) && arguments.Contains(" add ", StringComparison.Ordinal))
            {
                var addIndex = arguments.IndexOf(" add ", StringComparison.Ordinal);
                var slnxPath = arguments["sln ".Length..addIndex].Trim().Trim('"');
                var projectPath = arguments[(addIndex + " add ".Length)..].Trim().Trim('"').Replace('\\', '/');
                var solution = File.ReadAllText(slnxPath);
                var projectEntry = $"  <Project Path=\"{projectPath}\" />\n";
                solution = solution.Replace("</Solution>\n", projectEntry + "</Solution>\n", StringComparison.Ordinal);
                File.WriteAllText(slnxPath, solution);
                return;
            }

            throw new InvalidOperationException($"Unexpected dotnet command in test: {arguments}");
        };
    }

    private static Action<string, string> CreateFakeGitRunner(List<string>? commands = null)
    {
        return (workingDirectory, arguments) =>
        {
            commands?.Add(arguments);

            if (string.Equals(arguments, "init", StringComparison.Ordinal))
            {
                Directory.CreateDirectory(Path.Combine(workingDirectory, ".git"));
                return;
            }

            throw new InvalidOperationException($"Unexpected git command in test: {arguments}");
        };
    }
}
