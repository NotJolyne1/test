using RelayNetworking.Common.Protocol.Networking;
using RelayNetworking.Common.Serialization;

namespace RelayNetworking.Common.Protocol
{
    public abstract class NetworkCommand : INetworkCommand
    {
        public abstract CommandCode Code { get; }

        public abstract void Serialize(NetworkCommandWriter writer);

        public abstract void Deserialize(NetworkCommandReader reader);
    }
}