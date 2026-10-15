namespace RelayNetworking.Common.Authentication
{
    public enum AuthenticationResultCode : byte
    {
        Success,
        InvalidCredentials,
        InvalidToken,
        UnsupportedProvider,
        ServerError
    }
}