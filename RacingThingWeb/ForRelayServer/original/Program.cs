using RelayNetworking.Common.Protocol;
using RelayNetworking.Server.Transport;

RelayServerOptions options = new();
RelayServer server = new(options);

server.Start();

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