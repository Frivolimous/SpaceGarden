# Space Garden (original) — Systems & Mechanics Inventory

*Reference for redesign discussions. Describes the TS/Pixi game in `src/` as of v0.0.2 (save data version 29). For the new direction see [DESIGN.md](DESIGN.md).*

**Units:** the sim runs once per frame at 60 fps. Rates in the data are **per tick**; the UI shows them ×60 as "/s". Example: Core `powerGen 0.4` → 24 power/s.

---

## 1. The plant graph

The plant is a graph of **nodes** joined by **links**, laid out by a force-directed physics sim.

1. **Core** is the root. Exactly one exists, it starts the game, and it can't be deleted.
2. **Links** connect parent → child ("outlets"). Every node has a `maxLinks` cap.
3. **Connectivity rule:** when a link breaks, any node no longer connected to the Core is destroyed with an explosion. Its fruits are destroyed too.
4. **Core distance:** each node stores its hop count from the Core. Only used by the "Longest Stem" achievement.
5. **Per-type build limits:** `maxCount` caps how many of a type exist. The bottom-bar button shows `count / max` and greys out at the cap.

## 2. Physics / layout (FDG)

- **Spring links:** pull together once longer than the rest length. Rest length = sum of radii, ×1.5 for some pairs. Elasticity 0.004.
- **Repulsion:** all node pairs push apart, scaled by `force_a × force_b / dist³`. Mass divides acceleration. Heavy nodes (Core, Seedling: mass 30) barely move.
- **Damping** 0.98 per tick, with a minimum-velocity cutoff.
- **World bounds:** a soft edge bounce keeps nodes in a 1500×1000 canvas.
- **Rotation:** every node sprite slowly spins (cosmetic).
- **Pulls:** a click-drag on a node attaches a spring to the cursor, so the player can tug the plant around.
- **Camera:** locked on the Core. Pan with WASD/arrows, zoom with +/-. Starfield background.

## 3. Building nodes

1. Press a node button in the bottom bar, then click or drag in the world.
2. The new node is a **ghost** (semi-transparent, no mass) that snaps to the nearest node with a free link slot within grab range (30px).
3. While dragging, it re-links to whatever is nearest. On release it becomes active if linked, and is discarded if not.
4. Building is **free**. Count caps are the only limit.
5. Nodes can't be moved or re-parented after placement, only deleted (see the Delete spell).

## 4. Power economy (core loop)

Each node has `powerCurrent / powerMax`. Its fill level is shown as glow intensity.

- **Generation / drain** (`powerGen`):
  - Positive gen scales with the node's own fill (`× min(fill, 1)`). An empty generator generates nothing, which creates a "keep it topped up" feedback loop.
  - Negative gen (drain) scales with fill too, so an empty node stops draining.
- **Transfer:** every `powerDelay` ticks (20, ±10% jitter), a node with fill > 25% sends a **clump** (`powerClump`) to one outlet. The target is the outlet with the lowest weighted fill (`fill / powerWeight`) that is below the sender's fill. Power always flows "downhill" and spreads evenly by percentage.
  - `powerWeight` biases who receives. The Seedling (2.0) pulls hard; Volatile (0.001) almost never receives.
  - Grow transfers are instant and show as a flash on the link.
- **Priority flag:** set by the Focus spell. That node gets power first until full.
- **Overcharge:** nodes can exceed 100% via fruit delivery. Shown with a separate colour gradient.
- **Plant overview** (sidebar): total power/max, smoothed total gen vs drain, crawler count, node count.

## 5. Fruits

Fruits are small child nodes (type `fruit`) attached to a parent's `fruits[]` list. They are not outlets.

1. **Spawning:** a node with a `fruitType` accumulates `fruitSpawn` from fruit particles. At ≥1 it spawns a fruit if under `maxFruits`.
2. **Fruit chains:** `fruitChain` lets fruits grow their own fruits (each link decrements the chain). Example: Core batteries chain 3 deep, so you get strings of batteries.
3. **Growing fruit:** a parent above 60% fill sends power clumps (`fruitClump` 5) to its emptiest fruit until that fruit is full.
4. **Harvestable** = fruit fill > 60% and not already claimed by a crawler.
5. **Outlet effects:** some fruits modify their parent's stats while attached, and the effect reverses on removal:
   - **Leaf** (on stems): +0.025 power gen, which cancels stem drain.
   - **Research** (on labs): ×1.1 research gen.
   - **Burr** (on generators): ×1.1 power clump (transfer rate).
