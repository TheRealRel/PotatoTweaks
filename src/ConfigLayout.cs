// Potato Tweaks - the settings layout of 1.5.17: 32 sections folded into 9, so Configuration Manager has few headers to
// look through. Keys that would clash in a shared section got a prefix ("Speed" -> "Smelter speed"). Every player's and
// server's old values are carried over once, and the old lines are dropped from the file.
using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using HarmonyLib;

namespace PotatoTweaks
{
    internal static class ConfigLayout
    {
        // Whole old sections: (new section, renamed keys; any other key keeps its name).
        private static readonly Dictionary<string, (string Section, Dictionary<string, string> Keys)> Sections =
            new Dictionary<string, (string, Dictionary<string, string>)>
        {
            { "1 - Camera", ("1 - General", new Dictionary<string, string> { { "Enabled", "Camera zoom" }, { "Land zoom multiplier", "Camera zoom multiplier on land" }, { "Ship zoom distance", "Camera zoom distance on ships" } }) },
            { "15 - Carts and boats", ("1 - General", new Dictionary<string, string> {  }) },
            { "16 - Map", ("1 - General", new Dictionary<string, string> { { "Reveal radius", "Map reveal radius" }, { "Reveal radius while sailing", "Map reveal radius while sailing" }, { "Remove death markers button", "Map button to remove death markers" } }) },
            { "3 - Ship and cart removal", ("1 - General", new Dictionary<string, string> { { "Enabled", "Ship and cart removal" } }) },
            { "4 - Tombstone retrieval", ("1 - General", new Dictionary<string, string> { { "Enabled", "Tombstone retrieval" }, { "Cooldown seconds", "Tombstone retrieval cooldown seconds" } }) },
            { "24 - Sleeping", ("1 - General", new Dictionary<string, string> { { "Share of players needed", "Sleep share of players needed" }, { "Emotes count", "Sleep emotes count" }, { "Players in bed needed", "Sleep players in bed needed" } }) },
            { "7 - Visual effects", ("2 - Visual effects", new Dictionary<string, string> {  }) },
            { "30 - Chests", ("3 - Interface", new Dictionary<string, string> { { "Name key", "Chest name key" }, { "Show contents", "Chest contents" }, { "Icon size", "Chest icon size" }, { "Most icons", "Chest most icons" } }) },
            { "32 - Station timers", ("3 - Interface", new Dictionary<string, string> { { "Show timers", "Station timers" } }) },
            { "27 - Comfort", ("3 - Interface", new Dictionary<string, string> { { "Tab name", "Comfort tab name" }, { "Check marks in every tab", "Comfort check marks in every tab" }, { "Message when resting", "Comfort message when resting" }, { "Message cooldown", "Comfort message cooldown" }, { "Pieces in the message", "Comfort message pieces" } }) },
            { "22 - Loot and items", ("4 - Items and loot", new Dictionary<string, string> {  }) },
            { "2 - Dropped items", ("4 - Items and loot", new Dictionary<string, string> { { "Enabled", "Dropped items last longer" }, { "Lifetime hours", "Dropped item lifetime hours" } }) },
            { "28 - Recycling", ("4 - Items and loot", new Dictionary<string, string> { { "Materials returned", "Recycling materials returned" }, { "Count upgrades", "Recycling counts upgrades" }, { "Confirm hotbar items", "Recycling confirms hotbar items" } }) },
            { "23 - Inventory rows", ("4 - Items and loot", new Dictionary<string, string> {  }) },
            { "19 - Spearfishing", ("4 - Items and loot", new Dictionary<string, string> { { "Spears", "Spearfishing with spears" }, { "Harpoons", "Spearfishing with harpoons" }, { "Bows", "Spearfishing with bows" }, { "Crossbows", "Spearfishing with crossbows" } }) },
            { "18 - Trophies", ("5 - Trophies", new Dictionary<string, string> {  }) },
            { "5 - Skill loss on death", ("6 - Skill loss on death", new Dictionary<string, string> {  }) },
            { "6 - Skill XP", ("7 - Skill XP", new Dictionary<string, string> {  }) },
            { "12 - Smelters and kilns", ("8 - Stations and building", new Dictionary<string, string> { { "Ore capacity", "Smelter ore capacity" }, { "Fuel capacity", "Smelter fuel capacity" }, { "Speed", "Smelter speed" }, { "Fuel per product", "Smelter fuel per product" }, { "Output", "Smelter output" }, { "Excluded stations", "Smelter excluded stations" } }) },
            { "13 - Fermenters", ("8 - Stations and building", new Dictionary<string, string> { { "Speed", "Fermenter speed" }, { "Output", "Fermenter output" } }) },
            { "14 - Crafting stations", ("8 - Stations and building", new Dictionary<string, string> { { "Build range", "Crafting station build range" } }) },
            { "29 - Serving tray", ("8 - Stations and building", new Dictionary<string, string> { { "Every crafted food", "Serving tray takes every crafted food" } }) },
            { "26 - Wisplight", ("8 - Stations and building", new Dictionary<string, string> { { "Upgrade levels", "Wisplight upgrade levels" }, { "Clearing per level", "Wisplight clearing per level" }, { "Wisps per level", "Wisplight wisps per level" }, { "Silver per level", "Wisplight silver per level" }, { "Eitr per level", "Wisplight eitr per level" } }) },
            { "31 - Doors", ("8 - Stations and building", new Dictionary<string, string> { { "Seconds open", "Door seconds open" } }) },
            { "33 - Smoke", ("8 - Stations and building", new Dictionary<string, string> { { "Passes through", "Smoke passes through" } }) },
            { "17 - Double dragon bed", ("8 - Stations and building", new Dictionary<string, string> { { "Enabled", "Double dragon bed" }, { "Blankets", "Double dragon bed blankets" } }) },
            { "25 - Item stands", ("8 - Stations and building", new Dictionary<string, string> { { "Items allowed", "Item stand items allowed" }, { "Stands", "Item stand pieces" }, { "Adjustments", "Item stand adjustments" }, { "Largest offset", "Item stand largest offset" }, { "Largest size", "Item stand largest size" }, { "Default adjustments", "Item stand default adjustments" }, { "Simple models", "Item stand simple models" }, { "Hide stands holding an item", "Item stand hidden when holding an item" }, { "Items closer to the wall", "Item stand items closer to the wall" } }) },
            { "9 - Epic Loot bounties", ("9 - Epic Loot", new Dictionary<string, string> { { "Complete on main target", "Bounty complete on main target" }, { "Spread out new bounties", "Bounties spread out" }, { "Spacing", "Bounty spacing" }, { "Extra enchanted copies", "Bounty extra enchanted copies" }, { "Guaranteed enchanted item", "Bounty guaranteed enchanted item" }, { "Normal star scaling", "Bounty normal star scaling" }, { "Overhead health bar", "Bounty overhead health bar" } }) },
            { "20 - Epic Loot enchanting table", ("9 - Epic Loot", new Dictionary<string, string> { { "Materials from nearby chests", "Enchanting materials from nearby chests" }, { "Chest range", "Enchanting chest range" }, { "Double output per item", "Sacrifice double output per item" } }) },
            { "21 - Epic Loot enchantments", ("9 - Epic Loot", new Dictionary<string, string> {  }) },
        };

