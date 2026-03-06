using ULinkRPC.CodeGen;
using Xunit;

namespace ULinkRPC.CodeGen.Tests;

public class NamingHelperTests
{
    [Theory]
    [InlineData("IPlayerService", "PlayerService")]
    [InlineData("IChatService", "ChatService")]
    [InlineData("PlayerService", "PlayerService")]
    [InlineData("I", "I")]
    [InlineData("Ia", "Ia")]
    public void GetServiceTypeName_StripsLeadingI(string input, string expected)
    {
        Assert.Equal(expected, NamingHelper.GetServiceTypeName(input));
    }

    [Theory]
    [InlineData("IPlayerService", "PlayerServiceClient")]
    [InlineData("IChatService", "ChatServiceClient")]
    public void GetClientTypeName_AppendsClient(string input, string expected)
    {
        Assert.Equal(expected, NamingHelper.GetClientTypeName(input));
    }

    [Theory]
    [InlineData("IPlayerService", "PlayerServiceBinder")]
    public void GetBinderTypeName_AppendsBinder(string input, string expected)
    {
        Assert.Equal(expected, NamingHelper.GetBinderTypeName(input));
    }

    [Theory]
    [InlineData("IGameCallback", "GameCallbackProxy")]
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
    [InlineData("DoSomething", "doSomethingRpcMethod")]
    [InlineData("Join", "joinRpcMethod")]
    public void GetClientMethodFieldName_ReturnsCamelCaseWithSuffix(string input, string expected)
    {
        Assert.Equal(expected, NamingHelper.GetClientMethodFieldName(input));
    }

    [Theory]
    [InlineData("OnEvent", "onEventPushMethod")]
    public void GetCallbackMethodFieldName_ReturnsCamelCaseWithSuffix(string input, string expected)
    {
        Assert.Equal(expected, NamingHelper.GetCallbackMethodFieldName(input));
    }

    [Theory]
    [InlineData("IPlayerService", "CreatePlayerService")]
    public void GetClientFactoryMethodName_PrependCreate(string input, string expected)
    {
        Assert.Equal(expected, NamingHelper.GetClientFactoryMethodName(input));
    }

    [Theory]
    [InlineData("IPlayerService", "playerService")]
    [InlineData("IChatService", "chatService")]
    public void GetServiceParamName_ReturnsCamelCase(string input, string expected)
    {
        Assert.Equal(expected, NamingHelper.GetServiceParamName(input));
    }

    [Theory]
    [InlineData("Hello", "hello")]
    [InlineData("", "method")]
    [InlineData("X", "x")]
    public void ToCamelCase_LowersFirstChar(string input, string expected)
    {
        Assert.Equal(expected, NamingHelper.ToCamelCase(input));
    }

    [Theory]
    [InlineData("MyGame.Contracts.IPlayerService", "MyGame.Contracts")]
    [InlineData("IPlayerService", "")]
    [InlineData("A.B.C", "A.B")]
    public void GetNamespaceFromFullName_ReturnsNamespace(string input, string expected)
    {
        Assert.Equal(expected, NamingHelper.GetNamespaceFromFullName(input));
    }

    [Fact]
    public void GetRequestPayloadType_ZeroParams_ReturnsRpcVoid()
    {
        var method = new RpcMethodInfo("Test", 1, [], null, true);
        Assert.Equal("RpcVoid", NamingHelper.GetRequestPayloadType(method));
    }

    [Fact]
    public void GetRequestPayloadType_OneParam_ReturnsParamType()
    {
        var method = new RpcMethodInfo("Test", 1,
            [new RpcParameterInfo("int", "x")], null, true);
        Assert.Equal("int", NamingHelper.GetRequestPayloadType(method));
    }

    [Fact]
    public void GetRequestPayloadType_MultipleParams_ReturnsTuple()
    {
        var method = new RpcMethodInfo("Test", 1,
            [new RpcParameterInfo("int", "x"), new RpcParameterInfo("string", "y")],
            null, true);
        Assert.Equal("(int, string)", NamingHelper.GetRequestPayloadType(method));
    }

    [Fact]
    public void GetMethodParameterSignature_Empty_ReturnsEmpty()
    {
        Assert.Equal("", NamingHelper.GetMethodParameterSignature([]));
    }

    [Fact]
    public void GetMethodParameterSignature_Multiple_Formats()
    {
        var parameters = new List<RpcParameterInfo>
        {
            new("int", "x"),
            new("string", "name")
        };
        Assert.Equal("int x, string name", NamingHelper.GetMethodParameterSignature(parameters));
    }

    [Fact]
    public void GetDefaultServerNamespace_StripContracts()
    {
        var svc = new RpcServiceInfo("IFoo", "MyGame.Contracts.IFoo", 1,
            [new RpcMethodInfo("Bar", 1, [], null, true)], []);
        var result = NamingHelper.GetDefaultServerNamespace([svc]);
        Assert.Equal("MyGame.Server.Generated", result);
    }

    [Fact]
    public void GetDefaultServerNamespace_NoContracts_KeepsBase()
    {
        var svc = new RpcServiceInfo("IFoo", "MyGame.Services.IFoo", 1,
            [new RpcMethodInfo("Bar", 1, [], null, true)], []);
        var result = NamingHelper.GetDefaultServerNamespace([svc]);
        Assert.Equal("MyGame.Services.Server.Generated", result);
    }

    [Fact]
    public void GetDefaultServerNamespace_Empty_ReturnsFallback()
    {
        var result = NamingHelper.GetDefaultServerNamespace([]);
        Assert.Equal("ULinkRPC.Server.Generated", result);
    }

    [Fact]
    public void ExcludeUsingDirectives_FiltersCorrectly()
    {
        var usings = new[] { "System", "System.Linq", "MyApp" };
        var result = NamingHelper.ExcludeUsingDirectives(usings, "System");
        Assert.Equal(["System.Linq", "MyApp"], result);
    }

    [Theory]
    [InlineData("hello", "Hello")]
    [InlineData("hello-world", "HelloWorld")]
    [InlineData("123abc", "_123abc")]
    [InlineData("", "Default")]
    public void ToPascalIdentifier_Converts(string input, string expected)
    {
        Assert.Equal(expected, NamingHelper.ToPascalIdentifier(input));
    }

    [Fact]
    public void GetForwardArguments_WithCt()
    {
        var parameters = new List<RpcParameterInfo>
        {
            new("int", "x"),
            new("string", "y")
        };
        Assert.Equal("x, y, ct", NamingHelper.GetForwardArguments(parameters, true));
    }

    [Fact]
    public void GetForwardArguments_WithoutCt()
    {
        var parameters = new List<RpcParameterInfo>
        {
            new("int", "x"),
        };
        Assert.Equal("x, CancellationToken.None", NamingHelper.GetForwardArguments(parameters, false));
    }

    [Fact]
    public void GetDeconstructVariableList_ReturnsArgList()
    {
        Assert.Equal("arg1, arg2, arg3", NamingHelper.GetDeconstructVariableList(3));
    }

    [Fact]
    public void GetInvokeArguments_Zero_ReturnsEmpty()
    {
        Assert.Equal("", NamingHelper.GetInvokeArguments(0));
    }

    [Fact]
    public void GetInvokeArguments_NonZero_ReturnsArgList()
    {
        Assert.Equal("arg1, arg2", NamingHelper.GetInvokeArguments(2));
    }
}
