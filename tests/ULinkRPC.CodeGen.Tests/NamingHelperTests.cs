using ULinkRPC.CodeGen;
using Xunit;

namespace ULinkRPC.CodeGen.Tests;

public class NamingHelperTests
{
    #region GetServiceTypeName

    [Theory]
    [InlineData("IPlayerService", "PlayerService")]
    [InlineData("IChatService", "ChatService")]
    [InlineData("PlayerService", "PlayerService")]
    [InlineData("I", "I")]
    [InlineData("Ia", "Ia")]
    [InlineData("IX", "X")]
    [InlineData("IIDouble", "IDouble")]
    public void GetServiceTypeName_StripsLeadingI(string input, string expected)
    {
        Assert.Equal(expected, NamingHelper.GetServiceTypeName(input));
    }

    #endregion

    #region Derived type names

    [Theory]
    [InlineData("IPlayerService", "PlayerServiceClient")]
    [InlineData("IChatService", "ChatServiceClient")]
    [InlineData("Foo", "FooClient")]
    public void GetClientTypeName_AppendsClient(string input, string expected)
    {
        Assert.Equal(expected, NamingHelper.GetClientTypeName(input));
    }

    [Theory]
    [InlineData("IPlayerService", "PlayerServiceBinder")]
    [InlineData("IFoo", "FooBinder")]
    public void GetBinderTypeName_AppendsBinder(string input, string expected)
    {
        Assert.Equal(expected, NamingHelper.GetBinderTypeName(input));
    }

    [Theory]
    [InlineData("IGameCallback", "GameCallbackProxy")]
    [InlineData("INotify", "NotifyProxy")]
    public void GetCallbackProxyTypeName_AppendsProxy(string input, string expected)
    {
        Assert.Equal(expected, NamingHelper.GetCallbackProxyTypeName(input));
    }

    [Theory]
    [InlineData("IGameCallback", "GameCallbackBinder")]
    public void GetCallbackBinderTypeName_AppendsBinder(string input, string expected)
    {
        Assert.Equal(expected, NamingHelper.GetCallbackBinderTypeName(input));
    }

    [Theory]
    [InlineData("IPlayerService", "PlayerServiceClientExtensions")]
    public void GetClientExtensionTypeName_AppendsExtensions(string input, string expected)
    {
        Assert.Equal(expected, NamingHelper.GetClientExtensionTypeName(input));
    }

    [Theory]
    [InlineData("IPlayerService", "CreatePlayerService")]
    [InlineData("IAuth", "CreateAuth")]
    public void GetClientFactoryMethodName_PrependCreate(string input, string expected)
    {
        Assert.Equal(expected, NamingHelper.GetClientFactoryMethodName(input));
    }

    #endregion

    #region Field & parameter names

    [Theory]
    [InlineData("DoSomething", "doSomethingRpcMethod")]
    [InlineData("Join", "joinRpcMethod")]
    [InlineData("A", "aRpcMethod")]
    public void GetClientMethodFieldName_ReturnsCamelCaseWithSuffix(string input, string expected)
    {
        Assert.Equal(expected, NamingHelper.GetClientMethodFieldName(input));
    }

    [Theory]
    [InlineData("OnEvent", "onEventPushMethod")]
    [InlineData("Notify", "notifyPushMethod")]
    public void GetCallbackMethodFieldName_ReturnsCamelCaseWithSuffix(string input, string expected)
    {
        Assert.Equal(expected, NamingHelper.GetCallbackMethodFieldName(input));
    }

    [Theory]
    [InlineData("DoWork", "doWorkHandler")]
    public void GetHandlerParameterName_AppendHandler(string input, string expected)
    {
        Assert.Equal(expected, NamingHelper.GetHandlerParameterName(input));
    }

    [Theory]
    [InlineData("DoWork", "_doWorkHandler")]
    public void GetHandlerFieldName_PrefixUnderscore(string input, string expected)
    {
        Assert.Equal(expected, NamingHelper.GetHandlerFieldName(input));
    }

    [Theory]
    [InlineData("IPlayerService", "playerService")]
    [InlineData("IChatService", "chatService")]
    [InlineData("Foo", "foo")]
    public void GetServiceParamName_ReturnsCamelCase(string input, string expected)
    {
        Assert.Equal(expected, NamingHelper.GetServiceParamName(input));
    }

    #endregion

    #region ToCamelCase

    [Theory]
    [InlineData("Hello", "hello")]
    [InlineData("", "method")]
    [InlineData("X", "x")]
    [InlineData("ABC", "aBC")]
    public void ToCamelCase_LowersFirstChar(string input, string expected)
    {
        Assert.Equal(expected, NamingHelper.ToCamelCase(input));
    }

    #endregion

    #region GetNamespaceFromFullName

    [Theory]
    [InlineData("MyGame.Contracts.IPlayerService", "MyGame.Contracts")]
    [InlineData("IPlayerService", "")]
    [InlineData("A.B.C", "A.B")]
    [InlineData("Single.Type", "Single")]
    [InlineData("X", "")]
    public void GetNamespaceFromFullName_ReturnsNamespace(string input, string expected)
    {
        Assert.Equal(expected, NamingHelper.GetNamespaceFromFullName(input));
    }

