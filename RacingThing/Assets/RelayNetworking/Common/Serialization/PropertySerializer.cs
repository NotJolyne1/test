using System;
using RelayNetworking.Common.Collections;
using RelayNetworking.Common.Protocol.Networking;

namespace RelayNetworking.Common.Serialization
{
    internal static class PropertySerializer
    {
        private const int MaxPropertiesPerPacket = 128;
        private const int MaxKeyLength = 64;
        private const int MaxStringValueLength = 512;

        public static void WriteReserved(NetworkCommandWriter writer, PropertyChanges changes)
        {
            writer.Put((ushort)changes.Reserved.Count);

            foreach (var pair in changes.Reserved)
            {
                writer.Put((byte)pair.Key);

                WriteValue(writer, pair.Value);
            }
        }

        public static void WriteCustom(NetworkCommandWriter writer, PropertyChanges changes)
        {
            writer.Put((ushort)changes.Custom.Count);

            foreach (var pair in changes.Custom)
            {
                writer.Put(pair.Key);

                WriteValue(writer, pair.Value);
            }
        }

        public static void ReadReserved(NetworkCommandReader reader, PropertyChanges changes)
        {
            ushort count = reader.GetUShort();

            if (count > MaxPropertiesPerPacket)
                throw new FormatException($"Reserved property count {count} exceeds limit.");

            for (int i = 0; i < count; i++)
            {
                ReservedPlayerProperty key = (ReservedPlayerProperty)reader.GetByte();

                changes.Reserved[key] = ReadValue(reader);
            }
        }

        public static void ReadCustom(NetworkCommandReader reader, PropertyChanges changes)
        {
            ushort count = reader.GetUShort();

            if (count > MaxPropertiesPerPacket)
                throw new FormatException($"Custom property count {count} exceeds limit.");

            for (int i = 0; i < count; i++)
            {
                string key = reader.GetString();

                if (key.Length == 0 || key.Length > MaxKeyLength)
                    throw new FormatException($"Custom property key length {key.Length} is invalid.");

                changes.Custom[key] = ReadValue(reader);
            }
        }

        private static void WriteValue(NetworkCommandWriter writer, object? value)
        {
            switch (value)
            {
                case null:
                    writer.Put((byte)PropertyType.Null);
                    break;

                case bool v:
                    writer.Put((byte)PropertyType.Bool);
                    writer.Put(v);
                    break;

                case byte v:
                    writer.Put((byte)PropertyType.Byte);
                    writer.Put(v);
                    break;

                case short v:
                    writer.Put((byte)PropertyType.Short);
                    writer.Put(v);
                    break;

                case int v:
                    writer.Put((byte)PropertyType.Int);
                    writer.Put(v);
                    break;

                case long v:
                    writer.Put((byte)PropertyType.Long);
                    writer.Put(v);
                    break;

                case float v:
                    writer.Put((byte)PropertyType.Float);
                    writer.Put(v);
                    break;

                case double v:
                    writer.Put((byte)PropertyType.Double);
                    writer.Put(v);
                    break;

                case string v:
                    writer.Put((byte)PropertyType.String);
                    writer.Put(v);
                    break;

                default:
                    throw new NotSupportedException(value.GetType().FullName);
            }
        }

        private static object? ReadValue(NetworkCommandReader reader)
        {
            PropertyType type = (PropertyType)reader.GetByte();

            return type switch
            {
                PropertyType.Null => null,
                PropertyType.Bool => reader.GetBool(),
                PropertyType.Byte => reader.GetByte(),
                PropertyType.Short => reader.GetShort(),
                PropertyType.Int => reader.GetInt(),
                PropertyType.Long => reader.GetLong(),
                PropertyType.Float => reader.GetFloat(),
                PropertyType.Double => reader.GetDouble(),
                PropertyType.String => ReadBoundedString(reader),
                _ => throw new NotSupportedException(type.ToString())
            };
        }

        private static string ReadBoundedString(NetworkCommandReader reader)
        {
            string value = reader.GetString();

            if (value.Length > MaxStringValueLength)
                throw new FormatException($"Property string value length {value.Length} exceeds limit.");

            return value;
        }
    }
}