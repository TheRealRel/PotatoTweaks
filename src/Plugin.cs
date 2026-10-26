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
    [BepInDependency(EssenceForAll.OriginalModId, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(ComfortHelper.OriginalModId, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInIncompatibility("valheim.epiclootprogressionfix")]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Id = "potato.tweaks";
        public const string Version = "1.5.17";
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
        internal static ConfigEntry<bool> BountyMainKillOnly, BountySpreadOut, BountyGuaranteedItem, BountyNormalScaling, BountyHealthBar;
        internal static ConfigEntry<float> BountySpacing;
        internal static ConfigEntry<int> BountyExtraCopies;
        internal static ConfigEntry<bool> GameFixesEnabled, EpicLootFixesEnabled, TidyConfigs;
        internal static ConfigEntry<string> SmokeThroughPieces;
        internal static ConfigEntry<bool> QolNoAutoEquip, QolLiveBuildMenu, QolBuildToolHealthBars, QolCombatHealthBars, QolTamingHover,
            QolGraveSlots, QolAutoPickupKey, QolTamedCrops, QolSwimSpeed;
        internal static ConfigEntry<float> QolAttackerRange, QolTreeDust;
        internal static ConfigEntry<string> SmelterExcluded;
        internal static ConfigEntry<bool> BlastFurnaceTakesAll, CraftableChain;
        internal static ConfigEntry<float> WindmillSpeed, WindmillCapacity, WindmillOutput, WindmillMinWind;
        internal static ConfigEntry<float> SmelterOreCapacity, SmelterFuelCapacity, SmelterFuelPerProduct, SmelterOutput, SmelterSpeed, FermenterSpeed, FermenterOutput, StationRange, MapRevealRadius,
            CartHealthBonus, BoatHealthBonus, MapSailingRadius;
        internal static ConfigEntry<bool> FishSpears, FishHarpoons, FishBows, FishCrossbows;
        internal static ConfigEntry<bool> ChestMaterialsEnabled, SacrificePerItem, DamageStacking, ShotNormalCost, ShotSummonLimit, ShardsIgnoreClashes, LuckyLootBirds;
        internal static ConfigEntry<float> ChestMaterialsRange;
        internal static ConfigEntry<float> LootCreatures, LootResources, LootPickables, ItemWeight, PickupRangeSetting;
        internal static ConfigEntry<bool> LootTrophies, ItemsFloat;
        internal static ConfigEntry<string> LootNever, LootOnly, EssenceItems;
        internal static ConfigEntry<bool> EssenceForEveryone;
        internal static ConfigEntry<float> EssencePlayerRange, ComfortMessageCooldown;
        internal static ConfigEntry<bool> ComfortTab, ComfortChecksEverywhere, ComfortHoverInfo, ComfortRestingMessage;
        internal static ConfigEntry<string> ComfortTabName;
        internal static ConfigEntry<int> ComfortSuggestions;
        internal static ConfigEntry<bool> RecycleEnabled, RecycleUpgrades, RecycleConfirmHotbar;
        internal static ConfigEntry<float> RecycleShare;
        internal static ConfigEntry<UnityEngine.KeyCode> RecycleKey;
        internal static ConfigEntry<bool> TrayEnabled;
        internal static ConfigEntry<bool> ChestNamesEnabled, ChestContentsEnabled, DoorsCloseEnabled, StationTimersEnabled;
        internal static ConfigEntry<KeyboardShortcut> ChestNameKey;
        internal static ConfigEntry<int> ChestIconSize, ChestIconCount;
        internal static ConfigEntry<float> DoorsCloseSeconds;
        internal static ConfigEntry<int> ItemStackSize;
        internal static ConfigEntry<bool> RowsEarlier, RowsThird, InventorySlotsVisibility, DeathMarkerButton;
        internal static ConfigEntry<int> RowsThirdPrice;
        internal static ConfigEntry<string> RowsThirdBoss;
        internal static ConfigEntry<int> SleepShare, SleepInBed;
        internal static ConfigEntry<bool> StatusEffectNamesHidden;
        internal static ConfigEntry<float> QolSneakSpeed, WispRangePerLevel;
        internal static ConfigEntry<int> WispLevels, WispWispsPerLevel, WispSilverPerLevel, WispEitrPerLevel;
        internal static ConfigEntry<float> ItemStandMaxOffset, ItemStandMaxScale;
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
            // The settings file before this start; "before" also lists old lines under their new names (ConfigLayout).
            var rawBefore = ConfigText.Read(Config.ConfigFilePath);
            var before = ConfigLayout.WithAliases(rawBefore);

            CameraEnabled = Config.Bind("1 - General", "Camera zoom", true,
                "Lets the camera zoom out farther, on land and on ships.");
            LandZoomMultiplier = Config.Bind("1 - General", "Camera zoom multiplier on land", 1.5f,
                new ConfigDescription("Multiplies the game's farthest camera zoom off ships (8 m). 1.5 = 12 m; 1 keeps the native limit.",
                    new AcceptableValueRange<float>(1f, 5f)));
            ShipZoomDistance = Config.Bind("1 - General", "Camera zoom distance on ships", 24f,
                new ConfigDescription("Farthest camera zoom in meters for the helmsman and passengers on a ship.",
                    new AcceptableValueRange<float>(1f, 100f)));

            DroppedItemsEnabled = Config.Bind("4 - Items and loot", "Dropped items last longer", true,
                Synced("Use Dropped item lifetime hours instead of the game's 1 hour for dropped items."));
            DroppedItemLifetimeHours = Config.Bind("4 - Items and loot", "Dropped item lifetime hours", 3f,
                Synced("Hours of world time a dropped item lasts before the game removes it. As in the game, an item inside a base, within 25 m of a player, in tar or part of a build stays. The game's own lifetime is 1 hour, which a night's sleep can skip at once.",
                    new AcceptableValueRange<float>(0.1f, 1000f)));

            ShipRemovalEnabled = Config.Bind("1 - General", "Ship and cart removal", true,
                Synced("Hold Ctrl and use the hammer's remove button to take apart a raft, karve, longship, drakkar or cart. The game's normal rules still apply: a workbench in range, no ward or no-build zone, nobody aboard a raft. Every material is refunded and the cargo drops in crates."));

            TombEnabled = Config.Bind("1 - General", "Tombstone retrieval", true,
                Synced("Players can type retrievetomb (or gettomb) in the F5 console, or /retrievetomb in chat, to bring their own tombstones to where they stand."));
            TombCooldownSeconds = Config.Bind("1 - General", "Tombstone retrieval cooldown seconds", 10,
                Synced("Seconds a player must wait between retrievals. 0 turns the wait off.", new AcceptableValueRange<int>(0, 3600)));

            SkillSettings.Bind(Config, (text, range) => Synced(text, range));

            GlowEnabled = Config.Bind("2 - Visual effects", "Upgrade glow", true,
                "Use Upgrade glow strength for gear upgraded past its normal max level (the game makes such gear glow brighter with every extra level). Personal.");
            GlowStrength = Config.Bind("2 - Visual effects", "Upgrade glow strength", 1f,
                new ConfigDescription("Strength of that glow: its particles, colour and light. 1 = the game's normal glow, 0.5 = half, 0 = no glow at all, 2 = twice as strong. Personal.",
                    new AcceptableValueRange<float>(0f, 2f)));
            QolTreeDust = Config.Bind("2 - Visual effects", "Tree-felling dust", 1f,
                new ConfigDescription("Dust and debris from trees and logs being hit, falling, landing and breaking. 1 = the game's normal amount, 0.5 = half, 0 = none. Sounds stay. Personal.",
                    new AcceptableValueRange<float>(0f, 1f)));
            DimEnabled = Config.Bind("2 - Visual effects", "Combat effects dimmer", true,
                "Make the particles that cover creatures in a fight fainter, so their attack wind-ups stay visible: set how much with Combat effects opacity, brightness and size (all 1 = the game's normal). Applies from the next hit. Personal.");
            DimOpacity = Config.Bind("2 - Visual effects", "Combat effects opacity", 1f,
                new ConfigDescription("1 = fully visible (the game's normal), 0.35 = faint, 0 = invisible. Personal.", new AcceptableValueRange<float>(0f, 1f)));
            DimBrightness = Config.Bind("2 - Visual effects", "Combat effects brightness", 1f,
                new ConfigDescription("1 = the game's normal, lower = darker. Personal.", new AcceptableValueRange<float>(0f, 1f)));
            DimSize = Config.Bind("2 - Visual effects", "Combat effects size", 1f,
                new ConfigDescription("1 = the game's normal, lower = smaller particles. Personal.", new AcceptableValueRange<float>(0.1f, 1f)));
            DimStatus = Config.Bind("2 - Visual effects", "Dim status effects on creatures", true,
                "The flames of a burning creature, frost and poison clouds and the burst on every damage tick. Personal.");
            DimCreatureHits = Config.Bind("2 - Visual effects", "Dim creature hit bursts", true, "Blood, sparks and the critical and sneak-attack bursts when a creature is hit. Personal.");
            DimWeapons = Config.Bind("2 - Visual effects", "Dim weapon impacts", true, "What a player's weapon throws off where it hits, elemental bursts included. Personal.");
            DimImpacts = Config.Bind("2 - Visual effects", "Dim projectile and area impacts", true, "Arrows, bolts, staff shots, bombs and area attacks from players where they hit. Personal.");
            DimOnPlayers = Config.Bind("2 - Visual effects", "Also dim effects on players", false, "Also dim status effects and hit bursts on players, yourself included. Personal.");
            bool epicLoot = Chainloader.PluginInfos.ContainsKey(EpicLootFeatures.EpicLootId);
            if (epicLoot)
            {
                HideGlowing = Config.Bind("2 - Visual effects", "Hide Glowing enchantment", false,
                    "Hide the light and sparkles of Epic Loot's Glowing enchantment, on everyone. Only on your screen; the enchantment still works. Personal.");
                HideEnchantEffects = Config.Bind("2 - Visual effects", "Hide all enchantment effects", false,
                    "Hide every visual effect of Epic Loot enchantments and legendary items (auras, flames, sparkles), on everyone. Only on your screen. Personal.");
                HideLegendaryLooks = Config.Bind("2 - Visual effects", "Hide legendary looks", false,
                    "Show Epic Loot legendary items with their normal look instead of their special texture (like the blue Heimdall armor). Only on your screen. Personal.");
            }
            ConfigText.CarryOver(Config, before, "7 - Upgrade glow", "Enabled", GlowEnabled);
            ConfigText.CarryOver(Config, before, "7 - Upgrade glow", "Glow strength", GlowStrength);
            if (!Chainloader.PluginInfos.ContainsKey(DimmerModId) && !before.ContainsKey("2 - Visual effects|Combat effects opacity"))
                ConfigText.ImportDimmer(Config);
            // Attack cancel moved to PotatoFighter in 1.5.17. PotatoFighter copies the old choice on its first start; once its
            // settings file has it, the old line here is dropped.
            ConfigText.DropMoved(Config, "potato.fighter.cfg", "[2 - Combat]", "8 - Attack cancel", "Enabled");

            GameFixesEnabled = Config.Bind("1 - General", "Game bug fixes", true,
                "Fixes for bugs in Valheim: swings stopped by the deck of the ship you stand on, broken fuling shaman shields still blocking, plants that stop growing, buried ore veins losing support, berry bushes slow to regrow; also other mods' mining arrows and bolts that never dig (Warfare). Takes effect after a restart; keep it the same on the server and every player's game.");
            TidyConfigs = Config.Bind("1 - General", "Tidy other mods config files", true,
                "At startup, fixes small things in other mods' config files that make them warn on every launch, without changing what they do: tabs in LetMeTameYou's tame list comments, and berry bushes listed twice in StarLevelSystem's location resets. Personal.");
            if (TidyConfigs.Value) ConfigTidy.Run();
            if (epicLoot)
                EpicLootFixesEnabled = Config.Bind("9 - Epic Loot", "Enchantment fixes", true,
                    "Fixes for Epic Loot enchantments: Riches, Fire Rate, Noise, +Ranged Skills, Meads Cooldown, Indestructible, Bountiful Harvest, Quick Draw crossbow reload and Air Jump near the ground. Takes effect after a restart; keep it the same on the server and every player's game.");
            if (epicLoot)
                FasterChestLoading = Config.Bind("9 - Epic Loot", "Faster chest loading", true,
                    "Epic Loot goes through every item of every loaded chest each time the game checks it for changes (once a second, more often with crafting-from-chests mods), even when nothing changed. On: it only does so when the chest's items really changed, with the same result. Personal.");
            if (epicLoot)
            {
                BountyMainKillOnly = Config.Bind("9 - Epic Loot", "Bounty complete on main target", true,
                    Synced("A bounty completes when its main target dies; its minions are optional. Off: Epic Loot's rule, every minion must die too."));
                BountySpreadOut = Config.Bind("9 - Epic Loot", "Bounties spread out", true,
                    Synced("Place each new bounty evenly across its biome's distance band, at least Bounty spacing from every open bounty and unopened treasure chest. Off: Epic Loot's own placement, where players' bounties pile onto the same few spots."));
                BountySpacing = Config.Bind("9 - Epic Loot", "Bounty spacing", 300f,
                    Synced("Meters a new bounty keeps from other open bounties and treasure chests, or as far as possible when no spot is that far.", new AcceptableValueRange<float>(0f, 5000f)));
                BountyExtraCopies = Config.Bind("9 - Epic Loot", "Bounty extra enchanted copies", 1,
                    Synced("Each enchanted equipment drop from a bounty's main target is rolled again this many times, each copy with its own enchantments. 0 = Epic Loot's normal drops.", new AcceptableValueRange<int>(0, 5)));
                BountyGuaranteedItem = Config.Bind("9 - Epic Loot", "Bounty guaranteed enchanted item", true,
                    Synced("A bounty's main target always drops at least one enchanted item from its own equipment loot."));
                BountyNormalScaling = Config.Bind("9 - Epic Loot", "Bounty normal star scaling", true,
                    Synced("With StarLevelSystem: bounty targets keep Epic Loot's extra health after reloading and scale with stars like ordinary creatures instead of bosses. Their boss health bar stays."));
                BountyHealthBar = Config.Bind("9 - Epic Loot", "Bounty overhead health bar", true,
                    "Show a small name and health bar above bounty targets, as on ordinary creatures, besides the boss bar at the top of the screen. Personal.");
            }

            QolNoAutoEquip = Config.Bind("4 - Items and loot", "No auto-equip on pickup", true,
                "Walking over a club, pickaxe or torch with empty hands doesn't put it in your hands. Personal.");
            QolLiveBuildMenu = Config.Bind("3 - Interface", "Live hammer menu", true,
                "Hammer piece icons update as you walk in and out of workbench or chest range, without reopening the menu. Personal.");
            QolBuildToolHealthBars = Config.Bind("3 - Interface", "Health bars with build tools", true,
                "Creature health bars still show while you hold the hammer, hoe or cultivator. Personal.");
            QolCombatHealthBars = Config.Bind("3 - Interface", "Combat health bars", true,
                "Alerted enemies and animals being tamed (or tamed) keep their health bars through mist, trees and buildings; the ground and rock still hide them. Personal.");
            QolAttackerRange = Config.Bind("3 - Interface", "Attacker health bar range", 15f,
                new ConfigDescription("An enemy attacking you within this many meters shows its health bar through anything. Personal.", new AcceptableValueRange<float>(0f, 100f)));
            QolTamingHover = Config.Bind("3 - Interface", "Taming info at range", true,
                "See an animal's taming progress from health-bar range, and pet or rename big animals while standing beside them. Personal.");
            QolGraveSlots = Config.Bind("1 - General", "Tombstone items back in their slots", true,
                "Recovering your own tombstone returns every item to the slot it had when you died; items picked up since then move aside. Personal.");
            QolAutoPickupKey = Config.Bind("4 - Items and loot", "Auto-pickup key with Equipment & Quick Slots", true,
                "When a quick slot uses V, auto-pickup moves to ] (right bracket), so pressing V on an empty quick slot can't switch auto-pickup off. Read at startup. Personal.");
            InventorySlotsVisibility = Config.Bind("3 - Interface", "Inventory slot visibility", true,
                "No inventory slot stays hidden: when a window covers equipment, quick, quiver, backpack, chest or inventory-row slots, or pushes them past the screen edge, that window moves just enough to show them, at any screen size from 16:9 to 32:9 and any GUI scale. It moves back when no longer in the way, and each move is noted once in the log. Only looks again when the layout changes. Personal.");
            StatusEffectNamesHidden = Config.Bind("3 - Interface", "Hide status effect names", true,
                "Status effect icons stay where the game puts them, beside the minimap, but without their names (which get cut short). The time left, or the comfort level while resting, still shows on each icon. Personal.");
            QolTamedCrops = Config.Bind("1 - General", "Tamed animals spare crops", true,
                Synced("Your tamed animals walking through farm crops and planted bushes don't trample them. Wild animals and attacks still do damage."));
            QolSwimSpeed = Config.Bind("1 - General", "Swim skill raises swim speed", true,
                Synced("Swimming speed rises with the Swim skill the way running speed rises with Run: up to +25% at level 100."));
            QolSneakSpeed = Config.Bind("1 - General", "Sneak skill raises sneak speed", 100f,
                Synced("Percent faster sneaking at Sneak level 100, rising with the skill: 100 = 2 m/s becomes 4 m/s, as fast as jogging but quiet; 50 = 3 m/s. Not while over-encumbered. 0 = off, the game's normal.",
                    new AcceptableValueRange<float>(0f, 150f)));

            SmelterOreCapacity = Config.Bind("8 - Stations and building", "Smelter ore capacity", 1f,
                Synced("Multiplies how much ore (or wood, flax, sap...) smelter-type stations hold: smelter, blast furnace, charcoal kiln, eitr refinery, spinning wheel. Windmills have their own settings. 1 = the game's normal, 2 = twice as much. Applies as stations load.",
                    new AcceptableValueRange<float>(0.5f, 10f)));
            SmelterFuelCapacity = Config.Bind("8 - Stations and building", "Smelter fuel capacity", 1f,
                Synced("Multiplies how much fuel (coal, wood) these stations hold. 1 = the game's normal. Applies as stations load.", new AcceptableValueRange<float>(0.5f, 10f)));
            SmelterSpeed = Config.Bind("8 - Stations and building", "Smelter speed", 1f,
                Synced("Speeds up these stations: 2 = twice as fast. 1 = the game's normal. Applies as stations load.", new AcceptableValueRange<float>(0.5f, 10f)));
            SmelterFuelPerProduct = Config.Bind("8 - Stations and building", "Smelter fuel per product", 1f,
                Synced("Multiplies the fuel used for each product, rounded and never below 1 where the game uses fuel: 0.5 makes the smelter use 1 coal per bar instead of 2. 1 = the game's normal. Applies as stations load.",
                    new AcceptableValueRange<float>(0.1f, 5f)));
            SmelterOutput = Config.Bind("8 - Stations and building", "Smelter output", 1f,
                Synced("Multiplies what each product gives: 2 = two bars per ore, 1.5 = a 50% chance of a second one. 1 = the game's normal.", new AcceptableValueRange<float>(1f, 10f)));
            WindmillSpeed = Config.Bind("8 - Stations and building", "Windmill speed", 2f,
                Synced("Speeds up windmills grinding barley into flour: 2 = half the time. 1 = the game's normal. Applies as windmills load.", new AcceptableValueRange<float>(0.5f, 10f)));
            WindmillCapacity = Config.Bind("8 - Stations and building", "Windmill capacity", 1f,
                Synced("Multiplies how much barley a windmill holds. 1 = the game's normal. Applies as windmills load.", new AcceptableValueRange<float>(0.5f, 10f)));
            WindmillOutput = Config.Bind("8 - Stations and building", "Windmill output", 1f,
                Synced("Multiplies the flour each barley gives: 1.5 = a 50% chance of a second one. 1 = the game's normal.", new AcceptableValueRange<float>(1f, 10f)));
            WindmillMinWind = Config.Bind("8 - Stations and building", "Windmill minimum wind", 0f,
                Synced("The least power a windmill runs at, however calm the wind: 0.5 = never below half speed. Cover over the sails still slows it. 0 = the game's normal.", new AcceptableValueRange<float>(0f, 1f)));
            SmelterExcluded = Config.Bind("8 - Stations and building", "Smelter excluded stations", "",
                Synced("Stations these settings leave alone, by prefab name, separated by commas: smelter, blastfurnace, charcoal_kiln, eitrrefinery, piece_spinningwheel, windmill (or a modded station's prefab name). A windmill listed here ignores the windmill settings too."));
            BlastFurnaceTakesAll = Config.Bind("8 - Stations and building", "Blast furnace takes all ores", true,
                Synced("The blast furnace also smelts everything the regular smelter does: copper, tin, iron and silver ore and the scraps. Applies as blast furnaces load. Idea from Blast Furnace Takes All by TastyChickenLegs and Tueman."));
            FermenterSpeed = Config.Bind("8 - Stations and building", "Fermenter speed", 1f,
                Synced("Speeds up fermenting: 2 = half the time. 1 = the game's normal. Applies as fermenters load.", new AcceptableValueRange<float>(0.5f, 10f)));
            FermenterOutput = Config.Bind("8 - Stations and building", "Fermenter output", 1f,
                Synced("Multiplies the meads each batch gives, rounded: 1.5 turns 6 into 9. 1 = the game's normal. Applies as fermenters load. (A fermenter always holds one batch, so there's no capacity setting.)", new AcceptableValueRange<float>(0.5f, 5f)));
            StationRange = Config.Bind("8 - Stations and building", "Crafting station build range", 1f,
                Synced("Multiplies the build range around workbenches, forges and other crafting stations, extension bonuses included. 1 = the game's normal; 1.5 = half again as far.",
                    new AcceptableValueRange<float>(1f, 5f)));
            CraftableChain = Config.Bind("8 - Stations and building", "Craftable chain", true,
                Synced("Chain can be made at the forge from 2 Iron, one per craft. Off: chains only drop, as in the game. Changes take effect right away. Idea from Chain Manager by Digitalroot."));
            MapRevealRadius = Config.Bind("1 - General", "Map reveal radius", 1f,
                Synced("Multiplies how far around you the map is revealed as you explore (game: 100 m). 1 = the game's normal; 1.5 = 150 m.", new AcceptableValueRange<float>(1f, 5f)));
            MapSailingRadius = Config.Bind("1 - General", "Map reveal radius while sailing", 500f,
                Synced("How far around you (metres) the map is revealed while you or someone within 20 m is steering a ship. The sea has little to see up close, so charting coasts goes much faster. 0 = the normal radius.",
                    new AcceptableValueRange<float>(0f, 1000f)));
            DeathMarkerButton = Config.Bind("1 - General", "Map button to remove death markers", true,
                "Adds a button by the skull icon on the large map that removes all death markers (click twice to confirm). Your own pins stay. Personal.");
            CartHealthBonus = Config.Bind("1 - General", "Cart health from Crafting skill", 4f,
                Synced("New carts get extra health from their builder's Crafting skill: this value times the skill share, so 4 = +400% at Crafting 100, +200% at 50. Changing it rescales existing crafted carts. 0 = off.",
                    new AcceptableValueRange<float>(0f, 10f)));
            BoatHealthBonus = Config.Bind("1 - General", "Boat health from Crafting skill", 1f,
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

            BedEnabled = Config.Bind("8 - Stations and building", "Double dragon bed", true,
                "Each half of the dragon bed is a bed of its own: two players can claim it, sleep side by side and respawn on their own half. One player owning both halves sleeps in the middle. Shift+E on your half gives it up. Read at startup; keep it the same on the server and every player's game.");
            BedBlankets = Config.Bind("8 - Stations and building", "Double dragon bed blankets", true,
                "The fur draped over each half's edge rolls up at the sleeper's feet and unrolls over them, shaped to what they wear. Only looks. Personal.");
            ConfigText.CarryOver(Config, before, "13 - Double dragon bed", "Enabled", BedEnabled);
            ConfigText.CarryOver(Config, before, "13 - Double dragon bed", "Blankets", BedBlankets);
            if (!before.ContainsKey("8 - Stations and building|Double dragon bed blankets") && !before.ContainsKey("13 - Double dragon bed|Blankets")) ConfigText.CopyBool(Config, "hansenbansen.doublebed.cfg", "Blankets", "Enabled", BedBlankets);

            TrophyPity = Config.Bind("5 - Trophies", "Pity", true,
                Synced("Every kill that drops no trophy makes that creature's next trophy more likely; the count resets when it drops."));
            TrophyFlatMode = Config.Bind("5 - Trophies", "Flat growth", false,
                Synced("Off: each dry kill multiplies the chance (10% -> 11% -> 12.1% with 0.1 growth). On: each dry kill adds Flat bonus per dry kill."));
            TrophyGrowth = Config.Bind("5 - Trophies", "Growth per dry kill", 0.1f,
                Synced("Multiplying growth per dry kill: 0.1 = +10% of the current chance.", new AcceptableValueRange<float>(0f, 2f)));
            TrophyRareThreshold = Config.Bind("5 - Trophies", "Rare trophy threshold", 0.05f,
                Synced("Trophies whose normal chance is at or below this (0.05 = 5%) use Rare growth per dry kill.", new AcceptableValueRange<float>(0f, 1f)));
            TrophyRareGrowth = Config.Bind("5 - Trophies", "Rare growth per dry kill", 0.2f,
                Synced("Multiplying growth per dry kill for rare trophies.", new AcceptableValueRange<float>(0f, 2f)));
            TrophyFlatBonus = Config.Bind("5 - Trophies", "Flat bonus per dry kill", 0.05f,
                Synced("With flat growth: chance added per dry kill, 0.05 = +5 percentage points.", new AcceptableValueRange<float>(0f, 1f)));
            TrophyGuarantee = Config.Bind("5 - Trophies", "Guarantee after dry kills", 0,
                Synced("A trophy always drops after this many dry kills. 0 = off.", new AcceptableValueRange<int>(0, 1000)));
            TrophyExtraKillsPerStar = Config.Bind("5 - Trophies", "Extra dry kills per star", 1,
                Synced("A starred creature that drops no trophy counts as this many extra dry kills per star. 0 = one kill each.", new AcceptableValueRange<int>(0, 10)));
            TrophyChanceMultiplier = Config.Bind("5 - Trophies", "Chance multiplier", 1f,
                Synced("Multiplies the normal trophy chance before pity. 1 = the game's normal.", new AcceptableValueRange<float>(0f, 10f)));
            TrophyOnlyMine = Config.Bind("5 - Trophies", "Only count my kills", true,
                Synced("Kills count for the player who made them: the last hit (burning and poison after a hit count), or their tames and summons. This works in multiplayer even when the creature is run by another player's game. On: kills with no player behind them (other creatures, falls, traps) count for no one. Off: they count for the player whose game runs the creature."));
            TrophyOnlyUncollected = Config.Bind("5 - Trophies", "Only uncollected trophies", false,
                Synced("Pity and the chance multiplier only apply to trophies this character has never picked up."));
            TrophyShowChances = Config.Bind("5 - Trophies", "Show chances in Trophies tab", true, "Each trophy shows its next drop chance and dry kills. Personal.");
            TrophyShowMissing = Config.Bind("5 - Trophies", "Show missing trophies", true, "Trophies you haven't collected yet show greyed out in the place they'll take. Personal.");
            TrophySpoilerFree = Config.Bind("5 - Trophies", "Spoiler-free", true,
                "Missing trophies of creatures you've never killed show as a dark shape named ??????; after your first kill they show greyed out with their name. Personal.");
            TrophyHidden = Config.Bind("5 - Trophies", "Hidden trophies", "TrophyDeerWhite",
                "Trophy prefab names, separated by commas, never shown as missing. TrophyDeerWhite is unfinished game content. Personal.");
            TrophyNotify = Config.Bind("5 - Trophies", "Message on drop", false, "Show a message when a trophy drops. Personal.");
            TrophyShowProgress = Config.Bind("5 - Trophies", "Message on dry kill", false, "After a kill without a trophy, show the dry kills and the new chance. Personal.");
            foreach (var entry in new ConfigEntryBase[] { TrophyPity, TrophyFlatMode, TrophyGrowth, TrophyRareThreshold, TrophyRareGrowth, TrophyFlatBonus,
                TrophyGuarantee, TrophyExtraKillsPerStar, TrophyChanceMultiplier, TrophyOnlyMine, TrophyOnlyUncollected, TrophyShowChances,
                TrophyShowMissing, TrophySpoilerFree, TrophyHidden, TrophyNotify, TrophyShowProgress })
                ConfigText.CarryOver(Config, before, "14 - Trophies", entry.Definition.Key, entry);
            if (!before.ContainsKey("5 - Trophies|Pity") && !before.ContainsKey("14 - Trophies|Pity")) ConfigText.ImportTrophyAssister(Config);

            FishSpears = Config.Bind("4 - Items and loot", "Spearfishing with spears", true,
                Synced("A thrown spear that hits a fish in the water kills it; the fish floats up to be picked up. Modded spears count too."));
            FishHarpoons = Config.Bind("4 - Items and loot", "Spearfishing with harpoons", true,
                Synced("A harpoon that hits a fish in the water pulls it straight into your inventory (or leaves it floating when there's no room)."));
            FishBows = Config.Bind("4 - Items and loot", "Spearfishing with bows", true, Synced("Arrows kill fish in the water the same way."));
            FishCrossbows = Config.Bind("4 - Items and loot", "Spearfishing with crossbows", true, Synced("Crossbow bolts kill fish in the water the same way."));
            if (!before.ContainsKey("4 - Items and loot|Spearfishing with spears")) ConfigText.ImportSpearfishing(Config);

            LootCreatures = Config.Bind("4 - Items and loot", "Creature drops", 1f,
                Synced("Multiplies what creatures drop, after the game's own roll (stars and world modifiers included). 1 = the game's normal, 3 = three times as much; 1.5 = one and a half times on average. Items dropped once per player stay as they are.",
                    new AcceptableValueRange<float>(1f, 10f)));
            LootResources = Config.Bind("4 - Items and loot", "Resource drops", 1f,
                Synced("Multiplies what trees, logs, rocks, ore veins, destroyed objects and dungeon loot piles drop. 1 = the game's normal.", new AcceptableValueRange<float>(1f, 10f)));
            LootPickables = Config.Bind("4 - Items and loot", "Pickable drops", 1f,
                Synced("Multiplies what you pick: berries, mushrooms, crops, and flint, stones and branches lying on the ground. 1 = the game's normal.", new AcceptableValueRange<float>(1f, 10f)));
            LootTrophies = Config.Bind("4 - Items and loot", "Multiply trophies too", false,
                Synced("Off: trophies always drop one at a time. On: the creature multiplier applies to trophies too."));
            LootNever = Config.Bind("4 - Items and loot", "Never multiplied", "chest_hildir1, chest_hildir2, chest_hildir3",
                Synced("Item prefab names, separated by commas, that are never multiplied. Hildir's quest chests are in it by default."));
            LootOnly = Config.Bind("4 - Items and loot", "Only multiply these", "",
                Synced("Item prefab names, separated by commas. When set, only these items are multiplied; empty = every item."));
            ItemWeight = Config.Bind("4 - Items and loot", "Item weight", 1f,
                Synced("Multiplies every item's weight: 0.7 = 30% lighter, 0 = weightless. 1 = the game's normal. Applies to every item at once, carried, crafted or dropped.",
                    new AcceptableValueRange<float>(0f, 1f)));
            ItemStackSize = Config.Bind("4 - Items and loot", "Stack size", 1,
                Synced("Multiplies how many of an item fit in one stack (items that stack at all): 2 = 100 wood per stack instead of 50. 1 = the game's normal.", new AcceptableValueRange<int>(1, 10)));
            PickupRangeSetting = Config.Bind("4 - Items and loot", "Auto-pickup range", 2f,
                Synced("Meters around a player that items are picked up automatically. 2 = the game's normal.", new AcceptableValueRange<float>(1f, 10f)));
            ItemsFloat = Config.Bind("4 - Items and loot", "Items float in water", false,
                Synced("Every dropped item floats instead of sinking. Applies to items dropped from now on."));
            EssenceForEveryone = Config.Bind("4 - Items and loot", "Essence for everyone", true,
                Synced("Thunderblood and Frostfire essence always drop from the creatures that carry them, one for each player who was nearby when the creature appeared (stars still add the game's extra). The creature drop multiplier doesn't apply to them."));
            EssenceItems = Config.Bind("4 - Items and loot", "Essence items", "OrbThunderBlood, OrbFrostFire",
                Synced("Item prefab names, separated by commas, that Essence for everyone applies to. The log lists which creatures carry them as the game starts."));
            EssencePlayerRange = Config.Bind("4 - Items and loot", "Essence player range", 200f,
                Synced("Players within this many meters of a creature when it appears each get one essence. 0 = every player in the world.", new AcceptableValueRange<float>(0f, 1000f)));
            if (!before.ContainsKey("4 - Items and loot|Creature drops")) ConfigText.ImportDropMoreLoot(Config);

            RowsEarlier = Config.Bind("4 - Items and loot", "Earlier rows from Haldor", true,
                Synced("Haldor's two inventory rows unlock after the Elder (1,000 coins) and Bonemass (2,000) instead of Moder and the Queen. Off: the game's own timing."));
            RowsThird = Config.Bind("4 - Items and loot", "Third row from Haldor", true,
                Synced("Haldor also sells a third row, Roomier pockets, once per character. A character that bought it with EpicLoot Progression Fix keeps it and can't buy it again."));
            RowsThirdPrice = Config.Bind("4 - Items and loot", "Third row price", 3000,
                Synced("Coins for Roomier pockets.", new AcceptableValueRange<int>(1, 100000)));
            RowsThirdBoss = Config.Bind("4 - Items and loot", "Third row unlock", "defeated_goblinking",
                Synced("World key that must be set before Haldor offers Roomier pockets: defeated_goblinking = Yagluth, defeated_queen = the Queen, defeated_fader = Fader; empty = always."));

            SleepShare = Config.Bind("1 - General", "Sleep share of players needed", 50,
                Synced("Percent of players who must be in bed or emoting to skip the night. Only they sleep through it; everyone else keeps playing. When everyone is in bed the night is skipped as usual. 100 with Beds only = the game's normal.",
                    new AcceptableValueRange<int>(1, 100)));
            SleepVoteMode = Config.Bind("1 - General", "Sleep emotes count", SleepVotes.AnyEmote,
                Synced("Who counts besides players in bed: AnyEmote = anyone using an emote and standing still (moving cancels it); SittingOnly = anyone sitting; BedsOnly = beds only."));
            SleepInBed = Config.Bind("1 - General", "Sleep players in bed needed", 1,
                Synced("At least this many players must be in an actual bed before emotes count.", new AcceptableValueRange<int>(1, 10)));

            ItemStandMode = Config.Bind("8 - Stations and building", "Item stand items allowed", ItemStandItems.All,
                Synced("Which items go on item stands: All = any item; Compatible = only items that have a stand model; Vanilla = the game's normal."));
            ItemStandIds = Config.Bind("8 - Stations and building", "Item stand pieces", "itemstand,itemstandh,Placeable_HardRock",
                Synced("Which item stands this applies to, by prefab name, separated by commas: itemstand (wall), itemstandh (horizontal), Placeable_HardRock (pet rock)."));
            ItemStandAdjust = Config.Bind("8 - Stations and building", "Item stand adjustments", true,
                Synced("Players can resize, turn and move the item on a stand with the itemstand_scale, itemstand_rotation and itemstand_offset console commands (outside other players' wards), and the default adjustments below apply. Off: items sit as placed. Many adjusted stands can cost some performance."));
            ItemStandMaxOffset = Config.Bind("8 - Stations and building", "Item stand largest offset", 2f,
                Synced("Meters an item can be moved from its place with itemstand_offset. 0 = no limit.", new AcceptableValueRange<float>(0f, 20f)));
            ItemStandMaxScale = Config.Bind("8 - Stations and building", "Item stand largest size", 5f,
                Synced("Times its normal size an item can be made with itemstand_scale. 0 = no limit.", new AcceptableValueRange<float>(0f, 50f)));
            ItemStandTransformations = Config.Bind("8 - Stations and building", "Item stand default adjustments", "WolfFang,0,-0.254",
                Synced("Adjustments for every stand holding an item, separated by |: id,distance,offset_x,offset_y,angle_1,angle_2,angle_3,scale_1,scale_2,scale_3. Example: Wood,0,0,0,0,0,0,2,2,2 shows wood twice as big."));
            ItemStandLegacy = Config.Bind("8 - Stations and building", "Item stand simple models", false,
                Synced("Uses the item's first model part only. Try this if items look wrong on stands; some items then can't go on stands or show only part of their model."));
            ItemStandHide = Config.Bind("8 - Stations and building", "Item stand hidden when holding an item", false,
                "Hides the stand itself while it holds an item, so only the item shows. itemstand_hide sets this per stand for everyone. Personal.");
            ItemStandCloser = Config.Bind("8 - Stations and building", "Item stand items closer to the wall", false,
                "Items sit against the stand instead of slightly in front of it. Personal.");

            ComfortTab = Config.Bind("3 - Interface", "Comfort tab", true,
                "A Comfort tab in the build menu with every comfort piece, one category per comfort group (only the best piece of a group within 10 m counts). Check marks show what is nearby: green counts, grey is outdone by a better piece of its group. Read at startup. Personal.");
            ComfortTabName = Config.Bind("3 - Interface", "Comfort tab name", "Comfort", "Name of the Comfort tab. Read at startup. Personal.");
            ComfortChecksEverywhere = Config.Bind("3 - Interface", "Comfort check marks in every tab", false, "Also show the check marks on comfort pieces in the other build menu tabs. Personal.");
            ComfortHoverInfo = Config.Bind("3 - Interface", "Comfort in piece descriptions", true,
                "Comfort pieces say in their build menu description what they are worth and what they would add where you stand. Personal.");
            ComfortRestingMessage = Config.Bind("3 - Interface", "Comfort message when resting", true,
                "When you start resting, a message shows your comfort and the pieces you know that would raise it most. /comfort in chat lists them all any time. Personal.");
            ComfortMessageCooldown = Config.Bind("3 - Interface", "Comfort message cooldown", 600f,
                new ConfigDescription("Seconds before the resting message shows again. Personal.", new AcceptableValueRange<float>(0f, 3600f)));
            ComfortSuggestions = Config.Bind("3 - Interface", "Comfort message pieces", 4,
                new ConfigDescription("Pieces listed in the resting message (/comfort lists all). Personal.", new AcceptableValueRange<int>(1, 20)));

            RecycleEnabled = Config.Bind("4 - Items and loot", "Recycling", true,
                Synced("With a crafting station open, Ctrl + right-click an item in your inventory to take it apart at the station it is made at and get back part of its materials. Items made by hand are recycled with just the inventory open."));
            RecycleShare = Config.Bind("4 - Items and loot", "Recycling materials returned", 50f,
                Synced("Percent of the materials given back, rounded down (at least one of the main material).", new AcceptableValueRange<float>(0f, 100f)));
            RecycleUpgrades = Config.Bind("4 - Items and loot", "Recycling counts upgrades", true,
                Synced("Upgrades count toward what's given back. The idols and other materials only the Forge of Potential uses aren't returned."));
            RecycleKey = Config.Bind("4 - Items and loot", "Recycle key", UnityEngine.KeyCode.LeftControl, "Hold this and right-click an item to recycle it. Personal.");
            RecycleConfirmHotbar = Config.Bind("4 - Items and loot", "Recycling confirms hotbar items", true,
                "Ask before recycling an item on your hotbar. Enchanted items always ask. Personal.");

            TrayEnabled = Config.Bind("8 - Stations and building", "Serving tray takes every crafted food", true,
                Synced("The serving tray can set out every crafted food that has no place on it yet, from the game and from other mods, like the game's own dishes: one item, eaten and gone. Raw food and food cooked on the cooking station are left out. Takes effect on the next game start."));

            ChestNamesEnabled = Config.Bind("3 - Interface", "Chest names", true,
                Synced("Name a chest by looking at it and pressing the name key. Everyone sees the name in place of the chest's kind. A private chest can only be named by its owner."));
            ChestNameKey = Config.Bind("3 - Interface", "Chest name key", new KeyboardShortcut(UnityEngine.KeyCode.N, UnityEngine.KeyCode.LeftControl),
                "Look at a chest and press this to name it. Leave the name empty to clear it. Personal.");
            ChestContentsEnabled = Config.Bind("3 - Interface", "Chest contents", true,
                "Show a row of small item icons with counts under a chest's name when you look at it. Private chests show theirs only to their owner. Personal.");
            ChestIconSize = Config.Bind("3 - Interface", "Chest icon size", 24,
                new ConfigDescription("Size of the content icons in pixels. The inventory's own are about 64. Personal.", new AcceptableValueRange<int>(16, 48)));
            ChestIconCount = Config.Bind("3 - Interface", "Chest most icons", 8,
                new ConfigDescription("Most items shown, the biggest stacks first; the rest show as +N more. Personal.", new AcceptableValueRange<int>(1, 16)));

            DoorsCloseEnabled = Config.Bind("8 - Stations and building", "Doors close themselves", true,
                Synced("A door you open shuts on its own after a few seconds, once nobody is standing in the doorway. Only doors players built; doors that need a key are left alone."));
            DoorsCloseSeconds = Config.Bind("8 - Stations and building", "Door seconds open", 5f,
                Synced("How long a door stays open before it closes.", new AcceptableValueRange<float>(1f, 60f)));

            StationTimersEnabled = Config.Bind("3 - Interface", "Station timers", true,
                "Show the time left when you look at a working smelter, kiln, furnace, spinning wheel, windmill, fermenter or cooking station. Personal.");

            SmokeThroughPieces = Config.Bind("8 - Stations and building", "Smoke passes through", "darkwood_decowall,iron_floor_1x1,iron_floor_2x2,iron_grate,iron_wall_1x1,iron_wall_2x2,wood_stair,wood_stepladder",
                Synced("Pieces that smoke drifts up through, and that don't count as covering a fire below them: object ids separated by commas (cage floors and walls, the iron gate, the darkwood divider, stairs and ladders by default). Empty = the game's normal."));

            WispLevels = Config.Bind("8 - Stations and building", "Wisplight upgrade levels", 5,
                Synced("Highest level the Wisplight can be upgraded to at the workbench. Level 1 is the game's own; 1 = not upgradeable, the game's normal.", new AcceptableValueRange<int>(1, 10)));
            WispRangePerLevel = Config.Bind("8 - Stations and building", "Wisplight clearing per level", 5f,
                Synced("Meters of extra mist clearing around the wisp for each level above 1 (the game's own is about 10 m, so 5 = about 30 m at level 5).", new AcceptableValueRange<float>(0f, 25f)));
            WispWispsPerLevel = Config.Bind("8 - Stations and building", "Wisplight wisps per level", 5,
                Synced("Wisps each upgrade costs, rising with the level the way the game's upgrade costs do (level 5 costs four and a half times this).", new AcceptableValueRange<int>(0, 50)));
            WispSilverPerLevel = Config.Bind("8 - Stations and building", "Wisplight silver per level", 10,
                Synced("Silver each upgrade costs, rising the same way.", new AcceptableValueRange<int>(0, 100)));
            WispEitrPerLevel = Config.Bind("8 - Stations and building", "Wisplight eitr per level", 0,
                Synced("Refined eitr each upgrade costs, rising the same way. 0 = none.", new AcceptableValueRange<int>(0, 50)));

            if (epicLoot)
            {
                ChestMaterialsEnabled = Config.Bind("9 - Epic Loot", "Enchanting materials from nearby chests", true,
                    Synced("The enchanting table also takes its costs (enchanting, augmenting, upgrading, sacrificing...) from built chests near you that you can open and nobody else has open."));
                ChestMaterialsRange = Config.Bind("9 - Epic Loot", "Enchanting chest range", 50f,
                    Synced("Meters around you that chests are used from.", new AcceptableValueRange<float>(5f, 100f)));
                ChestTrophies = Config.Bind("9 - Epic Loot", "Chest trophies in Sacrifice", true,
                    "The Sacrifice tab also lists trophies from those chests. The same as the \"Include trophies from chests\" checkbox in that tab. Personal.");
                ChestShardstones = Config.Bind("9 - Epic Loot", "Chest shardstones in Sacrifice", false,
                    "The Sacrifice tab also lists shardstones from those chests (they turn into shards). Off by default so \"Select All\" can't sacrifice stored shardstones by accident. The same as the \"Include shardstones from chests\" checkbox. Personal.");
                SacrificePerItem = Config.Bind("9 - Epic Loot", "Sacrifice double output per item", true,
                    Synced("The table's chance of double output is rolled for every sacrificed item instead of once for the whole batch (all doubled or nothing). Same average, no all-or-nothing gamble."));

                DamageStacking = Config.Bind("9 - Epic Loot", "Damage enchantments stack", true,
                    Synced("A weapon can roll several damage enchantments together (fire and frost and +% damage, even the same one twice); Epic Loot normally allows only one. Double Magic Shot and Triple Shot are then weighted twice as much, so they stay as likely as before. Only changes new rolls (drops, enchanting, augmenting)."));
                ShotNormalCost = Config.Bind("9 - Epic Loot", "Double and Triple Shot normal cost", true,
                    Synced("When Double Magic Shot (staffs) or Triple Shot (bows, crossbows) fires its extra projectiles, Epic Loot charges three times the eitr or stamina. On: just the normal cost, so these enchantments are an upgrade, not a trade. Triple Shot still uses 3 arrows."));
                ShotSummonLimit = Config.Bind("9 - Epic Loot", "Double Magic Shot doubles the summon limit", true,
                    Synced("A summoning staff (the Staff of the Wild's roots) allows a set number of summons at once. Double Magic Shot fires two per cast, so with it the limit doubles."));
                ShardsIgnoreClashes = Config.Bind("9 - Epic Loot", "Shardstones ignore effect clashes", true,
                    Synced("A shardstone can be socketed even when its effect normally clashes with an effect the item already has (a damage shard on a weapon with a damage enchantment). Runestones, enchanting and loot keep the normal rules."));
                LuckyLootBirds = Config.Bind("9 - Epic Loot", "Lucky Loot on birds", true,
                    Synced("Birds (crows, seagulls) can get Lucky Loot too, with the same chance and drop multiplier as creatures. Epic Loot itself only does creatures."));
                LuckyLootMessage = Config.Bind("9 - Epic Loot", "Lucky Loot message", true,
                    "Show a message when the Lucky Loot shard multiplies drops near you, like \"Lucky Loot! Boar: 6x drops, +2 magic item rolls\". Personal.");
                LuckyLootPosition = Config.Bind("9 - Epic Loot", "Lucky Loot message position", MessageHud.MessageType.Center,
                    "Center: a big message in the middle of the screen. TopLeft: a small line in the top left corner. Personal.");
                EitrTooltip = Config.Bind("9 - Epic Loot", "Eitr cost in tooltips", true,
                    "Show the eitr cost of enchanted staffs and Spellsword weapons in their tooltip (Epic Loot before 0.15.2 shows it only for Spellsword; 0.15.2 and later show it themselves, and then this does nothing). Personal.");
                if (!before.ContainsKey("9 - Epic Loot|Enchanting materials from nearby chests")) ConfigText.ImportEpicLootAddons(Config);
            }

            ConfigLayout.Apply(Config, rawBefore);

            GlowEnabled.SettingChanged += (_, __) => UpgradeGlow.RefreshAll();
            GlowStrength.SettingChanged += (_, __) => UpgradeGlow.RefreshAll();

            harmony = new Harmony(Id);
            HarmonyInstance = harmony;
            Patch(typeof(CameraZoom));
            Patch(typeof(DroppedItemLifetime));
            Patch(typeof(ShipAndCartRemoval));
            Patch(typeof(UpgradeGlow));
            Patch(typeof(UpgradeGlow.KeepOriginals));
            if (GameFixesEnabled.Value)
                foreach (var type in GameFixes.Patches) Patch(type);
            foreach (var type in QualityOfLife.Patches) Patch(type);
            Patch(typeof(InventoryRows));
            foreach (var type in SleepVote.Patches) Patch(type);
            Patch(typeof(InventorySlotsVisibility));
            Patch(typeof(StatusEffectList));
            foreach (var type in new[] { typeof(TreeDust), typeof(TreeDust.Impact), typeof(TreeDust.Created), typeof(SmelterSettings), typeof(SmelterSettings.Output), typeof(SmelterSettings.WindPower),
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
            foreach (var type in Recycling.Patches) Patch(type);
            foreach (var type in ServingTray.Patches) Patch(type);
            foreach (var type in Chests.Patches) Patch(type);
            foreach (var type in AutoDoors.Patches) Patch(type);
            foreach (var type in StationTimers.Patches) Patch(type);
            EnableUnlessInstalled(Forging.BlastFurnaceModId, "Blast Furnace Takes All", "blast furnace taking all ores", () => Patch(typeof(Forging.BlastFurnaceTakesAll)));
            EnableUnlessInstalled(Forging.ChainModId, "Chain Manager", "craftable chain", Forging.AddChainRecipe);
            EnableUnlessInstalled(SmokeThrough.OriginalModId, "Smoke Collision", "smoke passing through", () => { foreach (var type in SmokeThrough.Patches) Patch(type); });
            EnableUnlessInstalled(ComfortHelper.OriginalModId, "ComfortHelper", "comfort helper", () =>
            {
                foreach (var type in ComfortHelper.Patches) Patch(type);
                ComfortHelper.RegisterCommand();
            });
            EnableUnlessInstalled(EssenceForAll.OriginalModId, "EssenceForAll", "essence for everyone", () =>
            {
                EssenceForAll.Initialize();
                foreach (var type in EssenceForAll.Patches) Patch(type);
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

        private void Update()
        {
            TombRetrieval.Update();
            try { Chests.Update(); } catch (Exception e) { Log.LogError($"Chest names: {e.GetBaseException().Message}"); }
            try { AutoDoors.Update(); } catch (Exception e) { Log.LogError($"Doors closing: {e.GetBaseException().Message}"); }
        }

        private void OnDestroy() => harmony?.UnpatchSelf();
    }
}
