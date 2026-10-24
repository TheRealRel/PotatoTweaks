Potato Tweaks is a collection of Valheim tweaks, fixes and additions made for the Potato Server. It goes on the server and in every player's game. Every feature can be switched off on its own. Server settings come from the server and are the same for everyone; personal settings are each player's own.

## Around the base

![](https://raw.githubusercontent.com/TheRealRel/PotatoTweaks/main/docs/images/icon-chest.png) ![](https://raw.githubusercontent.com/TheRealRel/PotatoTweaks/main/docs/images/icon-door.png) ![](https://raw.githubusercontent.com/TheRealRel/PotatoTweaks/main/docs/images/icon-house.png)

**Chests.** Look at a chest and press Ctrl + N to name it, and everyone sees the name. Looking at a chest also shows a row of small icons of what's inside, so you can find things without opening every box.

![A named chest and its contents on hover](https://raw.githubusercontent.com/TheRealRel/PotatoTweaks/main/docs/images/chest.jpg)

**Doors.** Doors you open close themselves 5 seconds later, once the doorway is clear.

**Serving tray.** Every crafted food can be set out on the serving tray, from the game and from food mods like OdinHorse and Wizardry, and eaten from it like the game's own dishes.

**Item stands.** Any item goes on an item stand. The `itemstand_scale`, `itemstand_rotation` and `itemstand_offset` console commands resize, turn or move what's on it.

**Comfort tab.** A tab in the build menu lists every comfort piece, ticks the ones already near you and says how much each would add.

**Sleeping.** The night is skipped once half the players are in bed or sitting, as long as at least one is in bed. Each half of the dragon bed is its own bed, so two players can sleep side by side.

**Wisplight.** Upgrade it at the workbench up to level 5. Each level clears more mist around you.

**Smoke.** Smoke drifts up through cage floors and walls, iron gates, the darkwood divider, stairs and ladders instead of pooling under them, and a fire under one of them no longer counts as covered. The list of pieces is a setting.

## Crafting and stations

![](https://raw.githubusercontent.com/TheRealRel/PotatoTweaks/main/docs/images/icon-anvil.png) ![](https://raw.githubusercontent.com/TheRealRel/PotatoTweaks/main/docs/images/icon-smelter.png)

**Station timers.** Smelters, kilns, furnaces, spinning wheels, windmills, fermenters and cooking stations show three timers when you look at them:

- until the next item is done
- until everything inside is done
- until the fuel runs out

![A smelter's timers on hover](https://raw.githubusercontent.com/TheRealRel/PotatoTweaks/main/docs/images/smelter.jpg)

**Recycling.** With a crafting station open, Ctrl + right-click an item to take it apart. You get half its materials back, upgrades included.

**Station settings.** Smelters, kilns and furnaces have settings for capacity, speed, fuel and output. Windmills mill twice as fast, fermenters have speed and output settings, and the server can widen crafting stations' build range.

**Carts and boats.** They get more health the higher their builder's Crafting skill: up to +400% for carts and +100% for boats at Crafting 100.

## Getting around

![](https://raw.githubusercontent.com/TheRealRel/PotatoTweaks/main/docs/images/icon-map.png)

**Camera.** Zooms out farther: 12 m on land instead of 8, and 24 m on a ship.

**Dropped items.** They last 3 hours of world time instead of 1, so loot dropped before a night's sleep is still there.

**Taking apart ships and carts.** Hold Ctrl and use the hammer's remove button on a raft, karve, longship, drakkar or cart. Every material is refunded, and its cargo drops in crates.

**Tombstones.** Type `retrievetomb` in the F5 console (or `/retrievetomb` in chat) to bring your own tombstones to you. Their items go back to the slots they came from.

**Map.** A button by the skull on the large map clears every death marker at once. While you or someone within 20 m is steering a ship, the map reveals 500 m around you instead of 100 m, and the server can widen the normal radius too.

## Fighting and hunting

![](https://raw.githubusercontent.com/TheRealRel/PotatoTweaks/main/docs/images/icon-shield.png)

**Attack cancel.** Press block during a swing's windup or recovery to cancel it and raise your guard at once.

**Trophies.** Every kill without a trophy makes the next one from that creature more likely, until it drops. The Trophies tab shows the chances and, without spoilers, which trophies you haven't collected.

**Spearfishing.** Spears, arrows and bolts catch fish, and a harpoon pulls them straight into your inventory.

**Shared essences.** Thunderblood and Frostfire essence drop for every player who was nearby.

## Skills, loot and trading

![](https://raw.githubusercontent.com/TheRealRel/PotatoTweaks/main/docs/images/icon-pouch.png) ![](https://raw.githubusercontent.com/TheRealRel/PotatoTweaks/main/docs/images/icon-coins.png)

**Skills.** The server sets how much skill you lose on death and how fast each skill gains XP. The Swim and Sneak skills also raise your swimming and sneaking speed.

**Loot and items.** Multipliers for drops from creatures, resources and pickables, and for item weight, stack size and auto-pickup range.

**Inventory rows.** Haldor sells extra inventory rows for 1,000, 2,000 and 3,000 coins: the first after the Elder, the second after Bonemass, the third after Yagluth.

**Trader prices.** The Potato Server's traders pay [these prices](https://github.com/TheRealRel/PotatoTweaks/blob/main/docs/sell-prices.csv) for every item. Running Traders Extended on your own server? [The same prices as a ready-made file](https://github.com/TheRealRel/PotatoTweaks/blob/main/docs/common.sell.csv) go in `BepInEx/config/shudnal.TradersExtended/`.

## Comforts, looks and fixes

![](https://raw.githubusercontent.com/TheRealRel/PotatoTweaks/main/docs/images/icon-fire.png) ![](https://raw.githubusercontent.com/TheRealRel/PotatoTweaks/main/docs/images/icon-mirror.png) ![](https://raw.githubusercontent.com/TheRealRel/PotatoTweaks/main/docs/images/icon-hammer.png)

**Small comforts.** Walking over a weapon no longer equips it, the hammer menu updates live, and health bars show while you hold the hammer and through mist and trees in a fight. Inventory windows move aside when mods' extra slots would end up behind them or off screen, at any resolution. Status effect icons drop their cut-off names, and tamed animals don't trample crops.

**Looks.** Sliders for the glow on over-upgraded gear, the dust from felling trees and the particles that cover creatures in a fight. These only change what you see.

**Game fixes.**

- Fast second bow shots no longer vanish.
- Swings from a ship's deck hit what's on the deck.
- A broken fuling shaman shield stops blocking arrows.
- Plants keep growing after a lag spike.
- Buried ore veins keep their support.
- Overdue berry bushes regrow when someone comes near.
- With Quick Stack - Store - Sort - Trash - Restock, sorting and storing leave Equipment and Quick Slots' slots alone.

## With Epic Loot

![](https://raw.githubusercontent.com/TheRealRel/PotatoTweaks/main/docs/images/icon-gem.png)

These switch on only when Epic Loot is installed.

**Bounties.** They complete when the main target dies and spread out across the map. The target gets a small health bar and always drops at least one enchanted item.

**Enchantments.** Riches, Fire Rate, Noise, +Ranged Skills, Meads Cooldown, Indestructible, Bountiful Harvest, Quick Draw and Air Jump work as described. Damage enchantments can roll together, and Lucky Loot works on birds.

**Enchanting table.** It takes its materials from chests within 50 m.

**Looks.** The Glowing aura, enchantment effects and legendary textures can be hidden.

## Settings

In `BepInEx/config/potato.tweaks.cfg`, or in game with Configuration Manager (F1). Server settings say *[Synced with Server]*: set them on the server, and every player gets them on joining. Values in brackets are the defaults.

| Section | What you can set | Set by |
|---|---|---|
| 1 - Camera | Zoom on land (1.5x, 12 m) and on a ship (24 m) | Personal |
| 2 - Dropped items | How long they last (3 hours) | Server |
| 3 - Ship and cart removal | On or off | Server |
| 4 - Tombstone retrieval | On or off, time between uses (10 s) | Server |
| 5 - Skill loss on death | Loss per skill (1 = the game's normal) | Server |
| 6 - Skill XP | XP per skill (1 = normal, up to 10) | Server |
| 7 - Visual effects | Upgrade glow, tree dust, combat particles; Epic Loot looks | Personal |
| 8 - Attack cancel | On or off | Server |
| 9 - Epic Loot bounties | Main kill only, spacing (300 m), extra copies (1), scaling | Server; health bar personal |
| 10 - Bug fixes | Game and Epic Loot fixes (restart after changing); tidying other mods' config files | Every game alike; tidying personal |
| 11 - Quality of life | Each comfort above, swim and sneak speed, crops | Personal; speeds and crops server |
| 12 - Smelters and kilns | Capacity, speed, fuel, output, stations left alone; windmills (2x speed) | Server |
| 13 - Fermenters | Speed and output (1 = normal) | Server |
| 14 - Crafting stations | Build range (1 = normal) | Server |
| 15 - Carts and boats | Health from Crafting skill (carts +400%, boats +100%) | Server |
| 16 - Map | Reveal radius (1 = normal), while sailing (500 m); death marker button | Server; button personal |
| 17 - Double dragon bed | On or off (restart after changing); blankets | Every game alike; blankets personal |
| 18 - Trophies | Pity growth (+10%, rare +20%), guarantees, star bonus | Server; Trophies tab personal |
| 19 - Spearfishing | Spears, harpoons, bows, crossbows | Server |
| 20 - Epic Loot enchanting table | Chests within 50 m, double output per item | Server; Sacrifice tab lists personal |
| 21 - Epic Loot enchantments | Stacking, shot costs, shard clashes, Lucky Loot on birds | Server; messages personal |
| 22 - Loot and items | Drop multipliers, weight, stack size, pickup range (2 m), floating, essences | Server |
| 23 - Inventory rows | Bosses and prices (1,000, 2,000, 3,000 coins) | Server |
| 24 - Sleeping | Share of players (50%), who counts, players in bed (1) | Server |
| 25 - Item stands | Allowed items and stands, adjust limits (2 m, 5x) | Server; hiding personal |
| 26 - Wisplight | Levels (5), clearing per level (5 m), costs | Server |
| 27 - Comfort | Tab, check marks, descriptions, resting message | Personal |
| 28 - Recycling | Materials back (50%), upgrades count | Server; key (Left Ctrl) personal |
| 29 - Serving tray | Every crafted food (next game start) | Server |
| 30 - Chests | Chest names | Server |
| | Name key (Ctrl + N), show contents, icon size (24 px), most icons (8) | Personal |
| 31 - Doors | Doors close themselves, seconds open (5) | Server |
| 32 - Station timers | Show timers | Personal |
| 33 - Smoke | Pieces smoke passes through | Server |

Trophy pity counts are kept per character in `BepInEx/config/potato.tweaks.trophies.txt`. If a Valheim update breaks something a feature relies on, that feature switches itself off and writes why in the BepInEx log; the rest keep working.

**Replacing other mods.** Potato Tweaks covers Tomb Retriever, SkillLoss, Double Dragon Bed, Dimmer Combat Particle Effects, TrophyAssister, Spearfishing, Epic Loot Add-ons, DropMoreLoot, EssenceForAll, ComfortHelper, Smoke Collision and BiggerBoatMapExploreRadius. If one of them is installed, Potato Tweaks leaves that feature to it and says so in the log. When you switch, their old settings (and TrophyAssister's pity counts) are copied over on first launch. It won't load next to EpicLoot Progression Fix, which it already contains.

**Credits.** Camera zoom, dropped items, ship and cart removal, attack cancelling, the Epic Loot bounty and enchantment fixes, the game fixes and most of the small comforts come from EpicLoot Progression Fix by its contributors (MIT). The double dragon bed comes from Double Dragon Bed by Hansenbansen and spearfishing from Spearfishing by karkkant (both MIT). Item stands and smoke passing through come from Item Stand All Items and Smoke Collision by JereKuusela (public domain). The bigger map reveal while sailing comes from BiggerBoatMapExploreRadius by nearbear (MIT). Essence for everyone and the comfort helper come from EssenceForAll and ComfortHelper by Koen-H (MIT). The rest was written for Potato Tweaks, some of it on ideas from RandyKnapp's Minimal Status Effects, blacks7ar's SNEAKer and Digitalroot's Better Wisps.

Bug reports go to [GitHub Issues](https://github.com/TheRealRel/PotatoTweaks/issues), where the source lives too. Questions are welcome on Discord: [relanebeis](https://discord.com/users/373574370267889674). The Changelog tab says whether everyone needs to update.

Needs [BepInExPack Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/) and [Jötunn](https://thunderstore.io/c/valheim/p/ValheimModding/Jotunn/). Epic Loot and StarLevelSystem are optional. Players without Potato Tweaks can't join a server that has it.

The code is under the MIT License: use it, change it and share it freely. The icon and screenshots are © TheRealRel under [CC BY-NC 4.0](https://creativecommons.org/licenses/by-nc/4.0/): free to share and adapt with credit, but not for commercial use. See LICENSE.
