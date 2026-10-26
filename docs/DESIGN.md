# Space Garden — Revival Design

*Status: draft, October 2026. Built in Godot 4 (.NET); see §7.*

## 1. Vision

A small, beautiful space plant that lives on your desktop. You glance at it, tend it for a minute, and leave it be. Over days, weeks and months it grows into something that is uniquely yours.

It is a **companion first, game second**. The game is there to give tending meaning, not to demand attention.

### Pillars
1. **Beautiful at a glance.** Every idle moment should look good: swaying stems, flowing light, drifting creatures. If a mechanic makes the garden uglier or noisier, it needs a very good reason.
2. **Slow and permanent.** The garden accumulates. Nothing you grew is taken away by the game's structure.
3. **Never punishing.** Absence is fine. Coming back is a pleasant surprise, never a disaster.
4. **Gentle agency.** The player makes small, meaningful choices: where to grow, what to evolve, how to shape it. No twitch, no optimisation pressure.
5. **Lightweight presence.** Runs all day on a desktop without anyone noticing it in CPU, memory or battery.

### Non-goals
- Being optimal-play-driven or min-maxable.
- Mandatory resets / prestige.
- Requiring the window to be open to make progress.

## 2. What changed, and why

The last iteration (v0.0.2, 2024) became a tactical idle game: a mandatory prestige loop, spells on cooldown, kill counts, frame-rate-tuned pacing. The systems themselves are good; their *framing* lost the charm.

**Decision:** the only mechanic removed is the **mandatory prestige reset** (`GameUI.nextStage` → `resetGame`). Every other system is kept and repurposed below.

The current version is preserved with a git tag (`tactical-idle`) before any changes.

## 3. Core loop (new)

```
 glance ─► notice something (a bloom, a ripe fruit, a creature, a full seedling)
   ▲                    │
   │                    ▼
 leave ◄── make one small choice (grow a node, prune, evolve, shape)
```

Short sessions of 30 seconds to 5 minutes, several times a day. The garden keeps growing between sessions.

### Seedling evolution without reset
Today the seedling fills up and a "Proceed" button wipes the garden to unlock the next skill tier. New flow:

1. The seedling fills slowly over roughly a few days of real time.
2. When full, it **blooms**: the player picks an evolution (an existing skill from `SkillData`) that applies **immediately and permanently** to this garden.
3. The seedling releases a seed that drifts into the sky and becomes a **star in the background starfield** (`Starfield.ts`), a visible record of every evolution. A new seedling sprouts in its place.
4. Skill tiers unlock as the garden matures (number of blooms), not through resets.

This keeps the old progression content (tiers 1 to 4) as the long-term arc, now spread over weeks.

## 4. Repurposing existing systems

| System (code) | Was | Becomes |
|---|---|---|
| Prestige / tiers (`nextStage`, `skillTier`) | Mandatory wipe to progress | **Removed** as a reset; tiers become maturity stages (see §3) |
| Skills (`SkillData`) | Picked between runs | Evolutions chosen at each bloom, applied live |
| Spells (`Spells/`) | Tactical cooldown abilities | **Gardener's tools**, with cooldowns in real time |
| └ Delete | Destroy node | Pruning shears: trim a branch. The trimmed piece becomes a small keepsake or compost |
| └ Weight | Pin/heavy node | Shape / stake: guide the plant's form |
| └ Buff | Temporary boost | Watering / sunlight: gentle growth boost, once or twice a day |
| └ Turbo | Debug speed | Stays dev-only |
| Crawlers (`CrawlerCommands/`) | Units to manage | **Garden creatures**: autonomous, mostly cosmetic, gently helpful |
| └ Eat / Breed / Research / Power / Buff | Economy actions | Kept as natural behaviours |
| └ Execute / GetExecuted / Frustrated / Starving | Population control, failure states | A natural life cycle: creatures age, rest, and return to the soil as nourishment. No "kill" framing |
| Achievements (`ATSData`, `GameKnowledge`) | Requirement → reward pressure | **Gardener's Almanac**: discoveries logged with a sketch and flavour text. Rewards are cosmetic or small. "Genocide" gets reworked |
| Scores | Run scores | Lifetime garden stats in the Almanac (age, blooms, tallest stem…) |
| Hub + toggles (`HubPanel`) | Resource routing micro-management | Greenhouse: long-term storage and upgrades. Toggles become set-and-forget preferences |
| Transfer blobs (`Transfers/`) | Resource packets | Kept. This is the main "living light" visual |
| Node types (`NodeData`) | Economy pieces | Kept. Visual and structural variety matters more than balance |
| Tutorial | Strict step gate | Gentle, skippable hints spread over the first days |

