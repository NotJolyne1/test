using System.Numerics;

public readonly struct SpawnData
{
    public readonly ushort PrefabId;
    public readonly uint NetworkId;
    public readonly int OwnerPlayerId;
    public readonly bool IsPlayer;
    public readonly Vector3 Position;
    public readonly Quaternion Rotation;
    public readonly uint RequestId;

    public SpawnData(
        ushort prefabId,
        uint networkId,
        int ownerPlayerId,
        bool isPlayer,
        Vector3 position,
        Quaternion rotation,
        uint requestId = 0)
    {
        PrefabId = prefabId;
        NetworkId = networkId;
        OwnerPlayerId = ownerPlayerId;
        IsPlayer = isPlayer;
        Position = position;
        Rotation = rotation;
        RequestId = requestId;
    }
}