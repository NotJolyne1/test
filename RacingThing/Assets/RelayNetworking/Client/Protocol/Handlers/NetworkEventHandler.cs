using System;
using System.Linq;
using RelayNetworking.Common;
using RelayNetworking.Common.Callbacks;
using RelayNetworking.Common.Collections;
using RelayNetworking.Common.Data;
using RelayNetworking.Common.Protocol.Networking;
using RelayNetworking.Common.Results;
using RelayNetworking.Common.Rooms;
using RelayNetworking.Common.Serialization;
using System.Numerics;
using System.Threading.Tasks;

namespace RelayNetworking.Client.Protocol.Handlers
{
    internal sealed class NetworkEventHandler
    {
        public void Handle(PacketHeader header, NetworkCommandReader reader)
        {
            switch ((EventCode)header.Code)
            {
                case EventCode.Connected:
                    {
                        HandleConnected();
                        break;
                    }

                case EventCode.Ping:
                    {
                        HandlePing(reader);
                        break;
                    }

                case EventCode.RoomCreated:
                    {
                        HandleRoomCreated(reader);
                        break;
                    }

                case EventCode.RoomCreationFailed:
                    {
                        HandleRoomCreationFailed(reader);
                        break;
                    }
                case EventCode.RoomJoined:
                    {
                        HandleRoomJoined(reader);
                        break;
                    }
                case EventCode.RoomLeft:
                    {
                        HandleRoomLeft();
                        break;
                    }
                case EventCode.RoomJoinFailed:
                    {
                        HandleRoomJoinFailed(reader);
                        break;
                    }
                case EventCode.Authenticated:
                    {
                        HandleAuthenticated(reader);
                        break;
                    }
                case EventCode.PlayerSync:
                    {
                        HandlePlayerSync(reader);
                        break;
                    }
                case EventCode.PlayerJoined:
                    {
                        HandlePlayerJoined(reader);
                        break;
                    }
                case EventCode.LeaderChanged:
                    {
                        HandleLeaderChanged(reader);
                        break;
                    }
                case EventCode.PlayerProperties:
                    {
                        HandlePlayerProperties(reader);
                        break;
                    }

                case EventCode.RoomProperties:
                    {
                        HandleRoomProperties(reader);
                        break;
                    }
                case EventCode.PlayerLeft:
                    {
                        HandlePlayerLeft(reader);
                        break;
                    }
                case EventCode.EventReceived:
                    HandleEventReceived(reader);
                    break;

                case EventCode.Spawn:
                    HandleObjectSpawned(reader);
                    break;

                case EventCode.SpawnFailed:
                    HandleSpawnFailed(reader);
                    break;

                case EventCode.Destroy:
                    HandleObjectDestroyed(reader);
                    break;

                case EventCode.ObjectHidden:
                    HandleObjectHidden(reader);
                    break;

                case EventCode.SceneLoad:
                    HandleSceneLoad(reader);
                    break;

                case EventCode.ChildTransform:
                    HandleChildTransform(reader);
                    break;

                case EventCode.ObjectTransform:
                    HandleObjectTransform(reader);
                    break;

                case EventCode.NetworkVariable:
                    HandleNetworkVariable(reader);
                    break;

                case EventCode.NetworkCollection:
                    HandleNetworkCollection(reader);
                    break;

                case EventCode.Rpc:
                    HandleRpc(reader);
                    break;
                case EventCode.OwnershipChanged:
                    {
                        HandleOwnershipChanged(reader);
                        break;
                    }
                case EventCode.TimeSync:
                    {
                        HandleTimeSync(reader);
                        break;
                    }
                case EventCode.OwnershipReclaimed:
                    {
                        RelayNetwork.OwnershipReclaimed();
                        break;
                    }
                case EventCode.DropConnection:
                    {
                        if (RelayNetwork.EnableDropConnection)
                            RelayNetwork.LeaveRoom();
                        break;
                    }
                default:
                    {
                        Console.WriteLine($"[Client] Unknown event ({(EventCode)header.Code})");
                        break;
                    }
            }
        }

