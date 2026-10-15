using System;
using RelayNetworking.Common.Results;
using RelayNetworking.Common.Rooms;

namespace RelayNetworking.Common.Callbacks
{
    public static class RelayCallbacks
    {
        public static event Action? Connected;
        public static event Action? Disconnected;
        public static event Action<RelayPlayer>? Authenticated;
        public static event Action<Room>? RoomCreated;
        public static event Action<string>? RoomCreationFailed;
        public static event Action<Room>? RoomJoined;
        public static event Action<Room>? RoomLeft;
        public static event Action<Room>? RoomClosed;
        public static event Action<JoinRoomResult>? RoomJoinFailed;
        public static event Action<RelayPlayer>? PlayerJoined;
        public static event Action<RelayPlayer>? PlayerLeft;
        public static event Action<RelayPlayer>? PlayerPropertiesChanged;
        public static event Action<RelayPlayer>? LeaderChanged;
        public static event Action<Room>? RoomPropertiesChanged;
        public static event Action<int>? PingUpdated;
        public static event Action? OwnershipReclaimed;

        internal static void OnConnected() => Connected?.Invoke();
        internal static void OnDisconnected() => Disconnected?.Invoke();
        internal static void OnAuthenticated(RelayPlayer player) => Authenticated?.Invoke(player);
        internal static void OnRoomCreated(Room room) => RoomCreated?.Invoke(room);
        internal static void OnRoomCreationFailed(string reason) => RoomCreationFailed?.Invoke(reason);
        internal static void OnRoomJoined(Room room) => RoomJoined?.Invoke(room);
        internal static void OnRoomLeft(Room room) => RoomLeft?.Invoke(room);
        internal static void OnRoomClosed(Room room) => RoomClosed?.Invoke(room);
        internal static void OnRoomJoinFailed(JoinRoomResult result) => RoomJoinFailed?.Invoke(result);
        internal static void OnPlayerJoined(RelayPlayer player) => PlayerJoined?.Invoke(player);
        internal static void OnPlayerLeft(RelayPlayer player) => PlayerLeft?.Invoke(player);
        internal static void OnLeaderChanged(RelayPlayer player) => LeaderChanged?.Invoke(player);
        internal static void OnPingUpdated(int ping) => PingUpdated?.Invoke(ping);
        internal static void OnPlayerPropertiesChanged(RelayPlayer player) => PlayerPropertiesChanged?.Invoke(player);
        internal static void OnRoomPropertiesChanged(Room room) => RoomPropertiesChanged?.Invoke(room);
        internal static void OnOwnershipReclaimed() => OwnershipReclaimed?.Invoke();
    }
}
