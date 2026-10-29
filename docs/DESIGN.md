# Space Garden — Design

*Status: draft, October 2026. Merged on Oct 26 from the design anchor (Oct 25, @Jeremy) and the first revival draft. Built in Godot 4 (.NET); see Platform. The original game's systems are inventoried in [ORIGINAL_SYSTEMS.md](ORIGINAL_SYSTEMS.md); it's preserved at the git tag `tactical-idle`.*

## Vision and pillars

Space Garden is a calm desktop companion with a strategy economy underneath: a space plant that grows over days, weeks and months on top of whatever you're doing. You glance at it, tend it for a minute, and leave it be. Tech is settled: Godot, transparent always-on-top window, force-directed graph physics all working.

**Pillars**

- **Long-term tending.** The garden lives for days to years; short check-ins, never long sessions.
- **Growing and pruning.** Choosing where to grow and what to cut are the core verbs.
- **Levelling up.** A visible progression arc over months.
- **Beauty.** Clean, clear, consistent; every idle moment looks good.

**High floor, high ceiling.** A neglected garden still thrives slowly; a tuned garden evolves faster. Optimisation changes speed, never safety. Strategy lives in the garden's shape, not in timely reactions. Everyone eventually sees all content.

**The plant is stupid; the player is the brain.** The plant has no smart growth behaviour: it never checks whether it can afford a new part, never saves power for later, never picks a useful spot. Blobs wander at random and parts grow wherever they land. Steering it (planting the first grove, placing generators and labs, pruning growth that drains the garden) is the player's job. A plant left alone never plants its first grove (only groves send growth blobs) and is never optimised, so balance and pacing simulations assume a smart player who is actively tending.

**Constraints carried from the first draft**

- **Never punishing.** Absence is fine; coming back is a pleasant surprise. The only consequence of neglect is a garden running low on power (see Economy), working slower, and it never destroys anything.
- **Lightweight presence.** Runs all day without anyone noticing it in CPU, memory or battery. See Performance budget and its benchmarks.
- **No progress gated on the window being open.** Time away is caught up (see Time model).

**Out of scope for now:** spells (cut), mandatory prestige resets (removed). The Almanac is peripheral: it arrives in step 4, and what form it takes is still open.

### Why the revival

The last iteration (v0.0.2, 2024) became a tactical idle game: a mandatory prestige loop, spells on cooldown, kill counts, frame-rate-tuned pacing. Its systems were good; their framing lost the charm. The revival keeps the plant, the flowing particles and the creatures, removes the reset, and rebuilds the economy around slow growth and pruning.

## Core loop

```
 glance ─► notice something (new growth, a dim branch, a full seedling, a rare node)
   ▲                    │
   │                    ▼
 leave ◄── make one small choice (bud, prune, shape, pick an evolution)
```

Short sessions of 30 seconds to 5 minutes, several times a day. The garden keeps growing between sessions.

## Visual language

Every node is a simple geometric shape; meaning is carried by a small, fixed set of channels.

| Channel | Meaning |
| --- | --- |
| Hue | Role: yellow = power, orange = fruit, purple = genes, green = structure, grey/white = specialist, blue = creatures |
| Shape | Specific node type within its role; every type has a unique shape |
| Brightness and scale | Power level: full nodes glow and swell, low nodes dim and shrink |
| Saturation | State: wild growth (subtle tone), dormant |
| Fruit | Modifiers on a node; later also crawler food |

Unique shapes double as colour-blind support. Texture and complex shapes are held in reserve if playtesting shows a state is hard to read. The garden should look consistent: wild growth differs only by a subtle tone.

How the rare nodes map onto the roles:
- **Specialist (grey/white):** Resistor, Amplifier and Buffer. They change how blobs and neighbours behave rather than producing a resource.
- **Creatures (blue):** Home.
- **Structure (green):** the Hub reads as a stem visually, drawn in its own shade of green. This is visual only: it doesn't take a stem slot or count against the stem cap.

The plant must look good **without any UI chrome**. Controls appear only in Active display mode.

## Economy

Three flowing resources move visibly through the stems; capacity is a set of limits, not a collected resource. Power shows as link flashes and node brightness; growth and genes travel as blobs. Blobs flow constantly, each carrying a small amount. Together they are the garden's "living light", its main idle visual.

| Resource | Colour | Source | Purpose |
| --- | --- | --- | --- |
| Power | Yellow | Core, generator flowers | Maintains nodes and runs the flowers |
| Growth blobs | Orange | Grove flowers | Wander the stems and grow fruit and new wild nodes where they're absorbed |
| Gene blobs | Purple | Lab flowers | Wander the stems; only the core absorbs them (later also seedlings and hubs) |

