// Debug window from the RelayNetworking test project. It uses a TestNetworkVariables
// script that isn't part of this package, so it's switched off. Add RELAY_TEST_UI to
// Project Settings > Player > Scripting Define Symbols (and bring that script over) to use it.
#if RELAY_TEST_UI
using RelayNetworking.Client;
using RelayNetworking.Common;
using RelayNetworking.Common.Collections;
using RelayNetworking.Common.Protocol;
using RelayNetworking.Common.Rpc;
using RelayNetworking.Unity;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class NetworkUI : MonoBehaviour
{
    private const byte TestEventCode = 1;

    private string _host = "127.0.0.1";
    private string _appId = "UnityTest";
    private string _room = "Room1";
    private string _username = "PlayerName";

    private Rect _window = new Rect(10, 10, 380, 480);
    private Vector2 _scroll;
    private readonly string[] _logs = new string[15];
    private int _logCount;
    private int _tab;
    private readonly string[] _tabs = { "Connect", "Properties", "Events", "Players" };

    [SerializeField]
    private NetworkObject _prefab;

    private NetworkObject _spawned;
    private void OnEnable()
    {
        RelayNetwork.OnEvent += OnNetworkEvent;
        RelayNetwork.Logger = Log;
        RelayNetwork.OnOwnershipReclaimed += OwnershipReclamed;
    }

    internal void OwnershipReclamed()
    {
        Log("Ownership reclaimed!");
    }

    private void OnDisable()
    {
        RelayNetwork.OnEvent -= OnNetworkEvent;
    }
    public void Log(string text)
    {
        if (_logCount < _logs.Length)
            _logs[_logCount++] = text;
        else
        {
            for (int i = 1; i < _logs.Length; i++)
                _logs[i - 1] = _logs[i];

            _logs[_logs.Length - 1] = text;
        }

        if (text.Contains("Ping")) return;
        Debug.Log(text);
    }
    private void OnNetworkEvent(RelayEvent e)
    {
        if (e.EventCode == TestEventCode)
            Log("Event 1 from " + e.SenderPlayerId);
    }

    private void OnGUI()
    {
        _window = GUI.Window(1234, _window, DrawWindow, "Relay Networking");

        float y = 10;

        for (int i = 0; i < _logCount; i++)
        {
            GUI.Label(new Rect(Screen.width - 260, y, 250, 20), _logs[i]);
            y += 20;
        }
    }

    private void DrawWindow(int id)
    {
        _tab = GUILayout.Toolbar(_tab, _tabs);

        GUILayout.Space(6);

        switch (_tab)
        {
            case 0: DrawConnectTab(); break;
            case 1: DrawPropertiesTab(); break;
            case 2: DrawEventsTab(); break;
            case 3: DrawPlayersTab(); break;
        }

        GUILayout.Space(8);

        GUILayout.Label("Connected: " + RelayNetwork.Connected + "   Room: " + (RelayNetwork.CurrentRoom?.Name ?? "-"));

        GUI.DragWindow(new Rect(0, 0, 10000, 20));
    }

    private void DrawConnectTab()
    {
        GUILayout.Label("Host");
        _host = GUILayout.TextField(_host);

        GUILayout.Label("App Id");
        _appId = GUILayout.TextField(_appId);

        GUILayout.Label("Username");
        _username = GUILayout.TextField(_username);

        GUILayout.Label("Room");
        _room = GUILayout.TextField(_room);

        GUILayout.Space(5);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Connect"))
        {
            Log("Connecting to " + _host + ":7777");
            RelayNetwork.Connect(_host);
			RelayNetwork.Logger = Debug.Log;
		}

        if (GUILayout.Button("load scene"))
        {
            RelayNetwork.LoadScene("testscene");
        }

        if (GUILayout.Button("Disconnect"))
        {
            Log("DisconnectPeerCalled");
            RelayNetwork.Disconnect();
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Authenticate"))
        {
            Log("Authenticating as " + _username + " AppId " + _appId);

            RelayNetwork.Authentication.Username = _username;
            RelayNetwork.Authentication.Type = AuthenticationType.None;
            RelayNetwork.Authenticate(_appId);
        }
        if (GUILayout.Button("Ping"))
        {
            Log("Ping");
            RelayNetwork.Ping();
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(8);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Create")) RelayNetwork.CreateRoom(_room);
        if (GUILayout.Button("Join")) RelayNetwork.JoinRoom(_room);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Random")) RelayNetwork.JoinRandomRoom();
        if (GUILayout.Button("Random prop based"))
            RelayNetwork.JoinRandomRoom(new Dictionary<string, object> { ["Map"] = "i forgot" });
        if (GUILayout.Button("Leave")) RelayNetwork.LeaveRoom();
        GUILayout.EndHorizontal();
    }

    private void DrawPropertiesTab()
    {
        GUILayout.Label("Player");

        if (GUILayout.Button("Set Username"))
        {
            PropertyChanges changes = new();
            changes.Username = _username;
            RelayNetwork.UpdateProperties(changes);
        }

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Set Color"))
        {
            PropertyChanges changes = new();
            changes["Color"] = "Blue";
            RelayNetwork.UpdateProperties(changes);
        }
        if (GUILayout.Button("Remove Color"))
        {
            PropertyChanges changes = new();
            changes["Color"] = null;
            RelayNetwork.UpdateProperties(changes);
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(10);
        GUILayout.Label("Room");

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Set Map"))
        {
            PropertyChanges changes = new();
            changes["Map"] = "i forgot";
            RelayNetwork.UpdateRoomProperties(changes);
        }
        if (GUILayout.Button("Remove Map"))
        {
            PropertyChanges changes = new();
            changes["Map"] = null;
            RelayNetwork.UpdateRoomProperties(changes);
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(10);
        GUILayout.Label("room proprties:");

        if (RelayNetwork.CurrentRoom != null)
        {
            foreach (var p in RelayNetwork.CurrentRoom.Properties)
                GUILayout.Label(p.Key + " = " + (p.Value ?? "null"));
        }
    }

    private void DrawEventsTab()
    {
        if (GUILayout.Button("Rpc Anyone Buffered"))
        {
            RelayNetwork.SendRpc<TestNetworkVariables>(nameof(TestNetworkVariables.RPC_Test), RpcTarget.All);
        }

        if (GUILayout.Button("Clear Rpc Buffer"))
        {
            var net = FindObjectOfType<TestNetworkVariables>();
            net.ClearBuffered();
        }

        if (GUILayout.Button("Rpc Anyone"))
        {
            var net = FindObjectOfType<TestNetworkVariables>();
            net.InvokeRpc2();
        }

        if (GUILayout.Button("Rpc Leader"))
        {
            var net = FindObjectOfType<TestNetworkVariables>();
            net.InvokeRpcL();
        }

        if (GUILayout.Button("Transfer Leader"))
        {
            foreach (RelayPlayer player in RelayNetwork.ActivePlayers)
            {
                if (player != RelayNetwork.LocalPlayer)
                {
                    RelayNetwork.TransferLeader(player);
                    break;
                }
            }
        }


        if (GUILayout.Button("Spawn Network"))
        {
            _spawned = NetworkSpawner.Spawn(_prefab);

            Debug.Log($"Null? {_spawned == null}");
        }

        if (GUILayout.Button("Spawn Network Interested"))
        {
            _spawned = NetworkSpawner.Spawn(_prefab, RelayNetwork.LocalPlayer);
        }

        if (GUILayout.Button("Find Spawned"))
        {
            NetworkObject[] objects = FindObjectsOfType<NetworkObject>();

            foreach (NetworkObject obj in objects)
            {
                if (obj.PrefabId == _prefab._prefabId)
                {
                    _spawned = obj;
                    Debug.Log($"Found spawned object {_spawned.NetworkId}");
                    break;
                }
            }

            if (_spawned == null)
                Debug.Log("No spawned object found.");
        }

        if (GUILayout.Button("Transfer Spawned"))
        {
            var ui = FindObjectOfType<NetworkUI>();
            ui.Log($"Before Owner: {_spawned.OwnerPlayerId}");

            foreach (RelayPlayer player in RelayNetwork.ActivePlayers)
            {
                if (player != RelayNetwork.LocalPlayer)
                {
                    _spawned.TransferOwnership(player);
                    break;
                }
            }

            ui.Log($"After Owner: {_spawned.OwnerPlayerId}");
        }

        if (_spawned != null)
        {
            if (GUILayout.Button("Destroy Network"))
            {
                RelayNetwork.Destroy(_spawned.NetworkId);
            }

            TestNetworkVariables vars = _spawned.GetComponent<TestNetworkVariables>();

            if (vars != null)
            {
                GUILayout.Space(10);

                GUILayout.Label("Health = " + vars.Health);

                if (GUILayout.Button("Damage (-10)"))
                {
                    vars.Health -= 10;

                    Log(
                        $"Changed NetworkVariable\n" +
                        $"Name: Health\n" +
                        $"Value: {vars.Health}\n" +
                        $"Owner: {_spawned.OwnerPlayerId}\n" +
                        $"Authority: Owner");
                }

                if (GUILayout.Button("Heal (+10)"))
                {
                    vars.Health += 10;

                    Log(
                        $"Changed NetworkVariable\n" +
                        $"Name: Health\n" +
                        $"Value: {vars.Health}\n" +
                        $"Owner: {_spawned.OwnerPlayerId}\n" +
                        $"Authority: Owner");
                }
            }
        }

        GUILayout.BeginHorizontal();

        if (GUILayout.Button("Up"))
            _spawned.transform.position += Vector3.forward;

        if (GUILayout.Button("Down"))
            _spawned.transform.position += Vector3.back;

        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();

        if (GUILayout.Button("Left"))
            _spawned.transform.position += Vector3.left;

        if (GUILayout.Button("Right"))
            _spawned.transform.position += Vector3.right;

        GUILayout.EndHorizontal();

        if (GUILayout.Button("Rotate"))
        {
            _spawned.transform.Rotate(0f, 45f, 0f);
        }

        if (GUILayout.Button("Raise Event 1 All"))
        {
            RelayNetwork.SendEvent(TestEventCode, Encoding.UTF8.GetBytes("hello"), EventTarget.All);
            Log("Raised event 1");
        }

        if (GUILayout.Button("Raise Event 1 Leader"))
        {
            RelayNetwork.SendEvent(TestEventCode, Encoding.UTF8.GetBytes("hello"), EventTarget.Leader);
            Log("Raised event 1");
        }

        if (GUILayout.Button("Raise Event 1 Others"))
        {
            RelayNetwork.SendEvent(TestEventCode, Encoding.UTF8.GetBytes("hello"), EventTarget.Others);
            Log("Raised event 1");
        }
    }

    private void DrawPlayersTab()
    {
        _scroll = GUILayout.BeginScrollView(_scroll);

        foreach (RelayPlayer player in RelayNetwork.ActivePlayers)
        {
            GUILayout.BeginVertical("box");

            GUILayout.Label(player.Username + (player.IsLeader ? " (Leader)" : ""));
            GUILayout.Label(player.UserId);

            foreach (var p in player.Properties)
                GUILayout.Label(p.Key + " = " + (p.Value ?? "null"));
            if (GUILayout.Button($"Drop Connection"))
            {
                RelayNetwork.DropConnection(player);
            }

            if (GUILayout.Button($"Add Interest"))
            {
                _spawned.SetPlayerInterested(player, true);
            }

            if (GUILayout.Button($"Remove Interest"))
            {
                _spawned.SetPlayerInterested(player, false);
            }

            GUILayout.EndVertical();
        }

        GUILayout.EndScrollView();
    }
}
#endif
