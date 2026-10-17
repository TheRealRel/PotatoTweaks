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
    [BepInIncompatibility("valheim.epiclootprogressionfix")]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Id = "potato.tweaks";
        public const string Version = "1.2.0";
        private const string TombRetrieverId = "ryan.tombretriever";
        private const string SkillLossId = "Mirfin.SkillLoss";

        internal static ManualLogSource Log;

        // Personal settings (each player chooses their own).
        internal static ConfigEntry<bool> CameraEnabled;
        internal static ConfigEntry<float> ShipZoomDistance, LandZoomMultiplier;
        internal static ConfigEntry<bool> GlowEnabled;
        internal static ConfigEntry<float> GlowStrength;

        // Server-controlled settings.
        internal static ConfigEntry<bool> DroppedItemsEnabled, ShipRemovalEnabled, TombEnabled;
        internal static ConfigEntry<float> DroppedItemLifetimeHours;
        internal static ConfigEntry<int> TombCooldownSeconds;
        internal static ConfigEntry<bool> AttackCancelEnabled;
        internal static ConfigEntry<bool> BountyMainKillOnly, BountySpreadOut, BountyGuaranteedItem, BountyNormalScaling, BountyHealthBar;
        internal static ConfigEntry<float> BountySpacing;
        internal static ConfigEntry<int> BountyExtraCopies;
        internal static ConfigEntry<bool> GameFixesEnabled, EpicLootFixesEnabled;

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

            GlowEnabled = Config.Bind("7 - Upgrade glow", "Enabled", true,
                "Use the glow strength below for gear upgraded past its normal max level (the game makes such gear glow brighter with every extra level).");
            GlowStrength = Config.Bind("7 - Upgrade glow", "Glow strength", 1f,
                new ConfigDescription("Strength of that glow: its particles, colour and light. 1 = the game's normal glow, 0.5 = half, 0 = no glow at all, 2 = twice as strong. Only changes what you see.",
                    new AcceptableValueRange<float>(0f, 2f)));
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