There is no core trickle of growth: groves are the only source, and a new garden has none. The player plants the first grove by hand (step 6's onboarding points there first).

**Power is a light show, not the pace.** Power fills and drains in seconds, as in the original, and keeps moving: the fills rock around a balance that shifts as the plant changes, never settling. It doesn't set the garden's pace (the unlock track does, through genes); it shows the garden's health, and a low garden works slower.

**Everything follows its fill.** The core and generators make power in proportion to their fill (none once full). Every part's upkeep is in proportion to its fill too, so a part never quite empties. Groves and labs send blobs in proportion to their fill. An overloaded garden (one that would cost more full than it can make full) doesn't break: it settles lower, its far flowers lowest and slowest, until the player prunes or adds a generator. Nodes are never destroyed. A new node starts empty and fills from its parent.

**Wilting (a design concept, not in the game).** Nothing wilts as a state: low power is shown only by a dim, shrunken node. Wilting stays a concept to reach for if neglect ever needs a stronger signal: a node out of power stops draining and is drawn dimmed, shrunk and slightly desaturated until it refills.

**Blobs.** Growth and gene blobs both do the same stupid random walk along the stems: never straight back unless at a dead end, fading after 30 links.
- **Gene blobs** pass every node by and are absorbed (100%) only by the core. A lab far from the core loses many of its genes on the way.
- **Growth blobs** are absorbed at 30% by a node with nothing forming, and at 100% by a node with a fruit or bud already forming. A full node (no fruit room and no free link) never absorbs.
- **Fruit or node.** A blob absorbed fresh at a node with a free link slot and room under the caps rolls between starting a fruit and starting a node bud (50/50, to tune); otherwise it starts a fruit. A wild node's type is rolled when its bud starts, and the bud holds its link and its cap slot until it's done. Each new fruit or bud on a node starts again at 30%.
- **Forming.** Each absorbed blob adds 0.1; a fruit completes at 1.0 and a node bud at 3.0. A forming fruit grows in scale beside its node. A forming node shows as a silhouette inside its parent, growing from a speck, in its final hue; it doesn't exist in the physics until it's complete, then pops out as its link extends. Buds can't be cancelled: the player prunes after one emerges.

Draft rates, to tune in play, at real speed: a fully powered grove sends a growth blob about every 3 s and a lab a gene blob about every 5 s. A gene blob carries one whole gene, so the player can see and count them; the 0.1 per blob is growth blobs only. At these rates wild growth fills the caps within minutes of an unlock, so the unlock track (genes) sets the garden's pace.

**Local power flow.** Each node stores its own power, which moves along links in clumps, as in the original (`PlantNodePower.attemptTransferPower`); there is no global pool. About three times a second (each economy step), each node fuller than a quarter sends a clump to its emptiest neighbour that's less full than itself. Each kind has a transfer rate (the core about 29 a second, stems 30, generators 18, groves and labs 15), and a clump is that rate times the step's length, so the step only sets how chunky the transfers are. Emptiest is judged by fill over weight: stems weigh 1.1 and the core 1.5, so power heads out along the stems. Clumps, not a smooth flow, are what keep the fills moving. Each clump flashes its link (brighter when the receiving node is fuller), at its own moment so the links don't flash in step, and each node's brightness and size follow its fill. A branch far from power runs lower on its own while the rest thrives, so generator placement matters.

**Fruit holds power** as in the original (`PlantNodePower.powerFruits`): each fruit holds 100 and costs nothing. A part at least 60% full feeds its emptiest fruit (15 a second) while that fruit is less full than the part, so new fruit start empty and brighten as they fill, and a garden's fruit only glow where their parts have power to spare. Fruit sends nothing on, and its effect (a leaf cutting its stem's upkeep, and so on) doesn't depend on its fill.

**Size follows fill** as in the original (`PlantNodeView.intensityInterpolation`): a node's scale is its shown fill, never below 0.2; past full it grows only a tenth as fast, up to twice its size. The shown fill eases after the real one, faster rising than falling. There's no artificial bounce: the clumps arriving are the motion.

**Power rules**

- The core is the main source. A generator produces half of the core's output. Core power upgrades come later, not in the MVP.
- Stems are cheap to maintain. Leaves cut a stem's upkeep: each takes a quarter off, so two leaves halve it. Leaves grow from growth blobs, like all fruit.
- A lab costs half as much again as a grove to keep (6 a second against 4).
- Generator, grove and lab fruits each raise their flower's output by their `FlowerBonus` (a tenth: a generator holds two fruit, so +20%; a grove or lab three, so +30%). A flower's upkeep is multiplied by 1 plus its fruits' `FlowerPowerFactor` (a leaf's is -0.25). Both are set on the fruit's own spec.
- The core has no fruit in the MVP; core fruit is reserved for showing later upgrades.

