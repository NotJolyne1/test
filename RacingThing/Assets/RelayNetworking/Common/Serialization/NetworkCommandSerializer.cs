using LiteNetLib.Utils;
using RelayNetworking.Common.Protocol;
using RelayNetworking.Common.Protocol.Networking;

namespace RelayNetworking.Common.Serialization
{
    public static class NetworkCommandSerializer
    {
        public static NetDataWriter Serialize(INetworkCommand command)
        {
            NetworkCommandWriter writer = new();
            writer.Put((ushort)command.Code);
            command.Serialize(writer);
            return writer.Writer;
        }
    }
}