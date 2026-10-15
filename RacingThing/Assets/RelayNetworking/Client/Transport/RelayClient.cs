using LiteNetLib;
using RelayNetworking.Client.Protocol.Handlers;
using RelayNetworking.Common.Protocol;
using RelayNetworking.Common.Protocol.Networking;
using RelayNetworking.Common.Serialization;

namespace RelayNetworking.Client.Transport
{
    public sealed class RelayClient
    {
        private readonly RelayListener _listener;
        private readonly NetManager _client;
        private readonly NetworkEventHandler _eventHandler;
        private NetPeer? _server;

        public RelayClient()
        {
            _listener = new RelayListener();
            _client = new NetManager(_listener);
            _eventHandler = new();
            _listener.PeerConnectedEvent += peer =>
            {
                _server = peer;
                RelayNetwork.Logger?.Invoke("[Client] Connected");
                RelayNetwork.TransportConnected();
            };
            _listener.PeerDisconnectedEvent += (_, info) =>
            {
                RelayNetwork.Logger?.Invoke("[Client] Disconnected");
                RelayNetwork.Logger?.Invoke("[Client] Reason: " + info.Reason);
                RelayNetwork.Logger?.Invoke("[Client] Socket Error: " + info.SocketErrorCode);
                _server = null;
                RelayNetwork.TransportDisconnected();
            };
            _listener.NetworkReceiveEvent += (_, reader, _, _) =>
            {
                NetworkCommandReader packet = new(reader);
                PacketHeader header = packet.ReadHeader();
                _eventHandler.Handle(header, packet);
                reader.Recycle();
            };
            _listener.NetworkErrorEvent += (endPoint, error) =>
            {
                RelayNetwork.Logger?.Invoke("[Client] Network Error: " + error);
                RelayNetwork.Logger?.Invoke("[Client] EndPoint: " + endPoint);
            };
            _listener.NetworkLatencyUpdateEvent += (peer, latency) =>
            {
                RelayNetwork.Logger?.Invoke("[Client] Ping: " + latency + "ms");
            };
            _listener.ConnectionRequestEvent += request =>
            {
                RelayNetwork.Logger?.Invoke("[Client] Connection request received");
            };
            _listener.NetworkReceiveUnconnectedEvent += (endPoint, reader, type) =>
            {
                RelayNetwork.Logger?.Invoke("[Client] Unconnected packet from " + endPoint + " (" + type + ")");
                reader.Recycle();
            };
        }

        public void Start()
        {
            if (!_client.IsRunning)
                _client.Start();
        }

        public void Connect(RelayClientOptions options)
        {
            Start();
            RelayNetwork.Logger?.Invoke($"[Client] Connecting to {options.Host}:{options.Port}");
            _client.Connect(options.Host, options.Port, options.ConnectionKey);
        }

        public void Disconnect()
        {
            if (_server != null && _server.ConnectionState == ConnectionState.Connected)
                _server.Disconnect();

            _server = null;

            if (_client.IsRunning)
                _client.Stop();
        }

        public void Poll()
        {
            _client.PollEvents();
        }

        public bool Send(INetworkCommand command, DeliveryMethod method = DeliveryMethod.ReliableOrdered)
        {
            if (_server == null || !Connected)
                return false;
            _server.Send(NetworkCommandSerializer.Serialize(command), method);
            return true;
        }

        public bool Send(NetworkCommandWriter writer, DeliveryMethod method = DeliveryMethod.ReliableOrdered)
        {
            if (_server == null || !Connected)
                return false;
            _server.Send(writer.Writer, method);
            return true;
        }

        public bool Connected => _server != null && _server.ConnectionState == ConnectionState.Connected;
    }
}