    #endregion

    #region Payload types

    [Fact]
    public void GetRequestPayloadType_OneParam_ReturnsDtoType()
    {
        var m = new RpcMethodInfo("T", 1, [new RpcParameterInfo("LoginRequest", "request")], null, true);
        Assert.Equal("LoginRequest", NamingHelper.GetRequestPayloadType(m));
    }

    [Fact]
    public void GetRequestPayloadType_ZeroParams_Throws()
    {
        var m = new RpcMethodInfo("T", 1, [], null, true);
        var ex = Assert.Throws<InvalidOperationException>(() => NamingHelper.GetRequestPayloadType(m));
        Assert.Contains("must declare exactly one DTO parameter", ex.Message);
    }

    [Fact]
    public void GetRequestPayloadType_MultipleParams_Throws()
    {
        var m = new RpcMethodInfo("T", 1,
            [new RpcParameterInfo("int", "x"), new RpcParameterInfo("string", "y")], null, true);
        var ex = Assert.Throws<InvalidOperationException>(() => NamingHelper.GetRequestPayloadType(m));
        Assert.Contains("must declare exactly one DTO parameter", ex.Message);
    }

    [Fact]
    public void GetRequestPayloadValue_OneParam_ReturnsName()
    {
        var m = new RpcMethodInfo("T", 1, [new RpcParameterInfo("LoginRequest", "request")], null, true);
        Assert.Equal("request", NamingHelper.GetRequestPayloadValue(m));
    }

    [Fact]
    public void GetRequestPayloadValue_ZeroParams_Throws()
    {
        var m = new RpcMethodInfo("T", 1, [], null, true);
        var ex = Assert.Throws<InvalidOperationException>(() => NamingHelper.GetRequestPayloadValue(m));
        Assert.Contains("must declare exactly one DTO parameter", ex.Message);
    }

    [Fact]
    public void GetCallbackPayloadType_OneParam_ReturnsDtoType()
    {
        var m = new RpcCallbackMethodInfo("T", 1, [new RpcParameterInfo("PlayerNotify", "notify")]);
        Assert.Equal("PlayerNotify", NamingHelper.GetCallbackPayloadType(m));
    }

    [Fact]
    public void GetCallbackPayloadType_ZeroParams_Throws()
    {
        var m = new RpcCallbackMethodInfo("T", 1, []);
        var ex = Assert.Throws<InvalidOperationException>(() => NamingHelper.GetCallbackPayloadType(m));
        Assert.Contains("must declare exactly one DTO parameter", ex.Message);
    }

    [Fact]
    public void GetCallbackPayloadValue_OneParam_ReturnsName()
    {
        var m = new RpcCallbackMethodInfo("T", 1, [new RpcParameterInfo("PlayerNotify", "notify")]);
        Assert.Equal("notify", NamingHelper.GetCallbackPayloadValue(m));
    }

    #endregion

    #region Return types and delegates

    [Fact]
    public void GetInterfaceReturnType_Void_ReturnsValueTask()
    {
        var m = new RpcMethodInfo("T", 1, [], null, true);
        Assert.Equal("ValueTask", NamingHelper.GetInterfaceReturnType(m));
    }

    [Fact]
    public void GetInterfaceReturnType_NonVoid_ReturnsGenericValueTask()
    {
        var m = new RpcMethodInfo("T", 1, [], "string", false);
        Assert.Equal("ValueTask<string>", NamingHelper.GetInterfaceReturnType(m));
    }

    [Fact]
    public void GetDelegateType_NoParams_Void()
    {
        var m = new RpcMethodInfo("T", 1, [], null, true);
        Assert.Equal("Func<ValueTask>", NamingHelper.GetDelegateType(m));
    }

    [Fact]
    public void GetDelegateType_WithParams_NonVoid()
    {
        var m = new RpcMethodInfo("T", 1,
            [new RpcParameterInfo("int", "x"), new RpcParameterInfo("string", "y")],
            "bool", false);
        Assert.Equal("Func<int, string, ValueTask<bool>>", NamingHelper.GetDelegateType(m));
    }

    [Fact]
    public void GetDelegateType_SingleParam_Void()
    {
        var m = new RpcMethodInfo("T", 1, [new RpcParameterInfo("int", "x")], null, true);
        Assert.Equal("Func<int, ValueTask>", NamingHelper.GetDelegateType(m));
    }

    #endregion

    #region Parameter signatures

    [Fact]
    public void GetMethodParameterSignature_Empty_ReturnsEmpty()
    {
        Assert.Equal("", NamingHelper.GetMethodParameterSignature([]));
    }

    [Fact]
    public void GetMethodParameterSignature_Single()
    {
        Assert.Equal("int x", NamingHelper.GetMethodParameterSignature([new RpcParameterInfo("int", "x")]));
    }

    [Fact]
    public void GetMethodParameterSignature_Multiple()
    {
        var p = new List<RpcParameterInfo> { new("int", "x"), new("string", "name") };
        Assert.Equal("int x, string name", NamingHelper.GetMethodParameterSignature(p));
    }

