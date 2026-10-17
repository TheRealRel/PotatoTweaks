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
    [BepInIncompatibility("valheim.epiclootprogressionfix")]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Id = "potato.tweaks";
        public const string Version = "1.3.0";
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
            ConfigText.CarryOver(Config, before, "7 - Upgrade glow", "Enabled", GlowEnabled);
            ConfigText.CarryOver(Config, before, "7 - Upgrade glow", "Glow strength", GlowStrength);
            if (!Chainloader.PluginInfos.ContainsKey(DimmerModId) && !before.ContainsKey(visual + "|Combat effects opacity"))
                ConfigText.ImportDimmer(Config);
            AttackCancelEnabled = Config.Bind("8 - Attack cancel", "Enabled", true,
                Synced("Pressing block during an attack's windup or recovery abandons the attack and raises the guard at once, so you can parry mid-swing. A hit the swing hasn't reached never lands; stamina already paid and a hit already dealt stay. Cancelling never lets you attack faster."));

            bool epicLoot = Chainloader.PluginInfos.ContainsKey(EpicLootFeatures.EpicLootId);
            GameFixesEnabled = Config.Bind("10 - Bug fixes", "Game bug fixes", true,
                "Fixes for bugs in Valheim: dropped fast bow shots, swings stopped by the deck of the ship you stand on, broken fuling shaman shields still blocking, plants that stop growing, buried ore veins losing support, berry bushes slow to regrow. Takes effect after a restart; keep it the same on the server and every player's game.");
            if (epicLoot)
                EpicLootFixesEnabled = Config.Bind("10 - Bug fixes", "Epic Loot enchantment fixes", true,
                    "Fixes for Epic Loot enchantments: Riches, Fire Rate, Noise, +Ranged Skills, Meads Cooldown, Indestructible, Bountiful Harvest, Quick Draw crossbow reload and Air Jump near the ground. Takes effect after a restart; keep it the same on the server and every player's game.");
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
            QolTamedCrops = Config.Bind(qol, "Tamed animals spare crops", true,
                Synced("Your tamed animals walking through farm crops and planted bushes don't trample them. Wild animals and attacks still do damage."));
            QolSwimSpeed = Config.Bind(qol, "Swim skill raises swim speed", true,
                Synced("Swimming speed rises with the Swim skill the way running speed rises with Run: up to +25% at level 100."));

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
            CartHealthBonus = Config.Bind(vehicles, "Cart health from Crafting skill", 4f,
                Synced("New carts get extra health from their builder's Crafting skill: this value times the skill share, so 4 = +400% at Crafting 100, +200% at 50. Changing it rescales existing crafted carts. 0 = off.",
                    new AcceptableValueRange<float>(0f, 10f)));
            BoatHealthBonus = Config.Bind(vehicles, "Boat health from Crafting skill", 1f,
                Synced("New boats (raft, karve, longship, drakkar) get extra health the same way: 1 = +100% at Crafting 100, +50% at 50. Changing it rescales existing crafted boats. 0 = off.",
                    new AcceptableValueRange<float>(0f, 10f)));

            const string bed = "17 - Double dragon bed";
            BedEnabled = Config.Bind(bed, "Enabled", true,
                "Each half of the dragon bed is a bed of its own: two players can claim it, sleep side by side and respawn on their own half. One player owning both halves sleeps in the middle. Shift+E on your half gives it up. Read at startup; keep it the same on the server and every player's game.");
            BedBlankets = Config.Bind(bed, "Blankets", true,
                "The fur draped over each half's edge rolls up at the sleeper's feet and unrolls over them, shaped to what they wear. Only looks. Personal.");
            if (!before.ContainsKey(bed + "|Blankets")) ConfigText.CopyBool(Config, "hansenbansen.doublebed.cfg", "Blankets", "Enabled", BedBlankets);

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
            if (!before.ContainsKey(trophies + "|Pity")) ConfigText.ImportTrophyAssister(Config);

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
            foreach (var type in new[] { typeof(TreeDust), typeof(TreeDust.Impact), typeof(TreeDust.Created), typeof(SmelterSettings), typeof(SmelterSettings.Output),
                typeof(FermenterSettings), typeof(StationRange), typeof(MapReveal) })
                Patch(type);
            Run("Auto-pickup key", AutoPickupKey.Initialize);
            EnableUnlessInstalled(DimmerModId, "Dimmer Combat Particle Effects", "combat effects dimmer", () => { foreach (var type in CombatEffects.Patches) Patch(type); });
            EnableUnlessInstalled(DoubleBed.DoubleDragonBed.OriginalModId, "Double Dragon Bed", "double dragon bed", () =>
            {
                if (BedEnabled.Value) DoubleBed.DoubleDragonBed.Enable(Patch);
            });
            EnableUnlessInstalled(Trophies.OriginalModId, "TrophyAssister", "trophy pity", () => { foreach (var type in Trophies.Patches) Patch(type); });
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
