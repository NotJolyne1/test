using System.Collections.Generic;
using System.Linq;
using RelayNetworking.Common.Callbacks;
using RelayNetworking.Common.Collections;

namespace RelayNetworking.Common.Rooms
{
    public sealed class Room
    {
        public Room()
        {
        }
        public string Name { get; set; } = string.Empty;
        public RelayPlayer? Leader { get; set; }
        public RoomProperties Properties { get; } = new();
        public string CurrentScene => Properties.CurrentScene;
        public string AppId { get; internal set; } = string.Empty;
        public int MaxPlayers { get; internal set; } = 10;
        public bool IsOpen { get; internal set; } = true;
        public bool IsVisible { get; internal set; } = true;
        public IReadOnlyList<RelayPlayer> ActivePlayers => _players;
        private readonly List<RelayPlayer> _players = new();

        public bool AddPlayer(RelayPlayer player)
        {
            if (_players.Contains(player))
                return false;
            _players.Add(player);
            if (Leader == null)
            {
                Leader = player;
            }
            foreach (RelayPlayer other in _players)
            {
                other.IsLeader = other == Leader;
            }
            RelayCallbacks.OnPlayerJoined(player);
            return true;
        }

        public bool RemovePlayer(RelayPlayer player)
        {
            if (!_players.Remove(player))
                return false;
            if (Leader == player)
                Leader = _players.FirstOrDefault();
            foreach (RelayPlayer other in _players)
            {
                other.IsLeader = other == Leader;
            }
            player.IsLeader = false;
            RelayCallbacks.OnPlayerLeft(player);
            return true;
        }
    }
}