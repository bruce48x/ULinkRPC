#nullable enable

using System;
using System.Threading.Tasks;
using Game.Rpc.Contracts;
using Rpc.Generated;
using ULinkRPC.Client;

namespace Rpc.Testing
{
    internal sealed class QuestSessionModule : RpcClient.QuestCallbackBase
    {
        private readonly IConnectionSessionHost _host;
        private IQuestService? _service;

        public QuestSessionModule(IConnectionSessionHost host)
        {
            _host = host;
        }

        public void Attach(IQuestService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        public ValueTask<int> LoadAsync()
        {
            return GetService().GetProgressAsync();
        }

        public async ValueTask<int> PollAsync()
        {
            var progress = await GetService().IncrProgress();
            if (!_host.IsStopping)
                _host.UpdateQuestProgress(progress);
            return progress;
        }

        public override void OnQuestNotify(string message)
        {
            if (_host.IsStopping)
                return;

            _host.UpdateLastMessage(message);
            _host.AppendLog($"Session[{_host.Index}] quest push: {message}");
        }

        private IQuestService GetService()
        {
            return _service ?? throw new InvalidOperationException("Quest service is not attached.");
        }
    }
}
