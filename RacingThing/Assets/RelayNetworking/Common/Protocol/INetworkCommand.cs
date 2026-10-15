using RelayNetworking.Common.Protocol.Networking;
using RelayNetworking.Common.Serialization;

namespace RelayNetworking.Common.Protocol
{
    public interface INetworkCommand
    {
        CommandCode Code { get; }
        void Serialize(NetworkCommandWriter writer);
        void Deserialize(NetworkCommandReader reader);
    }
}