    #endregion

    #region Forward arguments

    [Fact]
    public void GetForwardArguments_WithCt()
    {
        var p = new List<RpcParameterInfo> { new("int", "x"), new("string", "y") };
        Assert.Equal("x, y, ct", NamingHelper.GetForwardArguments(p, true));
    }

    [Fact]
    public void GetForwardArguments_WithoutCt()
    {
        var p = new List<RpcParameterInfo> { new("int", "x") };
        Assert.Equal("x, CancellationToken.None", NamingHelper.GetForwardArguments(p, false));
    }

    [Fact]
    public void GetForwardArguments_EmptyParams_OnlyCt()
    {
        Assert.Equal("ct", NamingHelper.GetForwardArguments([], true));
        Assert.Equal("CancellationToken.None", NamingHelper.GetForwardArguments([], false));
    }

    #endregion

    #region Using directives

    [Fact]
    public void GetContractUsingDirectives_SingleService_Deduplicates()
    {
        var svc = new RpcServiceInfo("IFoo", "Ns.IFoo", 1,
            [new RpcMethodInfo("M", 1, [], null, true)], ["Ns", "Ns", "Other"]);
        var result = NamingHelper.GetContractUsingDirectives(svc);

        Assert.Equal(2, result.Count); // "Ns" and "Other"
        Assert.Contains("Ns", result);
        Assert.Contains("Other", result);
    }

    [Fact]
    public void GetContractUsingDirectives_MultipleServices_MergesAndDeduplicates()
    {
        var svc1 = new RpcServiceInfo("IA", "Ns1.IA", 1,
            [new RpcMethodInfo("M", 1, [], null, true)], ["Ns1"]);
        var svc2 = new RpcServiceInfo("IB", "Ns2.IB", 2,
            [new RpcMethodInfo("M", 1, [], null, true)], ["Ns2", "Ns1"]);

        var result = NamingHelper.GetContractUsingDirectives(new[] { svc1, svc2 });

        Assert.Contains("Ns1", result);
        Assert.Contains("Ns2", result);
        Assert.Equal(1, result.Count(d => d == "Ns1"));
    }

    [Fact]
    public void ExcludeUsingDirectives_FiltersCorrectly()
    {
        var usings = new[] { "System", "System.Linq", "MyApp" };
        var result = NamingHelper.ExcludeUsingDirectives(usings, "System");
        Assert.Equal(["System.Linq", "MyApp"], result);
    }

    [Fact]
    public void ExcludeUsingDirectives_MultipleExclusions()
    {
        var usings = new[] { "A", "B", "C", "D" };
        var result = NamingHelper.ExcludeUsingDirectives(usings, "A", "C");
        Assert.Equal(["B", "D"], result);
    }

    [Fact]
    public void ExcludeUsingDirectives_NoExclusions_ReturnsAll()
    {
        var usings = new[] { "A", "B" };
        var result = NamingHelper.ExcludeUsingDirectives(usings);
        Assert.Equal(["A", "B"], result);
    }

    #endregion

    #region DefaultServerNamespace

    [Fact]
    public void GetDefaultServerNamespace_StripContracts()
    {
        var svc = new RpcServiceInfo("IFoo", "MyGame.Contracts.IFoo", 1,
            [new RpcMethodInfo("Bar", 1, [], null, true)], []);
        Assert.Equal("MyGame.Server.Generated", NamingHelper.GetDefaultServerNamespace([svc]));
    }

    [Fact]
    public void GetDefaultServerNamespace_NoContractsSuffix()
    {
        var svc = new RpcServiceInfo("IFoo", "MyGame.Services.IFoo", 1,
            [new RpcMethodInfo("Bar", 1, [], null, true)], []);
        Assert.Equal("MyGame.Services.Server.Generated", NamingHelper.GetDefaultServerNamespace([svc]));
    }

    [Fact]
    public void GetDefaultServerNamespace_Empty_ReturnsFallback()
    {
        Assert.Equal("ULinkRPC.Server.Generated", NamingHelper.GetDefaultServerNamespace([]));
    }

    [Fact]
    public void GetDefaultServerNamespace_TopLevelInterface()
    {
        var svc = new RpcServiceInfo("IFoo", "IFoo", 1,
            [new RpcMethodInfo("Bar", 1, [], null, true)], []);
        Assert.Equal("ULinkRPC.Server.Generated", NamingHelper.GetDefaultServerNamespace([svc]));
    }

    #endregion

    #region ToPascalIdentifier

    [Theory]
    [InlineData("hello", "Hello")]
    [InlineData("hello-world", "HelloWorld")]
    [InlineData("hello_world", "HelloWorld")]
    [InlineData("123abc", "_123abc")]
    [InlineData("", "Default")]
    [InlineData("   ", "Default")]
    [InlineData("already-UPPER", "AlreadyUPPER")]
    [InlineData("a.b.c", "ABC")]
    public void ToPascalIdentifier_Converts(string input, string expected)
    {
        Assert.Equal(expected, NamingHelper.ToPascalIdentifier(input));
    }

    #endregion
}
