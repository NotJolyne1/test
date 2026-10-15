using RelayNetworking.Common.Collections;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("RelayNetworking.Server")]
[assembly: InternalsVisibleTo("RelayNetworking.Client")]

namespace RelayNetworking.Common
{
    public sealed class RelayPlayer
    {
        public int PlayerId { get; internal set; }
        public string UserId { get; internal set; } = string.Empty;
        public bool IsLeader { get; internal set; }
        public PlayerProperties Properties { get; } = new();
        internal uint PlayerObjectId { get; set; }

        public string Username
        {
            get => Properties.Username;
            internal set => Properties.Username = value;
        }
    }
}