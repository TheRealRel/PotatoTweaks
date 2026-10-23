Potato Tweaks is a collection of Valheim tweaks, fixes and additions made for the Potato Server. It goes on the server and in every player's game. Every feature can be switched off on its own. Server settings come from the server and are the same for everyone; personal settings are each player's own.

![](https://raw.githubusercontent.com/TheRealRel/PotatoTweaks/main/docs/images/icon-chest.png) ![](https://raw.githubusercontent.com/TheRealRel/PotatoTweaks/main/docs/images/icon-door.png) ![](https://raw.githubusercontent.com/TheRealRel/PotatoTweaks/main/docs/images/icon-smelter.png)

**Chests, doors and station timers.** Name a chest by looking at it and pressing Ctrl + N, and everyone sees the name. Looking at a chest also shows a row of small icons with what's inside. Doors you open close themselves a few seconds later, once the doorway is clear. Smelters, kilns, furnaces, spinning wheels, windmills, fermenters and cooking stations show how long until the next item, until everything is done, and until the fuel runs out.

![A named chest and its contents on hover](https://raw.githubusercontent.com/TheRealRel/PotatoTweaks/main/docs/images/chest.jpg)

![A smelter's timers on hover](https://raw.githubusercontent.com/TheRealRel/PotatoTweaks/main/docs/images/smelter.jpg)

![](https://raw.githubusercontent.com/TheRealRel/PotatoTweaks/main/docs/images/icon-map.png) **Getting around.** The camera zooms out farther: 12 m on land instead of 8, and 24 m on a ship. Dropped items last 3 hours of world time instead of 1, so loot dropped before a night's sleep is still there. Holding Ctrl and using the hammer's remove button takes apart a raft, karve, longship, drakkar or cart, refunding every material and dropping its cargo in crates. Type `retrievetomb` in the F5 console (or `/retrievetomb` in chat) to bring your own tombstones to you. A button by the skull on the large map clears every death marker at once.

![](https://raw.githubusercontent.com/TheRealRel/PotatoTweaks/main/docs/images/icon-house.png) **Around the base.** Every crafted food can be set out with the serving tray, from the game and from food mods like OdinHorse and Wizardry, and eaten like the game's own dishes. Any item goes on an item stand, and the `itemstand_scale`, `itemstand_rotation` and `itemstand_offset` console commands resize, turn or move it. A Comfort tab in the build menu lists every comfort piece, ticks what's nearby and says what each would add. The night is skipped once half the players are in bed or sitting, with at least one in bed. The Wisplight can be upgraded at the workbench up to level 5, clearing more mist at each level.

![](https://raw.githubusercontent.com/TheRealRel/PotatoTweaks/main/docs/images/icon-anvil.png) **Crafting and stations.** With a crafting station open, Ctrl + right-click an item to take it apart for half its materials, upgrades included. Smelters, kilns and furnaces have settings for capacity, speed, fuel and output, windmills mill twice as fast, and fermenters have speed and output settings. Carts and boats get more health the higher their builder's Crafting skill.

![](https://raw.githubusercontent.com/TheRealRel/PotatoTweaks/main/docs/images/icon-shield.png) **Fighting and hunting.** Press block during a swing's windup or recovery to cancel it and raise your guard at once. Every kill without a trophy makes the next one from that creature more likely, until it drops; the Trophies tab shows the chances and, without spoilers, what you haven't collected. Spears, arrows and bolts catch fish, and a harpoon pulls them straight into your inventory. Thunderblood and Frostfire essence drop for every player who was nearby.

![](https://raw.githubusercontent.com/TheRealRel/PotatoTweaks/main/docs/images/icon-pouch.png) **Skills, loot and inventory.** Skill loss on death and XP per skill are set by the server. Drops from creatures, resources and pickables, item weight, stack size and auto-pickup range all have multipliers. Haldor's extra inventory rows unlock after the Elder and Bonemass, and he sells a third after Yagluth. The Swim and Sneak skills raise swim and sneak speed, and tamed animals don't trample crops.

![](https://raw.githubusercontent.com/TheRealRel/PotatoTweaks/main/docs/images/icon-fire.png) **Small comforts.** Tombstone items go back to the slots they came from, walking over a weapon no longer equips it, the hammer menu updates live, and health bars show while you hold the hammer and through mist and trees in a fight. Inventory windows move aside when mods' extra slots would end up behind them or off screen, at any resolution. Status effect icons drop their cut-off names. Each half of the dragon bed is its own bed, so two players can sleep side by side.

![](https://raw.githubusercontent.com/TheRealRel/PotatoTweaks/main/docs/images/icon-mirror.png) **Looks.** Sliders for the glow on over-upgraded gear, the dust from felling trees and the particles that cover creatures in a fight. With Epic Loot, the Glowing aura, enchantment effects and legendary textures can be hidden. These only change what you see.

![](https://raw.githubusercontent.com/TheRealRel/PotatoTweaks/main/docs/images/icon-hammer.png) **Game fixes.** Fast second bow shots no longer vanish, swings from a ship's deck hit what's on the deck, a broken fuling shaman shield stops blocking arrows, plants keep growing after a lag spike, buried ore veins keep their support, and overdue berry bushes regrow when someone comes near. With Quick Stack - Store - Sort - Trash - Restock, sorting and storing leave Equipment and Quick Slots' slots alone.

![](https://raw.githubusercontent.com/TheRealRel/PotatoTweaks/main/docs/images/icon-gem.png) **With Epic Loot.** Bounties complete when the main target dies, spread out across the map, and the target gets a small health bar and always drops at least one enchanted item. Riches, Fire Rate, Noise, +Ranged Skills, Meads Cooldown, Indestructible, Bountiful Harvest, Quick Draw and Air Jump work as described. The enchanting table takes its materials from chests within 50 m, damage enchantments can roll together, and Lucky Loot works on birds. Without Epic Loot these stay off.

![](https://raw.githubusercontent.com/TheRealRel/PotatoTweaks/main/docs/images/icon-coins.png) **Trader prices.** The Potato Server's traders pay [these prices](https://github.com/TheRealRel/PotatoTweaks/blob/main/docs/sell-prices.csv) for every item. Running Traders Extended on your own server? [The same prices as a ready-made file](https://github.com/TheRealRel/PotatoTweaks/blob/main/docs/common.sell.csv) go in `BepInEx/config/shudnal.TradersExtended/`.

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
| 10 - Bug fixes | Game and Epic Loot fixes (restart after changing) | Every game alike |
| 11 - Quality of life | Each comfort above, swim and sneak speed, crops | Personal; speeds and crops server |
| 12 - Smelters and kilns | Capacity, speed, fuel, output, stations left alone; windmills (2x speed) | Server |
| 13 - Fermenters | Speed and output (1 = normal) | Server |
| 14 - Crafting stations | Build range (1 = normal) | Server |
| 15 - Carts and boats | Health from Crafting skill (carts +400%, boats +100%) | Server |
| 16 - Map | Reveal radius (1 = normal); death marker button | Server; button personal |
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

Trophy pity counts are kept per character in `BepInEx/config/potato.tweaks.trophies.txt`. If a Valheim update breaks something a feature relies on, that feature switches itself off and writes why in the BepInEx log; the rest keep working.

**Replacing other mods.** Potato Tweaks covers Tomb Retriever, SkillLoss, Double Dragon Bed, Dimmer Combat Particle Effects, TrophyAssister, Spearfishing, Epic Loot Add-ons, DropMoreLoot, EssenceForAll and ComfortHelper. If one of them is installed, Potato Tweaks leaves that feature to it and says so in the log. When you switch, their old settings (and TrophyAssister's pity counts) are copied over on first launch. It won't load next to EpicLoot Progression Fix, which it already contains.

**Credits.** Camera zoom, dropped items, ship and cart removal, attack cancelling, the Epic Loot bounty and enchantment fixes, the game fixes and most of the small comforts come from EpicLoot Progression Fix by its contributors (MIT). The double dragon bed comes from Double Dragon Bed by Hansenbansen and spearfishing from Spearfishing by karkkant (both MIT). Item stands come from Item Stand All Items by JereKuusela (public domain). Essence for everyone and the comfort helper come from EssenceForAll and ComfortHelper by Koen-H (MIT). The rest was written for Potato Tweaks, some of it on ideas from RandyKnapp's Minimal Status Effects, blacks7ar's SNEAKer and Digitalroot's Better Wisps.

Bug reports go to [GitHub Issues](https://github.com/TheRealRel/PotatoTweaks/issues), where the source lives too. Questions are welcome on Discord: [relanebeis](https://discord.com/users/373574370267889674). The Changelog tab says whether everyone needs to update.

Needs [BepInExPack Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/) and [Jötunn](https://thunderstore.io/c/valheim/p/ValheimModding/Jotunn/). Epic Loot and StarLevelSystem are optional. Players without Potato Tweaks can't join a server that has it.

The code is under the MIT License: use it, change it and share it freely. The icon and screenshots are © TheRealRel under [CC BY-NC 4.0](https://creativecommons.org/licenses/by-nc/4.0/): free to share and adapt with credit, but not for commercial use. See LICENSE.