Fruit, once grown, stays. Groves are needed while the garden grows; if growth stalls, they stop being useful. The unlock curve should be paced so a small number of groves always stays useful, keeping up with new stems and flowers.

**Grove tuning target.** Tune the grove's growth blob rate so one or two groves can just keep up with leaf demand from new stems as well as their share of wild buds. Each new stem has room for two leaves, so week one's 26 new stems (4 to 30) mean about 52 leaves.

Values tuned for feel from the original game's (its per-tick numbers times 60). Each kind's numbers live in one entry of `NodeSpecs` (`godot/src/Core/NodeSpecs.cs`), as the original's `Nodes` config, with the shared base values in `NodeBase` (`PowerTransferPerSecond` is how fast a node can send power on); garden-wide knobs are in `Tuning`. Rates are per real second when full, and scale with fill:

| Node | Holds | Produces | Upkeep | Sends on |
| --- | --- | --- | --- | --- |
| Core | 800 | 24 | — | 28.8 |
| Generator | 200 | 12 | — | 18 |
| Stem | 150 | — | 3 (2.25 with one leaf, 1.5 with two) | 30 |
| Grove | 200 | — | 4 | 15 |
| Lab | 200 | — | 6 | 15 |
| Fruit | 100 | — | — | — |

Sanity check: the core alone keeps 8 bare stems full; the end-of-week-one garden (30 stems, 10 generators) makes 144 against 90 of bare stem upkeep, or 45 with every stem in two leaves, leaving room for groves and labs.

