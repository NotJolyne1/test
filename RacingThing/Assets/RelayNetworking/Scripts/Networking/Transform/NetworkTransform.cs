using RelayNetworking.Client;
using UnityEngine;

namespace RelayNetworking.Unity
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkTransform : MonoBehaviour
    {
        [SerializeField]
        private float _sendRate = 20f;

        [SerializeField]
        private float _positionThreshold = 0.001f;

        [SerializeField]
        private float _rotationThreshold = 0.1f;

        private NetworkObject _networkObject;

        private Vector3 _lastPosition;
        private Quaternion _lastRotation;

        private float _timer;
        private Vector3 _targetPosition;
        private Quaternion _targetRotation;

        private bool _received;

        private void Awake()
        {
            _networkObject = GetComponent<NetworkObject>();

            _lastPosition = transform.position;
            _lastRotation = transform.rotation;

            _targetPosition = transform.position;
            _targetRotation = transform.rotation;
        }

        private void Update()
        {
            if (!_networkObject.HasControl)
            {
                if (_received)
                {
                    transform.position = Vector3.Lerp(
                        transform.position,
                        _targetPosition,
                        Time.deltaTime * 15f);

                    transform.rotation = Quaternion.Slerp(
                        transform.rotation,
                        _targetRotation,
                        Time.deltaTime * 15f);
                }

                return;
            }

            _timer += Time.deltaTime;

            if (_timer < 1f / _sendRate)
                return;

            _timer = 0f;

            Vector3 position = transform.position;
            Quaternion rotation = transform.rotation;

            if (Vector3.Distance(position, _lastPosition) < _positionThreshold &&
                Quaternion.Angle(rotation, _lastRotation) < _rotationThreshold)
                return;

            _lastPosition = position;
            _lastRotation = rotation;

            RelayNetwork.SendTransform(
                _networkObject.NetworkId,
                new System.Numerics.Vector3(position.x, position.y, position.z),
                new System.Numerics.Quaternion(rotation.x, rotation.y, rotation.z, rotation.w),
                System.Numerics.Vector3.Zero,
                System.Numerics.Vector3.Zero);
        }

        internal void SetRemoteTransform(Vector3 position, Quaternion rotation)
        {
            _targetPosition = position;
            _targetRotation = rotation;
            _received = true;
        }
    }
}