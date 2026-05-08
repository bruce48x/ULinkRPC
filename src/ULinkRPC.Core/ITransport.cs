namespace ULinkRPC.Core
{
    /// <summary>
    ///     Transport boundary for RPC: sends and receives complete frames (one message).
    ///     TCP/WS/KCP differences are hidden below this interface.
    /// </summary>
    public interface ITransport : IAsyncDisposable
    {
        bool IsConnected { get; }

        /// <summary>
        ///     Prepares this transport for frame I/O.
        /// </summary>
        /// <remarks>
        ///     Client transports use this call to actively connect to their remote endpoint.
        ///     Accepted server transports use it to initialize per-connection state such as streams,
        ///     schedulers, or framing over an already accepted connection. In-memory or already-open
        ///     transports may implement it as an idempotent no-op.
        /// </remarks>
        ValueTask ConnectAsync(CancellationToken ct = default);
        ValueTask SendFrameAsync(ReadOnlyMemory<byte> frame, CancellationToken ct = default);
        ValueTask<TransportFrame> ReceiveFrameAsync(CancellationToken ct = default);
    }
}
