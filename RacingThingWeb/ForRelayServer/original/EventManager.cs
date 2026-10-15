using RelayNetworking.Common;
using RelayNetworking.Common.Data;
using RelayNetworking.Common.Protocol.Networking;
using RelayNetworking.Common.Results;
using RelayNetworking.Common.Rooms;
using RelayNetworking.Common.Serialization;
using RelayNetworking.Server.Transport;
using System.Numerics;

namespace RelayNetworking.Server.Protocol.Events;

internal sealed class EventManager
{
    private readonly RelayServer _server;

    public EventManager(RelayServer server)
    {
        _server = server;
    }

    public void Connected(RelayConnection connection)
    {
        NetworkCommandWriter writer = new();

        writer.WriteHeader(EventCode.Connected);

        _server.Send(connection.Peer, writer);
    }

    public void Disconnected(RelayConnection connection)
    {
        NetworkCommandWriter writer = new();

        writer.WriteHeader(EventCode.Disconnected);
        _server.Send(connection.Peer, writer);
    }

    public void Ping(RelayConnection connection, long time)
    {
        Console.WriteLine($"[Client] ping {connection.Peer.Id}");
        NetworkCommandWriter writer = new();

        writer.WriteHeader(EventCode.Ping);
        writer.Put(time);

        _server.Send(connection.Peer, writer);
    }

    public void RoomCreated(RelayConnection connection, Room room)
    {
        NetworkCommandWriter writer = new();

        writer.WriteHeader(EventCode.RoomCreated);
        writer.Put(room.Name);

        _server.Send(connection.Peer, writer);
    }

    public void RoomCreationFailed(RelayConnection connection, string reason)
    {
        NetworkCommandWriter writer = new();

        writer.WriteHeader(EventCode.RoomCreationFailed);
        writer.Put(reason);

        _server.Send(connection.Peer, writer);
    }

    public void RoomJoined(RelayConnection connection, Room room)
    {
        NetworkCommandWriter writer = new();

        writer.WriteHeader(EventCode.RoomJoined);
        writer.Put(room.Name);

        _server.Send(connection.Peer, writer);
    }

    public void RoomLeft(RelayConnection connection)
    {
        NetworkCommandWriter writer = new();

        writer.WriteHeader(EventCode.RoomLeft);
        _server.Send(connection.Peer, writer);
    }

    public void RoomJoinFailed(RelayConnection connection, JoinRoomResult result)
    {
        NetworkCommandWriter writer = new();

        writer.WriteHeader(EventCode.RoomJoinFailed);
        writer.Put((byte)result);

        _server.Send(connection.Peer, writer);
    }

    public void Authenticated(RelayConnection connection)
    {
        if (connection.Player == null)
            return;

        NetworkCommandWriter writer = new();

        writer.WriteHeader(EventCode.Authenticated);

        writer.Put(connection.Player.PlayerId);
        writer.Put(connection.Player.UserId);
        writer.Put(connection.Player.Username);
        writer.Put(connection.Player.IsLeader);
        writer.Put(connection.ReclamationToken);
        writer.Put(NetworkProtocol.CurrentVersion);

        _server.Send(connection.Peer, writer);
    }

    public void PlayerSync(RelayConnection target, RelayPlayer player)
    {
        NetworkCommandWriter writer = new();

        writer.WriteHeader(EventCode.PlayerSync);

        writer.Put(player.PlayerId);
        writer.Put(player.UserId);
        writer.Put(player.Username);
        writer.Put(player.IsLeader);

        _server.Send(target.Peer, writer);
        Console.WriteLine($"[Server] To {target.Player?.Username} -> {player.Username} Leader={player.IsLeader}");
    }

    public void PlayerJoined(RelayConnection target, RelayPlayer player)
    {
        NetworkCommandWriter writer = new();

        writer.WriteHeader(EventCode.PlayerJoined);

        writer.Put(player.PlayerId);
        writer.Put(player.UserId);
        writer.Put(player.Username);
        writer.Put(player.IsLeader);

        _server.Send(target.Peer, writer);
    }

    public void LeaderChanged(RelayConnection connection, RelayPlayer player)
    {
        NetworkCommandWriter writer = new();

        writer.WriteHeader(EventCode.LeaderChanged);
        writer.Put(player.PlayerId);

        _server.Send(connection.Peer, writer);
    }

    public void PlayerProperties(RelayConnection connection, RelayPlayer player, PropertyChanges changes)
    {
        NetworkCommandWriter writer = new();

        writer.WriteHeader(EventCode.PlayerProperties);

        writer.Put(player.PlayerId);

        PropertySerializer.WriteReserved(writer, changes);
        PropertySerializer.WriteCustom(writer, changes);

        _server.Send(connection.Peer, writer);
    }

