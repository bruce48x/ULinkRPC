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

    public ValueTask<int> GetRevisionAsync()
    {
        if (!_announced)
        {
            _announced = true;
            _callback.OnInventoryNotify("Inventory ready.");
        }

        return new ValueTask<int>(_revision);
    }

    public ValueTask<int> IncrRevision()
    {
        _revision++;
        _callback.OnInventoryNotify($"Inventory revision => {_revision}");
        return new ValueTask<int>(_revision);
    }
}
