namespace RelayNetworking.Common.Protocol.Networking
{
    public struct PacketHeader
    {
        public const byte CurrentVersion = 1;
        public byte Version;
        public ushort Code;
        public byte Flags;
    }
}