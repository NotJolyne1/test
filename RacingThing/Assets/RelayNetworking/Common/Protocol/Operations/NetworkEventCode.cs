namespace RelayNetworking.Common.Protocol.Networking
{
    public enum EventCode : ushort
    {
        Connected = 1,
        Disconnected,
        Authenticated,
        AuthenticationFailed,
        RoomCreated,
        RoomCreationFailed,
        RoomJoined,
        RoomJoinFailed,
        RoomLeft,
        PlayerJoined,
        PlayerLeft,
        PlayerSync,
        PlayerProperties,
        RoomProperties,
        LeaderChanged,
        Spawn,
        Destroy,
        ObjectTransform,
        Rpc,
        Ping,
        EventReceived,
        NetworkVariable,
        OwnershipChanged,
        TimeSync,
        OwnershipReclaimed,
        DropConnection,
        ObjectHidden,
        SceneLoad,
        ChildTransform,
        SpawnFailed,
        NetworkCollection
    }
}