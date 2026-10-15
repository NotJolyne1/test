namespace RelayNetworking.Common.Collections
{
    public sealed class RoomProperties : PropertyCollection
    {
        public const string CurrentSceneKey = "CurrentScene";

        public string CurrentScene => Get<string>(CurrentSceneKey) ?? string.Empty;
    }
}