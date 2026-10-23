Racing Thing: browser players for the relay server
==================================================

Files in this zip:
  WebRelay.cs   -> put in your RelayNetworking.Server folder (next to Program.cs)
  index.html    -> put in the same folder (the one you run "dotnet run" in)

Nothing else changes. Don't edit Program.cs.

1. Copy WebRelay.cs and index.html into RelayNetworking.Server.
2. Run the server as normal:   dotnet run
3. Look for this line in the console:
       [Web] Browser players: open http://<this computer's address>:8080
4. Everyone opens  http://10.18.98.171:8080  -> Multiplayer -> Host or Join with the code.

If the [Web] line does NOT appear:
  Add this one line to Program.cs right after  server.Start();
      RelayNetworking.Server.Web.WebRelay.Start(8080);
  and run it again.

If Windows asks whether dotnet can use the network, click Allow.
Browsers can only reach the server on port 8080, and only over the same network.
