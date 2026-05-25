using Microsoft.CodeAnalysis;
using Xunit;

namespace ULinkRPC.Analyzers.Tests;

public sealed class ULinkRpcSourceGeneratorTests
{
    [Fact]
    public void SourceGenerator_ExplicitClientAndServerGeneration_ProducesCompilableGlue()
    {
        var compilation = AnalyzerTestHelpers.CreateCompilation(ContractWithCallbackSource);
        var runResult = AnalyzerTestHelpers.RunGenerator(
            compilation,
            new Dictionary<string, string>
            {
                ["build_property.ULinkRPCGenerateClient"] = "true",
                ["build_property.ULinkRPCGenerateServer"] = "true",
                ["build_property.ULinkRPCGeneratedNamespace"] = "Client.Generated",
                ["build_property.ULinkRPCServerGeneratedNamespace"] = "Server.Generated"
            },
            out var outputCompilation);

        Assert.Empty(runResult.Diagnostics);
        Assert.Empty(AnalyzerTestHelpers.ErrorDiagnostics(outputCompilation));

        var generatedHintNames = runResult.Results
            .Single()
            .GeneratedSources
            .Select(static source => source.HintName)
            .ToArray();

        Assert.Contains("PingServiceClient.g.cs", generatedHintNames);
        Assert.Contains("PingCallbackBinder.g.cs", generatedHintNames);
        Assert.Contains("RpcApi.g.cs", generatedHintNames);
        Assert.Contains("PingServiceBinder.g.cs", generatedHintNames);
        Assert.Contains("PingCallbackProxy.g.cs", generatedHintNames);
        Assert.Contains("AllServicesBinder.g.cs", generatedHintNames);

        var allServicesBinder = runResult.Results.Single().GeneratedSources.Single(static source => source.HintName == "AllServicesBinder.g.cs").SourceText.ToString();
        Assert.Contains("[assembly: RpcGeneratedServicesBinder(typeof(Server.Generated.AllServicesBinder))]", allServicesBinder);
    }

    [Fact]
    public void SourceGenerator_ReferencedContractAssembly_IsDiscoveredForClientGeneration()
    {
        var contractCompilation = AnalyzerTestHelpers.CreateCompilation(ReferencedContractSource, "Contracts");
        var contractReference = AnalyzerTestHelpers.EmitReference(contractCompilation);
        var appCompilation = AnalyzerTestHelpers.CreateCompilation(
            "public sealed class App { }",
            additionalReferences: new[] { contractReference });

        var runResult = AnalyzerTestHelpers.RunGenerator(
            appCompilation,
            new Dictionary<string, string>
            {
                ["build_property.ULinkRPCGenerateClient"] = "true"
            },
            out var outputCompilation);

        Assert.Empty(runResult.Diagnostics);
        Assert.Empty(AnalyzerTestHelpers.ErrorDiagnostics(outputCompilation));

        var generatedSource = string.Join(
            "\n",
            runResult.Results.Single().GeneratedSources.Select(static source => source.SourceText.ToString()));
        Assert.Contains("Referenced.Generated", generatedSource);
        Assert.Contains("ExternalServiceClient", generatedSource);
    }

    [Fact]
    public void SourceGenerator_UnityAssemblyRequiresClientGenerationMarker()
    {
        var unmarkedCompilation = AnalyzerTestHelpers.CreateCompilation(
            SimpleClientContractSource,
            assemblyName: "Assembly-CSharp");
        var unmarkedRun = AnalyzerTestHelpers.RunGenerator(unmarkedCompilation, null, out _);

        Assert.Empty(unmarkedRun.Results.Single().GeneratedSources);

        var markedCompilation = AnalyzerTestHelpers.CreateCompilation(
            """
            using ULinkRPC.Core;

            [assembly: ULinkRPCGenerateClient("Unity.Generated")]

            namespace UnityContracts
            {
                public sealed class PingRequest { }
                public sealed class PingReply { }

                [RpcService(7)]
                public interface IPingService
                {
                    [RpcMethod(1)]
                    System.Threading.Tasks.ValueTask<PingReply> PingAsync(PingRequest request);
                }
            }
            """,
            assemblyName: "Assembly-CSharp");
        var markedRun = AnalyzerTestHelpers.RunGenerator(markedCompilation, null, out var markedOutput);

        Assert.Empty(markedRun.Diagnostics);
        Assert.Empty(AnalyzerTestHelpers.ErrorDiagnostics(markedOutput));

        var generatedSource = string.Join(
            "\n",
            markedRun.Results.Single().GeneratedSources.Select(static source => source.SourceText.ToString()));
        Assert.Contains("namespace Unity.Generated", generatedSource);
        Assert.Contains("PingServiceClient", generatedSource);
    }

    private const string ContractWithCallbackSource = """
        using System.Threading.Tasks;
        using ULinkRPC.Core;

        namespace Game.Contracts
        {
            public sealed class PingRequest
            {
                public string Message { get; set; } = string.Empty;
            }

            public sealed class PingReply
            {
                public string Message { get; set; } = string.Empty;
            }

            public sealed class NotifyRequest
            {
                public string Message { get; set; } = string.Empty;
            }

            [RpcService(1, Callback = typeof(IPingCallback))]
            public interface IPingService
            {
                [RpcMethod(1)]
                ValueTask<PingReply> PingAsync(PingRequest request);
            }

            [RpcCallback(typeof(IPingService))]
            public interface IPingCallback
            {
                [RpcPush(1)]
                void OnNotify(NotifyRequest request);
            }
        }
        """;

    private const string ReferencedContractSource = """
        using System.Threading.Tasks;
        using ULinkRPC.Core;

        namespace Referenced.Generated
        {
            public sealed class ExternalRequest { }
            public sealed class ExternalReply { }

            [RpcService(23)]
            public interface IExternalService
            {
                [RpcMethod(1)]
                ValueTask<ExternalReply> CallAsync(ExternalRequest request);
            }
        }
        """;

    private const string SimpleClientContractSource = """
        using System.Threading.Tasks;
        using ULinkRPC.Core;

        namespace UnityContracts
        {
            public sealed class PingRequest { }
            public sealed class PingReply { }

            [RpcService(7)]
            public interface IPingService
            {
                [RpcMethod(1)]
                ValueTask<PingReply> PingAsync(PingRequest request);
            }
        }
        """;
}
