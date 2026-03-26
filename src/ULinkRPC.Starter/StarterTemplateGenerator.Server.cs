namespace ULinkRPC.Starter;

internal static class StarterServerTemplate
{
    public static void Generate(StarterTemplateContext context)
    {
        var serverPath = context.Paths.ServerAppPath;
        var serverProjectName = Path.GetFileName(serverPath);
        var servicesPath = Path.Combine(serverPath, "Services");
        Directory.CreateDirectory(servicesPath);

        StarterFileWriter.Write(Path.Combine(serverPath, $"{serverProjectName}.csproj"), BuildServerProjectFile(context));
        StarterFileWriter.Write(Path.Combine(serverPath, "Program.cs"), BuildServerProgramSource(context.Serializer, context.Transport));
        StarterFileWriter.Write(Path.Combine(servicesPath, "PingService.cs"), BuildPingServiceSource());
    }

    private static string BuildServerProjectFile(StarterTemplateContext context)
    {
        var transportPackage = NuGetVersionResolver.GetTransportPackage(context.Transport);
        var serializerPackage = NuGetVersionResolver.GetSerializerPackage(context.Serializer);

        return $$"""
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RootNamespace>Server</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\Shared\Shared.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="ULinkRPC.Server" Version="{{context.Versions.Server}}" />
    <PackageReference Include="{{transportPackage}}" Version="{{context.Versions.Transport}}" />
    <PackageReference Include="{{serializerPackage}}" Version="{{context.Versions.Serializer}}" />
  </ItemGroup>
</Project>
""";
    }

    private static string BuildServerProgramSource(SerializerKind serializer, TransportKind transport) => $$"""
{{GetServerProgramUsings(serializer, transport)}}

{{GetServerProgramBody(serializer, transport)}}
""";

    private static string BuildPingServiceSource() => """
using Shared.Interfaces;

namespace Server.Services
{
    public sealed class PingService : IPingService
    {
        public ValueTask<PingReply> PingAsync(PingRequest request)
        {
            return ValueTask.FromResult(new PingReply
            {
                Message = string.IsNullOrWhiteSpace(request.Message) ? "pong" : "pong: " + request.Message,
                ServerTimeUtc = DateTime.UtcNow.ToString("O")
            });
        }
    }
}
""";

    private static string GetServerSerializerConstruction(SerializerKind serializer) => serializer switch
    {
        SerializerKind.Json => "new JsonRpcSerializer()",
        SerializerKind.MemoryPack => "new MemoryPackRpcSerializer()",
        _ => throw new ArgumentOutOfRangeException(nameof(serializer), serializer, null)
    };

    private static string GetServerTransportConstruction(TransportKind transport) => transport switch
    {
        TransportKind.Tcp => "builder.UseAcceptor(new TcpConnectionAcceptor(builder.ResolvePort(20000)));",
        TransportKind.WebSocket => "builder.UseAcceptor(async ct => await WsConnectionAcceptor.CreateAsync(builder.ResolvePort(20000), \"/ws\", ct));",
        TransportKind.Kcp => "builder.UseAcceptor(new KcpConnectionAcceptor(builder.ResolvePort(20000)));",
        _ => throw new ArgumentOutOfRangeException(nameof(transport), transport, null)
    };

    private static string GetServerProgramUsings(SerializerKind serializer, TransportKind transport)
    {
        var lines = new List<string>
        {
            "using ULinkRPC.Core;",
            "using ULinkRPC.Server;"
        };

        lines.Add(serializer switch
        {
            SerializerKind.Json => "using ULinkRPC.Serializer.Json;",
            SerializerKind.MemoryPack => "using ULinkRPC.Serializer.MemoryPack;",
            _ => throw new ArgumentOutOfRangeException(nameof(serializer), serializer, null)
        });

        lines.Add(transport switch
        {
            TransportKind.Tcp => "using ULinkRPC.Transport.Tcp;",
            TransportKind.WebSocket => "using ULinkRPC.Transport.WebSocket;",
            TransportKind.Kcp => "using ULinkRPC.Transport.Kcp;",
            _ => throw new ArgumentOutOfRangeException(nameof(transport), transport, null)
        });

        return string.Join(Environment.NewLine, lines);
    }

    private static string GetServerProgramBody(SerializerKind serializer, TransportKind transport)
    {
        var serializerSetup = GetServerSerializerConstruction(serializer);
        var transportSetup = GetServerTransportConstruction(transport);
        return $$"""
var commandLineArgs = Environment.GetCommandLineArgs().Skip(1).ToArray();
var builder = RpcServerHostBuilder.Create()
    .UseCommandLine(commandLineArgs)
    .UseSerializer({{serializerSetup}});

{{transportSetup}}

await builder.RunAsync();
""";
    }
}
