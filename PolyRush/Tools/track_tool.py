#!/usr/bin/env python3
"""
PolyRush track checker / previewer.

Parses the same track DSL the game uses (Assets/PolyRush/Resources/Tracks/*.txt),
samples the centre line exactly like TrackPath.cs, and then:
  * checks the track never overlaps itself (unless one part is a bridge high enough above the other)
  * checks the track never dips below ground level (y < 0)
  * simulates every jump (KICK + GAP) at a range of speeds and reports where the car lands
  * estimates a par time with the same speed model as TrackPath.EstimateParTime()
  * writes a top-down PNG preview next to the output folder (needs Pillow)

Usage:  python3 Tools/track_tool.py Assets/PolyRush/Resources/Tracks [out_dir]
"""
import math
import os
import sys

STEP = 1.0
HALF_WIDTH = 6.0
WALL = 0.7
G = 9.81
AIR_GRAVITY_MUL = 1.6   # must match CarController.airGravityMultiplier (+1)
MAX_SPEED = 62.0
ACCEL = 22.0
BRAKE = 28.0
LAT_MAX = 30.0


def smooth(t):
    return t * t * (3 - 2 * t)


def parse(path):
    meta = {}
    segs = []
    with open(path, encoding="utf-8") as f:
        for raw in f:
            line = raw.split("#", 1)[0].strip()
            if not line:
                continue
            if ":" in line and line.split(":", 1)[0].strip().isalpha() and " " not in line.split(":", 1)[0].strip():
                k, v = line.split(":", 1)
                meta[k.strip().lower()] = v.strip()
                continue
            toks = line.split()
            cmd = toks[0].upper()
            nums = []
            kv = {}
            for t in toks[1:]:
                if "=" in t:
                    a, b = t.split("=", 1)
                    kv[a.lower()] = float(b)
                else:
                    nums.append(float(t))
            segs.append((cmd, nums, kv))
    return meta, segs


def sample(segs):
    """Returns list of dict samples (x, y, z, yaw, gap, bankTarget) and markers."""
    x = z = y = 0.0
    yaw = 0.0
    pts = [dict(x=x, y=y, z=z, yaw=yaw, gap=False, bank=0.0)]
    cps = []
    boosts = []
    gaps = []

    def push(gap=False, bank=0.0):
        pts.append(dict(x=x, y=y, z=z, yaw=yaw, gap=gap, bank=bank))

    for cmd, n, kv in segs:
        if cmd in ("S", "UP", "DOWN"):
            length = n[0]
            rise = n[1] if len(n) > 1 else 0.0
            if cmd == "DOWN":
                rise = -abs(rise)
            cnt = max(1, int(round(length / STEP)))
            y0 = y
            for i in range(1, cnt + 1):
                t = i / cnt
                x += math.sin(math.radians(yaw)) * length / cnt
                z += math.cos(math.radians(yaw)) * length / cnt
                y = y0 + rise * smooth(t)
                push()
        elif cmd in ("L", "R"):
            ang = n[0] * (1 if cmd == "R" else -1)
            rad = n[1]
            rise = n[2] if len(n) > 2 else 0.0
            bank = kv.get("b", kv.get("bank", 0.0))
            arc = rad * math.radians(abs(ang))
            cnt = max(2, int(round(arc / STEP)))
            y0 = y
            yaw0 = yaw
            for i in range(1, cnt + 1):
                t = i / cnt
                ym = yaw0 + ang * (i - 0.5) / cnt
                x += math.sin(math.radians(ym)) * arc / cnt
                z += math.cos(math.radians(ym)) * arc / cnt
                yaw = yaw0 + ang * t
                y = y0 + rise * smooth(t)
                push(bank=-math.copysign(bank, ang))
        elif cmd == "KICK":
            length, rise = n[0], n[1]
            cnt = max(1, int(round(length / STEP)))
            y0 = y
            for i in range(1, cnt + 1):
                t = i / cnt
                x += math.sin(math.radians(yaw)) * length / cnt
                z += math.cos(math.radians(yaw)) * length / cnt
                y = y0 + rise * t * t
                push()
        elif cmd in ("LAND", "OUTRUN"):
            # LAND: parabolic landing slope (gets steeper, follows a jump arc)
            # OUTRUN: eases the slope back to flat
            length, drop = n[0], -abs(n[1])
            cnt = max(1, int(round(length / STEP)))
            y0 = y
            for i in range(1, cnt + 1):
                t = i / cnt
                x += math.sin(math.radians(yaw)) * length / cnt
                z += math.cos(math.radians(yaw)) * length / cnt
                if cmd == "LAND":
                    y = y0 + drop * t * t
                else:
                    y = y0 + drop * (1 - (1 - t) * (1 - t))
                push()
        elif cmd == "GAP":
            length = n[0]
            rise = n[1] if len(n) > 1 else 0.0
            cnt = max(1, int(round(length / STEP)))
            y0 = y
            gaps.append(len(pts) - 1)
            for i in range(1, cnt + 1):
                t = i / cnt
                x += math.sin(math.radians(yaw)) * length / cnt
                z += math.cos(math.radians(yaw)) * length / cnt
                y = y0 + rise * t
                push(gap=True)
        elif cmd == "CP":
            cps.append(len(pts) - 1)
        elif cmd == "BOOST":
            boosts.append((len(pts) - 1, n[0]))
        else:
            raise ValueError("unknown command " + cmd)
    return pts, cps, boosts, gaps


