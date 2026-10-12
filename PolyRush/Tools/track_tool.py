#!/usr/bin/env python3
# Checks the track files and draws a little map of each one.
#
#   python3 Tools/track_tool.py Assets/PolyRush/Resources/Tracks [preview_dir]
#
# The sampling here has to match TrackPath.cs, otherwise the checks are meaningless.

import math
import os
import sys

TILE = 10.0
LEVEL = 2.5
HALF_WIDTH = 4.5
WALL = 0.5
LOOP_RADIUS = 12.0
AIR_G = 9.81 * 1.6


def smooth(t):
    return t * t * (3 - 2 * t)


def parse(path):
    meta, cmds = {}, []
    for raw in open(path, encoding="utf-8"):
        line = raw.split("#", 1)[0].strip()
        if not line:
            continue
        if ":" in line:
            k, v = line.split(":", 1)
            meta[k.strip().lower()] = v.strip()
            continue
        parts = line.split()
        cmds.append((parts[0].lower(), parts[1:]))
    return meta, cmds


def num(args, i, default):
    vals = [a for a in args if a.replace(".", "", 1).isdigit()]
    return float(vals[i]) if i < len(vals) else default


def opt(args, key, default=0.0):
    for a in args:
        if a.startswith(key + "="):
            return float(a.split("=", 1)[1])
    return default


def sample(cmds):
    pts = []
    x = y = z = 0.0
    yaw = 0.0
    loop_id = 0
    marks = {"cp": [], "boost": [], "gap": [], "start": None, "finish": None, "ramp": []}

    def fwd(a):
        return math.sin(math.radians(a)), math.cos(math.radians(a))

    def push(gap=False, loop=0):
        pts.append(dict(x=x, y=y, z=z, yaw=yaw, gap=gap, loop=loop))

    push()

    def straight(length, rise=0.0, ease="smooth", gap=False):
        nonlocal x, y, z
        n = max(1, int(round(length)))
        y0 = y
        dx, dz = fwd(yaw)
        for i in range(1, n + 1):
            t = i / n
            x += dx * length / n
            z += dz * length / n
            if ease == "smooth":
                e = smooth(t)
            elif ease == "in":
                e = t * t
            else:
                e = t
            y = y0 + rise * e
            push(gap=gap)

    for op, args in cmds:
        if op in ("forward", "f"):
            straight(num(args, 0, 1) * TILE)
        elif op == "start":
            marks["start"] = len(pts) - 1
            straight(TILE)
        elif op == "finish":
            marks["finish"] = len(pts) - 1 + int(TILE / 2)
            straight(TILE * 3)
        elif op in ("cp", "checkpoint"):
            marks["cp"].append(len(pts) - 1 + int(TILE / 2))
            straight(TILE)
        elif op == "boost":
            n = num(args, 0, 1)
            marks["boost"].append((len(pts) - 1, int(n * TILE)))
            straight(n * TILE)
        elif op in ("up", "down"):
            n = num(args, 0, 1)
            per = LEVEL * (2 if "steep" in args else 1)
            straight(n * TILE, per * n * (1 if op == "up" else -1))
        elif op == "ramp":
            marks["ramp"].append(len(pts) - 1 + int(TILE))
            straight(TILE, 0.3, ease="in")
        elif op == "gap":
            n = num(args, 0, 1)
            drop = -opt(args, "drop") * LEVEL
            marks["gap"].append(len(pts) - 1)
            straight(n * TILE, drop, ease="linear", gap=True)
        elif op in ("left", "right"):
            size = num(args, 0, 1)
            rad = (size - 0.5) * TILE
            ang = 90.0 if op == "right" else -90.0
            rise = (opt(args, "up") - opt(args, "down")) * LEVEL
            arc = rad * math.pi / 2
            n = max(2, int(round(arc)))
            y0, yaw0 = y, yaw
            for i in range(1, n + 1):
                t = i / n
                ym = yaw0 + ang * (i - 0.5) / n
                dx, dz = fwd(ym)
                x += dx * arc / n
                z += dz * arc / n
                yaw = yaw0 + ang * t
                y = y0 + rise * smooth(t)
                push()
        elif op == "loop":
            loop_id += 1
            side = -1.0 if "left" in args else 1.0
            R = LOOP_RADIUS
            adv = 2 * TILE
            n = int(round(2 * math.pi * R * 1.15))
            fx, fz = fwd(yaw)
            rx, rz = fz, -fx
            x0, y0, z0 = x, y, z
            for i in range(1, n + 1):
                th = 2 * math.pi * i / n
                along = R * math.sin(th) + adv * th / (2 * math.pi)
                up = R * (1 - math.cos(th))
                lat = side * TILE * smooth(th / (2 * math.pi))
                x = x0 + fx * along + rx * lat
                z = z0 + fz * along + rz * lat
                y = y0 + up
                push(loop=loop_id)
            y = y0
        else:
            raise ValueError("unknown piece '%s'" % op)
    return pts, marks


