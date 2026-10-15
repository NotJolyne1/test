using System.Numerics;

namespace RelayNetworking.Common.Data
{
    public readonly struct TransformData
    {
        public readonly uint NetworkId;
        public readonly Vector3 Position;
        public readonly Quaternion Rotation;
        public readonly Vector3 Velocity;
        public readonly Vector3 AngularVelocity;
        internal TransformData(uint networkId, Vector3 position, Quaternion rotation, Vector3 velocity, Vector3 angularVelocity)
        {
            NetworkId = networkId;
            Position = position;
            Rotation = rotation;
            Velocity = velocity;
            AngularVelocity = angularVelocity;
        }
    }
}