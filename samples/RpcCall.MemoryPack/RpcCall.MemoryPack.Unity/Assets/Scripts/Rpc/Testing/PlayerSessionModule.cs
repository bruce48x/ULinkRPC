#nullable enable

using System;
using System.Threading.Tasks;
using Game.Rpc.Contracts;
using Rpc.Generated;
using ULinkRPC.Client;

namespace Rpc.Testing
{
    internal sealed class PlayerSessionModule : RpcClient.PlayerCallbackBase
    {
        private readonly IConnectionSessionHost _host;
        private IPlayerService? _service;

        public PlayerSessionModule(IConnectionSessionHost host)
        {
            _host = host;
        }

        public void Attach(IPlayerService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        public ValueTask<LoginReply> LoginAsync(LoginRequest request)
        {
            return GetService().LoginAsync(request);
        }

        public async ValueTask<int> PollAsync()
        {
            var step = await GetService().IncrStep();
            if (!_host.IsStopping)
                _host.UpdatePlayerStep(step);
            return step;
        }

        public override void OnPlayerNotify(string message)
        {
            if (_host.IsStopping)
                return;

            _host.UpdateLastMessage(message);
            _host.AppendLog($"Session[{_host.Index}] player push: {message}");
        }

        private IPlayerService GetService()
        {
            return _service ?? throw new InvalidOperationException("Player service is not attached.");
        }
    }
}
