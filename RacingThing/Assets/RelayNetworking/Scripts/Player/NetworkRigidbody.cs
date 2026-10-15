using RelayNetworking.Client;
using RelayNetworking.Common;
using UnityEngine;

namespace RelayNetworking.Unity
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class NetworkRigidbody : MonoBehaviour
    {
        [SerializeField]
        private float _sendRate = 20f;

        [SerializeField]
        private float _positionThreshold = 0.001f;

        [SerializeField]
        private float _rotationThreshold = 0.1f;

        [SerializeField]
        private float _interpolationSpeed = 15f;

        private NetworkObject _networkObject;
        private Rigidbody _rigidbody;

        private Vector3 _lastPosition;
        private Quaternion _lastRotation;

        private float _timer;

        private Vector3 _targetPosition;
        private Quaternion _targetRotation;
        private Vector3 _targetVelocity;
        private Vector3 _targetAngularVelocity;

        private bool _received;

        public Vector3 velocity
        {
            get => _rigidbody.velocity;
            set
            {
                if (!_networkObject.HasControl)
                    return;

                _rigidbody.velocity = value;
            }
        }

        public Vector3 angularVelocity
        {
            get => _rigidbody.angularVelocity;
            set
            {
                if (!_networkObject.HasControl)
                    return;

                _rigidbody.angularVelocity = value;
            }
        }

        public bool freezeRotation
        {
            get => _rigidbody.freezeRotation;
            set => _rigidbody.freezeRotation = value;
        }

        public RigidbodyInterpolation interpolation
        {
            get => _rigidbody.interpolation;
            set => _rigidbody.interpolation = value;
        }

        public Vector3 position => _rigidbody.position;

        public Quaternion rotation => _rigidbody.rotation;

        public Rigidbody Rigidbody => _rigidbody;

        public void AddForce(Vector3 force, ForceMode mode = ForceMode.Force)
        {
            if (!_networkObject.HasControl)
                return;

            _rigidbody.AddForce(force, mode);
        }

        public void AddTorque(Vector3 torque, ForceMode mode = ForceMode.Force)
        {
            if (!_networkObject.HasControl)
                return;

            _rigidbody.AddTorque(torque, mode);
        }

        public void MovePosition(Vector3 position)
        {
            if (!_networkObject.HasControl)
                return;

            _rigidbody.MovePosition(position);
        }

        public void MoveRotation(Quaternion rotation)
        {
            if (!_networkObject.HasControl)
                return;

            _rigidbody.MoveRotation(rotation);
        }

        private void Awake()
        {
            _networkObject = GetComponent<NetworkObject>();
            _rigidbody = GetComponent<Rigidbody>();

            _lastPosition = _rigidbody.position;
            _lastRotation = _rigidbody.rotation;

            _targetPosition = _rigidbody.position;
            _targetRotation = _rigidbody.rotation;

            ApplyAuthorityState();
        }

        private void Start()
        {
            ApplyAuthorityState();
        }


        private void OnEnable()
        {
            _networkObject.OnOwnershipChanged += HandleOwnershipChanged;
        }

        private void OnDisable()
        {
            _networkObject.OnOwnershipChanged -= HandleOwnershipChanged;
        }

        private void HandleOwnershipChanged(RelayPlayer oldOwner, RelayPlayer newOwner)
        {
            ApplyAuthorityState();
        }

        private void ApplyAuthorityState()
        {
            _rigidbody.isKinematic = !_networkObject.HasControl;

            if (!_networkObject.HasControl)
            {
                _received = false;
            }
        }

        private void Update()
        {
            if (!_networkObject.HasControl)
                return;

            _timer += Time.deltaTime;

            if (_timer < 1f / _sendRate)
                return;

            _timer = 0f;

            Vector3 position = _rigidbody.position;
            Quaternion rotation = _rigidbody.rotation;

            if (Vector3.Distance(position, _lastPosition) < _positionThreshold &&
                Quaternion.Angle(rotation, _lastRotation) < _rotationThreshold)
                return;

            _lastPosition = position;
            _lastRotation = rotation;

            Vector3 velocity = _rigidbody.velocity;
            Vector3 angularVelocity = _rigidbody.angularVelocity;

            RelayNetwork.SendTransform(
                _networkObject.NetworkId,
                new System.Numerics.Vector3(position.x, position.y, position.z),
                new System.Numerics.Quaternion(rotation.x, rotation.y, rotation.z, rotation.w),
                new System.Numerics.Vector3(velocity.x, velocity.y, velocity.z),
                new System.Numerics.Vector3(angularVelocity.x, angularVelocity.y, angularVelocity.z));
        }

        private void FixedUpdate()
        {
            if (_networkObject.HasControl)
                return;

            if (!_received)
                return;

            _rigidbody.MovePosition(Vector3.Lerp(_rigidbody.position, _targetPosition, Time.fixedDeltaTime * _interpolationSpeed));
            _rigidbody.MoveRotation(Quaternion.Slerp(_rigidbody.rotation, _targetRotation, Time.fixedDeltaTime * _interpolationSpeed));
        }

        internal void SetRemoteState(Vector3 position, Quaternion rotation, Vector3 velocity, Vector3 angularVelocity)
        {
            _targetPosition = position;
            _targetRotation = rotation;
            _targetVelocity = velocity;
            _targetAngularVelocity = angularVelocity;
            _received = true;
        }
    }
}