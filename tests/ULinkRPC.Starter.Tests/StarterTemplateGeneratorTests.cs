using System.Text.Json;
using ULinkRPC.Starter;
using Xunit;

namespace ULinkRPC.Starter.Tests;

public sealed class StarterTemplateGeneratorTests
{
    private static readonly ResolvedVersions Versions = new("1.2.3", "2.3.4", "3.4.5", "4.5.6", "5.6.7", "6.7.8", "6.7.8", "8.9.10");

    [Fact]
    public void GenerateIntoTargetDirectory_RollsBackStagingDirectory_WhenGenerationFails()
    {
        var parentRoot = CreateTempRoot();
        var targetRoot = Path.Combine(parentRoot, "Sample");

        try
        {
            var ex = Assert.Throws<InvalidOperationException>(() =>
                StarterOutputManager.GenerateIntoTargetDirectory(
                    targetRoot,
                    stagingRoot =>
                    {
                        File.WriteAllText(Path.Combine(stagingRoot, "partial.txt"), "partial");
                        throw new InvalidOperationException("boom");
                    }));

            Assert.Equal("boom", ex.Message);
            Assert.False(Directory.Exists(targetRoot));
            Assert.Empty(Directory.GetDirectories(parentRoot, ".Sample.tmp-*", SearchOption.TopDirectoryOnly));
        }
        finally
        {
            Directory.Delete(parentRoot, recursive: true);
        }
    }

    [Fact]
    public void GenerateIntoTargetDirectory_Fails_WhenTargetDirectoryIsNotEmpty()
    {
        var parentRoot = CreateTempRoot();
        var targetRoot = Path.Combine(parentRoot, "Sample");
        Directory.CreateDirectory(targetRoot);
        File.WriteAllText(Path.Combine(targetRoot, "existing.txt"), "x");

        try
        {
            var ex = Assert.Throws<InvalidOperationException>(() =>
                StarterOutputManager.GenerateIntoTargetDirectory(targetRoot, _ => { }));

            Assert.Contains("Target directory already exists and is not empty", ex.Message);
        }
        finally
        {
            Directory.Delete(parentRoot, recursive: true);
        }
    }

    [Fact]
    public void ResolveVersions_UsesBundledReleaseManifest_InsteadOfIndependentLatestLookups()
    {
        var jsonVersions = NuGetVersionResolver.ResolveVersions(TransportKind.WebSocket, SerializerKind.Json);
        var memoryPackVersions = NuGetVersionResolver.ResolveVersions(TransportKind.Kcp, SerializerKind.MemoryPack);

        Assert.Equal("0.11.2", jsonVersions.Core);
        Assert.Equal("0.11.7", jsonVersions.Server);
        Assert.Equal("0.11.0", jsonVersions.Client);
        Assert.Equal("0.11.3", jsonVersions.Transport);
        Assert.Equal("0.11.0", jsonVersions.Serializer);
        Assert.Equal("0.16.4", jsonVersions.CodeGen);
        Assert.Null(jsonVersions.SerializerRuntime);
        Assert.Null(jsonVersions.SerializerRuntimeCore);

        Assert.Equal("0.11.8", memoryPackVersions.Transport);
        Assert.Equal("0.11.0", memoryPackVersions.Serializer);
        Assert.Equal("1.21.4", memoryPackVersions.SerializerRuntime);
        Assert.Equal("1.21.4", memoryPackVersions.SerializerRuntimeCore);
    }

