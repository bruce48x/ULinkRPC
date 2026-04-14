using System;

namespace ULinkRPC.Core
{
    /// <summary>
    ///     Serializer for RPC method payloads (arguments and return values).
    ///     Envelope encoding is handled by <see cref="RpcEnvelopeCodec"/>.
    /// </summary>
    public interface IRpcSerializer
    {
        TransportFrame SerializeFrame<T>(T value);
        T Deserialize<T>(ReadOnlySpan<byte> data);
        T Deserialize<T>(ReadOnlyMemory<byte> data);
    }
}
