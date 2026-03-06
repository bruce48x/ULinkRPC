using System;
using System.Threading.Tasks;
using Game.Rpc.Contracts;
using ULinkRPC.Core;

namespace Rpc.Generated
{
    public static class PlayerCallbackBinder
    {
        private const int ServiceId = 1;

        private static readonly RpcPushMethod<string> onNotifyPushMethod = new(ServiceId, 1);

        public static void Bind(IRpcClient client, IPlayerCallback receiver)
        {
            client.RegisterPushHandler(onNotifyPushMethod, (arg) =>
            {
                receiver.OnNotify(arg);
            });

        }
    }
}
