using RelayNetworking.Client;
using RelayNetworking.Common;
using RelayNetworking.Common.Protocol.Networking;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace RelayNetworking.Unity
{
    [DisallowMultipleComponent]
    public sealed class NetworkObject : MonoBehaviour
    {
        public event Action<RelayPlayer, RelayPlayer> OnOwnershipChanged;

        [SerializeField]
        internal ushort _prefabId;
        public ushort PrefabId => _prefabId;

        [SerializeField]
        private bool _leaderCanControl;

        [SerializeField]
        private bool _interestBased;
        public bool InterestBased => _interestBased;

        public uint NetworkId { get; internal set; }
        public int OwnerPlayerId { get; internal set; }
        public RelayPlayer Owner => RelayNetwork.GetPlayer(OwnerPlayerId);

        public bool LeaderCanControl
        {
            get => _leaderCanControl;
            set => _leaderCanControl = value;
        }

        public bool IsMine => RelayNetwork.LocalPlayer != null && RelayNetwork.LocalPlayer.PlayerId == OwnerPlayerId;
        public bool IsLeader => RelayNetwork.LocalPlayer.IsLeader;
        public bool HasControl => IsMine || (_leaderCanControl && IsLeader);

        private readonly List<NetworkBehaviour> _behaviours = new List<NetworkBehaviour>();
        private ushort _nextBehaviourId;

        internal ushort Register(NetworkBehaviour behaviour)
        {
            ushort behaviourId = _nextBehaviourId;
            _nextBehaviourId++;
            _behaviours.Add(behaviour);
            return behaviourId;
        }

        internal NetworkBehaviour GetBehaviour(ushort behaviourId)
        {
            if (behaviourId >= _behaviours.Count)
                return null;
            return _behaviours[behaviourId];
        }

        public void ClearBufferedRpcs()
        {
            if (OwnerPlayerId != RelayNetwork.LocalPlayer.PlayerId && (!LeaderCanControl || !RelayNetwork.LocalPlayer.IsLeader))
            {
                Debug.LogWarning("You do not have authority over this network object");
                return;
            }
            RelayNetwork.ClearBufferedRpcs(NetworkId);
        }

        public static void ClearBufferedRpcs(NetworkObject networkObject)
        {
            if (networkObject == null)
                throw new ArgumentNullException(nameof(networkObject));
            if (networkObject.OwnerPlayerId != RelayNetwork.LocalPlayer.PlayerId && (!networkObject.LeaderCanControl || !RelayNetwork.LocalPlayer.IsLeader))
            {
                Debug.LogWarning("You do not have authority over this network object");
                return;
            }
            RelayNetwork.ClearBufferedRpcs(networkObject.NetworkId);
        }

        public bool TransferOwnership(RelayPlayer player)
        {
            if (player == null)
                throw new ArgumentNullException(nameof(player));
            if (!HasControl)
            {
                Debug.LogWarning("You do not have authority over this network object");
                return false;
            }
            return RelayNetwork.TransferOwnership(NetworkId, player);
        }

        public static bool TransferOwnership(NetworkObject networkObject, RelayPlayer player)
        {
            if (networkObject == null)
                throw new ArgumentNullException(nameof(networkObject));
            return networkObject.TransferOwnership(player);
        }

        public bool SetPlayerInterested(RelayPlayer player, bool interested)
        {
            if (player == null)
                throw new ArgumentNullException(nameof(player));

            Debug.Log($"[NetworkObject] SetPlayerInterested networkId={NetworkId} player={player.PlayerId} interested={interested}");

            if (!_interestBased)
            {
                Debug.LogWarning("This network object isn't interest based");
                return false;
            }
            if (NetworkId == 0)
            {
                Debug.LogWarning("This network object hasn't been confirmed by the server yet");
                return false;
            }
            return RelayNetwork.SetPlayerInterested(NetworkId, player, interested);
        }

        public bool SetPlayerInterested(int playerId, bool interested)
        {
            Debug.Log($"[NetworkObject] SetPlayerInterested networkId={NetworkId} playerId={playerId} interested={interested}");

            if (!_interestBased)
            {
                Debug.LogWarning("This network object isn't interest based");
                return false;
            }
            if (NetworkId == 0)
            {
                Debug.LogWarning("This network object hasn't been confirmed by the server yet");
                return false;
            }
            return RelayNetwork.SetPlayerInterested(NetworkId, playerId, interested);
        }

        internal void OwnershipChanged(RelayPlayer oldOwner, RelayPlayer newOwner)
        {
            OnOwnershipChanged?.Invoke(oldOwner, newOwner);
        }
    }
}