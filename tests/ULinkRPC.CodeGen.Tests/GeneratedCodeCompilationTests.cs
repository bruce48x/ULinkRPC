using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using ULinkRPC.CodeGen;
using Xunit;

namespace ULinkRPC.CodeGen.Tests;

/// <summary>
/// The ultimate code generator test: verify that all generated code
/// compiles successfully against stub runtime type definitions.
/// This catches any structural, syntactic, or type-resolution bugs
/// that would surface at consumer compile time.
/// </summary>
public class GeneratedCodeCompilationTests
{
    #region Runtime stubs

    private const string CoreRuntimeStubs = """
        using System;
        using System.Threading;
        using System.Threading.Tasks;

        namespace ULinkRPC.Core
        {
            public interface IRpcClient
            {
                ValueTask<TResult> CallAsync<TArg, TResult>(RpcMethod<TArg, TResult> method, TArg arg, CancellationToken ct);
                void RegisterPushHandler<TArg>(RpcPushMethod<TArg> method, Action<TArg> handler);
            }

            public readonly struct RpcMethod<TArg, TResult>
            {
                public RpcMethod(int serviceId, int methodId) { }
            }

            public readonly struct RpcPushMethod<TArg>
            {
                public RpcPushMethod(int serviceId, int methodId) { }
            }

            public interface ITransport { }

            public sealed class RpcKeepAliveOptions
            {
            }

            public interface IRpcSerializer
            {
                byte[] Serialize<T>(T value);
                T Deserialize<T>(ReadOnlySpan<byte> data);
                T Deserialize<T>(ReadOnlyMemory<byte> data);
            }

            public struct RpcVoid { }

            public enum RpcStatus { Ok = 0, Error = 1 }
        }
        """;

    private const string ClientRuntimeStubs = """
        using System;
        using System.Threading;
        using System.Threading.Tasks;
        using ULinkRPC.Core;

        namespace ULinkRPC.Client
        {
            public sealed class RpcClientOptions
            {
                public RpcClientOptions(ITransport transport, IRpcSerializer serializer)
                {
                    Transport = transport;
                    Serializer = serializer;
                }

                public RpcKeepAliveOptions KeepAlive { get; } = new();
                public ITransport Transport { get; }
                public IRpcSerializer Serializer { get; }
            }

            public sealed class RpcClientRuntime : IRpcClient, IAsyncDisposable
            {
                public event Action<Exception?>? Disconnected;

                public RpcClientRuntime(ITransport transport, IRpcSerializer serializer, RpcKeepAliveOptions keepAlive) { }

                public ValueTask StartAsync(CancellationToken ct = default) => default;
                public ValueTask<TResult> CallAsync<TArg, TResult>(RpcMethod<TArg, TResult> method, TArg arg, CancellationToken ct) => default;
                public void RegisterPushHandler<TArg>(RpcPushMethod<TArg> method, Action<TArg> handler) { }
                public ValueTask DisposeAsync() => default;
            }

        }
        """;

    private const string ServerRuntimeStubs = """
        using System;
        using System.Threading;
        using System.Threading.Tasks;
        using ULinkRPC.Core;

        namespace ULinkRPC.Server
        {
            [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
            public sealed class RpcGeneratedServicesBinderAttribute : Attribute
            {
                public RpcGeneratedServicesBinderAttribute(Type binderType) { }
            }

            public sealed class RpcServiceRegistry
            {
                public void Register(int serviceId, int methodId,
                    Func<RpcSession, RpcRequestEnvelope, CancellationToken, ValueTask<RpcResponseEnvelope>> handler) { }
            }

            public class RpcSession
            {
                public IRpcSerializer Serializer => null!;
                public TService GetOrAddScopedService<TService>(int serviceId, Func<RpcSession, TService> factory) where TService : class => null!;
                public ValueTask PushAsync<T>(int serviceId, int methodId, T value) => default;
            }

            public interface IRpcSerializer
            {
                T? Deserialize<T>(ReadOnlySpan<byte> data);
                byte[] Serialize<T>(T value);
            }

            public class RpcRequestEnvelope
            {
                public int RequestId { get; set; }
                public byte[] Payload { get; set; } = Array.Empty<byte>();
            }

            public class RpcResponseEnvelope
            {
                public int RequestId { get; set; }
                public RpcStatus Status { get; set; }
                public byte[] Payload { get; set; } = Array.Empty<byte>();
            }
        }
        """;

