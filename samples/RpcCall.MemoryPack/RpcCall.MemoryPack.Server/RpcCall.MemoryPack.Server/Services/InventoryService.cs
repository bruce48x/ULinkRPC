using Game.Rpc.Contracts;

namespace RpcCall.MemoryPack.Server.Services;

public class InventoryService : IInventoryService
{
    private readonly IInventoryCallback _callback;
    private int _revision;

    public InventoryService(IInventoryCallback callback)
    {
        _callback = callback;
    }

    public ValueTask<LoginReply> LoginAsync(LoginRequest req)
    {
        _callback.OnInventoryNotify($"Inventory ready for {req.Account}.");
        return new ValueTask<LoginReply>(new LoginReply
        {
            Code = 0,
            Token = $"inventory-{req.Account}-{Guid.NewGuid():N}"
        });
    }

    public ValueTask<int> IncrRevision()
    {
        _revision++;
        _callback.OnInventoryNotify($"Inventory revision => {_revision}");
        return new ValueTask<int>(_revision);
    }
}
