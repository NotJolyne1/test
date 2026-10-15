using RelayNetworking.Common;

namespace RelayNetworking.Client
{
    public readonly struct RelayEvent
    {
        public readonly int SenderPlayerId;
        public readonly byte EventCode;
        public readonly byte[] Payload;
        public readonly ulong Tick;
        public RelayPlayer? Player => RelayNetwork.GetPlayer(SenderPlayerId);
        internal RelayEvent(int senderPlayerId, byte eventCode, byte[] payload, ulong sentTick)
        {
            SenderPlayerId = senderPlayerId;
            EventCode = eventCode;
            Payload = payload;
            Tick = sentTick;
        }
    }
}