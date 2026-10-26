// Potato Tweaks - reading older settings files, so settings carry over when a section moves or a player switches from
// a mod Potato Tweaks now includes.
using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;

namespace PotatoTweaks
{
    internal static class ConfigText
    {
        // "section|key" -> value, from a BepInEx .cfg file; empty when the file doesn't exist.
        internal static Dictionary<string, string> Read(string path)
        {
            var values = new Dictionary<string, string>();
            try
            {
                if (!File.Exists(path)) return values;
                string section = "";
                foreach (var raw in File.ReadAllLines(path))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#")) continue;
                    if (line.StartsWith("[") && line.EndsWith("]")) { section = line.Substring(1, line.Length - 2); continue; }
                    int equals = line.IndexOf('=');
                    if (equals > 0) values[section + "|" + line.Substring(0, equals).Trim()] = line.Substring(equals + 1).Trim();
                }
            }
            catch (Exception e) { Plugin.Log.LogWarning($"Couldn't read {Path.GetFileName(path)}: {e.Message}"); }
            return values;
        }

        private static void Set(ConfigEntryBase entry, string value)
        {
            try { entry.BoxedValue = TomlTypeConverter.ConvertToValue(value, entry.SettingType); }
            catch (Exception e) { Plugin.Log.LogWarning($"Couldn't copy '{value}' into {entry.Definition.Key}: {e.Message}"); }
        }

        // A setting that moved: copy the old value once, then drop the old line from the file.
        internal static void CarryOver(ConfigFile config, Dictionary<string, string> before, string oldSection, string oldKey, ConfigEntryBase entry)
        {
            string newKey = entry.Definition.Section + "|" + entry.Definition.Key;
            if (before.ContainsKey(newKey) || !before.TryGetValue(oldSection + "|" + oldKey, out var value)) return;
            Set(entry, value);
            if (AccessTools.Property(typeof(ConfigFile), "OrphanedEntries")?.GetValue(config) is Dictionary<ConfigDefinition, string> orphans)
                orphans.Remove(new ConfigDefinition(oldSection, oldKey));
            config.Save();
        }

        // A setting that moved to another mod: once that mod's settings file has it (it copies the old value itself), drop the
        // old line from this file. Until then it stays, so the other mod can still read it whichever loads first.
        internal static void DropMoved(ConfigFile config, string otherFile, string otherSection, string oldSection, string oldKey)
        {
            try
            {
                string other = Path.Combine(Paths.ConfigPath, otherFile);
                if (!File.Exists(other) || !File.ReadAllText(other).Contains(otherSection)) return;
                if (AccessTools.Property(typeof(ConfigFile), "OrphanedEntries")?.GetValue(config) is Dictionary<ConfigDefinition, string> orphans &&
                    orphans.Remove(new ConfigDefinition(oldSection, oldKey)))
                    config.Save();
            }
            catch (Exception e) { Plugin.Log.LogWarning($"Couldn't tidy the moved setting {oldSection} / {oldKey}: {e.Message}"); }
        }

        internal static void CopyBool(ConfigFile config, string otherFile, string section, string key, ConfigEntry<bool> entry)
        {
            var other = Read(Path.Combine(Paths.ConfigPath, otherFile));
            if (other.TryGetValue(section + "|" + key, out var value)) Set(entry, value);
        }

        private static void Copy(Dictionary<string, string> other, string section, string key, ConfigEntryBase entry)
        {
            if (other.TryGetValue(section + "|" + key, out var value)) Set(entry, value);
        }

        // Only when switching from Dimmer Combat Particle Effects: its settings file is still in the config folder.
        internal static void ImportDimmer(ConfigFile config)
        {
            var other = Read(Path.Combine(Paths.ConfigPath, Plugin.DimmerModId + ".cfg"));
            if (other.Count == 0) return;
            Copy(other, "1 - General", "Enabled", Plugin.DimEnabled);
            Copy(other, "2 - Strength", "Opacity", Plugin.DimOpacity);
            Copy(other, "2 - Strength", "Brightness", Plugin.DimBrightness);
            Copy(other, "2 - Strength", "Size", Plugin.DimSize);
            Copy(other, "3 - Sources", "Character hit effects", Plugin.DimCreatureHits);
            Copy(other, "3 - Sources", "Weapon hit effects", Plugin.DimWeapons);
            Copy(other, "3 - Sources", "Projectile hit effects", Plugin.DimImpacts);
            Copy(other, "3 - Sources", "Status effect visuals on creatures", Plugin.DimStatus);
            Copy(other, "3 - Sources", "Include hits on players", Plugin.DimOnPlayers);
            Plugin.Log.LogInfo("Copied the combat effects dimmer settings from Dimmer Combat Particle Effects.");
        }

