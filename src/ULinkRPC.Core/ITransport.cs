namespace ULinkRPC.Core
{
    /// <summary>
    ///     Transport boundary for RPC: sends and receives complete frames (one message).
    ///     TCP/WS/KCP differences are hidden below this interface.
    /// </summary>
    public interface ITransport : IAsyncDisposable
    {
        bool IsConnected { get; }
        ValueTask ConnectAsync(CancellationToken ct = default);
        ValueTask SendFrameAsync(ReadOnlyMemory<byte> frame, CancellationToken ct = default);
        ValueTask<TransportFrame> ReceiveFrameAsync(CancellationToken ct = default);
    }
}
