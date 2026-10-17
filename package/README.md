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
| **Upgrade glow slider** | Gear upgraded past its normal max level glows, brighter with every extra level. Turn that glow down, off, or up to twice as strong. Only changes what you see. | Each player |
| **Skill loss and XP per skill** | Choose, skill by skill, how much is lost on death (0 = nothing, 1 = normal) and an XP multiplier (1 = normal, up to 10). Everything starts at the game's normal values. | Server |
| **Cancel an attack into a block** | Press block during a swing's windup or recovery to drop the attack and raise your guard at once, so you can parry mid-swing. A hit the swing hasn't reached never lands; stamina already spent stays spent. Cancelling never lets you attack faster. | Server |
| **Epic Loot bounty fixes** | *Only with Epic Loot installed.* Bounties complete when the main target dies (minions optional), new bounties spread out instead of piling onto the same spots, targets that stood still while the ground's pathing loaded now come at you, the main target drops one extra copy of each enchanted item and always at least one, and targets get a small overhead health bar. With StarLevelSystem, targets keep their extra health after reloading and scale with stars like normal creatures. | Server (health bar: each player) |
| **Game bug fixes** | Fast second bow shots at high Bows skill no longer vanish; swings from a ship's deck hit what's on the deck instead of the ship; a broken fuling shaman shield stops blocking arrows; plants no longer stop growing after a lag spike; long buried ore veins keep their support; overdue berry bushes and other pickables regrow as soon as someone is near. | Each game (keep the same everywhere) |
| **Quality of life** | Tombstone items go back to the slots they had when you died; no auto-equip when walking over weapons and tools; the hammer menu updates live as you move between stations and chests; health bars show while holding the hammer; combat health bars stay visible through mist, trees and buildings; taming progress shows from health-bar range; with Equipment & Quick Slots, V no longer switches auto-pickup off by accident; a tree-felling dust slider. | Each player |
| **Farming, swimming and stations** | Tamed animals don't trample crops; Swim skill raises swim speed; smelter-type stations hold twice as much (speed adjustable); fermenters give 1.5× the meads (speed adjustable); crafting station range and map reveal radius multipliers; carts and boats built by skilled crafters get more health. | Server |
| **Epic Loot enchantment fixes** | *Only with Epic Loot installed.* Riches gives treasure at its stated chance; Fire Rate speeds up bows, crossbows and staffs; Noise reduces noise by its percentage; +Ranged Skills raises Bows and Crossbows; Meads Cooldown works with Instant Mead; Indestructible armor stops wearing down; Bountiful Harvest works on other players' plants; Quick Draw no longer slows crossbow reloads; Air Jump isn't wasted just above the ground. | Each game (keep the same everywhere) |

## Requirements

- BepInExPack Valheim
- Jotunn

Install on the **server and every player's game**. Players without the mod can't join a server that has it.

Not compatible with EpicLoot Progression Fix, which already contains the camera, dropped-item, ship and cart, attack-cancel, bounty and bug-fix features. Potato Tweaks will not load next to it.

Epic Loot and StarLevelSystem are optional. Without Epic Loot the bounty fixes simply stay off.

Can replace the **Tomb Retriever** and **SkillLoss** mods. Neither is needed. If either is installed, Potato Tweaks leaves that feature to it and writes a warning in the log; remove it to use Potato Tweaks' version.

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
- **7 - Upgrade glow**: Enabled, Glow strength (1 = normal, 0 = off, 2 = twice as strong). Shown as a slider in the configuration manager. Personal. Glows added by other mods, such as Epic Loot enchantments, are separate and not changed.
- **8 - Attack cancel**: Enabled. Synced from the server.
- **9 - Epic Loot bounties** (only with Epic Loot): Complete on main target, Spread out new bounties, Spacing (300 m), Extra enchanted copies (1), Guaranteed enchanted item, Normal star scaling (StarLevelSystem), Overhead health bar (personal). The rest are synced from the server.
- **10 - Bug fixes**: Game bug fixes, Epic Loot enchantment fixes (only with Epic Loot). Read at startup, so restart after changing them, and keep them the same on the server and every player's game.
- **11 - Quality of life**: No auto-equip on pickup, Live hammer menu, Health bars with build tools, Combat health bars, Attacker health bar range (15 m), Taming info at range, Tombstone items back in their slots, Auto-pickup key with Equipment & Quick Slots, Tree-felling dust (slider, 1 = normal, 0 = none) are personal. Tamed animals spare crops and Swim skill raises swim speed are synced from the server.
- **12 - Stations, vehicles and map** (synced from the server): Smelter capacity (2×), Smelter speed (1×), Fermenter speed (1×), Fermenter output (1.5×), Crafting station range (1×), Map reveal radius (1×), Cart health from Crafting skill (4 = +400% at Crafting 100), Boat health from Crafting skill (1 = +100% at Crafting 100). Station and fermenter values apply as those stations load.

The skill settings start at the game's normal values: normal death loss and 1× XP for every skill. Change them on the server; players always use the server's values.

**Only if you are switching from the SkillLoss mod:** on first launch, Potato Tweaks copies your old values from `Mirfin.SkillLoss.cfg` when that file is in the config folder. Without that file nothing is copied and the normal values are used.

If a Valheim update changes the game code a feature relies on, that feature switches itself off and writes the reason to the BepInEx log. The other features keep working.

## Credits

Camera zoom, dropped-item lifetime, ship/cart removal, attack cancelling, the Epic Loot bounty and enchantment fixes, the game bug fixes and most quality-of-life features come from **EpicLoot Progression Fix** by the EpicLoot Progression Fix contributors, used under the MIT License. Tombstone retrieval, the skill settings, the upgrade glow slider, the tree dust slider, the station, fermenter, range and map settings and boat health were written for Potato Tweaks. See `LICENSE`.
