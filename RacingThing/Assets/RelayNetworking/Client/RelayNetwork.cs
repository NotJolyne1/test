using System;
using System.Collections.Generic;
using System.Numerics;
using LiteNetLib;
using RelayNetworking.Client.Session;
using RelayNetworking.Client.Time;
using RelayNetworking.Client.Transport;
using RelayNetworking.Common;
using RelayNetworking.Common.Callbacks;
using RelayNetworking.Common.Collections;
using RelayNetworking.Common.Data;
using RelayNetworking.Common.Protocol;
using RelayNetworking.Common.Protocol.Networking;
using RelayNetworking.Common.Rooms;
using RelayNetworking.Common.Rpc;
using RelayNetworking.Common.Serialization;

namespace RelayNetworking.Client
{
    public static class RelayNetwork
    {
        private static readonly RelayClient _client = new RelayClient();

        private static readonly ClientClock _clock = new ClientClock();

        private static double _timeSyncIntervalSeconds = 5.0;

        private static double _timeSyncTimeoutSeconds = 2.0;

        private static double _nextTimeSyncAt;

        private static double _timeSyncSentAt;

        private static ulong _timeSyncRequestId;

        private static uint _nextSpawnRequestId;

        private static uint _nextCollectionRequestId;

        private static bool _timeSyncPending;

        private static string _ownershipReclamationToken = string.Empty;

        private static string _reconnectToken = string.Empty;

        private static string _pendingReclamationToken = string.Empty;

        private static string _lastAppId = string.Empty;

        private static RelayClientOptions? _lastConnectionOptions;

        private static bool _manualDisconnect;

        private static bool _reconnecting;

        private static bool _reconnectAttemptPending;

        private static bool _awaitingReclamation;

        private static int _reconnectAttempt;

        private static DateTime _reconnectDeadlineUtc;

        private static DateTime _nextReconnectAttemptUtc;

        private static DateTime _reclamationDeadlineUtc;

        private static ushort _serverProtocolVersion = NetworkProtocol.LegacyVersion;

        private static bool _syncScenes = true;

        private static bool _sceneLoadPending;

        private static string _pendingSceneName = string.Empty;

        public static Action<string>? Logger;

        public static RelayNetworking.Client.Session.Session Session { get; } = new RelayNetworking.Client.Session.Session();


        public static bool Connected => _client.Connected;

        public static RelayPlayer? Leader => Session.Leader;

        public static bool IsLeader => LocalPlayer?.IsLeader ?? false;

        public static RelayPlayer? LocalPlayer => Session.LocalPlayer;

        public static RelayIdentity Identity { get; } = new RelayIdentity();


        public static AuthenticationValues Authentication { get; } = new AuthenticationValues();


        public static IReadOnlyList<RelayPlayer> ActivePlayers => Session.ActivePlayers;

        public static Room? CurrentRoom => Session.CurrentRoom;

        public static double LocalTime => _clock.LocalTime;

        public static double ServerTime => _clock.ServerTime;

        public static ulong CurrentTick => _clock.CurrentTick;

        public static int TickRate => _clock.TickRate;

        public static double TickDuration => _clock.TickDuration;

        public static bool IsSimulationSynced => _clock.SimulationSynced;

        public static bool OwnershipReclamationAvailable => !string.IsNullOrEmpty(_ownershipReclamationToken);

        public static bool IsReconnecting => _reconnecting;

        public static ushort ProtocolVersion => NetworkProtocol.CurrentVersion;

        public static ushort ServerProtocolVersion => _serverProtocolVersion;

        public static bool EnableDropConnection { get; set; }

        public static bool SyncScenes
        {
            get => _syncScenes;
            set
            {
                if (_syncScenes == value)
                    return;

                _syncScenes = value;

                if (!_syncScenes)
                {
                    _sceneLoadPending = false;
                    _pendingSceneName = string.Empty;
                    return;
                }

                string sceneName = CurrentRoom?.CurrentScene ?? string.Empty;

                if (!string.IsNullOrWhiteSpace(sceneName))
                    SceneLoadRequested(sceneName);
            }
        }

        public static bool SceneLoadPending => _sceneLoadPending;

        public static string PendingSceneName => _pendingSceneName;

        public static event Action<RelayEvent>? OnEvent;

        public static event Action<SpawnData>? OnObjectSpawned;

        public static event Action<uint>? OnSpawnFailed;

        public static event Action<DestroyData>? OnObjectDestroyed;

        public static event Action<TransformData>? OnObjectTransform;

        public static event Action<uint, int>? OnOwnershipChanged;

        public static event Action? OnDisconnected;

        public static event Action? OnReconnecting;

        public static event Action<int>? OnReconnectAttempt;

        public static event Action? OnReconnected;

        public static event Action? OnReconnectFailed;

        public static event Action? OnOwnershipReclaimed;

        public static event Action<string>? OnSceneLoadRequested;

        public static event Action<uint, ushort, object>? OnNetworkVariable;

        public static event Action<uint, ushort, ushort, object>? OnNetworkVariableDetailed;

        public static event Action<NetworkCollectionData>? OnNetworkCollection;

        public static event Action<RpcData>? OnRpc;

        public static void Initialize()
        {
            _client.Start();
        }

        public static void Connect(string host = RelayClientOptions.TestingServerIP, int port = 7777, string connectionKey = "RelayNetworking")
        {
            Logger = Console.WriteLine;
            RelayClientOptions options = new RelayClientOptions { Host = host, Port = port, ConnectionKey = connectionKey };
            Connect(options);
        }

