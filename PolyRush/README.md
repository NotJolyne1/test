# PolyRush: Summer Season

A low-poly, time-trial track racer in the spirit of PolyTrack, built in Unity. It has 7 original summer tracks, snappy arcade physics, checkpoints, boost pads, jumps, banked turns, ghosts, medals, and a full menu flow. All the art (meshes, textures, sky, audio) is generated from code when the game starts. The project has no imported assets, so nothing is copied from another game and there are no broken asset references.

> **Engine:** Unity **2022.3 LTS** (made for 2022.3.62f3) with the Built-in render pipeline.
> 2022.3 is the better choice for laptops: the editor and the player are lighter, and the Built-in pipeline runs well on integrated GPUs.
> The project also opens in Unity 6 (6000.x). Accept the API updater prompt there, because some physics property names changed.

---

## Getting started

1. Install **Unity 2022.3 LTS** with **Windows Build Support (Mono)** from Unity Hub. For a website build later, also add **WebGL Build Support**.
2. Unzip the project, then use **Unity Hub > Add > Add project from disk** and pick the `PolyRush` folder.
3. Open it. The first import takes a minute. A setup script then runs on its own: it creates `Assets/PolyRush/Scenes/Main.unity`, adds it to Build Settings, and applies the player settings (Linear colour, fullscreen and so on).
4. Press **Play**.

If the scene does not open by itself, use **PolyRush > Setup Project** or open `Assets/PolyRush/Scenes/Main.unity`.

## Building for Windows

**PolyRush > Build Windows (64-bit)** writes `Builds/Windows/PolyRush.exe` and opens the folder.

To build from the command line (CI):

```
"C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Unity.exe" -batchmode -quit -projectPath "path\to\PolyRush" ^
  -executeMethod PolyRush.EditorTools.BuildScript.BuildWindows -logFile build.log
```

**PolyRush > Build WebGL** is there for later, when you put the game on your website.

---

## Controls

| Key | Action |
| --- | --- |
| W / Up | Accelerate |
| S / Down | Brake, then reverse |
| A D / Left Right | Steer |
| Space / Shift | Drift |
| Enter | Respawn at the last checkpoint (keeps the speed you had there) |
| R / Backspace | Restart the run |
| C | Change camera (chase / far chase / hood) |
| Esc / P | Pause |
| Gamepad | Left stick to drive, B drift, X respawn, Y restart, Start pause |

## Features

- **7 summer tracks:** Sunny Meadow, Golden Coast, Pine Ridge, Lakeside Leap, Sunbaked Canyon, Sunset Speedway and Tropic Island. Each has its own environment (lakes, ocean coast, pine forest, desert mesas, sunset light, island).
- **Track pieces:** straights, turns of any radius, banked curves, slopes, elevated bridges on pillars, a spiral climb, gap jumps with landing ramps, boost pads, checkpoint gates, a start gate with a grandstand, and a finish gate.
- **Arcade physics** (`CarController`): 4-ray spring/damper suspension, direct yaw-rate steering, strong grip with an optional drift, downforce for banked turns, a landing assist and in-air self-righting. Physics runs at 100 Hz for stable driving at 200+ km/h.
- **Time trial:** 3-2-1 countdown, millisecond timer (fixed step, interpolated at the line), checkpoints in any order, split deltas against your best run, a missing-checkpoint warning, instant restart and checkpoint respawn.
- **Ghost** of your personal best, saved to disk per track.
- **Medals** (Bronze / Silver / Gold / Author) calculated from each track's layout.
- **Menus:** start page with a live fly-over of the track, track select with cards and a details panel, settings (quality, resolution, fullscreen, vsync, three volume sliders, camera, car colour, units, ghost, FPS counter, reset records), controls page, pause menu, results screen and loading screen.
- **Procedural audio:** engine with gear shifts, wind, tyre squeal, countdown beeps, checkpoint chime, finish fanfare and boost whoosh.
- **Quality presets** Low, Medium, High and Ultra, which control shadows, MSAA, terrain resolution and tree density. Laptops with little video memory start on Medium.

## Project layout

```
Assets/PolyRush/
  Editor/            ProjectSetup (auto scene + settings), BuildScript (Windows / WebGL builds)
  Resources/Shaders/ Lit (vertex colour + glow), Sky, Water, Ghost, Unlit
  Resources/Tracks/  01..07 track files (plain text, see below)
  Scripts/Core/      GameManager (state machine), GameSettings, Records, GameInfo
  Scripts/Track/     TrackInfo (parser), TrackPath (centre line + frames), TrackLibrary, Theme
  Scripts/World/     TrackMeshBuilder, EnvironmentBuilder, PropFactory, MeshBuilder, textures, materials
  Scripts/Car/       CarController, CarInput, CarModelBuilder, CarAudio, CarEffects
  Scripts/Race/      RaceController, CameraRig, GhostData
  Scripts/UI/        MenuUI, RaceHud, UIKit
  Scripts/Audio/     AudioSynth (procedural sounds), AudioManager
Tools/track_tool.py  validates and previews track files
```

To rename the game, change `Scripts/Core/GameInfo.cs`.

## Making or editing tracks

Tracks are text files in `Assets/PolyRush/Resources/Tracks/`, loaded in file-name order. Units are metres and degrees:

```
name: My Track
theme: meadow          # meadow | coast | forest | lake | canyon | sunset | tropic
difficulty: 2          # 1 easy .. 4 expert
S 80                   # straight 80 m
R 90 50 b=8            # right turn, 90 degrees, radius 50 m, banked 8 degrees
UP 60 10               # straight that climbs 10 m (smooth)
CP                     # checkpoint gate here
BOOST 16               # 16 m boost pad starting here
KICK 10 0.4            # jump: take-off ramp
GAP 24 -0.4            # jump: empty air
LAND 50 6              # jump: landing slope (follows the jump arc)
OUTRUN 50 6            # jump: eases back to flat
L 120 45 3 b=10        # left turn that also climbs 3 m
S 110                  # finish is placed 70 m before the end
```

Check a track before playing it. The script reports self-overlaps and tracks that go below ground, simulates every jump at several speeds, estimates a par time, and draws a map:

```
python3 Tools/track_tool.py Assets/PolyRush/Resources/Tracks previews
```

## Getting ready for multiplayer later

The code is laid out so multiplayer can be added without rewriting the driving or race logic:

- The car only reads an `ICarInput` interface. A network-driven input can replace `LocalCarInput`.
- `GhostData` is a compact timestamped pose stream. The same format works for replays and for sending car states between players.
- Race flow (`RaceController`), world building (`TrackWorld`) and UI are separate, and the track world is built the same way on every machine from the track file.

## Notes

- Records and ghosts are stored in `PlayerPrefs` and `Application.persistentDataPath/ghosts`.
- On first start the game loads the last track you played, or track 1.
- All geometry and textures are generated at runtime, so `.meta` files and `Library/` are created by Unity the first time you open the project.
