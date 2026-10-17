# Changelog

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
