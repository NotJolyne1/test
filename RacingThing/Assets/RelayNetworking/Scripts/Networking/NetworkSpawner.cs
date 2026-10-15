using RelayNetworking.Client;
using RelayNetworking.Common;
using RelayNetworking.Common.Data;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RelayNetworking.Unity
{
    public static class NetworkSpawner
    {
        private static readonly Dictionary<uint, NetworkObject> _objects = new Dictionary<uint, NetworkObject>();
        private static readonly Queue<NetworkObject> _pendingSpawns = new Queue<NetworkObject>();

        internal static void Register(NetworkObject obj)
        {
            if (obj == null || obj.NetworkId == 0)
                return;
            _objects[obj.NetworkId] = obj;
        }

        internal static void Unregister(NetworkObject obj)
        {
            if (obj == null)
                return;
            _objects.Remove(obj.NetworkId);
        }

        public static NetworkObject Get(uint networkId)
        {
            NetworkObject obj;
            _objects.TryGetValue(networkId, out obj);
            return obj;
        }

        public static NetworkObject Spawn(NetworkObject prefab)
        {
            return Spawn(prefab, (int[])null);
        }

        public static NetworkObject Spawn(NetworkObject prefab, params RelayPlayer[] interestedPlayers)
        {
            int[] ids = null;

            if (interestedPlayers != null && interestedPlayers.Length > 0)
                ids = interestedPlayers.Select(p => p.PlayerId).ToArray();

            return Spawn(prefab, ids);
        }

        public static NetworkObject Spawn(NetworkObject prefab, int[] interestedPlayerIds)
        {
            if (prefab == null)
                return null;

            NetworkObject obj = Object.Instantiate(prefab, prefab.transform.position, prefab.transform.rotation);

            obj.OwnerPlayerId = RelayNetwork.LocalPlayer != null ? RelayNetwork.LocalPlayer.PlayerId : 0;

            _pendingSpawns.Enqueue(obj);

            NetworkTransformGroup group = prefab.GetComponent<NetworkTransformGroup>();
            byte childTransformCount = group != null ? (byte)group.Count : (byte)0;

            bool sent = RelayNetwork.Spawn(prefab.PrefabId, new System.Numerics.Vector3(prefab.transform.position.x, prefab.transform.position.y, prefab.transform.position.z), new System.Numerics.Quaternion(prefab.transform.rotation.x, prefab.transform.rotation.y, prefab.transform.rotation.z, prefab.transform.rotation.w), prefab.InterestBased, interestedPlayerIds, childTransformCount);

            if (!sent)
            {
                _pendingSpawns.Dequeue();
                Object.Destroy(obj.gameObject);
                return null;
            }

            return obj;
        }

        internal static bool TryResolvePendingSpawn(SpawnData data, out NetworkObject obj)
        {
            obj = null;

            if (data.IsPlayer)
                return false;

            if (RelayNetwork.LocalPlayer == null || data.OwnerPlayerId != RelayNetwork.LocalPlayer.PlayerId)
                return false;

            if (_pendingSpawns.Count == 0)
                return false;

            obj = _pendingSpawns.Dequeue();
            return true;
        }

        internal static void SpawnConfirmed(NetworkObject obj)
        {
            Register(obj);
        }

        public static bool Despawn(uint networkId)
        {
            if (networkId == 0)
                return false;
            RelayNetwork.Destroy(networkId);
            return true;
        }

        public static bool Despawn(GameObject obj)
        {
            if (obj == null)
                return false;
            return Despawn(obj.GetComponent<NetworkObject>());
        }

        public static bool Despawn(NetworkObject obj)
        {
            if (obj == null)
                return false;
            if (obj.NetworkId == 0)
                return false;
            RelayNetwork.Destroy(obj.NetworkId);
            return true;
        }

        internal static void DestroyConfirmed(NetworkObject obj)
        {
            if (obj == null)
                return;
            Unregister(obj);
            Object.Destroy(obj.gameObject);
        }

        public static void DestroyAll()
        {
            Camera camera = Camera.main;
            if (camera != null && camera.transform.parent != null)
                camera.transform.SetParent(null, true);
            foreach (NetworkObject obj in Object.FindObjectsByType<NetworkObject>(FindObjectsSortMode.None))
            {
                if (obj != null)
                    Object.Destroy(obj.gameObject);
            }
            _objects.Clear();
            _pendingSpawns.Clear();
        }
    }
}