using System;

public sealed class AuthenticationValues
{
    public AuthenticationType Type { get; set; } = AuthenticationType.None;

    public string Username { get; set; } = "Player";

    public byte[] Data { get; set; } = Array.Empty<byte>();
}