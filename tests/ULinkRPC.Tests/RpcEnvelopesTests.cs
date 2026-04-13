using ULinkRPC.Core;

namespace ULinkRPC.Tests;

public class RpcEnvelopesTests
{
    [Fact]
    public void RpcRequestEnvelope_DefaultPayload_IsEmpty()
    {
        var req = new RpcRequestEnvelope();
        Assert.True(req.Payload.IsEmpty);
    }

    [Fact]
    public void RpcResponseEnvelope_DefaultPayload_IsEmpty()
    {
        var resp = new RpcResponseEnvelope();
        Assert.True(resp.Payload.IsEmpty);
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
        Assert.True(push.Payload.IsEmpty);
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