        private void HandleConnected()
        {
        }

        private void HandlePing(NetworkCommandReader reader)
        {
            long time = reader.GetLong();

            int ping = (int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - time);

            RelayNetwork.Session.Ping = ping;
            RelayCallbacks.OnPingUpdated(ping);

            Console.WriteLine($"[Client] Pong ({ping} ms)");
        }

        private void HandleRoomCreated(NetworkCommandReader reader)
        {
            string roomName = reader.GetString();

            Room room = new()
            {
                Name = roomName
            };

            RelayNetwork.Session.CurrentRoom = room;
            RelayCallbacks.OnRoomCreated(room);

            Console.WriteLine($"[Client] Created room '{room.Name}'");
        }

        private static void HandleRoomCreationFailed(NetworkCommandReader reader)
        {
            string reason = reader.GetString();

            RelayCallbacks.OnRoomCreationFailed(reason);
            Console.WriteLine($"[Client] Failed to create room ({reason})");
        }

        private void HandleRoomJoined(NetworkCommandReader reader)
        {
            string roomName = reader.GetString();

            Room room = new()
            {
                Name = roomName
            };

            RelayNetwork.Session.CurrentRoom = room;
            RelayCallbacks.OnRoomJoined(room);

            Console.WriteLine($"[Client] Joined room '{room.Name}'");
        }

        private void HandleRoomLeft()
        {
            Room? room = RelayNetwork.Session.CurrentRoom;
            RelayNetwork.Session.CurrentRoom = null;
            RelayNetwork.Session.ClearPlayers();

            if (room != null)
                RelayCallbacks.OnRoomLeft(room);

            Console.WriteLine("[Client] Left room");
        }

        private void HandleRoomJoinFailed(NetworkCommandReader reader)
        {
            JoinRoomResult result = (JoinRoomResult)reader.GetByte();

            RelayCallbacks.OnRoomJoinFailed(result);
            Console.WriteLine($"[Client] Failed to join room ({result})");
        }

        private bool HandleAuthenticated(NetworkCommandReader reader)
        {
            RelayPlayer player = new()
            {
                PlayerId = reader.GetInt(),
                UserId = reader.GetString(),
                Username = reader.GetString(),
                IsLeader = reader.GetBool()
            };

            string reclamationToken = reader.AvailableBytes > 0 ? reader.GetString() : string.Empty;
            ushort serverProtocolVersion = reader.AvailableBytes > 0
                ? reader.GetUShort()
                : NetworkProtocol.LegacyVersion;

            RelayNetwork.SetOwnershipReclamationToken(reclamationToken);
            RelayNetwork.SetServerProtocolVersion(serverProtocolVersion);

            RelayPlayer? existing = RelayNetwork.GetPlayer(player.PlayerId);

            if (existing != null)
            {
                existing.UserId = player.UserId;
                existing.Username = player.Username;
                existing.IsLeader = player.IsLeader;
                player = existing;
            }
            else
            {
                RelayNetwork.Session.AddPlayer(player);
            }

            RelayNetwork.Session.LocalPlayer = player;

            if (!RelayNetwork.IsReconnecting)
                RelayCallbacks.OnAuthenticated(player);

            Console.WriteLine($"[Client] Authenticated as '{player.Username}'");

            return true;
        }
        private void HandlePlayerSync(NetworkCommandReader reader)
        {
            int playerId = reader.GetInt();
            string userId = reader.GetString();
            string username = reader.GetString();
            bool isLeader = reader.GetBool();

            RelayPlayer? player = RelayNetwork.Session.ActivePlayers
                .FirstOrDefault(x => x.PlayerId == playerId);

            if (player == null)
            {
                if (RelayNetwork.Session.LocalPlayer?.PlayerId == playerId)
                {
                    player = RelayNetwork.Session.LocalPlayer;
                }
                else
                {
                    player = new RelayPlayer();
                }

                RelayNetwork.Session.AddPlayer(player);
            }

            player.PlayerId = playerId;
            player.UserId = userId;
            player.Username = username;
            player.IsLeader = isLeader;

            Console.WriteLine($"[Client] Synced player '{player.Username}'");
        }

