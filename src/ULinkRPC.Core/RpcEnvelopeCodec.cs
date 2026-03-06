using System;
using System.Buffers.Binary;
using System.Text;

namespace ULinkRPC.Core
{
    public static class RpcEnvelopeCodec
    {
        public const int MaxPayloadSize = 64 * 1024 * 1024;

        public static RpcFrameType PeekFrameType(ReadOnlySpan<byte> data)
        {
            if (data.Length < 1)
                throw new InvalidOperationException("Frame is empty.");
            return (RpcFrameType)data[0];
        }

        public static byte[] EncodeRequest(RpcRequestEnvelope req)
        {
            if (req is null) throw new ArgumentNullException(nameof(req));

            var payload = req.Payload ?? Array.Empty<byte>();
            var total = 1 + 4 + 4 + 4 + 4 + payload.Length;
            var data = new byte[total];
            var offset = 0;

            data[offset++] = (byte)RpcFrameType.Request;
            WriteUInt32(data, ref offset, req.RequestId);
            WriteInt32(data, ref offset, req.ServiceId);
            WriteInt32(data, ref offset, req.MethodId);
            WriteInt32(data, ref offset, payload.Length);
            payload.AsSpan().CopyTo(data.AsSpan(offset));
            return data;
        }

        public static RpcRequestEnvelope DecodeRequest(ReadOnlySpan<byte> data)
        {
            var offset = 0;
            var frameType = (RpcFrameType)ReadByte(data, ref offset);
            if (frameType != RpcFrameType.Request)
                throw new InvalidOperationException($"Expected Request frame, got {frameType}.");

            var requestId = ReadUInt32(data, ref offset);
            var serviceId = ReadInt32(data, ref offset);
            var methodId = ReadInt32(data, ref offset);
            var payloadLen = ReadInt32(data, ref offset);
            ValidateLength(payloadLen);
            EnsureRemaining(data, offset, payloadLen);

            var payload = data.Slice(offset, payloadLen).ToArray();
            offset += payloadLen;
            if (offset != data.Length)
                throw new InvalidOperationException("Request envelope has extra trailing bytes.");

            return new RpcRequestEnvelope
            {
                RequestId = requestId,
                ServiceId = serviceId,
                MethodId = methodId,
                Payload = payload
            };
        }

        public static byte[] EncodeResponse(RpcResponseEnvelope resp)
        {
            if (resp is null) throw new ArgumentNullException(nameof(resp));

            var payload = resp.Payload ?? Array.Empty<byte>();
            var hasError = !string.IsNullOrEmpty(resp.ErrorMessage);
            var errorBytes = hasError ? Encoding.UTF8.GetBytes(resp.ErrorMessage!) : Array.Empty<byte>();

            var total = 1 + 4 + 1 + 4 + payload.Length + 1 + (hasError ? 4 + errorBytes.Length : 0);
            var data = new byte[total];
            var offset = 0;

            data[offset++] = (byte)RpcFrameType.Response;
            WriteUInt32(data, ref offset, resp.RequestId);
            data[offset++] = (byte)resp.Status;
            WriteInt32(data, ref offset, payload.Length);
            payload.AsSpan().CopyTo(data.AsSpan(offset));
            offset += payload.Length;
            data[offset++] = hasError ? (byte)1 : (byte)0;

            if (hasError)
            {
                WriteInt32(data, ref offset, errorBytes.Length);
                errorBytes.AsSpan().CopyTo(data.AsSpan(offset));
            }

            return data;
        }

