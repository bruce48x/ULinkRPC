using System;
using MemoryPack;
using ULinkRPC.Core;

namespace ULinkRPC.Serializer.MemoryPack
{
    public sealed class MemoryPackRpcSerializer : IRpcSerializer
    {
        public TransportFrame SerializeFrame<T>(T value)
        {
            using var buffer = new PooledFrameBufferWriter();
            MemoryPackSerializer.Serialize(buffer, value);
            return buffer.DetachFrame();
        }

        public T Deserialize<T>(ReadOnlySpan<byte> data)
        {
            return MemoryPackSerializer.Deserialize<T>(data)!;
        }

        public T Deserialize<T>(ReadOnlyMemory<byte> data)
        {
            return MemoryPackSerializer.Deserialize<T>(data.Span)!;
        }
    }
}
