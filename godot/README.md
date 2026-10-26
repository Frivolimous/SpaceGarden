# Space Garden — Godot prototype

A feel test for the revival (see the Platform section of [../docs/DESIGN.md](../docs/DESIGN.md#platform-godot-4-net)): the TS game's force-directed plant,
ported to Godot 4.7.2 .NET as a transparent, always-on-top overlay that covers the screen's work area (all but the
taskbar), so the plant seems to live on the desktop itself. Same layout as Eternal Dungeon and Typing Ship:
`src/Core` is plain C# (physics, growth, the power economy, motes, saves; no Godot), `game/` is the Godot project (drawing, input,
the overlay window).

Run `sg.bat` (builds, then launches; option 5 opens the editor). `dotnet test` runs the core's headless tests
(`tests/Core.Tests`, xUnit).

## Using it
- **Display modes.** It starts in **Ambient**: faded, and every click falls through to the desktop. Press
  **Ctrl+Alt+G** (from anywhere; Windows) for **Active**: full opacity, the toolbar and menu below, and a readout
  under the plant (power made and used an hour, how full it is, genes towards the next unlock, room left). Ctrl+Alt+G
  again goes back to Ambient. **Minimised** (from the menu, or a click on the yellow tray icon) hides the plant and
  pauses it like a locked screen, catching up when it's shown again; click the tray icon to bring it back, or
  right-click it for the menu.
- **Undo a prune**: for 30 s after a prune (`Tuning.UndoPruneSeconds`) an **Undo prune** pill above the readout puts
  the branch back (or Ctrl+Z while the widget has the focus). Only the last prune; wild growth waits out the window
  so the room is still there, and a bud placed in that room, or time away, ends it.
- **Rest the pointer** on a part to ring it. **Tap** it (click without dragging) for its toolbar: grow, pin in
  place (a yellow dot; drag a pinned part to move its pin), prune (cuts the part and everything beyond it). Tap
  again, pick a tool, click anywhere else, start a drag, or move away for a moment to close it.
- **Fruit** (leaves, burrs, research pods) ignores the pointer: it can't be hovered, tapped or dragged, and clicks
  on it fall through to the desktop.
- The plant floats free, as in the TS game (the radial layout; the core isn't planted).
- **Drag any part, the core included**: it's pulled to the pointer and the rest follows only through the links, at
  its real weight. Letting go keeps the momentum: fling it and the plant glides on and slows to rest. The screen
  edges are the garden's walls.
- The plant is never rescaled automatically, and the window never moves or resizes. **Size** in the menu sets the
  plant's scale (50-125%, scaled around the core).
- **Right-click** (Active mode) for the menu: display mode, always on top, size, growth speed (1x / 60x / 3600x), idle frame rate (15 / 20 /
  30 / 60 fps, default 30, saved; the rate while nothing is being touched or moving, and the idle CPU cost scales
  with it), new garden, quit, and (an
  experiment, on by default) "Fruit borrows its parent's push": the cheaper fruit physics described under
  Performance, switchable live to compare the feel.
- In Active mode, clicks anywhere off the plant, its toolbar and the Undo pill go straight through to the desktop. On Windows this is done by
  toggling the window's click-through style each frame (`ClickThrough.cs`), not Godot's passthrough region: on
  Windows that region also clips drawing, which made the window flash and would cut off the toolbar.
- It saves every minute and on quit (`%APPDATA%\Space Garden Prototype\garden.json`). Reopening catches up the
  time away (capped at 72 h) and says what grew.
- While nobody can see it (screen locked, display off, or a full-screen app such as a game or video on its
  monitor) it pauses: no drawing or physics, waking 4 times a second to check (about 1.5% of a core). When it's
  visible again the hidden time's growth is caught up at once, like time away. Windows only (`Visibility.cs`).

## What's ported, what's new
- **Ported from the TS game:** node sizes, masses, forces, link counts, shapes and colours (`NodeData.ts`);
  spring, repulsion and damping with the same constants (`FDGContainer.ts`, `Config.PHYSICS`); power shown as
  link flashes, as the original's power transfers were (`FDGLink.flash`): a link flashes as often as power flows
  along it, so a busy trunk pulses and a full branch rests. Each part's brightness and size follow its fill, eased as the
  original's node intensity was (`PlantNodeView`), and a part bounces a little each time power reaches it. (The step-1 motes are gone; travelling blobs return
  for fruit and genes.)