        private void HandlePlayerJoined(NetworkCommandReader reader)
        {
            RelayPlayer player = new()
            {
                PlayerId = reader.GetInt(),
                UserId = reader.GetString(),
                Username = reader.GetString(),
                IsLeader = reader.GetBool()
            };

            RelayPlayer? existing = RelayNetwork.GetPlayer(player.PlayerId);

            if (existing != null)
            {
                existing.UserId = player.UserId;
                existing.Username = player.Username;
                existing.IsLeader = player.IsLeader;
                return;
            }

            RelayNetwork.Session.AddPlayer(player);
            RelayCallbacks.OnPlayerJoined(player);
            Console.WriteLine($"[Client] Player joined '{player.Username}'");
        }

        private void HandleLeaderChanged(NetworkCommandReader reader)
        {
            int playerId = reader.GetInt();

            RelayPlayer? leader = null;

            foreach (RelayPlayer player in RelayNetwork.Session.ActivePlayers)
            {
                player.IsLeader = player.PlayerId == playerId;

                if (player.IsLeader)
                    leader = player;
            }

            if (RelayNetwork.Session.CurrentRoom != null)
                RelayNetwork.Session.CurrentRoom.Leader = leader;

            if (leader != null)
                RelayCallbacks.OnLeaderChanged(leader);
        }

        private void HandlePlayerProperties(NetworkCommandReader reader)
        {
            int playerId = reader.GetInt();

            RelayPlayer? player = RelayNetwork.ActivePlayers
                .FirstOrDefault(x => x.PlayerId == playerId);

            if (player == null)
                return;

            PropertyChanges changes = new();

            PropertySerializer.ReadReserved(reader, changes);
            PropertySerializer.ReadCustom(reader, changes);

            foreach (var property in changes.Reserved)
            {
                switch (property.Key)
                {
                    case ReservedPlayerProperty.Username:
                        if (property.Value is string username)
                        {
                            player.Username = username;
                        }
                        break;
                }
            }

            foreach (var property in changes.Custom)
            {
                if (property.Value == null)
                    player.Properties.Remove(property.Key);
                else
                    player.Properties[property.Key] = property.Value;
            }

            RelayCallbacks.OnPlayerPropertiesChanged(player);
        }

        private void HandleRoomProperties(NetworkCommandReader reader)
        {
            Room? room = RelayNetwork.Session.CurrentRoom;

            if (room == null)
                return;

            PropertyChanges changes = new();

            PropertySerializer.ReadReserved(reader, changes);
            PropertySerializer.ReadCustom(reader, changes);

            foreach (var property in changes.Custom)
            {
                if (property.Value == null)
                    room.Properties.Remove(property.Key);
                else
                    room.Properties[property.Key] = property.Value;
            }

            RelayCallbacks.OnRoomPropertiesChanged(room);

            if (changes.Custom.TryGetValue(RoomProperties.CurrentSceneKey, out object? sceneValue) && sceneValue is string sceneName)
                RelayNetwork.SceneLoadRequested(sceneName);
        }

        private static void HandleSceneLoad(NetworkCommandReader reader)
        {
            string sceneName = reader.GetString();
            RelayNetwork.SceneLoadRequested(sceneName);
        }

        private static void HandleChildTransform(NetworkCommandReader reader)
        {
            uint networkId = reader.GetUInt();
            byte count = reader.GetByte();

            byte[] indices = new byte[count];
            Vector3[] positions = new Vector3[count];
            Quaternion[] rotations = new Quaternion[count];

            for (int i = 0; i < count; i++)
            {
                indices[i] = reader.GetByte();

                positions[i] = new Vector3(
                    reader.GetFloat(),
                    reader.GetFloat(),
                    reader.GetFloat());

                rotations[i] = new Quaternion(
                    reader.GetFloat(),
                    reader.GetFloat(),
                    reader.GetFloat(),
                    reader.GetFloat());
            }

            RelayNetwork.ChildTransformReceived(new ChildTransformData(networkId, indices, positions, rotations));
        }

