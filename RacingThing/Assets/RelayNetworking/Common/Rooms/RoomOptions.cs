using System.Collections.Generic;

namespace RelayNetworking.Common.Rooms
{
    public sealed class RoomOptions
    {
        public int MaxPlayers { get; set; } = 10;
        public bool IsOpen { get; set; } = true;
        public bool IsVisible { get; set; } = true;
        public Dictionary<string, object> CustomProperties { get; set; } = new();
    }
}