    public void RoomProperties(RelayConnection connection, Room room, PropertyChanges changes)
    {
        NetworkCommandWriter writer = new();

        writer.WriteHeader(EventCode.RoomProperties);

        PropertySerializer.WriteReserved(writer, changes);
        PropertySerializer.WriteCustom(writer, changes);

        _server.Send(connection.Peer, writer);
    }

    public void PlayerLeft(RelayConnection connection, int playerId)
    {
        NetworkCommandWriter writer = new();

        writer.WriteHeader(EventCode.PlayerLeft);

        writer.Put(playerId);

        _server.Send(connection.Peer, writer);
    }

    public void EventReceived(RelayConnection connection, int senderPlayerId, byte eventCode, byte[] payload, ulong sentTick)
    {
        NetworkCommandWriter writer = new();

        writer.WriteHeader(EventCode.EventReceived);

        writer.Put(senderPlayerId);
        writer.Put(eventCode);
        writer.Put(payload);
        writer.Put(sentTick);

        _server.Send(connection.Peer, writer);
    }


    public void ObjectSpawned(
        RelayConnection connection,
        ushort prefabId,
        uint networkId,
        int ownerPlayerId,
        bool isPlayer,
        float x,
        float y,
        float z,
        float rx,
        float ry,
        float rz,
        float rw,
        uint requestId = 0)
    {
        NetworkCommandWriter writer = new();

        writer.WriteHeader(EventCode.Spawn);

        writer.Put(prefabId);
        writer.Put(networkId);
        writer.Put(ownerPlayerId);
        writer.Put(isPlayer);

        writer.Put(x);
        writer.Put(y);
        writer.Put(z);

        writer.Put(rx);
        writer.Put(ry);
        writer.Put(rz);
        writer.Put(rw);
        writer.Put(requestId);

        _server.Send(connection.Peer, writer);
    }

    public void SpawnFailed(RelayConnection connection, uint requestId)
    {
        NetworkCommandWriter writer = new();

        writer.WriteHeader(EventCode.SpawnFailed);
        writer.Put(requestId);

        _server.Send(connection.Peer, writer);
    }

    internal void ObjectDestroyed(
    RelayConnection connection,
    uint networkId)
    {
        NetworkCommandWriter writer = new();

        writer.WriteHeader(EventCode.Destroy);

        writer.Put(networkId);

        _server.Send(connection.Peer, writer);
    }

    internal void ObjectHidden(RelayConnection connection, uint networkId)
    {
        NetworkCommandWriter writer = new();

        writer.WriteHeader(EventCode.ObjectHidden);

        writer.Put(networkId);

        _server.Send(connection.Peer, writer);
    }

    public void ObjectTransform(
RelayConnection connection,
uint networkId,
float x,
float y,
float z,
float rx,
float ry,
float rz,
float rw,
float vx,
float vy,
float vz,
float avx,
float avy,
float avz)
    {
        NetworkCommandWriter writer = new();

        writer.WriteHeader(EventCode.ObjectTransform);

        writer.Put(networkId);

        writer.Put(x);
        writer.Put(y);
        writer.Put(z);

        writer.Put(rx);
        writer.Put(ry);
        writer.Put(rz);
        writer.Put(rw);

        writer.Put(vx);
        writer.Put(vy);
        writer.Put(vz);

        writer.Put(avx);
        writer.Put(avy);
        writer.Put(avz);

        _server.Send(connection.Peer, writer);
    }

    public void NetworkVariable(
    RelayConnection connection,
    uint networkId,
    ushort variableId,
    NetworkVariableType type,
    object value,
    ushort behaviourId = 0)
    {
        NetworkCommandWriter writer = new();

        writer.WriteHeader(EventCode.NetworkVariable);

        writer.Put(networkId);
        writer.Put(variableId);
        writer.Put((byte)type);

        NetworkVariableSerializer.Write(writer, type, value);
        writer.Put(behaviourId);

        _server.Send(connection.Peer, writer);
    }