6. **Passive fruit growth:** off by default. The skill "Fruitful Multiplication" makes all fruit fill slowly on its own.

| Fruit | Grown by | Purpose |
|---|---|---|
| battery | Core, Volatile | Stores power; generates a little (0.02) |
| leaf | Stem (after skill), Big Stem | Cancels stem drain |
| food | Home | Crawler food; spawning one can spawn a crawler |
| research | Lab, Wild Lab | Lab boost; crawlers carry it to the Seedling |
| gen | Grove | Generates a little power; crawlers carry it as power |
| burr | Generator | Generator transfer boost |
| wood | Lumber Grove, Buffer | No use yet. Described as a crawler building material but never implemented |

## 6. Particles ("transfer blobs")

Visible packets that travel along links hop by hop. They're the main "living light" visual.

| Type | Source | Effect on arrival |
|---|---|---|
| **Grow** (white) | any power transfer | Adds power instantly (flash, no travel) |
| **Research** (purple) | Lab / Wild Lab | Absorbed by the Seedling (→ Genes), or by the Hub if it is collecting |
| **Fruit** (orange) | Grove / Lumber Grove | 30% chance to apply at any node that can grow fruit; otherwise moves on. Also absorbed by the Hub if it is collecting |
| **Buff** (grey/white) | Buffer | Buffs the first un-buffed production node it reaches |

- **Fade / lifetime:** each blob has a hop budget (30 for research and fruit, 5 for buff) and dies at 0.
- **Routing:** random walk to a neighbour, never straight back, never into a **Resistor/Wall**. With the "Smart Particles" achievement (`BLOB_AI = 1`), blobs skip dead ends unless the dead end is a valid destination.
- **Amplifier:** a blob passing through gets ×2 amount and +3 hops, once per blob. Shown as bigger and brighter.
- Labs accumulate research and release one clump once it passes a threshold. Groves and buffers emit one blob per whole unit generated.

## 7. Node catalogue

Gen/drain values are per second. Count caps are base values; skills raise them.

| Node (display name) | Shape / colour | Role | Key stats | Cap | Unlock |
|---|---|---|---|---|---|
| **Core** (Core Root) | big yellow circle | Root, main generator | max 300, +24/s, 1 link | 1 | start |
| **Stem** (Stem Connector) | small green circle | Connector | max 100, −3/s, 3 links | 4 | start |
| **Generator** | yellow pentagon | Power | max 100, +12/s, grows burrs | 2 | start |
| **Grove** (Fruit Grove) | orange square | Fruit particles, grows gen fruit | −6/s | ∞ | start |
| **Lab** (Evolver / Evolution Lab) | purple hexagon | Research particles | −6/s, 0.1 research gen, grows research fruit | ∞ | start |
| **Seedling** | big dark-green hexagon | Progression sink (§8) | max 2000 (+skills), weight 2 | 1 | start |
| **Home** (Crawler Home) | blue circle | Grows food, spawns crawlers | −6/s | ∞ | T2 "Crawlers" |
| **Big Stem** | green circle | Connector, 4 links, grows leaves | −6/s | 2 | T3 skill |
| **Volatile** (Volatile Generator) | yellow triangle | +48/s, sends huge clumps rarely, almost never receives | max 300 | 1 | T3 skill |
| **Lumber Grove** | light-orange pentagon | 4× grove fruit rate, grows wood | −18/s | 1 | T3 skill |
| **Wild Lab** (Wild Evolver) | tiny purple circle | 3× research, chain-2 research fruit ×5 | −12/s | 1 | T3 skill |
| **Resistor / Wall** | grey square | Blocks particles | 2 links | 1 | T3 skill |
| **Amplifier** | green thin rect | Doubles particles once | −24/s, 2 links | 1 | T3 skill |
| **Hub** (Growth Hub) | big green pentagon | Collects resources, in-run upgrades (§11) | max 300, −15/s, 4 links, 6 fruits | 1 | achievement |
| **Buffer** (Node Buffer) | white fat rect | Buff particles | −18/s | 1 | T4 |

