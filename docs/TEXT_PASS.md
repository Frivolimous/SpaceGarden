# Text pass

All the words a player sees, grouped by where they appear. Write your version in **New text** and leave the row blank to keep it. Send the file back, or push it, and I'll put the changes into the code.

- **Where** is the file and line in `godot/` (`src/Core` or `game/scripts`).
- `{name}` marks a number or name the game fills in. Keep it, move it, or tell me in words what you want shown.
- A few rows show the code itself because the wording changes with a number (for example "A fruit grew." and "3 fruits grew."). Write the words you want for each case.
- Several strings on one line are separated by ·
- Not included: dev and test text (the `--` flags, the frame-rate log) and anything only in logs.

## Spotted while collecting

- `Toolbar.cs:439`: the Power heat map's tooltip still gives the old colours ("blue empty, green half, red full"). The map now shows red empty, yellow at 75%, green full and teal over full.
- `WildFlowers.cs:21`: the Wood fruit says "Crawlers will use it later". Crawlers aren't in the game.
- The tutorial says "Tending Mode" and "the tending menu", but the core menu's slot is "Build mode", and the tools are called "shaping tools" in some places and "gardening tools" in others. Worth picking one name for each.
- Capitals are mixed: "3/3 Connections Used" and the Hub's "PARTS / WILD FLOWERS / HUB" headings, against sentence case nearly everywhere else.

## Tutorial

| Where | Text | New text |
|---|---|---|
| `Tutorial.cs:34` | Welcome to Space Garden! | |
| `Tutorial.cs:35` | Grow a Grove | |
| `Tutorial.cs:36` | Wait for the Grove | |
| `Tutorial.cs:37` | Grow a Stem | |
| `Tutorial.cs:38` | Wait for the Stem | |
| `Tutorial.cs:39` | Grow a Lab | |
| `Tutorial.cs:40` | Tending Mode | |
| `Tutorial.cs:41` | Wait for Genes | |
| `Tutorial.cs:42` | Research Stems | |
| `Tutorial.cs:43` | You're on your own! | |
| `Tutorial.cs:49` | Guide your garden's growth and watch it bloom, expand and flourish. | |
| `Tutorial.cs:50` | Groves send Growth {Growth} that helps your garden grow faster. | |
| `Tutorial.cs:51` | Stems give you more space to grow flowers, and carry power between them. | |
| `Tutorial.cs:52` | Labs send Genes {Genes} to the core, which are used to buy upgrades. | |
| `Tutorial.cs:53` | While you wait, let's arrange your garden! Open the tending menu and plant your Core in a Gravity Pot. | |
| `Tutorial.cs:54` | Gardening tools are just used to arrange your garden and don't have any other in-game effect. You'll unlock more tools as your garden grows. | |
| `Tutorial.cs:55` | Labs send Genes {Genes} that are absorbed by the core. When you have enough, you can start upgrading your garden. | |
| `Tutorial.cs:56` | Explore your options and keep growing your garden while your genes accumulate! | |
| `Tutorial.cs:57` | This is how your garden grows! | |
| `Tutorial.cs:58` | You're doing great! Keep growing your garden, researching new things, and soon you'll unlock the next layer. | |
| `Widget.cs:124` | Continue | |
| `Widget.cs:380` | Getting started | |

## Core menu, Settings menu, tray

The Settings menu includes a few test items: Growth speed, Physics, Tracking, Developer. They're listed in case you want to rename or hide them.

| Where | Text | New text |
|---|---|---|
| `CoreMenu.cs:17` | Garden info · Almanac · Research · Build mode | |
| `CoreMenu.cs:18` | Ambient ({Hotkey}) · Minimise · Settings | |
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
| `Widget.cs:426` | Space Garden: click to show or hide, right-click for settings. {Hotkey} to tend. | |

## Part toolbar, bud hints, undo

