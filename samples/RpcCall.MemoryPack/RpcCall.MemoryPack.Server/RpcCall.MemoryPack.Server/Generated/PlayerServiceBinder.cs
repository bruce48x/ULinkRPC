using System;
using System.Threading.Tasks;
using Game.Rpc.Contracts;
using ULinkRPC.Core;
using ULinkRPC.Server;

namespace Game.Rpc.Server.Generated
{
    public static class PlayerServiceBinder
    {
        private const int ServiceId = 1;

        public static void Bind(RpcServer server, Func<LoginRequest, ValueTask<LoginReply>> loginAsyncHandler, Func<ValueTask> pingAsyncHandler, Func<string, int, bool, ValueTask<string>> composeGreetingAsyncHandler)
        {
            Bind(server, new DelegateImpl(loginAsyncHandler, pingAsyncHandler, composeGreetingAsyncHandler));
        }

        private sealed class DelegateImpl : IPlayerService
        {
            private readonly Func<LoginRequest, ValueTask<LoginReply>> _loginAsyncHandler;
            private readonly Func<ValueTask> _pingAsyncHandler;
            private readonly Func<string, int, bool, ValueTask<string>> _composeGreetingAsyncHandler;

            public DelegateImpl(Func<LoginRequest, ValueTask<LoginReply>> loginAsyncHandler, Func<ValueTask> pingAsyncHandler, Func<string, int, bool, ValueTask<string>> composeGreetingAsyncHandler)
            {
                _loginAsyncHandler = loginAsyncHandler ?? throw new ArgumentNullException(nameof(loginAsyncHandler));
                _pingAsyncHandler = pingAsyncHandler ?? throw new ArgumentNullException(nameof(pingAsyncHandler));
                _composeGreetingAsyncHandler = composeGreetingAsyncHandler ?? throw new ArgumentNullException(nameof(composeGreetingAsyncHandler));
            }

            public ValueTask<LoginReply> LoginAsync(LoginRequest req)
            {
                return _loginAsyncHandler(req);
            }

            public ValueTask PingAsync()
            {
                return _pingAsyncHandler();
            }

            public ValueTask<string> ComposeGreetingAsync(string name, int level, bool vip)
            {
                return _composeGreetingAsyncHandler(name, level, vip);
            }

        }

        public static void Bind(RpcServer server, Func<IPlayerCallback, IPlayerService> implFactory)
        {
            if (implFactory is null) throw new ArgumentNullException(nameof(implFactory));
            var callback = new PlayerCallbackProxy(server);
            var impl = implFactory(callback) ?? throw new InvalidOperationException("Service implementation factory returned null.");
            Bind(server, impl);
        }

        public static void Bind(RpcServer server, IPlayerService impl)
        {
            server.Register(ServiceId, 1, async (req, ct) =>
            {
                var arg1 = server.Serializer.Deserialize<LoginRequest>(req.Payload.AsSpan())!;
                var resp = await impl.LoginAsync(arg1);
                return new RpcResponseEnvelope { RequestId = req.RequestId, Status = RpcStatus.Ok, Payload = server.Serializer.Serialize(resp) };
            });

            server.Register(ServiceId, 2, async (req, ct) =>
            {
                await impl.PingAsync();
                return new RpcResponseEnvelope { RequestId = req.RequestId, Status = RpcStatus.Ok, Payload = Array.Empty<byte>() };
            });

            server.Register(ServiceId, 3, async (req, ct) =>
            {
                var (arg1, arg2, arg3) = server.Serializer.Deserialize<(string, int, bool)>(req.Payload.AsSpan())!;
                var resp = await impl.ComposeGreetingAsync(arg1, arg2, arg3);
                return new RpcResponseEnvelope { RequestId = req.RequestId, Status = RpcStatus.Ok, Payload = server.Serializer.Serialize(resp) };
            });

        }
    }
}