    #endregion

    #region Helpers

    private static Diagnostic[] CompileWithStubs(string[] generatedCodeFiles, string contractCode, bool includeServer = false)
    {
        var trees = new List<SyntaxTree>
        {
            CSharpSyntaxTree.ParseText(CoreRuntimeStubs, path: "CoreStubs.cs"),
            CSharpSyntaxTree.ParseText(contractCode, path: "Contracts.cs"),
            CSharpSyntaxTree.ParseText(ClientRuntimeStubs, path: "ClientStubs.cs"),
        };
        for (var i = 0; i < generatedCodeFiles.Length; i++)
            trees.Add(CSharpSyntaxTree.ParseText(generatedCodeFiles[i], path: $"Generated{i}.cs"));
        if (includeServer)
            trees.Add(CSharpSyntaxTree.ParseText(ServerRuntimeStubs, path: "ServerStubs.cs"));

        var references = new List<MetadataReference>();
        var trustedAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        if (!string.IsNullOrWhiteSpace(trustedAssemblies))
            foreach (var path in trustedAssemblies.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
                references.Add(MetadataReference.CreateFromFile(path));

        var compilation = CSharpCompilation.Create(
            "GeneratedCodeTest",
            trees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        return compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToArray();
    }

    private static void AssertCompilesCleanly(string generatedCode, string contractCode, bool includeServer = false)
        => AssertCompilesCleanly([generatedCode], contractCode, includeServer);

    private static void AssertCompilesCleanly(string[] generatedCodeFiles, string contractCode, bool includeServer = false)
    {
        var errors = CompileWithStubs(generatedCodeFiles, contractCode, includeServer);
        if (errors.Length > 0)
        {
            var allCode = string.Join("\n\n// --- next file ---\n\n", generatedCodeFiles);
            var messages = string.Join("\n",
                errors.Select(d => $"  {d.Id}: {d.GetMessage()} at {d.Location.GetLineSpan()}"));
            Assert.Fail($"Generated code has {errors.Length} compilation error(s):\n{messages}\n\n--- Generated Code ---\n{allCode}");
        }
    }

    #endregion

    #region Simple service (void + non-void, 0/1/multi params)

    private static readonly string SimpleContracts = """
        using System.Threading.Tasks;

        namespace MyGame.Contracts
        {
            public interface IPlayerService
            {
                ValueTask Ping();
                ValueTask SetName(string name);
                ValueTask Move(float x, float y, float z);
                ValueTask<string> GetName(int playerId);
                ValueTask<bool> Check(int a, string b);
            }
        }
        """;

    private static RpcServiceInfo SimpleService()
    {
        return new RpcServiceInfo("IPlayerService", "MyGame.Contracts.IPlayerService", 1,
            [
                new RpcMethodInfo("Ping", 1, [], null, true),
                new RpcMethodInfo("SetName", 2, [new RpcParameterInfo("string", "name")], null, true),
                new RpcMethodInfo("Move", 3,
                    [new RpcParameterInfo("float", "x"), new RpcParameterInfo("float", "y"), new RpcParameterInfo("float", "z")],
                    null, true),
                new RpcMethodInfo("GetName", 4, [new RpcParameterInfo("int", "playerId")], "string", false),
                new RpcMethodInfo("Check", 5,
                    [new RpcParameterInfo("int", "a"), new RpcParameterInfo("string", "b")], "bool", false),
            ],
            ["MyGame.Contracts"]);
    }

    [Fact]
    public void ClientCode_CompilesCleanly()
    {
        var code = ClientEmitter.GenerateClient(SimpleService(), "MyGame.Generated", "ULinkRPC.Core");
        AssertCompilesCleanly(code, SimpleContracts);
    }

    [Fact]
    public void ServerBinder_CompilesCleanly()
    {
        var code = ServerEmitter.GenerateBinder(SimpleService(), "MyGame.Server", "ULinkRPC.Core", "ULinkRPC.Server");
        AssertCompilesCleanly(code, SimpleContracts, includeServer: true);
    }

    [Fact]
    public void ClientFacade_CompilesCleanly()
    {
        var clientCode = ClientEmitter.GenerateClient(SimpleService(), "MyGame.Generated", "ULinkRPC.Core");
        var facadeCode = FacadeEmitter.GenerateClientFacade(
            [SimpleService()], "MyGame.Generated", "ULinkRPC.Core", "ULinkRPC.Client");
        AssertCompilesCleanly([clientCode, facadeCode], SimpleContracts);
    }

    [Fact]
    public void AllServicesBinder_CompilesCleanly()
    {
        var svc = SimpleService();
        var binderCode = ServerEmitter.GenerateBinder(svc, "MyGame.Server", "ULinkRPC.Core", "ULinkRPC.Server");
        var allBinderCode = ServerEmitter.GenerateAllServicesBinder([svc], "MyGame.Server", "ULinkRPC.Server");
        AssertCompilesCleanly([binderCode, allBinderCode], SimpleContracts, includeServer: true);
    }

    #endregion

    #region Service with callbacks

    private static readonly string CallbackContracts = """
        using System.Threading.Tasks;

        namespace MyGame.Contracts
        {
            public interface IGameCallback
            {
                void OnPlayerJoined(int playerId);
                void OnMessage(string sender, string text);
                void OnPing();
            }

            public interface IGameService
            {
                ValueTask Start();
                ValueTask<int> GetPlayerCount();
                ValueTask Send(string message);
            }
        }
        """;

    private static RpcServiceInfo CallbackService()
    {
        var svc = new RpcServiceInfo("IGameService", "MyGame.Contracts.IGameService", 5,
            [
                new RpcMethodInfo("Start", 1, [], null, true),
                new RpcMethodInfo("GetPlayerCount", 2, [], "int", false),
                new RpcMethodInfo("Send", 3, [new RpcParameterInfo("string", "message")], null, true),
            ],
            ["MyGame.Contracts"])
        {
            CallbackInterfaceName = "IGameCallback",
            CallbackInterfaceFullName = "MyGame.Contracts.IGameCallback"
        };
        svc.CallbackMethods =
        [
            new RpcCallbackMethodInfo("OnPlayerJoined", 1, [new RpcParameterInfo("int", "playerId")]),
            new RpcCallbackMethodInfo("OnMessage", 2,
                [new RpcParameterInfo("string", "sender"), new RpcParameterInfo("string", "text")]),
            new RpcCallbackMethodInfo("OnPing", 3, []),
        ];
        return svc;
    }

    [Fact]
    public void CallbackProxy_CompilesCleanly()
    {
        var code = ServerEmitter.GenerateCallbackProxy(
            CallbackService(), "MyGame.Server", "ULinkRPC.Core", "ULinkRPC.Server");
        AssertCompilesCleanly(code, CallbackContracts, includeServer: true);
    }

    [Fact]
    public void CallbackBinder_CompilesCleanly()
    {
        var code = ClientEmitter.GenerateCallbackBinder(
            CallbackService(), "MyGame.Generated", "ULinkRPC.Core");
        AssertCompilesCleanly(code, CallbackContracts);
    }

    [Fact]
    public void ServerBinder_WithCallbackFactory_CompilesCleanly()
    {
        var svc = CallbackService();
        var binderCode = ServerEmitter.GenerateBinder(svc, "MyGame.Server", "ULinkRPC.Core", "ULinkRPC.Server");
        var proxyCode = ServerEmitter.GenerateCallbackProxy(svc, "MyGame.Server", "ULinkRPC.Core", "ULinkRPC.Server");
        AssertCompilesCleanly([binderCode, proxyCode], CallbackContracts, includeServer: true);
    }

    [Fact]
    public void AllServicesBinder_WithCallbackFactory_CompilesCleanly()
    {
        var svc = CallbackService();
        var binderCode = ServerEmitter.GenerateBinder(svc, "MyGame.Server", "ULinkRPC.Core", "ULinkRPC.Server");
        var proxyCode = ServerEmitter.GenerateCallbackProxy(svc, "MyGame.Server", "ULinkRPC.Core", "ULinkRPC.Server");
        var allBinderCode = ServerEmitter.GenerateAllServicesBinder([svc], "MyGame.Server", "ULinkRPC.Server");
        AssertCompilesCleanly([binderCode, proxyCode, allBinderCode], CallbackContracts, includeServer: true);
    }

    #endregion

    #region Multiple services

    [Fact]
    public void MultipleFacadeGroups_CompilesCleanly()
    {
        var svc1 = new RpcServiceInfo("IPlayerService", "Game.IPlayerService", 1,
            [new RpcMethodInfo("Do", 1, [], null, true)], ["Game"]);
        var svc2 = new RpcServiceInfo("IAuthService", "Auth.IAuthService", 2,
            [new RpcMethodInfo("Login", 1, [new RpcParameterInfo("string", "token")], "bool", false)], ["Auth"]);

        var contracts = """
            using System.Threading.Tasks;
            namespace Game { public interface IPlayerService { ValueTask Do(); } }
            namespace Auth { public interface IAuthService { ValueTask<bool> Login(string token); } }
            """;

        var client1 = ClientEmitter.GenerateClient(svc1, "Gen", "ULinkRPC.Core");
        var client2 = ClientEmitter.GenerateClient(svc2, "Gen", "ULinkRPC.Core");
        var facade = FacadeEmitter.GenerateClientFacade([svc1, svc2], "Gen", "ULinkRPC.Core", "ULinkRPC.Client");

        AssertCompilesCleanly([client1, client2, facade], contracts);
    }

    #endregion

    #region End-to-end: source → parse → generate → compile

    [Fact]
    public void EndToEnd_ContractSource_ToCompiledClient()
    {
        var contractSource = """
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

            namespace Demo.Contracts
            {
                [RpcService(42)]
                public interface IDemoService
                {
                    [RpcMethod(1)]
                    ValueTask<string> Echo(string input);

                    [RpcMethod(2)]
                    ValueTask Fire(int x, int y);

                    [RpcMethod(3)]
                    ValueTask Noop();
                }
            }
            """;

        var tempDir = Path.Combine(Path.GetTempPath(), $"e2e_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "Contracts.cs"), contractSource);
            var services = ContractParser.FindRpcServicesFromSource(tempDir);
            Assert.Single(services);
            var svc = services[0];
            Assert.Equal(42, svc.ServiceId);
            Assert.Equal(3, svc.Methods.Count);

            var clientCode = ClientEmitter.GenerateClient(svc, "Demo.Generated", "ULinkRPC.Core");
            var facadeCode = FacadeEmitter.GenerateClientFacade([svc], "Demo.Generated", "ULinkRPC.Core", "ULinkRPC.Client");

            var runtimeContracts = """
                using System.Threading.Tasks;
                namespace Demo.Contracts
                {
                    public interface IDemoService
                    {
                        ValueTask<string> Echo(string input);
                        ValueTask Fire(int x, int y);
                        ValueTask Noop();
                    }
                }
                """;

            AssertCompilesCleanly([clientCode, facadeCode], runtimeContracts);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void EndToEnd_ContractSource_ToCompiledServerBinder()
    {
        var contractSource = """
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

            namespace Demo.Contracts
            {
                [RpcService(10)]
                public interface ICalcService
                {
                    [RpcMethod(1)]
                    ValueTask<int> Add(int a, int b);

                    [RpcMethod(2)]
                    ValueTask Reset();
                }
            }
            """;

        var tempDir = Path.Combine(Path.GetTempPath(), $"e2e_srv_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "Contracts.cs"), contractSource);
            var services = ContractParser.FindRpcServicesFromSource(tempDir);
            var svc = Assert.Single(services);

            var binderCode = ServerEmitter.GenerateBinder(svc, "Demo.Server", "ULinkRPC.Core", "ULinkRPC.Server");
            var allBinderCode = ServerEmitter.GenerateAllServicesBinder([svc], "Demo.Server", "ULinkRPC.Server");

            var runtimeContracts = """
                using System.Threading.Tasks;
                namespace Demo.Contracts
                {
                    public interface ICalcService
                    {
                        ValueTask<int> Add(int a, int b);
                        ValueTask Reset();
                    }
                }
                """;

            AssertCompilesCleanly([binderCode, allBinderCode], runtimeContracts, includeServer: true);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    #endregion
}