Tuning guide for later (Jeremy's, to discuss): transfer rates a little above the generation amounts; bigger stores to slow the overall fill speed, with the size curve reworked so nodes still visibly grow and shrink near the top of the range.

## Capacity

Capacity is a set of slot limits that cap growth until research raises them. When the garden is full, the player prunes to make room.

| Limit | Covers |
| --- | --- |
| Per-type caps | Structure and power only: stems and generators. Groves, labs and other accessory flowers are uncapped; they are limited by free link slots on stems and by power upkeep, so the player chooses the mix |
| Wild total + per-type caps | Rare wild nodes, e.g. 5 wilds total, up to 2 Lumber Groves and 1 Volatile |
| Seedling slots | Active seedlings |
| Local caps | Links per node and fruit per node. Stems are fixed at 3 links including the parent, so each stem supports a branching stem, a flower plus a continuation, or two flowers at a dead end. Large stems with 4 links unlock in limited numbers in step 5. The core starts with 1 link and gains more as major unlocks (first +1 at 15,000 genes, about 8 hours in); its maximum is open |

Bloomed seedlings become special versions of basic nodes and take normal slots within normal caps, so evolutions never need a separate limit.

## Performance budget

Plan for a soft cap of **250 main nodes** in a fully matured garden, to be refined through play.

- Stress test: after optimisation, the system supports upwards of 300 main nodes with negligible idle overhead (previously around 220). A setting limits idle frame rate (15/20/30/60 fps, default 30) for further savings.
- Fruit adds negligible load: tested at 3 fruit per main node at 220 nodes. Fruit borrows its parent's push in the physics, so it stays out of the collision grid.
- The 250 soft cap leaves headroom for future blob and crawler load and weaker machines.
- The sum of all capacity limits at their maximum should stay within this budget, so the capacity system enforces it. With 3-link stems, a garden with S stems, C core links and I inline (2-link) flowers has at most 2S + C + I + 1 main nodes, so the stem cap effectively sets the garden's maximum size (about 124 stems for 250 nodes). Each large stem adds one more slot.

Evolutions stay within the budget because a bloomed seedling becomes a special basic node in a normal slot, not an extra node.

### Benchmarks (anchor)

The current numbers, measured on Oct 26 2026. Later work is compared against these; if they prove too high in use, we optimise further.

Machine: i7-12700H, Windows 11. Idle at 30 fps, release-optimised C#. CPU is % of one core, median of six runs (from [spike-lite/README.md](../spike-lite/README.md)).

| Garden | CPU idle | Working set | Private memory |
| --- | --- | --- | --- |
| Empty | 4.1% (3.0–4.8) | 246 MB | 280 MB |
| 800 parts (two thirds fruit) | 6.4% (6.1–6.6, plus outliers when it left idle mode) | 260 MB | 283 MB |
| Hidden (screen locked or full-screen app) | about 1.5% | — | — |

- **Per frame at 800 parts:** about 0.85 ms physics, 0.3 ms drawing preparation, 0.3 ms renderer CPU, 0.5 ms GPU, in 8–10 draw calls at any size.
- **Physics stress:** 2000 parts take 1.2 ms a tick.
- **Where the idle cost goes:** most of it is waking to redraw and present a full-screen transparent window, which any engine pays. A bare Godot project with the same window costs about as much as the empty garden. A raylib shell measured the same CPU and only saved memory.

The first draft's aim of "well under 2% idle" isn't met. These benchmarks are the baseline instead. Known levers if it's needed: a lower idle frame rate (15 or 20 fps), or a smaller window than the full work area.

## Time model

Implemented in step 1.

- **Wall-clock driven.** Growth advances by real elapsed time, not frames.
- **Low-power idle.** When nothing is being touched or moving, rendering drops to the idle frame rate.
- **Catch-up on return.** The save stores when the garden was last seen; on launch the elapsed time is simulated without drawing, capped at **72 hours** (`Tuning.CatchUpCapSeconds`, to be tuned). Time hidden behind a lock screen or full-screen app is caught up the same way when the garden shows again.
- **Absence never hurts.** Catch-up can't destroy nodes; the worst case is an overloaded garden running low, which the player fixes by pruning.
- Parked idea: a short "while you were away" summary on return (new growth, rares, blooms).

## Growth and pruning

Once it has a grove, the garden grows wild until it fills its capacity; attentive players prune wild growth to make room and place buds where they want.

- **Wild growth** is random: it's wherever growth blobs land (see Economy, Blobs), with no check on power. Wild growth that drains the garden is the player's to prune. Once a garden has a grove, wild buds can be groves too; a garden with none grows nothing wild.
- **Manual buds** cost nothing. The cost is attention. The player picks the type (stem, generator, lab or grove), and the bud grows at once.
- **Pruning** frees capacity. A full garden can only grow where the player has cut.
- **Rare wild nodes** can't be planted. Pruning one frees its slot, and a new rare later appears by random chance, possibly a different type in a different place. Regrowth takes time.

**Pruning gives nothing back.** Its rewards are built in: it frees stem and generator capacity for deliberate growth, lowers power upkeep, reshapes the garden, and rerolls rare nodes. With no return, wild growth can't be farmed. The cut itself must feel satisfying: every link in the pruned branch is cut, and its loose parts drift off as the plant's push (and their own) sends them away, fading out from the moment of the cut over a few seconds (`Tuning.CutFadeSeconds`, 5 s).

**Undo and wild growth.** While a prune can be undone, growth blobs start no new buds anywhere and no new fruit on the pruned branch's parent (they still feed what's already forming), so an undo always has room to restore the branch.

## Tending commands

The player has eight basic commands.

1. **New sprout:** choose a basic node type and grow it at a chosen spot.
2. **Prune:** remove any node and all of its children. No preview; undo restores the last prune only, within about 30 seconds (to be tuned).
3. **Place a shaping tool** (Gravity Pot, Trellis, Gravity Well) in build mode: a subtle overlay, placed, moved and turned anywhere (see Shaping the garden).
4. **Connect a node to a Gravity Pot or Trellis** in build mode, by dragging the node onto it: in a pot it moves to the pot and locks there, and the nodes growing straight out of it are pushed gently in the pot's facing direction; on a trellis it's held on the trellis's line.
5. **Select evolution:** for the core, seedlings and other skill-based nodes.
6. **Zoom** in or out.
7. **Display mode:** Active (full opacity, controls visible, clickable, the default), Ambient (transparent and click-through), or Minimised (to the taskbar and tray). A hotkey toggles Active and Ambient, and brings a minimised garden back.
8. **Drag:** click and drag any node to tug the plant around on a spring. Purely tactile, no economy effect.

Undo covers only the most recent prune. Wild growth leaves the freed capacity alone until the undo window passes (see Growth and pruning).

## Shaping the garden

Players keep a mature garden compact and give it form through zoom and shaping tools.

- **Zoom:** the player can zoom the garden in or out.
- **Gravity Pots:** a node connected to a pot moves to it and locks in place; the nodes growing straight out of it are pushed subtly in the pot's facing direction, and the rest of the branch follows them.
- Long branches can still spill back over, so a large garden may need several pots.
- Wild growth respects pots, so a direction set once keeps shaping the garden.
- Shaping tools are purely about shape, with no economy effect.

