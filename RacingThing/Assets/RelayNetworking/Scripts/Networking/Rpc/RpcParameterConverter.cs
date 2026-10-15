using System;
using UnityEngine;

namespace RelayNetworking.Unity
{
    internal static class RpcParameterConverter
    {
        public static object Convert(object value, Type targetType)
        {
            if (value == null)
                return null;

            if (targetType.IsInstanceOfType(value))
                return value;

            if (value is System.Numerics.Vector2 nv2 && targetType == typeof(Vector2))
                return new Vector2(nv2.X, nv2.Y);

            if (value is System.Numerics.Vector3 nv3 && targetType == typeof(Vector3))
                return new Vector3(nv3.X, nv3.Y, nv3.Z);

            if (value is System.Numerics.Quaternion nq && targetType == typeof(Quaternion))
                return new Quaternion(nq.X, nq.Y, nq.Z, nq.W);

            if (targetType.IsEnum)
                return Enum.ToObject(targetType, value);

            if (targetType == typeof(object))
                return value;

            try
            {
                return System.Convert.ChangeType(value, targetType);
            }
            catch
            {
                return value;
            }
        }

        public static object ToWireValue(object value)
        {
            if (value is Vector2 v2)
                return new System.Numerics.Vector2(v2.x, v2.y);

            if (value is Vector3 v3)
                return new System.Numerics.Vector3(v3.x, v3.y, v3.z);

            if (value is Quaternion q)
                return new System.Numerics.Quaternion(q.x, q.y, q.z, q.w);

            return value;
        }
    }
}