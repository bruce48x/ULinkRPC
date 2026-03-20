using Game.Rpc.Contracts;

namespace RpcCall.MemoryPack.Server.Services;

public class InventoryService : IInventoryService
{
    private readonly IInventoryCallback _callback;
    private int _revision;
    private bool _announced;

    public InventoryService(IInventoryCallback callback)
    {
        _callback = callback;
    }

    public ValueTask<RevisionReply> GetRevisionAsync(RevisionRequest req)
    {
        if (!_announced)
        {
            _announced = true;
            _callback.OnInventoryNotify(new InventoryNotify
            {
                Message = "Inventory ready."
            });
        }

        return new ValueTask<RevisionReply>(new RevisionReply
        {
            Revision = _revision
        });
    }

    public ValueTask<RevisionReply> IncrRevision(RevisionRequest req)
    {
        _revision++;
        _callback.OnInventoryNotify(new InventoryNotify
        {
            Message = $"Inventory revision => {_revision}"
        });
        return new ValueTask<RevisionReply>(new RevisionReply
        {
            Revision = _revision
        });
    }
}
