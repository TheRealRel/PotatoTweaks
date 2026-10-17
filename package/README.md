# 🥔 Potato Tweaks

**These tweaks are for my Potato Server.** It consolidates, fixes and adds things I wanted.

> 📜 **Updating?** See the **Changelog** tab to determine if you need to update or not.

🛠️ Source code and bug reports: [github.com/TheRealRel/PotatoTweaks](https://github.com/TheRealRel/PotatoTweaks)

---

## Features

Each one can be switched off on its own.

| Feature | What it does | Who sets it |
|---|---|---|
| **Farther camera zoom** | Zoom out to 12 m on land (game: 8 m) and 24 m on a ship. | Each player |
| **Dropped items last 3 hours** | Dropped items last 3 hours of world time instead of 1, so loot dropped before a night's sleep survives it. Items in a base, near a player, in tar or part of a build still never disappear. | Server |
| **Take apart ships and carts** | Hold **Ctrl** and use the hammer's remove button (middle mouse) on a raft, karve, longship, drakkar or cart. Every material is refunded and cargo drops in crates. Normal rules still apply: workbench in range, no ward or no-build zone, nobody aboard a raft. Battering rams and catapults stay protected. | Server |
| **Tombstone retrieval** | Type `retrievetomb` (or `gettomb`) in the F5 console, or `/retrievetomb` in chat, to bring your own tombstones to where you stand. They stay put after the move. | Server |
| **Visual effects** | Sliders for the glow on gear upgraded past its max level (off to twice as strong), the dust from felling trees, and the particles that cover creatures in a fight (fainter, darker or smaller, so you can see their attacks coming). Only changes what you see. | Each player |
| **Skill loss and XP per skill** | Choose, skill by skill, how much is lost on death (0 = nothing, 1 = normal) and an XP multiplier (1 = normal, up to 10). Everything starts at the game's normal values. | Server |
| **Cancel an attack into a block** | Press block during a swing's windup or recovery to drop the attack and raise your guard at once, so you can parry mid-swing. A hit the swing hasn't reached never lands; stamina already spent stays spent. Cancelling never lets you attack faster. | Server |
| **Epic Loot bounty fixes** | *Only with Epic Loot installed.* Bounties complete when the main target dies (minions optional), new bounties spread out instead of piling onto the same spots, targets that stood still while the ground's pathing loaded now come at you, the main target drops one extra copy of each enchanted item and always at least one, and targets get a small overhead health bar. With StarLevelSystem, targets keep their extra health after reloading and scale with stars like normal creatures. | Server (health bar: each player) |
| **Game bug fixes** | Fast second bow shots at high Bows skill no longer vanish; swings from a ship's deck hit what's on the deck instead of the ship; a broken fuling shaman shield stops blocking arrows; plants no longer stop growing after a lag spike; long buried ore veins keep their support; overdue berry bushes and other pickables regrow as soon as someone is near. | Each game (keep the same everywhere) |
| **Quality of life** | Tombstone items go back to the slots they had when you died; no auto-equip when walking over weapons and tools; the hammer menu updates live as you move between stations and chests; health bars show while holding the hammer; combat health bars stay visible through mist, trees and buildings; taming progress shows from health-bar range; with Equipment & Quick Slots, V no longer switches auto-pickup off by accident. | Each player |
| **Farming, swimming and stations** | Tamed animals don't trample crops; Swim skill raises swim speed. Carts and boats get more health the higher their builder's Crafting skill. Optional server settings, normal by default: smelter, kiln and furnace capacity, speed, fuel use and output; fermenter speed and output; crafting station range; map reveal radius. | Server |
| **Double dragon bed** | Each half of the dragon bed is its own bed: two players can claim it, sleep side by side and respawn on their own half. One player owning both halves sleeps in the middle. Blankets roll over the sleepers. | Everyone (blankets: each player) |
| **Trophy pity** | Every kill without a trophy makes that creature's next trophy more likely, until it drops. The Trophies tab shows each trophy's next chance and, spoiler-free, the trophies you haven't collected yet. | Server (display: each player) |
| **Epic Loot enchantment fixes** | *Only with Epic Loot installed.* Riches gives treasure at its stated chance; Fire Rate speeds up bows, crossbows and staffs; Noise reduces noise by its percentage; +Ranged Skills raises Bows and Crossbows; Meads Cooldown works with Instant Mead; Indestructible armor stops wearing down; Bountiful Harvest works on other players' plants; Quick Draw no longer slows crossbow reloads; Air Jump isn't wasted just above the ground. | Each game (keep the same everywhere) |

## Requirements

- BepInExPack Valheim
- Jotunn

Install on the **server and every player's game**. Players without the mod can't join a server that has it.

Not compatible with EpicLoot Progression Fix, which already contains the camera, dropped-item, ship and cart, attack-cancel, bounty and bug-fix features. Potato Tweaks will not load next to it.

Epic Loot and StarLevelSystem are optional. Without Epic Loot the bounty fixes simply stay off.

Can replace the **Tomb Retriever**, **SkillLoss**, **Double Dragon Bed**, **Dimmer Combat Particle Effects** and **TrophyAssister** mods. None of them is needed. If one is installed, Potato Tweaks leaves that feature to it and writes a warning in the log; remove it to use Potato Tweaks' version. When switching, Potato Tweaks copies the old mod's settings (and TrophyAssister's pity counts) on first launch if their files are still in the config folder.

## Installation

**r2modman / Thunderstore Mod Manager / Gale**
- Install Potato Tweaks from the mod list. Jotunn and BepInEx are installed with it.

**Manual**
1. Install BepInExPack Valheim and Jotunn.
2. Copy `plugins/PotatoTweaks.dll` from the download into `BepInEx/plugins/PotatoTweaks/`.

**Dedicated server**
- Copy `PotatoTweaks.dll` into the server's `BepInEx/plugins/PotatoTweaks/` and restart the server.

## Configuration

`BepInEx/config/potato.tweaks.cfg`, created on first launch. Also editable in game with a configuration manager (F1).

- **1 - Camera**: Enabled, Land zoom multiplier (1.5), Ship zoom distance (24 m). Personal.
- **2 - Dropped items**: Enabled, Lifetime hours (3). Synced from the server.
- **3 - Ship and cart removal**: Enabled. Synced from the server.
- **4 - Tombstone retrieval**: Enabled, Cooldown seconds (10). Synced from the server.
- **5 - Skill loss on death**: Enabled, one value per skill, plus *Other skills* for skills added by other mods. Synced from the server.
- **6 - Skill XP**: Enabled, one multiplier per skill, plus *Other skills*. Synced from the server.
- **7 - Visual effects** (personal): Upgrade glow + Upgrade glow strength (1 = normal, 0 = off, 2 = twice as strong); Tree-felling dust (1 = normal, 0 = none); Combat effects dimmer with opacity, brightness and size (all 1 = normal) and which sources to dim (status effects on creatures, creature hit bursts, weapon impacts, projectile and area impacts, optionally effects on players).
- **8 - Attack cancel**: Enabled. Synced from the server.
- **9 - Epic Loot bounties** (only with Epic Loot): Complete on main target, Spread out new bounties, Spacing (300 m), Extra enchanted copies (1), Guaranteed enchanted item, Normal star scaling (StarLevelSystem), Overhead health bar (personal). The rest are synced from the server.
- **10 - Bug fixes**: Game bug fixes, Epic Loot enchantment fixes (only with Epic Loot). Read at startup, so restart after changing them, and keep them the same on the server and every player's game.
- **11 - Quality of life**: No auto-equip on pickup, Live hammer menu, Health bars with build tools, Combat health bars, Attacker health bar range (15 m), Taming info at range, Tombstone items back in their slots, Auto-pickup key with Equipment & Quick Slots are personal. Tamed animals spare crops and Swim skill raises swim speed are synced from the server.
- **12 - Smelters and kilns** (synced; all 1 = the game's normal): Ore capacity, Fuel capacity, Speed, Fuel per product, Output, Excluded stations. Covers the smelter, blast furnace, charcoal kiln, eitr refinery, spinning wheel and windmill; applies as they load.
- **13 - Fermenters** (synced; 1 = normal): Speed, Output. Applies as fermenters load.
- **14 - Crafting stations** (synced): Build range (1 = normal).
- **15 - Carts and boats** (synced): Cart health from Crafting skill (4 = up to +400% at Crafting 100), Boat health from Crafting skill (1 = up to +100%). Scales with the builder's skill; 0 = off.
- **16 - Map** (synced): Reveal radius (1 = normal, 100 m).
- **17 - Double dragon bed**: Enabled (read at startup; keep it the same on the server and every player's game), Blankets (personal).
- **18 - Trophies**: Pity, Flat growth, Growth per dry kill (0.1), Rare trophy threshold (5%), Rare growth (0.2), Flat bonus, Guarantee after dry kills (off), Extra dry kills per star (1), Chance multiplier (1), Only count my kills, Only uncollected trophies are synced from the server. Show chances, Show missing trophies, Spoiler-free, Hidden trophies, Message on drop, Message on dry kill are personal. Pity counts are kept per character in `BepInEx/config/potato.tweaks.trophies.txt`.

The skill settings start at the game's normal values: normal death loss and 1× XP for every skill. Change them on the server; players always use the server's values.

**Only if you are switching from the SkillLoss mod:** on first launch, Potato Tweaks copies your old values from `Mirfin.SkillLoss.cfg` when that file is in the config folder. Without that file nothing is copied and the normal values are used.

If a Valheim update changes the game code a feature relies on, that feature switches itself off and writes the reason to the BepInEx log. The other features keep working.

## Credits

Camera zoom, dropped-item lifetime, ship/cart removal, attack cancelling, the Epic Loot bounty and enchantment fixes, the game bug fixes and most quality-of-life features come from **EpicLoot Progression Fix** by the EpicLoot Progression Fix contributors, used under the MIT License. The double dragon bed comes from **Double Dragon Bed** by Hansenbansen, used under the MIT License. Tombstone retrieval, the skill settings, the visual effect sliders and combat effects dimmer, trophy pity, the station, fermenter, range and map settings and boat health were written for Potato Tweaks. See `LICENSE`.
