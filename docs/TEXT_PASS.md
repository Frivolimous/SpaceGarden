# Text pass

All the words a player sees, grouped by where they appear. Write your version in **New text** and leave the row blank to keep it. Send the file back, or push it, and I'll put the changes into the code.

- **Where** is the file and line in `godot/` (`src/Core` or `game/scripts`).
- `{name}` marks a number or name the game fills in. Keep it, move it, or tell me in words what you want shown.
- A few rows show the code itself because the wording changes with a number (for example "A fruit grew." and "3 fruits grew."). Write the words you want for each case.
- Several strings on one line are separated by ·
- Not included: dev and test text (the `--` flags, the frame-rate log) and anything only in logs.

## Terms (Jeremy, Nov 4; applied)

Expansions (was Research), tending mode (was build mode), tending tools, motes (was blobs), drain (was upkeep), max power (was store), links (was connections), power gen ("Gen" for short on the cards), wildflower (one word), genes, notes (the lab's fruit, was research), grow (was plant: grow a seedling, grow the Hub), the garden (was the plant, for the whole structure). Sentence case for every label, title and heading; names keep their capitals (Gravity Pot, Lumber Grove, the evolutions). American spelling (minimize).

## Still open

Nothing.


## Tutorial

| Where | Text | New text |
|---|---|---|
| `Tutorial.cs:34` | Welcome to Space Garden! | |
| `Tutorial.cs:35` | Grow a grove | |
| `Tutorial.cs:36` | Wait for the grove | |
| `Tutorial.cs:37` | Grow a stem | |
| `Tutorial.cs:38` | Wait for the stem | |
| `Tutorial.cs:39` | Grow a lab | |
| `Tutorial.cs:40` | Tending mode | |
| `Tutorial.cs:41` | Wait for genes | |
| `Tutorial.cs:42` | Expand your stems | |
| `Tutorial.cs:43` | You're on your own! | |
| `Tutorial.cs:49` | Guide your garden's growth and watch it bloom, expand and flourish. | |
| `Tutorial.cs:50` | Groves send growth {Growth} that helps your garden grow faster. | |
| `Tutorial.cs:51` | Stems give you more space to grow flowers, and carry power between them. | |
| `Tutorial.cs:52` | Labs send genes {Genes} to the core, which are used to buy upgrades. | |
| `Tutorial.cs:53` | While you wait, let's arrange your garden! Open tending mode from the core's menu and set your core in a Gravity Pot. | |
| `Tutorial.cs:54` | Tending tools are just used to arrange your garden and don't have any other in-game effect. You'll unlock more tools as your garden grows. | |
| `Tutorial.cs:55` | Labs send genes {Genes} that are absorbed by the core. When you have enough, you can start upgrading your garden. | |
| `Tutorial.cs:56` | Explore your options and keep growing your garden while your genes accumulate! | |
| `Tutorial.cs:57` | This is how your garden grows! | |
| `Tutorial.cs:58` | You're doing great! Keep growing your garden and soon you'll unlock the next layer. | |
| `Widget.cs:124` | Continue | |
| `Widget.cs:380` | Getting started | |

## Core menu, Settings menu, tray

| Where | Text | New text |
|---|---|---|
| `CoreMenu.cs:17` | Garden info · Almanac · Expansions · Tending mode | |
| `CoreMenu.cs:18` | Ambient ({Hotkey}) · Minimize · Settings | |
| `Widget.cs:104` | New garden | |
| `Widget.cs:105` | Start a new garden? This one can't be brought back. | |
| `Widget.cs:106` | Start a new garden | |
| `Widget.cs:313` | Size | |
| `Widget.cs:315` | Idle frame rate | |
| `Widget.cs:317` | `menu.AddRadioCheckItem($"{IdleRates[i]} fps{(IdleRates[i] == 30 ? " (default)" : "")}", (int)MenuId.Idle0 + i);` | |
| `Widget.cs:319` | Opacity | |
| `Widget.cs:322` | Size | |
| `Widget.cs:324` | `ambientMenu.AddRadioCheckItem($"{AmbientScales[i] * 100:0}% of Size{(AmbientScales[i] == 1 ? " (default)" : "")}", (int)MenuId.AmbientScale0 + i);` | |
| `Widget.cs:325` | Idle frame rate | |
| `Widget.cs:327` | `ambientMenu.AddRadioCheckItem($"{IdleRates[i]} fps{(IdleRates[i] == 30 ? " (default)" : "")}", (int)MenuId.AmbientIdle0 + i);` | |
| `Widget.cs:330` | Ambient | |
| `Widget.cs:332` | New garden... | |
| `Widget.cs:336` | Always on top | |
| `Widget.cs:337` | Growth speed | |
| `Widget.cs:338` | Real time (1x) | |
| `Widget.cs:339` | Fast (60x) | |
| `Widget.cs:340` | Very fast (3600x) | |
| `Widget.cs:341` | Physics | |
| `Widget.cs:342` | Fruit borrows its parent's push (cheaper) | |
| `Widget.cs:343` | Tracking | |
| `Widget.cs:346` | Developer | |
| `Widget.cs:348` | Quit | |
| `Widget.cs:426` | Space Garden: click to show or hide, right-click for settings. {Hotkey} to wake the garden. | |

## Part toolbar, bud hints, undo

| Where | Text | New text |
|---|---|---|
| `Toolbar.cs:99` | Grow a stem · Grow a generator · Grow a lab | |
| `Toolbar.cs:100` | Grow a grove · Grow a seedling | |
| `Toolbar.cs:101` | Grow the Hub | |
| `Toolbar.cs:102` | Hub upgrades · Take it off its pot or trellis | |
| `Toolbar.cs:103` | Prune · Remove | |
| `Toolbar.cs:177` | Cancel the {kind} bud (its growth is lost) | |
| `Toolbar.cs:178` | Change the bud to {kind} (it starts over) | |
| `Toolbar.cs:185` | {Links}/{MaxLinks} links used | |
| `Widget.cs:1521` | Tap again to cancel | |
| `Widget.cs:1522` | Tap again to switch types | |
| `Card.cs:338` | Put the last pruned branch back (Ctrl+Z) | |
| `Card.cs:357` | Undo prune  {UndoSecondsLeft} s | |

## Tending mode and its tools

| Where | Text | New text |
|---|---|---|
| `Toolbar.cs:425` | Drag to move the garden | |
| `Toolbar.cs:439` | Power · Show each part's power on a heat map | |
| `Toolbar.cs:440` | Mote paths · Show where gene, growth and buff motes have been going | |
| `Toolbar.cs:470` | Done tending | |
| `Toolbar.cs:494` |  Another at garden size {at}. | |
| `Toolbar.cs:495` | {name}: comes at garden size {size} (now {size}:  | |
| `Toolbar.cs:496` | the most stems and flowers it has had, the core too). {kind} | |
| `Toolbar.cs:497` | {name}: drag one into the garden.{Another at…} {what it does} | |
| `Toolbar.cs:498` | {name}: all placed.{Another at…} {what it does} | |
| `ShapingLook.cs:16` | Gravity Pot | |
| `ShapingLook.cs:17` | Trellis | |
| `ShapingLook.cs:18` | Sway Controller | |
| `ShapingLook.cs:19` | Gravity Well | |
| `ShapingLook.cs:24` | Drop a part on it: the part is held there. | |
| `ShapingLook.cs:25` | Drop parts on it: they're held along its line. | |
| `ShapingLook.cs:26` | Place it anywhere and turn its knob to adjust the garden's sway. | |
| `ShapingLook.cs:27` | Pulls the whole garden towards it. Pot the core to keep the garden where it is. | |

## Info cards (part and garden)

| Where | Text | New text |
|---|---|---|
| `Card.cs:70` | Moonlit: a Moonflower nearby | |
| `Card.cs:147` | Power:  | |
| `Card.cs:150` |  (sends {BurstShare} of its max power at {BurstFill}) | |
| `Card.cs:151` |  (sends {Sends}/s) | |
| `Card.cs:153` | Gen: {Makes}/s{sends} | |
| `Card.cs:154` | Drain: {Upkeep}/s{sends} | |
| `Card.cs:155` | Sends: {Sends}/s | |
| `Card.cs:156` | Growth: {GrowthBlobsPerHour}/min | |
| `Card.cs:157` | Genes: {GenesPerHour}/min | |
| `Card.cs:158` | Links: {Links} / {MaxLinks} | |
| `Card.cs:161` | Evolution:  | |
| `Card.cs:165` | Active for: {Life} (motes worth double) | |
| `Card.cs:167` | `Line(ref used, $"{(spec.Kind == NodeKind.Ember ? "Burns out" : "Withers")} in: {Eta(Math.Max(0, Garden.LifeOf(node) - node.Life) / speed)}");` | |
| `Card.cs:172` | Hub points:  | |
| `Card.cs:173` | Pool: {HubPool} genes | |
| `Card.cs:174` | Next point: {genes} more genes | |
| `Card.cs:179` | Next seedling: {waiting} | |
| `Card.cs:183` | `Line(ref used, $"Forming: {forming.Name}{(forming.IsFruit ? "" : " bud")} {stats.FormingProgress * 100:0}%");` | |
| `Card.cs:198` | Garden | |
| `Card.cs:201` | Power:  | |
| `Card.cs:204` | Seedling: {SeedlingPower} / {SeedlingStore} | |
| `Card.cs:206` | Gen:  | |
| `Card.cs:207` | , Drain: {Upkeep}/s | |
| `Card.cs:212` | Genes:  | |
| `Card.cs:214` |  (in Expansions) ·  (ETA {eta}) | |
| `Card.cs:215` |  (grow a lab) | |
| `Card.cs:216` | Next: {next}, {Cost}{when} | |
| `Card.cs:220` | Genes: {GeneBalance} | |
| `Card.cs:221` | Next: every upgrade bought | |
| `Card.cs:225` | Stems:  | |
| `Card.cs:226` | Generators:  | |
| `Card.cs:228` | Core links:  | |
| `Card.cs:232` | Wildflowers:  | |
| `Card.cs:234` | Groves: {Groves}, Labs: {Labs} | |
| `Card.cs:290` | under 1m | |

## Expansions panel and upgrades

| Where | Text | New text |
|---|---|---|
| `ResearchPanel.cs:40` | Expansions · Close Expansions | |
| `ResearchPanel.cs:74` | Stems | |
| `ResearchPanel.cs:75` | Generators | |
| `ResearchPanel.cs:76` | Core | |
| `ResearchPanel.cs:90` | Minimize | |
| `ResearchPanel.cs:108` | Show the cards · Minimize | |
| `ResearchPanel.cs:209` | Buy | |
| `ResearchPanel.cs:315` | {GeneBalance} genes  ({perSecond}/s) | |
| `ResearchPanel.cs:320` | All bought | |
| `ResearchPanel.cs:321` | Every upgrade on this track is bought. | |
| `ResearchPanel.cs:329` | Buy | |
| `ResearchPanel.cs:330` | {lacking} more | |
| `ResearchPanel.cs:331` | in {time} · no genes coming | |
| `ResearchPanel.cs:338` | Stem · Generator · Core | |
| `ResearchPanel.cs:341` | {name}: done · {name}: Buy | |
| `ResearchPanel.cs:342` | {name}: in {time} · {name}: no genes coming | |
| `ResearchPanel.cs:343` | {name}: {lacking} more | |
| `Garden.cs:2718` | +1 stem | |
| `Garden.cs:2719` | +1 generator | |
| `Garden.cs:2720` | +1 wildflower slot | |
| `Garden.cs:2721` | +1 core link | |
| `Garden.cs:2722` | +1 seedling slot | |
| `Garden.cs:2723` | Hub | |
| `Garden.cs:2729` | Increases the total number of stems. | |
| `Garden.cs:2730` | Increases the total number of generators. | |
| `Garden.cs:2731` | Wildflowers are rare, stronger kinds of flower. Any new bud may grow into one instead if available. | |
| `Garden.cs:2732` | Lets one more branch grow straight from the core. | |
| `Garden.cs:2733` | Each seedling unlocks a single powerful evolution. | |
| `Garden.cs:2734` | The Hub takes in genes for a different unlock path. | |

## Hub panel and paths

| Where | Text | New text |
|---|---|---|
| `HubPanel.cs:34` |  (in {time}) | |
| `HubPanel.cs:42` | Hub · Close the Hub | |
| `HubPanel.cs:55` | Reset levels · Confirm reset | |
| `HubPanel.cs:56` | Every path back to level 0, its points refunded but {HubRespecCost} (a level's worth) | |
| `HubPanel.cs:69` | Parts | |
| `HubPanel.cs:75` | Wildflowers | |
| `HubPanel.cs:76` | Hub | |
| `HubPanel.cs:165` | Each level: {PerLevel}. ⏎  | |
| `HubPanel.cs:166` | Level {Level} ({Level} points): {Text}. | |
| `HubPanel.cs:183` | 1 point · {HubPoints} points | |
| `HubPanel.cs:186` | {HubPool} / {NextPointCost} genes to the next point | |
| `HubPanel.cs:200` | Not discovered | |
| `HubPanel.cs:207` | Not discovered | |
| `HubPanel.cs:212` | Complete: {…} | |
| `HubPanel.cs:213` | Next: {Text} · Next: {PerLevel} | |
| `HubPanel.cs:216` | Max | |
| `HubPanel.cs:221` | 1 pt · {cost} pts | |
| `Hubs.cs:77` | +20% transfer | |
| `Hubs.cs:78` | Stem max power +{HubStemStore} | |
| `Hubs.cs:79` | Stem cap +{HubStemCap} | |
| `Hubs.cs:80` | +20% power gen | |
| `Hubs.cs:81` | Power transfer ×1.5 | |
| `Hubs.cs:82` | Power gen 50-100% with stored power, instead of 0-100% | |
| `Hubs.cs:83` | +20% growth | |
| `Hubs.cs:84` | +1 fruit slot | |
| `Hubs.cs:85` | +2 fruit slots | |
| `Hubs.cs:86` | +20% genes | |
| `Hubs.cs:87` | Gene motes that fade give {HubFadedGenes3} of their genes to the core | |
| `Hubs.cs:88` | Gene motes that fade give {HubFadedGenes5} of their genes to the core | |
| `Hubs.cs:90` | +10% chance to double a mote's value; core +20% power gen | |
| `Hubs.cs:92` | {HubTripleChance}% chance to triple | |
| `Hubs.cs:93` | +1 glow; core +20% power gen | |
| `Hubs.cs:94` | +1 link | |
| `Hubs.cs:95` | +30% transfer; stems +10% transfer | |
| `Hubs.cs:97` | +1 link | |
| `Hubs.cs:98` | +30% transfer; stems +10% transfer | |
| `Hubs.cs:100` | +1 link | |
| `Hubs.cs:101` | +30% power gen; generators +10% power gen | |
| `Hubs.cs:103` | Max power ×1.5: bigger bursts | |
| `Hubs.cs:104` | +30% power gen; generators +10% power gen | |
| `Hubs.cs:106` | Transfer +50% | |
| `Hubs.cs:107` | +30% power gen; generators +10% power gen | |
| `Hubs.cs:109` | Burns 50% longer (fades over 1.5 days) | |
| `Hubs.cs:110` | +30% growth; groves +10% growth | |
| `Hubs.cs:112` | Drain −50% | |
| `Hubs.cs:113` | +30% growth; groves +10% growth | |
| `Hubs.cs:115` | Buds its motes start are twice as likely to grow wild | |
| `Hubs.cs:116` | +30% genes; labs +10% genes | |
| `Hubs.cs:118` | Max power +{HubWildLabStore}: steadier | |
| `Hubs.cs:119` | +30% genes; labs +10% genes | |
| `Hubs.cs:121` | With all 4 leaves, makes {HubLichenGen} power gen a second and transfers at {HubLichenGen} | |
| `Hubs.cs:123` | All buffs +10% | |
| `Hubs.cs:124` | Buffs last +{HubBuffSeconds} s | |
| `Hubs.cs:125` | Active 2 minutes longer; core +20% power gen | |
| `Hubs.cs:127` | Lingers half as long once dormant | |
| `Hubs.cs:128` | Wildflowers near it +5% more; core +20% power gen | |
| `Hubs.cs:130` | Range from {MoonflowerReach} links to {HubMoonflowerReach} | |
| `Hubs.cs:134` | +1 link | |
| `Hubs.cs:135` | The drain each Hub upgrade adds −20% | |

## Parts and fruit (names, Codex descriptions)

| Where | Text | New text |
|---|---|---|
| `NodeSpecs.cs:159` | Core · The heart: makes power, and takes in genes. | |
| `NodeSpecs.cs:166` | Stem · Structure: carries power and motes. Low drain, less with leaves. | |
| `NodeSpecs.cs:174` | Generator · Makes power for its branch. | |
| `NodeSpecs.cs:182` | Lab · Sends gene motes; only those that find the core count. | |
| `NodeSpecs.cs:191` | Grove · Sends growth motes that grow fruit and new parts. | |
| `NodeSpecs.cs:200` | Leaf · Cuts its stem's drain by a quarter. | |
| `NodeSpecs.cs:206` | Burr · Raises output by 10%. | |
| `NodeSpecs.cs:212` | Note · Raises output by 10%. | |
| `NodeSpecs.cs:218` | Berry · Raises output by 10%. | |
| `NodeSpecs.cs:229` | Seedling · Takes in power, then blooms into its chosen evolution's wildflower. | |
| `NodeSpecs.cs:238` | Hub · Catches gene motes for points of its own, which boost each kind of part. Each boost increases the Hub's drain. | |

## Wildflowers (names, Codex: what it does / its catch)

| Where | Text | New text |
|---|---|---|
| `WildFlowers.cs:21` | Wood · Raises output by 10%. | |
| `WildFlowers.cs:27` | Glow · Sends 1 buff mote every 60s. | |
| `WildFlowers.cs:59` | Amplifier | |
| `WildFlowers.cs:60` | Gene and growth motes passing through have a 25% chance to double in value, buff motes move +3 steps. · Chance to double motes. | |
| `WildFlowers.cs:67` | Resistor | |
| `WildFlowers.cs:68` | Motes bounce off it from both sides. · Motes can't go through. | |
| `WildFlowers.cs:75` | Tendril | |
| `WildFlowers.cs:76` | High max power and a fast, steady transfer. Takes a wildflower and a stem slot. · Fast, steady transfer. | |
| `WildFlowers.cs:82` | Big Stem | |
| `WildFlowers.cs:83` | Biggest stem with 4 links but slow transfer. Takes a wildflower and a stem slot. · 4 links. | |
| `WildFlowers.cs:90` | Pulsar | |
| `WildFlowers.cs:91` | About twice a generator's power gen, released in bursts. Takes a wildflower and a generator slot. · Power released in bursts. | |
| `WildFlowers.cs:98` | Twin Star | |
| `WildFlowers.cs:99` | A generator that continues the branch: 2 links. Takes a wildflower and a generator slot. · Generator with 2 links. | |
| `WildFlowers.cs:104` | Ember | |
| `WildFlowers.cs:105` | Starts with a high power gen that fades over 24h. Takes a wildflower and a generator slot. | |
| `WildFlowers.cs:106` | Powerful, but burns out after 24h. | |
| `WildFlowers.cs:113` | Lumber Grove | |
| `WildFlowers.cs:114` | Growth motes twice a grove's size. · Double growth motes. | |
| `WildFlowers.cs:119` | Seed Pod | |
| `WildFlowers.cs:120` | A big burst of growth motes every 60s. · Big bursts of growth every minute. | |
| `WildFlowers.cs:127` | Wild Lab | |
| `WildFlowers.cs:128` | Has up to 6 fruit reaching twice the gene output of a lab. · More fruit gives more genes. | |
| `WildFlowers.cs:133` | Lichen | |
| `WildFlowers.cs:134` | Slightly higher output than a lab, and its 4 leaves bring its drain to 0. · Leaves reduce its drain to 0. | |
| `WildFlowers.cs:141` | Buffer | |
| `WildFlowers.cs:142` | Buffed flowers have 50% increased output for 30s. Sends one buff mote every 4s. · Buffs nearby flowers. | |
| `WildFlowers.cs:146` | Comet Bloom | |
| `WildFlowers.cs:147` | 10 minutes of double-value gene and growth motes, and buffs last twice as long. | |
| `WildFlowers.cs:148` | Doubles all motes for 10m. | |
| `WildFlowers.cs:153` | Moonflower | |
| `WildFlowers.cs:154` | Buds nearby grow wild twice as often; wildflowers near it have a 25% higher output. · Boosts wildflowers nearby. | |
| `WildFlowers.cs:159` | Nebula Bloom | |
| `WildFlowers.cs:160` | Power reaching a seedling within 5 links counts double. · Retired: no longer grows. | |

## Evolutions (names, effects, the choice menu)

| Where | Text | New text |
|---|---|---|
| `Evolutions.cs:74` | Core power gen | |
| `Evolutions.cs:75` | Core link | |
| `Evolutions.cs:76` | core transfer | |
| `Evolutions.cs:77` | core max power | |
| `Evolutions.cs:78` | All stem transfer | |
| `Evolutions.cs:79` | their max power | |
| `Evolutions.cs:80` | Stems hold +{StemFruits} leaf | |
| `Evolutions.cs:81` | Motes always avoid dead ends | |
| `Evolutions.cs:82` | Motes avoid dead ends {SmartChance}% of the time | |
| `Evolutions.cs:83` | All gene motes' size | |
| `Evolutions.cs:84` | their drain | |
| `Evolutions.cs:85` | Generator cap | |
| `Evolutions.cs:86` | Every generator kind's power gen | |
| `Evolutions.cs:87` | All generator transfer | |
| `Evolutions.cs:88` | Generator transfer | |
| `Evolutions.cs:89` | Wildflower limit | |
| `Evolutions.cs:90` | Wildflower output +{WildEffect}% | |
| `Evolutions.cs:91` | Buffs {BuffBonus}% of the output | |
| `Evolutions.cs:118` | Core Growth | |
| `Evolutions.cs:120` | Second Path | |
| `Evolutions.cs:122` | Long Stemmed | |
| `Evolutions.cs:124` | Smart Motes | |
| `Evolutions.cs:126` | Rapid Evolution | |
| `Evolutions.cs:128` | More Power | |
| `Evolutions.cs:130` | Best Generation | |
| `Evolutions.cs:132` | Twin Stars | |
| `Evolutions.cs:134` | Big Stem | |
| `Evolutions.cs:136` | Wild Bloom | |
| `Evolutions.cs:138` | Resonant Frequency | |
| `EvolutionMenu.cs:125` | Blooms into {flower} {flower} | |
| `EvolutionMenu.cs:126` | ; {flower}s can then grow wild | |

## Almanac

| Where | Text | New text |
|---|---|---|
| `Almanac.cs:37` | Almanac · Close the Almanac | |
| `Almanac.cs:46` | Journal | |
| `Almanac.cs:47` | Codex | |
| `Almanac.cs:48` | Evolutions | |
| `Almanac.cs:158` | Day {day}, {time} | |
| `Almanac.cs:182` | Day {day} | |
| `Almanac.cs:239` | From the start · Found day {age}, {age} | |
| `Almanac.cs:242` | Parts | |
| `Almanac.cs:243` | Wildflowers | |
| `Almanac.cs:244` | Fruit | |
| `Almanac.cs:266` | None yet. Grow a seedling, fill it with power, and choose its evolution. | |
| `Almanac.cs:279` | Chosen day {day}, {time} | |
| `Almanac.cs:282` | Delete? | |
| `Almanac.cs:283` | Take this evolution back: grow a new seedling to choose a different evolution | |

## Journal and the away note

| Where | Text | New text |
|---|---|---|
| `Garden.cs:148` | A new garden began | |
| `Garden.cs:1540` | First passive bud started | |
| `Garden.cs:1697` | Evolution chosen: {evolution} | |
| `Garden.cs:1720` | {evolution} bloomed into {flower} {flower} | |
| `Garden.cs:1776` | Evolution taken back: {evolution} | |
| `Garden.cs:1922` | Evolution chosen: {evolution} | |
| `Garden.cs:2014` | The Hub's levels were reset | |
| `Garden.cs:2076` | {CoreLinks} core link bought | |
| `Garden.cs:2077` | Hub bought | |
| `Tracking.cs:60` | Wildflower discovered: {Name} | |
| `Tracking.cs:62` | First Hub grown | |
| `Tracking.cs:63` | First {Name} emerged | |
| `Tracking.cs:66` | The garden reached {milestone} parts | |
| `Tracking.cs:317` | {Name}, now {Becomes} {Becomes} | |
| `Tracking.cs:319` | `if (bloomed.Count > 0) lines.Add($"{(bloomed.Count == 1 ? "A seedling" : $"{bloomed.Count} seedlings")} bloomed: {string.Join("; ", bloomed)}.");` | |
| `Tracking.cs:330` | `lines.Add($"{(parts == 1 ? "A new part" : $"{parts} new parts")} grew: {List(kinds)}.");` | |
| `Tracking.cs:333` | A fruit grew. · {fruit} fruits grew. | |
| `Tracking.cs:342` | Wildflowers grew: {names}. | |
| `Tracking.cs:347` | {Text} (see the Codex). | |
| `Tracking.cs:359` | {genes} genes reached the core. | |
| `Tracking.cs:362` | `if (points > 0) lines.Add($"The Hub earned {(points == 1 ? "a point" : $"{points} points")} ({garden.HubPoints} to spend).");` | |
| `Tracking.cs:372` | {Count} more in the Almanac. | |
| `Tracking.cs:379` | Power ran low: down to {lowest}% of what it held when you left. | |
| `Tracking.cs:398` |  and  | |
| `Main.cs:392` | `Toast($"{string.Join(". ", gained)}. Place {(gained.Count == 1 ? "it" : "them")} in tending mode.", ShapingLook.Tint);` | |
| `Main.cs:414` | While you were away ({time away}) | |
