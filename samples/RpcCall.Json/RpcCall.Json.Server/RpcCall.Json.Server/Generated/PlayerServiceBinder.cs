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

        public static void Bind(RpcServer server, Func<LoginRequest, ValueTask<LoginReply>> loginAsyncHandler, Func<ValueTask> pingAsyncHandler)
        {
            Bind(server, new DelegateImpl(loginAsyncHandler, pingAsyncHandler));
        }

        private sealed class DelegateImpl : IPlayerService
        {
            private readonly Func<LoginRequest, ValueTask<LoginReply>> _loginAsyncHandler;
            private readonly Func<ValueTask> _pingAsyncHandler;

            public DelegateImpl(Func<LoginRequest, ValueTask<LoginReply>> loginAsyncHandler, Func<ValueTask> pingAsyncHandler)
            {
                _loginAsyncHandler = loginAsyncHandler ?? throw new ArgumentNullException(nameof(loginAsyncHandler));
                _pingAsyncHandler = pingAsyncHandler ?? throw new ArgumentNullException(nameof(pingAsyncHandler));
            }

            public ValueTask<LoginReply> LoginAsync(LoginRequest req)
            {
                return _loginAsyncHandler(req);
            }

            public ValueTask PingAsync()
            {
                return _pingAsyncHandler();
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

        }
    }
}
