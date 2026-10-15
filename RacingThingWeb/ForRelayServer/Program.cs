using RelayNetworking.Common.Protocol;
using RelayNetworking.Server.Transport;

RelayServerOptions options = new();
RelayServer server = new(options);

server.Start();

// browser players (the web version of Racing Thing) connect over WebSockets on port 8080
RelayNetworking.Server.Web.WebRelay.Start(8080);

Console.WriteLine("[Server] Running");

while (true)
{
    try
    {
        server.Poll();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Server] {ex}");
    }

    Thread.Sleep(15);
}