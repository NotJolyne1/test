using System;
using LiteNetLib;

namespace RelayNetworking.Common.Rpc
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class RelayRpcAttribute : Attribute
    {
        public RpcCaller Caller { get; }
        public DeliveryMethod Delivery { get; }
        public RelayRpcAttribute()
        {
            Caller = RpcCaller.Anyone;
            Delivery = DeliveryMethod.ReliableOrdered;
        }
        public RelayRpcAttribute(RpcCaller caller)
        {
            Caller = caller;
            Delivery = DeliveryMethod.ReliableOrdered;
        }
        public RelayRpcAttribute(DeliveryMethod delivery)
        {
            Caller = RpcCaller.Anyone;
            Delivery = delivery;
        }
        public RelayRpcAttribute(RpcCaller caller, DeliveryMethod delivery)
        {
            Caller = caller;
            Delivery = delivery;
        }
    }
}