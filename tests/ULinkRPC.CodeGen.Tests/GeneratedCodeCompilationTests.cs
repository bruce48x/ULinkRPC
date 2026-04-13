using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Reflection;
using System.Runtime.Loader;
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
                TransportFrame SerializeFrame<T>(T value);
                byte[] Serialize<T>(T value);
                T Deserialize<T>(ReadOnlySpan<byte> data);
                T Deserialize<T>(ReadOnlyMemory<byte> data);
            }

            public sealed class TransportFrame { }

            public struct RpcVoid { }

            public enum RpcStatus { Ok = 0, Error = 1 }
        }
        """;

    private const string ClientRuntimeStubs = """
        using System;
        using System.Collections.Generic;
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

            public sealed class RecordingRpcClient : IRpcClient
            {
                public readonly List<string> Calls = new();
                public readonly List<object?> Args = new();
                public readonly List<CancellationToken> CancellationTokens = new();

                public ValueTask<TResult> CallAsync<TArg, TResult>(RpcMethod<TArg, TResult> method, TArg arg, CancellationToken ct)
                {
                    Calls.Add(typeof(TResult).FullName ?? typeof(TResult).Name);
                    Args.Add(arg);
                    CancellationTokens.Add(ct);
                    return ValueTask.FromResult(default(TResult)!);
                }

                public void RegisterPushHandler<TArg>(RpcPushMethod<TArg> method, Action<TArg> handler) { }
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
                T? Deserialize<T>(ReadOnlyMemory<byte> data);
                TransportFrame SerializeFrame<T>(T value);
                byte[] Serialize<T>(T value);
            }

            public sealed class TransportFrame { }

            public class RpcRequestEnvelope
            {
                public int RequestId { get; set; }
                public ReadOnlyMemory<byte> Payload { get; set; } = ReadOnlyMemory<byte>.Empty;
            }

            public class RpcResponseEnvelope
            {
                public int RequestId { get; set; }
                public RpcStatus Status { get; set; }
                public ReadOnlyMemory<byte> Payload { get; set; } = ReadOnlyMemory<byte>.Empty;
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

    private static Assembly CompileAssemblyWithStubs(string[] generatedCodeFiles, string contractCode, bool includeServer = false)
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
            $"GeneratedCodeBehavior_{Guid.NewGuid():N}",
            trees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var peStream = new MemoryStream();
        var emitResult = compilation.Emit(peStream);
        if (!emitResult.Success)
        {
            var messages = string.Join("\n",
                emitResult.Diagnostics
                    .Where(d => d.Severity == DiagnosticSeverity.Error)
                    .Select(d => $"  {d.Id}: {d.GetMessage()} at {d.Location.GetLineSpan()}"));
            Assert.Fail($"Generated code failed behavior-test compilation:\n{messages}");
        }

        peStream.Position = 0;
        return AssemblyLoadContext.Default.LoadFromStream(peStream);
    }

    #endregion

    #region Simple service

    private static readonly string SimpleContracts = """
        using System.Threading.Tasks;

        namespace MyGame.Contracts
        {
            public class PingRequest { }
            public class SetNameRequest { public string Name { get; set; } = ""; }
            public class MoveRequest { public float X { get; set; } public float Y { get; set; } public float Z { get; set; } }
            public class GetNameRequest { public int PlayerId { get; set; } }
            public class GetNameReply { public string Name { get; set; } = ""; }
            public class CheckRequest { public int A { get; set; } public string B { get; set; } = ""; }
            public class CheckReply { public bool Ok { get; set; } }

            public interface IPlayerService
            {
                ValueTask Ping(PingRequest request);
                ValueTask SetName(SetNameRequest request);
                ValueTask Move(MoveRequest request);
                ValueTask<GetNameReply> GetName(GetNameRequest request);
                ValueTask<CheckReply> Check(CheckRequest request);
            }
        }
        """;

    private static RpcServiceInfo SimpleService()
    {
        return new RpcServiceInfo("IPlayerService", "MyGame.Contracts.IPlayerService", 1,
            [
                new RpcMethodInfo("Ping", 1, [new RpcParameterInfo("PingRequest", "request")], null, true),
                new RpcMethodInfo("SetName", 2, [new RpcParameterInfo("SetNameRequest", "request")], null, true),
                new RpcMethodInfo("Move", 3, [new RpcParameterInfo("MoveRequest", "request")], null, true),
                new RpcMethodInfo("GetName", 4, [new RpcParameterInfo("GetNameRequest", "request")], "GetNameReply", false),
                new RpcMethodInfo("Check", 5, [new RpcParameterInfo("CheckRequest", "request")], "CheckReply", false),
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
    public async Task ClientCode_ParameterlessOverload_ForwardsToCallAsyncOnce()
    {
        var code = ClientEmitter.GenerateClient(SimpleService(), "MyGame.Generated", "ULinkRPC.Core");
        var assembly = CompileAssemblyWithStubs([code], SimpleContracts);

        var rpcClientType = assembly.GetType("ULinkRPC.Client.RecordingRpcClient", throwOnError: true)!;
        var clientType = assembly.GetType("MyGame.Generated.PlayerServiceClient", throwOnError: true)!;
        var requestType = assembly.GetType("MyGame.Contracts.PingRequest", throwOnError: true)!;

        var rpcClient = Activator.CreateInstance(rpcClientType)!;
        var client = Activator.CreateInstance(clientType, rpcClient)!;
        var request = Activator.CreateInstance(requestType)!;

        var pingMethod = clientType.GetMethod("Ping", [requestType])!;
        var result = (ValueTask)pingMethod.Invoke(client, [request])!;
        await result;

        var calls = (System.Collections.IList)rpcClientType.GetField("Calls")!.GetValue(rpcClient)!;
        var args = (System.Collections.IList)rpcClientType.GetField("Args")!.GetValue(rpcClient)!;
        var cancellationTokens = (System.Collections.IList)rpcClientType.GetField("CancellationTokens")!.GetValue(rpcClient)!;

        Assert.Single(calls);
        Assert.Same(request, args[0]);
        Assert.Equal(CancellationToken.None, (CancellationToken)cancellationTokens[0]!);
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
                void OnPlayerJoined(PlayerJoinedNotify notify);
                void OnMessage(MessageNotify notify);
                void OnPing(PingNotify notify);
            }

            public interface IGameService
            {
                ValueTask Start(StartRequest request);
                ValueTask<GetPlayerCountReply> GetPlayerCount(GetPlayerCountRequest request);
                ValueTask Send(SendRequest request);
            }

            public class StartRequest { }
            public class GetPlayerCountRequest { }
            public class GetPlayerCountReply { public int Count { get; set; } }
            public class SendRequest { public string Message { get; set; } = ""; }
            public class PlayerJoinedNotify { public int PlayerId { get; set; } }
            public class MessageNotify { public string Sender { get; set; } = ""; public string Text { get; set; } = ""; }
            public class PingNotify { public string Message { get; set; } = ""; }
        }
        """;

    private static RpcServiceInfo CallbackService()
    {
        var svc = new RpcServiceInfo("IGameService", "MyGame.Contracts.IGameService", 5,
            [
                new RpcMethodInfo("Start", 1, [new RpcParameterInfo("StartRequest", "request")], null, true),
                new RpcMethodInfo("GetPlayerCount", 2, [new RpcParameterInfo("GetPlayerCountRequest", "request")], "GetPlayerCountReply", false),
                new RpcMethodInfo("Send", 3, [new RpcParameterInfo("SendRequest", "request")], null, true),
            ],
            ["MyGame.Contracts"])
        {
            CallbackInterfaceName = "IGameCallback",
            CallbackInterfaceFullName = "MyGame.Contracts.IGameCallback"
        };
        svc.CallbackMethods =
        [
            new RpcCallbackMethodInfo("OnPlayerJoined", 1, [new RpcParameterInfo("PlayerJoinedNotify", "notify")]),
            new RpcCallbackMethodInfo("OnMessage", 2, [new RpcParameterInfo("MessageNotify", "notify")]),
            new RpcCallbackMethodInfo("OnPing", 3, [new RpcParameterInfo("PingNotify", "notify")]),
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
            [new RpcMethodInfo("Do", 1, [new RpcParameterInfo("Game.DoRequest", "request")], null, true)], ["Game"]);
        var svc2 = new RpcServiceInfo("IAuthService", "Auth.IAuthService", 2,
            [new RpcMethodInfo("Login", 1, [new RpcParameterInfo("Auth.LoginRequest", "request")], "Auth.LoginReply", false)], ["Auth"]);

        var contracts = """
            using System.Threading.Tasks;
            namespace Game { public class DoRequest { } public interface IPlayerService { ValueTask Do(DoRequest request); } }
            namespace Auth { public class LoginRequest { public string Token { get; set; } = ""; } public class LoginReply { public bool Ok { get; set; } } public interface IAuthService { ValueTask<LoginReply> Login(LoginRequest request); } }
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
                public class EchoRequest { public string Input { get; set; } = ""; }
                public class EchoReply { public string Output { get; set; } = ""; }
                public class FireRequest { public int X { get; set; } public int Y { get; set; } }
                public class NoopRequest { }

                [RpcService(42)]
                public interface IDemoService
                {
                    [RpcMethod(1)]
                    ValueTask<EchoReply> Echo(EchoRequest request);

                    [RpcMethod(2)]
                    ValueTask Fire(FireRequest request);

                    [RpcMethod(3)]
                    ValueTask Noop(NoopRequest request);
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
                    public class EchoRequest { public string Input { get; set; } = ""; }
                    public class EchoReply { public string Output { get; set; } = ""; }
                    public class FireRequest { public int X { get; set; } public int Y { get; set; } }
                    public class NoopRequest { }
                    public interface IDemoService
                    {
                        ValueTask<EchoReply> Echo(EchoRequest request);
                        ValueTask Fire(FireRequest request);
                        ValueTask Noop(NoopRequest request);
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
                public class AddRequest { public int A { get; set; } public int B { get; set; } }
                public class AddReply { public int Sum { get; set; } }
                public class ResetRequest { }

                [RpcService(10)]
                public interface ICalcService
                {
                    [RpcMethod(1)]
                    ValueTask<AddReply> Add(AddRequest request);

                    [RpcMethod(2)]
                    ValueTask Reset(ResetRequest request);
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
                    public class AddRequest { public int A { get; set; } public int B { get; set; } }
                    public class AddReply { public int Sum { get; set; } }
                    public class ResetRequest { }
                    public interface ICalcService
                    {
                        ValueTask<AddReply> Add(AddRequest request);
                        ValueTask Reset(ResetRequest request);
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
