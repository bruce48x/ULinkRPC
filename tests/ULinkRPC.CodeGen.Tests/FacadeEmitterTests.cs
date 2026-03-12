using ULinkRPC.CodeGen;
using Xunit;

namespace ULinkRPC.CodeGen.Tests;

public class FacadeEmitterTests
{
    private static RpcServiceInfo MakeSvc(string iface, string fullName, int id) =>
        new(iface, fullName, id, [new RpcMethodInfo("Do", 1, [], null, true)], []);

    #region Basic facade

    [Fact]
    public void GeneratesRpcApiClass()
    {
        var svc = MakeSvc("IPlayerService", "Game.IPlayerService", 1);
        var code = FacadeEmitter.GenerateClientFacade([svc], "Gen", "ULinkRPC.Core", "ULinkRPC.Client");

        Assert.Contains("public sealed class RpcApi", code);
        Assert.Contains("public RpcApi(IRpcClient client)", code);
        Assert.Contains("if (client is null) throw new ArgumentNullException(nameof(client));", code);
    }

    [Fact]
    public void GeneratesExtensionMethod()
    {
        var svc = MakeSvc("IPlayerService", "Game.IPlayerService", 1);
        var code = FacadeEmitter.GenerateClientFacade([svc], "Gen", "ULinkRPC.Core", "ULinkRPC.Client");

        Assert.Contains("public static class RpcApiExtensions", code);
        Assert.Contains("public static RpcApi CreateRpcApi(this IRpcClient client)", code);
    }

    [Fact]
    public void GeneratesTypedConnection()
    {
        var svc = new RpcServiceInfo("IPlayerService", "Game.IPlayerService", 1,
            [new RpcMethodInfo("Do", 1, [], null, true)], [])
        {
            CallbackInterfaceName = "IPlayerCallback",
            CallbackInterfaceFullName = "Game.IPlayerCallback"
        };
        svc.CallbackMethods =
        [
            new RpcCallbackMethodInfo("OnNotify", 1, [new RpcParameterInfo("string", "message")])
        ];

        var code = FacadeEmitter.GenerateClientFacade([svc], "Gen", "ULinkRPC.Core", "ULinkRPC.Client");

        Assert.Contains("public sealed class RpcConnection : IAsyncDisposable", code);
        Assert.Contains("public static ValueTask<RpcConnection> ConnectAsync(RpcClientBuilder builder, CancellationToken ct = default)", code);
        Assert.Contains("public static ValueTask<RpcConnection> ConnectAsync(RpcClientBuilder builder, RpcCallbackBindings callbacks, CancellationToken ct = default)", code);
        Assert.Contains("public sealed class RpcCallbackBindings", code);
        Assert.Contains("public void Add(IPlayerCallback playerCallback)", code);
        Assert.Contains("PlayerCallbackBinder.Bind(client, _playerCallback);", code);
        Assert.Contains("public abstract class PlayerCallbackBase : IPlayerCallback", code);
        Assert.Contains("public virtual void OnNotify(string message)", code);
        Assert.DoesNotContain("ConnectAsync<TCallbacks>", code);
        Assert.DoesNotContain("public abstract class GameRpcCallbacksBase", code);
        Assert.DoesNotContain("SetPlayerCallbackOnNotify", code);
        Assert.Contains("public sealed class GameRpcClient : IAsyncDisposable", code);
        Assert.Contains("public static async ValueTask<GameRpcClient> ConnectAsync(RpcClientBuilder builder, CancellationToken ct = default)", code);
        Assert.Contains("public static async ValueTask<GameRpcClient> ConnectAsync(RpcClientBuilder builder, RpcConnection.RpcCallbackBindings callbacks, CancellationToken ct = default)", code);
    }

    [Fact]
    public void GeneratesGroupClass()
    {
        var svc = MakeSvc("IPlayerService", "Game.IPlayerService", 1);
        var code = FacadeEmitter.GenerateClientFacade([svc], "Gen", "ULinkRPC.Core", "ULinkRPC.Client");

        Assert.Contains("RpcGroup", code);
        Assert.Contains("client.CreatePlayerService()", code);
    }

    #endregion

    #region Grouping

    [Fact]
    public void MultipleServices_SameNamespace_SameGroup()
    {
        var svc1 = MakeSvc("IPlayerService", "Game.IPlayerService", 1);
        var svc2 = MakeSvc("IChatService", "Game.IChatService", 2);
        var code = FacadeEmitter.GenerateClientFacade([svc1, svc2], "Gen", "ULinkRPC.Core", "ULinkRPC.Client");

        var groupCount = code.Split("RpcGroup").Length - 1;
        Assert.True(groupCount >= 2); // type name + property
    }

    [Fact]
    public void MultipleServices_DifferentNamespaces_DifferentGroups()
    {
        var svc1 = MakeSvc("IPlayerService", "Game.IPlayerService", 1);
        var svc2 = MakeSvc("IAuthService", "Auth.IAuthService", 2);
        var code = FacadeEmitter.GenerateClientFacade([svc1, svc2], "Gen", "ULinkRPC.Core", "ULinkRPC.Client");

        Assert.Contains("GameRpcGroup", code);
        Assert.Contains("AuthRpcGroup", code);
        Assert.Contains("public sealed class RpcClientHost : IAsyncDisposable", code);
    }

    [Fact]
    public void ServicePropertyName_StripsServiceSuffix()
    {
        var svc = MakeSvc("IPlayerService", "Game.IPlayerService", 1);
        var code = FacadeEmitter.GenerateClientFacade([svc], "Gen", "ULinkRPC.Core", "ULinkRPC.Client");

        Assert.Contains("Player { get; }", code);
    }

    [Fact]
    public void ServicePropertyName_NoServiceSuffix_UsesTypeName()
    {
        var svc = MakeSvc("IChat", "Game.IChat", 1);
        var code = FacadeEmitter.GenerateClientFacade([svc], "Gen", "ULinkRPC.Core", "ULinkRPC.Client");

        Assert.Contains("Chat { get; }", code);
    }

    [Fact]
    public void DuplicatePropertyNames_GetSuffix()
    {
        var svc1 = MakeSvc("IPlayerService", "Game.Sub1.IPlayerService", 1);
        var svc2 = MakeSvc("IPlayerService", "Game.Sub2.IPlayerService", 2);
        var code = FacadeEmitter.GenerateClientFacade([svc1, svc2], "Gen", "ULinkRPC.Core", "ULinkRPC.Client");

        Assert.Contains("Player { get; }", code);
        Assert.Contains("Player2 { get; }", code);
    }

    [Fact]
    public void TopLevelInterface_GroupedAsDefault()
    {
        var svc = MakeSvc("IFoo", "IFoo", 1);
        var code = FacadeEmitter.GenerateClientFacade([svc], "Gen", "ULinkRPC.Core", "ULinkRPC.Client");

        Assert.Contains("DefaultRpcGroup", code);
    }

    #endregion

    #region Usings

    [Fact]
    public void IncludesContractUsings()
    {
        var svc = new RpcServiceInfo("ISvc", "My.Contracts.ISvc", 1,
            [new RpcMethodInfo("Do", 1, [], null, true)], ["My.Contracts"]);
        var code = FacadeEmitter.GenerateClientFacade([svc], "Gen", "ULinkRPC.Core", "ULinkRPC.Client");

        Assert.Contains("using My.Contracts;", code);
        Assert.Contains("using ULinkRPC.Core;", code);
        Assert.Contains("using ULinkRPC.Client;", code);
    }

    #endregion
}
