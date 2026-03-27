using ULinkRPC.CodeGen;
using Xunit;

namespace ULinkRPC.CodeGen.Tests;

public class ContractParserTests : IDisposable
{
    private readonly List<string> _tempDirs = [];

    public void Dispose()
    {
        foreach (var dir in _tempDirs)
            if (Directory.Exists(dir))
                Directory.Delete(dir, true);
    }

    private string CreateTempContracts(params string[] files)
    {
        var dir = Path.Combine(Path.GetTempPath(), $"ulinkrpc_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        _tempDirs.Add(dir);
        for (var i = 0; i < files.Length; i++)
            File.WriteAllText(Path.Combine(dir, $"File{i}.cs"), files[i]);
        return dir;
    }

    private const string AttributeDefinitions = """
        using System;
        using System.Threading.Tasks;

        [AttributeUsage(AttributeTargets.Interface)]
        public class RpcServiceAttribute : Attribute
        {
            public RpcServiceAttribute(int id) { }
            public Type Callback { get; set; }
        }

        [AttributeUsage(AttributeTargets.Interface)]
        public class RpcCallbackAttribute : Attribute
        {
            public RpcCallbackAttribute(Type serviceType) { }
        }

        [AttributeUsage(AttributeTargets.Method)]
        public class RpcMethodAttribute : Attribute
        {
            public RpcMethodAttribute(int id) { }
        }

        [AttributeUsage(AttributeTargets.Method)]
        public class RpcPushAttribute : Attribute
        {
            public RpcPushAttribute(int id) { }
        }
        """;

    #region Basic parsing

    [Fact]
    public void ParsesSimpleService_WithVoidAndNonVoidMethods()
    {
        var dir = CreateTempContracts(AttributeDefinitions, """
            using System.Threading.Tasks;

            public class LoginRequest { public string Account { get; set; } = ""; }
            public class LoginReply { public string Token { get; set; } = ""; }

            [RpcService(1)]
            public interface IPlayerService
            {
                [RpcMethod(1)]
                ValueTask<LoginReply> LoginAsync(LoginRequest request);
            }
            """);

        var services = ContractParser.FindRpcServicesFromSource(dir);

        Assert.Single(services);
        var svc = services[0];
        Assert.Equal("IPlayerService", svc.InterfaceName);
        Assert.Equal(1, svc.ServiceId);
        Assert.Single(svc.Methods);

        var login = svc.Methods[0];
        Assert.Equal("LoginAsync", login.Name);
        Assert.Equal(1, login.MethodId);
        Assert.False(login.IsVoid);
        Assert.Equal("LoginReply", login.RetTypeName);
        Assert.Single(login.Parameters);
        Assert.Equal("LoginRequest", login.Parameters[0].TypeName);
        Assert.Equal("request", login.Parameters[0].Name);
    }

    [Fact]
    public void MultipleParameters_ThrowInvalidOperation()
    {
        var dir = CreateTempContracts(AttributeDefinitions, """
            using System.Threading.Tasks;

            public class CheckRequest { }
            public class CheckReply { }

            [RpcService(10)]
            public interface IMultiSvc
            {
                [RpcMethod(5)]
                ValueTask<CheckReply> Check(CheckRequest request, string extra);
            }
            """);

        var ex = Assert.Throws<InvalidOperationException>(
            () => ContractParser.FindRpcServicesFromSource(dir));
        Assert.Contains("exactly one request DTO parameter", ex.Message);
    }

    [Fact]
    public void ZeroParameterMethod_ThrowsInvalidOperation()
    {
        var dir = CreateTempContracts(AttributeDefinitions, """
            using System.Threading.Tasks;

            [RpcService(1)]
            public interface ISvc
            {
                [RpcMethod(1)]
                ValueTask PingAsync();
            }
            """);

        var ex = Assert.Throws<InvalidOperationException>(
            () => ContractParser.FindRpcServicesFromSource(dir));
        Assert.Contains("exactly one request DTO parameter", ex.Message);
    }

    #endregion

    #region Filtering

    [Fact]
    public void IgnoresInterfacesWithoutAttribute()
    {
        var dir = CreateTempContracts("""
            using System.Threading.Tasks;
            public interface INotAnRpcService
            {
                ValueTask DoStuff();
            }
            """);

        Assert.Empty(ContractParser.FindRpcServicesFromSource(dir));
    }

    [Fact]
    public void SkipsMethodsWithoutRpcMethodAttribute()
    {
        var dir = CreateTempContracts(AttributeDefinitions, """
            using System.Threading.Tasks;
            public class TaggedRequest { }

            [RpcService(1)]
            public interface ISvc
            {
                [RpcMethod(1)]
                ValueTask Tagged(TaggedRequest request);

                ValueTask Untagged();
            }
            """);

        var services = ContractParser.FindRpcServicesFromSource(dir);
        Assert.Single(services);
        Assert.Single(services[0].Methods);
        Assert.Equal("Tagged", services[0].Methods[0].Name);
    }

    [Fact]
    public void SkipsServicesWithNoTaggedMethods()
    {
        var dir = CreateTempContracts(AttributeDefinitions, """
            using System.Threading.Tasks;

            [RpcService(1)]
            public interface IEmptySvc
            {
                ValueTask NotTagged();
            }
            """);

        Assert.Empty(ContractParser.FindRpcServicesFromSource(dir));
    }

    #endregion

    #region Multiple services and files

    [Fact]
    public void MultipleServicesInOneFile()
    {
        var dir = CreateTempContracts(AttributeDefinitions, """
            using System.Threading.Tasks;
            public class RequestA { }
            public class RequestB { }

            [RpcService(1)]
            public interface IServiceA
            {
                [RpcMethod(1)]
                ValueTask DoA(RequestA request);
            }

            [RpcService(2)]
            public interface IServiceB
            {
                [RpcMethod(1)]
                ValueTask DoB(RequestB request);
            }
            """);

        var services = ContractParser.FindRpcServicesFromSource(dir);
        Assert.Equal(2, services.Count);
        Assert.Contains(services, s => s.InterfaceName == "IServiceA" && s.ServiceId == 1);
        Assert.Contains(services, s => s.InterfaceName == "IServiceB" && s.ServiceId == 2);
    }

    [Fact]
    public void ServicesAcrossMultipleFiles()
    {
        var dir = CreateTempContracts(
            AttributeDefinitions,
            """
            using System.Threading.Tasks;
            public class RequestOne { }
            [RpcService(1)]
            public interface IFileOneService
            {
                [RpcMethod(1)]
                ValueTask Do1(RequestOne request);
            }
            """,
            """
            using System.Threading.Tasks;
            public class RequestTwo { }
            [RpcService(2)]
            public interface IFileTwoService
            {
                [RpcMethod(1)]
                ValueTask Do2(RequestTwo request);
            }
            """);

        var services = ContractParser.FindRpcServicesFromSource(dir);
        Assert.Equal(2, services.Count);
        Assert.Contains(services, s => s.InterfaceName == "IFileOneService");
        Assert.Contains(services, s => s.InterfaceName == "IFileTwoService");
    }

    #endregion

    #region Callback interfaces

    [Fact]
    public void ParsesCallbackInterface_ViaRpcCallbackAttribute()
    {
        var dir = CreateTempContracts(AttributeDefinitions, """
            using System;
            using System.Threading.Tasks;

            public class JoinedNotice { public int PlayerId { get; set; } }
            public class MessageNotice { public string Message { get; set; } = ""; }
            public class StartRequest { }

            [RpcCallback(typeof(IGameService))]
            public interface IGameCallback
            {
                [RpcPush(1)]
                void OnPlayerJoined(JoinedNotice notice);

                [RpcPush(2)]
                void OnMessage(MessageNotice notice);
            }

            [RpcService(5, Callback = typeof(IGameCallback))]
            public interface IGameService
            {
                [RpcMethod(1)]
                ValueTask Start(StartRequest request);
            }
            """);

        var services = ContractParser.FindRpcServicesFromSource(dir);
        Assert.Single(services);
        var svc = services[0];

        Assert.Equal("IGameService", svc.InterfaceName);
        Assert.Equal("IGameCallback", svc.CallbackInterfaceName);
        Assert.True(svc.HasCallback);
        Assert.Equal(2, svc.CallbackMethods.Count);
        Assert.Equal("OnPlayerJoined", svc.CallbackMethods[0].Name);
        Assert.Equal(1, svc.CallbackMethods[0].MethodId);
        Assert.Single(svc.CallbackMethods[0].Parameters);
        Assert.Equal("JoinedNotice", svc.CallbackMethods[0].Parameters[0].TypeName);
        Assert.Equal("OnMessage", svc.CallbackMethods[1].Name);
    }

    [Fact]
    public void CallbackInterface_ZeroParamMethod_ThrowsInvalidOperation()
    {
        var dir = CreateTempContracts(AttributeDefinitions, """
            using System;
            using System.Threading.Tasks;

            public class DoRequest { }

            [RpcCallback(typeof(ISvc))]
            public interface INotify
            {
                [RpcPush(1)]
                void Ping();
            }

            [RpcService(1, Callback = typeof(INotify))]
            public interface ISvc
            {
                [RpcMethod(1)]
                ValueTask Do(DoRequest request);
            }
            """);

        var ex = Assert.Throws<InvalidOperationException>(
            () => ContractParser.FindRpcServicesFromSource(dir));
        Assert.Contains("exactly one push DTO parameter", ex.Message);
    }

    [Fact]
    public void CallbackInterface_InDifferentFile()
    {
        var dir = CreateTempContracts(
            AttributeDefinitions,
            """
            using System;
            using System.Threading.Tasks;

            public class EventNotice { public int Code { get; set; } }

            [RpcCallback(typeof(IMySvc))]
            public interface IEvents
            {
                [RpcPush(1)]
                void OnEvent(EventNotice notice);
            }
            """,
            """
            using System.Threading.Tasks;

            public class ActRequest { }

            [RpcService(3, Callback = typeof(IEvents))]
            public interface IMySvc
            {
                [RpcMethod(1)]
                ValueTask Act(ActRequest request);
            }
            """);

        var services = ContractParser.FindRpcServicesFromSource(dir);
        var svc = Assert.Single(services);
        Assert.True(svc.HasCallback);
        Assert.Equal("IEvents", svc.CallbackInterfaceName);
        Assert.Single(svc.CallbackMethods);
    }

    [Fact]
    public void ServiceWithoutCallback_HasCallbackIsFalse()
    {
        var dir = CreateTempContracts(AttributeDefinitions, """
            using System.Threading.Tasks;
            public class DoRequest { }
            [RpcService(1)]
            public interface IPlainSvc
            {
                [RpcMethod(1)]
                ValueTask Do(DoRequest request);
            }
            """);

        var svc = Assert.Single(ContractParser.FindRpcServicesFromSource(dir));
        Assert.False(svc.HasCallback);
        Assert.Empty(svc.CallbackMethods);
    }

    #endregion

    #region Error cases

    [Fact]
    public void UnsupportedReturnType_ThrowsInvalidOperation()
    {
        var dir = CreateTempContracts(AttributeDefinitions, """
            using System.Threading.Tasks;
            public class BadRequest { }

            [RpcService(1)]
            public interface IBadSvc
            {
                [RpcMethod(1)]
                Task<int> NotValueTask(BadRequest request);
            }
            """);

        var ex = Assert.Throws<InvalidOperationException>(
            () => ContractParser.FindRpcServicesFromSource(dir));
        Assert.Contains("Unsupported return type", ex.Message);
        Assert.Contains("NotValueTask", ex.Message);
    }

    [Fact]
    public void VoidReturnType_ThrowsInvalidOperation()
    {
        var dir = CreateTempContracts(AttributeDefinitions, """
            public class BadRequest { }
            [RpcService(1)]
            public interface IBadSvc
            {
                [RpcMethod(1)]
                void BadMethod(BadRequest request);
            }
            """);

        var ex = Assert.Throws<InvalidOperationException>(
            () => ContractParser.FindRpcServicesFromSource(dir));
        Assert.Contains("Unsupported return type", ex.Message);
    }

    [Fact]
    public void DuplicateServiceId_ThrowsInvalidOperation()
    {
        var dir = CreateTempContracts(AttributeDefinitions, """
            using System.Threading.Tasks;

            public class DoRequest { }

            [RpcService(1)]
            public interface ISvcA
            {
                [RpcMethod(1)]
                ValueTask Do(DoRequest request);
            }

            [RpcService(1)]
            public interface ISvcB
            {
                [RpcMethod(1)]
                ValueTask Do(DoRequest request);
            }
            """);

        var ex = Assert.Throws<InvalidOperationException>(
            () => ContractParser.FindRpcServicesFromSource(dir));
        Assert.Contains("Duplicate ServiceId 1", ex.Message);
        Assert.Contains("ISvcA", ex.Message);
        Assert.Contains("ISvcB", ex.Message);
    }

    [Fact]
    public void DuplicateMethodId_ThrowsInvalidOperation()
    {
        var dir = CreateTempContracts(AttributeDefinitions, """
            using System.Threading.Tasks;

            public class DoRequest { }

            [RpcService(1)]
            public interface ISvc
            {
                [RpcMethod(1)]
                ValueTask DoA(DoRequest request);

                [RpcMethod(1)]
                ValueTask DoB(DoRequest request);
            }
            """);

        var ex = Assert.Throws<InvalidOperationException>(
            () => ContractParser.FindRpcServicesFromSource(dir));
        Assert.Contains("Duplicate MethodId 1", ex.Message);
        Assert.Contains("DoA", ex.Message);
        Assert.Contains("DoB", ex.Message);
    }

    #endregion

    #region Namespace collection

    [Fact]
    public void CollectsNamespacesFromParameterAndReturnTypes()
    {
        var dir = CreateTempContracts(AttributeDefinitions, """
            using System.Threading.Tasks;
            using System.Collections.Generic;

            namespace MyGame.Models
            {
                public class PlayerInfo { }
            }

            namespace MyGame.Contracts
            {
                [RpcService(1)]
                public interface IPlayerSvc
                {
                    [RpcMethod(1)]
                    ValueTask<MyGame.Models.PlayerInfo> GetPlayer(MyGame.Models.PlayerInfo request);
                }
            }
            """);

        var svc = Assert.Single(ContractParser.FindRpcServicesFromSource(dir));
        Assert.Contains("MyGame.Models", svc.UsingDirectives);
        Assert.Contains("MyGame.Contracts", svc.UsingDirectives);
    }

    [Fact]
    public void PrimitiveRequestType_ThrowsInvalidOperation()
    {
        var dir = CreateTempContracts(AttributeDefinitions, """
            using System.Threading.Tasks;

            public class LoginReply { }

            [RpcService(1)]
            public interface ISvc
            {
                [RpcMethod(1)]
                ValueTask<LoginReply> LoginAsync(string account);
            }
            """);

        var ex = Assert.Throws<InvalidOperationException>(
            () => ContractParser.FindRpcServicesFromSource(dir));
        Assert.Contains("must be a DTO object type", ex.Message);
    }

    [Fact]
    public void PrimitiveResponseType_ThrowsInvalidOperation()
    {
        var dir = CreateTempContracts(AttributeDefinitions, """
            using System.Threading.Tasks;

            public class LoginRequest { }

            [RpcService(1)]
            public interface ISvc
            {
                [RpcMethod(1)]
                ValueTask<int> LoginAsync(LoginRequest request);
            }
            """);

        var ex = Assert.Throws<InvalidOperationException>(
            () => ContractParser.FindRpcServicesFromSource(dir));
        Assert.Contains("response type must be a DTO object type", ex.Message);
    }

    [Fact]
    public void PrimitiveCallbackType_ThrowsInvalidOperation()
    {
        var dir = CreateTempContracts(AttributeDefinitions, """
            using System;
            using System.Threading.Tasks;

            public class DoRequest { }

            [RpcCallback(typeof(ISvc))]
            public interface INotify
            {
                [RpcPush(1)]
                void OnNotify(string message);
            }

            [RpcService(1, Callback = typeof(INotify))]
            public interface ISvc
            {
                [RpcMethod(1)]
                ValueTask Do(DoRequest request);
            }
            """);

        var ex = Assert.Throws<InvalidOperationException>(
            () => ContractParser.FindRpcServicesFromSource(dir));
        Assert.Contains("must be a DTO object type", ex.Message);
    }

    [Fact]
    public void CollectionCallbackType_ExplainsDtoWrapperRequirement()
    {
        var dir = CreateTempContracts(AttributeDefinitions, """
            using System.Collections.Generic;
            using System.Threading.Tasks;

            public class PlayerPosition { }
            public class DoRequest { }

            [RpcCallback(typeof(ISvc))]
            public interface INotify
            {
                [RpcPush(1)]
                void OnMove(List<PlayerPosition> playerPositions);
            }

            [RpcService(1, Callback = typeof(INotify))]
            public interface ISvc
            {
                [RpcMethod(1)]
                ValueTask Do(DoRequest request);
            }
            """);

        var ex = Assert.Throws<InvalidOperationException>(
            () => ContractParser.FindRpcServicesFromSource(dir));
        Assert.Contains("Collection-like payload roots are not allowed", ex.Message);
        Assert.Contains("wrap the collection in a DTO", ex.Message);
    }

    #endregion
}