    [Fact]
    public void GenerateTemplate_CreatesSharedLayout_ForUnityCompatibilityRules()
    {
        var root = CreateTempRoot();
        try
        {
            var generator = new StarterTemplateGenerator(CreateFakeDotNetRunner(), CreateFakeGitRunner());

            generator.GenerateTemplate(root, "My Game!@#$", ClientEngineKind.Unity, TransportKind.Kcp, SerializerKind.MemoryPack, Versions);

            var sharedProps = File.ReadAllText(Path.Combine(root, "Shared", "Directory.Build.props"));
            var sharedCsproj = File.ReadAllText(Path.Combine(root, "Shared", "Shared.csproj"));
            var sharedAsmdef = File.ReadAllText(Path.Combine(root, "Shared", "Shared.asmdef"));
            var sharedDtos = File.ReadAllText(Path.Combine(root, "Shared", "Interfaces", "SharedDtos.cs"));
            var gitIgnore = File.ReadAllText(Path.Combine(root, ".gitignore"));

            Assert.Contains("<LangVersion>latest</LangVersion>", sharedCsproj);
            Assert.Contains("<ImplicitUsings>disable</ImplicitUsings>", sharedCsproj);
            Assert.Contains("<TargetFrameworks>netstandard2.1;net10.0</TargetFrameworks>", sharedCsproj);
            Assert.Contains("<RootNamespace>Shared</RootNamespace>", sharedCsproj);
            Assert.Contains("<PackageReference Include=\"ULinkRPC.Core\" Version=\"1.2.3\" />", sharedCsproj);
            Assert.Contains("<PackageReference Include=\"ULinkRPC.Serializer.MemoryPack\" Version=\"5.6.7\" />", sharedCsproj);
            Assert.Contains("<PackageReference Include=\"MemoryPack\" Version=\"6.7.8\" />", sharedCsproj);
            Assert.Contains("<PackageReference Include=\"MemoryPack.Generator\" Version=\"6.7.8\">", sharedCsproj);
            Assert.Contains(@"<MSBuildProjectExtensionsPath>..\_artifacts\Shared\obj\</MSBuildProjectExtensionsPath>", sharedProps);
            Assert.Contains(@"<BaseIntermediateOutputPath>..\_artifacts\Shared\obj\</BaseIntermediateOutputPath>", sharedProps);
            Assert.Contains(@"<BaseOutputPath>..\_artifacts\Shared\bin\</BaseOutputPath>", sharedProps);
            Assert.Contains("\"rootNamespace\": \"Shared\"", sharedAsmdef);
            Assert.Contains("\"overrideReferences\": true", sharedAsmdef);
            Assert.Contains("\"allowUnsafeCode\": true", sharedAsmdef);
            Assert.Contains("\"ULinkRPC.Core.dll\"", sharedAsmdef);
            Assert.Contains("\"MemoryPack.Core.dll\"", sharedAsmdef);
            Assert.Contains("\"System.Runtime.CompilerServices.Unsafe.dll\"", sharedAsmdef);
            Assert.DoesNotContain("My Game", sharedDtos, StringComparison.Ordinal);
            Assert.Contains("using MemoryPack;", sharedDtos);
            Assert.Contains("[MemoryPackable]", sharedDtos);
            Assert.DoesNotContain("GenerateType.VersionTolerant", sharedDtos, StringComparison.Ordinal);
            Assert.Contains("public sealed partial class PingRequest", sharedDtos);
            Assert.Contains("public sealed partial class PingReply", sharedDtos);
            Assert.Contains("[MemoryPackOrder(0)]", sharedDtos);
            Assert.Contains("[MemoryPackOrder(1)]", sharedDtos);
            Assert.Contains("namespace Shared.Interfaces", sharedDtos);
            Assert.DoesNotContain("namespace Shared.Interfaces;", sharedDtos, StringComparison.Ordinal);
            Assert.DoesNotContain("DateTimeOffset", sharedDtos, StringComparison.Ordinal);
            Assert.Contains("public string ServerTimeUtc { get; set; } = string.Empty;", sharedDtos);
            Assert.True(File.Exists(Path.Combine(root, "Shared", "Interfaces", "IPingService.cs")));
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

            generator.GenerateTemplate(root, "Starter-App", ClientEngineKind.Unity, TransportKind.Tcp, SerializerKind.Json, Versions);

            var slnxPath = Path.Combine(root, "Server", "Server.slnx");
            var slnx = File.ReadAllText(slnxPath);
            var codegenScript = File.ReadAllText(Path.Combine(root, "codegen.ps1"));
            var codegenShellScript = File.ReadAllText(Path.Combine(root, "codegen.sh"));

            Assert.Contains("new sln -n \"Server\" --format slnx", commands);
            Assert.Contains($"sln \"{slnxPath}\" add \"..{Path.DirectorySeparatorChar}Shared{Path.DirectorySeparatorChar}Shared.csproj\"", commands);
            Assert.Contains($"sln \"{slnxPath}\" add \"Server{Path.DirectorySeparatorChar}Server.csproj\"", commands);
            Assert.Contains("new tool-manifest", commands);
            Assert.Contains("tool install ULinkRPC.CodeGen --version 6.7.8", commands);
            Assert.Contains($"tool run ulinkrpc-codegen -- --contracts \"{Path.Combine(root, "Shared")}\" --mode server --server-output \"Generated\" --server-namespace \"Server.Generated\"", commands);
            Assert.Contains($"tool run ulinkrpc-codegen -- --contracts \"{Path.Combine(root, "Shared")}\" --mode unity --output \"Assets{Path.DirectorySeparatorChar}Scripts{Path.DirectorySeparatorChar}Rpc{Path.DirectorySeparatorChar}Generated\" --namespace \"Rpc.Generated\"", commands);
            Assert.Contains("dotnet tool restore", codegenScript);
            Assert.Contains("--mode', 'server'", codegenScript);
            Assert.Contains("--mode', 'unity'", codegenScript);
            Assert.Contains("Assets\\Scripts\\Rpc\\Generated", codegenScript);
            Assert.Contains("#!/usr/bin/env sh", codegenShellScript);
            Assert.Contains("--mode unity", codegenShellScript);
            Assert.Contains("--output \"Assets/Scripts/Rpc/Generated\"", codegenShellScript);
            Assert.Contains("<Project Path=\"../Shared/Shared.csproj\" />", slnx);
            Assert.Contains("<Project Path=\"Server/Server.csproj\" />", slnx);
            Assert.True(File.Exists(Path.Combine(root, "Server", "Server", "Server.csproj")));
            Assert.True(File.Exists(Path.Combine(root, "Server", "Server", "Generated", "AllServicesBinder.cs")));
            Assert.True(File.Exists(Path.Combine(root, "Client", "Assets", "Scripts", "Rpc", "Generated", "RpcApi.cs")));
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

            generator.GenerateTemplate(root, "Bad Project Name %$#", ClientEngineKind.Unity, TransportKind.WebSocket, SerializerKind.Json, Versions);

            var manifestJson = File.ReadAllText(Path.Combine(root, "Client", "Packages", "manifest.json"));
            using var manifest = JsonDocument.Parse(manifestJson);
            var sharedDependency = manifest.RootElement
                .GetProperty("dependencies")
                .GetProperty("com.ulinkrpc.badprojectname.shared")
                .GetString();

            var serverProgram = File.ReadAllText(Path.Combine(root, "Server", "Server", "Program.cs"));
            var serverCsproj = File.ReadAllText(Path.Combine(root, "Server", "Server", "Server.csproj"));
            var pingServicePath = Path.Combine(root, "Server", "Server", "Services", "PingService.cs");
            var pingService = File.ReadAllText(pingServicePath);
            var packagesConfig = File.ReadAllText(Path.Combine(root, "Client", "Assets", "packages.config"));
            var nugetConfig = File.ReadAllText(Path.Combine(root, "Client", "Assets", "NuGet.config"));
            var projectVersion = File.ReadAllText(Path.Combine(root, "Client", "ProjectSettings", "ProjectVersion.txt"));
            var clientReadme = File.ReadAllText(Path.Combine(root, "Client", "README.md"));
            var testerScript = File.ReadAllText(Path.Combine(root, "Client", "Assets", "Scripts", "Rpc", "Testing", "RpcConnectionTester.cs"));
            var testerScriptMeta = File.ReadAllText(Path.Combine(root, "Client", "Assets", "Scripts", "Rpc", "Testing", "RpcConnectionTester.cs.meta"));
            var scene = File.ReadAllText(Path.Combine(root, "Client", "Assets", "Scenes", "ConnectionTest.unity"));
            var autoOpenSceneScript = File.ReadAllText(Path.Combine(root, "Client", "Assets", "Editor", "AutoOpenConnectionScene.cs"));
            var editorBuildSettings = File.ReadAllText(Path.Combine(root, "Client", "ProjectSettings", "EditorBuildSettings.asset"));
            var starterGeneratedAsmdefPath = Path.Combine(root, "Client", "Assets", "Scripts", "Rpc", "Generated", "ULinkRPC.Generated.asmdef");

            Assert.Equal("file:../../Shared", sharedDependency);
            Assert.DoesNotContain("Bad Project Name", serverProgram, StringComparison.Ordinal);
            Assert.DoesNotContain("DateTimeOffset", serverProgram, StringComparison.Ordinal);
            Assert.Contains("using ULinkRPC.Core;", serverProgram);
            Assert.Contains("using ULinkRPC.Server;", serverProgram);
            Assert.Contains("using ULinkRPC.Serializer.Json;", serverProgram);
            Assert.Contains("using ULinkRPC.Transport.WebSocket;", serverProgram);
            Assert.Contains("var commandLineArgs = Environment.GetCommandLineArgs().Skip(1).ToArray();", serverProgram);
            Assert.Contains("var builder = RpcServerHostBuilder.Create()", serverProgram);
            Assert.Contains(".UseCommandLine(commandLineArgs)", serverProgram);
            Assert.Contains(".UseSerializer(new JsonRpcSerializer())", serverProgram);
            Assert.Contains("builder.UseAcceptor(async ct => await WsConnectionAcceptor.CreateAsync(builder.ResolvePort(20000), \"/ws\", builder.Limits.MaxPendingAcceptedConnections, ct));", serverProgram);
            Assert.Contains("await builder.RunAsync();", serverProgram);
            Assert.True(File.Exists(pingServicePath));
            Assert.False(File.Exists(Path.Combine(root, "Server", "Server", "PingService.cs")));
            Assert.Contains("public sealed class PingService : IPingService", pingService);
            Assert.Contains("ServerTimeUtc = DateTime.UtcNow.ToString(\"O\")", pingService);
            Assert.Contains("<RootNamespace>Server</RootNamespace>", serverCsproj);
            Assert.Contains("<ProjectReference Include=\"..\\..\\Shared\\Shared.csproj\" />", serverCsproj);
            Assert.Contains("<package id=\"ULinkRPC.Core\" version=\"1.2.3\" />", packagesConfig);
            Assert.Contains("<package id=\"ULinkRPC.Transport.WebSocket\" version=\"4.5.6\" manuallyInstalled=\"true\" />", packagesConfig);
            Assert.Contains("<package id=\"ULinkRPC.Serializer.Json\" version=\"5.6.7\" manuallyInstalled=\"true\" />", packagesConfig);
            Assert.Contains("<package id=\"Microsoft.Bcl.AsyncInterfaces\" version=\"10.0.7\" />", packagesConfig);
            Assert.Contains("<package id=\"System.IO.Pipelines\" version=\"10.0.6\" />", packagesConfig);
            Assert.Contains("<package id=\"System.Text.Encodings.Web\" version=\"10.0.7\" />", packagesConfig);
            Assert.Contains("<package id=\"System.Buffers\" version=\"4.6.1\" />", packagesConfig);
            Assert.Contains("<package id=\"System.Memory\" version=\"4.6.3\" />", packagesConfig);
            Assert.Contains("<package id=\"System.Runtime.CompilerServices.Unsafe\" version=\"6.1.2\" />", packagesConfig);
            Assert.Contains("<package id=\"System.Threading.Tasks.Extensions\" version=\"4.6.3\" />", packagesConfig);
            Assert.Contains("<package id=\"System.Text.Json\" version=\"10.0.7\" />", packagesConfig);
            Assert.Contains("<disabledPackageSources />", nugetConfig);
            Assert.Contains("<activePackageSource>", nugetConfig);
            Assert.Contains("<add key=\"All\" value=\"(Aggregate source)\" />", nugetConfig);
            Assert.Contains("m_EditorVersion: 2022.3.62f3c1", projectVersion);
            Assert.Contains("m_EditorVersionWithRevision: 2022.3.62f3c1 (1623fc0bbb97)", projectVersion);
            Assert.Contains("the editor will auto-open `Assets/Scenes/ConnectionTest.unity`", clientReadme);
            Assert.Contains("using Rpc.Generated;", testerScript);
            Assert.Contains("using Shared.Interfaces;", testerScript);
            Assert.Contains("using ULinkRPC.Transport.WebSocket;", testerScript);
            Assert.Contains("using ULinkRPC.Serializer.Json;", testerScript);
            Assert.Contains("new WsTransport($\"ws://{_endpoint.Host}:{_endpoint.Port}{NormalizePath(_endpoint.Path)}\")", testerScript);
            Assert.Contains("new JsonRpcSerializer()", testerScript);
            Assert.Contains("_client.Api.Shared.Ping.PingAsync", testerScript);
            Assert.DoesNotContain("}, _cts.Token);", testerScript, StringComparison.Ordinal);
            Assert.Contains("                });", testerScript);
            Assert.Contains("public string Path = string.Empty;", testerScript);
            Assert.Contains("Path = \"/ws\"", testerScript);
            Assert.Contains("guid: 8fbb7dbe54784d7995143ce24cf85121", testerScriptMeta);
            Assert.Contains("guid: 8fbb7dbe54784d7995143ce24cf85121", scene);
            Assert.Contains("Path: /ws", scene);
            Assert.Contains("m_Name: RpcConnectionTester", scene);
            Assert.Contains("[InitializeOnLoad]", autoOpenSceneScript);
            Assert.Contains("EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);", autoOpenSceneScript);
            Assert.Contains("SessionState.GetBool(SessionStateKey, false)", autoOpenSceneScript);
            Assert.Contains("Assets/Scenes/ConnectionTest.unity", editorBuildSettings);
            Assert.Contains("guid: d4d2d5faafe942e58a33f4a41e3b7cf2", editorBuildSettings);
            Assert.True(File.Exists(starterGeneratedAsmdefPath));
            Assert.Contains("\"name\": \"ULinkRPC.Generated\"", File.ReadAllText(starterGeneratedAsmdefPath));
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

            generator.GenerateTemplate(root, "Builder-Test", ClientEngineKind.Unity, TransportKind.Tcp, SerializerKind.MemoryPack, Versions);

            var serverProgram = File.ReadAllText(Path.Combine(root, "Server", "Server", "Program.cs"));

            Assert.Contains("using ULinkRPC.Core;", serverProgram);
            Assert.Contains("using ULinkRPC.Serializer.MemoryPack;", serverProgram);
            Assert.Contains("using ULinkRPC.Transport.Tcp;", serverProgram);
            Assert.Contains(".UseSerializer(new MemoryPackRpcSerializer())", serverProgram);
            Assert.Contains("builder.UseAcceptor(new TcpConnectionAcceptor(builder.ResolvePort(20000)));", serverProgram);
            Assert.DoesNotContain(".UseJson()", serverProgram, StringComparison.Ordinal);
            Assert.DoesNotContain(".UseWebSocket(", serverProgram, StringComparison.Ordinal);
            Assert.DoesNotContain(".UseMemoryPack()", serverProgram, StringComparison.Ordinal);
            Assert.DoesNotContain(".UseTcp(", serverProgram, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void GenerateTemplate_CreatesMemoryPackClientPackages_WithRequiredUnityDependencies()
    {
        var root = CreateTempRoot();
        try
        {
            var generator = new StarterTemplateGenerator(CreateFakeDotNetRunner(), CreateFakeGitRunner());

            generator.GenerateTemplate(root, "MemoryPack-Test", ClientEngineKind.Unity, TransportKind.Tcp, SerializerKind.MemoryPack, Versions);

            var packagesConfig = File.ReadAllText(Path.Combine(root, "Client", "Assets", "packages.config"));

            Assert.Contains("<package id=\"ULinkRPC.Core\" version=\"1.2.3\" />", packagesConfig);
            Assert.Contains("<package id=\"ULinkRPC.Transport.Tcp\" version=\"4.5.6\" manuallyInstalled=\"true\" />", packagesConfig);
            Assert.Contains("<package id=\"ULinkRPC.Serializer.MemoryPack\" version=\"5.6.7\" manuallyInstalled=\"true\" />", packagesConfig);
            Assert.Contains("<package id=\"MemoryPack\" version=\"6.7.8\" />", packagesConfig);
            Assert.Contains("<package id=\"MemoryPack.Core\" version=\"8.9.10\" />", packagesConfig);
            Assert.Contains("<package id=\"MemoryPack.Generator\" version=\"6.7.8\" />", packagesConfig);
            Assert.Contains("<package id=\"Microsoft.CodeAnalysis.Common\" version=\"5.3.0\" />", packagesConfig);
            Assert.Contains("<package id=\"Microsoft.CodeAnalysis.CSharp\" version=\"5.3.0\" />", packagesConfig);
            Assert.Contains("<package id=\"System.Collections.Immutable\" version=\"10.0.6\" />", packagesConfig);
            Assert.Contains("<package id=\"System.Reflection.Metadata\" version=\"10.0.7\" />", packagesConfig);
            Assert.Contains("<package id=\"System.Text.Encoding.CodePages\" version=\"10.0.7\" />", packagesConfig);
            Assert.Contains("<package id=\"System.Threading.Tasks.Extensions\" version=\"4.5.4\" />", packagesConfig);
            Assert.Contains("<package id=\"System.Memory\" version=\"4.5.4\" />", packagesConfig);
            Assert.Contains("<package id=\"System.Runtime.CompilerServices.Unsafe\" version=\"6.1.2\" />", packagesConfig);
            Assert.Contains("<package id=\"System.IO.Pipelines\" version=\"10.0.6\" />", packagesConfig);
            var sharedCsproj = File.ReadAllText(Path.Combine(root, "Shared", "Shared.csproj"));
            Assert.Contains("<PackageReference Include=\"ULinkRPC.Serializer.MemoryPack\" Version=\"5.6.7\" />", sharedCsproj);
            Assert.Contains("<PackageReference Include=\"MemoryPack\" Version=\"6.7.8\" />", sharedCsproj);
            Assert.Contains("<PackageReference Include=\"MemoryPack.Generator\" Version=\"6.7.8\">", sharedCsproj);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void GenerateTemplate_CreatesKcpClientPackages_WithRequiredUnityDependencies()
    {
        var root = CreateTempRoot();
        try
        {
            var generator = new StarterTemplateGenerator(CreateFakeDotNetRunner(), CreateFakeGitRunner());

            generator.GenerateTemplate(root, "Kcp-Test", ClientEngineKind.Unity, TransportKind.Kcp, SerializerKind.MemoryPack, Versions);

            var packagesConfig = File.ReadAllText(Path.Combine(root, "Client", "Assets", "packages.config"));
            var testerScript = File.ReadAllText(Path.Combine(root, "Client", "Assets", "Scripts", "Rpc", "Testing", "RpcConnectionTester.cs"));
            var scene = File.ReadAllText(Path.Combine(root, "Client", "Assets", "Scenes", "ConnectionTest.unity"));
            var sharedDtos = File.ReadAllText(Path.Combine(root, "Shared", "Interfaces", "SharedDtos.cs"));

            Assert.Contains("<package id=\"ULinkRPC.Transport.Kcp\" version=\"4.5.6\" manuallyInstalled=\"true\" />", packagesConfig);
            Assert.Contains("<package id=\"Kcp\" version=\"2.7.0\" />", packagesConfig);
            Assert.Contains("<package id=\"System.Memory\" version=\"4.5.4\" />", packagesConfig);
            Assert.Contains("<package id=\"System.Threading.Tasks.Extensions\" version=\"4.5.4\" />", packagesConfig);
            Assert.Contains("<package id=\"ULinkRPC.Serializer.MemoryPack\" version=\"5.6.7\" manuallyInstalled=\"true\" />", packagesConfig);
            Assert.Contains("<package id=\"MemoryPack\" version=\"6.7.8\" />", packagesConfig);
            Assert.Contains("using ULinkRPC.Transport.Kcp;", testerScript);
            Assert.Contains("using ULinkRPC.Serializer.MemoryPack;", testerScript);
            Assert.Contains("new KcpTransport(_endpoint.Host, _endpoint.Port)", testerScript);
            Assert.Contains("new MemoryPackRpcSerializer()", testerScript);
            Assert.Contains("[MemoryPackable]", sharedDtos);
            Assert.DoesNotContain("GenerateType.VersionTolerant", sharedDtos, StringComparison.Ordinal);
            Assert.Contains("public sealed partial class PingRequest", sharedDtos);
            Assert.Contains("public sealed partial class PingReply", sharedDtos);
            Assert.Contains("Path: ", scene);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void GenerateTemplate_CreatesGodotClientFiles_AndRunsGodotCodeGen()
    {
        var root = CreateTempRoot();
        try
        {
            var commands = new List<string>();
            var generator = new StarterTemplateGenerator(CreateFakeDotNetRunner(commands), CreateFakeGitRunner());
            var sdkSource = CreateFakeGodotSdkSource(root, "4.6.1");

            WithGodotSdkSource(
                sdkSource,
                () => generator.GenerateTemplate(root, "Godot-Test", ClientEngineKind.Godot, TransportKind.WebSocket, SerializerKind.Json, Versions));

            var projectFile = File.ReadAllText(Path.Combine(root, "Client", "project.godot"));
            var clientCsproj = File.ReadAllText(Path.Combine(root, "Client", "Client.csproj"));
            var nugetConfig = File.ReadAllText(Path.Combine(root, "Client", "NuGet.config"));
            var clientReadme = File.ReadAllText(Path.Combine(root, "Client", "README.md"));
            var scene = File.ReadAllText(Path.Combine(root, "Client", "Main.tscn"));
            var testerScript = File.ReadAllText(Path.Combine(root, "Client", "Scripts", "Rpc", "Testing", "RpcConnectionTester.cs"));
            var generatedClientApi = Path.Combine(root, "Client", "Scripts", "Rpc", "Generated", "RpcApi.cs");
            var codegenScript = File.ReadAllText(Path.Combine(root, "codegen.ps1"));
            var codegenShellScript = File.ReadAllText(Path.Combine(root, "codegen.sh"));

            Assert.Contains($"tool run ulinkrpc-codegen -- --contracts \"{Path.Combine(root, "Shared")}\" --mode godot --output \"Scripts{Path.DirectorySeparatorChar}Rpc{Path.DirectorySeparatorChar}Generated\" --namespace \"Rpc.Generated\"", commands);
            Assert.Contains("config/name=\"Godot-Test\"", projectFile);
            Assert.Contains("run/main_scene=\"res://Main.tscn\"", projectFile);
            Assert.Contains("config/features=PackedStringArray(\"4.6\", \"C#\")", projectFile);
            Assert.Contains("project/assembly_name=\"Client\"", projectFile);
            Assert.DoesNotContain("websocket", projectFile, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("json", projectFile, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("<Project Sdk=\"Godot.NET.Sdk/4.6.1\">", clientCsproj);
            Assert.Contains("<TargetFramework>net8.0</TargetFramework>", clientCsproj);
            Assert.Contains("<CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>", clientCsproj);
            Assert.Contains("<NuGetAudit>false</NuGetAudit>", clientCsproj);
            Assert.Contains("<ProjectReference Include=\"..\\Shared\\Shared.csproj\" />", clientCsproj);
            Assert.Contains("<PackageReference Include=\"ULinkRPC.Transport.WebSocket\" Version=\"4.5.6\" />", clientCsproj);
            Assert.Contains("<PackageReference Include=\"ULinkRPC.Serializer.Json\" Version=\"5.6.7\" />", clientCsproj);
            Assert.Contains("<add key=\"godot-local\" value=\"" + sdkSource + "\" />", nugetConfig);
            Assert.Contains("<TargetFrameworks>net8.0;net10.0</TargetFrameworks>", File.ReadAllText(Path.Combine(root, "Shared", "Shared.csproj")));
            Assert.Contains("Godot 4.6", clientReadme);
            Assert.Contains(sdkSource, clientReadme);
            Assert.Contains("[node name=\"Main\" type=\"Node\"]", scene);
            Assert.Contains("path=\"res://Scripts/Rpc/Testing/RpcConnectionTester.cs\"", scene);
            Assert.Contains("using Godot;", testerScript);
            Assert.Contains("using ULinkRPC.Transport.WebSocket;", testerScript);
            Assert.Contains("using ULinkRPC.Serializer.Json;", testerScript);
            Assert.DoesNotContain("namespace Rpc.Testing", testerScript, StringComparison.Ordinal);
            Assert.Contains("public partial class RpcConnectionTester : Node", testerScript);
            Assert.Contains("new WsTransport($\"ws://{_host}:{_port}{NormalizePath(_path)}\")", testerScript);
            Assert.Contains("new JsonRpcSerializer()", testerScript);
            Assert.Contains("GD.Print($\"Ping ok:", testerScript);
            Assert.Contains("[Export] private string _path = \"/ws\";", testerScript);
            Assert.Contains("public override void _Ready()", testerScript);
            Assert.Contains("CallDeferred(MethodName.BeginAutoConnect);", testerScript);
            Assert.Contains("private async void BeginAutoConnect()", testerScript);
            Assert.Contains("public override void _ExitTree()", testerScript);
            Assert.Contains("_ = ShutdownAsync();", testerScript);
            Assert.Contains("--mode', 'godot'", codegenScript);
            Assert.Contains("Scripts\\Rpc\\Generated", codegenScript);
            Assert.Contains("--mode godot", codegenShellScript);
            Assert.Contains("--output \"Scripts/Rpc/Generated\"", codegenShellScript);
            Assert.True(File.Exists(generatedClientApi));
            Assert.False(File.Exists(Path.Combine(root, "Client", "Assets", "Scripts", "Rpc", "Generated", "RpcApi.cs")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void GenerateTemplate_CreatesGodotKcpMemoryPackClient_WithExpectedReferencesAndScript()
    {
        var root = CreateTempRoot();
        try
        {
            var generator = new StarterTemplateGenerator(CreateFakeDotNetRunner(), CreateFakeGitRunner());
            var sdkSource = CreateFakeGodotSdkSource(root, "4.6.1");

            WithGodotSdkSource(
                sdkSource,
                () => generator.GenerateTemplate(root, "Godot-Kcp", ClientEngineKind.Godot, TransportKind.Kcp, SerializerKind.MemoryPack, Versions));

            var projectFile = File.ReadAllText(Path.Combine(root, "Client", "project.godot"));
            var clientCsproj = File.ReadAllText(Path.Combine(root, "Client", "Client.csproj"));
            var nugetConfig = File.ReadAllText(Path.Combine(root, "Client", "NuGet.config"));
            var testerScript = File.ReadAllText(Path.Combine(root, "Client", "Scripts", "Rpc", "Testing", "RpcConnectionTester.cs"));
            var generatedClientApi = Path.Combine(root, "Client", "Scripts", "Rpc", "Generated", "RpcApi.cs");

            Assert.Contains("config/features=PackedStringArray(\"4.6\", \"C#\")", projectFile);
            Assert.Contains("project/assembly_name=\"Client\"", projectFile);
            Assert.DoesNotContain("config/features=PackedStringArray(\"4.6\", \"C#\", \"kcp\"", projectFile, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("config/features=PackedStringArray(\"4.6\", \"C#\", \"memorypack\"", projectFile, StringComparison.OrdinalIgnoreCase);

            Assert.Contains("<Project Sdk=\"Godot.NET.Sdk/4.6.1\">", clientCsproj);
            Assert.Contains("<CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>", clientCsproj);
            Assert.Contains("<NuGetAudit>false</NuGetAudit>", clientCsproj);
            Assert.Contains("<PackageReference Include=\"ULinkRPC.Transport.Kcp\" Version=\"4.5.6\" />", clientCsproj);
            Assert.Contains("<PackageReference Include=\"ULinkRPC.Serializer.MemoryPack\" Version=\"5.6.7\" />", clientCsproj);
            Assert.Contains("<PackageReference Include=\"MemoryPack\" Version=\"6.7.8\" />", clientCsproj);
            Assert.Contains("<PackageReference Include=\"MemoryPack.Core\" Version=\"8.9.10\" />", clientCsproj);
            Assert.Contains("<add key=\"godot-local\" value=\"" + sdkSource + "\" />", nugetConfig);
            Assert.Contains("<TargetFrameworks>net8.0;net10.0</TargetFrameworks>", File.ReadAllText(Path.Combine(root, "Shared", "Shared.csproj")));

            Assert.Contains("using ULinkRPC.Transport.Kcp;", testerScript);
            Assert.Contains("using ULinkRPC.Serializer.MemoryPack;", testerScript);
            Assert.DoesNotContain("namespace Rpc.Testing", testerScript, StringComparison.Ordinal);
            Assert.Contains("public partial class RpcConnectionTester : Node", testerScript);
            Assert.Contains("new KcpTransport(_host, _port)", testerScript);
            Assert.Contains("new MemoryPackRpcSerializer()", testerScript);
            Assert.Contains("[Export] private string _path = \"\";", testerScript);
            Assert.Contains("if (_isShuttingDown || _client is not null)", testerScript);
            Assert.True(File.Exists(generatedClientApi));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void GenerateTemplate_CreatesTuanjieClientFiles_UsingUnityCompatibleTemplateAndCodeGen()
    {
        var root = CreateTempRoot();
        try
        {
            var commands = new List<string>();
            var generator = new StarterTemplateGenerator(CreateFakeDotNetRunner(commands), CreateFakeGitRunner());

            generator.GenerateTemplate(root, "Tuanjie-Test", ClientEngineKind.Tuanjie, TransportKind.Tcp, SerializerKind.Json, Versions);

            var sharedCsproj = File.ReadAllText(Path.Combine(root, "Shared", "Shared.csproj"));
            var clientReadme = File.ReadAllText(Path.Combine(root, "Client", "README.md"));
            var manifestJson = File.ReadAllText(Path.Combine(root, "Client", "Packages", "manifest.json"));
            var projectVersion = File.ReadAllText(Path.Combine(root, "Client", "ProjectSettings", "ProjectVersion.txt"));
            var nugetConfig = File.ReadAllText(Path.Combine(root, "Client", "Assets", "NuGet.config"));
            var generatedClientApi = Path.Combine(root, "Client", "Assets", "Scripts", "Rpc", "Generated", "RpcApi.cs");

            Assert.Contains("<TargetFrameworks>netstandard2.1;net10.0</TargetFrameworks>", sharedCsproj);
            Assert.Contains("Tuanjie Client Starter (Tuanjie (Unity-compatible))", clientReadme);
            Assert.Contains("Open this folder with Tuanjie (Unity-compatible).", clientReadme);
            Assert.Contains("file:../../Shared", manifestJson);
            Assert.Contains("m_EditorVersion: 2022.3.61t11", projectVersion);
            Assert.Contains("m_EditorVersionWithRevision: 2022.3.61t11 (122146d53e32)", projectVersion);
            Assert.Contains("m_TuanjieEditorVersion: 1.6.10", projectVersion);
            Assert.Contains("<add key=\"nuget.org\" value=\"https://nuget.cdn.azure.cn/v3/index.json\" enableCredentialProvider=\"false\" />", nugetConfig);
            Assert.Contains($"tool run ulinkrpc-codegen -- --contracts \"{Path.Combine(root, "Shared")}\" --mode unity --output \"Assets{Path.DirectorySeparatorChar}Scripts{Path.DirectorySeparatorChar}Rpc{Path.DirectorySeparatorChar}Generated\" --namespace \"Rpc.Generated\"", commands);
            Assert.True(File.Exists(generatedClientApi));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData("tuanjie")]
    [InlineData("unity-china")]
    [InlineData("unitycn")]
    public void TryParseArgs_ParsesTuanjieClientEngineAliases(string rawClientEngine)
    {
        var ok = StarterCli.TryParseArgs(
            ["--client-engine", rawClientEngine],
            out var options,
            out var error);

        Assert.True(ok);
        Assert.Equal(string.Empty, error);
        Assert.Equal(ClientEngineKind.Tuanjie, options.ClientEngine);
    }


    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), $"ulinkrpc_starter_{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        return root;
    }

    private static string CreateFakeGodotSdkSource(string root, string version)
    {
        var dir = Path.Combine(root, "fake-godot-sdk");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, $"Godot.NET.Sdk.{version}.nupkg"), string.Empty);
        return dir;
    }

    private static void WithGodotSdkSource(string path, Action action)
    {
        var previous = Environment.GetEnvironmentVariable("ULINKRPC_GODOT_NUPKGS");
        Environment.SetEnvironmentVariable("ULINKRPC_GODOT_NUPKGS", path);

        try
        {
            action();
        }
        finally
        {
            Environment.SetEnvironmentVariable("ULINKRPC_GODOT_NUPKGS", previous);
        }
    }

    private static Action<string, string> CreateFakeDotNetRunner(List<string>? commands = null)
    {
        return (workingDirectory, arguments) =>
        {
            commands?.Add(arguments);

            if (arguments.StartsWith("new sln -n ", StringComparison.Ordinal))
            {
                var nameAndOptions = arguments["new sln -n ".Length..];
                var formatIndex = nameAndOptions.IndexOf(" --format ", StringComparison.Ordinal);
                var solutionName = (formatIndex >= 0 ? nameAndOptions[..formatIndex] : nameAndOptions)
                    .Trim()
                    .Trim('"');
                var slnxPath = Path.Combine(workingDirectory, $"{solutionName}.slnx");
                File.WriteAllText(slnxPath, "<Solution>\n</Solution>\n");
                return;
            }

            if (string.Equals(arguments, "new tool-manifest", StringComparison.Ordinal))
            {
                var toolDir = Path.Combine(workingDirectory, ".config");
                Directory.CreateDirectory(toolDir);
                File.WriteAllText(Path.Combine(toolDir, "dotnet-tools.json"), "{ }\n");
                return;
            }

            if (arguments.StartsWith("tool install ULinkRPC.CodeGen --version ", StringComparison.Ordinal))
            {
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

            if (arguments.StartsWith("tool run ulinkrpc-codegen -- ", StringComparison.Ordinal))
            {
                if (arguments.Contains("--mode server", StringComparison.Ordinal))
                {
                    var outputDir = Path.Combine(workingDirectory, "Generated");
                    Directory.CreateDirectory(outputDir);
                    File.WriteAllText(Path.Combine(outputDir, "AllServicesBinder.cs"), "// generated\n");
                    return;
                }

                if (arguments.Contains("--mode unity", StringComparison.Ordinal))
                {
                    var outputDir = Path.Combine(workingDirectory, "Assets", "Scripts", "Rpc", "Generated");
                    Directory.CreateDirectory(outputDir);
                    File.WriteAllText(Path.Combine(outputDir, "RpcApi.cs"), "// generated\n");
                    File.WriteAllText(
                        Path.Combine(outputDir, "ULinkRPC.Generated.asmdef"),
                        """
{
  "name": "ULinkRPC.Generated"
}
""");
                    return;
                }

                if (arguments.Contains("--mode godot", StringComparison.Ordinal))
                {
                    var outputDir = Path.Combine(workingDirectory, "Scripts", "Rpc", "Generated");
                    Directory.CreateDirectory(outputDir);
                    File.WriteAllText(Path.Combine(outputDir, "RpcApi.cs"), "// generated\n");
                    return;
                }
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
