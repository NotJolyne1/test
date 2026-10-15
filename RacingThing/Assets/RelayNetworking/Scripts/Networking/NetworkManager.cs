using RelayNetworking.Client;
using RelayNetworking.Common;
using RelayNetworking.Common.Data;
using RelayNetworking.Unity;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public class NetworkManager : MonoBehaviour
{
    private static NetworkManager _instance;
    private string _loadingScene;
    public static NetworkObject PlayerPrefab { get; private set; }

    [SerializeField]
    private NetworkObject _playerPrefab;

    [SerializeField]
    [Tooltip("Automatically sync scene changes across all players in the room when the room leader loads a new scene via RelayNetwork.LoadScene")]
    private bool syncScenes;

    [SerializeField]
    [Tooltip("Allow the leader to drop players connections and kick them from the room")]
    private bool AllowDropConnection;


    private void Awake()
    {
        if (_instance != null)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;

        DontDestroyOnLoad(gameObject);

        RelayNetwork.Initialize();
        RelayNetwork.OnObjectSpawned += OnObjectSpawned;
        RelayNetwork.OnObjectTransform += OnObjectTransform;
        RelayNetwork.OnObjectDestroyed += OnObjectDestroyed;
        RelayNetwork.OnDisconnected += OnDisconnected;
        RelayNetwork.OnOwnershipChanged += OnOwnershipChanged;
        RelayNetwork.OnSceneLoadRequested += OnSceneLoadRequested;
        RelayNetwork.OnChildTransform += OnChildTransform;
        PlayerPrefab = _playerPrefab;
        RelayNetwork.SyncScenes = syncScenes;
        RelayNetwork.EnableDropConnection = AllowDropConnection;

        NetworkTransformGroup playerGroup = _playerPrefab != null ? _playerPrefab.GetComponent<NetworkTransformGroup>() : null;
        RelayNetwork.PlayerChildTransformCount = playerGroup != null ? (byte)playerGroup.Count : (byte)0;
    }

    private void OnDestroy()
    {
        RelayNetwork.OnObjectSpawned -= OnObjectSpawned;
        RelayNetwork.OnObjectTransform -= OnObjectTransform;
        RelayNetwork.OnObjectDestroyed -= OnObjectDestroyed;
        RelayNetwork.OnDisconnected -= OnDisconnected;
        RelayNetwork.OnOwnershipChanged -= OnOwnershipChanged;
        RelayNetwork.OnSceneLoadRequested -= OnSceneLoadRequested;
        RelayNetwork.OnChildTransform -= OnChildTransform;
    }

    private void Update()
    {
        RelayNetwork.Tick();
    }

    private void OnSceneLoadRequested(string sceneName)
    {
        if (!RelayNetwork.SyncScenes)
            return;

        if (!string.IsNullOrEmpty(_loadingScene))
            return;

        StartCoroutine(LoadSynchronizedScene(sceneName));
    }

    private IEnumerator LoadSynchronizedScene(string sceneName)
    {
        _loadingScene = sceneName;

        yield return null;

        if (SceneManager.GetActiveScene().name != sceneName)
        {
            NetworkSpawner.DestroyAll();

            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);

            if (operation == null)
            {
                _loadingScene = null;
                yield break;
            }

            while (!operation.isDone)
                yield return null;

            yield return null;
        }

        if (RelayNetwork.CurrentRoom == null)
        {
            _loadingScene = null;
            yield break;
        }

        if (RelayNetwork.CurrentRoom.CurrentScene != sceneName)
        {
            string nextScene = RelayNetwork.CurrentRoom.CurrentScene;
            _loadingScene = null;

            if (RelayNetwork.SyncScenes && !string.IsNullOrWhiteSpace(nextScene))
                StartCoroutine(LoadSynchronizedScene(nextScene));

            yield break;
        }

        RelayNetwork.SceneReady(sceneName);
        _loadingScene = null;
    }

    private void OnApplicationQuit()
    {
        RelayNetwork.Disconnect();
    }

    private void OnObjectSpawned(SpawnData data)
    {
        if (RelayNetwork.SyncScenes && RelayNetwork.SceneLoadPending)
            return;

        Debug.Log($"RECEIVED SPAWN id {data.NetworkId} owner {data.OwnerPlayerId}");

        NetworkObject obj;

        if (NetworkSpawner.TryResolvePendingSpawn(data, out obj))
        {
            Debug.Log($"Reconciled locally predicted spawn {data.NetworkId}");

            obj.NetworkId = data.NetworkId;
            obj.OwnerPlayerId = data.OwnerPlayerId;
            obj.transform.SetPositionAndRotation(
                new Vector3(data.Position.X, data.Position.Y, data.Position.Z),
                new Quaternion(data.Rotation.X, data.Rotation.Y, data.Rotation.Z, data.Rotation.W));
        }
        else
        {
            if (NetworkPrefabRegistry.Instance == null)
            {
                Debug.LogError("No NetworkPrefabRegistry in the scene, cannot resolve spawned prefabs.");
                return;
            }

            NetworkObject prefab = data.IsPlayer ? PlayerPrefab : NetworkPrefabRegistry.Instance.Get(data.PrefabId);

            if (prefab == null)
            {
                Debug.LogError($"Prefab null for spawn {data.NetworkId} (PrefabId {data.PrefabId}, IsPlayer {data.IsPlayer})");
                return;
            }

            if (NetworkSpawner.Get(data.NetworkId) != null)
            {
                Debug.LogError($"Already exists {data.NetworkId}");
                return;
            }

            Debug.Log($"Instantiating {data.NetworkId}");

            obj = Instantiate(
                prefab,
                new Vector3(data.Position.X, data.Position.Y, data.Position.Z),
                new Quaternion(data.Rotation.X, data.Rotation.Y, data.Rotation.Z, data.Rotation.W));

            Debug.Log($"Instantiate returned {obj}");

            obj.NetworkId = data.NetworkId;
            obj.OwnerPlayerId = data.OwnerPlayerId;
        }

        if (obj.IsMine)
        {
            foreach (NetworkBehaviour behaviour in obj.GetComponents<NetworkBehaviour>())
            {
                RelayNetwork.RegisterNetworkVariables(
                    obj.NetworkId,
                    behaviour.Variables
                        .Select(x => (x.Id, x.Type, (NetworkVariableAuthority)x.Authority))
                        .ToList());

                if (behaviour.RpcMethods.Count > 0)
                {
                    RelayNetwork.RegisterRpcs(
                        obj.NetworkId,
                        behaviour.RpcMethods
                            .Select(x => (behaviour.BehaviourId, x.MethodId, x.Caller, x.Delivery))
                            .ToList());
                }
            }
        }

        NetworkSpawner.SpawnConfirmed(obj);

        Debug.Log($"Registered={NetworkSpawner.Get(data.NetworkId) != null}");
    }

    private void OnObjectDestroyed(DestroyData data)
    {
        NetworkObject obj = NetworkSpawner.Get(data.NetworkId);

        if (obj == null)
            return;

        NetworkSpawner.DestroyConfirmed(obj);
    }

    private void OnObjectTransform(TransformData data)
    {
        if (RelayNetwork.SyncScenes && RelayNetwork.SceneLoadPending)
            return;

        NetworkObject obj = NetworkSpawner.Get(data.NetworkId);

        if (obj == null)
            return;

        if (obj.HasControl)
            return;

        NetworkRigidbody networkRigidbody = obj.GetComponent<NetworkRigidbody>();

        if (networkRigidbody != null)
        {
            networkRigidbody.SetRemoteState(
                new Vector3(
                    data.Position.X,
                    data.Position.Y,
                    data.Position.Z),
                new Quaternion(
                    data.Rotation.X,
                    data.Rotation.Y,
                    data.Rotation.Z,
                    data.Rotation.W),
                new Vector3(
                    data.Velocity.X,
                    data.Velocity.Y,
                    data.Velocity.Z),
                new Vector3(
                    data.AngularVelocity.X,
                    data.AngularVelocity.Y,
                    data.AngularVelocity.Z));

            return;
        }

        NetworkTransform networkTransform = obj.GetComponent<NetworkTransform>();

        if (networkTransform != null)
        {
            networkTransform.SetRemoteTransform(
                new Vector3(
                    data.Position.X,
                    data.Position.Y,
                    data.Position.Z),
                new Quaternion(
                    data.Rotation.X,
                    data.Rotation.Y,
                    data.Rotation.Z,
                    data.Rotation.W));
        }
        else
        {
            obj.transform.SetPositionAndRotation(
                new Vector3(
                    data.Position.X,
                    data.Position.Y,
                    data.Position.Z),
                new Quaternion(
                    data.Rotation.X,
                    data.Rotation.Y,
                    data.Rotation.Z,
                    data.Rotation.W));
        }
    }

    private void OnDisconnected()
    {
        NetworkSpawner.DestroyAll();
    }

    private void OnOwnershipChanged(uint networkId, int ownerPlayerId)
    {
        NetworkObject obj = NetworkSpawner.Get(networkId);

        if (obj == null)
            return;

        RelayPlayer oldOwner = obj.Owner;
        obj.OwnerPlayerId = ownerPlayerId;
        RelayPlayer newOwner = obj.Owner;

        obj.OwnershipChanged(oldOwner, newOwner);
    }

    private void OnChildTransform(ChildTransformData data)
    {
        NetworkObject obj = NetworkSpawner.Get(data.NetworkId);

        if (obj == null)
            return;

        NetworkTransformGroup group = obj.GetComponent<NetworkTransformGroup>();

        if (group == null)
            return;

        if (obj.HasControl)
            return;

        for (int i = 0; i < data.Indices.Length; i++)
        {
            group.SetRemoteState(
                data.Indices[i],
                new Vector3(data.Positions[i].X, data.Positions[i].Y, data.Positions[i].Z),
                new Quaternion(data.Rotations[i].X, data.Rotations[i].Y, data.Rotations[i].Z, data.Rotations[i].W));
        }
    }
}