        public static void Connect(RelayClientOptions options)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            _lastConnectionOptions = CopyOptions(options);
            _timeSyncIntervalSeconds = options.TimeSyncIntervalSeconds;
            _timeSyncTimeoutSeconds = options.TimeSyncTimeoutSeconds;
            _nextTimeSyncAt = 0.0;
            _timeSyncPending = false;
            _manualDisconnect = false;
            _serverProtocolVersion = NetworkProtocol.LegacyVersion;
            _client.Connect(options);
        }

        public static void Disconnect()
        {
            _manualDisconnect = true;
            _reconnecting = false;
            _reconnectAttemptPending = false;
            _awaitingReclamation = false;
            _ownershipReclamationToken = string.Empty;
            _reconnectToken = string.Empty;
            _pendingReclamationToken = string.Empty;
            _sceneLoadPending = false;
            _pendingSceneName = string.Empty;
            _client.Disconnect();
            Session.Reset();
            Disconnected();
            _manualDisconnect = false;
        }

        internal static void TransportConnected()
        {
            Session.Connected = true;
            _reconnectAttemptPending = false;
            RelayCallbacks.OnConnected();

            if (!_reconnecting)
                return;

            _awaitingReclamation = true;
            _reclamationDeadlineUtc = DateTime.UtcNow.AddSeconds(
                Math.Max(0.5, _lastConnectionOptions?.ReclamationConfirmTimeoutSeconds ?? 3.0));

            Authenticate(_lastAppId);
        }

        internal static void TransportDisconnected()
        {
            Session.Connected = false;
            _timeSyncPending = false;
            _reconnectAttemptPending = false;
            _awaitingReclamation = false;

            if (_manualDisconnect)
                return;

            if (_reconnecting)
            {
                if (!string.IsNullOrEmpty(_pendingReclamationToken))
                    _reconnectToken = _pendingReclamationToken;

                ScheduleNextReconnect();
                return;
            }

            if (CanAutoReconnect())
            {
                BeginReconnect();
                return;
            }

            Session.Reset();
            Disconnected();
        }

        internal static void Disconnected()
        {
            _sceneLoadPending = false;
            _pendingSceneName = string.Empty;
            OnDisconnected?.Invoke();
            RelayCallbacks.OnDisconnected();
        }

        internal static void SetOwnershipReclamationToken(string token)
        {
            string value = token ?? string.Empty;

            if (_reconnecting)
            {
                _pendingReclamationToken = value;
                return;
            }

            _ownershipReclamationToken = value;
        }

        internal static void SetServerProtocolVersion(ushort version)
        {
            _serverProtocolVersion = version;
        }

        internal static void OwnershipReclaimed()
        {
            if (_reconnecting)
            {
                if (!string.IsNullOrEmpty(_pendingReclamationToken))
                    _ownershipReclamationToken = _pendingReclamationToken;

                _reconnectToken = string.Empty;
                _pendingReclamationToken = string.Empty;
                _reconnecting = false;
                _reconnectAttemptPending = false;
                _awaitingReclamation = false;
                _reconnectAttempt = 0;

                OnReconnected?.Invoke();
            }

            OnOwnershipReclaimed?.Invoke();
            RelayCallbacks.OnOwnershipReclaimed();
        }

        internal static void SceneLoadRequested(string sceneName)
        {
            if (!SyncScenes || string.IsNullOrWhiteSpace(sceneName))
                return;

            if (_sceneLoadPending && string.Equals(_pendingSceneName, sceneName, StringComparison.Ordinal))
                return;

            _sceneLoadPending = true;
            _pendingSceneName = sceneName;
            OnSceneLoadRequested?.Invoke(sceneName);
        }

        public static bool LoadScene(string sceneName)
        {
            if (!SyncScenes || !Connected || CurrentRoom == null || LocalPlayer == null || !IsLeader)
                return false;

            sceneName = sceneName?.Trim() ?? string.Empty;

            if (sceneName.Length == 0 || sceneName.Length > 128)
                return false;

            if (string.Equals(CurrentRoom.CurrentScene, sceneName, StringComparison.Ordinal))
                return false;

            return Send(CommandCode.CmdLoadScene, writer => writer.Put(sceneName));
        }

        public static bool SceneReady(string sceneName)
        {
            if (!SyncScenes || !Connected || CurrentRoom == null)
                return false;

            sceneName = sceneName?.Trim() ?? string.Empty;

            if (sceneName.Length == 0 || sceneName.Length > 128)
                return false;

            bool sent = Send(CommandCode.CmdSceneReady, writer => writer.Put(sceneName));

            if (sent && string.Equals(_pendingSceneName, sceneName, StringComparison.Ordinal))
            {
                _sceneLoadPending = false;
                _pendingSceneName = string.Empty;
            }

            return sent;
        }

        public static void Tick()
        {
            _client.Poll();

            if (_reconnecting)
                UpdateReconnect();

            if (Connected)
                UpdateTimeSync();
        }

        private static RelayClientOptions CopyOptions(RelayClientOptions options)
        {
            return new RelayClientOptions
            {
                Host = options.Host,
                Port = options.Port,
                ConnectionKey = options.ConnectionKey,
                TimeSyncIntervalSeconds = options.TimeSyncIntervalSeconds,
                TimeSyncTimeoutSeconds = options.TimeSyncTimeoutSeconds,
                AutoReconnect = options.AutoReconnect,
                ReconnectWindowSeconds = options.ReconnectWindowSeconds,
                ReconnectInitialDelaySeconds = options.ReconnectInitialDelaySeconds,
                ReconnectMaxDelaySeconds = options.ReconnectMaxDelaySeconds,
                ReclamationConfirmTimeoutSeconds = options.ReclamationConfirmTimeoutSeconds
            };
        }