        // Single keys from the old mixed sections ("10 - Bug fixes", "11 - Quality of life").
        private static readonly Dictionary<(string, string), (string Section, string Key)> Keys =
            new Dictionary<(string, string), (string, string)>
        {
            { ("10 - Bug fixes", "Game bug fixes"), ("1 - General", "Game bug fixes") },
            { ("10 - Bug fixes", "Tidy other mods config files"), ("1 - General", "Tidy other mods config files") },
            { ("10 - Bug fixes", "Epic Loot enchantment fixes"), ("9 - Epic Loot", "Enchantment fixes") },
            { ("10 - Bug fixes", "Epic Loot faster chest loading"), ("9 - Epic Loot", "Faster chest loading") },
            { ("11 - Quality of life", "Swim skill raises swim speed"), ("1 - General", "Swim skill raises swim speed") },
            { ("11 - Quality of life", "Sneak skill raises sneak speed"), ("1 - General", "Sneak skill raises sneak speed") },
            { ("11 - Quality of life", "Tamed animals spare crops"), ("1 - General", "Tamed animals spare crops") },
            { ("11 - Quality of life", "Live hammer menu"), ("3 - Interface", "Live hammer menu") },
            { ("11 - Quality of life", "Health bars with build tools"), ("3 - Interface", "Health bars with build tools") },
            { ("11 - Quality of life", "Combat health bars"), ("3 - Interface", "Combat health bars") },
            { ("11 - Quality of life", "Attacker health bar range"), ("3 - Interface", "Attacker health bar range") },
            { ("11 - Quality of life", "Taming info at range"), ("3 - Interface", "Taming info at range") },
            { ("11 - Quality of life", "Inventory Slots Visibility"), ("3 - Interface", "Inventory slot visibility") },
            { ("11 - Quality of life", "Hide status effect names"), ("3 - Interface", "Hide status effect names") },
            { ("11 - Quality of life", "No auto-equip on pickup"), ("4 - Items and loot", "No auto-equip on pickup") },
            { ("11 - Quality of life", "Auto-pickup key with Equipment & Quick Slots"), ("4 - Items and loot", "Auto-pickup key with Equipment & Quick Slots") },
            { ("11 - Quality of life", "Tombstone items back in their slots"), ("1 - General", "Tombstone items back in their slots") },
        };

