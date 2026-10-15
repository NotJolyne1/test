# Racing Thing (web)

The whole game is in one file, `index.html`: all 7 tracks, ghosts, medals, settings, sounds
and online races. Nothing to install to play on your own.

## Playing on your own

Double-click `index.html`. It opens in your browser. Chrome, Edge or Firefox all work.

## Playing with friends

You need [Node.js](https://nodejs.org) (the LTS one) on the computer that hosts. Nothing else:
no `npm install`.

1. Keep `server.js` and `index.html` in the same folder.
2. Double-click `start-server.bat`, or open a terminal in this folder and run `node server.js`.
3. It prints the addresses to use, like:

   ```
   On this computer open:     http://localhost:8080
   Other computers open:      http://10.18.98.171:8080
   ```

4. You open the first address. Friends open the second one in their browser, on the same
   Wi-Fi as you.
5. Click **Multiplayer**, then **Host a game**. You get a room code. Friends type the code and
   click **Join**.
6. The host picks the track and presses **Start race**. Everyone loads in, there's a 3-2-1
   countdown, and the first one over the line wins. Times show up when everyone finishes,
   or 30 seconds after the winner.

The host can kick players or make someone else host from the player list. If the host
leaves, someone else becomes host automatically.

Want a different port? Run `node server.js 3000`.

### If friends can't connect

- Windows may ask whether Node can use the network the first time. Click **Allow**, and
  tick private networks.
- Some school and work Wi-Fi blocks devices from talking to each other. Then nothing on
  that network will work. A free tunnel like [playit.gg](https://playit.gg) or
  [Tailscale](https://tailscale.com) gets around it, with no router changes needed. Point it at port 8080.

## Controls

| Key | |
|---|---|
| W / Up | Accelerate |
| S / Down | Brake / reverse |
| A D / Left Right | Steer |
| Space / Shift | Drift |
| Enter | Back to the last checkpoint |
| R | Restart (online: last checkpoint) |
| C | Change camera |
| H | Horn (online) |
| Esc | Pause / menu |

A gamepad works too.

Built with [three.js](https://threejs.org) (MIT licence, included inside `index.html`).