        public static RpcResponseEnvelope DecodeResponse(ReadOnlySpan<byte> data)
        {
            var offset = 0;
            var frameType = (RpcFrameType)ReadByte(data, ref offset);
            if (frameType != RpcFrameType.Response)
                throw new InvalidOperationException($"Expected Response frame, got {frameType}.");

            var requestId = ReadUInt32(data, ref offset);
            var status = (RpcStatus)ReadByte(data, ref offset);
            var payloadLen = ReadInt32(data, ref offset);
            ValidateLength(payloadLen);
            EnsureRemaining(data, offset, payloadLen);
            var payload = data.Slice(offset, payloadLen).ToArray();
            offset += payloadLen;

            var hasError = ReadByte(data, ref offset) != 0;
            string? error = null;
            if (hasError)
            {
                var errLen = ReadInt32(data, ref offset);
                ValidateLength(errLen);
                EnsureRemaining(data, offset, errLen);
                error = Encoding.UTF8.GetString(data.Slice(offset, errLen));
                offset += errLen;
            }

            if (offset != data.Length)
                throw new InvalidOperationException("Response envelope has extra trailing bytes.");

            return new RpcResponseEnvelope
            {
                RequestId = requestId,
                Status = status,
                Payload = payload,
                ErrorMessage = error
            };
        }

        public static byte[] EncodePush(RpcPushEnvelope push)
        {
            if (push is null) throw new ArgumentNullException(nameof(push));

            var payload = push.Payload ?? Array.Empty<byte>();
            var total = 1 + 4 + 4 + 4 + payload.Length;
            var data = new byte[total];
            var offset = 0;

            data[offset++] = (byte)RpcFrameType.Push;
            WriteInt32(data, ref offset, push.ServiceId);
            WriteInt32(data, ref offset, push.MethodId);
            WriteInt32(data, ref offset, payload.Length);
            payload.AsSpan().CopyTo(data.AsSpan(offset));
            return data;
        }

        public static RpcPushEnvelope DecodePush(ReadOnlySpan<byte> data)
        {
            var offset = 0;
            var frameType = (RpcFrameType)ReadByte(data, ref offset);
            if (frameType != RpcFrameType.Push)
                throw new InvalidOperationException($"Expected Push frame, got {frameType}.");

            var serviceId = ReadInt32(data, ref offset);
            var methodId = ReadInt32(data, ref offset);
            var payloadLen = ReadInt32(data, ref offset);
            ValidateLength(payloadLen);
            EnsureRemaining(data, offset, payloadLen);

            var payload = data.Slice(offset, payloadLen).ToArray();
            offset += payloadLen;
            if (offset != data.Length)
                throw new InvalidOperationException("Push envelope has extra trailing bytes.");

            return new RpcPushEnvelope
            {
                ServiceId = serviceId,
                MethodId = methodId,
                Payload = payload
            };
        }

        private static uint ReadUInt32(ReadOnlySpan<byte> data, ref int offset)
        {
            EnsureRemaining(data, offset, 4);
            var value = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(offset, 4));
            offset += 4;
            return value;
        }

        private static int ReadInt32(ReadOnlySpan<byte> data, ref int offset)
        {
            EnsureRemaining(data, offset, 4);
            var value = BinaryPrimitives.ReadInt32BigEndian(data.Slice(offset, 4));
            offset += 4;
            return value;
        }

        private static byte ReadByte(ReadOnlySpan<byte> data, ref int offset)
        {
            EnsureRemaining(data, offset, 1);
            return data[offset++];
        }

        private static void WriteUInt32(Span<byte> data, ref int offset, uint value)
        {
            BinaryPrimitives.WriteUInt32BigEndian(data.Slice(offset, 4), value);
            offset += 4;
        }

        private static void WriteInt32(Span<byte> data, ref int offset, int value)
        {
            BinaryPrimitives.WriteInt32BigEndian(data.Slice(offset, 4), value);
            offset += 4;
        }

        private static void EnsureRemaining(ReadOnlySpan<byte> data, int offset, int count)
        {
            if (offset < 0 || count < 0 || data.Length - offset < count)
                throw new InvalidOperationException("RPC envelope is malformed.");
        }

        private static void ValidateLength(int length)
        {
            if (length < 0 || length > MaxPayloadSize)
                throw new InvalidOperationException($"RPC envelope length is invalid: {length}");
        }
    }
}
