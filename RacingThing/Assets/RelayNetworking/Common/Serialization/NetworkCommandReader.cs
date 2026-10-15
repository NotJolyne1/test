using System;
using LiteNetLib.Utils;
using RelayNetworking.Common.Protocol.Networking;

namespace RelayNetworking.Common.Serialization
{
    public sealed class NetworkCommandReader
    {
        private readonly NetDataReader _reader;

        public NetworkCommandReader(NetDataReader reader)
        {
            _reader = reader;
        }

        public bool GetBool() => _reader.GetBool();
        public byte GetByte() => _reader.GetByte();
        public short GetShort() => _reader.GetShort();
        public ushort GetUShort() => _reader.GetUShort();
        public int GetInt() => _reader.GetInt();
        public uint GetUInt() => _reader.GetUInt();
        public long GetLong() => _reader.GetLong();
        public ulong GetULong() => _reader.GetULong();
        public float GetFloat() => _reader.GetFloat();
        public double GetDouble() => _reader.GetDouble();
        public string GetString() => _reader.GetString();
        public byte[] GetBytes() => _reader.GetBytesWithLength();
        public int AvailableBytes => _reader.AvailableBytes;

        public Guid GetGuid()
        {
            return new Guid(GetBytes());
        }

        public PacketHeader ReadHeader()
        {
            return new PacketHeader { Version = GetByte(), Code = GetUShort(), Flags = GetByte() };
        }

        public byte[] GetBytes(int length)
        {
            byte[] data = new byte[length];
            _reader.GetBytes(data, length);
            return data;
        }
    }
}