        private void HandlePlayerLeft(NetworkCommandReader reader)
        {
            int playerId = reader.GetInt();

            RelayPlayer? player = RelayNetwork.ActivePlayers.FirstOrDefault(player => player.PlayerId == playerId);

            if (player == null)
                return;

            RelayNetwork.Session.RemovePlayer(player.PlayerId);

            RelayCallbacks.OnPlayerLeft(player);

            Console.WriteLine($"[Client] Player left '{player.Username}'");
        }

        private void HandleEventReceived(NetworkCommandReader reader)
        {
            int senderPlayerId = reader.GetInt();
            byte eventCode = reader.GetByte();
            byte[] payload = reader.GetBytes();
            ulong sentTick = reader.GetULong();

            RelayNetwork.EventReceived(senderPlayerId, eventCode, payload, sentTick);
        }

        internal void HandleObjectSpawned(NetworkCommandReader reader)
        {
            ushort prefabId = reader.GetUShort();
            uint networkId = reader.GetUInt();
            int ownerPlayerId = reader.GetInt();
            bool isPlayer = reader.GetBool();

            Vector3 position = new(
                reader.GetFloat(),
                reader.GetFloat(),
                reader.GetFloat());

            Quaternion rotation = new(
                reader.GetFloat(),
                reader.GetFloat(),
                reader.GetFloat(),
                reader.GetFloat());

            if (isPlayer && RelayNetwork.LocalPlayer?.PlayerId == ownerPlayerId)
                RelayNetwork.LocalPlayer.PlayerObjectId = networkId;

            uint requestId = reader.AvailableBytes >= 4 ? reader.GetUInt() : 0;

            RelayNetwork.ObjectSpawned(
                new SpawnData(
                    prefabId,
                    networkId,
                    ownerPlayerId,
                    isPlayer,
                    position,
                    rotation,
                    requestId));
        }

        private static void HandleSpawnFailed(NetworkCommandReader reader)
        {
            RelayNetwork.SpawnFailed(reader.GetUInt());
        }

        private static void HandleObjectDestroyed(NetworkCommandReader reader)
        {
            uint networkId = reader.GetUInt();

            Console.WriteLine($"[Client] RECEIVED DESTROY id {networkId}");

            RelayNetwork.ObjectDestroyed(
                new DestroyData(networkId));
        }

        private static void HandleObjectHidden(NetworkCommandReader reader)
        {
            uint networkId = reader.GetUInt();

            Console.WriteLine($"[Client] RECEIVED OBJECT HIDDEN id {networkId}");

            RelayNetwork.ObjectDestroyed(
                new DestroyData(networkId));
        }
        private static void HandleObjectTransform(NetworkCommandReader reader)
        {
            uint networkId = reader.GetUInt();

            System.Numerics.Vector3 position = new(
                reader.GetFloat(),
                reader.GetFloat(),
                reader.GetFloat());

            System.Numerics.Quaternion rotation = new(
                reader.GetFloat(),
                reader.GetFloat(),
                reader.GetFloat(),
                reader.GetFloat());

            System.Numerics.Vector3 velocity = new(
                reader.GetFloat(),
                reader.GetFloat(),
                reader.GetFloat());

            System.Numerics.Vector3 angularVelocity = new(
                reader.GetFloat(),
                reader.GetFloat(),
                reader.GetFloat());

            RelayNetwork.ObjectTransform(
                new TransformData(
                    networkId,
                    position,
                    rotation,
                    velocity,
                    angularVelocity));
        }

        private static void HandleNetworkVariable(NetworkCommandReader reader)
        {
            uint networkId = reader.GetUInt();
            ushort variableId = reader.GetUShort();

            NetworkVariableType type =
                (NetworkVariableType)reader.GetByte();

            object value =
                NetworkVariableSerializer.Read(reader, type);

            ushort behaviourId = reader.AvailableBytes >= 2 ? reader.GetUShort() : (ushort)0;

            RelayNetwork.NetworkVariableReceived(
                networkId,
                behaviourId,
                variableId,
                value);
        }