        private static bool CanAutoReconnect()
        {
            return _lastConnectionOptions != null &&
                   _lastConnectionOptions.AutoReconnect &&
                   !string.IsNullOrEmpty(_ownershipReclamationToken) &&
                   !string.IsNullOrEmpty(_lastAppId);
        }

        private static void BeginReconnect()
        {
            RelayClientOptions options = _lastConnectionOptions!;

            _reconnecting = true;
            _reconnectAttemptPending = false;
            _awaitingReclamation = false;
            _reconnectAttempt = 0;
            _reconnectToken = _ownershipReclamationToken;
            _pendingReclamationToken = string.Empty;

            double window = Math.Max(1.0, options.ReconnectWindowSeconds);
            double initialDelay = Math.Max(0.0, options.ReconnectInitialDelaySeconds);

            _reconnectDeadlineUtc = DateTime.UtcNow.AddSeconds(window);
            _nextReconnectAttemptUtc = DateTime.UtcNow.AddSeconds(initialDelay);

            OnReconnecting?.Invoke();
        }

        private static void UpdateReconnect()
        {
            DateTime now = DateTime.UtcNow;

            if (now >= _reconnectDeadlineUtc)
            {
                FailReconnect();
                return;
            }

            if (_awaitingReclamation)
            {
                if (now < _reclamationDeadlineUtc)
                    return;

                _awaitingReclamation = false;

                if (Connected)
                {
                    _manualDisconnect = true;
                    _client.Disconnect();
                    _manualDisconnect = false;
                    Session.Connected = false;
                }

                ScheduleNextReconnect();
                return;
            }

            if (Connected || _reconnectAttemptPending || now < _nextReconnectAttemptUtc)
                return;

            if (_lastConnectionOptions == null)
            {
                FailReconnect();
                return;
            }

            _reconnectAttempt++;
            _reconnectAttemptPending = true;

            OnReconnectAttempt?.Invoke(_reconnectAttempt);
            _client.Connect(_lastConnectionOptions);
        }

        private static void ScheduleNextReconnect()
        {
            if (!_reconnecting || _lastConnectionOptions == null)
                return;

            double initial = Math.Max(0.1, _lastConnectionOptions.ReconnectInitialDelaySeconds);
            double maximum = Math.Max(initial, _lastConnectionOptions.ReconnectMaxDelaySeconds);
            double delay = Math.Min(maximum, initial * Math.Pow(2.0, Math.Max(0, _reconnectAttempt - 1)));

            _nextReconnectAttemptUtc = DateTime.UtcNow.AddSeconds(delay);
        }

        private static void FailReconnect()
        {
            _reconnecting = false;
            _reconnectAttemptPending = false;
            _awaitingReclamation = false;
            _reconnectAttempt = 0;
            _reconnectToken = string.Empty;
            _pendingReclamationToken = string.Empty;
            _ownershipReclamationToken = string.Empty;

            if (Connected)
            {
                _manualDisconnect = true;
                _client.Disconnect();
                _manualDisconnect = false;
            }

            Session.Reset();
            OnReconnectFailed?.Invoke();
            Disconnected();
        }

        private static void UpdateTimeSync()
        {
            double localTime = _clock.LocalTime;
            if (_timeSyncPending)
            {
                if (localTime - _timeSyncSentAt < _timeSyncTimeoutSeconds)
                {
                    return;
                }

                _timeSyncPending = false;
            }

            if (!(localTime < _nextTimeSyncAt))
            {
                _timeSyncRequestId++;
                _timeSyncSentAt = localTime;
                _timeSyncPending = true;
                Send(CommandCode.CmdTimeSyncRequest, delegate (NetworkCommandWriter writer)
                {
                    writer.Put(_timeSyncRequestId);
                }, DeliveryMethod.Unreliable);
                _nextTimeSyncAt = localTime + _timeSyncIntervalSeconds;
            }
        }

        internal static void TimeSyncReceived(ulong requestId, double serverTime, ulong serverTick, int tickRate)
        {
            if (_timeSyncPending && requestId == _timeSyncRequestId)
            {
                _timeSyncPending = false;
                _clock.OnSyncResponse(_timeSyncSentAt, _clock.LocalTime, serverTime, tickRate);
            }
        }

        public static RelayPlayer? GetPlayer(int playerId)
        {
            foreach (RelayPlayer activePlayer in Session.ActivePlayers)
            {
                if (activePlayer.PlayerId == playerId)
                {
                    return activePlayer;
                }
            }

            return null;
        }

        public static bool Send(INetworkCommand command, DeliveryMethod method = DeliveryMethod.ReliableOrdered)
        {
            if (!Connected)
            {
                return false;
            }

            return _client.Send(command, method);
        }

        public static bool Send(CommandCode code, Action<NetworkCommandWriter>? write = null, DeliveryMethod method = DeliveryMethod.ReliableOrdered)
        {
            if (!Connected)
            {
                return false;
            }

            NetworkCommandWriter networkCommandWriter = new NetworkCommandWriter();
            networkCommandWriter.Put((ushort)code);
            write?.Invoke(networkCommandWriter);
            return _client.Send(networkCommandWriter, method);
        }

        public static byte PlayerChildTransformCount { get; set; }

