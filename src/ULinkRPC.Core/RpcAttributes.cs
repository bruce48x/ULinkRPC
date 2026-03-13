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
        public Type? Callback { get; set; }
    }

    /// <summary>
    ///     Marks an interface as the callback contract for a specific RPC service.
    /// </summary>
    [AttributeUsage(AttributeTargets.Interface)]
    public sealed class RpcCallbackAttribute : Attribute
    {
        public RpcCallbackAttribute(Type serviceType)
        {
            ServiceType = serviceType;
        }

        public Type ServiceType { get; }
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

    /// <summary>
    ///     Marks an interface method as a server-to-client push callback. MethodId must be stable within a callback contract.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public sealed class RpcPushAttribute : Attribute
    {
        public RpcPushAttribute(int methodId)
        {
            MethodId = methodId;
        }

        public int MethodId { get; }
    }
}