**Three shaping tools** (step 3a, done). They're overlays, not graph nodes, so they take no node slot and count against no cap but their own. The rules are in `godot/src/Core/Shaping.cs` and `Garden.cs` (Shaping), their strengths in `Tuning` (Shaping).

| Tool | What it does | Holds | Unlocks |
| --- | --- | --- | --- |
| Gravity Pot | The node dropped on it moves to the pot's mouth and is locked there. Only the main nodes growing straight out of it (one link away) are pushed gently the way the pot faces; nodes further out just follow them through their links. New buds on the potted node head that way too | One node (dropping another swaps it) | 1 from the start, +1 at 15,000 genes |
| Trellis | A straight line between two posts. The nodes dropped on it are held on the line but slide along it, so the plant's own push spaces them out, like an espalier wire | Any number | 1 at 7,200 genes |
| Gravity Well | Pulls every main node faintly towards it. The core takes the whole pull back, so the plant leans towards the well rather than drifting into it | Nothing | 1 at 70,000 genes |

Fruit is never tied or pulled: it follows its node. A pruned node lets go of its tool; undoing the prune ties it again if the tool is still there (and a pot still free). Pruning or removing never refunds or costs anything.

**Build mode** is where the garden is arranged: the plant menu's "Build mode" (or Ctrl+B with the garden focused) in Active mode; Done, Escape or leaving Active ends it.
- A dark, opaque disc fades in round the plant and its tools, catching every click on it, so arranging never clicks through to the desktop.
- A bar above the disc holds the three tools (each with how many more the caps allow, greyed with what unlocks it until then) and Done. Pressing a tool takes a new one off the bar and carries it until let go; a tap sets it down just under the bar.
- Drag a tool's body to move it, a pot's knob (beyond its mouth) to turn it, a trellis's end posts to swing and stretch it. Handles come before nodes under the pointer, since a pot's knob sits where its branch grows. Tapping a tool opens a small toolbar with Remove; dragging a tool back onto the bar also puts it away.
- Dragging a node and letting go over a pot or trellis ties it there. A tied node's toolbar gains Unhook.

**How they show.** A pale moonlight blue, no role's hue, in the parts' outline look (dark edge, crisp outline, dark fill), under the plant. Each moves gently: chevrons rise out of a holding pot's mouth, rings draw slowly in to a well. Faint while tending in Active mode (the pointer passes through them), full in build mode, hidden in Ambient and Minimised. Saved with the garden, with what they hold (save version 7).

