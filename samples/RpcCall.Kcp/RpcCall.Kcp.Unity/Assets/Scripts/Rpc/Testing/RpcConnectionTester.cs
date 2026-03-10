using ULinkRPC.Client;
using UnityEngine;

namespace Rpc.Testing
{
    public sealed class RpcConnectionTester : RpcConnectionTesterBase
    {
        [SerializeField] private RpcEndpointSettings _endpoint = RpcEndpointSettings.CreateKcp("127.0.0.1", 20000);

        protected override RpcEndpointSettings Endpoint => _endpoint;
        protected override string RuntimeTitle => "RpcCall.Kcp Runtime";
        protected override RpcTransportKind TransportKind => RpcTransportKind.Kcp;

        protected override RpcClientBuilder CreateClientBuilder()
        {
            return RpcClientBuilder.Create()
                .UseMemoryPack()
                .UseKcp(_endpoint.Host, _endpoint.Port);
        }
    }
}