        public static void Authenticate(string appId)
        {
            _lastAppId = appId ?? string.Empty;

            string appId2 = _lastAppId;
            string reclamationToken = _reconnecting ? _reconnectToken : _ownershipReclamationToken;

            Send(CommandCode.CmdAuthenticate, delegate (NetworkCommandWriter writer)
            {
                writer.Put(appId2);
                writer.Put((byte)Authentication.Type);
                writer.Put(Authentication.Username);
                writer.Put(Authentication.Data);
                writer.Put(reclamationToken);
                writer.Put(PlayerChildTransformCount);
                writer.Put(NetworkProtocol.CurrentVersion);
            });
        }

        public static void CreateRoom(string roomName)
        {
            CreateRoom(roomName, new RoomOptions());
        }

        public static void CreateRoom(string roomName, RoomOptions options)
        {
            string roomName2 = roomName;
            RoomOptions options2 = options ?? new RoomOptions();

            Send(CommandCode.CmdCreateRoom, delegate (NetworkCommandWriter writer)
            {
                writer.Put(roomName2);
                writer.Put(options2.MaxPlayers);
                writer.Put(options2.IsOpen);
                writer.Put(options2.IsVisible);

                PropertyChanges changes = new PropertyChanges();

                if (options2.CustomProperties != null)
                {
                    foreach (var pair in options2.CustomProperties)
                    {
                        if (string.Equals(pair.Key, RoomProperties.CurrentSceneKey, StringComparison.Ordinal))
                            continue;

                        changes[pair.Key] = pair.Value;
                    }
                }

                PropertySerializer.WriteCustom(writer, changes);
            });
        }

        public static void JoinRoom(string roomName)
        {
            string roomName2 = roomName;
            Send(CommandCode.CmdJoinRoom, delegate (NetworkCommandWriter writer)
            {
                writer.Put(roomName2);
            });
        }

        public static void LeaveRoom()
        {
            Send(CommandCode.CmdLeaveRoom);
            Disconnected();
        }

        public static void JoinRandomRoom()
        {
            JoinRandomRoom(null);
        }

        public static void JoinRandomRoom(Dictionary<string, object>? expectedProperties)
        {
            Dictionary<string, object>? filter = expectedProperties;

            Send(CommandCode.CmdJoinRandomRoom, delegate (NetworkCommandWriter writer)
            {
                PropertyChanges changes = new PropertyChanges();

                if (filter != null)
                {
                    foreach (var pair in filter)
                        changes[pair.Key] = pair.Value;
                }

                PropertySerializer.WriteCustom(writer, changes);
            });
        }

        public static bool DropConnection()
        {
            return DropConnection(LocalPlayer);
        }

        public static bool DropConnection(RelayPlayer? player)
        {
            if (!Connected || LocalPlayer == null || CurrentRoom == null || !IsLeader)
                return false;

            if (player == null) return false;

            bool found = false;

            foreach (RelayPlayer activePlayer in ActivePlayers)
            {
                if (activePlayer.PlayerId != player.PlayerId)
                    continue;

                found = true;
                break;
            }

            if (!found)
                return false;

            return DropConnection(player.PlayerId);
        }

        public static bool DropConnection(int playerId)
        {
            if (!Connected || LocalPlayer == null || CurrentRoom == null || !IsLeader)
                return false;

            RelayPlayer? target = GetPlayer(playerId);

            if (target == null)
                return false;

            return Send(CommandCode.CmdDropConnection, delegate (NetworkCommandWriter writer)
            {
                writer.Put(target.PlayerId);
            });
        }

        public static bool UpdateProperties(PropertyChanges changes)
        {
            if (LocalPlayer == null)
            {
                return false;
            }

            return UpdateProperties(LocalPlayer, changes);
        }

        public static bool UpdateProperties(RelayPlayer player, PropertyChanges changes)
        {
            RelayPlayer player2 = player;
            PropertyChanges changes2 = changes;
            return Send(CommandCode.CmdPlayerProperties, delegate (NetworkCommandWriter writer)
            {
                writer.Put(player2.PlayerId);
                PropertySerializer.WriteReserved(writer, changes2);
                PropertySerializer.WriteCustom(writer, changes2);
            });
        }

        public static bool UpdateRoomProperties(PropertyChanges changes)
        {
            PropertyChanges changes2 = changes;
            if (!Connected)
            {
                return false;
            }

            if (Session.CurrentRoom == null)
            {
                return false;
            }

            if (changes2.Custom.ContainsKey(RoomProperties.CurrentSceneKey))
                return false;

            Send(CommandCode.CmdRoomProperties, delegate (NetworkCommandWriter writer)
            {
                PropertySerializer.WriteReserved(writer, changes2);
                PropertySerializer.WriteCustom(writer, changes2);
            });
            return true;
        }

        public static bool UpdateRoomProperties(Action<PropertyChanges> build)
        {
            PropertyChanges propertyChanges = new PropertyChanges();
            build(propertyChanges);
            return UpdateRoomProperties(propertyChanges);
        }

