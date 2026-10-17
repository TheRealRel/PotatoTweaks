# Changelog

## 1.5.0 — update needed
- **Loot and items** (section 22, server): multipliers for creature, resource (trees, rocks, ore, destroyed objects, dungeon loot) and pickable drops, item weight, stack size, auto-pickup range and items that float in water. Replaces DropMoreLoot, now set by the server for everyone:
  - The game's own drop rules (stars, world modifiers) stay in charge; only the result is multiplied. Items dropped once per player and Hildir's quest chests aren't multiplied; trophies only if you turn that on.
  - Weight and stack size apply to every copy of an item, so crafted items and items loaded with your character are lighter and stack higher too, not only items picked up from the ground.
  - Fractions work: 1.5 gives one and a half times as much on average.
- Switching from DropMoreLoot: remove it; its settings are copied over on first launch (including multiplying trophies, as it did).
- The mod page's feature list and settings are sorted into groups, Epic Loot together, with who sets each one (server or player).
- The mod page links a ready-made `common.sell.csv` with the trader sell prices for servers running Traders Extended; both links explain themselves when you hover over them.
- Everyone, including the server, needs 1.5.0; 1.4.x can't join a 1.5.0 server.

## 1.4.1
- The mod page links to the Potato Server's trader sell prices: a searchable list of what the traders pay for every item. No changes to the mod itself; you don't need to update from 1.4.0, and 1.4.0 and 1.4.1 players can play together.

## 1.4.0 — update needed
- **Spearfishing** (section 19, server): catch fish with spears, harpoons, bows and crossbows. A hit fish floats up to be picked up; a harpoon pulls it straight into your inventory. Modded spears, bows and crossbows count too. Ported from Spearfishing.
- **Epic Loot enchanting table** (section 20, only with Epic Loot):
  - The table takes its costs from built chests within 50 m that you can open and nobody else has open (server).
  - The Sacrifice tab can list trophies and shardstones from those chests, with checkboxes in the tab (personal).
  - The double-output chance is rolled per sacrificed item instead of all or nothing (server).
- **Epic Loot enchantments** (section 21, only with Epic Loot):
  - Damage enchantments can roll together on one weapon; Double Magic Shot and Triple Shot keep their odds (server).
  - Double Magic Shot and Triple Shot cost the normal eitr or stamina instead of three times as much (server).
  - Double Magic Shot doubles a summoning staff's summon limit (server).
  - Shardstones can be socketed even when their effect clashes with one the item has (server).
  - Lucky Loot works on birds (server) and shows a message when it triggers (personal).
  - Enchanted staffs show their eitr cost in the tooltip (personal).
- **Visual effects** (section 7, personal, only with Epic Loot): hide the Glowing aura, all enchantment effects, or legendary textures on your screen.
- **Bug fixes** (section 10, personal, only with Epic Loot): Epic Loot no longer goes through every item of every loaded chest each second when nothing changed.
- The stations, carts and boats and map settings have their own sections (12 Smelters and kilns, 13 Fermenters, 14 Crafting stations, 15 Carts and boats, 16 Map), and the bed and trophies are 17 and 18. Values set in 1.3.0's combined section carry over.
- Switching from Spearfishing or Epic Loot Add-ons: remove them; their settings are copied over on first launch.
- Everyone, including the server, needs 1.4.0; 1.3.x can't join a 1.4.0 server.

## 1.3.0 — update needed
- **Quality of life** (section 11, personal unless noted):
  - Recovering your tombstone puts every item back in the slot it had when you died.
  - Walking over a weapon or tool with empty hands no longer equips it.
  - The hammer menu updates live as you move in and out of workbench or chest range.
  - Creature health bars show while holding the hammer, hoe or cultivator.
  - Combat health bars stay visible through mist, trees and buildings; an enemy attacking you within 15 m shows through anything.
  - Taming progress shows from health-bar range, and big animals can be petted or renamed while standing beside them.
  - With Equipment & Quick Slots, auto-pickup moves to ] when a quick slot uses V, so V can't switch it off by accident.
  - Tamed animals don't trample farm crops (server).
  - Swim skill raises swim speed, up to +25% at level 100 (server).
