using RelayNetworking.Common.Data;
using System;

internal sealed class BufferedRpc
{
    public RpcData Rpc { get; init; }

    public DateTime Created { get; init; }
}