namespace RelayNetworking.Client.Transport
{
    public sealed class RelayClientOptions
    {
        internal const string TestingServerIP = "73.213.159.220";
        public string Host { get; set; } = TestingServerIP;
        public int Port { get; set; } = 7777;
        public string ConnectionKey { get; set; } = "RelayNetworking";
        public double TimeSyncIntervalSeconds { get; set; } = 5.0;
        public double TimeSyncTimeoutSeconds { get; set; } = 2.0;
        public bool AutoReconnect { get; set; } = true;
        public double ReconnectWindowSeconds { get; set; } = 15.0;
        public double ReconnectInitialDelaySeconds { get; set; } = 0.5;
        public double ReconnectMaxDelaySeconds { get; set; } = 3.0;
        public double ReclamationConfirmTimeoutSeconds { get; set; } = 3.0;
    }
}