    public void NetworkCollection(RelayConnection connection, NetworkCollectionData data)
    {
        if (connection.ProtocolVersion < 2)
            return;

        NetworkCommandWriter writer = new();

        writer.WriteHeader(EventCode.NetworkCollection);

        writer.Put(data.NetworkId);
        writer.Put(data.BehaviourId);
        writer.Put(data.CollectionId);
        writer.Put((byte)data.CollectionType);
        writer.Put((byte)data.Operation);
        writer.Put(data.RequestId);
        writer.Put((byte)data.KeyType);
        writer.Put((byte)data.ValueType);

        if (data.Operation == NetworkCollectionOperation.Snapshot)
        {
            writer.Put((ushort)data.Entries.Length);

            foreach (NetworkCollectionEntry entry in data.Entries)
            {
                if (data.CollectionType == NetworkCollectionType.Dictionary)
                    NetworkVariableSerializer.Write(writer, data.KeyType, entry.Key);

                NetworkVariableSerializer.Write(writer, data.ValueType, entry.Value);
            }
        }
        else
        {
            switch (data.CollectionType)
            {
                case NetworkCollectionType.List:
                    switch (data.Operation)
                    {
                        case NetworkCollectionOperation.Add:
                        case NetworkCollectionOperation.Insert:
                        case NetworkCollectionOperation.Set:
                            writer.Put(data.Index);
                            NetworkVariableSerializer.Write(writer, data.ValueType, data.Value);
                            break;

                        case NetworkCollectionOperation.RemoveAt:
                            writer.Put(data.Index);
                            break;
                    }
                    break;

                case NetworkCollectionType.Dictionary:
                    switch (data.Operation)
                    {
                        case NetworkCollectionOperation.Add:
                        case NetworkCollectionOperation.Set:
                            NetworkVariableSerializer.Write(writer, data.KeyType, data.Key);
                            NetworkVariableSerializer.Write(writer, data.ValueType, data.Value);
                            break;

                        case NetworkCollectionOperation.Remove:
                            NetworkVariableSerializer.Write(writer, data.KeyType, data.Key);
                            break;
                    }
                    break;

                case NetworkCollectionType.Set:
                    if (data.Operation == NetworkCollectionOperation.Add ||
                        data.Operation == NetworkCollectionOperation.Remove)
                    {
                        NetworkVariableSerializer.Write(writer, data.ValueType, data.Value);
                    }
                    break;
            }
        }

        _server.Send(connection.Peer, writer);
    }

    public void Rpc(
        RelayConnection connection,
        RpcData rpc)
    {
        NetworkCommandWriter writer = new();

        writer.WriteHeader(EventCode.Rpc);

        RpcSerializer.WriteEvent(writer, rpc);

        _server.Send(
            connection.Peer,
            writer,
            rpc.Delivery);
    }

    public void OwnershipChanged(RelayConnection connection, uint networkId, int ownerPlayerId)
    {
        NetworkCommandWriter writer = new();

        writer.WriteHeader(EventCode.OwnershipChanged);

        writer.Put(networkId);
        writer.Put(ownerPlayerId);

        _server.Send(connection.Peer, writer);
    }


    public void OwnershipReclaimed(RelayConnection connection)
    {
        NetworkCommandWriter writer = new();

        writer.WriteHeader(EventCode.OwnershipReclaimed);

        _server.Send(connection.Peer, writer);
    }

    public void DropConnection(RelayConnection connection)
    {
        NetworkCommandWriter writer = new();

        writer.WriteHeader(EventCode.DropConnection);

        _server.Send(connection.Peer, writer);
    }

    public void SceneLoad(RelayConnection connection, string sceneName)
    {
        NetworkCommandWriter writer = new();

        writer.WriteHeader(EventCode.SceneLoad);
        writer.Put(sceneName);

        _server.Send(connection.Peer, writer);
    }

    public void ChildTransform(RelayConnection connection, uint networkId, byte[] indices, Vector3[] positions, Quaternion[] rotations)
    {
        NetworkCommandWriter writer = new();

        writer.WriteHeader(EventCode.ChildTransform);

        writer.Put(networkId);
        writer.Put((byte)indices.Length);

        for (int i = 0; i < indices.Length; i++)
        {
            writer.Put(indices[i]);

            writer.Put(positions[i].X);
            writer.Put(positions[i].Y);
            writer.Put(positions[i].Z);

            writer.Put(rotations[i].X);
            writer.Put(rotations[i].Y);
            writer.Put(rotations[i].Z);
            writer.Put(rotations[i].W);
        }

        _server.Send(connection.Peer, writer);
    }

    public void TimeSync(RelayConnection connection, ulong requestId, double serverTime, ulong currentTick, int tickRate)
    {
        NetworkCommandWriter writer = new();

        writer.WriteHeader(EventCode.TimeSync);

        writer.Put(requestId);
        writer.Put(serverTime);
        writer.Put(currentTick);
        writer.Put(tickRate);

        _server.Send(connection.Peer, writer, LiteNetLib.DeliveryMethod.Unreliable);
    }
}