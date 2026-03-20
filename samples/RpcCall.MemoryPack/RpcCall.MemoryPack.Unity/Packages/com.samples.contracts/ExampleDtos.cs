using MemoryPack;

namespace Game.Rpc.Contracts
{
    [MemoryPackable]
    public partial class LoginRequest
    {
        public string Account { get; set; } = "";
        public string Password { get; set; } = "";
    }

    [MemoryPackable]
    public partial class LoginReply
    {
        public int Code { get; set; }
        public string Token { get; set; } = "";
    }

    [MemoryPackable]
    public partial class StepRequest
    {
    }

    [MemoryPackable]
    public partial class StepReply
    {
        public int Step { get; set; }
    }

    [MemoryPackable]
    public partial class RevisionRequest
    {
    }

    [MemoryPackable]
    public partial class RevisionReply
    {
        public int Revision { get; set; }
    }

    [MemoryPackable]
    public partial class ProgressRequest
    {
    }

    [MemoryPackable]
    public partial class ProgressReply
    {
        public int Progress { get; set; }
    }

    [MemoryPackable]
    public partial class PlayerNotify
    {
        public string Message { get; set; } = "";
    }

    [MemoryPackable]
    public partial class InventoryNotify
    {
        public string Message { get; set; } = "";
    }

    [MemoryPackable]
    public partial class QuestNotify
    {
        public string Message { get; set; } = "";
    }
}
