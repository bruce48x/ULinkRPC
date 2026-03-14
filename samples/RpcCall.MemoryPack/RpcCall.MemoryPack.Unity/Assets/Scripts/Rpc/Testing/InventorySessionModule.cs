#nullable enable

using System;
using System.Threading.Tasks;
using Game.Rpc.Contracts;
using Rpc.Generated;
using ULinkRPC.Client;

namespace Rpc.Testing
{
    internal sealed class InventorySessionModule : RpcClient.InventoryCallbackBase
    {
        private readonly IConnectionSessionHost _host;
        private IInventoryService? _service;

        public InventorySessionModule(IConnectionSessionHost host)
        {
            _host = host;
        }

        public void Attach(IInventoryService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        public ValueTask<int> LoadAsync()
        {
            return GetService().GetRevisionAsync();
        }

        public async ValueTask<int> PollAsync()
        {
            var revision = await GetService().IncrRevision();
            if (!_host.IsStopping)
                _host.UpdateInventoryRevision(revision);
            return revision;
        }

        public override void OnInventoryNotify(string message)
        {
            if (_host.IsStopping)
                return;

            _host.UpdateLastMessage(message);
            _host.AppendLog($"Session[{_host.Index}] inventory push: {message}");
        }

        private IInventoryService GetService()
        {
            return _service ?? throw new InvalidOperationException("Inventory service is not attached.");
        }
    }
}