def check(name, pts, cps, boosts, gaps):
    errors = []
    import numpy as np
    xs = np.array([p["x"] for p in pts])
    zs = np.array([p["z"] for p in pts])
    ys = np.array([p["y"] for p in pts])
    if ys.min() < -0.01:
        errors.append("track goes below ground (min y %.2f)" % ys.min())
    n = len(pts)
    lim = 2 * (HALF_WIDTH + WALL) + 3.0
    worst = None
    for i in range(0, n, 2):
        j0 = i + 40
        if j0 >= n:
            break
        dx = xs[j0:] - xs[i]
        dz = zs[j0:] - zs[i]
        dy = np.abs(ys[j0:] - ys[i])
        d = np.sqrt(dx * dx + dz * dz)
        bad = (d < lim) & (dy < 7.5)
        if bad.any():
            j = j0 + int(np.argmax(bad))
            worst = (i, j, float(d[j - j0]), float(dy[j - j0]))
            break
    if worst:
        errors.append("self overlap between %dm and %dm (dist %.1f, dy %.1f)" % worst)
    return errors


def jump_report(pts, gaps):
    out = []
    for gi in gaps:
        p0 = pts[gi]
        pm = pts[gi - 1]
        slope = (p0["y"] - pm["y"]) / STEP
        ang = math.atan(slope)
        # find road after gap
        k = gi + 1
        while k < len(pts) and pts[k]["gap"]:
            k += 1
        land_start = k
        res = []
        for v in (30, 40, 50, 60, 70):
            vx = v * math.cos(ang)
            vy = v * math.sin(ang)
            g = G * AIR_GRAVITY_MUL
            # walk along samples after takeoff
            landed = None
            for s in range(gi + 1, min(len(pts), gi + 400)):
                dist = (s - gi) * STEP
                t = dist / vx
                cy = p0["y"] + vy * t - 0.5 * g * t * t
                road = pts[s]
                if not road["gap"] and cy <= road["y"] + 0.05:
                    vyy = vy - g * t
                    rs = (pts[min(s + 1, len(pts) - 1)]["y"] - road["y"]) / STEP
                    rel = math.degrees(math.atan2(vyy, vx) - math.atan(rs))
                    landed = (s - land_start, rel)
                    break
                if road["gap"] and cy < road["y"] - 12:
                    break
            res.append((v, landed))
        out.append((gi, land_start - gi, res))
    return out


