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
            var generator = new StarterTemplateGenerator(CreateFakeDotNetRunner());

            generator.GenerateTemplate(root, "My Game!@#$", TransportKind.Kcp, SerializerKind.MemoryPack, Versions);

            var sharedCsproj = File.ReadAllText(Path.Combine(root, "Shared", "Shared.csproj"));
            var sharedAsmdef = File.ReadAllText(Path.Combine(root, "Shared", "Shared.asmdef"));
            var sharedDtos = File.ReadAllText(Path.Combine(root, "Shared", "Interfaces", "SharedDtos.cs"));

            Assert.Contains("<LangVersion>9.0</LangVersion>", sharedCsproj);
            Assert.Contains("<RootNamespace>Shared</RootNamespace>", sharedCsproj);
            Assert.Contains("\"rootNamespace\": \"Shared\"", sharedAsmdef);
            Assert.DoesNotContain("My Game", sharedDtos, StringComparison.Ordinal);
            Assert.Contains("namespace Shared.Interfaces;", sharedDtos);
            Assert.True(File.Exists(Path.Combine(root, "Shared", "package.json")));
            Assert.False(File.Exists(Path.Combine(root, "Shared", "UnityPackage", "package.json")));
            Assert.False(File.Exists(Path.Combine(root, "Shared", "UnityPackage", "SharedDtos.cs")));
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
            var generator = new StarterTemplateGenerator(CreateFakeDotNetRunner(commands));

            generator.GenerateTemplate(root, "Starter-App", TransportKind.Tcp, SerializerKind.Json, Versions);

            var slnxPath = Path.Combine(root, "Server", "Server.slnx");
            var slnx = File.ReadAllText(slnxPath);

            Assert.Contains("new sln -n \"Server\"", commands);
            Assert.Contains($"sln \"{slnxPath}\" add \"..{Path.DirectorySeparatorChar}Shared{Path.DirectorySeparatorChar}Shared.csproj\"", commands);
            Assert.Contains($"sln \"{slnxPath}\" add \"Server{Path.DirectorySeparatorChar}Server.csproj\"", commands);
            Assert.Contains("<Project Path=\"../Shared/Shared.csproj\" />", slnx);
            Assert.Contains("<Project Path=\"Server/Server.csproj\" />", slnx);
            Assert.True(File.Exists(Path.Combine(root, "Server", "Server", "Server.csproj")));
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
            var generator = new StarterTemplateGenerator(CreateFakeDotNetRunner());

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
            Assert.Contains("<RootNamespace>Server</RootNamespace>", serverCsproj);
            Assert.Contains("<ProjectReference Include=\"..\\..\\Shared\\Shared.csproj\" />", serverCsproj);
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
}