Open, for the gardening mode in Jeremy's todo:
- Whether build mode grows into the planned gardening mode (opaque radius, arranging the plant and the guides) or stays separate. The pieces are meant to move: the backdrop and the bar are presentation only, and placement goes through `Garden.Place`, `Tie` and `Remove`.
- Whether the garden should pause or hold wild growth while arranging. It doesn't now.
- How the disc should behave round a very large plant (it grows with the plant and its tools, and can cover most of the screen).
- Whether tools should be tappable outside build mode at all (now they're inert there), and whether to show tool counts somewhere outside build mode.
- How many of each tool later steps allow (proposed: rising with core levels).

## Node roster

Nodes come in two tiers. Main nodes are called **stems** (the structure) and **flowers** (everything else: generators, groves, labs and rares); they can be grown and pruned. **Fruits** grow on stems and flowers and add minor or enhancing effects. Most flowers have 1 link and end a branch; amplifiers, resistors and possibly some new wild flowers have 2 links and sit inline, continuing a branch.

| Group | Nodes | How they appear |
| --- | --- | --- |
| Fixed | Core | Always present, can't be pruned |
| Plantable | Stem, Generator (yellow), Grove (orange), Lab (purple) | Wild growth or manual buds; the first grove only by a manual bud |
| Rare wild-only | Hub, Amplifier, Resistor, Volatile, Lumber Grove, Wild Lab, Buffer, Home | Wild growth only, within wild limits |
| Future unlock | Big Stem (large stem, 4 links) | Unlocked in limited numbers in step 5; never wild |
| Seedling | Seedling | Separate from the wild pool (see Progression) |
| Fruits | Leaf on stems, plus each flower's own fruit set | Grown by growth blobs |

Each flower type has its own set of fruits, all grown by growth blobs. Leaves on stems cut the stem's power upkeep; generator, grove and lab fruits raise their flower's efficiency (see Economy).

Open: Home as a random rare could delay crawlers by luck. It may work better as a plantable node unlocked by an evolution.

**Specialists** (grey/white) are the utility rares: Resistor (blocks blobs), Amplifier (strengthens blobs passing through) and Buffer (temporarily boosts nearby production). With spells cut, they're the garden's only modifiers of flow and output. Their exact effects are to be designed in step 5.

## Progression

Research has three kinds of upgrade, and each has a home in the garden. Nothing resets: every upgrade applies immediately and permanently.

| Upgrade type | Example | Where it lives |
| --- | --- | --- |
| Linear / numeric | +1 stem capacity, +15% power transfer | The core, as genes reach it |
| Mechanic change / unlock | Large stems, new node in the wild pool | Seedlings |
| Bundled major change | Labs make ×5 genes but drain ×2 power | Seedlings |

**Core.** In the MVP, genes reaching the core drive the linear unlock track below. From step 5, core levels add choice-based numeric boosts. Core levels show as a deepening hue; core fruit is reserved for showing later upgrades and milestones.

**Seedlings.** A seedling sprouts, the player picks its evolution path, gene blobs fill it over days, and it blooms. The evolution's effect applies permanently the moment it blooms. The seedling then becomes a special version of a basic node (for example a lab or generator), marked by a unique fruit. Pruning it removes only the special node, never the effect. All evolution effects, including bundled changes, are designed as net positives with mild drawbacks, so no choice is regretted. The original skill tree (see [ORIGINAL_SYSTEMS.md §9](ORIGINAL_SYSTEMS.md)) is the content pool to draw evolutions from.

Note the order change from the first draft: the evolution path is chosen when the seedling sprouts, not when it blooms.

**Wild pool.** Evolutions can add new rare types to the pool; core upgrades raise wild limits.

**Linear unlock track (step 2).** Genes reaching the core raise caps automatically along a fixed sequence, with a small notification and no choices. Genes come only from labs (the core makes none), so the track starts once the player plants a lab. Only stems, generators, core links and shaping tools unlock; groves and labs are limited by slots and power. "Day N" means the start of day N. Draft week one, to be refined through play (thresholds in `Tuning.cs`):

| Genes (total) | Unlock | Lands (pacing run, median) |
| --- | --- | --- |
| Start | Core (1 link), 4 stems, 1 generator, 1 Gravity Pot | — |
| 200 | +3 stems | 20 min |
| 1,500 | +1 generator | 1.2 hr |
| 7,200 | +4 stems, +1 generator, +1 Trellis | 4.5 hr |
| 15,000 | +1 core link, +1 stem, +1 generator, +1 Gravity Pot | 8 hr |
| 70,000 | +4 stems, +1 generator, +1 Gravity Well | Day 2 (31 hr) |
| 200,000 | +3 stems, +1 generator | Day 4 (93 hr) |
| 470,000 | +3 stems, +1 generator | Day 7–8 for some gardens |
| 1,000,000 | +3 stems, +1 generator | Not by day 8 |
| 2,500,000 | +5 stems, +2 generators (30 stems, 10 generators, 2 core links; 2 pots, a trellis and a well) | Not by day 8 |

Jeremy's draft, meant to reach the end in about seven days. At the current economy it doesn't: gene income stays near 50,000 a day, so a garden ends week one with 19 stems, 6 generators and 2 core links (41 main nodes, 6–10 labs, 5–9 groves, about 95 fruit). The track ends here; long-term pacing comes later.

The timings assume a smart player actively tending (see Vision and pillars): the pacing simulation scripts one. It checks in every two hours while awake (16 hours a day), plants the first grove and lab at once, keeps labs within four links of the core (further out, most of their gene blobs wander off), fills the stem cap nearest the core and the generator cap where the plant is hungriest, keeps a grove per 15 stems, and prunes wild groves past that and labs that wilt or drain the garden. Wild growth fills every free link within minutes of an unlock or a prune, so between check-ins the plant fills itself with wild labs and groves. A garden with no labs makes no genes and unlocks nothing, and one with no grove doesn't grow. Proposed: the free track running to roughly 100 stems, and up to 5–6 core links through step 5.

**Long-term pacing: TBD.** Week one has to feel right first; pacing beyond it (bloom timing, how long a garden takes to mature) gets planned once it does.

Open:

- How seedling slots grow. Options: a separate track, or a capstone after every N core levels (recommended).
- Which basic node each evolution turns into, and what its unique fruit does.
- The Hub's role. Proposed: local upgrades that boost only its own branch.

## Crawlers

Crawlers aren't developed until after the public beta (step 7+). When they arrive, they should feel like the garden coming alive. When they unlock in a garden's life is a placeholder until then. Starting assumption: about one week into a garden's life, to be confirmed in step 7.

- They start simple, keeping the original's traits: a home node, preferences, autonomous behaviour.
- They eat fruit. A fruit is either a modifier on its node or food for a crawler, so eating a leaf removes the stem's bonus until it regrows.
- No "kill" framing: the original's execution, frustration and starvation states become a gentle life cycle (creatures age, rest, and return to the soil).
- **Later layer, not in the current design:** a crawler society with buildings, ownership, automatic pruning and consuming, and rebellions to quell. Today's crawlers should leave room for it without building it.
## Desktop widget

| Feature | Priority | State |
| --- | --- | --- |
| Frameless window, transparent background | Must | Done (prototype) |
| Always on top | Must | Done (prototype, menu toggle) |
| Click-through (Ambient mode) | Must | Done (prototype) |
| Low idle CPU / memory, pause while hidden | Must | Done (prototype) |
| Display modes: Active / Ambient / Minimised, hotkey | Must | Done (step 2). Active (default) is full opacity with the toolbar, menu and readout; Ambient fades the window to 55% and lets every click through; Minimised minimises the window and pauses the garden like a locked screen (caught up on return). Ctrl+Alt+G toggles Active and Ambient from anywhere (Windows) and restores a minimised garden, as do its taskbar button and the tray icon |
| Remembers size and position | Must | Plant scale saved; the overlay covers the work area and Minimised is a plain minimise, so there is no window position to keep. The display mode isn't saved: every launch starts in Active |
| Tray icon: show/hide, quit | Should | Done (step 2). Click to show or hide (Minimised); right-click for the menu (modes, Quit) |
| Readout: the garden's stats | Must | Done (step 2, expanded in 3b). Tinted lines under the plant (over it if there's no room below) in Active mode, fading in over 1 s while the pointer rests on a part and out over 1 s after it leaves; an Undo pill shows above them while the last prune can be undone. See Information display |
| Selected part panel | Must | Done (step 3b). A small card above the readout's lines for the part last tapped. See Information display |
| Build mode | Must | Done (step 3a). Within Active: an opaque disc round the plant, the shaping bar, tools to move, turn and remove. See Shaping the garden |
| Launch on startup (opt-in) | Should | — |
| Pinned to the desktop layer instead of on top (user choice) | Could | — |
| Notifications for rare events (opt-in, very sparing) | Could | — |

