using System;

namespace RelayNetworking.Common.Data
{
    public enum NetworkCollectionType : byte
    {
        List,
        Dictionary,
        Set
    }

    public enum NetworkCollectionOperation : byte
    {
        Snapshot,
        Add,
        Insert,
        Set,
        Remove,
        RemoveAt,
        Clear
    }

    public readonly struct NetworkCollectionEntry
    {
        public object Key { get; }
        public object Value { get; }

        public NetworkCollectionEntry(object key, object value)
        {
            Key = key;
            Value = value;
        }
    }

    public sealed class NetworkCollectionRegistration
    {
        public ushort Id;
        public NetworkCollectionType CollectionType;
        public NetworkVariableAuthority Authority;
        public NetworkVariableType KeyType;
        public NetworkVariableType ValueType;
        public NetworkCollectionEntry[] Entries = Array.Empty<NetworkCollectionEntry>();
    }

    public readonly struct NetworkCollectionData
    {
        public uint NetworkId { get; }
        public ushort BehaviourId { get; }
        public ushort CollectionId { get; }
        public NetworkCollectionType CollectionType { get; }
        public NetworkCollectionOperation Operation { get; }
        public uint RequestId { get; }
        public NetworkVariableType KeyType { get; }
        public NetworkVariableType ValueType { get; }
        public int Index { get; }
        public object Key { get; }
        public object Value { get; }
        public NetworkCollectionEntry[] Entries { get; }

        public NetworkCollectionData(
            uint networkId,
            ushort behaviourId,
            ushort collectionId,
            NetworkCollectionType collectionType,
            NetworkCollectionOperation operation,
            uint requestId,
            NetworkVariableType keyType,
            NetworkVariableType valueType,
            int index,
            object key,
            object value,
            NetworkCollectionEntry[] entries)
        {
            NetworkId = networkId;
            BehaviourId = behaviourId;
            CollectionId = collectionId;
            CollectionType = collectionType;
            Operation = operation;
            RequestId = requestId;
            KeyType = keyType;
            ValueType = valueType;
            Index = index;
            Key = key;
            Value = value;
            Entries = entries ?? Array.Empty<NetworkCollectionEntry>();
        }
    }
}
