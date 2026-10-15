using System;
using RelayNetworking.Common.Data;
using System.Numerics;

namespace RelayNetworking.Common.Serialization
{
    public static class NetworkVariableSerializer
    {
        public static void Write(NetworkCommandWriter writer, NetworkVariableType type, object value)
        {
            switch (type)
            {
                case NetworkVariableType.Bool:
                    writer.Put((bool)value);
                    break;

                case NetworkVariableType.Byte:
                    writer.Put((byte)value);
                    break;

                case NetworkVariableType.Short:
                    writer.Put((short)value);
                    break;

                case NetworkVariableType.UShort:
                    writer.Put((ushort)value);
                    break;

                case NetworkVariableType.Int:
                    writer.Put((int)value);
                    break;

                case NetworkVariableType.UInt:
                    writer.Put((uint)value);
                    break;

                case NetworkVariableType.Long:
                    writer.Put((long)value);
                    break;

                case NetworkVariableType.ULong:
                    writer.Put((ulong)value);
                    break;

                case NetworkVariableType.Float:
                    writer.Put((float)value);
                    break;

                case NetworkVariableType.Double:
                    writer.Put((double)value);
                    break;

                case NetworkVariableType.String:
                    writer.Put((string)value);
                    break;

                case NetworkVariableType.Vector2:
                    {
                        Vector2 v = (Vector2)value;
                        writer.Put(v.X);
                        writer.Put(v.Y);
                        break;
                    }

                case NetworkVariableType.Vector3:
                    {
                        Vector3 v = (Vector3)value;
                        writer.Put(v.X);
                        writer.Put(v.Y);
                        writer.Put(v.Z);
                        break;
                    }

                case NetworkVariableType.Quaternion:
                    {
                        Quaternion q = (Quaternion)value;
                        writer.Put(q.X);
                        writer.Put(q.Y);
                        writer.Put(q.Z);
                        writer.Put(q.W);
                        break;
                    }

                default:
                    throw new NotSupportedException(type.ToString());
            }
        }

        public static object Read(NetworkCommandReader reader, NetworkVariableType type)
        {
            switch (type)
            {
                case NetworkVariableType.Bool:
                    return reader.GetBool();

                case NetworkVariableType.Byte:
                    return reader.GetByte();

                case NetworkVariableType.Short:
                    return reader.GetShort();

                case NetworkVariableType.UShort:
                    return reader.GetUShort();

                case NetworkVariableType.Int:
                    return reader.GetInt();

                case NetworkVariableType.UInt:
                    return reader.GetUInt();

                case NetworkVariableType.Long:
                    return reader.GetLong();

                case NetworkVariableType.ULong:
                    return reader.GetULong();

                case NetworkVariableType.Float:
                    return reader.GetFloat();

                case NetworkVariableType.Double:
                    return reader.GetDouble();

                case NetworkVariableType.String:
                    return reader.GetString();

                case NetworkVariableType.Vector2:
                    return new Vector2(reader.GetFloat(), reader.GetFloat());

                case NetworkVariableType.Vector3:
                    return new Vector3(reader.GetFloat(), reader.GetFloat(), reader.GetFloat());

                case NetworkVariableType.Quaternion:
                    return new Quaternion(reader.GetFloat(), reader.GetFloat(), reader.GetFloat(), reader.GetFloat());

                default:
                    throw new NotSupportedException(type.ToString());
            }
        }

        public static NetworkVariableType GetType(object value)
        {
            switch (value)
            {
                case bool:
                    return NetworkVariableType.Bool;

                case byte:
                    return NetworkVariableType.Byte;

                case short:
                    return NetworkVariableType.Short;

                case ushort:
                    return NetworkVariableType.UShort;

                case int:
                    return NetworkVariableType.Int;

                case uint:
                    return NetworkVariableType.UInt;

                case long:
                    return NetworkVariableType.Long;

                case ulong:
                    return NetworkVariableType.ULong;

                case float:
                    return NetworkVariableType.Float;

                case double:
                    return NetworkVariableType.Double;

                case string:
                    return NetworkVariableType.String;

                case Vector2:
                    return NetworkVariableType.Vector2;

                case Vector3:
                    return NetworkVariableType.Vector3;

                case Quaternion:
                    return NetworkVariableType.Quaternion;

                default:
                    throw new NotSupportedException(value.GetType().FullName);
            }
        }
    }
}