Unused slugs in the type list: `enemy-core`, `enemy-box`, `big-evil`, `small-evil`. They're leftovers of an abandoned enemy idea.

## 8. Seedling & prestige ("Launch")

The central progression loop. The revival removes the mandatory reset.

1. Build the **Seedling**. It drains huge amounts of power (big max, high weight).
2. Research particles and crawler-carried research fruit fill its **Genes** (`researchCurrent`).
3. Genes convert to **Evolutions** (skill points) on an escalating cost table: 10, 50, 100, 200, 400 … up to 3.5M. Past the end of the table, costs repeat the last 9 entries ×10 per cycle. A progress bar shows Genes toward the next point.
4. Spend Evolutions in the **Next Evolution** skill panel (opened from the Seedling's sidebar entry). These skills apply only to the **next** plant.
5. When the Seedling reaches 100% power (`LAUNCH_PERCENT`), the **Launch Seedling** button unlocks.
6. **Launch:**
   - Next skills become current skills.
   - Skill tier is updated.
   - Hub levels are wiped.
   - Run timer is reset.
   - The whole garden is wiped back to a lone Core.
   - The launch counts toward the "Eventuality" and "Longest Stem" achievements.
7. **Genetic Memory** (T3 skill): keeps 25% of Genes when the Seedling is launched or deleted.
8. A run timer (`prestigeTime`) is shown. Total play time is tracked too.

## 9. Skill tree (Evolutions)

- Four **tiers**, six skills each, plus a **tier-gate skill**. The gate requires all six skills of its tier.
- Buying a gate **permanently** unlocks that tier's skills (always applied from then on) and opens the next tier. Each gate also adds a big seedling penalty: more max power and/or drain.
- **Tier penalties ("negatives"):**
  - T1: each skill adds +500 seedling max power.
  - T2: each skill adds +1.2/s seedling drain.
  - T3: each skill adds +0.1/s drain to Grove, Home and Lab.
- Skill effects are data-driven. Types:
  - `node` stat change (additive, multiplicative or replace)
  - `crawler` stat or command list change
  - `crawler-command` config change
  - `buildable` unlock
  - `crawler-available` unlock
  - `spell` unlock
  - `config` global tweak
  - `tier` change

**Tier 1 (base)**
| Skill | Cost | Effect |
|---|---|---|
| Build your Core | 1 | Core gen ×1.5 |
| Second Path | 1 | Core +1 link, transfer ×1.5 |
| Long Stemmed | 2 | +4 stems, stem transfer ×3 |
| Leafy Goodness | 2 | Stems grow leaves (cancel stem drain) |
| Rapid Evolution | 3 | Lab research ×5, lab drain ×2 |
| More Power | 3 | +2 generators |
| → **Tier 2** gate | 2 | Unlock T1 permanently; seedling drain ↑ |

**Tier 2 (crawlers)**
| Skill | Cost | Effect |
|---|---|---|
| Crawlers | 1 | Unlock Home |
| Librarians | 2 | Crawlers carry research fruit to the Seedling |
| Woodcutters | 3 | Crawlers carry grove fruit as power to the Seedling or low nodes |
| Handmaidens | 3 | Crawlers over-eat to breed |
| Worshippers | 4 | Crawlers dance on the Core to charge its fruits |
| Longevity | 4 | Crawler health drain ×0.75 (text says −50%), speed ×1.2 |
| → **Tier 3** gate | 5 | Unlock T2 permanently; seedling max +20,000 |

**Tier 3 (exotic nodes)**
| Skill | Cost | Effect |
|---|---|---|
| Big Stem | 5 | Unlock Big Stem ×2 |
| Leadership | 5 | Unlock Chieftain + Shaman crawlers |
| Volatile Power | 6 | Generators +12/s; unlock Volatile |
| Fruitful Multiplication | 6 | Passive fruit growth; unlock Lumber Grove |
| Blobism | 6 | Unlock Resistor + Amplifier |
| Genetic Memory | 8 | Keep 25% Genes on launch; unlock Wild Lab |
| → **Tier 4** gate | 10 | Seedling max +450,000, drain +1/s; unlimited Stems, Big Stems and Generators; unlock Buffer |

**Tier 4:** a placeholder "You win!" (cost 99). It's the effective end of content and becomes a sandbox.

UI: the panel has tier tabs. Affordable skills are highlighted and locked ones greyed. A second, read-only "Current Evolution" panel opens from the Core.

## 10. Crawlers (creatures)

Autonomous creatures that live on the graph and walk node to node along links (A* pathing).

**Lifecycle**
1. **Spawn:** when a Home grows a food fruit, a crawler spawns if crawlers < number of Homes. The crawler type is random among unlocked types under their caps.
2. **Health:** starts at 1.0 and drains every tick (0.0002/tick ≈ 83 s from full to empty).
3. **Eat:** the crawler walks to a harvestable food fruit, picks it up, and eats it. Health gained = fruit power × 0.0065.
4. **Breed:** if health > `breedThreshold` (1.25), it halves its health and spawns a new crawler.
5. **Death:** at health ≤ 0 it plays a death animation. It also dies if the node it stands on is deleted.
6. Deaths are counted toward the "Genocide" achievement.

**AI:** each crawler has a command list. When idle, it picks the command with the lowest `genPriority()` (a utility score with random jitter). Each crawler has a random **preference** ("Loves to …") that lowers its score for that command. Its colour tints to the current command.

| Command | Behaviour | Availability |
|---|---|---|
| Wander | Walk 1–2 random hops | base |
| Idle | Bob around current node | base |
| Eat | Fetch and eat food; top priority when health < 0.5 | base |
| Frustrated (red) | Jitter in place; triggered when a path fails or a target vanishes | base |
| Starving (dark blue) | No food reachable: walk to a Home and wait | base |
| Research | Carry research fruit to the Seedling (fruit power × 0.5 → Genes) | Librarians |
| Power | Carry grove fruit to the Seedling or a low-power node (fruit power × 2 → power) | Woodcutters |
| Breed | Keep eating food until over breed threshold | Handmaidens |
| Dance | Hop on the Core for ~7 s, charging Core fruits | Worshippers |
| Buff (Shaman) | "Bless" a crawler on the same node: ×1.5 speed for 10 s | Leadership |
| Execute / Get Executed | Shaman kills crawlers flagged for execution; victim trembles | **disabled in code** |

**Types:**
- **Crawler:** basic.
- **Chieftain:** max 1; more health, faster. Just a stat variant.
- **Shaman:** max 1; slow, buffs others.

**Claiming:** a crawler claims the fruit it's walking to so two can't take the same one. A claimed node left with no links is destroyed when released.

## 11. Growth Hub (in-run upgrades)

- One Hub node per plant, unlocked by the "Expanded Evolution" achievement.
- It intercepts and **stores** three currencies:
  - research particles → Evolution
  - fruit particles → Fruits
  - its own drain, converted into stored Power
- **Collection toggles** turn each intake on or off. These are themselves unlocked by the "Toggle Toggle" achievement.
- The Hub panel has three columns, one per currency. Each upgrade has 5 levels with ~×18–25 cost scaling. **Every level also adds +1/s Hub drain.**
- Upgrades:
  - Core links +1
  - Core power +24/s
  - Stem count +1
  - Generator count +1
  - Generator power +3.6/s
  - Lab research ×1.2
  - Lab fruits +1
  - Grove production ×1.2
  - Grove fruits +1
  - Home fruits +1
- Hub levels apply **only to the current plant**. They're wiped on launch, and removed if the Hub is deleted (re-applied if rebuilt).
- A `buff` cost type exists in code but has no upgrades.

## 12. Spells (active abilities)

Spells live as buttons in the bottom bar: click the spell, then click a target. Each has a cooldown.

| Spell | Effect | Cooldown | Availability |
|---|---|---|---|
| **Delete** | Destroy a non-Core node (explodes). Its disconnected subtree explodes too | 2 s | base |
| **Focus** (weight) | Give a node power priority for 10 s (yellow halo) | 10 s | base |
| **Buff** | Buff a production node: ×1.5 gen for ~28 s | ~42 s | achievement "Super Focus" |
| Turbo | ×3 game speed toggle | — | dev only |

**Buffed state:** used by the Buff spell, Buffer particles, and the Shaman's version for crawlers. It multiplies power, research, fruit and buff gen by 1.5 and shows a visual marker.

## 13. Achievements

They're permanent across launches. Each one applies its reward immediately. Progress counters show in the sidebar Knowledge tab.

| Name | Requirement | Reward |
|---|---|---|
| Eventuality | Launch 5 times (text says 10) | Launch at 95% instead of 100% |
| Smart Particles | 15 particles alive at once | Smarter blob routing |
| Longest Stem | Launch with the Seedling ≥ 9 hops from the Core | +1 stem |
| Super Focus | Cast Focus 20 times | Unlock Buff spell |
| Overpopulation | 15 crawlers at once | Eating gives +10% health |
| Genocide | 100 crawler deaths total | Crawlers +10% speed |
| Expanded Evolution | Reach Tier 3 | Unlock Hub |
| Toggle Toggle | Any Hub upgrade to level 3 | Hub collection toggles |

**Scores tracked:** launches, crawler deaths, Genes saved, Focus casts.

## 14. UI & presentation

- **Bottom bar:** node build buttons with counts, spell buttons with cooldown timers, and the Launch Seedling button showing %.
- **Sidebar:** three tabs.
  - **Node list:** every node with a live power bar. Can hide stems. Hover highlights the node in the world. Seedling, Core and Hub entries open their panels.
  - **Crawler list:** health, current action, and preference.
  - **Knowledge:** plant overview plus achievements.
- **Tooltips:** hover any node or crawler for detailed stats (gen, drain, transfer rate, connections, buffs).
- **Visual feedback:**
  - Node glow = fill.
  - Pulse colour on receiving fruit or research.
  - Connection-count labels while building or deleting.
  - Highlight of valid spell targets.
  - Explosion particles on delete.
- **Info popups:** toast text, e.g. "Game Saved!" and achievement unlocks.
- **Run timer** display.
- **Main menu:**
  - Start Game
  - Full Reset (with confirm)
  - Language button (only English exists)
  - Credits string

## 15. Tutorial

It's a strict step gate: node buttons stay disabled until each step is done.

1. Welcome modal.
2. Arrow pointing at Stem: "drag a Stem onto your Core".
3. The game waits until a stem exists and is connected.
4. All buttons unlock (Core stays disabled).
5. "Keep building…" closing modal.

## 16. Save / persistence

- **Autosave** every 30 s to local storage, plus manual export/import via console. Save data versioning exists (v29).
- **Saved data:**
  - Nodes: type, power, position, links; extra fields for the Seedling and Hub.
  - Crawlers: type, health, preference, location.
  - Skills current/next, tier.
  - Hub levels.
  - Achievements and scores.
  - Tutorial step.
  - Timers.
- **No offline progress:** the garden only lives while the tab is open, and nothing advances while closed.
- Unused fields: `options.autoFill`, `logins`, `firstVersion`.

## 17. Dev / debug features (GOD_MODE)

- Game-speed keys `[` `]`.
- Space to pause. This one also works in normal play.
- Tier preset saves (keys 0–4, Ctrl+0–2).
- Spawn a crawler.
- Cheat research.
- Force a prestige event.
- Cheap hub costs.
- Force an achievement.
- FPS and blob counters.
- Turbo spell.

---

## Quick list of loose ends / quirks worth knowing

- Text/code mismatches:
  - Eventuality: 10 vs 5.
  - Longevity: −50% vs ×0.75.
- Wood fruit has no use.
- Execute/GetExecuted are implemented but disabled.
- Enemy node slugs are unused.
- Chieftain has no unique behaviour.
- The Buff hub cost type is unused.
- Tier 4 is a "You win" placeholder plus sandbox caps.
- All pacing is frame-based (60 ticks/s). The sim runs every frame and pauses when the tab is closed.
