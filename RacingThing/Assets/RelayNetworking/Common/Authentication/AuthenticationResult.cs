public sealed class AuthenticationResult
{
    public bool Success { get; init; }

    public string UserId { get; set; } = string.Empty;

    public string Username { get; init; } = string.Empty;

    public string? ErrorMessage { get; init; }
}