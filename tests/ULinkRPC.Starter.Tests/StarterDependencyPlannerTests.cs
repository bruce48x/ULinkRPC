using ULinkRPC.Starter;
using Xunit;

namespace ULinkRPC.Starter.Tests;

public sealed class StarterDependencyPlannerTests
{
    private static readonly ResolvedVersions Versions = new("1.2.3", "2.3.4", "3.4.5", "4.5.6", "5.6.7", "0.1.2", "6.7.8", "7.8.9", "8.9.10");

    [Fact]
    public void SharedMemoryPack_IncludesSerializerRuntimeAndGenerator()
    {
        var ids = CreateIds(StarterProjectRole.Shared, SerializerKind.MemoryPack);

        Assert.Contains("ULinkRPC.Core", ids);
        Assert.Contains("ULinkRPC.Serializer.MemoryPack", ids);
        Assert.Contains("MemoryPack", ids);
        Assert.Contains("MemoryPack.Generator", ids);
    }

    [Fact]
    public void SharedJson_DoesNotIncludeJsonSerializer()
    {
        var ids = CreateIds(StarterProjectRole.Shared, SerializerKind.Json);

        Assert.Contains("ULinkRPC.Core", ids);
        Assert.DoesNotContain("ULinkRPC.Serializer.Json", ids);
    }

    [Fact]
    public void ServerMemoryPack_DoesNotRepeatSharedSerializerDependencies()
    {
        var ids = CreateIds(StarterProjectRole.Server, SerializerKind.MemoryPack);

        Assert.Contains("ULinkRPC.Server", ids);
        Assert.Contains("ULinkRPC.Transport.WebSocket", ids);
        Assert.DoesNotContain("ULinkRPC.Serializer.MemoryPack", ids);
        Assert.DoesNotContain("MemoryPack", ids);
        Assert.DoesNotContain("MemoryPack.Generator", ids);
    }

    [Fact]
    public void ServerJson_IncludesJsonSerializer()
    {
        var plan = CreatePlan(StarterProjectRole.Server, SerializerKind.Json);
        var ids = plan.PackageReferences.Select(static reference => reference.Id).ToArray();

        Assert.Contains("ULinkRPC.Serializer.Json", ids);
        Assert.Contains("ULinkRPC.Analyzers", ids);
        Assert.Contains(plan.PackageReferences, static reference =>
            reference.Id == "ULinkRPC.Analyzers" &&
            reference.PrivateAssets == "all" &&
            reference.IncludeAssets == "runtime; build; native; contentfiles; analyzers; buildtransitive");
    }

    [Fact]
    public void GodotMemoryPack_DoesNotRepeatSharedSerializerDependencies()
    {
        var ids = CreateIds(StarterProjectRole.GodotClient, SerializerKind.MemoryPack, ClientEngineKind.Godot);

        Assert.Contains("ULinkRPC.Core", ids);
        Assert.Contains("ULinkRPC.Client", ids);
        Assert.Contains("ULinkRPC.Transport.WebSocket", ids);
        Assert.Contains("ULinkRPC.Analyzers", ids);
        Assert.DoesNotContain("ULinkRPC.Serializer.MemoryPack", ids);
        Assert.DoesNotContain("MemoryPack", ids);
        Assert.DoesNotContain("MemoryPack.Core", ids);
    }

    [Fact]
    public void GodotJson_IncludesJsonSerializer()
    {
        var ids = CreateIds(StarterProjectRole.GodotClient, SerializerKind.Json, ClientEngineKind.Godot);

        Assert.Contains("ULinkRPC.Serializer.Json", ids);
    }

    [Fact]
    public void StrideMemoryPack_DoesNotRepeatSharedSerializerDependencies()
    {
        var ids = CreateIds(StarterProjectRole.StrideClient, SerializerKind.MemoryPack, ClientEngineKind.Stride3D);

        Assert.Contains("Stride.Engine", ids);
        Assert.Contains("Stride.Core.Assets.CompilerApp", ids);
        Assert.Contains("ULinkRPC.Core", ids);
        Assert.Contains("ULinkRPC.Client", ids);
        Assert.Contains("ULinkRPC.Transport.WebSocket", ids);
        Assert.Contains("ULinkRPC.Analyzers", ids);
        Assert.DoesNotContain("ULinkRPC.Serializer.MemoryPack", ids);
        Assert.DoesNotContain("MemoryPack", ids);
        Assert.DoesNotContain("MemoryPack.Core", ids);
    }