### Information display

Step 3b. The plant is stupid and the player is the brain, so the player needs the numbers a decision turns on, at a glance and without a menu. Both panels follow the readout's rules: Active mode only, shown while the pointer rests on the plant (or the panels themselves), fading in and out over 1 s. They are compact centred lines, each tinted by its role's hue, so they read on a desktop widget. The numbers come from the rules (`GardenStats`, and each kind's spec in `NodeSpecs` in `godot/src/Core`), never worked out in the view.

**The readout** (the whole garden), one line each:

| Line | Shows | The decision it serves |
| --- | --- | --- |
| Power | Held against the total store (and %), made against used per second; turns red-orange and says "overloaded" when the garden would cost more full than it can make full | Prune or add a generator so the garden can run full |
| Genes | Genes so far, and genes reaching the core per real minute | Whether the labs are placed well |
| Next unlock | What it gives, its threshold, genes still to go, and an ETA at the current rate ("plant a lab" with none) | Whether to wait, or add or move labs |
| Room | Stems and generators against their caps, core links used against allowed, fruit grown against fruit room | Where there's room to bud, and what to prune for it |
| Flowers | Groves and labs, and the wild buds forming by kind (or that nothing grows wild without a grove) | What's about to take the free slots, to prune early |

Gene income is expected, not measured: each lab's blob rate now times the chance its blob finds the core before it fades (`GardenStats.GeneReach`, the blob's random walk worked out as odds). It's steady from the first moment, and the same odds tell the player what a lab's placement is worth.

**The selected part panel.** Tapping a part selects it (as well as opening its toolbar, as before); the selection stays, ringed, after the toolbar closes, until another part is tapped, it's pruned, or a click lands off the plant. A dim card above the readout's lines shows its name (in its hue) and its power held against its store, a one-line description of what it does (one per node kind, in its `NodeSpec`), and its own numbers: power made or upkeep per second, fruit against its room, what's forming in it and how far along, and for a lab the share of its genes that reach the core and its genes per minute.

## Platform: Godot 4 (.NET)

**Decided (October 2026): Godot 4.7 with C#**, laid out like the other Godot projects: the rules in plain C# (`godot/src/Core`, no Godot), the presentation in the Godot project (`godot/game/`). The prototype in [godot/](../godot/README.md) is the starting point: the TS game's force-directed plant as a transparent, click-through, always-on-top overlay over the screen's work area.

