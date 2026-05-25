using Xunit;

namespace ULinkRPC.Analyzers.Tests;

public sealed class RpcContractIdAnalyzerTests
{
    [Fact]
    public async Task ContractIdAnalyzer_ReportsInvalidAndDuplicateIdsInTheirOwnScopes()
    {
        var compilation = AnalyzerTestHelpers.CreateCompilation(
            """
            using ULinkRPC.Core;

            namespace Analyzer.Contracts
            {
                public sealed class Request { }
                public sealed class Reply { }

                [RpcService(1, Callback = typeof(IFirstCallback))]
                public interface IFirstService
                {
                    [RpcMethod(1)]
                    System.Threading.Tasks.ValueTask<Reply> OneAsync(Request request);

                    [RpcMethod(1)]
                    System.Threading.Tasks.ValueTask<Reply> DuplicateAsync(Request request);
                }

                [RpcService(1)]
                public interface ISecondService
                {
                    [RpcMethod(2)]
                    System.Threading.Tasks.ValueTask<Reply> TwoAsync(Request request);
                }

                [RpcService(0)]
                public interface IInvalidService
                {
                    [RpcMethod(1)]
                    System.Threading.Tasks.ValueTask<Reply> InvalidAsync(Request request);
                }

                [RpcCallback(typeof(IFirstService))]
                public interface IFirstCallback
                {
                    [RpcPush(0)]
                    void InvalidPush(Request request);

                    [RpcPush(2)]
                    void PushOne(Request request);

                    [RpcPush(2)]
                    void PushDuplicate(Request request);
                }
            }
            """);

        var diagnostics = await AnalyzerTestHelpers.RunContractIdAnalyzerAsync(compilation);
        var ids = diagnostics.Select(static diagnostic => diagnostic.Id).OrderBy(static id => id).ToArray();

        Assert.Contains(RpcContractIdAnalyzer.InvalidServiceIdDiagnosticId, ids);
        Assert.Contains(RpcContractIdAnalyzer.DuplicateServiceIdDiagnosticId, ids);
        Assert.Contains(RpcContractIdAnalyzer.DuplicateMethodIdDiagnosticId, ids);
        Assert.Contains(RpcContractIdAnalyzer.InvalidPushIdDiagnosticId, ids);
        Assert.Contains(RpcContractIdAnalyzer.DuplicatePushIdDiagnosticId, ids);

        Assert.Equal(2, diagnostics.Count(static diagnostic => diagnostic.Id == RpcContractIdAnalyzer.DuplicateServiceIdDiagnosticId));
        Assert.Equal(2, diagnostics.Count(static diagnostic => diagnostic.Id == RpcContractIdAnalyzer.DuplicateMethodIdDiagnosticId));
        Assert.Equal(2, diagnostics.Count(static diagnostic => diagnostic.Id == RpcContractIdAnalyzer.DuplicatePushIdDiagnosticId));
    }
}