    [Fact]
    public void StrideJson_IncludesJsonSerializer()
    {
        var ids = CreateIds(StarterProjectRole.StrideClient, SerializerKind.Json, ClientEngineKind.Stride3D);

        Assert.Contains("ULinkRPC.Serializer.Json", ids);
        Assert.Contains("ULinkRPC.Analyzers", ids);
    }

    [Fact]
    public void UnityMemoryPack_KeepsExplicitSerializerAndRuntimeDependencies()
    {
        var plan = CreatePlan(StarterProjectRole.UnityClient, SerializerKind.MemoryPack, ClientEngineKind.Tuanjie);
        var ids = plan.PackageReferences.Select(static reference => reference.Id).ToArray();

        Assert.Contains("ULinkRPC.Serializer.MemoryPack", ids);
        Assert.Contains("ULinkRPC.Analyzers", ids);
        Assert.Contains("MemoryPack", ids);
        Assert.Contains("MemoryPack.Core", ids);
        Assert.Contains("MemoryPack.Generator", ids);
        Assert.Contains("Microsoft.CodeAnalysis.CSharp", ids);
        Assert.Contains("System.IO.Pipelines", ids);
        Assert.Contains(plan.PackageReferences, static reference =>
            reference.Id == "ULinkRPC.Serializer.MemoryPack" && reference.ManuallyInstalled);
    }

    [Fact]
    public void UnityJson_KeepsExplicitSerializerAndRuntimeDependencies()
    {
        var plan = CreatePlan(StarterProjectRole.UnityClient, SerializerKind.Json);
        var ids = plan.PackageReferences.Select(static reference => reference.Id).ToArray();

        Assert.Contains("ULinkRPC.Serializer.Json", ids);
        Assert.Contains("Microsoft.Bcl.AsyncInterfaces", ids);
        Assert.Contains("System.Text.Json", ids);
        Assert.Contains("System.IO.Pipelines", ids);
        Assert.Contains(plan.PackageReferences, static reference =>
            reference.Id == "ULinkRPC.Serializer.Json" && reference.ManuallyInstalled);
    }

    [Fact]
    public void UnityKcp_IncludesTransportRuntimeDependencies()
    {
        var ids = CreatePlan(StarterProjectRole.UnityClient, SerializerKind.Json, transport: TransportKind.Kcp)
            .PackageReferences
            .Select(static reference => reference.Id)
            .ToArray();

        Assert.Contains("ULinkRPC.Transport.Kcp", ids);
        Assert.Contains("Kcp", ids);
        Assert.Contains("System.Memory", ids);
        Assert.Contains("System.Threading.Tasks.Extensions", ids);
    }

    [Fact]
    public void SharedMemoryPackGenerator_UsesAnalyzerMetadata()
    {
        var generator = CreatePlan(StarterProjectRole.Shared, SerializerKind.MemoryPack)
            .PackageReferences
            .Single(static reference => reference.Id == "MemoryPack.Generator");

        Assert.Equal("all", generator.PrivateAssets);
        Assert.Equal("runtime; build; native; contentfiles; analyzers; buildtransitive", generator.IncludeAssets);
    }

    private static string[] CreateIds(
        StarterProjectRole role,
        SerializerKind serializer,
        ClientEngineKind clientEngine = ClientEngineKind.Unity,
        TransportKind transport = TransportKind.WebSocket) =>
        CreatePlan(role, serializer, clientEngine, transport)
            .PackageReferences
            .Select(static reference => reference.Id)
            .ToArray();

    private static StarterDependencyPlan CreatePlan(
        StarterProjectRole role,
        SerializerKind serializer,
        ClientEngineKind clientEngine = ClientEngineKind.Unity,
        TransportKind transport = TransportKind.WebSocket)
    {
        var context = new StarterTemplateContext(
            "Starter",
            "starter",
            clientEngine,
            transport,
            serializer,
            NuGetForUnitySourceKind.Embedded,
            Versions,
            new StarterPaths("Root", "Root/Shared", "Root/Server", "Root/Server/Server", "Root/Client"));

        return StarterDependencyPlanner.Create(context, role);
    }
}
