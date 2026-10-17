# 🥔 Potato Tweaks

**These tweaks are for my Potato Server.** It consolidates, fixes and adds things I wanted.

> 📜 **Updating?** See the **Changelog** tab to determine if you need to update or not.

🛠️ Source code and bug reports: [github.com/TheRealRel/PotatoTweaks](https://github.com/TheRealRel/PotatoTweaks)

💰 **Trader sell prices:** what the Potato Server's traders pay for every item, materials to Epic Loot gear: [open the searchable list](https://github.com/TheRealRel/PotatoTweaks/blob/main/docs/sell-prices.csv "A table of every item the Potato Server's traders buy and the coins they pay. Type in the search box to find an item.")

🗂️ Running **Traders Extended** on your own server? [Download the same prices as a ready-made file](https://github.com/TheRealRel/PotatoTweaks/blob/main/docs/common.sell.csv "common.sell.csv for the Traders Extended mod: use the download button at the top right, put the file in BepInEx/config/shudnal.TradersExtended/ on your server and restart. Every trader then buys items at these prices; items from mods you don't have are skipped.") and put it in `BepInEx/config/shudnal.TradersExtended/` on your server.

---

## Features

Every feature can be switched off on its own. **Server** means the server's settings apply to everyone (players can't change them); **Player** means each player chooses for themselves.

### Playing

| Feature | What it does | Set by |
|---|---|---|
| **Farther camera zoom** | Zoom out to 12 m on land (game: 8 m) and 24 m on a ship. | Player |
| **Dropped items last 3 hours** | Dropped items last 3 hours of world time instead of 1, so loot dropped before a night's sleep survives it. Items in a base, near a player, in tar or part of a build still never disappear. | Server |
| **Take apart ships and carts** | Hold **Ctrl** and use the hammer's remove button (middle mouse) on a raft, karve, longship, drakkar or cart. Every material is refunded and cargo drops in crates. Normal rules still apply: workbench in range, no ward or no-build zone, nobody aboard a raft. | Server |
| **Tombstone retrieval** | Type `retrievetomb` (or `gettomb`) in the F5 console, or `/retrievetomb` in chat, to bring your own tombstones to where you stand. They stay put after the move. | Server |
| **Cancel an attack into a block** | Press block during a swing's windup or recovery to drop the attack and raise your guard at once. A hit the swing hasn't reached never lands; stamina already spent stays spent; you can't attack faster this way. | Server |
| **Spearfishing** | Throw a spear or shoot an arrow or bolt at a fish in the water to catch it: it floats up to be picked up. A harpoon pulls it straight into your inventory. Modded spears, bows and crossbows work too. | Server |
| **Double dragon bed** | Each half of the dragon bed is its own bed: two players can claim it, sleep side by side and respawn on their own half. Blankets roll over the sleepers. | Server and every player alike (blankets: Player) |
| **Trophy pity** | Every kill without a trophy makes that creature's next trophy more likely, until it drops. The Trophies tab shows each trophy's next chance and, spoiler-free, the trophies you haven't collected yet. | Server (Trophies tab: Player) |
| **Quality of life** | Tombstone items go back to the slots they had when you died; no auto-equip when walking over weapons and tools; the hammer menu updates live; health bars show while holding the hammer and stay visible through mist, trees and buildings in a fight; taming progress shows from health-bar range; with Equipment & Quick Slots, V no longer switches auto-pickup off by accident. | Player |
| **Farming and swimming** | Tamed animals don't trample crops; the Swim skill raises swim speed. | Server |

### World and economy

All of these start at the game's normal values, except cart and boat health and the inventory rows.

| Feature | What it does | Set by |
|---|---|---|
| **Skill loss and XP** | How much of each skill is lost on death (0 = nothing, 1 = normal) and an XP multiplier per skill (up to 10). | Server |
| **Loot and items** | Multipliers for creature, resource and pickable drops (trophies only if you want); item weight; stack size; auto-pickup range; items that float in water. Weight and stack size apply to every copy of an item, crafted, carried or dropped. | Server |
| **Smelters, fermenters and stations** | Smelter, kiln and furnace capacity, speed, fuel use and output; fermenter speed and output; crafting station build range. | Server |
| **Carts and boats** | Carts and boats get more health the higher their builder's Crafting skill: carts up to +400%, boats up to +100%. | Server |
| **Map** | How far around you the map is revealed as you explore. | Server |
| **Inventory rows** | Haldor's two inventory rows unlock after the Elder and Bonemass instead of Moder and the Queen, and he sells a third, Roomier pockets, after Yagluth for 3,000 coins. Each is bought once per character. | Server |

### Looks

| Feature | What it does | Set by |
|---|---|---|
| **Visual effects** | Sliders for the glow on gear upgraded past its max level, the dust from felling trees, and the particles that cover creatures in a fight (fainter, darker or smaller, so you can see attacks coming). With Epic Loot: hide the Glowing aura, all enchantment effects or legendary textures. Only changes what you see. | Player |

### Fixes

| Feature | What it does | Set by |
|---|---|---|
| **Game bug fixes** | Fast second bow shots no longer vanish; swings from a ship's deck hit what's on the deck; a broken fuling shaman shield stops blocking arrows; plants no longer stop growing after a lag spike; long buried ore veins keep their support; overdue berry bushes regrow as soon as someone is near. | Every game alike (restart after changing) |

### Epic Loot

*Only with Epic Loot installed; without it these simply stay off.*

| Feature | What it does | Set by |
|---|---|---|
| **Bounty fixes** | Bounties complete when the main target dies (minions optional); new bounties spread out instead of piling onto the same spots; targets that stood still while the ground's pathing loaded now come at you; the main target drops one extra copy of each enchanted item and always at least one; targets get a small overhead health bar. With StarLevelSystem, targets keep their extra health after reloading and scale with stars like normal creatures. | Server (health bar: Player) |
| **Enchantment fixes** | Riches gives treasure at its stated chance; Fire Rate speeds up bows, crossbows and staffs; Noise reduces noise by its percentage; +Ranged Skills raises Bows and Crossbows; Meads Cooldown works with Instant Mead; Indestructible armor stops wearing down; Bountiful Harvest works on other players' plants; Quick Draw no longer slows crossbow reloads; Air Jump isn't wasted just above the ground. | Every game alike (restart after changing) |
| **Enchanting table** | Takes its costs from built chests within 50 m that you can open, and the Sacrifice tab can list trophies and shardstones from them (checkboxes in the tab). The double-output chance is rolled per sacrificed item instead of all or nothing. | Server (chest lists: Player) |
| **Enchantments and shards** | Damage enchantments can roll together on one weapon; Double Magic Shot and Triple Shot cost the normal eitr or stamina, and Double Magic Shot doubles a summoning staff's limit; shardstones can be socketed even when their effect clashes with one the item has; Lucky Loot works on birds. | Server |
| **Messages and tooltips** | A message when Lucky Loot triggers near you; enchanted staffs show their eitr cost; Epic Loot no longer goes through every chest's items each second when nothing changed. | Player |

## Requirements

- BepInExPack Valheim
- Jotunn

Install on the **server and every player's game**. Players without the mod can't join a server that has it.

Not compatible with EpicLoot Progression Fix, which already contains the camera, dropped-item, ship and cart, attack-cancel, bounty and bug-fix features. Potato Tweaks will not load next to it.

Epic Loot and StarLevelSystem are optional. Without Epic Loot the Epic Loot features simply stay off.

Can replace the **Tomb Retriever**, **SkillLoss**, **Double Dragon Bed**, **Dimmer Combat Particle Effects**, **TrophyAssister**, **Spearfishing**, **Epic Loot Add-ons** and **DropMoreLoot** mods. None of them is needed. If one is installed, Potato Tweaks leaves that feature to it and writes a warning in the log; remove it to use Potato Tweaks' version. When switching, Potato Tweaks copies the old mod's settings (and TrophyAssister's pity counts) on first launch if their files are still in the config folder.

## Installation

**r2modman / Thunderstore Mod Manager / Gale**
- Install Potato Tweaks from the mod list. Jotunn and BepInEx are installed with it.

**Manual**
1. Install BepInExPack Valheim and Jotunn.
2. Copy `plugins/PotatoTweaks.dll` from the download into `BepInEx/plugins/PotatoTweaks/`.

**Dedicated server**
- Copy `PotatoTweaks.dll` into the server's `BepInEx/plugins/PotatoTweaks/` and restart the server.

## Configuration

`BepInEx/config/potato.tweaks.cfg`, created on first launch. Also editable in game with a configuration manager (F1). Server settings are marked *[Synced with Server]*: set them in the server's file, and every player gets them on joining.

| Section | Settings | Set by |
|---|---|---|
| **1 - Camera** | Enabled, Land zoom multiplier (1.5), Ship zoom distance (24 m) | Player |
| **2 - Dropped items** | Enabled, Lifetime hours (3) | Server |
| **3 - Ship and cart removal** | Enabled | Server |
| **4 - Tombstone retrieval** | Enabled, Cooldown seconds (10) | Server |
| **5 - Skill loss on death** | Enabled, one value per skill, *Other skills* for modded skills | Server |
| **6 - Skill XP** | Enabled, one multiplier per skill, *Other skills* | Server |
| **7 - Visual effects** | Upgrade glow and its strength; Tree-felling dust; Combat effects dimmer (opacity, brightness, size) and what to dim; with Epic Loot: Hide Glowing enchantment, Hide all enchantment effects, Hide legendary looks | Player |
| **8 - Attack cancel** | Enabled | Server |
| **9 - Epic Loot bounties** | Complete on main target, Spread out new bounties, Spacing (300 m), Extra enchanted copies (1), Guaranteed enchanted item, Normal star scaling | Server |
| | Overhead health bar | Player |
| **10 - Bug fixes** | Game bug fixes; Epic Loot enchantment fixes (read at startup; keep them the same everywhere) | Every game alike |
| | Epic Loot faster chest loading | Player |
| **11 - Quality of life** | No auto-equip on pickup, Live hammer menu, Health bars with build tools, Combat health bars, Attacker health bar range (15 m), Taming info at range, Tombstone items back in their slots, Auto-pickup key with Equipment & Quick Slots | Player |
| | Tamed animals spare crops, Swim skill raises swim speed | Server |
| **12 - Smelters and kilns** | Ore capacity, Fuel capacity, Speed, Fuel per product, Output (all 1 = normal), Excluded stations | Server |
| **13 - Fermenters** | Speed, Output (1 = normal) | Server |
| **14 - Crafting stations** | Build range (1 = normal) | Server |
| **15 - Carts and boats** | Cart health from Crafting skill (4 = up to +400%), Boat health from Crafting skill (1 = up to +100%); 0 = off | Server |
| **16 - Map** | Reveal radius (1 = normal, 100 m) | Server |
| **17 - Double dragon bed** | Enabled (read at startup; keep it the same everywhere) | Every game alike |
| | Blankets | Player |
| **18 - Trophies** | Pity, Flat growth, Growth per dry kill, Rare trophy threshold and growth, Flat bonus, Guarantee after dry kills, Extra dry kills per star, Chance multiplier, Only count my kills, Only uncollected trophies | Server |
| | Show chances, Show missing trophies, Spoiler-free, Hidden trophies, Message on drop, Message on dry kill | Player |
| **19 - Spearfishing** | Spears, Harpoons, Bows, Crossbows | Server |
| **20 - Epic Loot enchanting table** | Materials from nearby chests, Chest range (50 m), Double output per item | Server |
| | Chest trophies in Sacrifice (on), Chest shardstones in Sacrifice (off) | Player |
| **21 - Epic Loot enchantments** | Damage enchantments stack, Double and Triple Shot normal cost, Double Magic Shot doubles the summon limit, Shardstones ignore effect clashes, Lucky Loot on birds | Server |
| | Lucky Loot message and its position, Eitr cost in tooltips | Player |
| **22 - Loot and items** | Creature drops, Resource drops, Pickable drops (1 = normal), Multiply trophies too, Never multiplied, Only multiply these, Item weight (1 = normal), Stack size (1 = normal), Auto-pickup range (2 m), Items float in water | Server |
| **23 - Inventory rows** | Earlier rows from Haldor, Third row from Haldor, Third row price (3,000), Third row unlock (Yagluth) | Server |

Trophy pity counts are kept per character in `BepInEx/config/potato.tweaks.trophies.txt`.

The skill settings start at the game's normal values: normal death loss and 1× XP for every skill. Change them on the server; players always use the server's values.

**Only if you are switching from the SkillLoss mod:** on first launch, Potato Tweaks copies your old values from `Mirfin.SkillLoss.cfg` when that file is in the config folder. Without that file nothing is copied and the normal values are used.

If a Valheim update changes the game code a feature relies on, that feature switches itself off and writes the reason to the BepInEx log. The other features keep working.

## Credits

Camera zoom, dropped-item lifetime, ship/cart removal, attack cancelling, the Epic Loot bounty and enchantment fixes, the game bug fixes and most quality-of-life features come from **EpicLoot Progression Fix** by the EpicLoot Progression Fix contributors, used under the MIT License. The double dragon bed comes from **Double Dragon Bed** by Hansenbansen and spearfishing from **Spearfishing** by karkkant, both used under the MIT License. The Epic Loot enchanting table and enchantment add-ons were written for Potato Tweaks to replace DigiTeam's Epic Loot Add-ons, and the loot and item settings to replace TastyChickenLegs' DropMoreLoot. Tombstone retrieval, the skill settings, the visual effect sliders and combat effects dimmer, trophy pity, the station, fermenter, range and map settings and boat health were written for Potato Tweaks. See `LICENSE`.