        private static bool New(string oldSection, string oldKey, out string newSection, out string newKey)
        {
            if (Keys.TryGetValue((oldSection, oldKey), out var k)) { newSection = k.Section; newKey = k.Key; return true; }
            if (Sections.TryGetValue(oldSection, out var s)) { newSection = s.Section; newKey = s.Keys.TryGetValue(oldKey, out var r) ? r : oldKey; return true; }
            newSection = newKey = null;
            return false;
        }

        // The file as it was before this start ("section|key" -> value), with each old line also listed under its new name,
        // so the first-start checks (importing other mods' settings only on a first start) see a moved setting as there.
        internal static Dictionary<string, string> WithAliases(Dictionary<string, string> before)
        {
            var all = new Dictionary<string, string>(before);
            foreach (var pair in before)
            {
                int bar = pair.Key.IndexOf('|');
                if (bar < 0) continue;
                string section = pair.Key.Substring(0, bar), key = pair.Key.Substring(bar + 1);
                if (New(section, key, out var ns, out var nk) && !all.ContainsKey(ns + "|" + nk)) all[ns + "|" + nk] = pair.Value;
            }
            return all;
        }

        // Once every setting is bound: an old line whose new setting wasn't in the file yet gives it its value, and every
        // line left in an old section is dropped (the bound ones are rewritten under their new names).
        internal static void Apply(ConfigFile config, Dictionary<string, string> before)
        {
            try
            {
                var orphans = AccessTools.Property(typeof(ConfigFile), "OrphanedEntries")?.GetValue(config) as Dictionary<ConfigDefinition, string>;
                int moved = 0;
                foreach (var pair in before)
                {
                    int bar = pair.Key.IndexOf('|');
                    if (bar < 0) continue;
                    string section = pair.Key.Substring(0, bar), key = pair.Key.Substring(bar + 1);
                    if (!New(section, key, out var ns, out var nk) || before.ContainsKey(ns + "|" + nk)) continue;
                    var def = new ConfigDefinition(ns, nk);
                    if (!config.ContainsKey(def)) continue;
                    ConfigText.SetValue(config[def], pair.Value);
                    moved++;
                }
                int dropped = 0;
                if (orphans != null)
                    foreach (var def in orphans.Keys.ToList())
                        if (Sections.ContainsKey(def.Section) || Keys.ContainsKey((def.Section, def.Key)) || def.Section == "11 - Quality of life")
                        { orphans.Remove(def); dropped++; }
                if (moved > 0 || dropped > 0)
                {
                    config.Save();
                    Plugin.Log.LogInfo($"Settings regrouped into 9 sections: {moved} values carried over to their new places, {dropped} old lines tidied away.");
                }
            }
            catch (Exception e) { Plugin.Log.LogWarning($"Couldn't carry the old settings over to the new layout: {e.Message}"); }
        }
    }
}