| Where | Text | New text |
|---|---|---|
| `Toolbar.cs:99` | Grow a stem · Grow a generator · Grow a lab | |
| `Toolbar.cs:100` | Grow a grove · Plant a seedling (it uses a seedling slot) | |
| `Toolbar.cs:101` | Plant the Hub (one at a time) | |
| `Toolbar.cs:102` | Hub upgrades · Take it off its pot or trellis | |
| `Toolbar.cs:103` | Prune · Take this away | |
| `Toolbar.cs:177` | Cancel the {kind} bud (its growth is lost) | |
| `Toolbar.cs:178` | Change the bud to a/an {kind} (it starts over) | |
| `Toolbar.cs:185` | {Links}/{MaxLinks} Connections Used | |
| `Widget.cs:1521` | Tap again to cancel | |
| `Widget.cs:1522` | Tap again to switch types | |
| `Card.cs:338` | Put the last pruned branch back (Ctrl+Z) | |
| `Card.cs:357` | Undo prune  {UndoSecondsLeft} s | |

## Build mode and its tools

| Where | Text | New text |
|---|---|---|
| `Toolbar.cs:425` | Drag to move the whole garden at once | |
| `Toolbar.cs:439` | Power · Show each part's power as heat: blue empty, green half, red full | |
| `Toolbar.cs:440` | Blob paths · Show where gene and growth blobs have been going lately (counted from now) | |
| `Toolbar.cs:470` | Done arranging | |
| `Toolbar.cs:494` |  Another at garden size {at}. | |
| `Toolbar.cs:495` | {name}: comes at garden size {next} (now {size}: the most stems and flowers it has had, the core too). {what it does} | |
| `Toolbar.cs:497` | {name}: drag one into the garden.{Another at…} {what it does} | |
| `Toolbar.cs:498` | {name}: all placed.{Another at…} {what it does} | |
| `ShapingLook.cs:16` | Gravity Pot | |
| `ShapingLook.cs:17` | Trellis | |
| `ShapingLook.cs:18` | Sway Controller | |
| `ShapingLook.cs:19` | Gravity Well | |
| `ShapingLook.cs:24` | Drop a part on it: the part is held there, and its branch grows the way the pot faces. | |
| `ShapingLook.cs:25` | Drop parts on it: they're held along its line. | |
| `ShapingLook.cs:26` | Place it anywhere and turn its knob: left calms the plant's sway (to none), right stirs it (up to {3}×). Straight up is the usual. | |
| `ShapingLook.cs:28` | Pulls the whole plant towards it, core and all. Pot the core to keep the plant where it is. | |
| `Garden.cs:2122` | Gravity Pot | |
| `Garden.cs:2123` | Trellis | |
| `Garden.cs:2124` | Sway Controller | |
| `Garden.cs:2125` | Gravity Well | |

## Info cards (part and garden)

| Where | Text | New text |
|---|---|---|
| `Card.cs:70` | Moonlit: a Moonflower nearby | |
| `Card.cs:147` | Power:  | |
| `Card.cs:150` |  (sends {BurstShare} of its store at {BurstFill}) | |
| `Card.cs:151` |  (sends {Sends}/s) | |
| `Card.cs:153` | Gen: {Makes}/s{sends} | |
| `Card.cs:154` | Drain: {Upkeep}/s{sends} | |
| `Card.cs:155` | Sends: {Sends}/s | |
| `Card.cs:156` | Growth: {GrowthBlobsPerHour}/min | |
| `Card.cs:157` | Genes: {GenesPerHour}/min | |
| `Card.cs:158` | Connections: {Links} / {MaxLinks} | |
| `Card.cs:161` | Evolution:  | |
| `Card.cs:165` | Active for: {time} (blobs worth double) | |
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
| `Card.cs:214` |  (in Research) ·  (ETA {eta}) | |
| `Card.cs:215` |  (plant a lab) | |
| `Card.cs:216` | Next: {next}, {Cost}{when} | |
| `Card.cs:220` | Genes: {GeneBalance} | |
| `Card.cs:221` | Next: every upgrade bought | |
| `Card.cs:225` | Stems:  | |
| `Card.cs:226` | Generators:  | |
| `Card.cs:228` | Core links:  | |
| `Card.cs:232` | Wild flowers:  | |
| `Card.cs:234` | Groves: {Groves}, Labs: {Labs} | |
| `Card.cs:290` | under 1m | |

