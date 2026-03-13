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

            [RpcService(1)]
            public interface IPlayerService
            {
                [RpcMethod(1)]
                ValueTask<string> GetName(int playerId);

                [RpcMethod(2)]
                ValueTask SetName(string name);
            }
            """);

        var services = ContractParser.FindRpcServicesFromSource(dir);

        Assert.Single(services);
        var svc = services[0];
        Assert.Equal("IPlayerService", svc.InterfaceName);
        Assert.Equal(1, svc.ServiceId);
        Assert.Equal(2, svc.Methods.Count);

        var getName = svc.Methods[0];
        Assert.Equal("GetName", getName.Name);
        Assert.Equal(1, getName.MethodId);
        Assert.False(getName.IsVoid);
        Assert.Equal("string", getName.RetTypeName);
        Assert.Single(getName.Parameters);
        Assert.Equal("int", getName.Parameters[0].TypeName);
        Assert.Equal("playerId", getName.Parameters[0].Name);

        var setName = svc.Methods[1];
        Assert.Equal("SetName", setName.Name);
        Assert.True(setName.IsVoid);
        Assert.Null(setName.RetTypeName);
    }

    [Fact]
    public void MultipleParameters_ParsedCorrectly()
    {
        var dir = CreateTempContracts(AttributeDefinitions, """
            using System.Threading.Tasks;

            [RpcService(10)]
            public interface IMultiSvc
            {
                [RpcMethod(5)]
                ValueTask<bool> Check(int a, string b, float c);
            }
            """);

        var services = ContractParser.FindRpcServicesFromSource(dir);
        var method = Assert.Single(services).Methods[0];

        Assert.Equal(3, method.Parameters.Count);
        Assert.Equal("int", method.Parameters[0].TypeName);
        Assert.Equal("string", method.Parameters[1].TypeName);
        Assert.Equal("float", method.Parameters[2].TypeName);
    }

    [Fact]
    public void ZeroParameterMethod_ParsedCorrectly()
    {
        var dir = CreateTempContracts(AttributeDefinitions, """
            using System.Threading.Tasks;

            [RpcService(1)]
            public interface ISvc
            {
                [RpcMethod(1)]
                ValueTask Ping();
            }
            """);

        var services = ContractParser.FindRpcServicesFromSource(dir);
        var method = Assert.Single(services).Methods[0];

        Assert.Empty(method.Parameters);
        Assert.True(method.IsVoid);
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

            [RpcService(1)]
            public interface ISvc
            {
                [RpcMethod(1)]
                ValueTask Tagged();

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

            [RpcService(1)]
            public interface IServiceA
            {
                [RpcMethod(1)]
                ValueTask DoA();
            }

            [RpcService(2)]
            public interface IServiceB
            {
                [RpcMethod(1)]
                ValueTask DoB();
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
            [RpcService(1)]
            public interface IFileOneService
            {
                [RpcMethod(1)]
                ValueTask Do1();
            }
            """,
            """
            using System.Threading.Tasks;
            [RpcService(2)]
            public interface IFileTwoService
            {
                [RpcMethod(1)]
                ValueTask Do2();
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

            [RpcCallback(typeof(IGameService))]
            public interface IGameCallback
            {
                [RpcPush(1)]
                void OnPlayerJoined(int playerId);

                [RpcPush(2)]
                void OnMessage(string msg);
            }

            [RpcService(5, Callback = typeof(IGameCallback))]
            public interface IGameService
            {
                [RpcMethod(1)]
                ValueTask Start();
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
        Assert.Equal("OnMessage", svc.CallbackMethods[1].Name);
    }

    [Fact]
    public void CallbackInterface_ZeroParamMethod()
    {
        var dir = CreateTempContracts(AttributeDefinitions, """
            using System;
            using System.Threading.Tasks;

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
                ValueTask Do();
            }
            """);

        var services = ContractParser.FindRpcServicesFromSource(dir);
        var svc = Assert.Single(services);
        Assert.True(svc.HasCallback);
        Assert.Single(svc.CallbackMethods);
        Assert.Empty(svc.CallbackMethods[0].Parameters);
    }

    [Fact]
    public void CallbackInterface_InDifferentFile()
    {
        var dir = CreateTempContracts(
            AttributeDefinitions,
            """
            using System;
            using System.Threading.Tasks;

            [RpcCallback(typeof(IMySvc))]
            public interface IEvents
            {
                [RpcPush(1)]
                void OnEvent(int code);
            }
            """,
            """
            using System.Threading.Tasks;

            [RpcService(3, Callback = typeof(IEvents))]
            public interface IMySvc
            {
                [RpcMethod(1)]
                ValueTask Act();
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
            [RpcService(1)]
            public interface IPlainSvc
            {
                [RpcMethod(1)]
                ValueTask Do();
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

            [RpcService(1)]
            public interface IBadSvc
            {
                [RpcMethod(1)]
                Task<int> NotValueTask();
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
            [RpcService(1)]
            public interface IBadSvc
            {
                [RpcMethod(1)]
                void BadMethod();
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

            [RpcService(1)]
            public interface ISvcA
            {
                [RpcMethod(1)]
                ValueTask Do();
            }

            [RpcService(1)]
            public interface ISvcB
            {
                [RpcMethod(1)]
                ValueTask Do();
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

            [RpcService(1)]
            public interface ISvc
            {
                [RpcMethod(1)]
                ValueTask DoA();

                [RpcMethod(1)]
                ValueTask DoB();
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
                    ValueTask<MyGame.Models.PlayerInfo> GetPlayer(int id);
                }
            }
            """);

        var svc = Assert.Single(ContractParser.FindRpcServicesFromSource(dir));
        Assert.Contains("MyGame.Models", svc.UsingDirectives);
        Assert.Contains("MyGame.Contracts", svc.UsingDirectives);
    }

    #endregion
}
