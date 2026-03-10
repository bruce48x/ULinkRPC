using ULinkRPC.Client;
using UnityEngine;

namespace Rpc.Testing
{
    public sealed class RpcConnectionTester : RpcConnectionTesterBase
    {
        [SerializeField] private RpcEndpointSettings _endpoint = RpcEndpointSettings.CreateWebSocket("127.0.0.1", 20000, "/ws");

        protected override RpcEndpointSettings Endpoint => _endpoint;
        protected override string RuntimeTitle => "RpcCall.Json Runtime";
        protected override RpcTransportKind TransportKind => RpcTransportKind.WebSocket;

        protected override RpcClientBuilder CreateClientBuilder()
        {
            return RpcClientBuilder.Create()
                .UseJson()
                .UseWebSocket(_endpoint.GetWebSocketUrl());
        }
    }
}
