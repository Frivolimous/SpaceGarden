# Space Garden — Godot prototype

A feel test for the revival (see [../docs/DESIGN.md](../docs/DESIGN.md) §7): the TS game's force-directed plant,
ported to Godot 4.7.2 .NET as a transparent, always-on-top overlay that covers the screen's work area (all but the
taskbar), so the plant seems to live on the desktop itself. Same layout as Eternal Dungeon and Typing Ship:
`src/Core` is plain C# (physics, growth, motes, saves; no Godot), `game/` is the Godot project (drawing, input,
the overlay window).

Run `sg.bat` (builds, then launches; option 5 opens the editor).

## Using it
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
- **Right-click** for the menu: always on top, size, growth speed (1x / 60x / 3600x), new garden, quit.
- Clicks anywhere off the plant and its toolbar go straight through to the desktop. On Windows this is done by
  toggling the window's click-through style each frame (`ClickThrough.cs`), not Godot's passthrough region: on
  Windows that region also clips drawing, which made the window flash and would cut off the toolbar.
- It saves every minute and on quit (`%APPDATA%\Space Garden Prototype\garden.json`). Reopening catches up the
  time away (capped at 72 h) and says what grew.

## What's ported, what's new
- **Ported from the TS game:** node sizes, masses, forces, link counts, shapes and colours (`NodeData.ts`);
  spring, repulsion and damping with the same constants (`FDGContainer.ts`, `Config.PHYSICS`); motes of light
  hopping along the stems and lighting what they reach (the transfer blobs).
- **Also ported:** holding a part (the TS game's pull: `PULL_FRICTION`, `TARGET_RATIO`) and the borders
  (`EDGE_BOUNCE`, now the screen edges). Links are the TS game's soft springs, with no length limit, so a pull
  stretches them and they drag the rest along. The held part is pulled by its weight, so the core is slow and
  heavy to drag and a leaf quick.
- **New:** wind that sways the parts more the further they are from the core, growth on real time (a new part every 20 minutes at 1x), catch-up on return,
  halos, grow-in animation, a 30 fps idle frame rate.
- Everything that shapes the feel is in `src/Core/Tuning.cs`.

## Dev options
`--path game -- [options]`: `--grow=N`, `--upright` (the shelved planted layout: core fixed at the bottom, a pull
upward, a ground), `--fresh`, `--seed=N`, `--away=HOURS`,
`--profile=NAME` (use `user://NAME.json` instead of the real save), `--backdrop=RRGGBB`,
`--screenshot=out.png [--frames=N | --at=a,b,c]` (saves the viewport, keeping transparency, then quits),
`--parts=N` (grow a new garden to N parts at once, past the usual 220 limit, for stress tests), `--fpslog` (prints
the frame rate, per-frame process and physics time and draw calls four times a second).

## Performance
Idle CPU (30 fps, % of one core, i7-12700H, the debug build that runs from the project): an empty garden 7%,
220 parts 9%, 500 parts 14%, 1000 parts 25%, 2000 parts 62%. Drawing is 8-10 draw calls whatever the size
(every layer is one batch; see `GardenView`). Physics is what grows: parts push each other through a grid, exact
for neighbours and summed per cell for distant ones (`Garden.Repel`), so it no longer checks every pair. The
distant push is kept and worked out again every 4 ticks, a quarter of the cells each tick (`FarPushEvery`). An
exported (release) build runs the physics about 4x faster than the debug build.

## Not in the prototype
No crawlers, skills, seedling, UI panels, sound or tests; no tray icon or launch-on-startup; it still shows in the
taskbar.
