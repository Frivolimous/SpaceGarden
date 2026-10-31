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
  (grow a stem, generator, lab or grove from it, or plant a seedling, on parts with room for branches; the kind growing there now has a
  pulsing outline, and a part with every link taken shows "3/3 Connections Used" in their place; prune it and everything beyond
  it; and Unhook if it's tied to a pot or trellis) and its **card**: its name, power, and its own numbers
  ("Drain: 6/s", "Genes: 12/min", what's
  forming). Tap it again to close both. Picking a tool, clicking anywhere else, starting a drag, or moving away for
  a moment closes just the toolbar; the card stays until a click lands off the plant, Escape, or another tap.
- **Tap the core** for its menu, a half ring of buttons under it (names show under the pointer). **Info** and
  **Build** are switches, lit while on: Info shows the garden's card (power, what it makes and drains, the gene
  balance, the cheapest next upgrade with its cost and ETA, or "in Research" once affordable, and the caps) above the
  part's whenever you tap the plant, and Build is build mode.
  **Almanac** (lit while open) opens a window with tabs across the top, **Journal** and **Codex**. The Journal is the garden's
  moments under day headings ("Day 2"), newest first, each with its time on that day. They're its planting, each
  kind's first part, its first passive bud, core links and the Hub bought (in the genes' purple), shaping tools gained
  from the garden's size (in the tools' blue), part counts reached, the first of each shaping tool placed, and wild
  flowers discovered (a pale gold) or ended. The Codex lists the kinds the garden has grown (only those), each with
  when it was first found and what it does.
  Drag its header to move it, its corner grip to resize it; the wheel scrolls it. It stays open (clicks elsewhere,
  Escape and losing the focus leave it) until its ✕ or the slot again; it hides in Ambient and Minimised and comes
  back in Active.
  **Research** (the flask, lit while open) opens the upgrade tracks' panel, a window like the Almanac's (drag its
  header; ✕ or the slot closes it): the gene balance in its header, and three cards in their roles' hues, **Stems**,
  **Generators** and **Core**, each with a glyph of its part and only its next upgrade, its cost and **Buy** (dim,
  saying how many more genes it needs over a fill showing how close the balance is, while it falls short; a card you can afford glows; the (i) by each title says at once what its next upgrade does). Buying
  spends the balance and raises the cap at once (the core ripples purple). See Upgrade tracks in DESIGN.md.
  **Ambient**, **Minimise** and **Settings** close the menu. The core's toolbar (its buds) opens above it at the same
  time. Tap the core again, anywhere else, or Escape to close it. The cards sit in one spot beside the plant.
- **Undo a prune**: for 30 s after a prune (`Tuning.UndoPruneSeconds`) an **Undo prune** pill under the plant puts
  the branch back (or Ctrl+Z while the widget has the focus). Only the last prune; passive growth waits out the window
  so the room is still there, and a bud placed in that room, or time away, ends it.
- **Fruit** (leaves, burrs, research pods) ignores the pointer: it can't be hovered, tapped or dragged, and clicks
  on it fall through to the desktop.