## 5. Time model

The biggest technical change. Today the simulation advances once per rendered frame at 60fps (`JMTicker`), so the garden only lives while the tab is open, and it burns CPU while it does.

### Requirements
- **Wall-clock driven.** Simulation advances by real elapsed time, not frames.
- **Fixed simulation step, decoupled from rendering.** For example, the sim ticks at a fixed rate (1–10 Hz is likely plenty for the economy) while rendering interpolates.
- **Low-power idle mode.** When the player isn't interacting, drop the render rate (e.g. 10–20fps) and keep only ambient animation. Target well under 2% CPU idle.
- **Catch-up on return, gently capped.** Store `lastSeen`. On launch, simulate the elapsed time up to a cap (e.g. 72 hours; to be tuned) without rendering, then let physics settle. Show a short "while you were away" summary: new blooms, creatures, fruit.
- **Absence never hurts.** Catch-up must not starve nodes or kill creatures. Neglect at most slows growth.

### Pacing
All rates are currently tuned for minutes of play at 60 ticks/s. They need a rebalance pass against real-world targets:

| Milestone | Target |
|---|---|
| First bloom | Day 1 to 2 |
| Each subsequent bloom | Every 2 to 5 days |
| Noticeable visual change | Every session (each glance shows something new) |
| Full tier-4 garden | Around 2 to 3 months |

Rates should be expressed **per real second/hour** in data, not per tick.

## 6. Desktop widget

| Feature | Priority |
|---|---|
| Small frameless window, transparent background | Must |
| Always-on-top **or** pinned to desktop layer (user choice) | Must |
| Remembers size/position | Must |
| Low idle CPU / memory | Must |
| Tray icon: show/hide, quit | Should |
| Launch on startup (opt-in) | Should |
| Click-through mode (look but don't block clicks) | Should |
| Expand to a larger "tending" view on click | Could |
| Notifications for rare events (opt-in, very sparing) | Could |

The plant must look good **without any UI chrome**. Sidebar, bottom bar and panels appear only in the tending view.

## 7. Platform: Godot 4 (.NET)

**Decided (October 2026): Godot 4.7 with C#**, laid out like the other Godot projects: the rules in plain C# (`godot/src/Core`, no Godot), the presentation in the Godot project (`godot/game/`). The prototype in [godot/](../godot/README.md) is the starting point: the TS game's force-directed plant as a transparent, click-through, always-on-top overlay over the screen's work area.

Why, over the alternatives:
- **Web (TS + Pixi in Tauri or Electron):** heavier for an always-running widget, and transparency and click-through go through the wrapper. Godot gives native transparent, borderless windows; click-through is a few Win32 calls (`ClickThrough.cs`).
- **A lighter shell (a custom .NET app on raylib)**, measured in [spike-lite/](../spike-lite/README.md): the same idle CPU as Godot (about 4% of a core empty and 6% at 800 parts, at 30 fps), since most of the idle cost is waking to redraw and present a full-screen transparent window, which any engine pays. Its only gain was memory (about 130 MB against 250 MB), not worth rebuilding Godot's UI, input, text, audio and export pipeline.

Because the rules don't depend on Godot, the presentation layer can still be swapped later if that ever changes.

## 8. Roadmap

**Phase 0: Preserve.** Tag `tactical-idle`. Note what's currently broken.

**Phase 1: Foundations.** Platform decision (done: Godot, §7). Wall-clock fixed-step sim, `lastSeen` + capped catch-up, low-power idle rendering. Remove the prestige reset; implement in-place blooming.

**Phase 2: Calm.** Reframe spells as tools, creatures' life cycle, Almanac, gentle tutorial. Pacing rebalance to §5 targets.

**Phase 3: Widget.** Transparent always-on-top window, tray, autostart, compact vs. tending views.

**Phase 4: Charm.** Day/night tied to local time, seasons over weeks, rare events (visiting comets, creature migrations, special blooms), seed-stars in the sky, visual polish pass.

## 9. Open questions
- Catch-up cap: 24h, 72h, a week?
- One plant per garden, or can seeds be planted as additional plants over time?
- Is there any "care" need (e.g. watering), or is the garden fully self-sustaining with care only as a boost?
- Save migration: carry the old v29 saves forward, or start fresh?
- Monetisation / distribution target (itch.io, Steam, free/open)?
