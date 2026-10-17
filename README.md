# 🥔 Potato Tweaks

A Valheim mod made for the Potato Server. It consolidates, fixes and adds things I wanted.

**Download:** [Thunderstore — Potato Tweaks](https://thunderstore.io/c/valheim/p/Potato_Tweaks/PotatoTweaks/) (install with r2modman, Thunderstore Mod Manager or Gale)

## What it does

- Farther camera zoom on land and on ships
- Dropped items last 3 hours instead of 1
- Ctrl + hammer remove takes apart ships and carts with a full refund
- `retrievetomb` brings your tombstones to you (and they stay put)
- Skill loss on death and XP rates, per skill
- Visual effect sliders: upgrade glow, tree-felling dust and a combat effects dimmer
- Double dragon bed for two players, and trophy pity with chances in the Trophies tab
- Cancel an attack into a block
- Spearfishing: catch fish with spears, harpoons, bows and crossbows
- Epic Loot bounty fixes (only with Epic Loot)
- Epic Loot enchanting table uses nearby chests; damage enchantments stack, normal Double/Triple Shot cost, shardstones ignore effect clashes, Lucky Loot messages and birds, eitr cost tooltip, hide enchantment looks (only with Epic Loot)
- Fixes for Valheim bugs and Epic Loot enchantments
- Quality of life: tombstone items back in their slots, live hammer menu, better health bars, taming info, tree dust slider, and more
- Carts and boats get more health from their builder's Crafting skill
- Haldor inventory rows: earlier unlocks and a third row (Roomier pockets)
- Loot and item settings: drop multipliers, item weight, stack size, auto-pickup range, floating items (server-controlled)
- Optional smelter, fermenter, crafting station range and map reveal settings (normal by default)

**Trader sell prices:** [docs/sell-prices.csv](docs/sell-prices.csv) lists what the Potato Server's traders (Traders Extended) pay for every item. GitHub shows it as a table you can search. To use the same prices on a Traders Extended server, put [docs/common.sell.csv](docs/common.sell.csv) in `BepInEx/config/shudnal.TradersExtended/`.

The full description, settings and requirements are in [package/README.md](package/README.md). Changes per version: [CHANGELOG.md](CHANGELOG.md).

## Bug reports

Open an [issue](https://github.com/TheRealRel/PotatoTweaks/issues) with:
- the Potato Tweaks version,
- what happened and what you expected,
- your `BepInEx/LogOutput.log` (look for lines mentioning Potato Tweaks or "is disabled").

## Building

See [BUILD.md](BUILD.md).

## Credits and license

MIT License, see [LICENSE](LICENSE). Camera zoom, dropped-item lifetime, ship/cart removal, attack cancelling, the Epic Loot bounty and enchantment fixes, the game bug fixes and most quality-of-life features are adapted from **EpicLoot Progression Fix** by the EpicLoot Progression Fix contributors (MIT). The double dragon bed comes from **Double Dragon Bed** by Hansenbansen (MIT) and spearfishing from **Spearfishing** by karkkant (MIT). The Epic Loot enchanting table and enchantment add-ons and the loot and item settings were written for Potato Tweaks. Tombstone retrieval, the skill settings, the visual effect sliders and combat effects dimmer, trophy pity, the station, fermenter, range and map settings and boat health were written for Potato Tweaks.