- **Build mode** (the core's menu, or Ctrl+B with the garden focused) arranges the garden: an opaque disc round the
  plant and a bar of shaping tools (Gravity Pot, Trellis, Gravity Well, as the garden's size brings them: its
  tooltip says at what size the next comes). Drag a tool
  off the bar to place it, drag its body, knob or posts to move and turn it, and drop a part on a pot or trellis to
  tie it (or an empty pot on a part to plant it). Each part shows its links used against its most (white 1/N, yellow, red when full), to see where
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
  the core, shaping tools gained and other moments, and any dip in power (also after 5 minutes or more hidden or
  minimised). A shaping tool gained while you watch gets a short note above the plant instead.
- While nobody can see it (screen locked, display off, or a full-screen app such as a game or video on its
  monitor) it pauses: no drawing or physics, waking 4 times a second to check (about 1.5% of a core). When it's
  visible again the hidden time's growth is caught up at once, like time away. Windows only (`Visibility.cs`).

## What's ported, what's new
- **Ported from the TS game:** node sizes, masses, forces, link counts, shapes and colours (`NodeData.ts`);
  spring, repulsion and damping with the same constants (`FDGContainer.ts`, `Config.PHYSICS`); power shown as
  link flashes, as the original's power transfers were (`FDGLink.flash`): a link flashes as often as power flows
  along it, so a busy trunk pulses and a full branch rests. Each part's brightness and size follow its fill, eased as the
  original's node intensity was (`PlantNodeView`), but along the square of the fill so they still move near full,
  where the bigger stores keep a tended garden; past full a part swells at half its overfill, up to 1.5 times its
  size (`FillLookCurve`, `OverFillLook`, `FillLookMax`). The arriving clumps are the motion, with no added bounce.
  Travelling blobs carry growth from groves and genes from labs.
- **Also ported:** holding a part (the TS game's pull: `PULL_FRICTION`, `TARGET_RATIO`) and the borders
  (`EDGE_BOUNCE`, now the screen edges). Links are the TS game's soft springs, with no length limit, so a pull
  stretches them and they drag the rest along. The held part is pulled by its weight, so the core is slow and
  heavy to drag and a leaf quick.
- **New:** wind that sways the parts more the further they are from the core, growth on real time (passive buds wherever a grove's growth blobs land), catch-up on return,
  halos, grow-in animation, a 30 fps idle frame rate.
- **Economy:** power is the original game's (`PlantNodePower`): the core and generators make power and stems, groves
  and labs cost upkeep (leaves halve a stem's), all in proportion to their fill, and about three times a second each
  part sends a clump of power to its emptiest neighbour, so far tips run low first and the fills never settle. Nothing
  wilts and nothing is destroyed; an overloaded garden just runs lower. Passive growth stays within the caps (4 stems,
  1 generator, 1 core link at the start), raised by the upgrades bought in Research. Catch-up runs the
  same fixed economy steps. Saves carry a version (now 9); an older one is set aside (`garden.v8.json` and so on)
  and a new garden starts.
- **Upgrade tracks** (step 5b, `Garden.Buy`, `Garden.Next`, costs in `Tuning` under Research): genes reaching the
  core fill the gene balance, and the Research panel spends it. Stems (+1 cap each, 26 upgrades to 30 stems),
  Generators (+1 cap each, 9 to 10) and the Core track (the wild flower limit, a second core link, seedling slots
  and the Hub, in a fixed order). Seedling slots and the Hub are counters in `Capacity` for
  now; 5c and the Hub's step give them mechanics (the wild flower limit has its own since 5d). The levels bought are saved (`GardenSave.Research`); a 5a
  save without them keeps its caps and reads them as upgrades bought. The shaping tools come from the garden's
  size instead (`Tuning.ToolsAtSize`): a Gravity Pot at the start, a Trellis at 15, a second pot at 25, a Gravity
  Well at 40, another pot at 60 and 100, then every 50.
- **Wild flowers** (step 5d, `src/Core/WildFlowers.cs`, numbers in `Tuning` under Wild flowers; DESIGN.md, Wild
  flowers): each wild flower limit bought in the Core track is a slot. A new bud, passive or planted, rolls as it
  starts: `PassiveWildChance` (5%) or `PlantedWildChance` (4%) per free slot. If it grows wild it forms as one of the
  kinds that can stand in for its basic kind (and, for a passive bud, the white passive-only kinds) and takes a wild
  slot; a stem or generator kind takes its base's slot too (a Tendril a stem's, a Pulsar a generator's), but the
  Amplifier and the Resistor don't. Pruning frees the slots. Fifteen kinds, each with its own shape: Amplifier,
  Resistor, Tendril, Big Stem, Pulsar, Twin Star, Ember, Lumber Grove, Seed Pod, Wild Lab, Lichen, Buffer, Comet
  Bloom, Moonflower, Nebula Bloom. Big Stem, Pulsar and Twin Star join the pool only through `Garden.UnlockWild`
  (their evolutions, step 5c; a passive bud that grows wild leans two to one to its base's own kinds). An Amplifier doubles half the blobs passing it; blobs never step onto a Resistor. An
  Ember fades from twice a generator to nothing over 3 days and a Comet Bloom withers after one: the only parts the
  garden ever loses on its own, drifting off like a prune; the journal and the away note say so. Their fruit
  includes two new kinds: glow (white, sending buff blobs at a quarter of a Buffer's rate) and wood. The part's card
  shows its quirk under its name (and how long an Ember or Comet Bloom has left); the garden card a "Wild flowers:
  1 / 2" line once there's a limit. All wild flowers draw from one atlas texture in one batch per layer.
- **Seedlings and evolutions** (step 5c, `src/Core/Evolutions.cs` and `Garden.cs`, numbers in `Tuning` under
  Seedlings; DESIGN.md, Seedlings and evolutions): plant a seedling from a part's toolbar while a seedling slot is
  free (1 to start, more in the Core track). It only takes power in (no upkeep, no transfer, a heavy power weight),
  and its fill is its progress: full at 200, it waits: tap it for a half ring of three evolutions (each its flower's
  shape in its role hue; the one under the pointer says what it does and the wild flower it blooms into). Choosing
  raises its store to the target: 1,000 for the first, then ×5 for each later one. Full again, it blooms with a
  burst of light: the effect applies for good, the wild flower limit (and its flower's base cap, where it takes that
  slot) rises by 1, the seedling becomes its wild flower (the Almanac's Evolutions tab lists them
  all, with their levels and when each was chosen). The ten
  evolutions and their levels are data (`Evolutions.All`); repeats ("More Power II") come once the garden has five,
  each adding level I's effect again (Build your Core ×1.5, ×2, ×2.5); Long Stemmed is offered only then. Power reaching a seedling within 5 links of a Nebula Bloom counts twice. Pruning a
  seedling after its choice leaves the evolution waiting for the next one planted. The card shows a seedling's
  evolution and its power against its target; the journal its choice and bloom; the away note its bloom.
- Everything that shapes the feel is in `src/Core/Tuning.cs`.

## Tracking
`src/Core/Tracking.cs` (DESIGN.md, Step 4). Saved with the garden (version 9).
- **Lifetime totals:** the garden's age, parts grown by kind, parts pruned, passive buds, the most parts at once,
  and the garden size (`MostMainNodes`: the most main nodes, fruit aside and the core counted, ever reached; pruning
  never lowers it, and the shaping tools come by it).
  Genes earned and upgrades bought are the garden's own (`Genes`, `StemsBought`, `GeneratorsBought`, `CoreBought`).
  The export names them `genes_earned`, `gene_balance`, `upgrades_bought` (and each track's `stems_bought` and so
  on), `passive_buds`, `most_main_nodes` and so on.
- **Gene balance** (step 5a): `Garden.GeneBalance`, the genes left to spend. Genes reaching the core add to it and to
  `Genes` alike; `CanSpend`, `Spend` and `Buy` take from it alone, so genes earned never drop.
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
shaping tool, at once or at tick N), `--build` (starts in build mode), `--inspect` (a lab's card, `--inspect=wild` a wild flower's
open, or the core's with no lab; `--inspect=core`, or `--inspect=garden` for Info on as well), `--coremenu[=SLOT]` (the core's
menu open, with that slot's name showing, e.g. `--coremenu=settings`), `--tap=full` or `--tap=forming` (as if a stem
with every link taken, or a part forming a bud, were tapped: its toolbar and card, the toolbar staying open),
`--almanac` (the Almanac open, or `--almanac=codex` on its Codex), `--research` (the Research panel open, or `--research=hint` with the Stems card's hint showing; with `--genes=N` for a balance to show,
e.g. `--fresh --grove --genes=1200 --research`),
`--awaynote[=HOURS]` (catch up that long at once, 8 h by default, on the garden loaded or planted, and show the away
note; with `--grove` a new garden has something to grow), `--export=PATH` (debug builds: write the tracking CSVs, see
Tracking, then quit unless screenshotting), `--ambient` (starts in Ambient) and
`--corehover` (as if the pointer were on the core there),
`--parts=N` (grow a new garden to N parts at once, past the usual 500 limit and the caps, for stress tests),
`--drain` (start with every part but the core empty, to watch power flow back), `--genes=N` (bring the gene balance
up to N, as if they had reached the core), `--wildlimit=N` (set the wild flower limit), `--wild=KIND[,KIND...]` (grow those
wild flowers at once, each on a new stem from the core, whatever their lock and the caps, every part full;
`--wild=all` lays out one of each round the middle of the screen: with `--scale=2 --labels` it's the sheet of their
looks), `--unlockwild[=KIND,...]` (unlock Big Stem, Pulsar and Twin Star, as their evolutions do), `--evolve=NAME[,NAME]`
(choose and bloom those evolutions at once, by name: `SecondPath` or `second-path`), `--seedling` (plant a seedling at
once), `--seedfill=N` (set its power, planting one if there is none: full at 200 brings its choice), `--choose=N`
(each waiting seedling takes its Nth offer), `--bloom` (fill each chosen seedling, so it blooms with its burst), `--tap=seedling` (tap it: its choice menu open, the first card showing), `--almanac=evolutions` (the Almanac at its Evolutions tab), `--scale=N`
(the plant's scale, past the Size setting's range), `--labels` (each wild flower's name beside it), `--fpslog` (prints
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

How: drawing is 8-10 draw calls whatever the size, every layer one instanced batch (see `GardenView`; the wild
flowers add two at most, their fifteen shapes sharing one atlas texture and a batch per layer), stems
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
No crawlers, skills, seedlings, the Hub, player-facing graphs, or sound; no launch-on-startup; it still shows in
the taskbar.
