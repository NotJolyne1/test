using System;
using LiteNetLib.Utils;
using RelayNetworking.Common.Protocol.Networking;

namespace RelayNetworking.Common.Serialization
{
    public sealed class NetworkCommandWriter
    {
        public NetDataWriter Writer { get; } = new();
        public void Reset() => Writer.Reset();
        public void Put(bool value) => Writer.Put(value);
        public void Put(byte value) => Writer.Put(value);
        public void Put(short value) => Writer.Put(value);
        public void Put(ushort value) => Writer.Put(value);
        public void Put(int value) => Writer.Put(value);
        public void Put(uint value) => Writer.Put(value);
        public void Put(long value) => Writer.Put(value);
        public void Put(ulong value) => Writer.Put(value);
        public void Put(float value) => Writer.Put(value);
        public void Put(double value) => Writer.Put(value);
        public void Put(string value) => Writer.Put(value);
        public void Put(byte[] value) => Writer.PutBytesWithLength(value);

        public void Put(Guid value)
        {
            Writer.Put(value.ToByteArray());
        }

        public void WriteHeader(CommandCode code)
        {
            Put(PacketHeader.CurrentVersion);
            Put((ushort)code);
            Put((byte)0);
        }

        public void WriteHeader(EventCode code)
        {
            Put(PacketHeader.CurrentVersion);
            Put((ushort)code);
            Put((byte)0);
        }
    }
}