Why, over the alternatives:
- **Web (TS + Pixi in Tauri or Electron):** heavier for an always-running widget, and transparency and click-through go through the wrapper. Godot gives native transparent, borderless windows; click-through is a few Win32 calls (`ClickThrough.cs`).
- **A lighter shell (a custom .NET app on raylib)**, measured in [spike-lite/](../spike-lite/README.md): the same idle CPU as Godot, since most of the idle cost is waking to redraw and present a full-screen transparent window. Its only gain was memory (about 130 MB against 250 MB), not worth rebuilding Godot's UI, input, text, audio and export pipeline.

Because the rules don't depend on Godot, the presentation layer can still be swapped later if that ever changes.

## Development plan

Each step should leave the game playable. The garden alone, before crawlers, should be fun and rewarding enough for a public beta.

| Step | Name | Contents |
| --- | --- | --- |
| 1 | Foundation (done) | Godot widget, transparency, force-directed physics, node and fruit rendering, stress test, time model (wall-clock sim, catch-up, low-power idle) |
| 2 | Living garden | Power economy, upkeep, wilting; visible power flow (link flashes, node brightness); new sprout, prune with undo, drag, display modes; wild growth, capacity and the node budget; groves, growth blobs, fruit, labs, genes. Light progression: genes reaching the core automatically raise caps along a fixed, linear unlock sequence, with a small notification and no menu or choices |
| 3a | Shaping (done) | Gravity Pot, Trellis, Gravity Well, placed and connected in build mode. See Shaping the garden |
| 3b | Information display | The hover readout expanded to what an active player decides by (power against its store, gene income, the next unlock's ETA, caps used, flowers, wild buds forming), and a panel for the selected part (name, power, what it does, its own numbers). See Desktop widget |
| 3c | Visual polish → MVP | A short round of visual polish, including the prune effect. End of step = MVP |
| 4 | Information and tracking | Stat tracking over time, Almanac, any information display 3b left out. Supports tuning |
| 5 | Evolutions and rares | Choice-based core levels, large stems, seedlings, special basic nodes, rare wild pool with caps and rerolls |
| 6 | Polish → public beta | Garden-only proof of concept, polished, with onboarding (its first hint: plant a grove, since nothing grows wild without one). Revisit idle CPU against the benchmarks before release. End of step = public beta candidate |
| 7+ | Post-beta | Crawlers (when they unlock in a garden: placeholder, decided here) and everything after |

**Idle CPU before the public beta (step 6):** revisit it against the benchmarks. Current levers: a lower idle frame rate in Ambient mode, and a window sized to the plant's bounds instead of the full work area.

**Design needed before each step:** the linear unlock sequence before step 2; choice-based core levels and seedling slot timing before step 5.

Open: where events and seasons, and identity features fall in the plan.
## Parked ideas

From the first draft, not contradicted by the anchor but not scheduled. Candidates for the "Events and surprises" and "Identity" topics:

- Each bloomed seedling releases a seed that drifts up and becomes a star in the background, a visible record of every evolution.
- Day and night tied to local time; seasons over weeks.
- Rare events: visiting comets, creature migrations, special blooms.
- Lifetime garden stats (age, blooms, tallest stem) as Almanac material.
- Onboarding as gentle, skippable hints spread over the first days.

## Open questions

None at the moment.

Settled since the first draft: platform (Godot); "is there any care need?" (no; neglect only slows growth, via wilting). Settled in the pacing pass: no power blobs (power stays link flashes and node brightness, at a cadence of every few seconds); blob rates are at real speed. Settled Oct 26:
- **One plant per garden.**
- **No save migration until release.** Old v29 saves are not carried over, and prototype save formats may break freely before release.
- **Distribution:** probably Steam.
- **Catch-up cap:** keep 72 h for now; tweak later if play shows a need.
- **Core links:** the core starts with 1 link and gains +1 at day 3 (already in the unlock track); the code's current 2 is a bug.

## Next topics

Design layers still to work through:

- [x] Information display: the readout and the selected part panel (step 3b)
- [ ] Stat tracking and Almanac, including what form the Almanac takes (step 4)
- [ ] Specialist effects: Resistor, Amplifier, Buffer (step 5)
- [ ] Long-term pacing beyond week one
- [ ] Choice-based core levels and seedling slot timing (step 5)
- [ ] Which basic node each evolution becomes, and its fruit (step 5)
- [ ] Onboarding over the first days (step 6), starting with planting the first grove
- [ ] Identity: what makes each garden uniquely yours after months
- [ ] Events and surprises: rare blooms, seasons, day and night
- [ ] Audio: likely silent by default
- [ ] Final name for genes