- **Smelters, fermenters, crafting stations, carts and boats, map** (sections 12–16, server):
  - Station, fermenter, range and map settings start at the game's normal values, so they change nothing until you set them.
  - Smelter-type stations (smelter, blast furnace, charcoal kiln, eitr refinery, spinning wheel, windmill): ore capacity, fuel capacity, speed, fuel per product and output, with a list of stations to leave alone.
  - Fermenters: speed and output.
  - Crafting station range and map reveal radius multipliers.
  - Carts and boats built from now on get more health the higher their builder's Crafting skill: carts up to +400%, boats up to +100% at Crafting 100 (adjustable, 0 = off).
- **Visual effects** (section 7, personal): the upgrade glow, tree-felling dust and a new combat effects dimmer now sit together. The dimmer makes burning, frost and poison effects, hit bursts and impact effects on creatures fainter, darker or smaller. Your upgrade glow settings carry over.
- **Double dragon bed** (section 17): two players can claim and sleep in the dragon bed, each respawning on their own half, with blankets. Ported from Double Dragon Bed; beds claimed with it keep their owners.
- **Trophy pity** (section 18): each kill without a trophy raises that creature's next trophy chance until it drops; the Trophies tab shows the chances and, spoiler-free, the trophies still missing.
- Switching from Double Dragon Bed, Dimmer Combat Particle Effects or TrophyAssister: remove them; their settings (and TrophyAssister's pity counts) are copied over on first launch.
- New icon: the viking potato.
- Everyone, including the server, needs 1.3.0; 1.2.x can't join a 1.3.0 server.

## 1.2.0 — update needed
- **Cancel an attack into a block:** pressing block mid-swing drops the attack and raises your guard, so you can parry. Server setting, on by default.
- **Epic Loot bounty fixes** (only when Epic Loot is installed; server settings):
  - Bounties complete when the main target dies; minions are optional. Contracts whose target already died finish too.
  - New bounties spread out across their biome, at least 300 m from other open bounties and treasure, instead of piling onto the same spots.
  - Bounty creatures no longer stand still while the ground's pathing is still loading.
  - The main target drops one extra copy of each enchanted item (each with its own enchantments) and always at least one enchanted item.
  - A small overhead name and health bar on bounty targets (each player can turn it off).
  - With StarLevelSystem: targets keep Epic Loot's extra health after reloading or changing hands, and scale with stars like ordinary creatures instead of bosses.
- **Game bug fixes** (section 10):
  - Fast second bow shots at high Bows skill are no longer silently dropped; they fire as soon as the bow is ready (blocking cancels them).
  - Swinging from a ship's deck hits logs and creatures on the deck instead of the ship.
  - A broken fuling shaman shield stops absorbing arrows that land together.
  - Plants no longer stop growing for good after a lag spike.
  - Long buried ore veins keep their support instead of breaking off.
  - Overdue berry bushes and other pickables regrow as soon as a player is near, and keep their saved regrowth time.
- **Epic Loot enchantment fixes** (section 10, only when Epic Loot is installed): Riches, Fire Rate, Noise, +Ranged Skills, Meads Cooldown with Instant Mead, Indestructible armor, Bountiful Harvest on other players' plants, Quick Draw crossbow reload, and Air Jump near the ground.
- Everyone, including the server, needs 1.2.0; 1.1.x can't join a 1.2.0 server.

## 1.1.3
- New look for the top of the mod page. No changes to the mod itself; you don't need to update from 1.1.2.

## 1.1.2
- Added an **Upgrade glow** slider (section 7): sets how strongly gear upgraded past its normal max level glows. 1 = the game's normal glow, 0 = no glow, up to 2 = twice as strong. Each player sets their own, and the change shows at once on gear already out.
- Glowing gear keeps its intended brightness when its effect is switched off and on again (the game re-read values it had already boosted).
- 1.1.x players can still play together.

## 1.1.1
- Clearer description of the skill settings: they start at the game's normal values and are set by the server. Copying settings from the SkillLoss mod only happens for servers switching from it. No gameplay changes; 1.1.0 and 1.1.1 players can play together.

## 1.1.0
- Added tombstone retrieval: `retrievetomb` / `gettomb` in the console, `/retrievetomb` in chat. Moved tombstones stay where they are put (the game's saved drop point moves with them, so they no longer snap back to the death site).
- Added per-skill death loss and XP multipliers, set by the server and starting at the game's normal values. Servers switching from the SkillLoss mod get their old values copied over on first launch.
- If Tomb Retriever or SkillLoss is still installed, the matching feature stays off with a warning instead of doubling up.

## 1.0.0
- First release: farther camera zoom, 3-hour dropped items, and Ctrl + hammer remove to take apart ships and carts.
