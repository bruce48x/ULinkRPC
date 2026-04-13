using ULinkRPC.Client;
using ULinkRPC.Core;

namespace ULinkRPC.Tests;

public sealed class RpcKeepAliveOptionsTests
{
    [Fact]
    public void Disabled_ReturnsSingletonDefaultOptions()
    {
        var first = RpcKeepAliveOptions.Disabled;
        var second = RpcKeepAliveOptions.Disabled;

        Assert.Same(first, second);
        Assert.False(first.Enabled);
        Assert.Equal(TimeSpan.FromSeconds(15), first.Interval);
        Assert.Equal(TimeSpan.FromSeconds(45), first.Timeout);
        Assert.True(first.MeasureRtt);
        Assert.False(second.Enabled);
    }

    [Fact]
    public void RpcClientOptions_DefaultKeepAlive_UsesSharedImmutableDisabledOptions()
    {
        var first = new RpcClientOptions(new StubTransport(), new StubSerializer());
        var second = new RpcClientOptions(new StubTransport(), new StubSerializer());

        Assert.Same(RpcKeepAliveOptions.Disabled, first.KeepAlive);
        Assert.Same(first.KeepAlive, second.KeepAlive);
        Assert.False(first.KeepAlive.Enabled);
        Assert.Equal(TimeSpan.FromSeconds(15), first.KeepAlive.Interval);
    }

    private sealed class StubTransport : ITransport
    {
        public bool IsConnected => true;

        public ValueTask ConnectAsync(CancellationToken ct = default) => default;

        public ValueTask SendFrameAsync(ReadOnlyMemory<byte> frame, CancellationToken ct = default) => default;

        public ValueTask<TransportFrame> ReceiveFrameAsync(CancellationToken ct = default) =>
            ValueTask.FromResult(TransportFrame.Empty);

        public ValueTask DisposeAsync() => default;
    }

    private sealed class StubSerializer : IRpcSerializer
    {
        public TransportFrame SerializeFrame<T>(T value) => TransportFrame.Empty;

        public byte[] Serialize<T>(T value) => Array.Empty<byte>();

        public T Deserialize<T>(ReadOnlySpan<byte> payload) => default!;

        public T Deserialize<T>(ReadOnlyMemory<byte> payload) => default!;
    }
}
