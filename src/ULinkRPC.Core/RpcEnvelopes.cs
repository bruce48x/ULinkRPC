namespace ULinkRPC.Core
{
    public enum RpcFrameType : byte
    {
        Request = 1,
        Response = 2,
        Push = 3,
        KeepAlivePing = 4,
        KeepAlivePong = 5
    }

    public enum RpcStatus : byte
    {
        Ok = 0,
        NotFound = 1,
        Exception = 2
    }

    public sealed class RpcRequestEnvelope
    {
        public uint RequestId { get; set; }
        public int ServiceId { get; set; }
        public int MethodId { get; set; }
        public ReadOnlyMemory<byte> Payload { get; set; } = ReadOnlyMemory<byte>.Empty;
    }

    public sealed class RpcRequestFrame : IDisposable
    {
        public RpcRequestFrame(uint requestId, int serviceId, int methodId, TransportFrame payload)
        {
            RequestId = requestId;
            ServiceId = serviceId;
            MethodId = methodId;
            Payload = payload;
        }

        public uint RequestId { get; }
        public int ServiceId { get; }
        public int MethodId { get; }
        public TransportFrame Payload { get; }

        public void Dispose() => Payload.Dispose();
    }

    public sealed class RpcResponseEnvelope
    {
        public uint RequestId { get; set; }
        public RpcStatus Status { get; set; }
        public ReadOnlyMemory<byte> Payload { get; set; } = ReadOnlyMemory<byte>.Empty;
        public string? ErrorMessage { get; set; }
    }

    public sealed class RpcResponseFrame : IDisposable
    {
        public RpcResponseFrame(uint requestId, RpcStatus status, TransportFrame payload, string? errorMessage)
        {
            RequestId = requestId;
            Status = status;
            Payload = payload;
            ErrorMessage = errorMessage;
        }

        public uint RequestId { get; }
        public RpcStatus Status { get; }
        public TransportFrame Payload { get; }
        public string? ErrorMessage { get; }

        public void Dispose() => Payload.Dispose();
    }

    public sealed class RpcPushEnvelope
    {
        public int ServiceId { get; set; }
        public int MethodId { get; set; }
        public ReadOnlyMemory<byte> Payload { get; set; } = ReadOnlyMemory<byte>.Empty;
    }

    public sealed class RpcPushFrame : IDisposable
    {
        public RpcPushFrame(int serviceId, int methodId, TransportFrame payload)
        {
            ServiceId = serviceId;
            MethodId = methodId;
            Payload = payload;
        }

        public int ServiceId { get; }
        public int MethodId { get; }
        public TransportFrame Payload { get; }

        public void Dispose() => Payload.Dispose();
    }

    public sealed class RpcKeepAlivePingEnvelope
    {
        public long TimestampTicksUtc { get; set; }
    }

    public sealed class RpcKeepAlivePongEnvelope
    {
        public long TimestampTicksUtc { get; set; }
    }

    public sealed class RpcVoid
    {
        public static readonly RpcVoid Instance = new();
    }
}
