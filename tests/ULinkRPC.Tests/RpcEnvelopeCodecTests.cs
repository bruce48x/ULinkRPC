using System.Buffers.Binary;
using System.Text;
using ULinkRPC.Core;

namespace ULinkRPC.Tests;

public class RpcEnvelopeCodecTests
{
    [Fact]
    public void RequestRoundTrip_PreservesAllFields()
    {
        var original = new RpcRequestEnvelope
        {
            RequestId = 42,
            ServiceId = 7,
            MethodId = 3,
            Payload = new byte[] { 1, 2, 3, 4, 5 }
        };

        var encoded = RpcEnvelopeCodec.EncodeRequest(original);
        var decoded = RpcEnvelopeCodec.DecodeRequest(encoded);

        Assert.Equal(original.RequestId, decoded.RequestId);
        Assert.Equal(original.ServiceId, decoded.ServiceId);
        Assert.Equal(original.MethodId, decoded.MethodId);
        Assert.Equal(original.Payload, decoded.Payload);
    }

    [Fact]
    public void ResponseRoundTrip_PreservesAllFields()
    {
        var original = new RpcResponseEnvelope
        {
            RequestId = 99,
            Status = RpcStatus.Ok,
            Payload = new byte[] { 10, 20 },
            ErrorMessage = null
        };

        var encoded = RpcEnvelopeCodec.EncodeResponse(original);
        var decoded = RpcEnvelopeCodec.DecodeResponse(encoded);

        Assert.Equal(original.RequestId, decoded.RequestId);
        Assert.Equal(original.Status, decoded.Status);
        Assert.Equal(original.Payload, decoded.Payload);
        Assert.Null(decoded.ErrorMessage);
    }

    [Fact]
    public void ResponseRoundTrip_WithErrorMessage()
    {
        var original = new RpcResponseEnvelope
        {
            RequestId = 1,
            Status = RpcStatus.Exception,
            Payload = Array.Empty<byte>(),
            ErrorMessage = "something went wrong"
        };

        var encoded = RpcEnvelopeCodec.EncodeResponse(original);
        var decoded = RpcEnvelopeCodec.DecodeResponse(encoded);

        Assert.Equal(RpcStatus.Exception, decoded.Status);
        Assert.Equal("something went wrong", decoded.ErrorMessage);
    }

