using RelayNetworking.Common.Data;
using System.Reflection;

namespace RelayNetworking.Unity
{
    internal sealed class NetworkVariableInfo
    {
        public ushort Id;

        public NetworkVariableType Type;

        public FieldInfo Field;

        public NetworkVariableAuthority Authority;

        public object LastValue;
    }
}