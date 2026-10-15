using System.Numerics;

namespace RelayNetworking.Common.Data
{
    public readonly struct ChildTransformData
    {
        public readonly uint NetworkId;
        public readonly byte[] Indices;
        public readonly Vector3[] Positions;
        public readonly Quaternion[] Rotations;
        internal ChildTransformData(uint networkId, byte[] indices, Vector3[] positions, Quaternion[] rotations)
        {
            NetworkId = networkId;
            Indices = indices;
            Positions = positions;
            Rotations = rotations;
        }
    }
}