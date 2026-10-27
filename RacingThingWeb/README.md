# Race (web)

The whole game is one file, `index.html`: all 13 tracks, ghosts, medals, settings, sounds and
online races.

- **On your own:** double-click `index.html`.
- **Online, on your relay server:** put `ForRelayServer/WebRelay.cs` in your RelayNetworking.Server folder (see `ForRelayServer/HOW_TO_INSTALL.txt`). It adds a
  browser port (8080) to your RelayNetworking server. Then everyone opens
  `http://10.18.98.171:8080`, clicks Multiplayer, and hosts or joins with a room code.
- **Online without the relay server:** `node server.js` does the same job on any computer
  with Node.js installed (or double-click `start-server.bat`).

The host picks the track, starts the race, can kick players or hand over host. Results show
who won by name.

Controls: W/Up accelerate, S/Down brake, A D steer, Space/Shift drift, Enter back to the last
checkpoint, R restart, C camera, H horn (online), Esc menu. A gamepad works too.

Built with three.js (MIT licence, included inside index.html).
