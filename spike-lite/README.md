# spike-lite: does a lighter shell idle cheaper than Godot?

A throwaway measurement spike. It draws the same garden as the Godot prototype in `../godot/`, from the same rules
(`../godot/src/Core`, referenced unchanged), in a plain .NET 8 app on raylib instead of Godot. The point is to find
out whether leaving Godot would make the always-on desktop garden noticeably cheaper while idle.

**Verdict: no on CPU, yes on memory, and the memory saving alone isn't worth leaving Godot.** At 30 fps idle
the lite app costs about the same CPU as Godot, for an empty garden and an 800-part one alike. The differences sit
inside the run-to-run noise. Memory is the one clear win: about half the working set and a fifth of the
private memory. That is about 115 MB of RAM. For a desktop widget that matters less than CPU, and it isn't worth
giving up Godot's editor, UI, input, text and export pipeline, which would all have to be rebuilt.

## Results

i7-12700H, Windows 11, one session, 26 Oct 2026. Idle at 30 fps, `--seed=1 --grow=0`. CPU is the process's CPU
time over 15 s after a 30 s warm-up, in % of one core. Six runs per cell: two passes of `measure.ps1`, each with two
plain runs and one `--fpslog` run. Memory varied by less than 2 MB between runs.

| Garden | App | CPU, median (range) | Working set | Private memory | Steady fps |
|---|---|---|---|---|---|
| empty | **lite** (raylib) | **4.2%** (3.6–4.9) | 133 MB | 52 MB | 30 |
| empty | Godot 4.7.2 | **4.1%** (3.0–4.8) | 246 MB | 280 MB | 30 |
| 800 parts | **lite** (raylib) | **6.3%** (5.2–9.0) | 142 MB | 61 MB | 30 |
| 800 parts | Godot 4.7.2 | **6.4%** (6.1–16.4) | 260 MB | 283 MB | 30 |

Raw rows: `results/pass1/results.csv`, `results/pass2/results.csv`. Pass 2 also keeps its logs (`results/` is
gitignored). Pass 1's logs were overwritten by mistake; its CSV was rebuilt from the console output.

Notes:
- **Godot's two outliers** (16.4% and 11.1%, both 800 parts, both plain runs) look like the widget leaving idle
  mode, not drawing more slowly. Godot drops out of its 30 fps idle when the plant's fastest part passes
  `SettledSpeed` (0.45), and an 800-part plant sways at 0.2–0.3. Without a log for those runs this can't be
  confirmed. Its four other 800-part runs were all 6.1–6.6%. The lite app has no idle/active switch, so it can't
  have that outlier. Leaving those two out doesn't change the verdict.
- **Per frame at 800 parts** (from the `--fpslog` runs): lite does about 0.4–1.0 ms of physics, 0.3–0.4 ms of
  drawing preparation and 0.2–0.5 ms of flush and present. Godot reports 0.4–0.45 ms of physics, 0.3–0.4 ms of
  renderer CPU and about 1.5–1.8 ms of "process" time. Both draw in 8–9 draw calls.
- **The empty garden costs the same in both, about 4% of a core.** So most of that idle cost isn't Godot's
  engine overhead. It goes into waking 30 times a second to clear and present a full-work-area transparent
  OpenGL window, which DWM then composites. Any shell pays that. Ways to lower it work the same in Godot:
  a lower idle rate (already in the menu), or not redrawing when nothing visible moved.
- The spike's first version drew each quad with raylib's `DrawTexturePro` (immediate mode). That cost about 1.4
  points more at 800 parts than the dynamic-mesh batches it uses now (one upload and one draw call per layer, like
  `GardenView`'s `Batch`). Even the best effort only matches Godot here.

## What it is

`Program.cs`, about 400 lines:
- The window is raylib (via [Raylib-cs](https://github.com/ChrisDill/Raylib-cs) 7.0.2, raylib 5.5, GLFW, OpenGL
  3.3). It is borderless, per-pixel transparent and always on top, and covers the work area (one pixel short when
  that is the whole monitor, as in Godot). It is layered and click-through via raw Win32, the same style bits as
  `ClickThrough.cs`.
  - **Why raylib over SDL3:** raylib's config flags cover undecorated, transparent framebuffer and topmost in one
    line each, with a mature NuGet binding that ships the native DLL. SDL3 (SDL3-CS) can do the same, but the
    binding is younger, and its 2D renderer needs `SDL_RenderGeometry` hand-batching for the same result.
    Neither choice changes the fixed cost, which is the window and present (see notes).
- The garden is grown exactly as Godot's `--parts=N` grows it: `Garden.New(Layout.Radial, seed)`, borders set
  to the window, translated to the same home spot, one first sprout, sprouting with 3 `Step()`s each up to N, then
  1800 settle steps.
- It steps a fixed 60 Hz (at most 8 ticks a frame, as Godot) and draws at 30 fps, interpolating with
  `PreviousPosition`. Between frames it sleeps (`Thread.Sleep` with `timeBeginPeriod(1)`, as Godot does) rather
  than busy-waiting. There is no vsync.
- Drawing matches `GardenView`'s work: soft-edged stem quads, halos, each shape (circle to hexagon) as a lighter
  rim plus fill from supersampled, mipmapped textures, and motes. Each layer is one dynamic mesh written straight
  into unmanaged vertex memory, uploaded and drawn once per frame. It uses premultiplied blending, so the
  compositor shows the edges right. Pins, the hover ring and falling pruned parts are left out (none are on screen
  in these measurements).
- There is no input, toolbar, menu, saving or idle/active switching.

## Running it

```bash
dotnet build spike-lite -c Release
```

```bash
spike-lite/bin/Release/net8.0/SpaceGarden.Lite.exe --parts=800 --fpslog
```

Options: `--parts=N`, `--seed=N` (default 1), `--grow=N` growth speed (default 60x), `--fps=N` (default 30),
`--fpslog` (prints the frame rate and per-frame physics, drawing and present times once a second, to stdout),
`--seconds=N` (quits after that long). The window is click-through, so to quit early, end it from Task Manager or
use `--seconds`.

To repeat the comparison (about 10 minutes, both apps, empty and 800 parts):

```bash
powershell -ExecutionPolicy Bypass -File spike-lite/measure.ps1
```

It refuses to start if a Godot instance is already open, because that could be the real garden. It builds both,
parks the cursor at the top left and puts it back afterwards, and runs Godot's GUI exe with
`--profile=spiketest` so the real save is never touched. It deletes that profile's json at the end, stops exactly
the processes it started, and warns if any are left. Results go to `spike-lite/results/`. Options: `-Repeats`,
`-WarmUp`, `-Window`, `-Parts`; set `GODOT` if Godot isn't at `C:\Tools\Godot_v4.7.2-stable_mono_win64`.
