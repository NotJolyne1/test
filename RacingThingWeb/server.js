// Racing Thing multiplayer server.
//
//   node server.js            (port 8080)
//   node server.js 3000       (any other port)
//
// It serves the game (index.html next to this file) and relays rooms over WebSockets.
// Needs Node.js 16 or newer and nothing else: no npm install.
//
// Everyone opens http://<this computer's address>:8080 in a browser. One person hosts and
// gets a room code, the others type it in.

'use strict';

const http = require('http');
const crypto = require('crypto');
const fs = require('fs');
const path = require('path');
const os = require('os');

const PORT = parseInt(process.argv[2] || process.env.PORT || '8080', 10);
const MAX_PLAYERS = 8;
const CODE_LETTERS = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789';
const PAGE = path.join(__dirname, 'index.html');

// ------------------------------------------------------------------ rooms

const rooms = new Map();     // code -> room
let nextId = 1;

function newCode() {
  for (;;) {
    let c = '';
    for (let i = 0; i < 5; i++) c += CODE_LETTERS[crypto.randomInt(CODE_LETTERS.length)];
    if (!rooms.has(c)) return c;
  }
}

function playerInfo(p) { return { id: p.id, name: p.name, props: p.props }; }

function roomState(room, you) {
  return {
    t: 'joined', code: room.code, you: you.id, host: room.host,
    players: [...room.players.values()].map(playerInfo), props: room.props,
  };
}

function toRoom(room, msg, except) {
  const data = JSON.stringify(msg);
  for (const p of room.players.values()) if (p !== except) p.sendRaw(data);
}

function leaveRoom(p, reason) {
  const room = p.room;
  if (!room) return;
  room.players.delete(p.id);
  p.room = null;
  if (room.players.size === 0) {
    rooms.delete(room.code);
    log(`room ${room.code} closed`);
    return;
  }
  toRoom(room, { t: 'left', id: p.id, reason: reason || 'left' });
  if (room.host === p.id) {
    room.host = room.players.keys().next().value;
    toRoom(room, { t: 'host', id: room.host });
  }
}

// ------------------------------------------------------------------ messages from players

const limits = { chat: [5, 3000], props: [20, 1000], msg: [80, 1000] };

function allowed(p, kind) {
  const [n, ms] = limits[kind];
  const now = Date.now();
  const w = p.windows[kind] || (p.windows[kind] = []);
  while (w.length && now - w[0] > ms) w.shift();
  if (w.length >= n) return false;
  w.push(now);
  return true;
}

function cleanName(s) {
  s = String(s || '').replace(/[\u0000-\u001f<>]/g, '').trim().slice(0, 16);
  return s || 'Racer' + (100 + crypto.randomInt(900));
}

function cleanChanges(c) {
  const out = {};
  if (!c || typeof c !== 'object') return out;
  for (const [k, v] of Object.entries(c)) {
    if (k.length > 32) continue;
    out[k] = v === undefined ? null : v;
  }
  return out;
}

function handle(p, msg) {
  if (!allowed(p, 'msg') && msg.t !== 'car') return;
  const room = p.room;
  const isHost = room && room.host === p.id;
  switch (msg.t) {
    case 'hello':
      p.name = cleanName(msg.name);
      p.send({ t: 'welcome', id: p.id, time: Date.now() });
      break;

    case 'ping':
      p.send({ t: 'pong', c: msg.c, s: Date.now() });
      break;

    case 'create': {
      if (room) leaveRoom(p);
      const r = { code: newCode(), host: p.id, players: new Map(), props: cleanChanges(msg.props) };
      r.players.set(p.id, p);
      rooms.set(r.code, r);
      p.room = r;
      p.props = cleanChanges(msg.me);
      p.send(roomState(r, p));
      log(`${p.name} made room ${r.code}`);
      break;
    }

    case 'join': {
      const code = String(msg.code || '').toUpperCase().replace(/[^A-Z0-9]/g, '');
      const r = rooms.get(code);
      if (!r) return p.send({ t: 'error', what: 'join', reason: 'No room with the code ' + code + '.' });
      if (r.players.size >= MAX_PLAYERS) return p.send({ t: 'error', what: 'join', reason: 'That room is full.' });
      if (room) leaveRoom(p);
      p.props = cleanChanges(msg.me);
      r.players.set(p.id, p);
      p.room = r;
      p.send(roomState(r, p));
      toRoom(r, { t: 'joinedRoom', player: playerInfo(p) }, p);
      log(`${p.name} joined ${r.code} (${r.players.size} in it)`);
      break;
    }

    case 'leave':
      leaveRoom(p);
      break;

    case 'kick': {
      if (!isHost) return;
      const target = room.players.get(msg.id);
      if (!target || target === p) return;
      target.send({ t: 'kicked' });
      leaveRoom(target, 'kicked');
      break;
    }

    case 'makeHost':
      if (!isHost || !room.players.has(msg.id)) return;
      room.host = msg.id;
      toRoom(room, { t: 'host', id: room.host });
      break;

    case 'props': {
      // room properties: only the host can change them
      if (!isHost || !allowed(p, 'props')) return;
      const c = cleanChanges(msg.c);
      for (const [k, v] of Object.entries(c)) { if (v === null) delete room.props[k]; else room.props[k] = v; }
      if (JSON.stringify(room.props).length > 16000) return;
      toRoom(room, { t: 'props', c });
      break;
    }

    case 'me': {
      if (!room || !allowed(p, 'props')) return;
      const c = cleanChanges(msg.c);
      for (const [k, v] of Object.entries(c)) { if (v === null) delete p.props[k]; else p.props[k] = v; }
      toRoom(room, { t: 'me', id: p.id, c });
      break;
    }

    case 'chat': {
      if (!room || !allowed(p, 'chat')) return;
      const text = String(msg.text || '').slice(0, 100).trim();
      if (text) toRoom(room, { t: 'chat', id: p.id, text });
      break;
    }

    case 'event':
      if (room) toRoom(room, { t: 'event', from: p.id, kind: String(msg.kind || '').slice(0, 20), data: msg.data }, p);
      break;

    case 'car':
      // where your car is, ~20 times a second, passed straight on to everyone else
      if (room) toRoom(room, { t: 'car', id: p.id, p: msg.p, q: msg.q, c: msg.c, r: msg.r }, p);
      break;

    case 'honk':
      if (room && allowed(p, 'chat')) toRoom(room, { t: 'honk', id: p.id }, p);
      break;
  }
}

