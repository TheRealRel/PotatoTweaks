# 🔨 Potato Hammer

**Sorts the build hammer by mod.** Mods put their build pieces wherever they like: spread over several of the game's tabs, in no tab, or in the wrong one. Potato Hammer moves every mod's hammer pieces to **a tab named after the mod**, and leaves the game's own pieces in the game's tabs.

- Works with any mod, Jotunn-based or not: Jotunn keeps track of which mod added each piece.
- Only the hammer is sorted. Mods with their own hammer (like Odin Architect), the hoe, the cultivator and the serving tray are left alone.
- A mod with more than 90 pieces (one tab's worth) continues on "Name 2".
- Client-side: it only changes your own build menu. The server and other players don't need it.

Part of the **PotatoClient** pack for the Potato Server. 🛠️ Source and bug reports: [github.com/TheRealRel/PotatoTweaks](https://github.com/TheRealRel/PotatoTweaks)

## Settings

In `BepInEx/config/potato.hammer.cfg`, or in game with Configuration Manager. Changes take effect the next time you take out the hammer.

| Section | Setting | What it does |
|---|---|---|
| **1 - General** | Sort the hammer (on) | Switch the sorting off to put every piece back where its mod put it. |
| | Mods to leave alone | Mods whose pieces stay where the mod put them, separated by commas. |
| | Extra pieces | Pieces whose mod can't be told, put on a tab by name: `piece=Tab`, separated by commas. The log lists those pieces. |
| **2 - Tabs** | One line per mod | The tab name for that mod's pieces (the mod's name by default). Give two mods the same name to share a tab; leave it empty to leave that mod's pieces where they were. The lines appear after the first time you take out the hammer in a world. |

## Requirements

- [BepInExPack Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/)
- [Jotunn](https://thunderstore.io/c/valheim/p/ValheimModding/Jotunn/)
