namespace ULinkRPC.Core
{
    public enum RpcFrameType : byte
    {
        Request = 1,
        Response = 2,
        Push = 3
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
        public byte[] Payload { get; set; } = Array.Empty<byte>();
    }

    public sealed class RpcResponseEnvelope
    {
        public uint RequestId { get; set; }
        public RpcStatus Status { get; set; }
        public byte[] Payload { get; set; } = Array.Empty<byte>();
        public string? ErrorMessage { get; set; }
    }

    public sealed class RpcPushEnvelope
    {
        public int ServiceId { get; set; }
        public int MethodId { get; set; }
        public byte[] Payload { get; set; } = Array.Empty<byte>();
    }

    public sealed class RpcVoid
    {
        public static readonly RpcVoid Instance = new();
    }
}
