using RelayNetworking.Client;
using RelayNetworking.Common;
using RelayNetworking.Common.Data;
using RelayNetworking.Common.Rpc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace RelayNetworking.Unity
{
    public class NetworkBehaviour : MonoBehaviour
    {
        internal NetworkObject NetworkObject { get; private set; }

        internal ushort BehaviourId { get; private set; }

        private readonly List<NetworkVariableInfo> _variables = new();

        private static readonly Dictionary<Type, List<RpcMethodInfo>> _rpcCache = new();

        private readonly Dictionary<ushort, RpcMethodInfo> _rpcMethods = new();

        private List<RpcMethodInfo> _rpcMethodList = new();

        internal IReadOnlyList<RpcMethodInfo> RpcMethods => _rpcMethodList;

        protected virtual void Awake()
        {
            NetworkObject = GetComponent<NetworkObject>();

            if (NetworkObject != null)
                BehaviourId = NetworkObject.Register(this);

            CacheVariables();
            CacheRpcs();
        }

        protected virtual void OnEnable()
        {
            RelayNetwork.OnNetworkVariable += OnNetworkVariable;
            RelayNetwork.OnRpc += OnRpc;
        }

        protected virtual void OnDisable()
        {
            RelayNetwork.OnNetworkVariable -= OnNetworkVariable;
            RelayNetwork.OnRpc -= OnRpc;
        }

        protected virtual void Update()
        {
            if (NetworkObject == null)
                return;

            foreach (NetworkVariableInfo variable in _variables)
            {
                switch (variable.Authority)
                {
                    case NetworkVariableAuthority.Owner:
                        if (!NetworkObject.HasControl)
                            continue;
                        break;

                    case NetworkVariableAuthority.Leader:
                        if (!RelayNetwork.LocalPlayer.IsLeader)
                            continue;
                        break;

                    case NetworkVariableAuthority.Server:
                        continue;

                    case NetworkVariableAuthority.Everyone:
                        break;
                }

                object value = variable.Field.GetValue(this);

                if (Equals(value, variable.LastValue))
                    continue;

                variable.LastValue = value;

                RelayNetwork.SetNetworkVariable(
                    NetworkObject.NetworkId,
                    variable.Id,
                    variable.Type,
                    value);
            }
        }

        private void CacheVariables()
        {
            FieldInfo[] fields = GetType().GetFields(
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic);

            ushort id = 0;

            foreach (FieldInfo field in fields)
            {
                NetworkVariableAttribute attribute =
                    field.GetCustomAttribute<NetworkVariableAttribute>();

                if (attribute == null)
                    continue;

                NetworkVariableType type;

                if (!TryGetType(field.FieldType, out type))
                    throw new Exception(field.Name + " isn't supported.");

                _variables.Add(new NetworkVariableInfo
                {
                    Id = id++,
                    Field = field,
                    Type = type,
                    Authority = attribute.Authority,
                    LastValue = field.GetValue(this)
                });
            }
        }

        private void CacheRpcs()
        {
            Type type = GetType();

            if (!_rpcCache.TryGetValue(type, out List<RpcMethodInfo> methods))
            {
                methods = BuildRpcMethods(type);
                _rpcCache[type] = methods;
            }

            _rpcMethodList = methods;

            foreach (RpcMethodInfo method in methods)
            {
                _rpcMethods[method.MethodId] = method;

                RpcRegistry.Register(
                    method.Name,
                    BehaviourId,
                    method.MethodId,
                    method.Caller,
                    method.Delivery,
                    method.Method.DeclaringType);
            }
        }

        private static List<RpcMethodInfo> BuildRpcMethods(Type type)
        {
            MethodInfo[] methods = type.GetMethods(
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic);

            List<RpcMethodInfo> result = new();

            ushort id = 0;

            foreach (MethodInfo method in methods
                .Where(m => m.GetCustomAttribute<RelayRpcAttribute>() != null)
                .OrderBy(m => m.Name, StringComparer.Ordinal))
            {
                RelayRpcAttribute attribute = method.GetCustomAttribute<RelayRpcAttribute>();

                ParameterInfo[] parameters = method.GetParameters();

                int infoIndex = Array.FindIndex(
                    parameters,
                    p => p.ParameterType == typeof(RpcInfo));

                int secondInfoIndex = infoIndex < 0
                    ? -1
                    : Array.FindIndex(parameters, infoIndex + 1, p => p.ParameterType == typeof(RpcInfo));

                if (secondInfoIndex >= 0)
                {
                    throw new Exception(
                        $"{type.Name}.{method.Name} has more than one RpcInfo parameter.");
                }

                result.Add(new RpcMethodInfo
                {
                    MethodId = id++,
                    Name = method.Name,
                    Method = method,
                    Parameters = parameters,
                    Caller = attribute.Caller,
                    Delivery = attribute.Delivery,
                    InfoParameterIndex = infoIndex
                });
            }

            return result;
        }

        private void OnNetworkVariable(
            uint networkId,
            ushort variableId,
            object value)
        {
            if (NetworkObject == null)
                return;

            if (networkId != NetworkObject.NetworkId)
                return;

            foreach (NetworkVariableInfo variable in _variables)
            {
                if (variable.Id != variableId)
                    continue;

                variable.LastValue = value;
                variable.Field.SetValue(this, value);
                break;
            }
        }

        private void OnRpc(RpcData rpc)
        {
            if (NetworkObject == null)
                return;

            if (rpc.NetworkId != NetworkObject.NetworkId)
                return;

            if (rpc.BehaviourId != BehaviourId)
                return;

            if (!_rpcMethods.TryGetValue(rpc.MethodId, out RpcMethodInfo method))
                return;

            object[] arguments = new object[method.Parameters.Length];

            int wireIndex = 0;

            for (int i = 0; i < method.Parameters.Length; i++)
            {
                if (i == method.InfoParameterIndex)
                {
                    arguments[i] = BuildRpcInfo(rpc);
                    continue;
                }

                object raw = wireIndex < rpc.Parameters.Length ? rpc.Parameters[wireIndex] : null;
                wireIndex++;

                arguments[i] = RpcParameterConverter.Convert(
                    raw,
                    method.Parameters[i].ParameterType);
            }

            method.Method.Invoke(this, arguments);
        }

        private static RpcInfo BuildRpcInfo(RpcData rpc)
        {
            RelayPlayer sender = RelayNetwork.GetPlayer(rpc.SenderPlayerId);

            return new RpcInfo(rpc.SenderPlayerId, sender, rpc.IsLeader, rpc.Tick);
        }

        protected void SendRpc(string methodName, RpcTarget target, params object[] parameters)
        {
            if (NetworkObject == null)
            {
                Debug.LogWarning($"[NetworkBehaviour] Cannot send RPC '{methodName}': no NetworkObject.");
                return;
            }

            if (NetworkObject.NetworkId == 0)
            {
                Debug.LogWarning($"[NetworkBehaviour] Cannot send RPC '{methodName}': NetworkObject not spawned yet.");
                return;
            }

            RelayRpc.Send(NetworkObject.NetworkId, methodName, target, parameters);
        }

        protected void SendRpc(string methodName, RelayPlayer targetPlayer, params object[] parameters)
        {
            if (NetworkObject == null)
            {
                Debug.LogWarning($"[NetworkBehaviour] Cannot send RPC '{methodName}': no NetworkObject.");
                return;
            }

            if (NetworkObject.NetworkId == 0)
            {
                Debug.LogWarning($"[NetworkBehaviour] Cannot send RPC '{methodName}': NetworkObject not spawned yet.");
                return;
            }

            RelayRpc.Send(NetworkObject.NetworkId, methodName, targetPlayer, parameters);
        }

        internal IReadOnlyList<NetworkVariableInfo> Variables => _variables;

        private static bool TryGetType(Type type, out NetworkVariableType result)
        {
            if (type == typeof(bool))
            {
                result = NetworkVariableType.Bool;
                return true;
            }

            if (type == typeof(byte))
            {
                result = NetworkVariableType.Byte;
                return true;
            }

            if (type == typeof(short))
            {
                result = NetworkVariableType.Short;
                return true;
            }

            if (type == typeof(ushort))
            {
                result = NetworkVariableType.UShort;
                return true;
            }

            if (type == typeof(int))
            {
                result = NetworkVariableType.Int;
                return true;
            }

            if (type == typeof(uint))
            {
                result = NetworkVariableType.UInt;
                return true;
            }

            if (type == typeof(long))
            {
                result = NetworkVariableType.Long;
                return true;
            }

            if (type == typeof(ulong))
            {
                result = NetworkVariableType.ULong;
                return true;
            }

            if (type == typeof(float))
            {
                result = NetworkVariableType.Float;
                return true;
            }

            if (type == typeof(double))
            {
                result = NetworkVariableType.Double;
                return true;
            }

            if (type == typeof(string))
            {
                result = NetworkVariableType.String;
                return true;
            }

            if (type == typeof(Vector2))
            {
                result = NetworkVariableType.Vector2;
                return true;
            }

            if (type == typeof(Vector3))
            {
                result = NetworkVariableType.Vector3;
                return true;
            }

            if (type == typeof(Quaternion))
            {
                result = NetworkVariableType.Quaternion;
                return true;
            }

            result = default;
            return false;
        }
    }
}