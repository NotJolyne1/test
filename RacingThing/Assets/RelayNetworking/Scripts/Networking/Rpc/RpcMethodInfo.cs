using RelayNetworking.Common.Rpc;
using System.Reflection;
using LiteNetLib;

namespace RelayNetworking.Unity
{
    internal sealed class RpcMethodInfo
    {
        public ushort MethodId;

        public string Name;

        public MethodInfo Method;

        public ParameterInfo[] Parameters;

        public RpcCaller Caller;

        public DeliveryMethod Delivery;


        public int InfoParameterIndex = -1;
    }
}