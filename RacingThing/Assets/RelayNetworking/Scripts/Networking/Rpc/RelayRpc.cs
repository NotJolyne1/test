using RelayNetworking.Client;
using RelayNetworking.Common;
using RelayNetworking.Common.Rpc;
using System.Collections.Generic;
using System.Linq;

namespace RelayNetworking.Unity
{
    public static class RelayRpc
    {
        public static void Send(string methodName, RpcTarget target, params object[] parameters)
        {
            RelayNetwork.SendRpc(methodName, target, Convert(parameters));
        }

        public static void Send(string methodName, RelayPlayer targetPlayer, params object[] parameters)
        {
            RelayNetwork.SendRpc(methodName, targetPlayer, Convert(parameters));
        }

        public static void Send(uint networkId, string methodName, RpcTarget target, params object[] parameters)
        {
            RelayNetwork.SendRpc(networkId, methodName, target, Convert(parameters));
        }

        public static void Send(uint networkId, string methodName, RelayPlayer targetPlayer, params object[] parameters)
        {
            RelayNetwork.SendRpc(networkId, methodName, targetPlayer, Convert(parameters));
        }

        private static object[] Convert(object[] parameters)
        {
            if (parameters == null || parameters.Length == 0)
                return parameters;

            List<object> result = new List<object>(parameters.Length);

            foreach (object parameter in parameters)
            {
                if (parameter is RpcInfo)
                    continue;

                result.Add(RpcParameterConverter.ToWireValue(parameter));
            }

            return result.ToArray();
        }
    }
}