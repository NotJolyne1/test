using RelayNetworking.Common;

namespace RelayNetworking.Common.Rpc
{
    public readonly struct RpcInfo
    {
        public int SenderPlayerId { get; }
        public RelayPlayer? Sender { get; }
        public bool IsLeader { get; }
        public ulong Tick { get; }

        public RpcInfo(int senderPlayerId, RelayPlayer? sender, bool isLeader, ulong sentTick)
        {
            SenderPlayerId = senderPlayerId;
            Sender = sender;
            IsLeader = isLeader;
            Tick = sentTick;
        }
    }
}