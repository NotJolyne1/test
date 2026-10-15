using RelayNetworking.Client;
using System.Collections.Generic;
using UnityEngine;

namespace RelayNetworking.Unity
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkTransformGroup : MonoBehaviour
    {
        private const int MaxTransforms = 64;

        [SerializeField]
        private List<Transform> transforms = new();

        [SerializeField]
        private float _sendRate = 20f;

        [SerializeField]
        private float _positionThreshold = 0.001f;

        [SerializeField]
        private float _rotationThreshold = 0.1f;

        private NetworkObject _networkObject;

        private Vector3[] _lastPositions;
        private Quaternion[] _lastRotations;
        private Vector3[] _targetPositions;
        private Quaternion[] _targetRotations;
        private bool[] _received;

        private byte[] _dirtyIndices;
        private System.Numerics.Vector3[] _dirtyPositions;
        private System.Numerics.Quaternion[] _dirtyRotations;

        private float _timer;

        internal int Count => Mathf.Min(transforms.Count, MaxTransforms);

        private void Awake()
        {
            _networkObject = GetComponent<NetworkObject>();

            _lastPositions = new Vector3[Count];
            _lastRotations = new Quaternion[Count];
            _targetPositions = new Vector3[Count];
            _targetRotations = new Quaternion[Count];
            _received = new bool[Count];

            _dirtyIndices = new byte[Count];
            _dirtyPositions = new System.Numerics.Vector3[Count];
            _dirtyRotations = new System.Numerics.Quaternion[Count];

            for (int i = 0; i < Count; i++)
            {
                if (transforms[i] == null)
                    continue;

                _lastPositions[i] = transforms[i].localPosition;
                _lastRotations[i] = transforms[i].localRotation;
                _targetPositions[i] = _lastPositions[i];
                _targetRotations[i] = _lastRotations[i];
            }
        }

        private void OnValidate()
        {
            if (transforms.Count > MaxTransforms)
                Debug.LogWarning($"NetworkTransformGroup on '{name}' has {transforms.Count} transforms, only the first {MaxTransforms} will be synced.");

            for (int i = 0; i < transforms.Count; i++)
            {
                if (transforms[i] == this.transform)
                    Debug.LogWarning($"NetworkTransformGroup on '{name}' has its own root Transform in the list at index {i}.");
            }
        }

        private void Update()
        {
            if (!_networkObject.HasControl)
            {
                for (int i = 0; i < Count; i++)
                {
                    if (!_received[i] || transforms[i] == null)
                        continue;

                    transforms[i].localPosition = Vector3.Lerp(transforms[i].localPosition, _targetPositions[i], Time.deltaTime * 15f);
                    transforms[i].localRotation = Quaternion.Slerp(transforms[i].localRotation, _targetRotations[i], Time.deltaTime * 15f);
                }

                return;
            }

            _timer += Time.deltaTime;

            if (_timer < 1f / _sendRate)
                return;

            _timer = 0f;

            int dirtyCount = 0;

            for (int i = 0; i < Count; i++)
            {
                if (transforms[i] == null)
                    continue;

                Vector3 position = transforms[i].localPosition;
                Quaternion rotation = transforms[i].localRotation;

                if (Vector3.Distance(position, _lastPositions[i]) < _positionThreshold &&
                    Quaternion.Angle(rotation, _lastRotations[i]) < _rotationThreshold)
                    continue;

                _lastPositions[i] = position;
                _lastRotations[i] = rotation;

                _dirtyIndices[dirtyCount] = (byte)i;
                _dirtyPositions[dirtyCount] = new System.Numerics.Vector3(position.x, position.y, position.z);
                _dirtyRotations[dirtyCount] = new System.Numerics.Quaternion(rotation.x, rotation.y, rotation.z, rotation.w);
                dirtyCount++;
            }

            if (dirtyCount == 0)
                return;

            byte[] indices = new byte[dirtyCount];
            System.Numerics.Vector3[] positions = new System.Numerics.Vector3[dirtyCount];
            System.Numerics.Quaternion[] rotations = new System.Numerics.Quaternion[dirtyCount];

            for (int i = 0; i < dirtyCount; i++)
            {
                indices[i] = _dirtyIndices[i];
                positions[i] = _dirtyPositions[i];
                rotations[i] = _dirtyRotations[i];
            }

            RelayNetwork.SendChildTransforms(_networkObject.NetworkId, indices, positions, rotations);
        }

        internal void SetRemoteState(byte index, Vector3 position, Quaternion rotation)
        {
            if (index >= Count)
                return;

            _targetPositions[index] = position;
            _targetRotations[index] = rotation;
            _received[index] = true;
        }
    }
}