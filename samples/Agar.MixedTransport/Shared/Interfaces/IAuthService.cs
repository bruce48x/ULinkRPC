using System.Threading.Tasks;
using ULinkRPC.Core;

namespace Shared.Interfaces;

[RpcService(1)]
public interface IAuthService
{
    [RpcMethod(1)]
    ValueTask<LoginReply> LoginAsync(LoginRequest request);
}
