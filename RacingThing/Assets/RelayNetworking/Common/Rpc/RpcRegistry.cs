using System;
using System.Collections.Generic;
using LiteNetLib;

namespace RelayNetworking.Common.Rpc
{
    public sealed class RpcMetadata
    {
        public string Name { get; init; } = string.Empty;
        public ushort BehaviourId { get; init; }
        public ushort MethodId { get; init; }
        public RpcCaller Caller { get; init; }
        public DeliveryMethod Delivery { get; init; }
    }

    public static class RpcRegistry
    {
        private static readonly Dictionary<string, RpcMetadata> _byName = new(StringComparer.Ordinal);
        private static readonly Dictionary<uint, RpcMetadata> _byId = new();
        private static readonly Dictionary<(Type DeclaringType, string Name), RpcMetadata> _byType = new();
        private static readonly object _lock = new();

        private static uint Key(ushort behaviourId, ushort methodId)
        {
            return ((uint)behaviourId << 16) | methodId;
        }

        public static void Register(string name, ushort behaviourId, ushort methodId, RpcCaller caller, DeliveryMethod delivery, Type? declaringType = null)
        {
            RpcMetadata metadata = new() { Name = name, BehaviourId = behaviourId, MethodId = methodId, Caller = caller, Delivery = delivery };
            lock (_lock)
            {
                _byName[name] = metadata;
                _byId[Key(behaviourId, methodId)] = metadata;
                if (declaringType != null)
                {
                    _byType[(declaringType, name)] = metadata;
                }
            }
        }

        public static bool TryGetByName(string name, out RpcMetadata? metadata)
        {
            lock (_lock)
            {
                return _byName.TryGetValue(name, out metadata);
            }
        }

        public static bool TryGetByIds(ushort behaviourId, ushort methodId, out RpcMetadata? metadata)
        {
            lock (_lock)
            {
                return _byId.TryGetValue(Key(behaviourId, methodId), out metadata);
            }
        }

        public static bool TryGetByType(Type declaringType, string name, out RpcMetadata? metadata)
        {
            lock (_lock)
            {
                return _byType.TryGetValue((declaringType, name), out metadata);
            }
        }

        public static void Clear()
        {
            lock (_lock)
            {
                _byName.Clear();
                _byId.Clear();
                _byType.Clear();
            }
        }
    }
}