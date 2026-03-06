using ULinkRPC.CodeGen;
using Xunit;

namespace ULinkRPC.CodeGen.Tests;

public class ModelTests
{
    #region RpcServiceInfo

    [Fact]
    public void Constructor_AddsContractNamespaceFromFullName()
    {
        var svc = new RpcServiceInfo("IFoo", "MyGame.Contracts.IFoo", 1,
            [new RpcMethodInfo("Bar", 1, [], null, true)], []);

        Assert.Contains("MyGame.Contracts", svc.UsingDirectives);
    }

    [Fact]
    public void Constructor_DoesNotAddEmptyNamespace()
    {
        var svc = new RpcServiceInfo("IFoo", "IFoo", 1,
            [new RpcMethodInfo("Bar", 1, [], null, true)], []);

        Assert.DoesNotContain("", svc.UsingDirectives);
    }

    [Fact]
    public void Constructor_PassesThroughInitialDirectives()
    {
        var svc = new RpcServiceInfo("IFoo", "Ns.IFoo", 1,
            [new RpcMethodInfo("Bar", 1, [], null, true)],
            ["Other", "Extra"]);

        Assert.Contains("Ns", svc.UsingDirectives);
        Assert.Contains("Other", svc.UsingDirectives);
        Assert.Contains("Extra", svc.UsingDirectives);
    }

    [Fact]
    public void Constructor_DeduplicatesInitialDirectives()
    {
        var svc = new RpcServiceInfo("IFoo", "Ns.IFoo", 1,
            [new RpcMethodInfo("Bar", 1, [], null, true)],
            ["Ns", "Ns", "Other"]);

        Assert.Equal(1, svc.UsingDirectives.Count(d => d == "Ns"));
        Assert.Equal(1, svc.UsingDirectives.Count(d => d == "Other"));
    }

    [Fact]
    public void AddUsingDirective_IgnoresDuplicateFromConstructorInit()
    {
        var svc = new RpcServiceInfo("IFoo", "Ns.IFoo", 1,
            [new RpcMethodInfo("Bar", 1, [], null, true)],
            ["Ns"]);

        int countBefore = svc.UsingDirectives.Count(d => d == "Ns");
        svc.AddUsingDirectives(["Ns"]);
        int countAfter = svc.UsingDirectives.Count(d => d == "Ns");
        Assert.Equal(countBefore, countAfter);
    }

    [Fact]
    public void AddUsingDirectives_DeduplicatesViaHashSet()
    {
        var svc = new RpcServiceInfo("IFoo", "Ns.IFoo", 1,
            [new RpcMethodInfo("Bar", 1, [], null, true)], ["Existing"]);

        svc.AddUsingDirectives(["Existing", "NewOne", "NewOne"]);

        Assert.Equal(1, svc.UsingDirectives.Count(d => d == "Existing"));
        Assert.Equal(1, svc.UsingDirectives.Count(d => d == "NewOne"));
    }

    [Fact]
    public void AddUsingDirectives_PreservesInsertionOrder()
    {
        var svc = new RpcServiceInfo("IFoo", "Ns.IFoo", 1,
            [new RpcMethodInfo("Bar", 1, [], null, true)], ["A"]);

        svc.AddUsingDirectives(["C", "B"]);

        var idx_a = svc.UsingDirectives.IndexOf("A");
        var idx_c = svc.UsingDirectives.IndexOf("C");
        var idx_b = svc.UsingDirectives.IndexOf("B");
        Assert.True(idx_a < idx_c);
        Assert.True(idx_c < idx_b);
    }

    [Fact]
    public void HasCallback_TrueWhenBothNameAndMethodsSet()
    {
        var svc = new RpcServiceInfo("ISvc", "Ns.ISvc", 1,
            [new RpcMethodInfo("M", 1, [], null, true)], [])
        {
            CallbackInterfaceName = "ICb",
            CallbackInterfaceFullName = "Ns.ICb"
        };
        svc.CallbackMethods = [new RpcCallbackMethodInfo("OnX", 1, [])];

        Assert.True(svc.HasCallback);
    }

    [Fact]
    public void HasCallback_FalseWhenNameIsNull()
    {
        var svc = new RpcServiceInfo("ISvc", "Ns.ISvc", 1,
            [new RpcMethodInfo("M", 1, [], null, true)], []);
        svc.CallbackMethods = [new RpcCallbackMethodInfo("OnX", 1, [])];

        Assert.False(svc.HasCallback);
    }

    [Fact]
    public void HasCallback_FalseWhenMethodsEmpty()
    {
        var svc = new RpcServiceInfo("ISvc", "Ns.ISvc", 1,
            [new RpcMethodInfo("M", 1, [], null, true)], [])
        {
            CallbackInterfaceName = "ICb",
            CallbackInterfaceFullName = "Ns.ICb"
        };

        Assert.False(svc.HasCallback);
    }

    #endregion

    #region Records

    [Fact]
    public void RawOptions_Empty_HasDefaults()
    {
        var empty = RawOptions.Empty;
        Assert.Equal(string.Empty, empty.ContractsPath);
        Assert.Equal(OutputMode.Unknown, empty.Mode);
    }

    [Fact]
    public void ResolvedOptions_WithExpression_CreatesModifiedCopy()
    {
        var original = new ResolvedOptions("/c", "/o", "Ns", "/so", "SNs", OutputMode.Unity);
        var modified = original with { ServerNamespace = "Changed" };

        Assert.Equal("SNs", original.ServerNamespace);
        Assert.Equal("Changed", modified.ServerNamespace);
        Assert.Equal(original.ContractsPath, modified.ContractsPath);
    }

    #endregion
}
