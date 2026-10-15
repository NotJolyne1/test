namespace RelayNetworking.Common.Protocol
{
    public enum EventTarget : byte
    {
        All = 0,
        Others = 1,
        Leader = 2,
        Self = 3
    }
}