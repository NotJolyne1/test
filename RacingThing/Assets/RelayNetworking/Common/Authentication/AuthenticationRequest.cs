public sealed class AuthenticationRequest
{
    public string AppId { get; init; } = string.Empty;
    public AuthenticationType Type { get; init; }
    public byte[] Data { get; init; } = System.Array.Empty<byte>();
    public string Username { get; init; } = string.Empty;
}