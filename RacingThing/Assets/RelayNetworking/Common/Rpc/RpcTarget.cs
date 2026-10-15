using System;

namespace RelayNetworking.Common.Rpc
{
    public enum RpcTarget : byte
    {
        /// <summary>
        /// Sends to everyone, including the sender
        /// </summary>
        All,
        /// <summary>
        /// Sends to everyone except the sender
        /// </summary>
        Others,
        /// <summary>
        /// Sends only to the current room leader
        /// </summary>
        Leader,
        /// <summary>
        /// Sends to everyone and replays it for players who join later
        /// </summary>
        AllBuffered,
        /// <summary>
        /// Sends to everyone except the sender and replays it for players who join later
        /// </summary>
        OthersBuffered
    }
}