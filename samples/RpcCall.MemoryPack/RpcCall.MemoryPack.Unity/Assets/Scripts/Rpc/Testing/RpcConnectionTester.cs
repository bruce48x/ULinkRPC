using ULinkRPC.Client;
using UnityEngine;

namespace Rpc.Testing
{
    public sealed class RpcConnectionTester : RpcConnectionTesterBase
    {
        [SerializeField] private RpcEndpointSettings _endpoint = RpcEndpointSettings.CreateTcp("127.0.0.1", 20000);

        protected override RpcEndpointSettings Endpoint => _endpoint;
        protected override string RuntimeTitle => "RpcCall.MemoryPack Runtime";
        protected override RpcTransportKind TransportKind => RpcTransportKind.Tcp;

        protected override RpcClientBuilder CreateClientBuilder()
        {
            return RpcClientBuilder.Create()
                .UseMemoryPack()
                .UseTcp(_endpoint.Host, _endpoint.Port);
        }
    }
}
