using RelayNetworking.Common.Data;
using RelayNetworking.Common.Rpc;

namespace RelayNetworking.Common.Serialization
{
    internal static class RpcSerializer
    {
        public static void WriteRequest(NetworkCommandWriter writer, RpcData rpc)
        {
            WriteCore(writer, rpc);
        }

        public static RpcData ReadRequest(NetworkCommandReader reader)
        {
            return ReadCore(reader);
        }

        public static void WriteEvent(NetworkCommandWriter writer, RpcData rpc)
        {
            WriteCore(writer, rpc);
            writer.Put(rpc.SenderPlayerId);
            writer.Put(rpc.IsLeader);
            writer.Put(rpc.Tick);
        }

        public static RpcData ReadEvent(NetworkCommandReader reader)
        {
            RpcData rpc = ReadCore(reader);
            rpc.SenderPlayerId = reader.GetInt();
            rpc.IsLeader = reader.GetBool();
            rpc.Tick = reader.GetULong();
            return rpc;
        }

        private static void WriteCore(NetworkCommandWriter writer, RpcData rpc)
        {
            writer.Put(rpc.NetworkId);
            writer.Put(rpc.BehaviourId);
            writer.Put(rpc.MethodId);
            writer.Put((byte)rpc.Target);
            writer.Put(rpc.HasSpecificTarget);
            if (rpc.HasSpecificTarget)
                writer.Put(rpc.SpecificTargetPlayerId);
            writer.Put((byte)rpc.Parameters.Length);
            foreach (object parameter in rpc.Parameters)
            {
                NetworkVariableType type = NetworkVariableSerializer.GetType(parameter);
                writer.Put((byte)type);
                NetworkVariableSerializer.Write(writer, type, parameter);
            }
        }

        private static RpcData ReadCore(NetworkCommandReader reader)
        {
            RpcData rpc = new();
            rpc.NetworkId = reader.GetUInt();
            rpc.BehaviourId = reader.GetUShort();
            rpc.MethodId = reader.GetUShort();
            rpc.Target = (RpcTarget)reader.GetByte();
            rpc.HasSpecificTarget = reader.GetBool();
            if (rpc.HasSpecificTarget)
                rpc.SpecificTargetPlayerId = reader.GetInt();
            int count = reader.GetByte();
            rpc.Parameters = new object[count];
            for (int i = 0; i < count; i++)
            {
                NetworkVariableType type = (NetworkVariableType)reader.GetByte();
                rpc.Parameters[i] = NetworkVariableSerializer.Read(reader, type);
            }
            return rpc;
        }
    }
}