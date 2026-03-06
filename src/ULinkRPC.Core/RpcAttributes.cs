using System;

namespace ULinkRPC.Core
{
    /// <summary>
    ///     Marks an interface as an RPC service. ServiceId must be stable across versions.
    /// </summary>
    [AttributeUsage(AttributeTargets.Interface)]
    public sealed class RpcServiceAttribute : Attribute
    {
        public RpcServiceAttribute(int serviceId)
        {
            ServiceId = serviceId;
        }

        public int ServiceId { get; }
    }

    /// <summary>
    ///     Marks an interface method as an RPC method. MethodId must be stable within a service.
    ///     Methods can declare zero to many parameters; ULinkRPC.CodeGen will generate payload packing/unpacking.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public sealed class RpcMethodAttribute : Attribute
    {
        public RpcMethodAttribute(int methodId)
        {
            MethodId = methodId;
        }

        public int MethodId { get; }
    }
}
