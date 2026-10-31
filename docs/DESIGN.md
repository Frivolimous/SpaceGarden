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

- **Never punishing.** Absence is fine; coming back is a pleasant surprise. The only consequence of neglect is a garden running low on power (see Economy), working slower, and it never destroys anything. The one exception (step 5): a wild flower whose own life cycle ends it (Ember, Comet Bloom), never neglect; see Wild flowers.
- **Lightweight presence.** Runs all day without anyone noticing it in CPU, memory or battery. See Performance budget and its benchmarks.
- **No progress gated on the window being open.** Time away is caught up (see Time model).

**Out of scope for now:** spells (cut), mandatory prestige resets (removed). The Almanac is peripheral: a journal of moments, arriving in step 4.

### Why the revival

The last iteration (v0.0.2, 2024) became a tactical idle game: a mandatory prestige loop, spells on cooldown, kill counts, frame-rate-tuned pacing. Its systems were good; their framing lost the charm. The revival keeps the plant, the flowing particles and the creatures, removes the reset, and rebuilds the economy around slow growth and pruning.

## Core loop

```
 glance ─► notice something (new growth, a dim branch, a full seedling, a wild flower)
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
| Saturation | State: dormant |
| Fruit | Modifiers on a node; later also crawler food |

Unique shapes double as colour-blind support. Texture and complex shapes are held in reserve if playtesting shows a state is hard to read. The garden should look consistent: a wild flower keeps its base node's hue, at most with a subtle shift, and only passive-only kinds are white.

How the wild flowers and the Hub map onto the roles:
- **Specialist (grey/white):** Resistor, Amplifier and Buffer, plus the other passive-only wild flowers, which have no base hue. They change how blobs and neighbours behave rather than producing a resource.
- **Creatures (blue):** none yet; crawlers bring their home node (step 7).
- **Structure (green):** the Hub reads as a stem visually, drawn in its own shade of green. This is visual only: it doesn't take a stem slot or count against the stem cap.

The plant must look good **without any UI chrome**. Controls appear only in Active display mode (in Ambient, only the core takes a tap, to wake the garden).

## Economy

Three flowing resources move visibly through the stems; capacity is a set of limits, not a collected resource. Power shows as link flashes and node brightness; growth and genes travel as blobs. Blobs flow constantly, each carrying a small amount. Together they are the garden's "living light", its main idle visual.

| Resource | Colour | Source | Purpose |
| --- | --- | --- | --- |
| Power | Yellow | Core, generator flowers | Maintains nodes and runs the flowers |
| Growth blobs | Orange | Grove flowers | Wander the stems and grow fruit and new buds where they're absorbed |
| Gene blobs | Purple | Lab flowers | Wander the stems; only the core and the Hub absorb them |

There is no core trickle of growth: groves are the only source of growth blobs, and a new garden has none. The player plants the first grove by hand (step 6's onboarding points there first); its bud grows on its own, like every node bud, and four times as fast while the garden has no grove (see Growth and pruning).

**Power is a light show, not the pace.** Power fills and drains in seconds, as in the original, and keeps moving: the fills rock around a balance that shifts as the plant changes, never settling. It doesn't set the garden's pace (the gene tracks do, through what the player buys with them); it shows the garden's health, and a low garden works slower.

**Everything follows its fill.** The core and generators make power in proportion to their fill (none once full). Every part's upkeep is in proportion to its fill too, so a part never quite empties. Groves and labs send blobs in proportion to their fill. An overloaded garden (one that would cost more full than it can make full) doesn't break: it settles lower, its far flowers lowest and slowest, until the player prunes or adds a generator. Nodes are never destroyed. A new node starts empty and fills from its parent.

**Wilting (a design concept, not in the game).** Nothing wilts as a state: low power is shown only by a dim, shrunken node. Wilting stays a concept to reach for if neglect ever needs a stronger signal: a node out of power stops draining and is drawn dimmed, shrunk and slightly desaturated until it refills.

**Blobs.** Growth and gene blobs both do the same stupid random walk along the stems: never straight back unless at a dead end, fading after 30 links.
- **Gene blobs** pass every node by and are absorbed (100%) only by the core and the Hub. A lab far from the core loses many of its genes on the way.
- **Growth blobs** are absorbed at 30% by a node with nothing forming, and at 100% by a node with a fruit or bud already forming. A full node (no fruit room and no free link) never absorbs.
- **Fruit or node.** A blob absorbed fresh at a node with a free link slot and room under the caps rolls between starting a fruit and starting a node bud (50/50, to tune); otherwise it starts a fruit. A passive bud's type, and whether it grows wild (see Wild flowers), are rolled when it starts, and the bud holds its link and its cap slot until it's done. Each new fruit or bud on a node starts again at 30%.
- **Forming.** Each absorbed blob adds 0.1; a fruit completes at 1.0 and a node bud at 3.0. A node bud, passive or manual, also grows on its own (see Growth and pruning); a fruit grows only from blobs. A forming fruit grows in scale beside its node. A forming node shows as a full-sized silhouette inside its parent from the moment it starts, dark and grey at first and brightening into its final hue as it grows, so a new bud is plain to see; it doesn't exist in the physics until it's complete, then pops out as its link extends. Buds can't be cancelled: the player prunes after one emerges.

Draft rates, to tune in play, at real speed: a fully powered grove sends a growth blob about every 3 s and a lab a gene blob about every 5 s. A gene blob carries one whole gene, so the player can see and count them; the 0.1 per blob is growth blobs only. At these rates passive growth fills the caps within minutes of an upgrade, so the genes needed for the next one (see Progression) set the garden's pace.

**Local power flow.** Each node stores its own power, which moves along links in clumps, as in the original (`PlantNodePower.attemptTransferPower`); there is no global pool. About three times a second (each economy step), each node fuller than a quarter sends a clump to its emptiest neighbour that's less full than itself. Each kind has a transfer rate (the core about 29 a second, stems 30, generators 18, groves and labs 15), and a clump is that rate times the step's length, so the step only sets how chunky the transfers are. Emptiest is judged by fill over weight: stems weigh 1.1 and the core 1.5, so power heads out along the stems. Clumps, not a smooth flow, are what keep the fills moving. Each clump flashes its link (brighter when the receiving node is fuller), at its own moment so the links don't flash in step, and each node's brightness and size follow its fill. A branch far from power runs lower on its own while the rest thrives, so generator placement matters.

**Fruit holds power** as in the original (`PlantNodePower.powerFruits`): each fruit holds 100 and costs nothing. A part at least 60% full feeds its emptiest fruit (15 a second) while that fruit is less full than the part, so new fruit start empty and brighten as they fill, and a garden's fruit only glow where their parts have power to spare. Fruit sends nothing on, and its effect (a leaf cutting its stem's upkeep, and so on) doesn't depend on its fill.

**Size follows fill**, as in the original (`PlantNodeView.intensityInterpolation`) but reworked for the larger stores (step 4). A node's scale and brightness run from 0.2 empty to 1 full along the square of its shown fill (`FillLookCurve`): flat near empty, so low nodes don't jump about, and steep near full, where a tended garden spends its time (about 70-105% full). Past full a node swells at half its overfill (`OverFillLook`), up to 1.5 times its size. The original's straight line, with a tenth past full, left a node at 75% and one at 95% looking alike, and an overfull stem barely moving. The shown fill eases after the real one, faster rising than falling. There's no artificial bounce: the clumps arriving are the motion.

**Power rules**

- The core is the main source. A generator produces half of the core's output. Core power upgrades come later, not in the MVP.
- Stems are cheap to maintain. Leaves cut a stem's upkeep: each takes a quarter off, so two leaves halve it. Leaves grow from growth blobs, like all fruit.
- A lab costs half as much again as a grove to keep (6 a second against 4).
- Generator, grove and lab fruits each raise their flower's output by their `FlowerBonus` (a tenth: a generator holds two fruit, so +20%; a grove or lab three, so +30%). A flower's upkeep is multiplied by 1 plus its fruits' `FlowerPowerFactor` (a leaf's is -0.25). Both are set on the fruit's own spec.
- The core has no fruit.

Fruit, once grown, stays. Groves are needed while the garden grows; if growth stalls, they stop being useful. The upgrade costs should be paced so a small number of groves always stays useful, keeping up with new stems and flowers.

**Grove tuning target.** Tune the grove's growth blob rate so one or two groves can just keep up with leaf demand from new stems as well as their share of passive buds. Each new stem has room for two leaves, so a full stem track's 26 new stems (4 to 30) mean about 52 leaves.

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
| Per-type caps | Structure and power only: stems and generators, each raised by buying upgrades. Groves, labs and other basic flowers are uncapped; they are limited by free link slots on stems and by power upkeep, so the player chooses the mix |
| Wild flower limit | One total for all wild flowers, raised in the core track and by seedlings. A wild flower with a capped base takes its base's slot too (a Tendril a stem's, a Pulsar a generator's). No per-kind caps (see Wild flowers) |
| Seedling slots | Seedlings planted or bloomed; a slot stays used after its seedling blooms, so each is one evolution |
| Local caps | Links per node and fruit per node. Stems are fixed at 3 links including the parent, so each stem supports a branching stem, a flower plus a continuation, or two flowers at a dead end. Big Stems (4 links) are a wild flower, unlocked by their evolution. The core starts with 1 link and gains one in the core track (4,000 genes); third and later links come only from evolutions |

A bloomed seedling becomes a wild flower and takes a wild flower slot, and its base's slot where it has a capped base (and adds one to each), so evolutions need no separate limit.

## Performance budget

Plan for a soft cap of **250 main nodes** in a fully matured garden, to be refined through play.

- Stress test: after optimisation, the system supports upwards of 300 main nodes with negligible idle overhead (previously around 220). A setting limits idle frame rate (15/20/30/60 fps, default 30) for further savings.
- Fruit adds negligible load: tested at 3 fruit per main node at 220 nodes. Fruit borrows its parent's push in the physics, so it stays out of the collision grid.
- The 250 soft cap leaves headroom for future blob and crawler load and weaker machines.
- The sum of all capacity limits at their maximum should stay within this budget, so the capacity system enforces it. With 3-link stems, a garden with S stems, C core links and I inline (2-link) flowers has at most 2S + C + I + 1 main nodes, so the stem cap effectively sets the garden's maximum size (about 124 stems for 250 nodes). Each large stem adds one more slot.

Evolutions stay within the budget because a bloomed seedling is a wild flower in a slot like any other, not an extra node.

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
- **Absence never hurts.** Catch-up can't destroy nodes (bar a wild flower whose own life cycle ends it, such as Ember; see Wild flowers); the worst case is an overloaded garden running low, which the player fixes by pruning.
- **While you were away** (step 4): on return, a short note says what changed (new growth, power dips, and in step 5 seedlings bloomed, new wild flowers, Hub points and burned-out Embers), drawn from the tracking and the Almanac. The old "unlocks reached" went with the unlock track; since 5b it lists shaping tools gained from the garden's size.

## Growth and pruning

Once it has a grove, the garden grows passively until it fills its capacity; attentive players prune passive growth to make room and place buds where they want.

- **Passive growth** is random: it's wherever growth blobs land (see Economy, Blobs), with no check on power. Passive growth that drains the garden is the player's to prune. Once a garden has a grove, passive buds can be groves too; a garden with none grows nothing passively.
- **Manual buds** cost nothing. The cost is attention. The player picks the type (stem, generator, lab or grove) and the spot, and a bud starts there at once: the same silhouette inside its parent as a passive bud, holding its link and cap slot from the start. A part forms one bud at a time; a fruit forming there gives way to the player's bud. While a bud forms on a part, its kind's button in the part's toolbar keeps its colour and is ringed by a pulsing outline. When every link of a part is taken, a note over its bud buttons says so ("3/3 Connections Used"); prune stays beside it.
- **Buds grow on their own.** Every node bud, passive or manual, grows slowly by itself, at a fixed rate scaled by its parent's fill, so buds on dim branches grow slower: about 2 minutes from speck to emergence at full fill (`Tuning.BudSelfGrowSeconds`, a draft), and none at all on an empty part. Growth blobs still speed a bud up and are absorbed at 100% as before (at full fill one blob is worth about 4 s of growing alone). Passive and manual buds differ only in how they start: a passive bud from a growth blob, a manual one from the player. Fruit never grows on its own, only from growth blobs, so groves keep their purpose.
- **Tuning the self-growth rate:** fast enough that a new player's first grove emerges within the opening minutes, slow enough that a grove-fed bud is noticeably faster. At the draft rate an ordinary bud takes about 2 minutes, and a bud beside a grove under one. **The first grove is faster:** while the garden has no grove (a new garden, or one that pruned its last), a grove bud grows four times as fast (`Tuning.FirstGroveSpeed`), so a new garden's first grove, planted at once, is out in about half a minute rather than two minutes of nothing happening (Jeremy, balance pass, Oct 29).
- **Pruning** frees capacity. A full garden can only grow where the player has cut.
- **Wild flowers** are not chosen: a new bud, planted or passive, may grow wild by chance (see Wild flowers). Pruning one frees its slot, and a later bud may roll a different kind in a different place.

**Pruning gives nothing back.** Its rewards are built in: it frees stem and generator capacity for deliberate growth, lowers power upkeep, reshapes the garden, and lets a new bud roll for a wild flower. With no return, passive growth can't be farmed. The cut itself must feel satisfying: every link in the pruned branch is cut, and its loose parts drift off as the plant's push (and their own) sends them away, fading out from the moment of the cut over a few seconds (`Tuning.CutFadeSeconds`, 5 s).

**Undo and passive growth.** While a prune can be undone, growth blobs start no new buds anywhere and no new fruit on the pruned branch's parent (they still feed what's already forming), so an undo always has room to restore the branch. A manual bud may still take the freed room (it holds the slot at once), which ends the chance to undo.

Settled Oct 29 (Jeremy, step 3e), ruled out for manual planting:
- **No mote store or spending:** manual buds stay free; attention is the cost.
- **Not passive growth only:** the player keeps planting by hand.
- **The plant doesn't grow its own first grove,** and **the core makes no motes:** a grove can always be planted, and grows on its own like any bud.

## Tending commands

The player has eight basic commands.

1. **New sprout:** choose a basic node type and start a bud of it at a chosen spot; it forms and emerges on its own (see Growth and pruning). A part with room only for its parent's link (a generator, grove or lab) offers no buds in its toolbar, only prune.
2. **Prune:** remove any node and all of its children. No preview; undo restores the last prune only, within about 30 seconds (to be tuned).
3. **Place a shaping tool** (Gravity Pot, Trellis, Gravity Well) in build mode: a subtle overlay, placed, moved and turned anywhere (see Shaping the garden).
4. **Connect a node to a Gravity Pot or Trellis** in build mode, by dragging the node onto it: in a pot it moves to the pot and locks there, and the nodes linked to it (its parent and what grows straight out of it) are pushed gently in the pot's facing direction; on a trellis it's held on the trellis's line.
5. **Select evolution:** on a growing seedling, a radial menu of 3 random evolutions (see Progression).
6. **Zoom** in or out.
7. **Display mode:** Active (full opacity, controls visible, clickable, the default), Ambient (faded and click-through, except the core: it brightens and is ringed under the pointer, and a tap wakes the garden to Active with the core's menu open), or Minimised (to the taskbar and tray). Ambient and Minimise are slots in the core's menu; a hotkey toggles Active and Ambient, and brings a minimised garden back.
8. **Drag:** click and drag any node to tug the plant around on a spring. Purely tactile, no economy effect.

Undo covers only the most recent prune. Passive growth leaves the freed capacity alone until the undo window passes (see Growth and pruning).

## Shaping the garden

Players keep a mature garden compact and give it form through zoom and shaping tools.

- **Zoom:** the player can zoom the garden in or out.
- **Gravity Pots:** a node connected to a pot moves to it and locks in place; the nodes linked to it (its parent and what grows straight out of it) are pushed subtly in the pot's facing direction, and the rest of the plant follows them.
- Long branches can still spill back over, so a large garden may need several pots.
- Passive growth respects pots, so a direction set once keeps shaping the garden.
- Shaping tools are purely about shape, with no economy effect.

**Three shaping tools** (step 3a, done; since 5b they come with garden size, the highest number of main nodes ever reached, not gene thresholds: see Progression). They're overlays, not graph nodes, so they take no node slot and count against no cap but their own. The rules are in `godot/src/Core/Shaping.cs` and `Garden.cs` (Shaping), their strengths in `Tuning` (Shaping).

| Tool | What it does | Holds | Unlocks |
| --- | --- | --- | --- |
| Gravity Pot | The node dropped on it moves to the pot and is locked there; the pot is drawn over the plant as a flower pot the node sits in up to its middle: a rim straight across the node's middle and a round bowl just bigger than the node, on the side away from the way it faces. Only the main nodes linked to it (its parent, and those growing straight out of it) are pushed gently the way the pot faces; nodes further out just follow them through their links. New buds on the potted node head that way too | One node (dropping another swaps it) | 1 from the start, a second at 25 main nodes, +1 at 60, 100, then every 50 |
| Trellis | A straight line between two posts. The nodes dropped on it are held on the line but slide along it, so the plant's own push spaces them out, like an espalier wire | Any number | 1 at 15 main nodes |
| Gravity Well | Pulls every node towards it, the core and fruit too, alike whatever their weight, like gravity. A free plant drifts to the well and settles round it; to keep the plant where it is, put its core in a pot, and it hangs towards the well instead | Nothing | 1 at 40 main nodes |

Fruit is never tied or pushed by a pot: it follows its node (a well pulls it along with everything else). A pruned node lets go of its tool; undoing the prune ties it again if the tool is still there (and a pot still free). Pruning or removing never refunds or costs anything.

**Build mode** is where the garden is arranged: the core menu's Build switch (or Ctrl+B with the garden focused) in Active mode; Build again, Done, Escape or leaving Active ends it.
- A dark, opaque disc fades in round the plant and its tools, catching every click on it, so arranging never clicks through to the desktop.
- A bar above the disc holds the three tools (each with how many more the caps allow, greyed, with the garden size that brings it, until then) and Done. Pressing a tool takes a new one off the bar and carries it until let go; a tap sets it down just under the bar.
- Drag a tool's body to move it, a pot's knob (just beyond its node) to turn it, a trellis's end posts to swing and stretch it. A potted node is locked, so dragging it moves its pot, in build mode or out (a tap still opens the node's toolbar). Handles come before nodes under the pointer, since a pot's knob sits where its branch grows. Tapping a tool opens a small toolbar with Remove; dragging a tool back onto the bar also puts it away.
- Dragging a node and letting go over a pot or trellis ties it there, and an empty pot let go over a node plants it. A tied node's toolbar gains Unhook.
- Each node that can have 2 or more links shows its links used against its most, "used/most" (a forming bud counts, as it holds one): white at 1/N (only its parent), yellow in between, red when full (N/N). So the player sees at a glance where there's room to bud (Jeremy, Oct 29).

**How they show.** A pale moonlight blue, no role's hue, in the parts' outline look (dark edge, crisp outline, dark fill): trellises and wells under the plant, pots over it. Each moves gently: chevrons rise past a holding pot's node, rings draw slowly in to a well. Faint while tending in Active mode (the pointer passes through them), where the well and a pot's chevrons are hidden altogether (the well keeps pulling), full in build mode, hidden in Ambient and Minimised. Saved with the garden, with what they hold (save version 7).

Settled Oct 29 (Jeremy):
- **Build mode is the gardening mode** from Jeremy's todo (the opaque radius for arranging the plant and its guides).
- **Passive growth carries on** while arranging; nothing pauses.
- **The backdrop may grow to fill the screen** round a very large plant and its tools.
- Outside build mode the tools stay inert, except that dragging a potted node moves its pot.

## Node roster

Nodes come in two tiers. Main nodes are called **stems** (the structure) and **flowers** (everything else: generators, groves, labs and wild flowers); they can be grown and pruned. **Fruits** grow on stems and flowers and add minor or enhancing effects. Most flowers have 1 link and end a branch; Amplifier, Resistor and Twin Star have 2 links and sit inline, continuing a branch.

| Group | Nodes | How they appear |
| --- | --- | --- |
| Fixed | Core | Always present, can't be pruned |
| Plantable | Stem, Generator (yellow), Grove (orange), Lab (purple) | Passive growth or manual buds; the first grove only by a manual bud |
| Wild flowers | 15 kinds (see Wild flowers) | Any new bud, planted or passive, can grow wild, within the wild flower limit; a bloomed seedling becomes one |
| Seedling | Seedling | Planted by hand from the toolbar, never passive (see Progression) |
| Hub | Hub | Planted by hand, one at a time; not a wild flower (see Progression) |
| Fruits | Leaf on stems and each flower's own fruit set | Grown by growth blobs |

Each flower type has its own set of fruits, all grown by growth blobs. Leaves on stems cut the stem's power upkeep; generator, grove and lab fruits raise their flower's efficiency (see Economy).

## Wild flowers

A new bud can turn into a special kind. That is **wild growth**, and the results are **wild flowers**. They are quirks, not upgrades: each trades something for something, and the player prunes what they don't want. (Settled Oct 30, step 5; slots, effects and fruit revised the same day by Jeremy's addendum. Every number here is a placeholder for playtesting.)

- **Any new bud can grow wild**, planted or passive. The chance is per free wild flower slot and differs by how the bud started: `PassiveWildChance` 5% for a passive bud, `PlantedWildChance` 4% for a planted one (two tuning variables; whether planted buds should grow wild at all is watched in playtesting). It is zero when no slot is free. The roll is made when the bud starts.
- **One total limit**, the wild flower limit, raised in the core track and by seedlings. A wild flower with a capped base counts as **both** its base type and a wild flower: a Tendril or Big Stem takes a stem slot too, a Pulsar, Twin Star or Ember a generator slot. The Amplifier, the Resistor and the passive-only kinds take only a wild flower slot; groves and labs are uncapped, so the grove and lab kinds do too.
- **No per-kind caps** for now (the data still supports them). An unlock evolution only adds its kind to the pool, plus its boost. A seedling's bloom adds +1 to the wild flower limit and +1 to its base type's cap, so its own flower costs nothing.
- **Pruning frees the slot.** A later bud may roll another wild flower, possibly a different kind in a different place.
- **Balance target:** one wild flower slot is worth about 0.65 labs, so each kind should be worth its base plus about that. Upkeep and transfer rate are the fine-tuning dials. Grove kinds are judged by feel (growth is rarely what a garden lacks), and the Amplifier, which scales with the labs beyond it, may well beat the target: it rewards a good shape.
- **Looks:** each kind has its own, more complex, radially symmetric shape. The base node's hue is kept, at most with a subtle shift. Passive-only kinds have no base and are white, and so are the Amplifier and the Resistor, the specialists (Jeremy's review, Oct 30). Shapes echo the base where that works (Pulsar is a 5-pointed star).
- **Unlocked** says when a kind can start to grow wild: **Start** kinds from the first wild flower slot, **Evolution** kinds once the matching evolution has been taken (see Progression). A bloomed seedling always becomes its evolution's own kind.

| Wild flower | Effect | Cost | Grows from | Unlocked | Fruit |
| --- | --- | --- | --- | --- | --- |
| Amplifier | 2 links; gene and growth blobs passing through grow 50% larger | 1 wild | Stem | Start | 1 glow |
| Resistor | 2 links; blobs bounce off it from both sides; low transfer | 1 wild | Stem | Start | 1 glow |
| Tendril | 3 links; long springy link, large power store, medium transfer | 1 wild, 1 stem | Stem | Start | 3 leaf |
| Big Stem | 4 links; slow transfer | 1 wild, 1 stem | Stem | Evolution | 2 leaf |
| Pulsar | 1.5× a generator's power, released in bursts | 1 wild, 1 generator | Generator | Evolution | 2 burr |
| Twin Star | 2 links; 80% of a generator's power | 1 wild, 1 generator | Generator | Evolution | 2 berry |
| Ember | Starts at 2× a generator, 1× at 1.5 days, keeps fading and burns out at 3 days unless pruned earlier | 1 wild, 1 generator | Generator | Start | None |
| Lumber Grove | 2× a grove's growth blobs at 1.5× upkeep | 1 wild | Grove | Start | 5 wood |
| Seed Pod | 1.5× a grove's growth blobs, in bursts | 1 wild | Grove | Start | 3 berry |
| Wild Lab | A lab's genes; its fruit bonus is doubled (+20% each), up to 2× with all 5 | 1 wild | Lab | Start | 5 research |
| Lichen | 0.8× a lab's genes; 4 leaves bring its upkeep to 0 | 1 wild | Lab | Start | 4 leaf |
| Buffer | Buff blobs give the parts they pass a 50% production boost | 1 wild | Passive only | Start | 2 glow |
| Comet Bloom | One burst of gene and growth blobs worth about 2 hours of a lab and a grove; withers after 24 hours | 1 wild | Passive only | Start | 2 glow |
| Moonflower | Nearby buds much more likely to grow wild; wild flowers within 5 links get +10% output | 1 wild | Passive only | Start | 3 glow |
| Nebula Bloom | Power reaching seedlings within 5 links counts double | 1 wild | Passive only | Start | 3 glow |

**Fruit:** every fruit gives +10% to its parent's main effect (burr, research, berry, and **wood**, which gains a crawler use later), except leaves (−25% upkeep each) and **glow**, a white fruit that grows like any other and launches buff blobs like a small Buffer (a quarter of its rate; buffs don't stack).

The Comet Bloom's small value against the target is accepted: an attentive player prunes it at once for the slot, as with a fading Ember.

Home leaves the pool: crawlers bring their own home node (step 7). The Hub is no longer a rare; it is planted (see Progression). Resistor, Amplifier and Buffer are the grey/white specialists in the visual language, the garden's modifiers of flow and output now that spells are cut.

Ember and Comet Bloom end themselves, the one exception to "never destroys anything" (decided Oct 30): only a wild flower's own life cycle ends it, never neglect; its slot is freed and the away note says so. An Ember rewards attention: pruned once it falls below a plain generator, around day 1.5, it averaged about 1.5 generators, and its slot is free for what grows next; neglected, it just fades out.

**Built in 5d** (Oct 30; the rules in `godot/src/Core/WildFlowers.cs` and `Garden.cs` (Wild flowers), every number a first pass in `Tuning`, Wild flowers). Details settled in the build, for Jeremy to review:
- **The roll.** A bud's basic kind is rolled first, as before (so it still needs room under its own cap), then whether it grows wild. With no free slot no dice are thrown, so a garden without slots grows exactly as it did. If it does grow wild, its kind is picked among the kinds that can stand in for its basic kind, plus the passive-only kinds for a passive bud; a planted bud never becomes a passive-only kind. For a passive bud the pick is weighted toward its base's own kinds (decided Oct 30, built in 5c; first built evenly, which made a grove bud a passive-only kind two times in three): when both groups have kinds that can grow, a passive bud picks among its base's own kinds two times in three (`Tuning.PassiveBaseShare`) and among the passive-only kinds the rest, alike within each group. A wild bud forms as its own silhouette from the start, so the player sees what's coming. The forming bud's toolbar button still pulses for its base kind. (So an Amplifier or Resistor can't appear while the stem cap is full: accepted.)
- **Slots.** A wild flower and a wild bud each hold their slots (and their base's where they take one); undoing a prune needs them back. The garden card counts a Tendril or Big Stem among its stems, a Pulsar, Twin Star or Ember among its generators. `Garden.UnlockWild(kind)` adds an Evolution kind to the pool (5c calls it as the evolution blooms); `Garden.WildCaps` holds per-kind caps, none set.
- **Amplifier:** each Amplifier a gene or growth blob passes doubles it half the time (`AmplifyChance`), at most once a blob, so blobs stay whole genes and chains don't compound: 50% larger on average. The second blob carries on from the Amplifier with the hops the first had left.
- **Resistor:** never one of a blob's choices of where to go next, from either side, so no hop is wasted; a blob with nowhere else to go ends. The garden card's gene reach counts this exactly (not an Amplifier's doubling). At the base of a lab-free branch it keeps gene blobs from wandering in; labs and passive growth beyond it are cut off. It sends power on at 6 a second (a stem 30).
- **Buffer:** 240 buff blobs an hour, white; a glow fruit 60. A buff blob wanders like the others for 5 links (8 once past an Amplifier) and a glow's starts on the part it hangs from; nothing absorbs it, and every part it passes makes 50% more power and blobs for the next 60 s. A second buff only starts the time again.
- **Tendril:** its links, to its parent and to its own branches, 2.5 times as long and 0.3 as springy; holds 600 (a stem 150), sends on at 20 (a stem 30). **Big Stem:** 4 links, transfer 15. **Twin Star:** 9.6 a second, 2 links.
- **Pulsar:** makes 18 a second into a hidden charge, released into its store every 20 s of its life, swelling it past full until it has sent it on (transfer 24).
- **Ember:** 24 a second at first, falling in a straight line to 0 over 3 days, drawn darker as it goes; it burns out at 0. **Comet Bloom:** 3,840 blobs as it appears (1,440 gene and 2,400 growth blobs, mixed as a lab's and a grove's are), a wider, brighter halo, withers after a day. An ending leaves like a prune (the part drifts off and fades) but isn't one: nothing to undo, not counted as pruned. The part's card counts down ("Burns out in: 2d 4h").
- **Lumber Grove:** 2,400 growth blobs an hour at 6 upkeep. **Seed Pod:** 1,800 an hour, leaving 60 at once (every 2 minutes at full fill). **Wild Lab:** 720 gene blobs an hour and 5 research each counting double. **Lichen:** 576 gene blobs an hour at upkeep 3.
- **Moonflower:** each within 5 links of a bud's part makes the chance three times as high (`MoonflowerBoost` 2, adding up); wild flowers within 5 links of one make 10% more (not stacking).
- **Nebula Bloom:** `NebulaReach` (5) and `NebulaFill` (2), inert in 5d, read by 5c's seedlings (see Built in 5c).
- **Discovery:** a kind is discovered the first time it grows (`Tracking.Discovered`, with the age). A wild flower's discovery is the journal's moment ("Wild flower discovered: Tendril", in a pale gold) in place of "First tendril emerged"; an ending is one too ("An Ember burned out"). The **Codex** tab lists the discovered kinds under Parts, Wild flowers and Fruit, in the order found, each with when ("Found day 2, 14:03") and its Effect line (a wild flower's quirk after it). A save from before 5d counts what it grew as found at its start.
- **Part cards:** a wild flower's quirk under its name (what it trades, and the base slot it takes); Growth or Genes by what it sends.
- **Away note:** the basic parts grown as before, then "Wild flowers grew: an Ember and 2 Tendrils.", each discovery on a line of its own in the pale gold ("Wild flower discovered: Ember (see the Codex)."), then "An Ember burned out." These moments aren't repeated among the three listed.
- **Looks** (revised after Jeremy's review of the first sheet): Amplifier a white plus, Resistor a white minus, Tendril a Big Stem's lobed disc with three bumps, Big Stem four lobes, Pulsar a five-pointed star, Twin Star two pentagons, Ember five flame petals, Lumber Grove two squares (an eight-pointed star; "ok, not the best"), Seed Pod a plain oval, Wild Lab two triangles, Lichen a scalloped hexagon at a stem's size, Buffer a solid eight-pointed burst (its glow fruit too), Comet Bloom a white six-toothed gear (the Resistor's first shape, kept as an idea), Moonflower six petals, Nebula Bloom three curling arms (the Tendril's first shape). They come from one atlas texture, drawn in a batch of its own per layer, so every wild flower together costs one draw call for the parts and one for their buds. Hues are the base's, shifted a little; the passive-only kinds are white. Glow is a small white burst, wood a brown diamond.
- **Save:** still version 9 (not shipped to players), with optional fields: each part's life and charge, the Evolution kinds unlocked, any per-kind caps, the discoveries and the endings.

## Progression

Nothing resets: every upgrade applies immediately and permanently. Step 5 (Oct 30) replaced the automatic week-long unlock track with **spending**. Every number below is a placeholder for playtesting.

| Upgrade type | Example | Where it lives |
| --- | --- | --- |
| Linear / numeric | +1 stem cap, +1 generator cap, wild flower limit | The three gene tracks in the Research panel |
| Mechanic change / unlock / bundled change | Core power ×1.5, Big Stem unlocked, labs ×2 genes at more upkeep | Seedlings: evolutions |
| Shaping tools | Gravity Pot, Trellis, Gravity Well | Garden size |
| Per-kind boosts | +10% to a kind's main effect | The Hub's points |

*History:* through step 4, genes reaching the core raised caps automatically along one fixed track of about a week (nine thresholds up to 330,000 genes), with only about 55-70k genes a day arriving from day 2, since 40-60% of blobs reach the core by day 3.

### Upgrade tracks (5b)

**Genes are spent.** Labs' gene blobs still end at the core, which adds them to the garden's gene balance. (5a: `Garden.GeneBalance`, beside `Garden.Genes`, the genes earned over the garden's life, which never drop; spending takes from the balance alone. 5b replaced the old unlock track, which counted genes earned.) The core menu's **Research** slot opens a panel with three columns (Stems, Generators, Core), each showing **only its next upgrade and its cost**. How the player learns something is affordable is part of the alerts design (see Open for step 5); red dots were found distracting. A new garden starts with the core at 1 link, 4 stems, 1 generator, 1 seedling slot and 1 Gravity Pot.

- **Stems (+1 cap each):** 100, 150, 200, 300, 450, 650, 950, 1,350, 1,850, 2,450, 3,150, 3,950, 4,850, 5,850, 6,950, 8,150, 9,450, 10,850, 12,350, 13,950, 15,700, 17,600, 19,650, 21,850, 24,200, 26,700 (26 upgrades, to 30 stems).
- **Generators (+1 cap each):** 1,000, 2,000, 3,000, 5,000, 10,000, 15,000, 20,000, 25,000, 30,000 (9 upgrades, to 10 generators; the first three lowered from 1,500, 2,500 and 3,500 by Jeremy in the build, Oct 30, so 111,000 in all).
- **Core**, in order:

| Upgrade | Cost |
| --- | --- |
| Wild flower limit +1 | 1,500 |
| Core link +1 | 4,000 |
| Wild flower limit +1 | 10,000 |
| Seedling slot +1 | 20,000 |
| Wild flower limit +1 | 35,000 |
| Hub | 60,000 |
| Seedling slot +1 | 90,000 (placeholder, 5b) |
| Wild flower limit +1 | 120,000 (placeholder, 5b) |
| Seedling slot +1 | 160,000 (placeholder, 5b) |
| Wild flower limit +1 | 210,000 (placeholder, 5b) |
| Seedling slot +1 | 270,000 (placeholder, 5b) |
| Wild flower limit +1 | 340,000 (placeholder, 5b) |

The six after the Hub alternate seedling slots and the wild flower limit at costs set in 5b as placeholders (each about a third dearer than the last); then the track ends, as long-term pacing isn't designed yet. Third and later core links come only from evolutions (Second Path). Groves and labs stay uncapped, limited by free links and power upkeep.

**Built in 5b** (`Garden.Buy`, `Garden.Next`; the costs are data in `Tuning`, Research). Buying spends the balance and raises the cap at once; the core ripples in the genes' purple. The wild flower limit, seedling slots and the Hub were counters in `Capacity` until 5c, 5d and the Hub's step give them mechanics (5d gave the wild flower limit its, 5c the seedling slots theirs). The levels bought go in the save, still version 9 (5a's v9 shipped on master, and the new field is optional): a 5a save, which has none, keeps its caps and reads them as the stem and generator upgrades bought (a second core link as the core track bought up to it). The Research panel is a window like the Almanac's (its header dragged, ✕ or the slot to close): the balance in its header ("1,600 genes"), then a card per track in its role's hue (stems green, generators yellow, the core a light neutral grey, Jeremy's pick), each with a glyph of its part (a stem's node and links, a generator's pentagon, the ringed core) and only the next upgrade ("+1 stem", "+1 wild flower", "+1 core link", "+1 seedling slot", "Hub"), its cost beside a gene dot, and **Buy**, filled with the hue. While the balance falls short the button is dim and says how many more genes it needs ("300 more"), over a fill in the hue that grows left to right as the balance nears the cost; beside each title an (i) shows, the moment the pointer is on it (no tooltip delay), a hint of what the next upgrade does ("Increases the total number of stems you can add to your garden", "Lets special, rare wild flowers grow instead of normal stems or flowers": `Upgrade.Does`); a card the balance covers glows softly, breathing. A finished track reads "All bought", its glyph ticked. No caps or counts show (Jeremy, Oct 30: the garden card has those). No alert yet: that waits on the alerts design.

All three tracks share one gene income, and the stem and generator tracks will probably need trimming in playtesting.

**Shaping tools come from garden size**, counted as the highest number of main nodes (not fruit) ever reached, so pruning never takes a tool away. The save keeps that peak. (5a: it counts the core too, so a new garden is size 1 and its first stem makes 2; `Tracking.MostMainNodes`, exported as `most_main_nodes`.)

| Main nodes reached | Tool |
| --- | --- |
| Start | 1 Gravity Pot |
| 15 | Trellis |
| 25 | Second Gravity Pot |
| 40 | Gravity Well |
| 60, 100, then every 50 | +1 Gravity Pot |

**Wording, settled in the build (5b):**
- **The build bar:** a tool not yet gained is greyed, its tooltip saying "Trellis: comes at garden size 15 (now 9: the most stems and flowers it has had, the core too)"; one gained says "Another at garden size 60" while more will come.
- **A tool gained** is a journal moment, "Trellis gained at garden size 15", "Second Gravity Pot gained at garden size 25", in the tools' moonlight blue. Live, a short note in that blue says it ("… Place it in build mode."); after time away, the away note lists it first among the moments.
- **Purchases in the journal:** "Second core link bought" and "Hub bought", in the genes' purple. Stems, generators, wild flower and seedling slots bought aren't moments.
- **The totals export** swaps `unlocks_reached` for `upgrades_bought`, with `stems_bought`, `generators_bought` and `core_bought`.
- The garden card's lines are under Information display.

**Pacing assumption.** The timings assume a smart player actively tending (see Vision and pillars). The pacing simulation scripts one: it watches the first hour (a check-in every minute), then checks in every two hours while awake; it plants the first grove and lab at once, keeps labs within four links of the core (further out, most of their gene blobs wander off), fills the stem cap nearest the core and the generator cap where the plant is hungriest, keeps a grove per 15 stems, and prunes passive groves past that and labs that run dry or drain the garden. Passive growth fills every free link within minutes of an upgrade or a prune. A garden with no labs makes no genes, and one with no grove doesn't grow. Since 5b it also spends at each check-in: while the garden is short of power and its generators are all grown, it buys (or saves for) the next generator; otherwise the cheapest next upgrade across the tracks, again and again while the balance covers it (`SmartPlayer.Research`; `PacingTests.TrackPacing`, run by hand with `PACING=1`).

**Simulated pacing (5b, Oct 30; median of 8 seeds over 14 days, at the first generators' earlier 1,500, 2,500 and 3,500, before Jeremy lowered them; the 456,650 genes below are that total, now 455,150).** First stem 12 minutes in; first generator 6 h; second core link 30 h (day 2); 20 stems day 3.3; last generator day 8.5; last stem day 9; the Hub day 11.2 (range 10.4-12.1), when the 456,650 genes for stems, generators and the core up to the Hub have been earned (day 11.1). Genes earned run about 30k on day 1, then 45-50k a day (315k by the end of day 7), every garden kept at 80-90% fill. So the full stem and generator tracks fit in about nine days, a little past the old week one, and the Hub lands around day 11, about three days later than Review decision 6's 7-8 days. A player buying only the cheapest upgrade did much worse (one generator until day 2, half the gardens overloaded, the Hub not reached by day 14 in most): saving for a generator while short of power is the sensible play the costs assume, and the alerts design may want to nudge it. To retune at the real-week check.

**Long-term pacing: TBD.** Week one has to feel right first; pacing beyond it gets planned once it does.

### Seedlings and evolutions (5c)

- **Slots.** A garden starts with 1 seedling slot; more are bought in the core track. A slot stays used after its seedling blooms (counted in the garden's data, from the evolutions chosen), so each slot is exactly one evolution.
- **Planting.** Seedlings never grow passively. The player plants them from the toolbar, like a bud.
- **Filling.** A seedling fills with **power**, not genes, and its fill is simply its power against its store (Jeremy, Oct 31: no separate intake parameter). It has no upkeep, a heavy power weight (so its neighbours favour it) and no transfer rate (it never sends power on): it only takes power in. Its store is 200 before the choice; full, selecting it opens a radial menu with **3 random evolutions**. Choosing one raises its store to the target, 1,000 for the first bloom, then ×5 each time (5,000, 25,000 and so on); full again, it blooms.
- **Evolution record** (Jeremy, Oct 31). The Almanac lists the garden's evolutions, each with its current level and when it was chosen. Nothing shows on the core for now: marker fruits read as flowers bare and as UI buttons in bubbles, and gems set into the core's rim didn't read well either. To revisit if needed.
- **Bloom.** The effect applies immediately and permanently, and the seedling becomes the evolution's wild flower with a flashy effect, which also adds +1 to the wild flower limit and +1 to its base type's cap (so the flower, which takes both slots, costs nothing). Pruning the flower removes only the flower, never the effect or the marker. All effects, including bundled changes, are net positives with mild drawbacks, so no choice is regretted.
- **Repeats.** Evolutions can be taken again ("Build your Core II"). Each has its own repeat limit and per-level values, kept in data. No repeats are offered until the garden has 5 evolutions. Smart Particles is one-off.
- **Unlock evolutions** (Pulsar Power, Twin Stars, Big Stem) also give a small boost to their base type and add their kind to the wild flower pool (no per-kind cap).
- The original skill tree (see [ORIGINAL_SYSTEMS.md §9](ORIGINAL_SYSTEMS.md)) stays the content pool for later evolutions.

**Built in 5c** (Oct 31; the rules in `godot/src/Core/Evolutions.cs` and `Garden.cs` (Seedlings and evolutions), every number a placeholder in `Tuning` (Seedlings) or the evolution table's data). Details settled in the build, for Jeremy to review:
- **Planting.** The Seedling is a fifth bud button in a part's toolbar (a pale green sprout: a round seed with two pointed leaves), greyed while no slot is free. It forms as a bud (never growing wild) and emerges as a small one-link flower at the end of its branch. Slots used are the evolutions chosen, plus seedlings yet to choose and seedling buds forming.
- **Filling** (revised by Jeremy, Oct 31; the first build took power in as an upkeep capped at 3 a second): the seedling's own spec has no upkeep, a power weight of 1.5 (the core's) and a transfer rate of 0, so it never sends power on. Its store is 200 (the spec's), then its evolution's target (`PlantNode.Store`), and its card's Power line ("Power: 640 / 1000") is its progress. A part sends it power as it would any neighbour less full than itself, so it fills as fast as its neighbours send and, like any part (and as in the TS game), only as full as they are: a garden running at 80% leaves it short of its choice or bloom. Kept so for now (Jeremy, Oct 31); if playtesting shows seedlings stalling, the TS game's Weight spell priority (a node below full takes power from any sender above 25%) or weight counting in the "less full" test are the options. Full at 200, it rolls its 3 offers then, from the garden's own dice, so tapping again shows the same three and a catch-up rolls alike; full of its target, it blooms. A seedling's power isn't counted in the garden card's Power, the history's stored power or the away note's dip.
- **Nebula Bloom** now works: power reaching a seedling within 5 links of one counts twice (not stacking).
- **The choice.** Tapping a waiting seedling opens a half ring of three discs under it (over it if the ring and its card don't fit below; the seedling's toolbar then goes below), in the core menu's look. Each shows the shape of the flower it blooms into, in its role hue (a repeat marked II or III); the one under the pointer shows a small card: the name ("More Power II"), the level's effect ("Generator cap +2") and "Blooms into a Lichen" ("; Pulsars can then grow wild" for an unlock). No signal that a choice waits (alerts are still open).
- **Targets** count the evolutions chosen before (1,000 × 5^n), so two seedlings growing at once get 1,000 and 5,000 in the order chosen.
- **Evolution record** (Oct 31; three looks on the core were tried and dropped: marker fruits in the flowers' own shapes, the same in pale bubbles, and round gems set into the core's rim). The **Almanac's Evolutions tab** lists each evolution taken in the order first chosen: its name at its current level in its theme's role hue (power yellow: Build your Core, More Power, Pulsar Power, Twin Stars; structure green: Second Path, Long Stemmed, Big Stem; genes purple: Rapid Evolution; white: Smart Particles, Wild Bloom), "Chosen day 2, 14:03" (its latest level), "Level 2 of 3, bloomed into a Lichen" (or still growing in a seedling, or waiting for one), and what each level does ("Core power ×1.5; then Core power ×1.25", or "Generator cap +2 a level"). Seedling slots are counted from `Garden.Evolved`. Each evolution keeps when it was chosen (`ChosenAt`, saved).
- **Bloom.** The effect applies at once, the journal says "Second Path bloomed into a Tendril", and the seedling is swapped for the flower where it stands, full from the power it held. The flashy effect (first pass, 2.4 s): a white flash swelling off the flower, twelve rays turning out from it, and three rings in its hue widening to six times its size, one after another. A catch-up shows none. The wild flower limit rises by 1, and the base cap by 1 where the flower takes that slot (stems for a Tendril or Big Stem, generators for an Ember, Pulsar or Twin Star); the Amplifier, Resistor, Wild Lab, Lichen and Moonflower take only a wild flower slot, so they add only that.
- **Effects** (each level multiplies the last; multipliers touch the basic kind only, so a Tendril, Big Stem or Pulsar keeps its own numbers):

| Evolution | Levels |
| --- | --- |
| Build your Core | Core power ×1.5, then ×1.25, ×1.2 |
| Second Path | Core link +1 and core transfer ×1.5, then +1 and ×1.25 |
| Long Stemmed | Stem transfer ×3, then ×1.5 |
| Smart Particles | One-off |
| Rapid Evolution | Labs' genes ×2 at ×1.25 upkeep, then ×1.5 at ×1.25 |
| More Power | Generator cap +2, three times |
| Pulsar Power | Generators' power ×1.2 (its boost), then ×1.1 |
| Twin Stars | Generator transfer ×1.5 (its boost), then ×1.25 |
| Big Stem | Stems hold +1 leaf (its boost), twice |
| Wild Bloom | Wild flower limit +2, three times |

- **Repeats** are offered only once the garden has chosen 5 evolutions; a level's count includes those still growing, so two seedlings can't both take a one-off.
- **Smart Particles:** a blob never steps into a dead end (a part with nowhere to go on but back) unless it could be taken in there: for a gene blob only the core, for a growth blob a part with something forming or room to start something, for a buff blob a part not yet buffed. With nowhere else to go it turns back as before. The garden card's gene reach follows the same rule.
- **Pruning a seedling.** Before its choice, its slot frees. After, its evolution waits (its slot stays, and the core's card says "Next seedling: Second Path"): the next seedling to emerge takes it up, starting empty like any new part (an undo brings the pruned one back with its power). Pruning the bloomed flower takes only the flower.
- **Away note:** "A seedling bloomed: Second Path, now a Tendril." first (several on one line), before the parts grown. **Journal:** "Evolution chosen: Second Path" and the bloom, in the seedling's pale green. **Export:** `evolutions_chosen`, `seedlings_bloomed`, `seedling_slots`.
- **Save:** still version 9, with optional fields: the evolutions chosen (kind, level, target, bloomed and when) and each seedling's offers and evolution (its power is saved as any part's). A loaded garden works its multipliers out again; its caps are in the saved capacity.
- **Dev aids:** `--evolve=NAME[,NAME]` (chosen and bloomed at once, the flower on the deepest free link), `--seedling`, `--seedfill=N` (its power), `--choose=N` (each waiting seedling takes its Nth offer), `--bloom` (each chosen seedling filled, so it blooms and its burst shows) and `--tap=seedling` (the choice menu open, its first card showing). Shots: `--fresh --grove --seedfill=200 --tap=seedling` for the menu, `--seedfill=200 --choose=0 --bloom --at=12,40,90` for the burst, `--evolve=` with some and `--almanac=evolutions` for the Almanac's list.

| Evolution | Effect | Becomes |
| --- | --- | --- |
| Build your Core | Core power ×1.5 | Ember |
| Second Path | Core link +1, core transfer ×1.5 | Tendril |
| Long Stemmed | Stem transfer ×3 | Amplifier |
| Smart Particles | Blobs never wander into dead ends | Resistor |
| Rapid Evolution | Labs ×2 genes, ×1.25 upkeep | Wild Lab |
| More Power | Generator cap +2 | Lichen |
| Pulsar Power | Generators make more; unlocks Pulsar | Pulsar |
| Twin Stars | Generator transfer up; unlocks Twin Star | Twin Star |
| Big Stem | Stems hold +1 leaf; unlocks Big Stem | Big Stem |
| Wild Bloom | Wild flower limit +2 | Moonflower |

### The Hub

- **Unlocked** in the core track (60,000 genes) and **planted by hand** from the toolbar like a seedling. It is not a wild flower; one at a time. It reads as a stem in the visual language, in its own shade of green, but takes no stem slot and counts against no stem cap.
- It **catches gene blobs like the core**, into **its own pool**, converted to **hub points**: 1 point every N genes, with N rising each time.
- **Tapping it opens its own upgrade panel** with **a path for every node type**, basic and wild (about 19 paths). Undiscovered kinds show "???"; a kind is discovered the first time it grows in the garden.
- Each level costs 1 point (2-3 for milestone levels) and gives +10% to that kind's main effect by default, with some hand-crafted milestones. Each upgrade raises the Hub's own power upkeep.
- If pruned, its upgrades go dormant until a new Hub is planted. A respec option comes later.

## Crawlers

Crawlers aren't developed until after the public beta (step 7+). When they arrive, they should feel like the garden coming alive. When they unlock in a garden's life is a placeholder until then. Starting assumption: around week two of a garden's life, after the Hub (moved from week one in step 5), to be confirmed in step 7.

- They start simple, keeping the original's traits: a home node, preferences, autonomous behaviour.
- They eat fruit. A fruit is either a modifier on its node or food for a crawler, so eating a leaf removes the stem's bonus until it regrows.
- No "kill" framing: the original's execution, frustration and starvation states become a gentle life cycle (creatures age, rest, and return to the soil).
- **Later layer, not in the current design:** a crawler society with buildings, ownership, automatic pruning and consuming, and rebellions to quell. Today's crawlers should leave room for it without building it.
## Desktop widget

| Feature | Priority | State |
| --- | --- | --- |
| Frameless window, transparent background | Must | Done (prototype) |
| Always on top | Must | Done (prototype; a toggle in the Developer settings) |
| Click-through (Ambient mode) | Must | Done (prototype; step 3d leaves the core clickable, to wake the garden) |
| Low idle CPU / memory, pause while hidden | Must | Done (prototype) |
| Display modes: Active / Ambient / Minimised, hotkey | Must | Done (step 2, revised in 3d). Active (default) is full opacity with the toolbar, the card and the core's menu; Ambient fades the window to 55% and lets every click through but on the core, which brightens and is ringed under the pointer and wakes the garden on a tap; Minimised minimises the window and pauses the garden like a locked screen (caught up on return). Ambient and Minimise are in the core's menu; Ctrl+Alt+G toggles Active and Ambient from anywhere (Windows) and restores a minimised garden, as do its taskbar button and the tray icon |
| Remembers size and position | Must | Plant scale saved; the overlay covers the work area and Minimised is a plain minimise, so there is no window position to keep. The display mode isn't saved: every launch starts in Active |
| Tray icon: show/hide, settings | Should | Done (step 2). Click to show or hide (Minimised); right-click for the settings |
| Core menu | Must | Done (step 3d). A half ring of icon buttons under the core: Info, Build, Ambient, Minimise, Settings. See Information display |
| Cards: a part's, and the garden's | Must | Done (step 3b, revised in 3d). A tapped part's card, and above it the garden's while the core menu's Info is on, in a spot of their own beside the plant. See Information display |
| Undo pill | Must | Done (step 2). Under the plant while the last prune can be undone, counting down |
| Settings | Must | Done (step 3d). Right-click, or the core menu's Settings: Size, Idle frame rate, New garden (asks first), Quit, and in debug builds a Developer submenu (Always on top, Growth speed, Physics) |
| Build mode | Must | Done (step 3a). Within Active: an opaque disc round the plant, the shaping bar, tools to move, turn and remove. See Shaping the garden |
| Launch on startup (opt-in) | Should | — |
| Pinned to the desktop layer instead of on top (user choice) | Could | — |
| Notifications for rare events (opt-in, very sparing) | Could | — |

### Information display

Step 3b, revised in step 3d (Jeremy's decisions, Oct 29). The plant is stupid and the player is the brain, so the player needs the numbers a decision turns on, at a glance. But they show only when asked for: a garden that's only being looked at shows nothing but itself. The numbers come from the rules (`GardenStats` and `NodeStats` in `godot/src/Core`), never worked out in the view.

**Hovering shows nothing** but the ring on the part under the pointer.

**Cards, in a fixed spot.** Tapping a part selects it (ringed), opens its toolbar and shows its card. **Info** in the core's menu is a switch (lit while on): while it's on, the garden's card shows above the part's whenever the player is tending (a part selected, or the core's menu open). A click off the plant puts both away; the next tap on the plant brings the garden's back, until Info is switched off. The cards are set down as they open beside the plant, level with the core (to its right, or its left if there's more room there), and stay there while open, their top corner fixed, so the layout never jumps (switching Info on pushes the part's card down below the garden's). Tapping the selected part again closes its card and toolbar together (the core: its menu too). The cards also close when a click lands off the plant, on Escape, or when the part is pruned.

**Card style**, after the original game: the name as a heading in the node's hue, then short "Label: value" lines in whole numbers, and no description line. Rates are per real second or minute at the current growth speed, and are the rates **now**: like the parts themselves they follow the fill (an empty part costs nothing; a full or overfull core or generator makes nothing, so its Gen shows its full rate instead, and the garden card's Gen counts it at that rate too). (The overload is judged at full: what the garden would make and cost with every part full.) Part cards use the garden card's words, Gen and Drain, for what a part makes and costs.

| Kind | Lines |
| --- | --- |
| Core | Power, Gen X/s (sends Y/s), Connections |
| Stem | Power, Drain X/s (sends Y/s), Connections |
| Generator | Power, Gen X/s (sends Y/s) |
| Grove | Power, Drain X/s, Growth X/min |
| Lab | Power, Drain X/s, Genes X/min |

- **Power** is held against its store. **Sends** is how fast it can send power on (its transfer rate), beside Gen or Drain on every power maker and every part that can have 2 or more links (a stem, the core, the inline wild flowers; Oct 31).
- **Growth** (growth blobs a minute) and **Genes** (genes a minute) are what the flower makes, not what arrives: many of a far lab's genes never find the core.
- **Connections** (links against the most it can have, the core's from Capacity) shows only on a node with 2 or more.
- **Forming** ("Forming: Lab bud 60%", "Forming: Leaf 40%") shows only while a fruit or bud is forming in the node.
- **Buffed** (Oct 31): while a buff blob's boost runs on a part (from a Buffer or a glow fruit), a small glow fruit, its shape and colour, sits beside the card's heading.

**The garden card**, in the same style:

```
Garden
Power: 6178 / 6800
Gen: 79/s, Drain: 81/s
Genes: 1840
Next: +1 stem, 3,150 (ETA 4h 47m)

