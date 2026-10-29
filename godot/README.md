# Space Garden — Godot prototype

A feel test for the revival (see the Platform section of [../docs/DESIGN.md](../docs/DESIGN.md#platform-godot-4-net)): the TS game's force-directed plant,
ported to Godot 4.7.2 .NET as a transparent, always-on-top overlay that covers the screen's work area (all but the
taskbar), so the plant seems to live on the desktop itself. Same layout as Eternal Dungeon and Typing Ship:
`src/Core` is plain C# (physics, growth, the power economy, motes, saves; no Godot), `game/` is the Godot project (drawing, input,
the overlay window).

Run `sg.bat` (builds, then launches; option 5 opens the editor). `dotnet test` runs the core's headless tests
(`tests/Core.Tests`, xUnit).

## Using it
- **Display modes.** It starts in **Active**: full opacity, with its toolbar, card and the core's menu. **Ambient**
  (the core's menu, or **Ctrl+Alt+G** from anywhere; Windows) fades it, and every click falls through to the
  desktop except on the core: it brightens and is ringed under the pointer, and a tap brings the garden back to Active with the
  core's menu open (so does Ctrl+Alt+G again). **Minimised** (the core's menu, a click on the yellow tray icon, or
  minimising the window any other way) minimises the window and pauses the garden like a locked screen, catching up
  when it's shown again. Its taskbar button, Ctrl+Alt+G or the tray icon bring it back in Active. Only one running
  copy gets the hotkey (a second logs a warning).
- **Rest the pointer** on a part to ring it; nothing else shows. **Tap** it (click without dragging) for its toolbar
  (grow a stem, generator, lab or grove from it, on parts with room for branches; prune it and everything beyond
  it; and Unhook if it's tied to a pot or trellis) and its **card**: its name, power, and its own numbers
  ("Drain: 6/s", "Genes: 12/min", what's
  forming). Tap it again to close both. Picking a tool, clicking anywhere else, starting a drag, or moving away for
  a moment closes just the toolbar; the card stays until a click lands off the plant, Escape, or another tap.
- **Tap the core** for its menu, a half ring of buttons under it (names show under the pointer). **Info** and
  **Build** are switches, lit while on: Info shows the garden's card (power, what it makes and drains, genes against
  the next unlock and its ETA, the caps) above the part's whenever you tap the plant, and Build is build mode.
  **Almanac** (lit while open) opens a window with tabs across the top (just **Journal** for now): the garden's
  moments under day headings ("Day 2"), newest first, each with its time on that day. They're its planting, each
  kind's first part, its first wild bud, every unlock, part counts reached, and the first of each shaping tool.
  Drag its header to move it, its corner grip to resize it; the wheel scrolls it. It stays open (clicks elsewhere,
  Escape and losing the focus leave it) until its ✕ or the slot again; it hides in Ambient and Minimised and comes
  back in Active.
  **Ambient**, **Minimise** and **Settings** close the menu. The core's toolbar (its buds) opens above it at the same
  time. Tap the core again, anywhere else, or Escape to close it. The cards sit in one spot beside the plant.
- **Undo a prune**: for 30 s after a prune (`Tuning.UndoPruneSeconds`) an **Undo prune** pill under the plant puts
  the branch back (or Ctrl+Z while the widget has the focus). Only the last prune; wild growth waits out the window
  so the room is still there, and a bud placed in that room, or time away, ends it.
- **Fruit** (leaves, burrs, research pods) ignores the pointer: it can't be hovered, tapped or dragged, and clicks
  on it fall through to the desktop.