## Research panel and upgrades

| Where | Text | New text |
|---|---|---|
| `ResearchPanel.cs:40` | Research · Close Research | |
| `ResearchPanel.cs:74` | Stems | |
| `ResearchPanel.cs:75` | Generators | |
| `ResearchPanel.cs:76` | Core | |
| `ResearchPanel.cs:90` | Minimise | |
| `ResearchPanel.cs:108` | Show the cards · Minimise | |
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
| `Garden.cs:2705` | +1 stem | |
| `Garden.cs:2706` | +1 generator | |
| `Garden.cs:2707` | +1 wild flower slot | |
| `Garden.cs:2708` | +1 core link | |
| `Garden.cs:2709` | +1 seedling slot | |
| `Garden.cs:2710` | Hub | |
| `Garden.cs:2716` | Increases the total number of stems you can add to your garden. | |
| `Garden.cs:2717` | Increases the total number of generators you can add to your garden. | |
| `Garden.cs:2718` | One more wild flower slot: while one is free, a new bud may grow into any wild flower instead. | |
| `Garden.cs:2719` | Lets one more branch grow straight from the core. | |
| `Garden.cs:2720` | Lets you plant one more seedling, and so choose one more evolution. | |
| `Garden.cs:2721` | Unlocks the Hub: plant it by hand, and it catches genes to boost each kind of part. | |

## Hub panel and paths

A path's name is its part's name. Each path has an "each level" line, then its milestones (level 3, level 5).

