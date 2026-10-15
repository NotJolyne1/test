using System;

namespace RelayNetworking.Unity
{
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class NetworkVariableAttribute : Attribute
    {
        public NetworkVariableAuthority Authority { get; }

        public NetworkVariableAttribute(
            NetworkVariableAuthority authority = NetworkVariableAuthority.Owner)
        {
            Authority = authority;
        }
    }
}