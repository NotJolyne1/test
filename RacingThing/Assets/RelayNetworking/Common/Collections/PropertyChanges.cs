using RelayNetworking.Common.Collections;
using System.Collections.Generic;

public sealed class PropertyChanges
{
    internal Dictionary<ReservedPlayerProperty, object?> Reserved { get; } = new();
    public bool Empty => Reserved.Count == 0 && Custom.Count == 0;

    internal Dictionary<string, object?> Custom { get; } = new();

    public string Username
    {
        set => Reserved[ReservedPlayerProperty.Username] = value;
    }

    public object? this[string key]
    {
        set => Custom[key] = value;
    }
}