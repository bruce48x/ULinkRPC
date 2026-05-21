using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using ULinkRPC.Analyzers;
using Xunit;

namespace ULinkRPC.CodeGen.Tests;

public sealed class RpcContractIdAnalyzerTests
{
    [Theory]
    [InlineData("[RpcService(0)] public interface ISvc { [RpcMethod(1)] void Do(); }", RpcContractIdAnalyzer.InvalidServiceIdDiagnosticId)]
    [InlineData("[RpcService(-1)] public interface ISvc { [RpcMethod(1)] void Do(); }", RpcContractIdAnalyzer.InvalidServiceIdDiagnosticId)]
    [InlineData("[RpcService(1)] public interface ISvc { [RpcMethod(0)] void Do(); }", RpcContractIdAnalyzer.InvalidMethodIdDiagnosticId)]
    [InlineData("[RpcCallback(typeof(ISvc))] public interface INotify { [RpcPush(0)] void OnNotify(); } [RpcService(1, Callback = typeof(INotify))] public interface ISvc { [RpcMethod(1)] void Do(); }", RpcContractIdAnalyzer.InvalidPushIdDiagnosticId)]
    public async Task ReportsInvalidNonPositiveIds(string contractSource, string diagnosticId)
    {
        var diagnostics = await AnalyzeAsync(contractSource);

        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == diagnosticId);
    }

    [Fact]
    public async Task ReportsDuplicateServiceIds()
    {
        var diagnostics = await AnalyzeAsync("""
            [RpcService(1)]
            public interface IA { [RpcMethod(1)] void A(); }

            [RpcService(1)]
            public interface IB { [RpcMethod(1)] void B(); }
            """);

        var duplicateDiagnostics = diagnostics
            .Where(static diagnostic => diagnostic.Id == RpcContractIdAnalyzer.DuplicateServiceIdDiagnosticId)
            .ToArray();

        Assert.Equal(2, duplicateDiagnostics.Length);
        Assert.All(duplicateDiagnostics, diagnostic => Assert.Contains("Duplicate ServiceId 1", diagnostic.GetMessage()));
    }

    [Fact]
    public async Task ReportsDuplicateMethodIdsWithinService()
    {
        var diagnostics = await AnalyzeAsync("""
            [RpcService(1)]
            public interface ISvc
            {
                [RpcMethod(1)] void A();
                [RpcMethod(1)] void B();
            }
            """);

        var duplicateDiagnostics = diagnostics
            .Where(static diagnostic => diagnostic.Id == RpcContractIdAnalyzer.DuplicateMethodIdDiagnosticId)
            .ToArray();

        Assert.Equal(2, duplicateDiagnostics.Length);
        Assert.All(duplicateDiagnostics, diagnostic => Assert.Contains("Duplicate MethodId 1", diagnostic.GetMessage()));
    }

    [Fact]
    public async Task AllowsSameMethodIdAcrossDifferentServices()
    {
        var diagnostics = await AnalyzeAsync("""
            [RpcService(1)]
            public interface IA { [RpcMethod(1)] void A(); }

            [RpcService(2)]
            public interface IB { [RpcMethod(1)] void B(); }
            """);

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == RpcContractIdAnalyzer.DuplicateMethodIdDiagnosticId);
    }

    [Fact]
    public async Task ReportsDuplicatePushIdsWithinCallbackInterface()
    {
        var diagnostics = await AnalyzeAsync("""
            [RpcCallback(typeof(ISvc))]
            public interface INotify
            {
                [RpcPush(1)] void A();
                [RpcPush(1)] void B();
            }

            [RpcService(1, Callback = typeof(INotify))]
            public interface ISvc { [RpcMethod(1)] void Do(); }
            """);

        var duplicateDiagnostics = diagnostics
            .Where(static diagnostic => diagnostic.Id == RpcContractIdAnalyzer.DuplicatePushIdDiagnosticId)
            .ToArray();

        Assert.Equal(2, duplicateDiagnostics.Length);
        Assert.All(duplicateDiagnostics, diagnostic => Assert.Contains("Duplicate PushId 1", diagnostic.GetMessage()));
    }

    private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string contractSource)
    {
        var source = AttributeDefinitions + Environment.NewLine + contractSource;
        var compilation = CSharpCompilation.Create(
            "AnalyzerTests",
            [CSharpSyntaxTree.ParseText(source)],
            GetPlatformReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var analyzer = new RpcContractIdAnalyzer();
        var compilationWithAnalyzers = compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(analyzer));
        return await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();
    }

    private static IEnumerable<MetadataReference> GetPlatformReferences()
    {
        var trustedAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        if (!string.IsNullOrWhiteSpace(trustedAssemblies))
        {
            foreach (var path in trustedAssemblies.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
                yield return MetadataReference.CreateFromFile(path);
        }
    }

    private const string AttributeDefinitions = """
        using System;

        public sealed class RpcServiceAttribute : Attribute
        {
            public RpcServiceAttribute(int serviceId) { }
            public Type? Callback { get; set; }
        }

        public sealed class RpcMethodAttribute : Attribute
        {
            public RpcMethodAttribute(int methodId) { }
        }

        public sealed class RpcCallbackAttribute : Attribute
        {
            public RpcCallbackAttribute(Type serviceType) { }
        }

        public sealed class RpcPushAttribute : Attribute
        {
            public RpcPushAttribute(int methodId) { }
        }
        """;
}
