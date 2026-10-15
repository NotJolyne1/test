using System;
using LiteNetLib;
using RelayNetworking.Common.Rpc;

namespace RelayNetworking.Common.Data
{
    public sealed class RpcData
    {
        public uint NetworkId;
        public ushort BehaviourId;
        public ushort MethodId;
        public RpcTarget Target;
        public bool HasSpecificTarget;
        public int SpecificTargetPlayerId;
        public DeliveryMethod Delivery;
        public object[] Parameters = Array.Empty<object>();
        public int SenderPlayerId;
        public bool IsLeader;
        public ulong Tick;
    }
}