        // Only when switching from Spearfishing: its settings file is still in the config folder.
        internal static void ImportSpearfishing(ConfigFile config)
        {
            var other = Read(Path.Combine(Paths.ConfigPath, Spearfishing.OriginalModId + ".cfg"));
            if (other.Count == 0) return;
            Copy(other, "General", "EnableSpears", Plugin.FishSpears);
            Copy(other, "General", "EnableHarpoons", Plugin.FishHarpoons);
            Copy(other, "General", "EnableBows", Plugin.FishBows);
            Copy(other, "General", "EnableCrossbows", Plugin.FishCrossbows);
            Plugin.Log.LogInfo("Copied the spearfishing settings from Spearfishing.");
        }

        // Only when switching from DropMoreLoot: its settings file is still in the config folder. DropMoreLoot also
        // multiplied trophies, so that stays on for servers switching from it.
        internal static void ImportDropMoreLoot(ConfigFile config)
        {
            var other = Read(Path.Combine(Paths.ConfigPath, LootAndItems.OriginalModId + ".cfg"));
            if (other.Count == 0) return;
            bool Is(string key, bool fallback) => other.TryGetValue(key, out var value) ? string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) : fallback;
            if (!Is("Pickup|Disable Loot Settings", false))
            {
                Copy(other, "Loot", "Multiplier for monster drops", Plugin.LootCreatures);
                Copy(other, "Loot", "Multiplier for resources", Plugin.LootResources);
                Copy(other, "Loot", "Multiplier for pickable objects", Plugin.LootPickables);
                Plugin.LootTrophies.Value = true;
                if (Is("Whitelist|Enable whitelist filter", false)) Copy(other, "Whitelist", "White List Itmes", Plugin.LootOnly);
            }
            if (Is("Loot|useWeightReduction", true)) Copy(other, "Loot", "percentofweight", Plugin.ItemWeight);
            if (Is("Stacking|Enable Stacking", true)) Copy(other, "Stacking", "Item Stack Multiplier", Plugin.ItemStackSize);
            if (Is("Pickup|Enable Pickup Range", true)) Copy(other, "Pickup", "Pickup Range", Plugin.PickupRangeSetting);
            Copy(other, "Pickup", "Items Float in Water", Plugin.ItemsFloat);
            Plugin.Log.LogInfo("Copied the loot and item settings from DropMoreLoot.");
        }

        // Only when switching from Epic Loot Add-ons: its settings file is still in the config folder. A part switched off
        // there ("0 - Sub-mods") switches its features off here.
        internal static void ImportEpicLootAddons(ConfigFile config)
        {
            var other = Read(Path.Combine(Paths.ConfigPath, EpicLootAddons.OriginalModId + ".cfg"));
            if (other.Count == 0) return;
            bool On(string part) => !other.TryGetValue("0 - Sub-mods|" + part, out var value) || !string.Equals(value, "false", StringComparison.OrdinalIgnoreCase);

            const string storage = "Epic Loot Storage Materials - ", fixes = "Epic Loot Fixes - ";
            Copy(other, storage + "General", "Enabled", Plugin.ChestMaterialsEnabled);
            Copy(other, storage + "General", "Range (m)", Plugin.ChestMaterialsRange);
            Copy(other, storage + "General", "Chest trophies in Sacrifice", Plugin.ChestTrophies);
            Copy(other, storage + "General", "Chest shardstones in Sacrifice", Plugin.ChestShardstones);
            Copy(other, storage + "Sacrifice", "Roll double output per item", Plugin.SacrificePerItem);
            if (!On("Epic Loot Storage Materials")) Plugin.ChestMaterialsEnabled.Value = Plugin.SacrificePerItem.Value = false;

            Copy(other, fixes + "Damage stacking", "Damage effects stack", Plugin.DamageStacking);
            Copy(other, fixes + "Double and Triple Shot", "Normal cost", Plugin.ShotNormalCost);
            Copy(other, fixes + "Double and Triple Shot", "Double Magic Shot doubles the summon limit", Plugin.ShotSummonLimit);
            Copy(other, fixes + "Lucky Loot", "Show Lucky Loot message", Plugin.LuckyLootMessage);
            Copy(other, fixes + "Lucky Loot", "Message position", Plugin.LuckyLootPosition);
            Copy(other, fixes + "Lucky Loot", "Lucky Loot on birds", Plugin.LuckyLootBirds);
            Copy(other, fixes + "Performance", "Faster chest loading", Plugin.FasterChestLoading);
            Copy(other, fixes + "Tooltips", "Show eitr cost on enchanted items", Plugin.EitrTooltip);
            if (!On("Epic Loot Fixes"))
                Plugin.DamageStacking.Value = Plugin.ShotNormalCost.Value = Plugin.ShotSummonLimit.Value = Plugin.LuckyLootMessage.Value =
                    Plugin.LuckyLootBirds.Value = Plugin.FasterChestLoading.Value = Plugin.EitrTooltip.Value = false;

            if (On("Enchant Glow Toggle"))
            {
                Copy(other, "Enchant Glow Toggle - Always", "Hide Glowing aura", Plugin.HideGlowing);
                Copy(other, "Enchant Glow Toggle - Always", "Hide all enchantment effects", Plugin.HideEnchantEffects);
                Copy(other, "Enchant Glow Toggle - Always", "Hide legendary textures", Plugin.HideLegendaryLooks);
            }

            Copy(other, "Shard Exclusivity - General", "Ignore effect exclusions for shards", Plugin.ShardsIgnoreClashes);
            if (!On("Shard Exclusivity")) Plugin.ShardsIgnoreClashes.Value = false;
            Plugin.Log.LogInfo("Copied the settings from Epic Loot Add-ons.");
        }

        // Only when switching from TrophyAssister: its settings file is still in the config folder.
        internal static void ImportTrophyAssister(ConfigFile config)
        {
            var other = Read(Path.Combine(Paths.ConfigPath, Trophies.OriginalModId + ".cfg"));
            if (other.Count == 0) return;
            Copy(other, "3 - Pity", "Enabled", Plugin.TrophyPity);
            if (other.TryGetValue("3 - Pity|Mode", out var mode)) Plugin.TrophyFlatMode.Value = string.Equals(mode, "Flat", StringComparison.OrdinalIgnoreCase);
            Copy(other, "3 - Pity", "GrowthPerKill", Plugin.TrophyGrowth);
            Copy(other, "3 - Pity", "RareThreshold", Plugin.TrophyRareThreshold);
            Copy(other, "3 - Pity", "RareGrowthPerKill", Plugin.TrophyRareGrowth);
            Copy(other, "3 - Pity", "FlatBonusPerKill", Plugin.TrophyFlatBonus);
            Copy(other, "3 - Pity", "GuaranteeAfterKills", Plugin.TrophyGuarantee);
            Copy(other, "3 - Pity", "OnlyCountMyKills", Plugin.TrophyOnlyMine);
            Copy(other, "3 - Pity", "ExtraKillsPerStar", Plugin.TrophyExtraKillsPerStar);
            if (other.TryGetValue("3 - Pity|StarKillBonus", out var stars) && string.Equals(stars, "false", StringComparison.OrdinalIgnoreCase))
                Plugin.TrophyExtraKillsPerStar.Value = 0;
            Copy(other, "2 - Base boost", "ChanceMultiplier", Plugin.TrophyChanceMultiplier);
            Copy(other, "2 - Base boost", "OnlyUncollected", Plugin.TrophyOnlyUncollected);
            Copy(other, "4 - Trophy list", "ShowChances", Plugin.TrophyShowChances);
            Copy(other, "4 - Trophy list", "ShowMissing", Plugin.TrophyShowMissing);
            Copy(other, "4 - Trophy list", "SpoilerFree", Plugin.TrophySpoilerFree);
            Copy(other, "4 - Trophy list", "HiddenTrophies", Plugin.TrophyHidden);
            Copy(other, "1 - General", "NotifyOnDrop", Plugin.TrophyNotify);
            Copy(other, "1 - General", "ShowPityProgress", Plugin.TrophyShowProgress);
            Plugin.Log.LogInfo("Copied the trophy settings from TrophyAssister.");
        }
    }
}
