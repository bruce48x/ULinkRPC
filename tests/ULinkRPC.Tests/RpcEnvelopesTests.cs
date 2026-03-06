using ULinkRPC.Core;

namespace ULinkRPC.Tests;

public class RpcEnvelopesTests
{
    [Fact]
    public void RpcRequestEnvelope_DefaultPayload_IsEmpty()
    {
        var req = new RpcRequestEnvelope();
        Assert.NotNull(req.Payload);
        Assert.Empty(req.Payload);
    }

    [Fact]
    public void RpcResponseEnvelope_DefaultPayload_IsEmpty()
    {
        var resp = new RpcResponseEnvelope();
        Assert.NotNull(resp.Payload);
        Assert.Empty(resp.Payload);
    }

    [Fact]
    public void RpcResponseEnvelope_DefaultErrorMessage_IsNull()
    {
        var resp = new RpcResponseEnvelope();
        Assert.Null(resp.ErrorMessage);
    }

    [Fact]
    public void RpcPushEnvelope_DefaultPayload_IsEmpty()
    {
        var push = new RpcPushEnvelope();
        Assert.NotNull(push.Payload);
        Assert.Empty(push.Payload);
    }

    [Fact]
    public void RpcVoid_Instance_IsSingleton()
    {
        var a = RpcVoid.Instance;
        var b = RpcVoid.Instance;
        Assert.Same(a, b);
    }

    [Fact]
    public void RpcStatus_Values()
    {
        Assert.Equal(0, (byte)RpcStatus.Ok);
        Assert.Equal(1, (byte)RpcStatus.NotFound);
        Assert.Equal(2, (byte)RpcStatus.Exception);
    }

    [Fact]
    public void RpcFrameType_Values()
    {
        Assert.Equal(1, (byte)RpcFrameType.Request);
        Assert.Equal(2, (byte)RpcFrameType.Response);
        Assert.Equal(3, (byte)RpcFrameType.Push);
    }
}