def check(pts):
    import numpy as np
    xs = np.array([p["x"] for p in pts])
    zs = np.array([p["z"] for p in pts])
    ys = np.array([p["y"] for p in pts])
    loops = np.array([p["loop"] for p in pts])
    errs = []
    if ys.min() < -0.01:
        errs.append("below ground (%.1f)" % ys.min())
    lim = 2 * (HALF_WIDTH + WALL) - 0.8
    for i in range(0, len(pts), 2):
        j0 = i + 30
        if j0 >= len(pts):
            break
        d = np.hypot(xs[j0:] - xs[i], zs[j0:] - zs[i])
        dy = np.abs(ys[j0:] - ys[i])
        same_loop = (loops[j0:] == loops[i]) & (loops[i] > 0)
        bad = (d < lim) & (dy < 6.0) & ~same_loop
        if bad.any():
            j = j0 + int(np.argmax(bad))
            errs.append("overlap at %dm and %dm" % (i, j))
            break
    return errs


def jumps(pts, marks):
    out = []
    for r in marks["ramp"]:
        p0, pm = pts[r], pts[r - 1]
        slope = p0["y"] - pm["y"]
        ang = math.atan(slope)
        res = []
        for v in (30, 45, 60, 75):
            vx, vy = v * math.cos(ang), v * math.sin(ang)
            landed = None
            for s in range(r + 1, min(len(pts), r + 300)):
                t = (s - r) / vx
                cy = p0["y"] + vy * t - 0.5 * AIR_G * t * t
                if not pts[s]["gap"] and cy <= pts[s]["y"] + 0.05:
                    # hitting the front of the landing block is not a landing
                    if pts[s - 1]["gap"] and cy < pts[s]["y"] - 1.0:
                        break
                    landed = s - r
                    break
                if pts[s]["gap"] and cy < pts[s]["y"] - 15:
                    break
            res.append("%d:%s" % (v, landed if landed else "MISS"))
        gap_end = r
        while gap_end + 1 < len(pts) and pts[gap_end + 1]["gap"]:
            gap_end += 1
        out.append("jump at %dm, gap %dm -> %s" % (r, gap_end - r, " ".join(res)))
    return out


def render(path_png, pts, marks):
    from PIL import Image, ImageDraw
    xs = [p["x"] for p in pts]
    zs = [p["z"] for p in pts]
    pad, size = 30, 700
    span = max(max(xs) - min(xs), max(zs) - min(zs)) + 2 * pad
    sc = size / span
    img = Image.new("RGB", (size, size), (120, 180, 95))
    d = ImageDraw.Draw(img)
    maxy = max(1.0, max(p["y"] for p in pts))

    def P(p):
        return ((p["x"] - min(xs) + pad) * sc, size - (p["z"] - min(zs) + pad) * sc)

    for i in range(1, len(pts)):
        if pts[i]["gap"]:
            continue
        h = pts[i]["y"] / maxy
        d.line([P(pts[i - 1]), P(pts[i])], fill=(int(80 + 170 * h), 80, int(120 - 60 * h)), width=max(2, int(9 * sc)))
    for c in marks["cp"]:
        x, y = P(pts[c]); d.ellipse([x - 5, y - 5, x + 5, y + 5], fill=(40, 150, 255))
    for b, _ in marks["boost"]:
        x, y = P(pts[b]); d.rectangle([x - 4, y - 4, x + 4, y + 4], fill=(255, 140, 0))
    x, y = P(pts[0]); d.ellipse([x - 7, y - 7, x + 7, y + 7], fill="white")
    if marks["finish"] is not None:
        x, y = P(pts[marks["finish"]]); d.ellipse([x - 7, y - 7, x + 7, y + 7], fill="black")
    img.save(path_png)


def main():
    folder = sys.argv[1]
    out = sys.argv[2] if len(sys.argv) > 2 else None
    ok = True
    for fn in sorted(os.listdir(folder)):
        if not fn.endswith(".txt"):
            continue
        meta, cmds = parse(os.path.join(folder, fn))
        pts, marks = sample(cmds)
        errs = check(pts)
        if marks["start"] is None or marks["finish"] is None:
            errs.append("needs a start and a finish")
        xs = [p["x"] for p in pts]
        zs = [p["z"] for p in pts]
        print("%-24s %-18s %5dm  cps %d  size %dx%d  top %.1fm" % (
            fn, meta.get("name", "?"), len(pts), len(marks["cp"]),
            max(xs) - min(xs), max(zs) - min(zs), max(p["y"] for p in pts)))
        for e in errs:
            ok = False
            print("    ERROR", e)
        for j in jumps(pts, marks):
            print("   ", j)
        if out:
            os.makedirs(out, exist_ok=True)
            render(os.path.join(out, fn.replace(".txt", ".png")), pts, marks)
    sys.exit(0 if ok else 1)


if __name__ == "__main__":
    main()
