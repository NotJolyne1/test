namespace RelayNetworking.Common.Protocol.Networking
{
    public enum CommandCode : ushort
    {
        CmdAuthenticate = 1,

        CmdCreateRoom,
        CmdJoinRoom,
        CmdLeaveRoom,
        CmdJoinRandomRoom,

        CmdSpawn,
        CmdDestroy,
        CmdTransform,

        CmdRegisterNetworkVariables,
        CmdNetworkVariable,

        CmdRegisterRpcs,
        CmdRpc,
        CmdClearBufferedRpcs,

        CmdPing,
        CmdEvent,

        CmdPlayerProperties,
        CmdRoomProperties,

        CmdTransferLeader,
        CmdTransferOwnership,

        CmdTimeSyncRequest,
        CmdDropConnection,
        CmdSetPlayerInterest,
        CmdLoadScene,
        CmdSceneReady,
        CmdChildTransform,
        CmdClearBufferedRpc,
        CmdClearAllBufferedRpcs,
        CmdRegisterNetworkCollections,
        CmdNetworkCollection
    }
}