    [Fact]
    public void EncodeRequest_NullArg_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => RpcEnvelopeCodec.EncodeRequest(null!));
    }

    [Fact]
    public void EncodeResponse_NullArg_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => RpcEnvelopeCodec.EncodeResponse(null!));
    }

    [Fact]
    public void DecodeRequest_TruncatedData_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            RpcEnvelopeCodec.DecodeRequest(new byte[] { 0, 0 }));
    }

    [Fact]
    public void DecodeResponse_TruncatedData_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            RpcEnvelopeCodec.DecodeResponse(new byte[] { 0, 0 }));
    }

    [Fact]
    public void DecodeRequest_TrailingBytes_Throws()
    {
        var req = new RpcRequestEnvelope
        {
            RequestId = 1, ServiceId = 1, MethodId = 1,
            Payload = new byte[] { 42 }
        };
        var valid = RpcEnvelopeCodec.EncodeRequest(req);
        var withExtra = new byte[valid.Length + 1];
        valid.CopyTo(withExtra, 0);
        withExtra[^1] = 0xFF;

        Assert.Throws<InvalidOperationException>(() =>
            RpcEnvelopeCodec.DecodeRequest(withExtra));
    }

    [Fact]
    public void DecodeResponse_TrailingBytes_Throws()
    {
        var resp = new RpcResponseEnvelope
        {
            RequestId = 1, Status = RpcStatus.Ok,
            Payload = new byte[] { 42 }
        };
        var valid = RpcEnvelopeCodec.EncodeResponse(resp);
        var withExtra = new byte[valid.Length + 1];
        valid.CopyTo(withExtra, 0);
        withExtra[^1] = 0xFF;

        Assert.Throws<InvalidOperationException>(() =>
            RpcEnvelopeCodec.DecodeResponse(withExtra));
    }

    [Fact]
    public void DecodeRequest_NegativePayloadLength_Throws()
    {
        var data = new byte[1 + 4 + 4 + 4 + 4];
        var offset = 0;
        data[offset++] = (byte)RpcFrameType.Request;
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(offset), 1); offset += 4;
        BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(offset), 1); offset += 4;
        BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(offset), 1); offset += 4;
        BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(offset), -1);

        Assert.Throws<InvalidOperationException>(() =>
            RpcEnvelopeCodec.DecodeRequest(data));
    }

    [Fact]
    public void DecodeRequest_OversizedPayloadLength_Throws()
    {
        var payloadLen = RpcEnvelopeCodec.MaxPayloadSize + 1;
        var data = new byte[1 + 4 + 4 + 4 + 4];
        var offset = 0;
        data[offset++] = (byte)RpcFrameType.Request;
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(offset), 1); offset += 4;
        BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(offset), 1); offset += 4;
        BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(offset), 1); offset += 4;
        BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(offset), payloadLen);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            RpcEnvelopeCodec.DecodeRequest(data));
        Assert.Contains("invalid", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DecodeResponse_OversizedPayloadLength_Throws()
    {
        var payloadLen = RpcEnvelopeCodec.MaxPayloadSize + 1;
        var data = new byte[1 + 4 + 1 + 4];
        var offset = 0;
        data[offset++] = (byte)RpcFrameType.Response;
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(offset), 1); offset += 4;
        data[offset++] = 0;
        BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(offset), payloadLen);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            RpcEnvelopeCodec.DecodeResponse(data));
        Assert.Contains("invalid", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PushRoundTrip_PreservesAllFields()
    {
        var original = new RpcPushEnvelope
        {
            ServiceId = 5,
            MethodId = 3,
            Payload = new byte[] { 10, 20, 30 }
        };

        var encoded = RpcEnvelopeCodec.EncodePush(original);
        var decoded = RpcEnvelopeCodec.DecodePush(encoded);

        Assert.Equal(original.ServiceId, decoded.ServiceId);
        Assert.Equal(original.MethodId, decoded.MethodId);
        Assert.Equal(original.Payload, decoded.Payload);
    }

    [Fact]
    public void KeepAlivePingRoundTrip_PreservesTimestamp()
    {
        var original = new RpcKeepAlivePingEnvelope
        {
            TimestampTicksUtc = DateTimeOffset.UtcNow.UtcTicks
        };

        var encoded = RpcEnvelopeCodec.EncodeKeepAlivePing(original);
        var decoded = RpcEnvelopeCodec.DecodeKeepAlivePing(encoded);

        Assert.Equal(original.TimestampTicksUtc, decoded.TimestampTicksUtc);
    }

    [Fact]
    public void KeepAlivePongRoundTrip_PreservesTimestamp()
    {
        var original = new RpcKeepAlivePongEnvelope
        {
            TimestampTicksUtc = DateTimeOffset.UtcNow.UtcTicks
        };

        var encoded = RpcEnvelopeCodec.EncodeKeepAlivePong(original);
        var decoded = RpcEnvelopeCodec.DecodeKeepAlivePong(encoded);

        Assert.Equal(original.TimestampTicksUtc, decoded.TimestampTicksUtc);
    }

    [Fact]
    public void PeekFrameType_ReturnsCorrectType()
    {
        var req = RpcEnvelopeCodec.EncodeRequest(new RpcRequestEnvelope
            { RequestId = 1, ServiceId = 1, MethodId = 1 });
        var resp = RpcEnvelopeCodec.EncodeResponse(new RpcResponseEnvelope
            { RequestId = 1, Status = RpcStatus.Ok });
        var push = RpcEnvelopeCodec.EncodePush(new RpcPushEnvelope
            { ServiceId = 1, MethodId = 1 });
        var ping = RpcEnvelopeCodec.EncodeKeepAlivePing(new RpcKeepAlivePingEnvelope
            { TimestampTicksUtc = 1 });
        var pong = RpcEnvelopeCodec.EncodeKeepAlivePong(new RpcKeepAlivePongEnvelope
            { TimestampTicksUtc = 2 });

        Assert.Equal(RpcFrameType.Request, RpcEnvelopeCodec.PeekFrameType(req));
        Assert.Equal(RpcFrameType.Response, RpcEnvelopeCodec.PeekFrameType(resp));
        Assert.Equal(RpcFrameType.Push, RpcEnvelopeCodec.PeekFrameType(push));
        Assert.Equal(RpcFrameType.KeepAlivePing, RpcEnvelopeCodec.PeekFrameType(ping));
        Assert.Equal(RpcFrameType.KeepAlivePong, RpcEnvelopeCodec.PeekFrameType(pong));
    }

    [Fact]
    public void PeekFrameType_EmptyData_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            RpcEnvelopeCodec.PeekFrameType(ReadOnlySpan<byte>.Empty));
    }

    [Fact]
    public void DecodeRequest_WrongFrameType_Throws()
    {
        var push = RpcEnvelopeCodec.EncodePush(new RpcPushEnvelope
            { ServiceId = 1, MethodId = 1 });

        Assert.Throws<InvalidOperationException>(() =>
            RpcEnvelopeCodec.DecodeRequest(push));
    }

    [Fact]
    public void EncodePush_NullArg_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => RpcEnvelopeCodec.EncodePush(null!));
    }

    [Fact]
    public void RequestRoundTrip_EmptyPayload()
    {
        var original = new RpcRequestEnvelope
        {
            RequestId = 0,
            ServiceId = 0,
            MethodId = 0,
            Payload = Array.Empty<byte>()
        };

        var encoded = RpcEnvelopeCodec.EncodeRequest(original);
        var decoded = RpcEnvelopeCodec.DecodeRequest(encoded);

        Assert.Empty(decoded.Payload);
    }

    [Fact]
    public void RequestRoundTrip_NullPayloadTreatedAsEmpty()
    {
        var original = new RpcRequestEnvelope
        {
            RequestId = 1,
            ServiceId = 1,
            MethodId = 1,
            Payload = null!
        };

        var encoded = RpcEnvelopeCodec.EncodeRequest(original);
        var decoded = RpcEnvelopeCodec.DecodeRequest(encoded);

        Assert.Empty(decoded.Payload);
    }

    [Fact]
    public void ResponseRoundTrip_UnicodeErrorMessage()
    {
        var original = new RpcResponseEnvelope
        {
            RequestId = 5,
            Status = RpcStatus.Exception,
            Payload = Array.Empty<byte>(),
            ErrorMessage = "错误消息 with emoji 🎉"
        };

        var encoded = RpcEnvelopeCodec.EncodeResponse(original);
        var decoded = RpcEnvelopeCodec.DecodeResponse(encoded);

        Assert.Equal(original.ErrorMessage, decoded.ErrorMessage);
    }

    [Fact]
    public void MaxPayloadSize_Is64MB()
    {
        Assert.Equal(64 * 1024 * 1024, RpcEnvelopeCodec.MaxPayloadSize);
    }
}