- **Also ported:** holding a part (the TS game's pull: `PULL_FRICTION`, `TARGET_RATIO`) and the borders
  (`EDGE_BOUNCE`, now the screen edges). Links are the TS game's soft springs, with no length limit, so a pull
  stretches them and they drag the rest along. The held part is pulled by its weight, so the core is slow and
  heavy to drag and a leaf quick.
- **New:** wind that sways the parts more the further they are from the core, growth on real time (a new part every 20 minutes at 1x), catch-up on return,
  halos, grow-in animation, a 30 fps idle frame rate.
- **Economy (step 2, first slice):** the core and generators make power, stems and labs cost upkeep (leaves halve a
  stem's), and power flows along the stems from fuller parts to emptier ones, so far tips run low first. A part that
  can't pay wilts (drawn greyer, on top of looking empty), stops draining, and recovers by itself; nothing is destroyed.
  Wild growth needs power to spare and stays within the caps (4 stems, 1 generator, 1 core link at the start).
  Catch-up runs the same fixed 10 s economy steps. Saves carry a version; an older one is set aside
  (`garden.v1.json`) and a new garden starts.
- Everything that shapes the feel is in `src/Core/Tuning.cs`.

## Dev options
`--path game -- [options]`: `--grow=N`, `--upright` (the shelved planted layout: core fixed at the bottom, a pull
upward, a ground), `--fresh`, `--seed=N`, `--away=HOURS`,
`--profile=NAME` (use `user://NAME.json` instead of the real save), `--backdrop=RRGGBB`, `--pausetest=AT,FOR`
(act as if hidden for FOR seconds from AT seconds after start, to test the pause),
`--screenshot=out.png [--frames=N | --at=a,b,c]` (saves the viewport, keeping transparency, then quits),
`--parts=N` (grow a new garden to N parts at once, past the usual 500 limit and the caps, for stress tests),
`--wilt` (start with every part drained and wilting, to watch power flow back), `--nearunlock` (start just short of
the next unlock on the gene track, to watch one fire), `--fpslog` (prints
the frame rate, per-frame process, physics and renderer CPU/GPU time, and draw calls four times a second).

## Performance
A garden grows to 500 parts, about two thirds of them fruit (`FruitChance`, `MaxNodes`). Both C# projects are
compiled optimised even in the debug build the game runs from the project (`<Optimize>` in the csproj files), so
it costs what an export would; unoptimised, the physics ran about 8x slower.

Idle CPU (30 fps, % of one core, i7-12700H, two thirds fruit, fruit borrowing its parent's push): an empty garden
4.5%, 800 parts 8%. Before idle pacing moved to low-processor mode they were 7% and 10-11% (and 400 parts 8%,
1200 parts 10%, 1600 parts 12%, 2400 parts 16%; expect those about 2.5 points lower now). Runs vary by about a
point. The old 220-part garden (a third fruit, every part in the grid, unoptimised) cost 9%. At 800 parts a frame
is about 0.85 ms of physics, 0.3 ms of drawing preparation and 0.3 ms of renderer CPU (0.5 ms GPU).

The empty garden's cost is Godot's own: a bare Godot project with an empty scene and the same window costs 5-7% at
30 fps under the max_fps cap. Idle pacing therefore uses low-processor mode (Godot sleeps between frames instead of
spending CPU to hold its timing), which saved about 2.5 points of a core at the same 30 fps. Tried without effect:
vsync off, a smaller window, a dummy audio driver; ANGLE (Direct3D) cost more.

How: drawing is 8-10 draw calls whatever the size, every layer one instanced batch (see `GardenView`), stems
included (soft-edged quads stretched along each stem, which cost the renderer a third of Godot's antialiased lines).
Parts push each other through a grid, exact for neighbours and summed per cell for distant ones (`Garden.Repel`),
so it no longer checks every pair. The distant push is kept and worked out again every 4 ticks, a quarter of the
cells each tick (`FarPushEvery`).

Being tried (the menu's "Fruit borrows its parent's push", `Garden.FruitBorrowsPush`, on by default): fruit stays
out of the grid and its push goes through its parent both ways. The parent pushes others with its fruit's force
added to its own, and each fruit gets the push its parent got from the rest of the plant, scaled to the fruit's
force and mass. Only its parent and sibling fruit push it directly. For a fruit-heavy 500-part plant that's about
4x cheaper (0.26 to 0.07 ms a tick, release; 1.8 to 0.7 ms a frame in the debug game), with about the same shape
and nearly as tidy: fruit over another branch's stem 6 to 7 a plant, fruit on another branch's fruit 0 to 1.

Tried on the way: fruit only meeting its own family (no borrowed push) was as cheap but untidy (35 and 13 such
overlaps); adding a push from nearby stems to that was tidy but slower than the grid (0.24 ms).

## Not in the prototype
No crawlers, skills, seedling, UI panels or sound; no tray icon or launch-on-startup; it still shows in the
taskbar.