- **Build mode** (the core's menu, or Ctrl+B with the garden focused) arranges the garden: an opaque disc round the
  plant and a bar of shaping tools (Gravity Pot, Trellis, Gravity Well, as the gene track unlocks them). Drag a tool
  off the bar to place it, drag its body, knob or posts to move and turn it, and drop a part on a pot or trellis to
  tie it. Each part shows its links used against its most (white 1/N, yellow, red when full), to see where
  there's room to bud. Done or Escape ends it. See Shaping the garden in DESIGN.md.
- The plant floats free, as in the TS game (the radial layout; the core isn't planted).
- **Drag any part, the core included**: it's pulled to the pointer and the rest follows only through the links, at
  its real weight. Letting go keeps the momentum: fling it and the plant glides on and slows to rest. The screen
  edges are the garden's walls.
- The plant is never rescaled automatically, and the window never moves or resizes. **Size** in the settings sets
  the plant's scale (50-125%, scaled around the core).
- **Settings**: right-click the plant (Active mode), the core menu's Settings, or right-click the tray icon. Size,
  idle frame rate (15 / 20 / 30 / 60 fps, default 30, saved; the rate while nothing is being touched or moving, and
  the idle CPU cost scales with it), New garden (asks first) and Quit. Debug builds (run from the project) add a
  **Developer** submenu: always on top, growth speed (1x / 60x / 3600x), and (an experiment, on by default) "Fruit
  borrows its parent's push", the cheaper fruit physics described under Performance, switchable live to compare the
  feel, and **Export history and totals (CSV)** (see Tracking).
- In Active mode, clicks anywhere off the plant, its toolbar, the core's menu and the Undo pill go straight through
  to the desktop. On Windows this is done by toggling the window's click-through style each frame
  (`ClickThrough.cs`), not Godot's passthrough region: on Windows that region also clips drawing, which made the
  window flash and would cut off the toolbar.
- It saves every minute and on quit (`%APPDATA%\Space Garden Prototype\garden.json`). Reopening catches up the
  time away (capped at 72 h) and a note above the plant says what changed: the parts grown, the genes that reached
  the core, unlocks and other moments, and any dip in power (also after 5 minutes or more hidden or minimised).
- While nobody can see it (screen locked, display off, or a full-screen app such as a game or video on its
  monitor) it pauses: no drawing or physics, waking 4 times a second to check (about 1.5% of a core). When it's
  visible again the hidden time's growth is caught up at once, like time away. Windows only (`Visibility.cs`).

## What's ported, what's new
- **Ported from the TS game:** node sizes, masses, forces, link counts, shapes and colours (`NodeData.ts`);
  spring, repulsion and damping with the same constants (`FDGContainer.ts`, `Config.PHYSICS`); power shown as
  link flashes, as the original's power transfers were (`FDGLink.flash`): a link flashes as often as power flows
  along it, so a busy trunk pulses and a full branch rests. Each part's brightness and size follow its fill, eased as the
  original's node intensity was (`PlantNodeView`); the arriving clumps are the motion, with no added bounce.
  Travelling blobs carry growth from groves and genes from labs.
- **Also ported:** holding a part (the TS game's pull: `PULL_FRICTION`, `TARGET_RATIO`) and the borders
  (`EDGE_BOUNCE`, now the screen edges). Links are the TS game's soft springs, with no length limit, so a pull
  stretches them and they drag the rest along. The held part is pulled by its weight, so the core is slow and
  heavy to drag and a leaf quick.
- **New:** wind that sways the parts more the further they are from the core, growth on real time (wild buds wherever a grove's growth blobs land), catch-up on return,
  halos, grow-in animation, a 30 fps idle frame rate.
- **Economy:** power is the original game's (`PlantNodePower`): the core and generators make power and stems, groves
  and labs cost upkeep (leaves halve a stem's), all in proportion to their fill, and about three times a second each
  part sends a clump of power to its emptiest neighbour, so far tips run low first and the fills never settle. Nothing
  wilts and nothing is destroyed; an overloaded garden just runs lower. Wild growth stays within the caps (4 stems,
  1 generator, 1 core link at the start), raised as genes reach the core along the unlock track. Catch-up runs the
  same fixed economy steps. Saves carry a version (now 8); an older one is set aside (`garden.v7.json` and so on)
  and a new garden starts.
- Everything that shapes the feel is in `src/Core/Tuning.cs`.

## Tracking
`src/Core/Tracking.cs` (DESIGN.md, Step 4). Saved with the garden (version 8).
- **Lifetime totals:** the garden's age, parts grown by kind, parts pruned, wild buds, the most parts at once.
  Genes earned and unlocks reached are the garden's own `Genes` and `Unlocks`.
- **History:** four series, sampled as the economy runs: stored power, Gen and Drain (averaged, per growth second)
  and the genes that reached the core. There is a sample a minute for the last day, an hour for the last month and
  a day beyond. Old samples roll up a whole hour or day at a time.
- **Times are the garden's age:** growth seconds lived, so a catch-up (capped at 72 h) and the dev speeds count as
  they grow.
- **The journal** feeds the Almanac and the away note.
- **Export** (debug builds; Settings > Developer, or `--export=PATH`): three CSV files for a spreadsheet.
  - `PATH` holds the history: tier, start in hours, seconds, power, gen_per_s, drain_per_s, genes.
  - `PATH-totals.csv` and `PATH-journal.csv` sit beside it.
  - From the menu they go to `%APPDATA%\Space Garden Prototype\exports\tracking-<time>.csv`.
  - To export the real garden without opening it for long, run `--export=C:\somewhere\garden.csv`. This catches up
    and saves, as a launch does.

## Dev options
`--path game -- [options]`: `--grow=N`, `--upright` (the shelved planted layout: core fixed at the bottom, a pull
upward, a ground), `--fresh`, `--seed=N`, `--away=HOURS`,
`--profile=NAME` (use `user://NAME.json` instead of the real save), `--backdrop=RRGGBB`, `--pausetest=AT,FOR`
(act as if hidden for FOR seconds from AT seconds after start, to test the pause),
`--screenshot=out.png [--frames=N | --at=a,b,c]` (saves the viewport, keeping transparency, then quits; with it,
`--prune=N` and `--undo=N` prune the biggest branch and undo it at tick N), `--shaping[=N]` (places one of each
shaping tool, at once or at tick N), `--build` (starts in build mode), `--inspect` (a lab's card
open, or the core's with no lab; `--inspect=core`, or `--inspect=garden` for Info on as well), `--coremenu[=SLOT]` (the core's
menu open, with that slot's name showing, e.g. `--coremenu=settings`), `--almanac` (the Almanac open),
`--awaynote[=HOURS]` (catch up that long at once, 8 h by default, on the garden loaded or planted, and show the away
note; with `--grove` a new garden has something to grow), `--export=PATH` (debug builds: write the tracking CSVs, see
Tracking, then quit unless screenshotting), `--ambient` (starts in Ambient) and
`--corehover` (as if the pointer were on the core there),
`--parts=N` (grow a new garden to N parts at once, past the usual 500 limit and the caps, for stress tests),
`--drain` (start with every part but the core empty, to watch power flow back), `--nearunlock` (start just short of
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
No crawlers, skills, seedlings, rares, player-facing graphs, or sound; no launch-on-startup; it still shows in
the taskbar.
