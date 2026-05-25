using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using ULinkRPC.Analyzers;
using Xunit;

namespace ULinkRPC.CodeGen.Tests;

public sealed class ULinkRpcSourceGeneratorTests
{
    [Fact]
    public void GenerateClient_EmitsFacadeAndClientTypes()
    {
        var compilation = CreateCompilation(ContractSource + ClientRuntimeStubs);
        var result = RunGenerator(
            compilation,
            new Dictionary<string, string>
            {
                ["build_property.ULinkRPCGenerateClient"] = "true",
                ["build_property.ULinkRPCGeneratedNamespace"] = "Rpc.Generated"
            });

        Assert.Empty(result.Diagnostics.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        Assert.Contains(result.GeneratedTrees, static tree => tree.FilePath.EndsWith("PingServiceClient.g.cs", StringComparison.Ordinal));
        Assert.Contains(result.GeneratedTrees, static tree => tree.FilePath.EndsWith("RpcApi.g.cs", StringComparison.Ordinal));
        Assert.Empty(result.Compilation.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
    }

    [Fact]
    public void GenerateServer_EmitsAllServicesBinderAssemblyAttribute()
    {
        var compilation = CreateCompilation(ContractSource + ServerRuntimeStubs + """

            namespace Server
            {
                public sealed class PingService : Shared.Interfaces.IPingService
                {
                    public System.Threading.Tasks.ValueTask<Shared.Interfaces.PingReply> PingAsync(Shared.Interfaces.PingRequest request)
                    {
                        return new System.Threading.Tasks.ValueTask<Shared.Interfaces.PingReply>(new Shared.Interfaces.PingReply());
                    }
                }
            }
            """);

        var result = RunGenerator(
            compilation,
            new Dictionary<string, string>
            {
                ["build_property.ULinkRPCGenerateServer"] = "true",
                ["build_property.ULinkRPCServerGeneratedNamespace"] = "Server.Generated"
            });

        Assert.Empty(result.Diagnostics.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        var allServices = Assert.Single(result.GeneratedTrees, static tree => tree.FilePath.EndsWith("AllServicesBinder.g.cs", StringComparison.Ordinal));
        Assert.Contains("[assembly: RpcGeneratedServicesBinder(typeof(Server.Generated.AllServicesBinder))]", allServices.GetText().ToString());
        Assert.Empty(result.Compilation.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
    }

    [Fact]
    public void GenerateClient_ReadsReferencedContractAssembly()
    {
        var contractCompilation = CreateCompilation(ContractSource, "Shared");
        var contractEmit = contractCompilation.Emit(Stream.Null);
        Assert.True(contractEmit.Success, string.Join(Environment.NewLine, contractEmit.Diagnostics));

        using var stream = new MemoryStream();
        contractCompilation.Emit(stream);
        stream.Position = 0;

        var clientCompilation = CSharpCompilation.Create(
            "Client",
            [CSharpSyntaxTree.ParseText(ClientRuntimeStubs)],
            GetPlatformReferences().Append(MetadataReference.CreateFromImage(stream.ToArray())),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var result = RunGenerator(
            clientCompilation,
            new Dictionary<string, string>
            {
                ["build_property.ULinkRPCGenerateClient"] = "true"
            });

        Assert.Empty(result.Diagnostics.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        Assert.Contains(result.GeneratedTrees, static tree => tree.FilePath.EndsWith("PingServiceClient.g.cs", StringComparison.Ordinal));
    }

    [Fact]
    public void GenerateClient_AutoDetectsClientRuntimeWhenPropertiesAreAbsent()
    {
        var compilation = CreateCompilation(ContractSource + ClientRuntimeStubs);

        var result = RunGenerator(compilation, new Dictionary<string, string>());

        Assert.Empty(result.Diagnostics.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        Assert.Contains(result.GeneratedTrees, static tree => tree.FilePath.EndsWith("PingServiceClient.g.cs", StringComparison.Ordinal));
        Assert.DoesNotContain(result.GeneratedTrees, static tree => tree.FilePath.EndsWith("AllServicesBinder.g.cs", StringComparison.Ordinal));
    }

    [Fact]
    public void GenerateClient_ExplicitFalseSuppressesAutoDetection()
    {
        var compilation = CreateCompilation(ContractSource + ClientRuntimeStubs);

        var result = RunGenerator(
            compilation,
            new Dictionary<string, string>
            {
                ["build_property.ULinkRPCGenerateClient"] = "false"
            });

        Assert.Empty(result.Diagnostics.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        Assert.Empty(result.GeneratedTrees);
    }

    private static GeneratorRunResult RunGenerator(CSharpCompilation compilation, Dictionary<string, string> properties)
    {
        var driver = CSharpGeneratorDriver.Create(
            [new ULinkRpcSourceGenerator()],
            optionsProvider: new TestAnalyzerConfigOptionsProvider(properties));

        driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var updatedCompilation,
            out var diagnostics);

        var result = driver.GetRunResult().Results.Single();
        return new GeneratorRunResult(updatedCompilation, diagnostics.AddRange(result.Diagnostics), result.GeneratedSources.Select(static source => source.SyntaxTree).ToArray());
    }

    private static CSharpCompilation CreateCompilation(string source, string assemblyName = "GeneratorTests") =>
        CSharpCompilation.Create(
            assemblyName,
            [CSharpSyntaxTree.ParseText(source)],
            GetPlatformReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

    private static IEnumerable<MetadataReference> GetPlatformReferences()
    {
        var trustedAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        if (!string.IsNullOrWhiteSpace(trustedAssemblies))
        {
            foreach (var path in trustedAssemblies.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
                yield return MetadataReference.CreateFromFile(path);
        }
    }

    private sealed record GeneratorRunResult(
        Compilation Compilation,
        ImmutableArray<Diagnostic> Diagnostics,
        IReadOnlyList<SyntaxTree> GeneratedTrees);

    private sealed class TestAnalyzerConfigOptionsProvider : AnalyzerConfigOptionsProvider
    {
        private readonly TestAnalyzerConfigOptions _globalOptions;

        public TestAnalyzerConfigOptionsProvider(IReadOnlyDictionary<string, string> values)
        {
            _globalOptions = new TestAnalyzerConfigOptions(values);
        }

        public override AnalyzerConfigOptions GlobalOptions => _globalOptions;
        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => Empty;
        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => Empty;

        private static readonly TestAnalyzerConfigOptions Empty = new(new Dictionary<string, string>());
    }

    private sealed class TestAnalyzerConfigOptions : AnalyzerConfigOptions
    {
        private readonly IReadOnlyDictionary<string, string> _values;

        public TestAnalyzerConfigOptions(IReadOnlyDictionary<string, string> values)
        {
            _values = values;
        }

        public override bool TryGetValue(string key, out string value)
        {
            if (_values.TryGetValue(key, out var found))
            {
                value = found;
                return true;
            }

            value = string.Empty;
            return false;
        }
    }

    private const string ContractSource = """
        using System;
        using System.Threading.Tasks;
        using ULinkRPC.Core;

        namespace Shared.Interfaces
        {
            public static class RpcContractIds
            {
                public static class Services
                {
                    public const int Ping = 1;
                }

                public static class PingMethods
                {
                    public const int PingAsync = 1;
                }
            }

            public sealed class PingRequest
            {
                public string Message { get; set; } = string.Empty;
            }

            public sealed class PingReply
            {
                public string Message { get; set; } = string.Empty;
            }

            [RpcService(RpcContractIds.Services.Ping)]
            public interface IPingService
            {
                [RpcMethod(RpcContractIds.PingMethods.PingAsync)]
                ValueTask<PingReply> PingAsync(PingRequest request);
            }
        }

        namespace ULinkRPC.Core
        {
            [AttributeUsage(AttributeTargets.Interface)]
            public sealed class RpcServiceAttribute : Attribute
            {
                public RpcServiceAttribute(int serviceId) { }
                public Type? Callback { get; set; }
            }

            [AttributeUsage(AttributeTargets.Method)]
            public sealed class RpcMethodAttribute : Attribute
            {
                public RpcMethodAttribute(int methodId) { }
            }

            [AttributeUsage(AttributeTargets.Interface)]
            public sealed class RpcCallbackAttribute : Attribute
            {
                public RpcCallbackAttribute(Type serviceType) { }
            }

            [AttributeUsage(AttributeTargets.Method)]
            public sealed class RpcPushAttribute : Attribute
            {
                public RpcPushAttribute(int methodId) { }
            }
        }
        """;

    private const string ClientRuntimeStubs = """
        namespace ULinkRPC.Core
        {
            public readonly struct RpcVoid { }
            public readonly struct RpcMethod<TArg, TResult>
            {
                public RpcMethod(int serviceId, int methodId) { }
            }

            public readonly struct RpcPushMethod<TArg>
            {
                public RpcPushMethod(int serviceId, int methodId) { }
            }

            public interface IRpcClient
            {
                System.Threading.Tasks.ValueTask<TResult> CallAsync<TArg, TResult>(RpcMethod<TArg, TResult> method, TArg arg, System.Threading.CancellationToken ct);
                void RegisterPushHandler<TArg>(RpcPushMethod<TArg> method, System.Action<TArg> handler);
            }
        }

        namespace ULinkRPC.Client
        {
            public sealed class RpcClientOptions { }

            public sealed class RpcClientRuntime : ULinkRPC.Core.IRpcClient, System.IAsyncDisposable
            {
                public RpcClientRuntime(RpcClientOptions options) { }
                public event System.Action<System.Exception?>? Disconnected;
                public System.Threading.Tasks.ValueTask StartAsync(System.Threading.CancellationToken ct) => default;
                public System.Threading.Tasks.ValueTask DisposeAsync() => default;
                public System.Threading.Tasks.ValueTask<TResult> CallAsync<TArg, TResult>(ULinkRPC.Core.RpcMethod<TArg, TResult> method, TArg arg, System.Threading.CancellationToken ct) => default;
                public void RegisterPushHandler<TArg>(ULinkRPC.Core.RpcPushMethod<TArg> method, System.Action<TArg> handler) { }
            }
        }
        """;

    private const string ServerRuntimeStubs = """
        namespace ULinkRPC.Core
        {
            public enum RpcStatus { Ok }

            public readonly struct TransportFrame
            {
                public System.ReadOnlyMemory<byte> Memory => default;
            }

            public static class RpcEnvelopeCodec
            {
                public static TransportFrame EncodeResponse(long requestId, RpcStatus status, System.ReadOnlyMemory<byte> payload) => default;
            }
        }

        namespace ULinkRPC.Server
        {
            public sealed class RpcGeneratedServicesBinderAttribute : System.Attribute
            {
                public RpcGeneratedServicesBinderAttribute(System.Type binderType) { }
            }

            public sealed class RpcRequestFrame
            {
                public long RequestId => 0;
                public PayloadFrame Payload => new();
            }

            public sealed class PayloadFrame
            {
                public System.ReadOnlyMemory<byte> Memory => default;
            }

            public interface IRpcSerializer
            {
                T? Deserialize<T>(System.ReadOnlyMemory<byte> payload);
                SerializedFrame SerializeFrame<T>(T value);
            }

            public readonly struct SerializedFrame : System.IDisposable
            {
                public System.ReadOnlyMemory<byte> Memory => default;
                public void Dispose() { }
            }

            public sealed class RpcSession
            {
                public IRpcSerializer Serializer => null!;
                public T GetOrAddScopedService<T>(int serviceId, System.Func<RpcSession, T> factory) => factory(this);
                public System.Threading.Tasks.ValueTask PushAsync<T>(int serviceId, int methodId, T arg) => default;
            }

            public sealed class RpcServiceRegistry
            {
                public void Register(int serviceId, int methodId, System.Func<RpcSession, RpcRequestFrame, System.Threading.CancellationToken, System.Threading.Tasks.ValueTask<ULinkRPC.Core.TransportFrame>> handler) { }
            }
        }
        """;
}
