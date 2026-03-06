using ULinkRPC.CodeGen;
using Xunit;

namespace ULinkRPC.CodeGen.Tests;

public class ContractParserTests
{
    [Fact]
    public void FindRpcServicesFromSource_ParsesSimpleService()
    {
        var tempDir = CreateTempContracts("""
            using System;
            using System.Threading.Tasks;

            [AttributeUsage(AttributeTargets.Interface)]
            public class RpcServiceAttribute : Attribute
            {
                public RpcServiceAttribute(int id) { }
            }

            [AttributeUsage(AttributeTargets.Method)]
            public class RpcMethodAttribute : Attribute
            {
                public RpcMethodAttribute(int id) { }
            }

            [RpcService(1)]
            public interface IPlayerService
            {
                [RpcMethod(1)]
                ValueTask<string> GetName(int playerId);

                [RpcMethod(2)]
                ValueTask SetName(string name);
            }
            """);

        try
        {
            var services = ContractParser.FindRpcServicesFromSource(tempDir);

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
            Assert.Equal(2, setName.MethodId);
            Assert.True(setName.IsVoid);
            Assert.Null(setName.RetTypeName);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void FindRpcServicesFromSource_IgnoresInterfacesWithoutAttribute()
    {
        var tempDir = CreateTempContracts("""
            using System.Threading.Tasks;

            public interface INotAnRpcService
            {
                ValueTask DoStuff();
            }
            """);

        try
        {
            var services = ContractParser.FindRpcServicesFromSource(tempDir);
            Assert.Empty(services);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void FindRpcServicesFromSource_SkipsMethodsWithoutAttribute()
    {
        var tempDir = CreateTempContracts("""
            using System;
            using System.Threading.Tasks;

            [AttributeUsage(AttributeTargets.Interface)]
            public class RpcServiceAttribute : Attribute
            {
                public RpcServiceAttribute(int id) { }
            }

            [AttributeUsage(AttributeTargets.Method)]
            public class RpcMethodAttribute : Attribute
            {
                public RpcMethodAttribute(int id) { }
            }

            [RpcService(1)]
            public interface ISvc
            {
                [RpcMethod(1)]
                ValueTask Tagged();

                ValueTask Untagged();
            }
            """);

        try
        {
            var services = ContractParser.FindRpcServicesFromSource(tempDir);
            Assert.Single(services);
            Assert.Single(services[0].Methods);
            Assert.Equal("Tagged", services[0].Methods[0].Name);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void FindRpcServicesFromSource_MultipleParameters()
    {
        var tempDir = CreateTempContracts("""
            using System;
            using System.Threading.Tasks;

            [AttributeUsage(AttributeTargets.Interface)]
            public class RpcServiceAttribute : Attribute
            {
                public RpcServiceAttribute(int id) { }
            }

            [AttributeUsage(AttributeTargets.Method)]
            public class RpcMethodAttribute : Attribute
            {
                public RpcMethodAttribute(int id) { }
            }

            [RpcService(10)]
            public interface IMultiSvc
            {
                [RpcMethod(5)]
                ValueTask<bool> Check(int a, string b, float c);
            }
            """);

        try
        {
            var services = ContractParser.FindRpcServicesFromSource(tempDir);
            Assert.Single(services);
            var method = services[0].Methods[0];
            Assert.Equal(3, method.Parameters.Count);
            Assert.Equal("a", method.Parameters[0].Name);
            Assert.Equal("int", method.Parameters[0].TypeName);
            Assert.Equal("b", method.Parameters[1].Name);
            Assert.Equal("string", method.Parameters[1].TypeName);
            Assert.Equal("c", method.Parameters[2].Name);
            Assert.Equal("float", method.Parameters[2].TypeName);
            Assert.False(method.IsVoid);
            Assert.Equal("bool", method.RetTypeName);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    private static string CreateTempContracts(string code)
    {
        var dir = Path.Combine(Path.GetTempPath(), $"ulinkrpc_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "Contracts.cs"), code);
        return dir;
    }
}
