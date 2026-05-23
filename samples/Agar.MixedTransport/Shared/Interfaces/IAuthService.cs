using System.Threading.Tasks;
using ULinkRPC.Core;

namespace Shared.Interfaces;

[RpcService(RpcContractIds.Services.Auth)]
public interface IAuthService
{
    [RpcMethod(RpcContractIds.AuthServiceMethods.LoginAsync)]
    ValueTask<LoginReply> LoginAsync(LoginRequest request);
}
