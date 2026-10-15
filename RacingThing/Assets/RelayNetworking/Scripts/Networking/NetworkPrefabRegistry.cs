using System.Collections.Generic;
using UnityEngine;

namespace RelayNetworking.Unity
{
    public sealed class NetworkPrefabRegistry : MonoBehaviour
    {
        public static NetworkPrefabRegistry Instance { get; private set; }

        [SerializeField]
        private NetworkObject[] _prefabs;

        private readonly Dictionary<ushort, NetworkObject> _lookup = new Dictionary<ushort, NetworkObject>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"[NetworkPrefabRegistry] Multiple registries in the scene, replacing '{Instance.name}' with '{name}'.");
            }

            Instance = this;

            foreach (NetworkObject prefab in _prefabs)
            {
                if (prefab == null)
                    continue;

                if (_lookup.ContainsKey(prefab.PrefabId))
                {
                    Debug.LogWarning($"[NetworkPrefabRegistry] Duplicate PrefabId {prefab.PrefabId} on '{prefab.name}', overwriting previous entry.");
                }

                _lookup[prefab.PrefabId] = prefab;

                Debug.Log($"[NetworkPrefabRegistry] Registered '{prefab.name}' as PrefabId {prefab.PrefabId}");
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public NetworkObject Get(ushort prefabId)
        {
            NetworkObject prefab;

            if (_lookup.TryGetValue(prefabId, out prefab))
                return prefab;

            Debug.LogError($"[NetworkPrefabRegistry] No prefab registered for PrefabId {prefabId}. Registered ids: {string.Join(", ", _lookup.Keys)}");
            return null;
        }
    }
}