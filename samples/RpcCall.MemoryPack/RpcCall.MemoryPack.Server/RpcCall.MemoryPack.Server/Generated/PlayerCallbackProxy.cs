using System;
using Game.Rpc.Contracts;
using ULinkRPC.Core;
using ULinkRPC.Server;

namespace Game.Rpc.Server.Generated
{
    public sealed class PlayerCallbackProxy : IPlayerCallback
    {
        private const int ServiceId = 1;
        private readonly RpcServer _server;

        public PlayerCallbackProxy(RpcServer server) { _server = server; }

        public void OnNotify(string message)
        {
            _server.PushAsync<string>(ServiceId, 1, message).AsTask().Wait();
        }

    }
}