| Where | Text | New text |
|---|---|---|
| `HubPanel.cs:34` |  (in {time}) | |
| `HubPanel.cs:42` | Hub · Close the Hub | |
| `HubPanel.cs:55` | Reset levels · Confirm reset | |
| `HubPanel.cs:56` | Every path back to level 0, its points refunded but {HubRespecCost} (a level's worth) | |
| `HubPanel.cs:69` | PARTS | |
| `HubPanel.cs:75` | WILD FLOWERS | |
| `HubPanel.cs:76` | HUB | |
| `HubPanel.cs:165` | Each level: {PerLevel}. ⏎  | |
| `HubPanel.cs:166` | Level {Level} ({Level} points): {Text}. | |
| `HubPanel.cs:183` | 1 point · {HubPoints} points | |
| `HubPanel.cs:186` | {HubPool} / {NextPointCost} genes to the next point | |
| `HubPanel.cs:200` | Not found in the garden yet | |
| `HubPanel.cs:207` | Not found in the garden yet | |
| `HubPanel.cs:212` | Complete: {…} | |
| `HubPanel.cs:213` | Next: {Text} · Next: {PerLevel} | |
| `HubPanel.cs:216` | Max | |
| `HubPanel.cs:221` | 1 pt · {cost} pts | |
| `Hubs.cs:77` | +20% transfer | |
| `Hubs.cs:78` | Stem max power +{HubStemStore} | |
| `Hubs.cs:79` | Stem cap +{HubStemCap} | |
| `Hubs.cs:80` | +20% power | |
| `Hubs.cs:81` | Power transfer ×1.5 | |
| `Hubs.cs:82` | Output 50-100% with stored power, instead of 0-100% | |
| `Hubs.cs:83` | +20% growth | |
| `Hubs.cs:84` | +1 berry slot | |
| `Hubs.cs:85` | +2 berry slots | |
| `Hubs.cs:86` | +20% genes | |
| `Hubs.cs:87` | Gene blobs that fade give {HubFadedGenes3} of their genes to the core | |
| `Hubs.cs:88` | Gene blobs that fade give {HubFadedGenes5} of their genes to the core | |
| `Hubs.cs:90` | +10% chance to double a blob's value; core +20% power | |
| `Hubs.cs:92` | {HubTripleChance}% chance to triple | |
| `Hubs.cs:93` | +1 glow; core +20% power | |
| `Hubs.cs:94` | +1 link | |
| `Hubs.cs:95` | +30% transfer; stems +10% transfer | |
| `Hubs.cs:97` | +1 link | |
| `Hubs.cs:98` | +30% transfer; stems +10% transfer | |
| `Hubs.cs:100` | +1 link | |
| `Hubs.cs:101` | +30% power; generators +10% power | |
| `Hubs.cs:103` | Power store ×1.5: bigger bursts | |
| `Hubs.cs:104` | +30% power; generators +10% power | |
| `Hubs.cs:106` | Transfer +50% | |
| `Hubs.cs:107` | +30% power; generators +10% power | |
| `Hubs.cs:109` | Burns 50% longer (fades over 1.5 days) | |
| `Hubs.cs:110` | +30% growth; groves +10% growth | |
| `Hubs.cs:112` | Upkeep −50% | |
| `Hubs.cs:113` | +30% growth; groves +10% growth | |
| `Hubs.cs:115` | Buds its blobs start are twice as likely to grow wild | |
| `Hubs.cs:116` | +30% genes; labs +10% genes | |
| `Hubs.cs:118` | Max power +{HubWildLabStore}: steadier | |
| `Hubs.cs:119` | +30% genes; labs +10% genes | |
| `Hubs.cs:121` | With all 4 leaves, makes {HubLichenGen} power a second and sends at {HubLichenGen} | |
| `Hubs.cs:123` | All buffs +10% of the output (150% to 160%) | |
| `Hubs.cs:124` | Buffs last +{HubBuffSeconds} s | |
| `Hubs.cs:125` | Active 2 minutes longer; core +20% power | |
| `Hubs.cs:127` | Lingers half as long once dormant | |
| `Hubs.cs:128` | Wild flowers near it +5% more; core +20% power | |
| `Hubs.cs:130` | Range from {MoonflowerReach} links to {HubMoonflowerReach} | |
| `Hubs.cs:134` | +1 link | |
| `Hubs.cs:135` | The upkeep each Hub upgrade adds −20% | |

## Parts and fruit (names, Codex descriptions)

| Where | Text | New text |
|---|---|---|
| `NodeSpecs.cs:159` | Core · The heart: makes the most power, and takes in genes. | |
| `NodeSpecs.cs:166` | Stem · Structure: carries power and blobs. Cheap upkeep, less with leaves. | |
| `NodeSpecs.cs:174` | Generator · Makes power for its branch, at half the core's rate. | |
| `NodeSpecs.cs:182` | Lab · Sends gene blobs; only those that find the core count. | |
| `NodeSpecs.cs:191` | Grove · Sends growth blobs that grow fruit and new parts. | |
| `NodeSpecs.cs:200` | Leaf · Cuts its stem's upkeep by a quarter. | |
| `NodeSpecs.cs:206` | Burr · Raises its generator's output a little. | |
| `NodeSpecs.cs:212` | Research · Raises its lab's gene output a little. | |
| `NodeSpecs.cs:218` | Berry · Raises its grove's growth output a little. | |
| `NodeSpecs.cs:229` | Seedling · Takes in power, then blooms into its chosen evolution's wild flower. | |
| `NodeSpecs.cs:238` | Hub · Catches gene blobs for points of its own, which boost each kind of part. | |

## Wild flowers (names, Codex: what it does / its catch)

Each flower has a name, then "what it does · its catch". Nebula Bloom is retired and only seen in old gardens.

| Where | Text | New text |
|---|---|---|
| `WildFlowers.cs:21` | Wood · Raises its Lumber Grove's growth output a little. Crawlers will use it later. | |
| `WildFlowers.cs:27` | Glow · Sends buff blobs, as a small Buffer. | |
| `WildFlowers.cs:59` | Amplifier | |
| `WildFlowers.cs:60` | Gene and growth blobs passing through have a 50% chance to double in value. · 2 links: it can't fork. High upkeep. | |
| `WildFlowers.cs:67` | Resistor | |
| `WildFlowers.cs:68` | Blobs bounce off it from both sides. · Low transfer: slows power beyond it. | |
| `WildFlowers.cs:75` | Tendril | |
| `WildFlowers.cs:76` | A long, springy link, a large store and a fast, steady transfer. · Takes a stem slot too. | |
| `WildFlowers.cs:82` | Big Stem | |
| `WildFlowers.cs:83` | 4 links. · Slower transfer; takes a stem slot too. | |
| `WildFlowers.cs:90` | Pulsar | |
| `WildFlowers.cs:91` | About twice a generator's power, released in bursts. · Uneven supply; takes a generator slot too. | |
| `WildFlowers.cs:98` | Twin Star | |
| `WildFlowers.cs:99` | A generator that continues the branch: 2 links. · Takes a generator slot too. | |
| `WildFlowers.cs:104` | Ember | |
| `WildFlowers.cs:105` | Starts at 2.5 times a generator's power, fading as it goes. · Fades over a day, then burns out unless pruned first; takes a generator slot too. | |
| `WildFlowers.cs:113` | Lumber Grove | |
| `WildFlowers.cs:114` | Growth blobs twice a grove's size, a little less often. · Higher upkeep. | |
| `WildFlowers.cs:119` | Seed Pod | |
| `WildFlowers.cs:120` | 20 growth blobs, each twice a grove's size, every minute. · Silent in between. | |
| `WildFlowers.cs:127` | Wild Lab | |
| `WildFlowers.cs:128` | 2 genes a blob every 6 s, and room for 6 research fruit. · High upkeep. | |
| `WildFlowers.cs:133` | Lichen | |
| `WildFlowers.cs:134` | 2 genes a blob every 6 s; its 4 leaves bring its upkeep to 0. · Sends no power on. | |
| `WildFlowers.cs:141` | Buffer | |
| `WildFlowers.cs:142` | Buff blobs give the parts they pass 50% more for 30 s. · Local only: 5 links (8 past an Amplifier). | |
| `WildFlowers.cs:146` | Comet Bloom | |
| `WildFlowers.cs:147` | 10 minutes of double-value gene and growth blobs, and buffs twice as long. · Then dormant, fading over 6 hours, and withers. | |
| `WildFlowers.cs:153` | Moonflower | |
| `WildFlowers.cs:154` | Buds nearby grow wild twice as often; wild flowers near it make 25% more. · Takes a slot. | |
| `WildFlowers.cs:159` | Nebula Bloom | |
| `WildFlowers.cs:160` | Power reaching a seedling within 5 links counts double. · Retired: no longer grows. | |

## Evolutions (names, effects, the choice menu)

Lines 74–91 are the effect labels the menu and Almanac put a number after ("Core power ×2", "Generator cap +2"). Twin Stars and Big Stem are retired and only seen in old gardens.

| Where | Text | New text |
|---|---|---|
| `Evolutions.cs:74` | Core power | |
| `Evolutions.cs:75` | Core link | |
| `Evolutions.cs:76` | core transfer | |
| `Evolutions.cs:77` | core store | |
| `Evolutions.cs:78` | Stem, Big Stem and Tendril transfer | |
| `Evolutions.cs:79` | their store | |
| `Evolutions.cs:80` | Stems hold +{StemFruits} leaf | |
| `Evolutions.cs:81` | Blobs always avoid dead ends | |
| `Evolutions.cs:82` | Blobs avoid dead ends {SmartChance}% of the time | |
| `Evolutions.cs:83` | Lab, Wild Lab and Lichen gene blobs' size | |
| `Evolutions.cs:84` | their upkeep | |
| `Evolutions.cs:85` | Generator cap | |
| `Evolutions.cs:86` | Every generator kind's power | |
| `Evolutions.cs:87` | Generator, Twin Star and Ember transfer | |
| `Evolutions.cs:88` | Generator transfer | |
| `Evolutions.cs:89` | Wild flower limit | |
| `Evolutions.cs:90` | Pulsar, Twin Star, Ember, Lumber Grove, Seed Pod, Wild Lab, Lichen and Buffer +{WildEffect}% | |
| `Evolutions.cs:91` | Buffs {BuffBonus}% of the output | |
| `Evolutions.cs:118` | Build your Core | |
| `Evolutions.cs:120` | Second Path | |
| `Evolutions.cs:122` | Long Stemmed | |
| `Evolutions.cs:124` | Smart Particles | |
| `Evolutions.cs:126` | Rapid Evolution | |
| `Evolutions.cs:128` | More Power | |
| `Evolutions.cs:130` | Pulsar Power | |
| `Evolutions.cs:132` | Twin Stars | |
| `Evolutions.cs:134` | Big Stem | |
| `Evolutions.cs:136` | Wild Bloom | |
| `Evolutions.cs:138` | Resonant Frequency | |
| `EvolutionMenu.cs:125` | Blooms into a/an {flower} | |
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
| `Almanac.cs:239` | At the planting · Found day {day}, {time} | |
| `Almanac.cs:242` | Parts | |
| `Almanac.cs:243` | Wild flowers | |
| `Almanac.cs:244` | Fruit | |
| `Almanac.cs:266` | None yet. Plant a seedling, fill it with power, and choose its evolution. | |
| `Almanac.cs:279` | Chosen day {day}, {time} | |
| `Almanac.cs:282` | Delete? | |
| `Almanac.cs:283` | Take this evolution back: its effect and caps go (its flower stays), and its seedling slot is free for a new seedling | |

## Journal and the away note

| Where | Text | New text |
|---|---|---|
| `Garden.cs:148` | A new garden was planted | |
| `Garden.cs:1527` | First passive bud started | |
| `Garden.cs:1684` | Evolution chosen: {evolution} | |
| `Garden.cs:1707` | {evolution} bloomed into a/an {flower} | |
| `Garden.cs:1763` | Evolution taken back: {evolution} | |
| `Garden.cs:1909` | Evolution chosen: {evolution} | |
| `Garden.cs:2001` | The Hub's levels were reset | |
| `Garden.cs:2063` | {Second/Third…} core link bought | |
| `Garden.cs:2064` | Hub bought | |
| `Garden.cs:2098` | {name} gained at garden size {size} | |
| `Tracking.cs:60` | Wild flower discovered: {Name} | |
| `Tracking.cs:62` | First Hub planted | |
| `Tracking.cs:63` | First {Name} emerged | |
| `Tracking.cs:66` | The garden reached {milestone} parts | |
| `Tracking.cs:77` | A/An {flower} burned out (an Ember) · A/An {flower} withered (a Comet Bloom) | |
| `Tracking.cs:317` | {evolution}, now a/an {flower} | |
| `Tracking.cs:319` | `if (bloomed.Count > 0) lines.Add($"{(bloomed.Count == 1 ? "A seedling" : $"{bloomed.Count} seedlings")} bloomed: {string.Join("; ", bloomed)}.");` | |
| `Tracking.cs:330` | `lines.Add($"{(parts == 1 ? "A new part" : $"{parts} new parts")} grew: {List(kinds)}.");` | |
| `Tracking.cs:333` | A fruit grew. · {fruit} fruits grew. | |
| `Tracking.cs:342` | Wild flowers grew: {names}. | |
| `Tracking.cs:347` | {Text} (see the Codex). | |
| `Tracking.cs:354` | An Ember burned out. · 2 Embers burned out. (withered, for the others) | |
| `Tracking.cs:359` | {genes} genes reached the core. | |
| `Tracking.cs:362` | `if (points > 0) lines.Add($"The Hub earned {(points == 1 ? "a point" : $"{points} points")} ({garden.HubPoints} to spend).");` | |
| `Tracking.cs:372` | {Count} more in the Almanac. | |
| `Tracking.cs:379` | Power ran low: down to {lowest}% of what it held when you left. | |
| `Tracking.cs:399` | (the "and" in lists: "8 stems and 4 groves") | |
| `Main.cs:392` | `Toast($"{string.Join(". ", gained)}. Place {(gained.Count == 1 ? "it" : "them")} in build mode.", ShapingLook.Tint);` | |
| `Main.cs:414` | While you were away ({time away}) | |