Stems: 16 / 16
Generators: 5 / 5
Core links: 2 / 2
Wild flowers: 1 / 2
Groves: 4, Labs: 9
```

Gen and Drain are what the garden makes and costs now; Gen's value is coloured by how it compares with Drain (see below). Since 5b, Genes is the balance left to spend and Next names the cheapest next upgrade across the three tracks, its cost and its ETA, or "(in Research)" once the balance covers it; with every upgrade bought it reads "Next: every upgrade bought". (Through step 4 it was the total against the next unlock's threshold.) The ETA is at the current rate of genes reaching the core, expected rather than measured: each lab's blob rate times the chance its blob finds the core before it fades (`GardenStats.GeneReach`, the blob's random walk worked out as odds). "(plant a lab)" stands in for it with no labs.

**Colour coding** (Jeremy, Oct 29): only the value, or a fraction's numerator, is tinted; labels and denominators stay white.
- **Power** (a part's, and the garden's total): white when full, shading to yellow above full (all yellow at 125%) and to red below it (all red at 50% and under).
- **Gen**: white when it matches Drain, shading to yellow as it makes more (all yellow at 125% of Drain) and to red as it makes less (all red at 80%, since an overload matters sooner than a low store).
- **Genes**: purple shading to white as the balance nears the cheapest next upgrade's cost (white once it covers it).
- **Stems, Generators, Core links, Wild flowers**: green with all the room left, shading to white as it fills (white when full). The Wild flowers line (5d) shows once the garden has a wild flower limit.

**The core's menu.** Tapping the core opens a half ring of round icon buttons under it (over it if there's no room below), which keeps the space above it clear for its toolbar and the shaping bar. The icons are drawn in the toolbar's style, and each slot's name shows beside it under the pointer. Slots: **Info** (the garden card), **Almanac** (step 4: the journal's window) and **Build** (build mode), switches that are lit while on and leave the menu and the core's toolbar open; **Ambient** (its label names the hotkey, Ctrl+Alt+G), **Minimise** and **Settings**, which close it. **Research** (step 5b: the three gene tracks; a flask, between Almanac and Build, lit while its panel is open, which only its slot or ✕ closes, like the Almanac) joins it: the ring spreads its slots evenly and widens as more join. It closes, with the core's card, on another tap of the core, a tap anywhere off it, Escape, or a pick of Ambient, Minimise or Settings. The core's own toolbar (its buds) opens with it, above the core (below it if the menu had to go above), and closes with it; tapping the core also shows the core's card.

**Waking from Ambient.** In Ambient everything is click-through but the core. The core brightens a little and shows the selection ring while the pointer is on it, and one tap switches to Active and opens its menu.

**No needs-attention dot on the core.** A red dot on the core (for an overloaded garden, later waiting evolutions and research) was tried and dropped (Jeremy, Oct 29): more confusing than helpful. The garden card's Gen colour shows an overload. How step 5 signals an affordable upgrade, a choice waiting or a bloom is the alerts design, still open (see Open for step 5).

**Settings.** Right-click (and the tray icon's right-click) opens them as a shortcut; so does Settings in the core's menu. For the player: Size, Idle frame rate, New garden (which asks first) and Quit. A Developer submenu, only in debug builds (`OS.IsDebugBuild()`), holds Always on top, Growth speed and Physics. Display mode moved to the core's menu.

**No pinning.** The toolbar's "Pin in place" is gone, with pinning in the rules and the save (Gravity Pots hold a part in place now). An older save's pins are ignored, so pinned parts come back free.

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
| 2 | Living garden (done) | Power economy, upkeep; visible power flow (link flashes, node brightness); new sprout, prune with undo, drag, display modes; passive growth, capacity and the node budget; groves, growth blobs, fruit, labs, genes. Light progression: genes reaching the core automatically raise caps along a fixed, linear unlock sequence, with a small notification and no menu or choices |
| 3a | Shaping (done) | Gravity Pot, Trellis, Gravity Well, placed and connected in build mode. See Shaping the garden |
| 3b | Information display (done) | The hover readout expanded to what an active player decides by (power against its store, gene income, the next unlock's ETA, caps used, flowers, passive buds forming), and a panel for the selected part (name, power, what it does, its own numbers). See Desktop widget; revised in 3d |
| 3c | Visual polish → MVP (done) | A short round of visual polish, including the prune effect; then the power economy reworked to the original's clumps. End of step = MVP, reached Oct 29 |
| 3d | UI revision (done) | Hovering shows only the ring; a part's card in a fixed spot, with the garden's above it while Info is on, in the original game's "Label: value" style; a half-ring menu under the core (Info, Build, Ambient, Minimise, Settings); the core clickable in Ambient to wake the garden; settings trimmed, with a Developer submenu in debug builds; pinning removed. See Information display |
| 3e | Manual planting (done) | A manual bud starts at once and forms like a passive one; every node bud grows on its own at its parent's fill (about 2 minutes at full), growth blobs hurry it, and fruit still grows only from blobs. Planting stays free, and a garden with no grove needs no special case. See Growth and pruning |
| 4 | Information and tracking (done) | Lifetime totals and four time series with tiered history (save v8), a debug-only export, the Almanac as a journal of moments, a "while you were away" note, power visuals at the top of the range, a light balance pass. See Step 4 below |
| 5 | Progression (decided Oct 30; 5a, 5b, 5c and 5d done) | 5a (done): prep work (the passive growth rename in the game's text, gene spending and save v9 groundwork). 5b (done): three gene-spending upgrade tracks (stems, generators, core) behind the core menu's Research slot, shaping tools from garden size. 5c (done): seedlings and evolutions, and the Almanac's evolutions list. 5d (done, built before 5c): wild flowers (wild growth, 15 kinds, one limit), the Codex, their journal moments and away-note lines. The Hub, the Codex, journal and away-note additions, save v9. The real-week tuning check (moved from step 4) is postponed until step 5 is built. See Step 5 below |
| 6 | Polish → public beta | Garden-only proof of concept, polished, with onboarding (its first hint: plant a grove, since nothing grows passively without one). Revisit idle CPU against the benchmarks before release. Achievements: Steam achievements only, with no in-game toast or list; test whether Steam's overlay toast shows over the transparent window, and keep a clean path without Steam. End of step = public beta candidate |
| 7+ | Post-beta | Crawlers (when they unlock in a garden: placeholder, decided here) and everything after |

**Idle CPU before the public beta (step 6):** revisit it against the benchmarks. Current levers: a lower idle frame rate in Ambient mode, and a window sized to the plant's bounds instead of the full work area.

**Design needed before each step:** the linear unlock sequence before step 2 (replaced by the spending tracks in step 5); step 5's design is settled below, with a few points open.

Open: where events and seasons, and identity features fall in the plan.

### Step 4: information and tracking (decided Oct 29)

**Tracking, kept small.**
- **Lifetime totals:** garden age, genes earned, parts grown, parts pruned, passive buds, unlocks reached (5b replaced the last with upgrades bought, by track; 5a added the peak main-node count).
- **Time series, four metrics only:** stored power, Gen, Drain, and genes reaching the core.
- **History is tiered:** per minute for the last day, per hour for the last month, per day beyond that. The totals and history go in the save, which bumps it to **version 8**.
- **Settled in the build (Oct 29):**
  - All times are the garden's **age**: growth time it has lived, so a catch-up's 72 h cap and the dev growth speeds apply to it as to everything else.
  - Each sample holds stored power (the parts' own, fruit aside, as the garden card shows it), Gen and Drain averaged over its stretch, and the genes that reached the core in it.
  - Old samples roll up a whole hour (or day) at a time, so the minute tier holds the last day plus up to an hour.
  - The history is fed from sums the economy step already works out, so a catch-up fills it at almost no cost.
  - Passive buds count as growth blobs start them. An undone prune doesn't count as pruned.

**Tuning first.**
- A debug-only export writes the time series and totals to a file, CSV or JSON.
  - Built as three CSV files: the history (one row per sample, its tier, its start in hours), the totals, and the journal. It runs from Settings > Developer or `--export=PATH`.
- Player-facing graphs wait until play shows which numbers matter.

**The Almanac is a journal of moments.**
- Milestones are logged automatically, such as "First grove emerged" or "Second core link unlocked".
  - Built: the planting, each kind's first part ("First grove emerged", "First leaf grew"), the first time the garden buds on its own, and every unlock (5b replaced the unlock moments with core links and the Hub bought, and shaping tools gained; see Upgrade tracks). They favour the garden's own moments (Jeremy, Oct 29). Of the player's doings only genuine milestones count: the first of each shaping tool placed (not the first prune).
  - Also the first time the garden reaches 25, 50, 100, 200, 300, 400 and 500 parts.
  - Built for the old track (replaced in 5b): an unlock was named by what it opened, biggest first, then the room it added: "Second core link and second Gravity Pot unlocked, with room for 1 more stem and 1 more generator", or "Unlocked room for 3 more stems".
- The catalogue of node kinds is the Codex tab (step 5): discovered kinds only.
- There is no big stats page.
- It opens from the Almanac slot in the core's menu. ~~As a larger panel in the cards' fixed spot, closing the way they do.~~ Changed by Jeremy, Oct 29: it's a large window of its own with a draggable header and a ✕.
  - It stays on screen whatever else happens (clicks elsewhere, the focus going, Escape, tapping parts). Only its slot or its ✕ closes it.
  - Built: the slot sits between Info and Build and is lit while the Almanac is open.
  - The first time, it opens beside the plant on the side with room. After that it opens wherever it was left, until the garden quits.
  - It hides while the garden is in Ambient or Minimised (only the core takes clicks there) and comes back in Active.
  - Tabs across the top, shown even with one: the **Journal** now, the **Codex** in step 5 (discovered kinds, each with a description and when it was first found), **Stats** if player-facing graphs come.
  - The Journal groups moments under day headings ("Day 1", "Day 2": each 24 hours of the garden's age), newest first, each with its time on that day. The header shows the garden's day and time. Purchases are in the genes' purple, shaping tools gained in the tools' blue. It scrolls under the wheel.
  - It can be resized by the grip in its bottom-right corner (300 × 240 at least). Size and place are kept until the garden quits.

**No advanced stats on the cards.** They stay as trimmed in 3d.

**While you were away.**
- On return from a catch-up, a short note says what changed: new growth, unlocks reached, power dips. (5b swapped unlocks for shaping tools gained; 5c and on add seedlings bloomed, new wild flowers, Hub points earned and Embers burned out.)
- It draws on the tracking and journal data.
- **Built:** it replaces the old "says what grew" message, as a small panel above the plant for 15 s.
  - It lists the parts grown by kind, the genes that reached the core, and up to 3 of the journal's moments (shaping tools gained first, since 5b), then how many more are in the Almanac.
  - A dip is stored power falling below half of what the garden held when the player left. (Its store grows as it does, so a share of the store would flag every growing garden.)
  - It shows on launch, and on un-hiding after at least 5 minutes away. It covers that catch-up's shaping tools gained, so their own note doesn't show as well.

**Power visuals at the top of the range** (done): parts still visibly grow and shrink now that stores are larger. Size and brightness follow the square of the fill, and past full a part swells at half its overfill (see Size follows fill).

**A light balance pass** (Oct 29) closed the step from simulated play: a faster first grove and unlock steps 5–9 retuned (on the old unlock track, since replaced). **The real-week tuning check moved to step 5, and is now postponed until step 5 is built** (Oct 30):
- Run a real garden for a week on the new tracks, then retune their costs from the data.
- With the same data, check the gene income ceiling: whether flat gene income (labs need to sit near the core) is a real problem.

**Achievements are deferred to step 6** (see the plan).

### Step 5: progression (decided Oct 30)

Jeremy settled the design with a chat agent on Oct 30. The rules live in Progression, Wild flowers and Growth and pruning; this section is the summary and the open points. **Every number in step 5 is a placeholder for playtesting**: costs, chances, rates, multipliers and thresholds.

**Naming** (applied through this document):
- **Passive growth:** the plant starting buds on its own via growth blobs. It replaces the old "wild growth"; passive buds likewise.
- **Wild growth:** a new bud turning into a special kind. **Wild flowers:** the results. They replace the old "rare wild nodes" and "rare wild pool".
- **Pulsar** replaces Volatile.

**Sub-steps**

| Sub-step | Contents |
| --- | --- |
| 5a (done) | Prep: the passive growth rename in the game's text, the gene balance (spent apart from genes earned), the garden size counter, the wild chance tuning variables, save v9. See Review decisions, 2 |
| 5b (done) | Upgrade tracks: spending genes on stems, generators and the core, the Research panel, shaping tools from garden size |
| 5c (done) | Seedlings and evolutions, the evolution record (the Almanac's list), repeats (see Seedlings and evolutions, Built in 5c) |
| 5d (done) | Wild flowers: the 15 kinds, the chance and limit, shapes. Built before 5c, since a bloomed seedling becomes its evolution's wild flower (see Wild flowers, Built in 5d) |
| Hub | Planted by hand, its own pool, points and per-kind panel |

**Across the steps**
- **Codex tab** in the Almanac: lists only discovered kinds, each with its description and when it was first found.
- **Journal:** each kind's first appearance, each evolution chosen and bloomed, the first Hub planted, core links bought and the Hub bought. These replace the old unlock moments (a core link or the Hub still reads as a moment; stems and generators bought do not).
- **While you were away:** seedlings bloomed, new wild flowers (discoveries highlighted), Hub points earned, Embers burned out. This replaces the old "unlocks reached".
- **Display text that named unlocks** got new wording in 5b: the garden card's "Next" line and Genes colouring (the cheapest next upgrade and the balance), the away note's "unlocks reached" and the journal's unlock moments (shaping tools gained, core links and the Hub bought), the build bar's tool captions (garden size), and the Step 4 totals' "unlocks reached" (upgrades bought). See Upgrade tracks, Wording.
- **Save version 9.** Older saves are set aside, not migrated. It adds gene spending and track levels, seedlings and evolutions, wild flowers and their state (an Ember's dimness, a Comet Bloom's age), the Hub (pool, points, levels, dormancy), discovered kinds, and the peak main-node count. 5a opened version 9 with the gene balance and the peak main-node count; 5b to 5d add their own fields as they're built.
- **Crawlers'** starting assumption moves to around week two, after the Hub.
- **The real-week tuning check is postponed until step 5 is built.** Then: run a real week on the new tracks, retune costs, and check the gene income ceiling (whether flat gene income, with labs needing to sit near the core, is a real problem).

**Review decisions (Oct 30)**
1. **Destruction.** Wild flowers that end themselves (Ember, Comet Bloom) are the one exception to "never destroys anything". Only their own life cycle ends them, never neglect. The slot is freed, and the away note says so.
2. **5a is prep work.** It covers the passive growth rename in the game's text, plus groundwork for gene spending and save v9. The one-week real playtest (once planned as 5a) runs at the end of step 5.
3. **Rename scope.** Player-facing text changes in 5a: the garden card's "wild buds forming", the journal's "budded on its own" moment, and the tracking total's export name `wild_buds`. Code identifiers are renamed as the code is touched. *Done in 5a:* the journal moment now reads "First passive bud started", the export's total is `passive_buds`, and the grove's description no longer says "wild parts". The garden card had no buds-forming line left to rename (3d dropped it), and the away note never said "wild". In code, `Garden.PassiveGrowth` (the flag) and `Tracking.PassiveBuds` replace the old names, along with the core's and tests' comments.
4. **Planted buds can grow wild.** Watched in playtesting. There are two tuning variables: `PassiveWildChance` (5%) and `PlantedWildChance` (4%), each per free wild flower slot. (In `Tuning` since 5a, unused until 5d.)
5. **Content size.** 5d is built in batches, the Start-unlocked kinds first.
6. **Gene totals.** Stems 213,650 + generators 112,500 + core to Hub 130,500 come to about 457k genes, against about 330k for the old week-one track. At 55-70k a day, that's roughly 7-8 days before the Hub's points and later core items. Kept, with the trimming note. (5b: the first three generators were lowered, so 455,150 in all.)

**Open for step 5**
- **Alerts.** How the garden tells the player that something waits: an upgrade affordable, a seedling's choice, a bloom, a discovery. Red dots are distracting (the core's dot was dropped in 3d), so look for a different way, for example a lit or pulsing Research slot, a glint on the core, a journal moment, or the away note. 5b shipped the Research panel with no signal (the garden card's Next line says "(in Research)" once the cheapest upgrade is affordable, when Info is on); the alert is still to design.

## Parked ideas

From the first draft, not contradicted by the anchor but not scheduled. Candidates for the "Events and surprises" and "Identity" topics:

- Each bloomed seedling releases a seed that drifts up and becomes a star in the background, a visible record of every evolution.
- Day and night tied to local time; seasons over weeks.
- Rare events: visiting comets, creature migrations, special blooms.
- Lifetime garden stats (age, blooms, tallest stem) as Almanac material.
- Onboarding as gentle, skippable hints spread over the first days.

## Open questions

None at the moment.

Settled since the first draft: platform (Godot); "is there any care need?" (no; neglect only slows growth, by running the garden low on power). Settled in the pacing pass: no power blobs (power stays link flashes and node brightness, at a cadence of every few seconds); blob rates are at real speed. Settled Oct 26:
- **One plant per garden.**
- **No save migration until release.** Old v29 saves are not carried over, and prototype save formats may break freely before release.
- **Distribution:** probably Steam.
- **Catch-up cap:** keep 72 h for now; tweak later if play shows a need.
- **Core links:** the core starts with 1 link (the old unlock track gave +1 at 15,000 genes; step 5 sells one at 4,000 genes in the core track).

## Next topics

Design layers still to work through:

- [x] Information display: the readout and the selected part panel (step 3b)
- [x] UI revision (step 3d): hovering shows nothing but the ring; one card in a fixed spot; the core's half-ring menu; the core tappable in Ambient mode to wake the garden. Build mode itself is unchanged, now opened from the core's menu
- [x] Manual planting (step 3e): free, a bud that starts at once and grows on its own like every node bud, hurried by growth blobs (see Growth and pruning)
- [x] Power visuals at the top of the range (step 4): size and brightness follow the square of the fill, steep near full, and swell at half the overfill past it (see Size follows fill)
- [x] Stat tracking and Almanac (step 4): small lifetime totals and four time series, a debug export, the Almanac as a journal of moments (see Step 4)
- [x] Specialist effects: Resistor, Amplifier, Buffer (step 5): in the wild flower table; numbers are placeholders
- [ ] Long-term pacing beyond week one
- [x] Choice-based core levels and seedling slot timing (step 5): replaced by the three gene-spending tracks, with seedling slots in the core track
- [x] Which basic node each evolution becomes, and its fruit (step 5): the evolution table and its record in the Almanac
- [ ] Step 5 open points (destruction exception, 5a, rename scope, planted buds growing wild, Research dot, content batching, gene totals)
- [ ] Build step 5 (5a prep, 5b tracks, 5c seedlings and 5d wild flowers done; the Hub), then the real-week tuning check
- [ ] Alerts: how the garden signals that something waits, without red dots (5b shipped the Research panel without one; still open)
- [ ] Hub details: N for the points curve, milestone levels, upkeep per upgrade, respec
- [ ] Onboarding over the first days (step 6), starting with planting the first grove
- [ ] Identity: what makes each garden uniquely yours after months
- [ ] Events and surprises: rare blooms, seasons, day and night
- [ ] Audio: likely silent by default
- [ ] Final name for genes
