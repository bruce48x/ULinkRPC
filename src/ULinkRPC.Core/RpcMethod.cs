namespace ULinkRPC.Core
{
    public readonly struct RpcMethod<TArg, TResult>
    {
        public RpcMethod(int serviceId, int methodId)
        {
            ServiceId = serviceId;
            MethodId = methodId;
        }

        public int ServiceId { get; }
        public int MethodId { get; }
    }

    public readonly struct RpcPushMethod<TArg>
    {
        public RpcPushMethod(int serviceId, int methodId)
        {
            ServiceId = serviceId;
            MethodId = methodId;
        }

        public int ServiceId { get; }
        public int MethodId { get; }
    }
}
