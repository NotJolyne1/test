#!/usr/bin/env python3
# Renders the game's sound effects into Assets/PolyRush/Resources/Audio.
# Needs numpy + scipy.  Run from the project folder:  python3 Tools/make_sounds.py
#
# Nothing here is sampled from anywhere, it's all synthesized. The engine is built from
# individual cylinder firings run through a few resonant filters (roughly how an exhaust
# behaves), then rendered at several rpm values so the game can crossfade between them.

import os
import wave
import numpy as np
from scipy import signal

SR = 44100
OUT = os.path.join(os.path.dirname(__file__), "..", "Assets", "PolyRush", "Resources", "Audio")
rng = np.random.default_rng(1234)


def save(name, x, peak=0.89):
    x = np.asarray(x, dtype=np.float64)
    m = np.max(np.abs(x)) or 1.0
    x = x / m * peak
    data = (np.clip(x, -1, 1) * 32767).astype("<i2")
    os.makedirs(OUT, exist_ok=True)
    with wave.open(os.path.join(OUT, name + ".wav"), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(data.tobytes())
    print("  %-14s %5.2fs" % (name, len(x) / SR))


def bandpass(x, lo, hi, order=2):
    b, a = signal.butter(order, [lo / (SR / 2), hi / (SR / 2)], btype="band")
    return signal.lfilter(b, a, x)


def lowpass(x, f, order=2):
    b, a = signal.butter(order, f / (SR / 2))
    return signal.lfilter(b, a, x)


def highpass(x, f, order=2):
    b, a = signal.butter(order, f / (SR / 2), btype="high")
    return signal.lfilter(b, a, x)


def resonator(x, f, q):
    b, a = signal.iirpeak(f / (SR / 2), q)
    return signal.lfilter(b, a, x)


def loop_render(make_one, n):
    # Render three copies of a periodic excitation through the filters and keep the
    # middle one, so the filter tails line up and the loop has no click.
    exc = make_one()
    tiled = np.concatenate([exc, exc, exc])
    y = make_filter_chain(tiled)
    return y[n:2 * n]


make_filter_chain = None


def fade(x, ms_in=4, ms_out=30):
    a = int(SR * ms_in / 1000)
    b = int(SR * ms_out / 1000)
    x = x.copy()
    if a:
        x[:a] *= np.linspace(0, 1, a)
    if b:
        x[-b:] *= np.linspace(1, 0, b)
    return x


def crossfade_loop(x, ms=60):
    n = int(SR * ms / 1000)
    head, body, tail = x[:n], x[n:-n], x[-n:]
    k = np.linspace(0, 1, n)
    return np.concatenate([body, tail * (1 - k) + head * k])


# ------------------------------------------------------------------ engine

ENGINE_RPMS = [900, 2000, 3200, 4500, 5800, 7200, 8600]


def engine(rpm):
    global make_filter_chain
    cycle = 120.0 / rpm          # 4-stroke: one full cycle every two revolutions
    cycles = max(4, int(round(1.6 / cycle)))
    n = int(round(cycles * cycle * SR))
    fire_gap = n / (cycles * 4)  # 4 cylinders
    pattern = np.array([1.0, 0.8, 0.94, 0.76])
    jitter = rng.normal(0, 0.012, cycles * 4)
    amps = rng.normal(1, 0.06, cycles * 4)
    bright = np.clip((rpm - 900) / 7700, 0, 1)

    def make():
        x = np.zeros(n)
        burst_len = int(SR * 0.006)
        t = np.arange(burst_len) / SR
        for k in range(cycles * 4):
            pos = (k + jitter[k]) * fire_gap
            i = int(pos) % n
            env = np.exp(-t / (0.0016 + 0.0012 * (1 - bright)))
            burst = (rng.normal(0, 1, burst_len) * 0.5 + 1.0) * env
            amp = pattern[k % 4] * amps[k]
            idx = (i + np.arange(burst_len)) % n
            x[idx] += burst * amp
        return x

    def chain(x):
        y = (resonator(x, 70 + rpm * 0.006, 1.4) * 1.0
             + resonator(x, 210 + rpm * 0.012, 2.2) * (0.7 + 0.3 * bright)
             + resonator(x, 620 + rpm * 0.03, 3.0) * (0.25 + 0.45 * bright)
             + resonator(x, 1700, 2.5) * (0.06 + 0.22 * bright))
        y += bandpass(rng.normal(0, 1, len(x)), 1800, 5200) * 0.004 * (0.3 + bright)
        y = lowpass(y, 2600 + 3200 * bright)
        y = np.tanh(y / (np.std(y) * 2.2 + 1e-9))
        return highpass(y, 35)

    make_filter_chain = chain
    return loop_render(make, n)


# ------------------------------------------------------------------ loops

def skid():
    n = SR * 2
    x = rng.normal(0, 1, n + SR)
    y = bandpass(x, 700, 2600, 2)
    chatter = 1 + 0.5 * np.sin(2 * np.pi * np.cumsum(rng.uniform(18, 34, n + SR)) / SR)
    t = np.arange(n + SR) / SR
    f = 1150 + 90 * np.sin(2 * np.pi * 0.7 * t) + 40 * np.sin(2 * np.pi * 2.3 * t)
    squeal = np.sin(2 * np.pi * np.cumsum(f) / SR)
    squeal = bandpass(squeal + rng.normal(0, 0.4, len(t)), 900, 1500)
    y = y * chatter * 0.8 + squeal * 0.3
    return crossfade_loop(y[SR // 2: SR // 2 + n])


def wind():
    n = SR * 3
    x = rng.normal(0, 1, n + SR)
    y = lowpass(x, 520, 2) + bandpass(x, 900, 2500) * 0.15
    t = np.arange(len(x)) / SR
    gust = 0.75 + 0.25 * np.sin(2 * np.pi * 0.31 * t) * np.sin(2 * np.pi * 0.13 * t + 1)
    return crossfade_loop((y * gust)[SR // 2: SR // 2 + n], 120)


def road():
    n = SR * 2
    x = rng.normal(0, 1, n + SR)
    y = lowpass(x, 180, 2) * 1.0 + bandpass(x, 300, 900) * 0.25
    return crossfade_loop(y[SR // 2: SR // 2 + n], 100)


def scrape():
    n = SR * 2
    x = rng.normal(0, 1, n + SR)
    y = bandpass(x, 1200, 5000) * 0.6 + bandpass(x, 200, 800) * 0.4
    crackle = (rng.random(n + SR) < 0.003) * rng.normal(0, 6, n + SR)
    y += highpass(crackle, 2000)
    return crossfade_loop(y[SR // 2: SR // 2 + n], 80)


# ------------------------------------------------------------------ one-shots

def bell(freq, dur, partials=((1, 1.0, 1.0), (2.76, 0.45, 2.2), (5.4, 0.25, 3.5), (8.9, 0.1, 5))):
    t = np.arange(int(SR * dur)) / SR
    y = np.zeros_like(t)
    for ratio, amp, decay in partials:
        y += amp * np.sin(2 * np.pi * freq * ratio * t) * np.exp(-t * decay * 3)
    attack = int(SR * 0.002)
    y[:attack] *= np.linspace(0, 1, attack)
    return y


def checkpoint():
    a = bell(1318.5, 0.9)
    b = bell(1760.0, 0.9)
    y = np.zeros(int(SR * 1.0))
    y[:len(a)] += a * 0.8
    off = int(SR * 0.075)
    y[off:off + len(b)] += b[:len(y) - off] * 0.7
    return fade(y, 1, 80)


def finish():
    notes = [523.25, 659.25, 783.99, 1046.5]
    y = np.zeros(int(SR * 2.2))
    for i, f in enumerate(notes):
        b = bell(f, 1.6) * (0.75 if i < 3 else 1.0)
        off = int(SR * 0.09 * i)
        y[off:off + len(b)] += b
    t = np.arange(len(y)) / SR
    pad = sum(np.sin(2 * np.pi * f * t) for f in (523.25, 659.25, 783.99)) * 0.08
    pad *= np.minimum(1, t * 6) * np.exp(-t * 1.6)
    return fade(y + pad, 1, 200)


def boost():
    n = int(SR * 0.9)
    t = np.arange(n) / SR
    x = rng.normal(0, 1, n)
    out = np.zeros(n)
    hop = 512
    for s in range(0, n - hop, hop):
        k = s / n
        f = 300 + 3200 * k ** 1.5
        seg = bandpass(x[max(0, s - 2048):s + hop], f * 0.7, f * 1.4)[-hop:]
        out[s:s + hop] = seg
    env = np.sin(np.pi * np.clip(t / 0.9, 0, 1)) ** 1.5
    thump = np.sin(2 * np.pi * 55 * t) * np.exp(-t * 14) * 0.8
    return fade(out * env + thump, 2, 60)


def tick(freq_lo, freq_hi, ms=12, gain=1.0):
    n = int(SR * ms / 1000)
    t = np.arange(n) / SR
    y = bandpass(rng.normal(0, 1, n), freq_lo, freq_hi) * np.exp(-t * 600)
    return fade(y * gain, 0, 3)


def land():
    n = int(SR * 0.5)
    t = np.arange(n) / SR
    y = np.sin(2 * np.pi * (55 + 40 * np.exp(-t * 30)) * t) * np.exp(-t * 9)
    y += lowpass(rng.normal(0, 1, n), 700) * np.exp(-t * 18) * 1.4
    return fade(y, 1, 60)


def hit():
    n = int(SR * 0.45)
    t = np.arange(n) / SR
    y = bandpass(rng.normal(0, 1, n), 250, 2200) * np.exp(-t * 22) * 1.3
    y += np.sin(2 * np.pi * 85 * t) * np.exp(-t * 16) * 0.7
    y += sum(np.sin(2 * np.pi * f * t) * np.exp(-t * d) for f, d in ((1230, 18), (1890, 24), (2710, 30))) * 0.12
    return fade(y, 0, 60)


def respawn():
    n = int(SR * 0.45)
    t = np.arange(n) / SR
    x = rng.normal(0, 1, n)
    y = bandpass(x, 400, 3000) * np.sin(np.pi * t / t[-1]) ** 2
    y += np.sin(2 * np.pi * (380 + 500 * t / t[-1]) * t) * 0.15 * np.sin(np.pi * t / t[-1])
    return fade(y, 2, 40)


def wrong():
    y = np.concatenate([bell(392, 0.18, ((1, 1, 6), (2, 0.3, 9))), bell(330, 0.3, ((1, 1, 5), (2, 0.3, 8)))])
    return fade(y, 1, 40)


def main():
    print("writing to", os.path.abspath(OUT))
    for r in ENGINE_RPMS:
        save("engine_%d" % r, engine(r), 0.8)
    save("skid", skid(), 0.7)
    save("wind", wind(), 0.7)
    save("road", road(), 0.7)
    save("scrape", scrape(), 0.6)
    save("checkpoint", checkpoint(), 0.7)
    save("finish", finish(), 0.75)
    save("boost", boost(), 0.75)
    save("click", tick(1800, 5000, 14), 0.5)
    save("hover", tick(2500, 6000, 8), 0.25)
    save("land", land(), 0.8)
    save("hit", hit(), 0.8)
    save("respawn", respawn(), 0.6)
    save("wrong", wrong(), 0.6)


if __name__ == "__main__":
    main()