// ------------------------------------------------------------------ a tiny WebSocket server
// Just enough of RFC 6455 for browsers: text frames, ping/pong, close.

class Player {
  constructor(socket) {
    this.socket = socket;
    this.id = nextId++;
    this.name = 'Racer';
    this.props = {};
    this.room = null;
    this.windows = {};
    this.buffer = Buffer.alloc(0);
    this.alive = true;
  }

  send(obj) { this.sendRaw(JSON.stringify(obj)); }

  sendRaw(text) {
    if (this.socket.destroyed) return;
    const data = Buffer.from(text);
    let head;
    if (data.length < 126) head = Buffer.from([0x81, data.length]);
    else if (data.length < 65536) { head = Buffer.alloc(4); head[0] = 0x81; head[1] = 126; head.writeUInt16BE(data.length, 2); }
    else { head = Buffer.alloc(10); head[0] = 0x81; head[1] = 127; head.writeBigUInt64BE(BigInt(data.length), 2); }
    this.socket.write(Buffer.concat([head, data]));
  }

  control(op, payload = Buffer.alloc(0)) {
    if (!this.socket.destroyed) this.socket.write(Buffer.concat([Buffer.from([0x80 | op, payload.length]), payload]));
  }

  feed(chunk) {
    this.buffer = Buffer.concat([this.buffer, chunk]);
    for (;;) {
      const b = this.buffer;
      if (b.length < 2) return;
      const op = b[0] & 0x0f, masked = b[1] & 0x80;
      let len = b[1] & 0x7f, off = 2;
      if (len === 126) { if (b.length < 4) return; len = b.readUInt16BE(2); off = 4; }
      else if (len === 127) { if (b.length < 10) return; len = Number(b.readBigUInt64BE(2)); off = 10; }
      if (len > 65536) return this.socket.destroy();
      const need = off + (masked ? 4 : 0) + len;
      if (b.length < need) return;
      let payload = b.subarray(off + (masked ? 4 : 0), need);
      if (masked) {
        const mask = b.subarray(off, off + 4);
        payload = Buffer.from(payload);
        for (let i = 0; i < payload.length; i++) payload[i] ^= mask[i & 3];
      }
      this.buffer = b.subarray(need);
      if (op === 0x8) { this.control(0x8); this.socket.end(); return; }
      if (op === 0x9) { this.control(0xA, payload); continue; }
      if (op === 0xA) { this.alive = true; continue; }
      if (op === 0x1) {
        let msg;
        try { msg = JSON.parse(payload.toString('utf8')); } catch (e) { continue; }
        if (msg && typeof msg.t === 'string') {
          try { handle(this, msg); } catch (e) { log('error: ' + e.message); }
        }
      }
    }
  }
}

const server = http.createServer((req, res) => {
  if (req.url === '/' || req.url.startsWith('/index.html') || req.url.startsWith('/?')) {
    fs.readFile(PAGE, (err, data) => {
      if (err) { res.writeHead(404); res.end('index.html is missing next to server.js'); return; }
      res.writeHead(200, { 'Content-Type': 'text/html; charset=utf-8', 'Cache-Control': 'no-cache' });
      res.end(data);
    });
    return;
  }
  res.writeHead(404);
  res.end();
});

server.on('upgrade', (req, socket) => {
  const key = req.headers['sec-websocket-key'];
  if (!key) return socket.destroy();
  const accept = crypto.createHash('sha1').update(key + '258EAFA5-E914-47DA-95CA-C5AB0DC85B11').digest('base64');
  socket.write('HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: ' + accept + '\r\n\r\n');
  socket.setNoDelay(true);
  const p = new Player(socket);
  players.add(p);
  socket.on('data', d => p.feed(d));
  const gone = () => {
    if (!players.has(p)) return;
    players.delete(p);
    if (p.room) log(`${p.name} disconnected`);
    leaveRoom(p, 'disconnected');
  };
  socket.on('close', gone);
  socket.on('error', gone);
});

const players = new Set();

// drop connections that stopped answering pings
setInterval(() => {
  for (const p of players) {
    if (!p.alive) { p.socket.destroy(); continue; }
    p.alive = false;
    p.control(0x9);
  }
}, 15000);

function log(s) { console.log(new Date().toLocaleTimeString() + '  ' + s); }

server.listen(PORT, () => {
  console.log('Racing Thing server is running.\n');
  console.log('On this computer open:     http://localhost:' + PORT);
  const nets = os.networkInterfaces();
  for (const list of Object.values(nets))
    for (const n of list || [])
      if (n.family === 'IPv4' && !n.internal) console.log('Other computers open:      http://' + n.address + ':' + PORT);
  console.log('\nLeave this window open while you play. Ctrl+C stops the server.\n');
});
server.on('error', e => {
  if (e.code === 'EADDRINUSE') console.log('Port ' + PORT + ' is already in use. Is the server already running? Or try: node server.js 8081');
  else console.log(e.message);
  process.exit(1);
});