def par_time(pts, boosts):
    n = len(pts)
    curv = [0.0] * n
    for i in range(1, n - 1):
        dyaw = (pts[i + 1]["yaw"] - pts[i - 1]["yaw"])
        curv[i] = abs(math.radians(dyaw)) / (2 * STEP)
    vmax = []
    boost_set = set()
    for (b, l) in boosts:
        for k in range(b, min(n, b + int(l))):
            boost_set.add(k)
    for i in range(n):
        c = max(curv[max(0, i - 3):i + 4])
        lim = MAX_SPEED if c < 1e-4 else min(MAX_SPEED + 15, math.sqrt(LAT_MAX / c))
        vmax.append(lim)
    v = [0.0] * n
    for i in range(1, n):
        acc = ACCEL * max(0.05, 1 - (v[i - 1] / MAX_SPEED) ** 2)
        if (i in boost_set):
            acc += 30
        v[i] = min(vmax[i], math.sqrt(v[i - 1] ** 2 + 2 * acc * STEP))
    for i in range(n - 2, -1, -1):
        v[i] = min(v[i], math.sqrt(v[i + 1] ** 2 + 2 * BRAKE * STEP))
    t = 0.0
    for i in range(1, n - 70):
        t += STEP / max(1.0, 0.5 * (v[i] + v[i - 1]))
    return t, v


def render(path_png, pts, cps, boosts, gaps):
    from PIL import Image, ImageDraw
    xs = [p["x"] for p in pts]
    zs = [p["z"] for p in pts]
    minx, maxx, minz, maxz = min(xs), max(xs), min(zs), max(zs)
    pad = 40
    size = 900
    span = max(maxx - minx, maxz - minz) + 2 * pad
    sc = size / span
    img = Image.new("RGB", (size, size), (110, 170, 90))
    d = ImageDraw.Draw(img)
    maxy = max(1.0, max(p["y"] for p in pts))

    def P(p):
        return ((p["x"] - minx + pad) * sc, size - (p["z"] - minz + pad) * sc)

    for i in range(1, len(pts)):
        if pts[i]["gap"]:
            continue
        h = pts[i]["y"] / maxy
        col = (int(90 + 165 * h), int(90 + 60 * h), int(100 - 40 * h))
        d.line([P(pts[i - 1]), P(pts[i])], fill=col, width=max(2, int(12 * sc)))
    for c in cps:
        x, y = P(pts[c])
        d.ellipse([x - 6, y - 6, x + 6, y + 6], fill=(40, 160, 255))
    for (b, l) in boosts:
        x, y = P(pts[b])
        d.rectangle([x - 4, y - 4, x + 4, y + 4], fill=(255, 140, 0))
    x, y = P(pts[0])
    d.ellipse([x - 8, y - 8, x + 8, y + 8], fill=(255, 255, 255))
    x, y = P(pts[-1])
    d.ellipse([x - 8, y - 8, x + 8, y + 8], fill=(0, 0, 0))
    d.text((10, 10), "%.0fm  maxY %.1f" % (len(pts) * STEP, maxy), fill=(0, 0, 0))
    img.save(path_png)


def main():
    folder = sys.argv[1]
    out = sys.argv[2] if len(sys.argv) > 2 else None
    ok = True
    for fn in sorted(os.listdir(folder)):
        if not fn.endswith(".txt"):
            continue
        meta, segs = parse(os.path.join(folder, fn))
        pts, cps, boosts, gaps = sample(segs)
        errs = check(fn, pts, cps, boosts, gaps)
        par, _ = par_time(pts, boosts)
        xs = [p["x"] for p in pts]
        zs = [p["z"] for p in pts]
        print("%-28s len %5dm  cps %d  boosts %d  jumps %d  par %.1fs  size %.0fx%.0f  maxY %.1f" % (
            fn, len(pts) * STEP, len(cps), len(boosts), len(gaps), par,
            max(xs) - min(xs), max(zs) - min(zs), max(p["y"] for p in pts)))
        for e in errs:
            ok = False
            print("   ERROR:", e)
        for gi, glen, res in jump_report(pts, gaps):
            s = ", ".join("%dm/s->%s" % (v, ("%+dm %.0fdeg" % l if l else "MISS")) for v, l in res)
            print("   jump at %dm gap %dm: %s" % (gi, glen, s))
        if out:
            os.makedirs(out, exist_ok=True)
            render(os.path.join(out, fn.replace(".txt", ".png")), pts, cps, boosts, gaps)
    sys.exit(0 if ok else 1)


if __name__ == "__main__":
    main()
