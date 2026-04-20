namespace ULinkRPC.Starter;

internal static class StarterGodotTemplate
{
    public static void Generate(StarterTemplateContext context)
    {
        EnsureClientDirectories(context.Paths.ClientPath);

        StarterFileWriter.Write(Path.Combine(context.Paths.ClientPath, "project.godot"), BuildProjectFile(context));
        StarterFileWriter.Write(Path.Combine(context.Paths.ClientPath, "Client.csproj"), BuildClientProject(context));
        StarterFileWriter.Write(Path.Combine(context.Paths.ClientPath, "README.md"), BuildReadme(context));
        StarterFileWriter.Write(Path.Combine(context.Paths.ClientPath, "Main.tscn"), BuildMainScene());
        StarterFileWriter.Write(Path.Combine(context.Paths.ClientPath, "Scripts", "Rpc", "Testing", "RpcConnectionTester.cs"), BuildTesterScript(context));
    }

    private static void EnsureClientDirectories(string clientPath)
    {
        Directory.CreateDirectory(Path.Combine(clientPath, "Scripts"));
        Directory.CreateDirectory(Path.Combine(clientPath, "Scripts", "Rpc", "Generated"));
        Directory.CreateDirectory(Path.Combine(clientPath, "Scripts", "Rpc", "Testing"));
    }

    private static string BuildProjectFile(StarterTemplateContext context) => $$"""
; Engine configuration file.
config_version=5

[application]
config/name="{{context.ProjectName}}"
run/main_scene="res://Main.tscn"
config/features=PackedStringArray("4.4", "C#")
""";

    private static string BuildClientProject(StarterTemplateContext context)
    {
        var transportPackage = NuGetVersionResolver.GetTransportPackage(context.Transport);
        var serializerPackage = NuGetVersionResolver.GetSerializerPackage(context.Serializer);
        var serializerRuntimeReferences = context.Serializer == SerializerKind.MemoryPack
            ? $$"""
    <PackageReference Include="MemoryPack" Version="{{context.Versions.SerializerRuntime}}" />
    <PackageReference Include="MemoryPack.Core" Version="{{context.Versions.SerializerRuntimeCore}}" />
"""
            : string.Empty;

        return $$"""
<Project Sdk="Godot.NET.Sdk/4.4.1">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <RootNamespace>Client</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\Shared\Shared.csproj" />
    <PackageReference Include="ULinkRPC.Core" Version="{{context.Versions.Core}}" />
    <PackageReference Include="ULinkRPC.Client" Version="{{context.Versions.Client}}" />
    <PackageReference Include="{{transportPackage}}" Version="{{context.Versions.Transport}}" />
    <PackageReference Include="{{serializerPackage}}" Version="{{context.Versions.Serializer}}" />
{{serializerRuntimeReferences}}</ItemGroup>
</Project>
""";
    }

    private static string BuildReadme(StarterTemplateContext context) => $$"""
# Godot Client Starter (Godot 4.x)

1. Open this folder with Godot 4.x.
2. Let Godot restore the C# solution, or run `dotnet restore Client.csproj`.
3. Build the project once so generated assemblies load.
4. Open `Main.tscn` and press Play to run the default connection example.

Selected transport: {{context.Transport}}
Selected serializer: {{context.Serializer}}
""";

    private static string BuildMainScene() => """
[gd_scene load_steps=2 format=3]

[ext_resource type="Script" path="res://Scripts/Rpc/Testing/RpcConnectionTester.cs" id="1"]

[node name="Main" type="Node"]
script = ExtResource("1")
""";

    private static string BuildTesterScript(StarterTemplateContext context)
    {
        var transportUsing = context.Transport switch
        {
            TransportKind.Tcp => "using ULinkRPC.Transport.Tcp;",
            TransportKind.WebSocket => "using ULinkRPC.Transport.WebSocket;",
            TransportKind.Kcp => "using ULinkRPC.Transport.Kcp;",
            _ => throw new ArgumentOutOfRangeException()
        };

        var serializerUsing = context.Serializer switch
        {
            SerializerKind.Json => "using ULinkRPC.Serializer.Json;",
            SerializerKind.MemoryPack => "using ULinkRPC.Serializer.MemoryPack;",
            _ => throw new ArgumentOutOfRangeException()
        };

        var transportConstruction = context.Transport switch
        {
            TransportKind.Tcp => "new TcpTransport(_host, _port)",
            TransportKind.WebSocket => "new WsTransport($\"ws://{_host}:{_port}{NormalizePath(_path)}\")",
            TransportKind.Kcp => "new KcpTransport(_host, _port)",
            _ => throw new ArgumentOutOfRangeException()
        };

        var serializerConstruction = context.Serializer switch
        {
            SerializerKind.Json => "new JsonRpcSerializer()",
            SerializerKind.MemoryPack => "new MemoryPackRpcSerializer()",
            _ => throw new ArgumentOutOfRangeException()
        };

        var defaultPath = context.Transport == TransportKind.WebSocket ? "/ws" : string.Empty;

        return $$"""
#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using Rpc.Generated;
using Shared.Interfaces;
using ULinkRPC.Client;
{{transportUsing}}
{{serializerUsing}}

namespace Rpc.Testing;

public partial class RpcConnectionTester : Node
{
    [Export] private string _host = "127.0.0.1";
    [Export] private int _port = 20000;
    [Export] private string _path = "{{defaultPath}}";
    [Export] private string _message = "hello";
    [Export] private bool _autoConnect = true;

    private readonly CancellationTokenSource _cts = new();
    private RpcClient? _client;
    private bool _isShuttingDown;

    public override void _Ready()
    {
        if (!_autoConnect)
            return;

        CallDeferred(MethodName.BeginAutoConnect);
    }

    public override void _ExitTree()
    {
        _ = ShutdownAsync();
    }

    private async void BeginAutoConnect()
    {
        await ConnectAndPingAsync();
    }

    public async Task ConnectAndPingAsync()
    {
        if (_isShuttingDown || _client is not null)
            return;

        GD.Print($"Connecting to {DescribeEndpoint()}");

        try
        {
            _client = new RpcClient(new RpcClientOptions(
                {{transportConstruction}},
                {{serializerConstruction}}));

            await _client.ConnectAsync(_cts.Token);

            var reply = await _client.Api.Shared.Ping.PingAsync(new PingRequest
            {
                Message = _message
            });

            GD.Print($"Ping ok: message={reply.Message}, serverTimeUtc={reply.ServerTimeUtc}");
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            GD.PushError($"Connect failed: {ex}");
            await ShutdownAsync();
        }
    }

    private string DescribeEndpoint()
    {
        var path = NormalizePath(_path);
        return string.IsNullOrEmpty(path) ? $"{_host}:{_port}" : $"{_host}:{_port}{path}";
    }

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        return path.StartsWith("/", StringComparison.Ordinal) ? path : "/" + path;
    }

    private async Task ShutdownAsync()
    {
        if (_isShuttingDown)
            return;

        _isShuttingDown = true;
        _cts.Cancel();

        if (_client is not null)
        {
            await _client.DisposeAsync();
            _client = null;
        }

        _cts.Dispose();
    }
}
""";
    }
}
