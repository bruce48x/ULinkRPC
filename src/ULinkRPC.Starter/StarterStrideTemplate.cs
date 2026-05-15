namespace ULinkRPC.Starter;

internal static class StarterStrideTemplate
{
    public static void Generate(StarterTemplateContext context)
    {
        EnsureClientDirectories(context.Paths.ClientPath);

        StarterFileWriter.Write(Path.Combine(context.Paths.ClientPath, "Client.csproj"), BuildClientProject(context));
        StarterFileWriter.Write(Path.Combine(context.Paths.ClientPath, "README.md"), BuildReadme(context));
        StarterFileWriter.Write(Path.Combine(context.Paths.ClientPath, "Program.cs"), BuildProgram());
        StarterFileWriter.Write(Path.Combine(context.Paths.ClientPath, "Scripts", "Rpc", "Testing", "RpcConnectionTester.cs"), BuildTesterScript(context));
    }

    private static void EnsureClientDirectories(string clientPath)
    {
        Directory.CreateDirectory(Path.Combine(clientPath, "Scripts"));
        Directory.CreateDirectory(Path.Combine(clientPath, "Scripts", "Rpc", "Generated"));
        Directory.CreateDirectory(Path.Combine(clientPath, "Scripts", "Rpc", "Testing"));
    }

    private static string BuildClientProject(StarterTemplateContext context)
    {
        var packageReferences = RenderPackageReferences(StarterDependencyPlanner.Create(context, StarterProjectRole.StrideClient));

        return $$"""
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <RootNamespace>Client</RootNamespace>
    <CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>
    <NuGetAudit>false</NuGetAudit>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\Shared\Shared.csproj" />
{{packageReferences}}
  </ItemGroup>
</Project>
""";
    }

    private static string RenderPackageReferences(StarterDependencyPlan plan) =>
        string.Join(Environment.NewLine, plan.PackageReferences.Select(static reference =>
            $"    <PackageReference Include=\"{reference.Id}\" Version=\"{reference.Version}\" />"));

    private static string BuildReadme(StarterTemplateContext context) => $$"""
# Stride3D Client Starter (Stride 4.3 code-only)

1. Install .NET 10 SDK and the Stride 4.3 prerequisites.
2. Run `dotnet restore Client.csproj`.
3. Start the server from the project root: `dotnet run --project Server/Server/Server.csproj`.
4. Run this client: `dotnet run --project Client.csproj`.

The generated client uses the Stride Community Toolkit code-only workflow and creates a minimal 3D scene while it runs the RPC ping example.

Selected transport: {{context.Transport}}
Selected serializer: {{context.Serializer}}
""";

    private static string BuildProgram() => """
using Client.Rpc.Testing;
using Stride.CommunityToolkit.Bepu;
using Stride.CommunityToolkit.Engine;
using Stride.CommunityToolkit.Rendering.ProceduralModels;
using Stride.Core.Mathematics;
using Stride.Engine;

using var game = new Game();
await using var tester = new RpcConnectionTester();

game.Run(start: Start);

void Start(Scene rootScene)
{
    game.SetupBase3DScene();

    var entity = game.Create3DPrimitive(PrimitiveModelType.Capsule);
    entity.Transform.Position = new Vector3(0f, 8f, 0f);
    entity.Scene = rootScene;

    _ = tester.ConnectAndPingAsync();
}
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
using Rpc.Generated;
using Shared.Interfaces;
using ULinkRPC.Client;
using ULinkRPC.Core;
{{transportUsing}}
{{serializerUsing}}

namespace Client.Rpc.Testing;

public sealed class RpcConnectionTester : IAsyncDisposable
{
    private readonly string _host = "127.0.0.1";
    private readonly int _port = 20000;
    private readonly string _path = "{{defaultPath}}";
    private readonly string _message = "hello";
    private readonly CancellationTokenSource _cts = new();

    private RpcClient? _client;
    private bool _isDisposed;

    public async Task ConnectAndPingAsync()
    {
        if (_isDisposed || _client is not null)
            return;

        Console.WriteLine($"Connecting to {DescribeEndpoint()}");

        try
        {
            _client = new RpcClient(new RpcClientOptions(
                {{transportConstruction}},
                {{serializerConstruction}})
                .UseSecurity(ConfigureTransportSecurity));

            await _client.ConnectAsync(_cts.Token);

            var reply = await _client.Api.Shared.Ping.PingAsync(new PingRequest
            {
                Message = _message
            });

            Console.WriteLine($"Ping ok: message={reply.Message}, serverTimeUtc={reply.ServerTimeUtc}");
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Connect failed: {ex}");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        _cts.Cancel();

        if (_client is not null)
        {
            await _client.DisposeAsync();
            _client = null;
        }

        _cts.Dispose();
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

    private static void ConfigureTransportSecurity(TransportSecurityConfig security)
    {
        security.EnableCompression = false;
        security.CompressionThresholdBytes = 1024;
        security.EnableEncryption = false;
        security.EncryptionKeyBase64 = null;
    }
}
""";
    }
}