        public static void Ping()
        {
            Send(CommandCode.CmdPing, delegate (NetworkCommandWriter writer)
            {
                writer.Put(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            });
        }

        public static bool SendEvent(byte eventCode, byte[]? payload = null, EventTarget target = EventTarget.All, int[]? targetPlayers = null, DeliveryMethod method = DeliveryMethod.ReliableOrdered)
        {
            int[] targetPlayers2 = targetPlayers;
            byte[] payload2 = payload;
            if (LocalPlayer == null)
            {
                return false;
            }

            return Send(CommandCode.CmdEvent, delegate (NetworkCommandWriter writer)
            {
                writer.Put(eventCode);
                writer.Put((byte)target);
                if (targetPlayers2 == null || targetPlayers2.Length == 0)
                {
                    writer.Put((byte)0);
                }
                else
                {
                    if (targetPlayers2.Length > 255)
                    {
                        throw new ArgumentOutOfRangeException("targetPlayers");
                    }

                    writer.Put((byte)targetPlayers2.Length);
                    for (int i = 0; i < targetPlayers2.Length; i++)
                    {
                        writer.Put(targetPlayers2[i]);
                    }
                }

                writer.Put(payload2 ?? Array.Empty<byte>());
            }, method);
        }

        public static bool SendEvent(byte eventCode, DeliveryMethod method)
        {
            return SendEvent(eventCode, null, EventTarget.All, null, method);
        }

        public static bool SendEvent(byte eventCode)
        {
            return SendEvent(eventCode, null);
        }

        public static bool SendEvent(byte eventCode, byte[] payload)
        {
            return SendEvent(eventCode, payload, EventTarget.All);
        }

        public static bool SendEvent(byte eventCode, byte[] payload, EventTarget target)
        {
            return SendEvent(eventCode, payload, target, null, DeliveryMethod.ReliableOrdered);
        }

        internal static void EventReceived(int senderPlayerId, byte eventCode, byte[] payload, ulong sentTick)
        {
            RelayNetwork.OnEvent?.Invoke(new RelayEvent(senderPlayerId, eventCode, payload, sentTick));
            Console.WriteLine($"{senderPlayerId} sent event {eventCode} with {payload.Length} bytes");
        }

        internal static void ObjectSpawned(SpawnData spawnData)
        {
            RelayNetwork.OnObjectSpawned?.Invoke(spawnData);
        }

        internal static void SpawnFailed(uint requestId)
        {
            RelayNetwork.OnSpawnFailed?.Invoke(requestId);
        }

        internal static void ObjectDestroyed(DestroyData data)
        {
            RelayNetwork.OnObjectDestroyed?.Invoke(data);
        }

        internal static void ObjectTransform(TransformData data)
        {
            RelayNetwork.OnObjectTransform?.Invoke(data);
        }

        public static bool Spawn(ushort prefabId, Vector3 position, Quaternion rotation)
        {
            return Spawn(prefabId, position, rotation, false, (int[])null);
        }

        public static bool Spawn(ushort prefabId, Vector3 position, Quaternion rotation, bool interestBased)
        {
            return Spawn(prefabId, position, rotation, interestBased, (int[])null);
        }

        public static bool Spawn(ushort prefabId, Vector3 position, Quaternion rotation, bool interestBased, int[]? interestedPlayerIds, byte childTransformCount = 0)
        {
            return Spawn(prefabId, position, rotation, interestBased, interestedPlayerIds, childTransformCount, out _);
        }

        public static bool Spawn(ushort prefabId, Vector3 position, Quaternion rotation, bool interestBased, int[]? interestedPlayerIds, byte childTransformCount, out uint requestId)
        {
            requestId = 0;

            if (CurrentRoom == null)
                return false;

            _nextSpawnRequestId++;

            if (_nextSpawnRequestId == 0)
                _nextSpawnRequestId++;

            uint spawnRequestId = _nextSpawnRequestId;
            ushort prefabId2 = prefabId;
            Vector3 position2 = position;
            Quaternion rotation2 = rotation;
            bool interestBased2 = interestBased;
            int[]? interestedPlayerIds2 = interestedPlayerIds;
            byte childTransformCount2 = childTransformCount;

            bool sent = Send(CommandCode.CmdSpawn, delegate (NetworkCommandWriter writer)
            {
                writer.Put(prefabId2);
                writer.Put(position2.X);
                writer.Put(position2.Y);
                writer.Put(position2.Z);
                writer.Put(rotation2.X);
                writer.Put(rotation2.Y);
                writer.Put(rotation2.Z);
                writer.Put(rotation2.W);

                writer.Put(interestBased2);

                ushort count = interestedPlayerIds2 == null ? (ushort)0 : (ushort)interestedPlayerIds2.Length;
                writer.Put(count);

                if (interestedPlayerIds2 != null)
                {
                    for (int i = 0; i < interestedPlayerIds2.Length; i++)
                        writer.Put(interestedPlayerIds2[i]);
                }

                writer.Put(childTransformCount2);
                writer.Put(spawnRequestId);
            });

            if (sent)
                requestId = spawnRequestId;

            return sent;
        }

        public static bool Spawn(ushort prefabId, Vector3 position, Quaternion rotation, bool interestBased, params RelayPlayer[] interestedPlayers)
        {
            int[]? ids = null;

            if (interestedPlayers != null && interestedPlayers.Length > 0)
            {
                ids = new int[interestedPlayers.Length];

                for (int i = 0; i < interestedPlayers.Length; i++)
                    ids[i] = interestedPlayers[i].PlayerId;
            }

            return Spawn(prefabId, position, rotation, interestBased, ids);
        }

        public static bool SetPlayerInterested(uint networkId, bool interested)
        {
            if (LocalPlayer == null)
                return false;

            return SetPlayerInterested(networkId, LocalPlayer.PlayerId, interested);
        }

        public static bool SetPlayerInterested(uint networkId, RelayPlayer player, bool interested)
        {
            if (player == null)
                return false;

            return SetPlayerInterested(networkId, player.PlayerId, interested);
        }

        public static bool SetPlayerInterested(uint networkId, int playerId, bool interested)
        {
            if (!Connected || CurrentRoom == null)
            {
                Console.WriteLine($"[Client] SetPlayerInterested({networkId}, {playerId}, {interested}) skipped: not connected or not in a room");
                return false;
            }

            uint networkId2 = networkId;
            int playerId2 = playerId;
            bool interested2 = interested;

            bool sent = Send(CommandCode.CmdSetPlayerInterest, delegate (NetworkCommandWriter writer)
            {
                writer.Put(networkId2);
                writer.Put(playerId2);
                writer.Put(interested2);
            });

            Console.WriteLine($"[Client] SetPlayerInterested({networkId}, {playerId}, {interested}) sent={sent}");

            return sent;
        }

        public static bool Destroy(uint networkId)
        {
            if (!Connected)
            {
                return false;
            }

            if (LocalPlayer == null)
            {
                return false;
            }

            return Send(CommandCode.CmdDestroy, delegate (NetworkCommandWriter writer)
            {
                writer.Put(networkId);
            });
        }

        public static bool SendTransform(uint networkId, Vector3 position, Quaternion rotation, Vector3 velocity, Vector3 angularVelocity)
        {
            return Send(CommandCode.CmdTransform, delegate (NetworkCommandWriter writer)
            {
                writer.Put(networkId);
                writer.Put(position.X);
                writer.Put(position.Y);
                writer.Put(position.Z);
                writer.Put(rotation.X);
                writer.Put(rotation.Y);
                writer.Put(rotation.Z);
                writer.Put(rotation.W);
                writer.Put(velocity.X);
                writer.Put(velocity.Y);
                writer.Put(velocity.Z);
                writer.Put(angularVelocity.X);
                writer.Put(angularVelocity.Y);
                writer.Put(angularVelocity.Z);
            });
        }

        public static event Action<ChildTransformData>? OnChildTransform;

        public static bool SendChildTransforms(uint networkId, byte[] indices, Vector3[] positions, Quaternion[] rotations)
        {
            if (indices == null || indices.Length == 0)
                return false;

            uint networkId2 = networkId;
            byte[] indices2 = indices;
            Vector3[] positions2 = positions;
            Quaternion[] rotations2 = rotations;

            return Send(CommandCode.CmdChildTransform, delegate (NetworkCommandWriter writer)
            {
                writer.Put(networkId2);
                writer.Put((byte)indices2.Length);

                for (int i = 0; i < indices2.Length; i++)
                {
                    writer.Put(indices2[i]);

                    writer.Put(positions2[i].X);
                    writer.Put(positions2[i].Y);
                    writer.Put(positions2[i].Z);

                    writer.Put(rotations2[i].X);
                    writer.Put(rotations2[i].Y);
                    writer.Put(rotations2[i].Z);
                    writer.Put(rotations2[i].W);
                }
            });
        }

        internal static void ChildTransformReceived(ChildTransformData data)
        {
            OnChildTransform?.Invoke(data);
        }

        public static bool RegisterNetworkVariables(uint networkId, IReadOnlyList<(ushort Id, NetworkVariableType Type, NetworkVariableAuthority Authority)> variables)
        {
            return RegisterNetworkVariables(networkId, 0, variables);
        }

        public static bool RegisterNetworkVariables(uint networkId, ushort behaviourId, IReadOnlyList<(ushort Id, NetworkVariableType Type, NetworkVariableAuthority Authority)> variables)
        {
            IReadOnlyList<(ushort Id, NetworkVariableType Type, NetworkVariableAuthority Authority)> variables2 = variables;
            return Send(CommandCode.CmdRegisterNetworkVariables, delegate (NetworkCommandWriter writer)
            {
                writer.Put(networkId);
                writer.Put((ushort)variables2.Count);
                foreach (var item in variables2)
                {
                    writer.Put(item.Id);
                    writer.Put((byte)item.Type);
                    writer.Put((byte)item.Authority);
                }
                writer.Put(behaviourId);
            });
        }

        public static bool RegisterRpcs(uint networkId, IReadOnlyList<(ushort BehaviourId, ushort MethodId, RpcCaller Caller, DeliveryMethod Delivery)> rpcs)
        {
            IReadOnlyList<(ushort BehaviourId, ushort MethodId, RpcCaller Caller, DeliveryMethod Delivery)> rpcs2 = rpcs;
            if (rpcs2 == null || rpcs2.Count == 0)
            {
                return false;
            }

            return Send(CommandCode.CmdRegisterRpcs, delegate (NetworkCommandWriter writer)
            {
                writer.Put(networkId);
                writer.Put((ushort)rpcs2.Count);
                foreach (var item in rpcs2)
                {
                    writer.Put(item.BehaviourId);
                    writer.Put(item.MethodId);
                    writer.Put((byte)item.Caller);
                    writer.Put((byte)item.Delivery);
                }
            });
        }

        public static bool SetNetworkVariable(uint networkId, ushort variableId, NetworkVariableType type, object value)
        {
            return SetNetworkVariable(networkId, 0, variableId, type, value);
        }

        public static bool SetNetworkVariable(uint networkId, ushort behaviourId, ushort variableId, NetworkVariableType type, object value)
        {
            object value2 = value;
            return Send(CommandCode.CmdNetworkVariable, delegate (NetworkCommandWriter writer)
            {
                writer.Put(networkId);
                writer.Put(variableId);
                writer.Put((byte)type);
                NetworkVariableSerializer.Write(writer, type, value2);
                writer.Put(behaviourId);
            });
        }

        internal static void NetworkVariableReceived(uint networkId, ushort behaviourId, ushort variableId, object value)
        {
            RelayNetwork.OnNetworkVariableDetailed?.Invoke(networkId, behaviourId, variableId, value);
            RelayNetwork.OnNetworkVariable?.Invoke(networkId, variableId, value);
        }

        public static bool RegisterNetworkCollections(
            uint networkId,
            ushort behaviourId,
            IReadOnlyList<NetworkCollectionRegistration> collections)
        {
            if (collections == null || collections.Count == 0)
                return false;

            if (ServerProtocolVersion < 2)
            {
                Logger?.Invoke("[RelayNetwork] Network collections require server protocol 2 or newer.");
                return false;
            }

            return Send(CommandCode.CmdRegisterNetworkCollections, delegate (NetworkCommandWriter writer)
            {
                writer.Put(networkId);
                writer.Put(behaviourId);
                writer.Put((ushort)collections.Count);

                foreach (NetworkCollectionRegistration collection in collections)
                {
                    writer.Put(collection.Id);
                    writer.Put((byte)collection.CollectionType);
                    writer.Put((byte)collection.Authority);
                    writer.Put((byte)collection.KeyType);
                    writer.Put((byte)collection.ValueType);
                    writer.Put((ushort)collection.Entries.Length);

                    foreach (NetworkCollectionEntry entry in collection.Entries)
                    {
                        if (collection.CollectionType == NetworkCollectionType.Dictionary)
                            NetworkVariableSerializer.Write(writer, collection.KeyType, entry.Key);

                        NetworkVariableSerializer.Write(writer, collection.ValueType, entry.Value);
                    }
                }
            });
        }

        public static uint SendNetworkCollectionOperation(
            uint networkId,
            ushort behaviourId,
            ushort collectionId,
            NetworkCollectionType collectionType,
            NetworkCollectionOperation operation,
            NetworkVariableType keyType,
            NetworkVariableType valueType,
            int index,
            object key,
            object value)
        {
            if (ServerProtocolVersion < 2)
            {
                Logger?.Invoke("[RelayNetwork] Network collections require server protocol 2 or newer.");
                return 0;
            }

            uint requestId = ++_nextCollectionRequestId;

            if (requestId == 0)
                requestId = ++_nextCollectionRequestId;

            bool sent = Send(CommandCode.CmdNetworkCollection, delegate (NetworkCommandWriter writer)
            {
                writer.Put(networkId);
                writer.Put(behaviourId);
                writer.Put(collectionId);
                writer.Put((byte)collectionType);
                writer.Put((byte)operation);
                writer.Put(requestId);

                switch (collectionType)
                {
                    case NetworkCollectionType.List:
                        switch (operation)
                        {
                            case NetworkCollectionOperation.Add:
                                NetworkVariableSerializer.Write(writer, valueType, value);
                                break;

                            case NetworkCollectionOperation.Insert:
                            case NetworkCollectionOperation.Set:
                                writer.Put(index);
                                NetworkVariableSerializer.Write(writer, valueType, value);
                                break;

                            case NetworkCollectionOperation.RemoveAt:
                                writer.Put(index);
                                break;
                        }
                        break;

                    case NetworkCollectionType.Dictionary:
                        switch (operation)
                        {
                            case NetworkCollectionOperation.Add:
                            case NetworkCollectionOperation.Set:
                                NetworkVariableSerializer.Write(writer, keyType, key);
                                NetworkVariableSerializer.Write(writer, valueType, value);
                                break;

                            case NetworkCollectionOperation.Remove:
                                NetworkVariableSerializer.Write(writer, keyType, key);
                                break;
                        }
                        break;

                    case NetworkCollectionType.Set:
                        if (operation == NetworkCollectionOperation.Add ||
                            operation == NetworkCollectionOperation.Remove)
                        {
                            NetworkVariableSerializer.Write(writer, valueType, value);
                        }
                        break;
                }
            });

            return sent ? requestId : 0;
        }

        internal static void NetworkCollectionReceived(NetworkCollectionData data)
        {
            OnNetworkCollection?.Invoke(data);
        }

        public static bool SendRpc(string methodName, RpcTarget target, params object[] parameters)
        {
            if (LocalPlayer == null)
            {
                Logger?.Invoke("[RelayNetwork] Cannot send RPC '" + methodName + "': not authenticated.");
                return false;
            }

            return SendRpc(LocalPlayer.PlayerObjectId, methodName, target, hasSpecificTarget: false, 0, parameters);
        }

        public static bool SendRpc(string methodName, RelayPlayer targetPlayer, params object[] parameters)
        {
            if (LocalPlayer == null)
            {
                Logger?.Invoke("[RelayNetwork] Cannot send RPC " + methodName + ": not authenticated.");
                return false;
            }

            if (targetPlayer == null)
            {
                throw new ArgumentNullException("targetPlayer");
            }

            return SendRpc(LocalPlayer.PlayerObjectId, methodName, RpcTarget.All, hasSpecificTarget: true, targetPlayer.PlayerId, parameters);
        }

        public static bool SendRpc(uint networkId, string methodName, RpcTarget target, params object[] parameters)
        {
            return SendRpc(networkId, methodName, target, hasSpecificTarget: false, 0, parameters);
        }

        public static bool SendRpc(uint networkId, string methodName, RelayPlayer targetPlayer, params object[] parameters)
        {
            if (targetPlayer == null)
            {
                throw new ArgumentNullException("targetPlayer");
            }

            return SendRpc(networkId, methodName, RpcTarget.All, hasSpecificTarget: true, targetPlayer.PlayerId, parameters);
        }

        public static bool SendRpc<T>(string methodName, RpcTarget target, params object[] parameters)
        {
            if (LocalPlayer == null)
            {
                Logger?.Invoke("[RelayNetwork] Cannot send RPC '" + methodName + "': not authenticated.");
                return false;
            }

            return SendRpc(typeof(T), LocalPlayer.PlayerObjectId, methodName, target, hasSpecificTarget: false, 0, parameters);
        }

        public static bool SendRpc<T>(string methodName, RelayPlayer targetPlayer, params object[] parameters)
        {
            if (LocalPlayer == null)
            {
                Logger?.Invoke("[RelayNetwork] Cannot send RPC " + methodName + ": not authenticated.");
                return false;
            }

            if (targetPlayer == null)
            {
                throw new ArgumentNullException("targetPlayer");
            }

            return SendRpc(typeof(T), LocalPlayer.PlayerObjectId, methodName, RpcTarget.All, hasSpecificTarget: true, targetPlayer.PlayerId, parameters);
        }

        private static bool SendRpc(uint networkId, string methodName, RpcTarget target, bool hasSpecificTarget, int specificTargetPlayerId, object[] parameters)
        {
            if (string.IsNullOrEmpty(methodName))
            {
                throw new ArgumentException("RPC method name cannot be empty", "methodName");
            }

            if (!RpcRegistry.TryGetByName(methodName, out RpcMetadata metadata) || metadata == null)
            {
                Logger?.Invoke("[RelayNetwork] RPC " + methodName + " is not registered");
                return false;
            }

            return SendRpc(networkId, metadata, target, hasSpecificTarget, specificTargetPlayerId, parameters);
        }

        private static bool SendRpc(System.Type behaviourType, uint networkId, string methodName, RpcTarget target, bool hasSpecificTarget, int specificTargetPlayerId, object[] parameters)
        {
            if (string.IsNullOrEmpty(methodName))
            {
                throw new ArgumentException("RPC method name cannot be empty", "methodName");
            }

            if (!RpcRegistry.TryGetByType(behaviourType, methodName, out RpcMetadata metadata) || metadata == null)
            {
                Logger?.Invoke("[RelayNetwork] RPC " + behaviourType.Name + "." + methodName + " is not registered");
                return false;
            }

            return SendRpc(networkId, metadata, target, hasSpecificTarget, specificTargetPlayerId, parameters);
        }

        private static bool SendRpc(uint networkId, RpcMetadata metadata, RpcTarget target, bool hasSpecificTarget, int specificTargetPlayerId, object[] parameters)
        {
            RpcData rpc = new RpcData { NetworkId = networkId, BehaviourId = metadata.BehaviourId, MethodId = metadata.MethodId, Target = target, HasSpecificTarget = hasSpecificTarget, SpecificTargetPlayerId = specificTargetPlayerId, Delivery = metadata.Delivery, Parameters = (parameters ?? Array.Empty<object>()) };
            return Send(CommandCode.CmdRpc, delegate (NetworkCommandWriter writer)
            {
                RpcSerializer.WriteRequest(writer, rpc);
            }, metadata.Delivery);
        }

        internal static void RpcReceived(RpcData rpc)
        {
            RelayNetwork.OnRpc?.Invoke(rpc);
        }

        public static bool ClearBufferedRpcs(uint networkId)
        {
            if (!Connected || LocalPlayer == null)
                return false;

            return Send(CommandCode.CmdClearBufferedRpcs, delegate (NetworkCommandWriter writer)
            {
                writer.Put(networkId);
            });
        }

        public static bool ClearBufferedRpcs(uint networkId, ushort behaviourId, ushort methodId)
        {
            if (!Connected || LocalPlayer == null)
                return false;

            return Send(CommandCode.CmdClearBufferedRpc, delegate (NetworkCommandWriter writer)
            {
                writer.Put(networkId);
                writer.Put(behaviourId);
                writer.Put(methodId);
            });
        }

        public static bool ClearAllBufferedRpcs()
        {
            if (!Connected || LocalPlayer == null || !LocalPlayer.IsLeader)
                return false;

            return Send(CommandCode.CmdClearAllBufferedRpcs, delegate (NetworkCommandWriter writer)
            {
            });
        }

        public static bool TransferLeader(RelayPlayer player)
        {
            RelayPlayer player2 = player;
            if (player2 == null)
            {
                throw new ArgumentNullException("player");
            }

            if (player2.IsLeader)
            {
                return false;
            }

            if (!LocalPlayer.IsLeader)
            {
                return false;
            }

            return Send(CommandCode.CmdTransferLeader, delegate (NetworkCommandWriter writer)
            {
                writer.Put(player2.PlayerId);
            });
        }

        internal static void OwnershipChanged(uint networkId, int ownerPlayerId)
        {
            RelayNetwork.OnOwnershipChanged?.Invoke(networkId, ownerPlayerId);
        }

        public static bool TransferOwnership(uint networkId, RelayPlayer player)
        {
            RelayPlayer player2 = player;
            if (LocalPlayer == null)
            {
                return false;
            }

            if (player2 == null)
            {
                throw new ArgumentNullException("player");
            }

            return Send(CommandCode.CmdTransferOwnership, delegate (NetworkCommandWriter writer)
            {
                writer.Put(networkId);
                writer.Put(player2.PlayerId);
            });
        }
    }
}