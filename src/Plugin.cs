// Potato Tweaks
// Camera zoom, dropped-item lifetime and ship/cart removal are carried over from EpicLoot Progression Fix 0.1.281
// (MIT License, Copyright (c) 2026 EpicLoot Progression Fix contributors). Tombstone retrieval and the skill
// settings are written from scratch for this mod. Attack cancelling, the Epic Loot bounty and enchantment fixes and the
// game bug fixes are adapted from the same mod. See LICENSE.
using System;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Jotunn.Managers;
using Jotunn.Utils;

namespace PotatoTweaks
{
    [BepInPlugin(Id, "Potato Tweaks", Version)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    [BepInDependency(TombRetrieverId, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(SkillLossId, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(EpicLootFeatures.EpicLootId, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(EpicLootFeatures.StarLevelSystemId, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(QualityOfLife.EquipmentAndQuickSlotsId, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(DimmerModId, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(DoubleBed.DoubleDragonBed.OriginalModId, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(Trophies.OriginalModId, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(EpicLootAddons.OriginalModId, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(Spearfishing.OriginalModId, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(LootAndItems.OriginalModId, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(ItemStands.OriginalModId, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(Wisplight.OriginalModId, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(QuickStackSlots.QuickStackId, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInIncompatibility("valheim.epiclootprogressionfix")]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Id = "potato.tweaks";
        public const string Version = "1.5.4";
        private const string TombRetrieverId = "ryan.tombretriever";
        private const string SkillLossId = "Mirfin.SkillLoss";

        internal static ManualLogSource Log;

        // Personal settings (each player chooses their own).
        internal static ConfigEntry<bool> CameraEnabled;
        internal static ConfigEntry<float> ShipZoomDistance, LandZoomMultiplier;
        internal static ConfigEntry<bool> GlowEnabled;
        internal static ConfigEntry<float> GlowStrength;
        internal static ConfigEntry<bool> DimEnabled, DimStatus, DimCreatureHits, DimWeapons, DimImpacts, DimOnPlayers;
        internal static ConfigEntry<float> DimOpacity, DimBrightness, DimSize;
        internal static ConfigEntry<bool> BedEnabled, BedBlankets;
        internal static ConfigEntry<bool> TrophyPity, TrophyFlatMode, TrophyOnlyMine, TrophyOnlyUncollected, TrophyShowChances, TrophyShowMissing,
            TrophySpoilerFree, TrophyNotify, TrophyShowProgress;
        internal static ConfigEntry<float> TrophyChanceMultiplier, TrophyGrowth, TrophyRareThreshold, TrophyRareGrowth, TrophyFlatBonus;
        internal static ConfigEntry<int> TrophyGuarantee, TrophyExtraKillsPerStar;
        internal static ConfigEntry<string> TrophyHidden;
        internal const string DimmerModId = "wheeze.valheim.dimmercombatparticleeffects";
        internal static ConfigEntry<bool> HideGlowing, HideEnchantEffects, HideLegendaryLooks, FasterChestLoading, ChestTrophies, ChestShardstones,
            LuckyLootMessage, EitrTooltip;
        internal static ConfigEntry<MessageHud.MessageType> LuckyLootPosition;

        // Server-controlled settings.
        internal static ConfigEntry<bool> DroppedItemsEnabled, ShipRemovalEnabled, TombEnabled;
        internal static ConfigEntry<float> DroppedItemLifetimeHours;
        internal static ConfigEntry<int> TombCooldownSeconds;
        internal static ConfigEntry<bool> AttackCancelEnabled;
        internal static ConfigEntry<bool> BountyMainKillOnly, BountySpreadOut, BountyGuaranteedItem, BountyNormalScaling, BountyHealthBar;
        internal static ConfigEntry<float> BountySpacing;
        internal static ConfigEntry<int> BountyExtraCopies;
        internal static ConfigEntry<bool> GameFixesEnabled, EpicLootFixesEnabled;
        internal static ConfigEntry<bool> QolNoAutoEquip, QolLiveBuildMenu, QolBuildToolHealthBars, QolCombatHealthBars, QolTamingHover,
            QolGraveSlots, QolAutoPickupKey, QolTamedCrops, QolSwimSpeed;
        internal static ConfigEntry<float> QolAttackerRange, QolTreeDust;
        internal static ConfigEntry<string> SmelterExcluded;
        internal static ConfigEntry<float> SmelterOreCapacity, SmelterFuelCapacity, SmelterFuelPerProduct, SmelterOutput, SmelterSpeed, FermenterSpeed, FermenterOutput, StationRange, MapRevealRadius,
            CartHealthBonus, BoatHealthBonus;
        internal static ConfigEntry<bool> FishSpears, FishHarpoons, FishBows, FishCrossbows;
        internal static ConfigEntry<bool> ChestMaterialsEnabled, SacrificePerItem, DamageStacking, ShotNormalCost, ShotSummonLimit, ShardsIgnoreClashes, LuckyLootBirds;
        internal static ConfigEntry<float> ChestMaterialsRange;
        internal static ConfigEntry<float> LootCreatures, LootResources, LootPickables, ItemWeight, PickupRangeSetting;
        internal static ConfigEntry<bool> LootTrophies, ItemsFloat;
        internal static ConfigEntry<string> LootNever, LootOnly;
        internal static ConfigEntry<int> ItemStackSize;
        internal static ConfigEntry<bool> RowsEarlier, RowsThird, InventorySlotsVisibility, DeathMarkerButton;
        internal static ConfigEntry<int> RowsThirdPrice;
        internal static ConfigEntry<string> RowsThirdBoss;
        internal static ConfigEntry<int> SleepShare, SleepInBed, StatusEffectTextSize;
        internal static ConfigEntry<bool> StatusEffectListEnabled;
        internal static ConfigEntry<float> QolSneakSpeed, WispRangePerLevel;
        internal static ConfigEntry<int> WispLevels, WispWispsPerLevel, WispSilverPerLevel, WispEitrPerLevel;
        internal static ConfigEntry<float> StatusEffectIconSize, StatusEffectSpacing, ItemStandMaxOffset, ItemStandMaxScale;
        internal static ConfigEntry<ItemStandItems> ItemStandMode;
        internal static ConfigEntry<string> ItemStandIds, ItemStandTransformations;
        internal static ConfigEntry<bool> ItemStandAdjust, ItemStandLegacy, ItemStandHide, ItemStandCloser;
        internal static ConfigEntry<SleepVotes> SleepVoteMode;

        internal static Harmony HarmonyInstance;

        private Harmony harmony;

        private static ConfigDescription Synced(string text, AcceptableValueBase range = null) =>
            new ConfigDescription(text + " [Synced with Server]", range, new ConfigurationManagerAttributes { IsAdminOnly = true });

        private void Awake()
        {
            Log = Logger;

            CameraEnabled = Config.Bind("1 - Camera", "Enabled", true,
                "Lets the camera zoom out farther, on land and on ships.");
            LandZoomMultiplier = Config.Bind("1 - Camera", "Land zoom multiplier", 1.5f,
                new ConfigDescription("Multiplies the game's farthest camera zoom off ships (8 m). 1.5 = 12 m; 1 keeps the native limit.",
                    new AcceptableValueRange<float>(1f, 5f)));
            ShipZoomDistance = Config.Bind("1 - Camera", "Ship zoom distance", 24f,
                new ConfigDescription("Farthest camera zoom in meters for the helmsman and passengers on a ship.",
                    new AcceptableValueRange<float>(1f, 100f)));

            DroppedItemsEnabled = Config.Bind("2 - Dropped items", "Enabled", true,
                Synced("Use the lifetime below instead of the game's 1 hour for dropped items."));
            DroppedItemLifetimeHours = Config.Bind("2 - Dropped items", "Lifetime hours", 3f,
                Synced("Hours of world time a dropped item lasts before the game removes it. As in the game, an item inside a base, within 25 m of a player, in tar or part of a build stays. The game's own lifetime is 1 hour, which a night's sleep can skip at once.",
                    new AcceptableValueRange<float>(0.1f, 1000f)));

            ShipRemovalEnabled = Config.Bind("3 - Ship and cart removal", "Enabled", true,
                Synced("Hold Ctrl and use the hammer's remove button to take apart a raft, karve, longship, drakkar or cart. The game's normal rules still apply: a workbench in range, no ward or no-build zone, nobody aboard a raft. Every material is refunded and the cargo drops in crates."));

            TombEnabled = Config.Bind("4 - Tombstone retrieval", "Enabled", true,
                Synced("Players can type retrievetomb (or gettomb) in the F5 console, or /retrievetomb in chat, to bring their own tombstones to where they stand."));
            TombCooldownSeconds = Config.Bind("4 - Tombstone retrieval", "Cooldown seconds", 10,
                Synced("Seconds a player must wait between retrievals. 0 turns the wait off.", new AcceptableValueRange<int>(0, 3600)));

            SkillSettings.Bind(Config, (text, range) => Synced(text, range));

            const string visual = "7 - Visual effects";
            var before = ConfigText.Read(Config.ConfigFilePath);
            GlowEnabled = Config.Bind(visual, "Upgrade glow", true,
                "Use the glow strength below for gear upgraded past its normal max level (the game makes such gear glow brighter with every extra level). Personal.");
            GlowStrength = Config.Bind(visual, "Upgrade glow strength", 1f,
                new ConfigDescription("Strength of that glow: its particles, colour and light. 1 = the game's normal glow, 0.5 = half, 0 = no glow at all, 2 = twice as strong. Personal.",
                    new AcceptableValueRange<float>(0f, 2f)));
            QolTreeDust = Config.Bind(visual, "Tree-felling dust", 1f,
                new ConfigDescription("Dust and debris from trees and logs being hit, falling, landing and breaking. 1 = the game's normal amount, 0.5 = half, 0 = none. Sounds stay. Personal.",
                    new AcceptableValueRange<float>(0f, 1f)));
            DimEnabled = Config.Bind(visual, "Combat effects dimmer", true,
                "Make the particles that cover creatures in a fight fainter, so their attack wind-ups stay visible: set how much with the three values below (all 1 = the game's normal). Applies from the next hit. Personal.");
            DimOpacity = Config.Bind(visual, "Combat effects opacity", 1f,
                new ConfigDescription("1 = fully visible (the game's normal), 0.35 = faint, 0 = invisible. Personal.", new AcceptableValueRange<float>(0f, 1f)));
            DimBrightness = Config.Bind(visual, "Combat effects brightness", 1f,
                new ConfigDescription("1 = the game's normal, lower = darker. Personal.", new AcceptableValueRange<float>(0f, 1f)));
            DimSize = Config.Bind(visual, "Combat effects size", 1f,
                new ConfigDescription("1 = the game's normal, lower = smaller particles. Personal.", new AcceptableValueRange<float>(0.1f, 1f)));
            DimStatus = Config.Bind(visual, "Dim status effects on creatures", true,
                "The flames of a burning creature, frost and poison clouds and the burst on every damage tick. Personal.");
            DimCreatureHits = Config.Bind(visual, "Dim creature hit bursts", true, "Blood, sparks and the critical and sneak-attack bursts when a creature is hit. Personal.");
            DimWeapons = Config.Bind(visual, "Dim weapon impacts", true, "What a player's weapon throws off where it hits, elemental bursts included. Personal.");
            DimImpacts = Config.Bind(visual, "Dim projectile and area impacts", true, "Arrows, bolts, staff shots, bombs and area attacks from players where they hit. Personal.");
            DimOnPlayers = Config.Bind(visual, "Also dim effects on players", false, "Also dim status effects and hit bursts on players, yourself included. Personal.");
            bool epicLoot = Chainloader.PluginInfos.ContainsKey(EpicLootFeatures.EpicLootId);
            if (epicLoot)
            {
                HideGlowing = Config.Bind(visual, "Hide Glowing enchantment", false,
                    "Hide the light and sparkles of Epic Loot's Glowing enchantment, on everyone. Only on your screen; the enchantment still works. Personal.");
                HideEnchantEffects = Config.Bind(visual, "Hide all enchantment effects", false,
                    "Hide every visual effect of Epic Loot enchantments and legendary items (auras, flames, sparkles), on everyone. Only on your screen. Personal.");
                HideLegendaryLooks = Config.Bind(visual, "Hide legendary looks", false,
                    "Show Epic Loot legendary items with their normal look instead of their special texture (like the blue Heimdall armor). Only on your screen. Personal.");
            }
            ConfigText.CarryOver(Config, before, "7 - Upgrade glow", "Enabled", GlowEnabled);
            ConfigText.CarryOver(Config, before, "7 - Upgrade glow", "Glow strength", GlowStrength);
            if (!Chainloader.PluginInfos.ContainsKey(DimmerModId) && !before.ContainsKey(visual + "|Combat effects opacity"))
                ConfigText.ImportDimmer(Config);
            AttackCancelEnabled = Config.Bind("8 - Attack cancel", "Enabled", true,
                Synced("Pressing block during an attack's windup or recovery abandons the attack and raises the guard at once, so you can parry mid-swing. A hit the swing hasn't reached never lands; stamina already paid and a hit already dealt stay. Cancelling never lets you attack faster."));

            GameFixesEnabled = Config.Bind("10 - Bug fixes", "Game bug fixes", true,
                "Fixes for bugs in Valheim: dropped fast bow shots, swings stopped by the deck of the ship you stand on, broken fuling shaman shields still blocking, plants that stop growing, buried ore veins losing support, berry bushes slow to regrow. Takes effect after a restart; keep it the same on the server and every player's game.");
            if (epicLoot)
                EpicLootFixesEnabled = Config.Bind("10 - Bug fixes", "Epic Loot enchantment fixes", true,
                    "Fixes for Epic Loot enchantments: Riches, Fire Rate, Noise, +Ranged Skills, Meads Cooldown, Indestructible, Bountiful Harvest, Quick Draw crossbow reload and Air Jump near the ground. Takes effect after a restart; keep it the same on the server and every player's game.");
            if (epicLoot)
                FasterChestLoading = Config.Bind("10 - Bug fixes", "Epic Loot faster chest loading", true,
                    "Epic Loot goes through every item of every loaded chest each time the game checks it for changes (once a second, more often with crafting-from-chests mods), even when nothing changed. On: it only does so when the chest's items really changed, with the same result. Personal.");
            if (epicLoot)
            {
                const string bounties = "9 - Epic Loot bounties";
                BountyMainKillOnly = Config.Bind(bounties, "Complete on main target", true,
                    Synced("A bounty completes when its main target dies; its minions are optional. Off: Epic Loot's rule, every minion must die too."));
                BountySpreadOut = Config.Bind(bounties, "Spread out new bounties", true,
                    Synced("Place each new bounty evenly across its biome's distance band, at least the spacing below from every open bounty and unopened treasure chest. Off: Epic Loot's own placement, where players' bounties pile onto the same few spots."));
                BountySpacing = Config.Bind(bounties, "Spacing", 300f,
                    Synced("Meters a new bounty keeps from other open bounties and treasure chests, or as far as possible when no spot is that far.", new AcceptableValueRange<float>(0f, 5000f)));
                BountyExtraCopies = Config.Bind(bounties, "Extra enchanted copies", 1,
                    Synced("Each enchanted equipment drop from a bounty's main target is rolled again this many times, each copy with its own enchantments. 0 = Epic Loot's normal drops.", new AcceptableValueRange<int>(0, 5)));
                BountyGuaranteedItem = Config.Bind(bounties, "Guaranteed enchanted item", true,
                    Synced("A bounty's main target always drops at least one enchanted item from its own equipment loot."));
                BountyNormalScaling = Config.Bind(bounties, "Normal star scaling", true,
                    Synced("With StarLevelSystem: bounty targets keep Epic Loot's extra health after reloading and scale with stars like ordinary creatures instead of bosses. Their boss health bar stays."));
                BountyHealthBar = Config.Bind(bounties, "Overhead health bar", true,
                    "Show a small name and health bar above bounty targets, as on ordinary creatures, besides the boss bar at the top of the screen. Personal.");
            }

            const string qol = "11 - Quality of life";
            QolNoAutoEquip = Config.Bind(qol, "No auto-equip on pickup", true,
                "Walking over a club, pickaxe or torch with empty hands doesn't put it in your hands. Personal.");
            QolLiveBuildMenu = Config.Bind(qol, "Live hammer menu", true,
                "Hammer piece icons update as you walk in and out of workbench or chest range, without reopening the menu. Personal.");
            QolBuildToolHealthBars = Config.Bind(qol, "Health bars with build tools", true,
                "Creature health bars still show while you hold the hammer, hoe or cultivator. Personal.");
            QolCombatHealthBars = Config.Bind(qol, "Combat health bars", true,
                "Alerted enemies and animals being tamed (or tamed) keep their health bars through mist, trees and buildings; the ground and rock still hide them. Personal.");
            QolAttackerRange = Config.Bind(qol, "Attacker health bar range", 15f,
                new ConfigDescription("An enemy attacking you within this many meters shows its health bar through anything. Personal.", new AcceptableValueRange<float>(0f, 100f)));
            QolTamingHover = Config.Bind(qol, "Taming info at range", true,
                "See an animal's taming progress from health-bar range, and pet or rename big animals while standing beside them. Personal.");
            QolGraveSlots = Config.Bind(qol, "Tombstone items back in their slots", true,
                "Recovering your own tombstone returns every item to the slot it had when you died; items picked up since then move aside. Personal.");
            QolAutoPickupKey = Config.Bind(qol, "Auto-pickup key with Equipment & Quick Slots", true,
                "When a quick slot uses V, auto-pickup moves to ] (right bracket), so pressing V on an empty quick slot can't switch auto-pickup off. Read at startup. Personal.");
            InventorySlotsVisibility = Config.Bind(qol, "Inventory Slots Visibility", true,
                "No inventory slot stays hidden: when a window covers equipment, quick, quiver, backpack, chest or inventory-row slots, or pushes them past the screen edge, that window moves just enough to show them, at any screen size from 16:9 to 32:9 and any GUI scale. It moves back when no longer in the way, and each move is noted once in the log. Only looks again when the layout changes. Personal.");
            StatusEffectListEnabled = Config.Bind(qol, "Status effect list", true,
                "Status effects show as bigger icons in rows under the minimap, each with its time left under it, instead of a row of small icons with names beside it. Fits the minimap at any screen size and GUI scale; top-right corner in worlds without a map. While sailing, the wind and speed indicators move aside if they would be covered. Personal.");
            StatusEffectIconSize = Config.Bind(qol, "Status icon size", 48f,
                new ConfigDescription("Size of the status effect icons under the minimap. Personal.", new AcceptableValueRange<float>(16f, 96f)));
            StatusEffectTextSize = Config.Bind(qol, "Status timer text size", 18,
                new ConfigDescription("Size of the time left shown under each status effect icon. Personal.", new AcceptableValueRange<int>(10, 40)));
            StatusEffectSpacing = Config.Bind(qol, "Status icon gap", 8f,
                new ConfigDescription("Space between the status effect icons. Personal.", new AcceptableValueRange<float>(0f, 40f)));
            QolTamedCrops = Config.Bind(qol, "Tamed animals spare crops", true,
                Synced("Your tamed animals walking through farm crops and planted bushes don't trample them. Wild animals and attacks still do damage."));
            QolSwimSpeed = Config.Bind(qol, "Swim skill raises swim speed", true,
                Synced("Swimming speed rises with the Swim skill the way running speed rises with Run: up to +25% at level 100."));
            QolSneakSpeed = Config.Bind(qol, "Sneak skill raises sneak speed", 100f,
                Synced("Percent faster sneaking at Sneak level 100, rising with the skill: 100 = 2 m/s becomes 4 m/s, as fast as jogging but quiet; 50 = 3 m/s. Not while over-encumbered. 0 = off, the game's normal.",
                    new AcceptableValueRange<float>(0f, 150f)));

            const string smelters = "12 - Smelters and kilns", fermenters = "13 - Fermenters", craftingStations = "14 - Crafting stations",
                vehicles = "15 - Carts and boats", map = "16 - Map";
            SmelterOreCapacity = Config.Bind(smelters, "Ore capacity", 1f,
                Synced("Multiplies how much ore (or wood, flax, barley, sap...) smelter-type stations hold: smelter, blast furnace, charcoal kiln, eitr refinery, spinning wheel, windmill. 1 = the game's normal, 2 = twice as much. Applies as stations load.",
                    new AcceptableValueRange<float>(0.5f, 10f)));
            SmelterFuelCapacity = Config.Bind(smelters, "Fuel capacity", 1f,
                Synced("Multiplies how much fuel (coal, wood) these stations hold. 1 = the game's normal. Applies as stations load.", new AcceptableValueRange<float>(0.5f, 10f)));
            SmelterSpeed = Config.Bind(smelters, "Speed", 1f,
                Synced("Speeds up these stations: 2 = twice as fast. 1 = the game's normal. Applies as stations load.", new AcceptableValueRange<float>(0.5f, 10f)));
            SmelterFuelPerProduct = Config.Bind(smelters, "Fuel per product", 1f,
                Synced("Multiplies the fuel used for each product, rounded and never below 1 where the game uses fuel: 0.5 makes the smelter use 1 coal per bar instead of 2. 1 = the game's normal. Applies as stations load.",
                    new AcceptableValueRange<float>(0.1f, 5f)));
            SmelterOutput = Config.Bind(smelters, "Output", 1f,
                Synced("Multiplies what each product gives: 2 = two bars per ore, 1.5 = a 50% chance of a second one. 1 = the game's normal.", new AcceptableValueRange<float>(1f, 10f)));
            SmelterExcluded = Config.Bind(smelters, "Excluded stations", "",
                Synced("Stations these settings leave alone, by prefab name, separated by commas: smelter, blastfurnace, charcoal_kiln, eitrrefinery, piece_spinningwheel, windmill (or a modded station's prefab name)."));
            FermenterSpeed = Config.Bind(fermenters, "Speed", 1f,
                Synced("Speeds up fermenting: 2 = half the time. 1 = the game's normal. Applies as fermenters load.", new AcceptableValueRange<float>(0.5f, 10f)));
            FermenterOutput = Config.Bind(fermenters, "Output", 1f,
                Synced("Multiplies the meads each batch gives, rounded: 1.5 turns 6 into 9. 1 = the game's normal. Applies as fermenters load. (A fermenter always holds one batch, so there's no capacity setting.)", new AcceptableValueRange<float>(0.5f, 5f)));
            StationRange = Config.Bind(craftingStations, "Build range", 1f,
                Synced("Multiplies the build range around workbenches, forges and other crafting stations, extension bonuses included. 1 = the game's normal; 1.5 = half again as far.",
                    new AcceptableValueRange<float>(1f, 5f)));
            MapRevealRadius = Config.Bind(map, "Reveal radius", 1f,
                Synced("Multiplies how far around you the map is revealed as you explore (game: 100 m). 1 = the game's normal; 1.5 = 150 m.", new AcceptableValueRange<float>(1f, 5f)));
            DeathMarkerButton = Config.Bind(map, "Remove death markers button", true,
                "Adds a button by the skull icon on the large map that removes all death markers (click twice to confirm). Your own pins stay. Personal.");
            CartHealthBonus = Config.Bind(vehicles, "Cart health from Crafting skill", 4f,
                Synced("New carts get extra health from their builder's Crafting skill: this value times the skill share, so 4 = +400% at Crafting 100, +200% at 50. Changing it rescales existing crafted carts. 0 = off.",
                    new AcceptableValueRange<float>(0f, 10f)));
            BoatHealthBonus = Config.Bind(vehicles, "Boat health from Crafting skill", 1f,
                Synced("New boats (raft, karve, longship, drakkar) get extra health the same way: 1 = +100% at Crafting 100, +50% at 50. Changing it rescales existing crafted boats. 0 = off.",
                    new AcceptableValueRange<float>(0f, 10f)));

            // 1.3.0 as first uploaded kept these in one "12 - Stations, vehicles and map" section, with the bed and trophies
            // in 13 and 14. Move any values set there.
            const string oldStations = "12 - Stations, vehicles and map";
            ConfigText.CarryOver(Config, before, oldStations, "Smelter ore capacity", SmelterOreCapacity);
            ConfigText.CarryOver(Config, before, oldStations, "Smelter capacity", SmelterOreCapacity);
            ConfigText.CarryOver(Config, before, oldStations, "Smelter fuel capacity", SmelterFuelCapacity);
            ConfigText.CarryOver(Config, before, oldStations, "Smelter speed", SmelterSpeed);
            ConfigText.CarryOver(Config, before, oldStations, "Smelter fuel per product", SmelterFuelPerProduct);
            ConfigText.CarryOver(Config, before, oldStations, "Smelter output", SmelterOutput);
            ConfigText.CarryOver(Config, before, oldStations, "Smelter excluded stations", SmelterExcluded);
            ConfigText.CarryOver(Config, before, oldStations, "Fermenter speed", FermenterSpeed);
            ConfigText.CarryOver(Config, before, oldStations, "Fermenter output", FermenterOutput);
            ConfigText.CarryOver(Config, before, oldStations, "Crafting station range", StationRange);
            ConfigText.CarryOver(Config, before, oldStations, "Map reveal radius", MapRevealRadius);
            ConfigText.CarryOver(Config, before, oldStations, "Cart health from Crafting skill", CartHealthBonus);
            ConfigText.CarryOver(Config, before, oldStations, "Boat health from Crafting skill", BoatHealthBonus);

            const string bed = "17 - Double dragon bed";
            BedEnabled = Config.Bind(bed, "Enabled", true,
                "Each half of the dragon bed is a bed of its own: two players can claim it, sleep side by side and respawn on their own half. One player owning both halves sleeps in the middle. Shift+E on your half gives it up. Read at startup; keep it the same on the server and every player's game.");
            BedBlankets = Config.Bind(bed, "Blankets", true,
                "The fur draped over each half's edge rolls up at the sleeper's feet and unrolls over them, shaped to what they wear. Only looks. Personal.");
            ConfigText.CarryOver(Config, before, "13 - Double dragon bed", "Enabled", BedEnabled);
            ConfigText.CarryOver(Config, before, "13 - Double dragon bed", "Blankets", BedBlankets);
            if (!before.ContainsKey(bed + "|Blankets") && !before.ContainsKey("13 - Double dragon bed|Blankets")) ConfigText.CopyBool(Config, "hansenbansen.doublebed.cfg", "Blankets", "Enabled", BedBlankets);

            const string trophies = "18 - Trophies";
            TrophyPity = Config.Bind(trophies, "Pity", true,
                Synced("Every kill that drops no trophy makes that creature's next trophy more likely; the count resets when it drops."));
            TrophyFlatMode = Config.Bind(trophies, "Flat growth", false,
                Synced("Off: each dry kill multiplies the chance (10% -> 11% -> 12.1% with 0.1 growth). On: each dry kill adds the flat bonus below."));
            TrophyGrowth = Config.Bind(trophies, "Growth per dry kill", 0.1f,
                Synced("Multiplying growth per dry kill: 0.1 = +10% of the current chance.", new AcceptableValueRange<float>(0f, 2f)));
            TrophyRareThreshold = Config.Bind(trophies, "Rare trophy threshold", 0.05f,
                Synced("Trophies whose normal chance is at or below this (0.05 = 5%) use the rare growth below.", new AcceptableValueRange<float>(0f, 1f)));
            TrophyRareGrowth = Config.Bind(trophies, "Rare growth per dry kill", 0.2f,
                Synced("Multiplying growth per dry kill for rare trophies.", new AcceptableValueRange<float>(0f, 2f)));
            TrophyFlatBonus = Config.Bind(trophies, "Flat bonus per dry kill", 0.05f,
                Synced("With flat growth: chance added per dry kill, 0.05 = +5 percentage points.", new AcceptableValueRange<float>(0f, 1f)));
            TrophyGuarantee = Config.Bind(trophies, "Guarantee after dry kills", 0,
                Synced("A trophy always drops after this many dry kills. 0 = off.", new AcceptableValueRange<int>(0, 1000)));
            TrophyExtraKillsPerStar = Config.Bind(trophies, "Extra dry kills per star", 1,
                Synced("A starred creature that drops no trophy counts as this many extra dry kills per star. 0 = one kill each.", new AcceptableValueRange<int>(0, 10)));
            TrophyChanceMultiplier = Config.Bind(trophies, "Chance multiplier", 1f,
                Synced("Multiplies the normal trophy chance before pity. 1 = the game's normal.", new AcceptableValueRange<float>(0f, 10f)));
            TrophyOnlyMine = Config.Bind(trophies, "Only count my kills", true,
                Synced("Only your kills add pity: your last hit (burning and poison after your hit count), or your tames and summons following you."));
            TrophyOnlyUncollected = Config.Bind(trophies, "Only uncollected trophies", false,
                Synced("Pity and the chance multiplier only apply to trophies this character has never picked up."));
            TrophyShowChances = Config.Bind(trophies, "Show chances in Trophies tab", true, "Each trophy shows its next drop chance and dry kills. Personal.");
            TrophyShowMissing = Config.Bind(trophies, "Show missing trophies", true, "Trophies you haven't collected yet show greyed out in the place they'll take. Personal.");
            TrophySpoilerFree = Config.Bind(trophies, "Spoiler-free", true,
                "Missing trophies of creatures you've never killed show as a dark shape named ??????; after your first kill they show greyed out with their name. Personal.");
            TrophyHidden = Config.Bind(trophies, "Hidden trophies", "TrophyDeerWhite",
                "Trophy prefab names, separated by commas, never shown as missing. TrophyDeerWhite is unfinished game content. Personal.");
            TrophyNotify = Config.Bind(trophies, "Message on drop", false, "Show a message when a trophy drops. Personal.");
            TrophyShowProgress = Config.Bind(trophies, "Message on dry kill", false, "After a kill without a trophy, show the dry kills and the new chance. Personal.");
            foreach (var entry in new ConfigEntryBase[] { TrophyPity, TrophyFlatMode, TrophyGrowth, TrophyRareThreshold, TrophyRareGrowth, TrophyFlatBonus,
                TrophyGuarantee, TrophyExtraKillsPerStar, TrophyChanceMultiplier, TrophyOnlyMine, TrophyOnlyUncollected, TrophyShowChances,
                TrophyShowMissing, TrophySpoilerFree, TrophyHidden, TrophyNotify, TrophyShowProgress })
                ConfigText.CarryOver(Config, before, "14 - Trophies", entry.Definition.Key, entry);
            if (!before.ContainsKey(trophies + "|Pity") && !before.ContainsKey("14 - Trophies|Pity")) ConfigText.ImportTrophyAssister(Config);

            const string fishing = "19 - Spearfishing";
            FishSpears = Config.Bind(fishing, "Spears", true,
                Synced("A thrown spear that hits a fish in the water kills it; the fish floats up to be picked up. Modded spears count too."));
            FishHarpoons = Config.Bind(fishing, "Harpoons", true,
                Synced("A harpoon that hits a fish in the water pulls it straight into your inventory (or leaves it floating when there's no room)."));
            FishBows = Config.Bind(fishing, "Bows", true, Synced("Arrows kill fish in the water the same way."));
            FishCrossbows = Config.Bind(fishing, "Crossbows", true, Synced("Crossbow bolts kill fish in the water the same way."));
            if (!before.ContainsKey(fishing + "|Spears")) ConfigText.ImportSpearfishing(Config);

            const string loot = "22 - Loot and items";
            LootCreatures = Config.Bind(loot, "Creature drops", 1f,
                Synced("Multiplies what creatures drop, after the game's own roll (stars and world modifiers included). 1 = the game's normal, 3 = three times as much; 1.5 = one and a half times on average. Items dropped once per player stay as they are.",
                    new AcceptableValueRange<float>(1f, 10f)));
            LootResources = Config.Bind(loot, "Resource drops", 1f,
                Synced("Multiplies what trees, logs, rocks, ore veins, destroyed objects and dungeon loot piles drop. 1 = the game's normal.", new AcceptableValueRange<float>(1f, 10f)));
            LootPickables = Config.Bind(loot, "Pickable drops", 1f,
                Synced("Multiplies what you pick: berries, mushrooms, crops, and flint, stones and branches lying on the ground. 1 = the game's normal.", new AcceptableValueRange<float>(1f, 10f)));
            LootTrophies = Config.Bind(loot, "Multiply trophies too", false,
                Synced("Off: trophies always drop one at a time. On: the creature multiplier applies to trophies too."));
            LootNever = Config.Bind(loot, "Never multiplied", "chest_hildir1, chest_hildir2, chest_hildir3",
                Synced("Item prefab names, separated by commas, that are never multiplied. Hildir's quest chests are in it by default."));
            LootOnly = Config.Bind(loot, "Only multiply these", "",
                Synced("Item prefab names, separated by commas. When set, only these items are multiplied; empty = every item."));
            ItemWeight = Config.Bind(loot, "Item weight", 1f,
                Synced("Multiplies every item's weight: 0.7 = 30% lighter, 0 = weightless. 1 = the game's normal. Applies to every item at once, carried, crafted or dropped.",
                    new AcceptableValueRange<float>(0f, 1f)));
            ItemStackSize = Config.Bind(loot, "Stack size", 1,
                Synced("Multiplies how many of an item fit in one stack (items that stack at all): 2 = 100 wood per stack instead of 50. 1 = the game's normal.", new AcceptableValueRange<int>(1, 10)));
            PickupRangeSetting = Config.Bind(loot, "Auto-pickup range", 2f,
                Synced("Meters around a player that items are picked up automatically. 2 = the game's normal.", new AcceptableValueRange<float>(1f, 10f)));
            ItemsFloat = Config.Bind(loot, "Items float in water", false,
                Synced("Every dropped item floats instead of sinking. Applies to items dropped from now on."));
            if (!before.ContainsKey(loot + "|Creature drops")) ConfigText.ImportDropMoreLoot(Config);

            const string rows = "23 - Inventory rows";
            RowsEarlier = Config.Bind(rows, "Earlier rows from Haldor", true,
                Synced("Haldor's two inventory rows unlock after the Elder (1,000 coins) and Bonemass (2,000) instead of Moder and the Queen. Off: the game's own timing."));
            RowsThird = Config.Bind(rows, "Third row from Haldor", true,
                Synced("Haldor also sells a third row, Roomier pockets, once per character. A character that bought it with EpicLoot Progression Fix keeps it and can't buy it again."));
            RowsThirdPrice = Config.Bind(rows, "Third row price", 3000,
                Synced("Coins for Roomier pockets.", new AcceptableValueRange<int>(1, 100000)));
            RowsThirdBoss = Config.Bind(rows, "Third row unlock", "defeated_goblinking",
                Synced("World key that must be set before Haldor offers Roomier pockets: defeated_goblinking = Yagluth, defeated_queen = the Queen, defeated_fader = Fader; empty = always."));

            const string sleep = "24 - Sleeping";
            SleepShare = Config.Bind(sleep, "Share of players needed", 50,
                Synced("Percent of players who must be in bed or emoting to skip the night. Only they sleep through it; everyone else keeps playing. When everyone is in bed the night is skipped as usual. 100 with Beds only = the game's normal.",
                    new AcceptableValueRange<int>(1, 100)));
            SleepVoteMode = Config.Bind(sleep, "Emotes count", SleepVotes.AnyEmote,
                Synced("Who counts besides players in bed: AnyEmote = anyone using an emote and standing still (moving cancels it); SittingOnly = anyone sitting; BedsOnly = beds only."));
            SleepInBed = Config.Bind(sleep, "Players in bed needed", 1,
                Synced("At least this many players must be in an actual bed before emotes count.", new AcceptableValueRange<int>(1, 10)));

            const string stands = "25 - Item stands";
            ItemStandMode = Config.Bind(stands, "Items allowed", ItemStandItems.All,
                Synced("Which items go on item stands: All = any item; Compatible = only items that have a stand model; Vanilla = the game's normal."));
            ItemStandIds = Config.Bind(stands, "Stands", "itemstand,itemstandh,Placeable_HardRock",
                Synced("Which item stands this applies to, by prefab name, separated by commas: itemstand (wall), itemstandh (horizontal), Placeable_HardRock (pet rock)."));
            ItemStandAdjust = Config.Bind(stands, "Adjustments", true,
                Synced("Players can resize, turn and move the item on a stand with the itemstand_scale, itemstand_rotation and itemstand_offset console commands (outside other players' wards), and the default adjustments below apply. Off: items sit as placed. Many adjusted stands can cost some performance."));
            ItemStandMaxOffset = Config.Bind(stands, "Largest offset", 2f,
                Synced("Meters an item can be moved from its place with itemstand_offset. 0 = no limit.", new AcceptableValueRange<float>(0f, 20f)));
            ItemStandMaxScale = Config.Bind(stands, "Largest size", 5f,
                Synced("Times its normal size an item can be made with itemstand_scale. 0 = no limit.", new AcceptableValueRange<float>(0f, 50f)));
            ItemStandTransformations = Config.Bind(stands, "Default adjustments", "WolfFang,0,-0.254",
                Synced("Adjustments for every stand holding an item, separated by |: id,distance,offset_x,offset_y,angle_1,angle_2,angle_3,scale_1,scale_2,scale_3. Example: Wood,0,0,0,0,0,0,2,2,2 shows wood twice as big."));
            ItemStandLegacy = Config.Bind(stands, "Simple models", false,
                Synced("Uses the item's first model part only. Try this if items look wrong on stands; some items then can't go on stands or show only part of their model."));
            ItemStandHide = Config.Bind(stands, "Hide stands holding an item", false,
                "Hides the stand itself while it holds an item, so only the item shows. itemstand_hide sets this per stand for everyone. Personal.");
            ItemStandCloser = Config.Bind(stands, "Items closer to the wall", false,
                "Items sit against the stand instead of slightly in front of it. Personal.");

            const string wisp = "26 - Wisplight";
            WispLevels = Config.Bind(wisp, "Upgrade levels", 5,
                Synced("Highest level the Wisplight can be upgraded to at the workbench. Level 1 is the game's own; 1 = not upgradeable, the game's normal.", new AcceptableValueRange<int>(1, 10)));
            WispRangePerLevel = Config.Bind(wisp, "Clearing per level", 5f,
                Synced("Meters of extra mist clearing around the wisp for each level above 1 (the game's own is about 10 m, so 5 = about 30 m at level 5).", new AcceptableValueRange<float>(0f, 25f)));
            WispWispsPerLevel = Config.Bind(wisp, "Wisps per level", 5,
                Synced("Wisps each upgrade costs, rising with the level the way the game's upgrade costs do (level 5 costs four and a half times this).", new AcceptableValueRange<int>(0, 50)));
            WispSilverPerLevel = Config.Bind(wisp, "Silver per level", 10,
                Synced("Silver each upgrade costs, rising the same way.", new AcceptableValueRange<int>(0, 100)));
            WispEitrPerLevel = Config.Bind(wisp, "Eitr per level", 0,
                Synced("Refined eitr each upgrade costs, rising the same way. 0 = none.", new AcceptableValueRange<int>(0, 50)));

            if (epicLoot)
            {
                const string table = "20 - Epic Loot enchanting table", enchantments = "21 - Epic Loot enchantments";
                ChestMaterialsEnabled = Config.Bind(table, "Materials from nearby chests", true,
                    Synced("The enchanting table also takes its costs (enchanting, augmenting, upgrading, sacrificing...) from built chests near you that you can open and nobody else has open."));
                ChestMaterialsRange = Config.Bind(table, "Chest range", 50f,
                    Synced("Meters around you that chests are used from.", new AcceptableValueRange<float>(5f, 100f)));
                ChestTrophies = Config.Bind(table, "Chest trophies in Sacrifice", true,
                    "The Sacrifice tab also lists trophies from those chests. The same as the \"Include trophies from chests\" checkbox in that tab. Personal.");
                ChestShardstones = Config.Bind(table, "Chest shardstones in Sacrifice", false,
                    "The Sacrifice tab also lists shardstones from those chests (they turn into shards). Off by default so \"Select All\" can't sacrifice stored shardstones by accident. The same as the \"Include shardstones from chests\" checkbox. Personal.");
                SacrificePerItem = Config.Bind(table, "Double output per item", true,
                    Synced("The table's chance of double output is rolled for every sacrificed item instead of once for the whole batch (all doubled or nothing). Same average, no all-or-nothing gamble."));

                DamageStacking = Config.Bind(enchantments, "Damage enchantments stack", true,
                    Synced("A weapon can roll several damage enchantments together (fire and frost and +% damage, even the same one twice); Epic Loot normally allows only one. Double Magic Shot and Triple Shot are then weighted twice as much, so they stay as likely as before. Only changes new rolls (drops, enchanting, augmenting)."));
                ShotNormalCost = Config.Bind(enchantments, "Double and Triple Shot normal cost", true,
                    Synced("When Double Magic Shot (staffs) or Triple Shot (bows, crossbows) fires its extra projectiles, Epic Loot charges three times the eitr or stamina. On: just the normal cost, so these enchantments are an upgrade, not a trade. Triple Shot still uses 3 arrows."));
                ShotSummonLimit = Config.Bind(enchantments, "Double Magic Shot doubles the summon limit", true,
                    Synced("A summoning staff (the Staff of the Wild's roots) allows a set number of summons at once. Double Magic Shot fires two per cast, so with it the limit doubles."));
                ShardsIgnoreClashes = Config.Bind(enchantments, "Shardstones ignore effect clashes", true,
                    Synced("A shardstone can be socketed even when its effect normally clashes with an effect the item already has (a damage shard on a weapon with a damage enchantment). Runestones, enchanting and loot keep the normal rules."));
                LuckyLootBirds = Config.Bind(enchantments, "Lucky Loot on birds", true,
                    Synced("Birds (crows, seagulls) can get Lucky Loot too, with the same chance and drop multiplier as creatures. Epic Loot itself only does creatures."));
                LuckyLootMessage = Config.Bind(enchantments, "Lucky Loot message", true,
                    "Show a message when the Lucky Loot shard multiplies drops near you, like \"Lucky Loot! Boar: 6x drops, +2 magic item rolls\". Personal.");
                LuckyLootPosition = Config.Bind(enchantments, "Lucky Loot message position", MessageHud.MessageType.Center,
                    "Center: a big message in the middle of the screen. TopLeft: a small line in the top left corner. Personal.");
                EitrTooltip = Config.Bind(enchantments, "Eitr cost in tooltips", true,
                    "Show the eitr cost of enchanted staffs and Spellsword weapons in their tooltip (Epic Loot shows it only for Spellsword). Personal.");
                if (!before.ContainsKey(table + "|Materials from nearby chests")) ConfigText.ImportEpicLootAddons(Config);
            }

            GlowEnabled.SettingChanged += (_, __) => UpgradeGlow.RefreshAll();
            GlowStrength.SettingChanged += (_, __) => UpgradeGlow.RefreshAll();

            harmony = new Harmony(Id);
            HarmonyInstance = harmony;
            Patch(typeof(CameraZoom));
            Patch(typeof(DroppedItemLifetime));
            Patch(typeof(ShipAndCartRemoval));
            Patch(typeof(UpgradeGlow));
            Patch(typeof(UpgradeGlow.KeepOriginals));
            foreach (var type in new[] { typeof(AttackCancel.SkipCancelled), typeof(AttackCancel.Register), typeof(AttackCancel.Controls), typeof(AttackCancel.Waiting) })
                Patch(type);
            if (GameFixesEnabled.Value)
                foreach (var type in GameFixes.Patches) Patch(type);
            foreach (var type in QualityOfLife.Patches) Patch(type);
            Patch(typeof(InventoryRows));
            foreach (var type in SleepVote.Patches) Patch(type);
            Patch(typeof(InventorySlotsVisibility));
            Patch(typeof(StatusEffectList));
            foreach (var type in new[] { typeof(TreeDust), typeof(TreeDust.Impact), typeof(TreeDust.Created), typeof(SmelterSettings), typeof(SmelterSettings.Output),
                typeof(FermenterSettings), typeof(StationRange), typeof(MapReveal), typeof(DeathMarkers) })
                Patch(type);
            Run("Auto-pickup key", AutoPickupKey.Initialize);
            EnableUnlessInstalled(DimmerModId, "Dimmer Combat Particle Effects", "combat effects dimmer", () => { foreach (var type in CombatEffects.Patches) Patch(type); });
            EnableUnlessInstalled(DoubleBed.DoubleDragonBed.OriginalModId, "Double Dragon Bed", "double dragon bed", () =>
            {
                if (BedEnabled.Value) DoubleBed.DoubleDragonBed.Enable(Patch);
            });
            EnableUnlessInstalled(Trophies.OriginalModId, "TrophyAssister", "trophy pity", () => { foreach (var type in Trophies.Patches) Patch(type); });
            EnableUnlessInstalled(Spearfishing.OriginalModId, "Spearfishing", "spearfishing", () => Patch(typeof(Spearfishing)));
            EnableUnlessInstalled(Wisplight.OriginalModId, "Better Wisps", "Wisplight upgrades", () =>
            {
                Wisplight.Initialize();
                foreach (var type in Wisplight.Patches) Patch(type);
            });
            EnableUnlessInstalled(ItemStands.OriginalModId, "Item Stand All Items", "item stand settings", () =>
            {
                ItemStands.Initialize();
                foreach (var type in ItemStands.Patches) Patch(type);
                foreach (var command in ItemStands.Commands()) CommandManager.Instance.AddConsoleCommand(command);
            });
            EnableUnlessInstalled(LootAndItems.OriginalModId, "DropMoreLoot", "loot and item settings", () =>
            {
                LootAndItems.Initialize();
                foreach (var type in LootAndItems.Patches) Patch(type);
            });
            if (QuickStackSlots.Wanted()) Run("Quick Stack Store leaves Equipment and Quick Slots' slots alone", () => QuickStackSlots.Enable(harmony));
            Run("Tombstone items back in their slots", () =>
            {
                // Looks up Equipment & Quick Slots' slot layout first, so a changed layout turns this feature off cleanly.
                System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(GraveSlotRecovery).TypeHandle);
                foreach (var type in QualityOfLife.GravePatches) Patch(type);
            });
            if (epicLoot) EnableEpicLoot();

            if (Chainloader.PluginInfos.ContainsKey(TombRetrieverId))
                Log.LogWarning("Tomb Retriever is installed, so Potato Tweaks' tombstone retrieval stays off. Remove Tomb Retriever to use it.");
            else
            {
                TombRetrieval.Active = true;
                Patch(typeof(TombRetrieval.RegisterRpcs));
                CommandManager.Instance.AddConsoleCommand(new TombRetrieval.Command("retrievetomb"));
                CommandManager.Instance.AddConsoleCommand(new TombRetrieval.Command("gettomb"));
            }

            if (Chainloader.PluginInfos.ContainsKey(SkillLossId))
                Log.LogWarning("SkillLoss is installed, so Potato Tweaks' skill settings stay off. Remove SkillLoss to use them.");
            else
            {
                Patch(typeof(SkillSettings.DeathLoss));
                Patch(typeof(SkillSettings.XpGain));
            }
        }

        // A feature that another installed mod already provides stays off, with a note in the log.
        private void EnableUnlessInstalled(string otherId, string otherName, string feature, Action enable)
        {
            if (Chainloader.PluginInfos.ContainsKey(otherId))
                Log.LogWarning($"{otherName} is installed, so Potato Tweaks' {feature} stays off. Remove {otherName} to use it.");
            else Run(feature, enable);
        }

        // Kept in its own method so Epic Loot's types are only looked at when Epic Loot is installed.
        private void EnableEpicLoot()
        {
            EpicLootFeatures.Enable(Patch, Run);
            if (EpicLootFixesEnabled.Value) EpicLootFixes.Enable(Patch, Run);
            string addons = EpicLootAddons.InstalledOriginal();
            if (addons != null)
                Log.LogWarning($"{addons} is installed, so Potato Tweaks' Epic Loot add-ons (enchanting table chests, damage stacking, shot cost, Lucky Loot, shard clashes, eitr tooltip, faster chest loading, hiding enchantment looks) stay off. Remove {addons} to use them.");
            else EpicLootAddons.Enable(Patch, Run);
        }

        private void Run(string name, Action initialize)
        {
            try { initialize(); }
            catch (Exception e) { Log.LogError($"{name} is disabled: {e.GetBaseException().Message}"); }
        }

        // Each feature is patched on its own, so one that no longer matches the game cannot stop the others.
        private void Patch(Type type)
        {
            try { harmony.CreateClassProcessor(type).Patch(); }
            catch (Exception e) { Log.LogError($"{type.Name} is disabled: {e.GetBaseException().Message}"); }
        }

        private void Update() => TombRetrieval.Update();

        private void OnDestroy() => harmony?.UnpatchSelf();
    }
}
