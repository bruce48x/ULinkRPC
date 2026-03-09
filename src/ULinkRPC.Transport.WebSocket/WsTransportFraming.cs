using System.Buffers;
using System.Net.WebSockets;
using ULinkRPC.Core;
using NetWebSocket = System.Net.WebSockets.WebSocket;

namespace ULinkRPC.Transport.WebSocket;

internal static class WsTransportFraming
{
    private const int MaxBufferedBytes = 64 * 1024 * 1024;

    public static async ValueTask SendFrameAsync(NetWebSocket webSocket, ReadOnlyMemory<byte> frame, CancellationToken ct)
    {
        if (webSocket.State != WebSocketState.Open)
            throw new InvalidOperationException("Not connected.");

        var packed = LengthPrefix.Pack(frame.Span);
        await webSocket.SendAsync(packed, WebSocketMessageType.Binary, true, ct).ConfigureAwait(false);
    }

    public static async ValueTask<ReadOnlyMemory<byte>> ReceiveFrameAsync(
        NetWebSocket webSocket,
        byte[] accum,
        Action<byte[]> setAccum,
        CancellationToken ct)
    {
        if (webSocket.State != WebSocketState.Open)
            throw new InvalidOperationException("Not connected.");

        while (true)
        {
            var tmp = ArrayPool<byte>.Shared.Rent(8 * 1024);
            try
            {
                var res = await webSocket.ReceiveAsync(tmp, ct).ConfigureAwait(false);
                if (res.MessageType == WebSocketMessageType.Close)
                    throw new IOException("WebSocket closed.");

                var oldLen = accum.Length;
                if (oldLen + res.Count > MaxBufferedBytes)
                    throw new InvalidOperationException("WebSocket frame buffer exceeded maximum size.");

                Array.Resize(ref accum, oldLen + res.Count);
                Array.Copy(tmp, 0, accum, oldLen, res.Count);

                var seq = new ReadOnlySequence<byte>(accum);
                if (LengthPrefix.TryUnpack(ref seq, out var payloadSeq))
                {
                    var payload = payloadSeq.ToArray();
                    setAccum(seq.ToArray());
                    return payload;
                }

                setAccum(accum);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(tmp);
            }
        }
    }

    public static async ValueTask DisposeAsync(NetWebSocket webSocket)
    {
        try
        {
            if (webSocket.State == WebSocketState.Open || webSocket.State == WebSocketState.CloseReceived)
                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None)
                    .ConfigureAwait(false);
        }
        catch
        {
        }

        webSocket.Dispose();
    }
}
