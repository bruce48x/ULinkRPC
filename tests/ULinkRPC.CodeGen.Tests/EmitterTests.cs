using ULinkRPC.CodeGen;
using Xunit;

namespace ULinkRPC.CodeGen.Tests;

public class EmitterTests
{
    private static RpcServiceInfo CreateSimpleService() => new(
        "IPlayerService", "MyGame.Contracts.IPlayerService", 1,
        [
            new RpcMethodInfo("GetName", 1, [new RpcParameterInfo("int", "playerId")], "string", false),
            new RpcMethodInfo("SetName", 2, [new RpcParameterInfo("string", "name")], null, true),
        ],
        ["MyGame.Contracts"]);

    [Fact]
    public void ClientEmitter_GeneratesClientClass()
    {
        var svc = CreateSimpleService();
        var code = ClientEmitter.GenerateClient(svc, "Test.Generated", "ULinkRPC.Core");

        Assert.Contains("public sealed class PlayerServiceClient : IPlayerService", code);
        Assert.Contains("private const int ServiceId = 1;", code);
        Assert.Contains("private readonly IRpcClient _client;", code);
        Assert.Contains("public PlayerServiceClient(IRpcClient client)", code);
        Assert.Contains("getNameRpcMethod", code);
        Assert.Contains("setNameRpcMethod", code);
    }

    [Fact]
    public void ClientEmitter_GeneratesExtensionMethod()
    {
        var svc = CreateSimpleService();
        var code = ClientEmitter.GenerateClient(svc, "Test.Generated", "ULinkRPC.Core");

        Assert.Contains("public static class PlayerServiceClientExtensions", code);
        Assert.Contains("public static IPlayerService CreatePlayerService(this IRpcClient client)", code);
    }

    [Fact]
    public void ClientEmitter_GeneratesUsings()
    {
        var svc = CreateSimpleService();
        var code = ClientEmitter.GenerateClient(svc, "Test.Generated", "ULinkRPC.Core");

        Assert.Contains("using System;", code);
        Assert.Contains("using ULinkRPC.Core;", code);
        Assert.Contains("namespace Test.Generated", code);
    }

    [Fact]
    public void ServerEmitter_GeneratesBinder()
    {
        var svc = CreateSimpleService();
        var code = ServerEmitter.GenerateBinder(svc, "Test.Server", "ULinkRPC.Core", "ULinkRPC.Server");

        Assert.Contains("public static class PlayerServiceBinder", code);
        Assert.Contains("private const int ServiceId = 1;", code);
        Assert.Contains("public static void Bind(RpcServer server, IPlayerService impl)", code);
        Assert.Contains("server.Register(ServiceId, 1,", code);
        Assert.Contains("server.Register(ServiceId, 2,", code);
    }

    [Fact]
    public void ServerEmitter_GeneratesDelegateOverload()
    {
        var svc = CreateSimpleService();
        var code = ServerEmitter.GenerateBinder(svc, "Test.Server", "ULinkRPC.Core", "ULinkRPC.Server");

        Assert.Contains("private sealed class DelegateImpl : IPlayerService", code);
    }

    [Fact]
    public void ServerEmitter_GeneratesAllServicesBinder()
    {
        var svc = CreateSimpleService();
        var code = ServerEmitter.GenerateAllServicesBinder([svc], "Test.Server", "ULinkRPC.Server");

        Assert.Contains("public static class AllServicesBinder", code);
        Assert.Contains("public static void BindAll(RpcServer server, IPlayerService playerService)", code);
        Assert.Contains("PlayerServiceBinder.Bind(server, playerService);", code);
    }

    [Fact]
    public void ServerEmitter_CallbackProxy_UsesFireAndForget()
    {
        var svc = new RpcServiceInfo("IGameService", "MyGame.Contracts.IGameService", 5,
            [new RpcMethodInfo("Start", 1, [], null, true)],
            ["MyGame.Contracts"])
        {
            CallbackInterfaceName = "IGameCallback",
            CallbackInterfaceFullName = "MyGame.Contracts.IGameCallback",
        };
        svc.CallbackMethods =
        [
            new RpcCallbackMethodInfo("OnEvent", 1, [new RpcParameterInfo("int", "code")])
        ];

        var code = ServerEmitter.GenerateCallbackProxy(svc, "Test.Server", "ULinkRPC.Core", "ULinkRPC.Server");

        Assert.Contains("public sealed class GameCallbackProxy : IGameCallback", code);
        Assert.Contains("_ = _server.PushAsync<int>(ServiceId, 1, code).AsTask();", code);
        Assert.DoesNotContain(".Wait()", code);
    }

    [Fact]
    public void FacadeEmitter_GeneratesRpcApi()
    {
        var svc = CreateSimpleService();
        var code = FacadeEmitter.GenerateClientFacade([svc], "Test.Generated", "ULinkRPC.Core");

        Assert.Contains("public sealed class RpcApi", code);
        Assert.Contains("public static class RpcApiExtensions", code);
        Assert.Contains("public static RpcApi CreateRpcApi(this IRpcClient client)", code);
    }

    [Fact]
    public void ClientEmitter_CallbackBinder_GeneratesBindMethod()
    {
        var svc = new RpcServiceInfo("IGameService", "MyGame.Contracts.IGameService", 5,
            [new RpcMethodInfo("Start", 1, [], null, true)],
            ["MyGame.Contracts"])
        {
            CallbackInterfaceName = "IGameCallback",
            CallbackInterfaceFullName = "MyGame.Contracts.IGameCallback"
        };
        svc.CallbackMethods =
        [
            new RpcCallbackMethodInfo("OnEvent", 1, [new RpcParameterInfo("int", "code")])
        ];

        var code = ClientEmitter.GenerateCallbackBinder(svc, "Test.Generated", "ULinkRPC.Core");

        Assert.Contains("public static class GameCallbackBinder", code);
        Assert.Contains("public static void Bind(IRpcClient client, IGameCallback receiver)", code);
        Assert.Contains("onEventPushMethod", code);
    }
}