        private static void HandleNetworkCollection(NetworkCommandReader reader)
        {
            uint networkId = reader.GetUInt();
            ushort behaviourId = reader.GetUShort();
            ushort collectionId = reader.GetUShort();
            NetworkCollectionType collectionType = (NetworkCollectionType)reader.GetByte();
            NetworkCollectionOperation operation = (NetworkCollectionOperation)reader.GetByte();
            uint requestId = reader.GetUInt();
            NetworkVariableType keyType = (NetworkVariableType)reader.GetByte();
            NetworkVariableType valueType = (NetworkVariableType)reader.GetByte();

            int index = -1;
            object key = null;
            object value = null;
            NetworkCollectionEntry[] entries = Array.Empty<NetworkCollectionEntry>();

            if (operation == NetworkCollectionOperation.Snapshot)
            {
                ushort count = reader.GetUShort();
                entries = new NetworkCollectionEntry[count];

                for (int i = 0; i < count; i++)
                {
                    object entryKey = null;

                    if (collectionType == NetworkCollectionType.Dictionary)
                        entryKey = NetworkVariableSerializer.Read(reader, keyType);

                    object entryValue = NetworkVariableSerializer.Read(reader, valueType);
                    entries[i] = new NetworkCollectionEntry(entryKey, entryValue);
                }
            }
            else
            {
                switch (collectionType)
                {
                    case NetworkCollectionType.List:
                        switch (operation)
                        {
                            case NetworkCollectionOperation.Add:
                                index = reader.GetInt();
                                value = NetworkVariableSerializer.Read(reader, valueType);
                                break;

                            case NetworkCollectionOperation.Insert:
                            case NetworkCollectionOperation.Set:
                                index = reader.GetInt();
                                value = NetworkVariableSerializer.Read(reader, valueType);
                                break;

                            case NetworkCollectionOperation.RemoveAt:
                                index = reader.GetInt();
                                break;
                        }
                        break;

                    case NetworkCollectionType.Dictionary:
                        switch (operation)
                        {
                            case NetworkCollectionOperation.Add:
                            case NetworkCollectionOperation.Set:
                                key = NetworkVariableSerializer.Read(reader, keyType);
                                value = NetworkVariableSerializer.Read(reader, valueType);
                                break;

                            case NetworkCollectionOperation.Remove:
                                key = NetworkVariableSerializer.Read(reader, keyType);
                                break;
                        }
                        break;

                    case NetworkCollectionType.Set:
                        if (operation == NetworkCollectionOperation.Add ||
                            operation == NetworkCollectionOperation.Remove)
                        {
                            value = NetworkVariableSerializer.Read(reader, valueType);
                        }
                        break;
                }
            }

            RelayNetwork.NetworkCollectionReceived(new NetworkCollectionData(
                networkId,
                behaviourId,
                collectionId,
                collectionType,
                operation,
                requestId,
                keyType,
                valueType,
                index,
                key,
                value,
                entries));
        }

        private static void HandleRpc(NetworkCommandReader reader)
        {
            RpcData rpc = RpcSerializer.ReadEvent(reader);

            RelayNetwork.RpcReceived(rpc);
        }

        private void HandleOwnershipChanged(NetworkCommandReader reader)
        {
            uint networkId = reader.GetUInt();
            int ownerPlayerId = reader.GetInt();

            RelayNetwork.OwnershipChanged(
                networkId,
                ownerPlayerId);
        }

        private static void HandleTimeSync(NetworkCommandReader reader)
        {
            ulong requestId = reader.GetULong();
            double serverTime = reader.GetDouble();
            ulong serverTick = reader.GetULong();
            int tickRate = reader.GetInt();

            RelayNetwork.TimeSyncReceived(requestId, serverTime, serverTick, tickRate);
        }
    }
}