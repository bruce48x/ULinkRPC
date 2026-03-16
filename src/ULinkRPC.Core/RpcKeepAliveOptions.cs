namespace ULinkRPC.Core;

public sealed class RpcKeepAliveOptions
{
    public static RpcKeepAliveOptions Disabled { get; } = new();

    public bool Enabled { get; set; }

    public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(15);

    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(45);

    public bool MeasureRtt { get; set; } = true;
}
