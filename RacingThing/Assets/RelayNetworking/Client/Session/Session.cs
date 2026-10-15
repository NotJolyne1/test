using System.Collections.Generic;
using System.Linq;
using RelayNetworking.Common;
using RelayNetworking.Common.Rooms;

namespace RelayNetworking.Client.Session
{
    public sealed class Session
    {
        internal Session()
        {
        }
        public RelayPlayer? LocalPlayer { get; internal set; }
        public Room? CurrentRoom { get; internal set; }
        public int Ping { get; internal set; }
        public bool Connected { get; internal set; }
        public bool IsLeader => LocalPlayer?.IsLeader ?? false;
        public bool InRoom => CurrentRoom != null;
        public RelayPlayer? Leader => CurrentRoom?.Leader;
        private readonly List<RelayPlayer> _players = new();
        public IReadOnlyList<RelayPlayer> ActivePlayers => _players;
        internal void AddPlayer(RelayPlayer player)
        {
            if (_players.Any(x => x.PlayerId == player.PlayerId))
                return;
            _players.Add(player);
        }
        internal bool RemovePlayer(int playerId)
        {
            RelayPlayer? player = _players.FirstOrDefault(x => x.PlayerId == playerId);
            if (player == null)
                return false;
            _players.Remove(player);
            return true;
        }
        internal void ClearPlayers()
        {
            _players.Clear();
        }
        internal void Reset()
        {
            LocalPlayer = null;
            CurrentRoom = null;
            Ping = 0;
            Connected = false;
            _players.Clear();
        }
    }
}