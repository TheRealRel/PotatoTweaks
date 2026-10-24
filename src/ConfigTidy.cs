// Small clean-ups in other mods' config files that make them warn on every launch (Valheim, PotatoTweaks). Each fix
// keeps the file's meaning exactly as it was; it only stops the warning. Runs once at startup, writes a file only
// when it needs the change, and says what it changed in the log.
//   LetMeTameYou_DefaultTameList.yml: tab characters (only ever in comment lines) become spaces. LetMeTameYou warns
//     "Config contains TAB indentation" and converts them in memory on every launch.
//   StarLevelSystem/LocationResetSettings.yaml: the raspberry, blueberry and cloudberry bushes are listed in the
//     Berries group, but the Foraging group's "$Pickable" already claims them, and StarLevelSystem always lets the
//     group that resets most often win (Foraging) and warns about each one. Taking them out of Berries changes nothing.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;

namespace PotatoTweaks
{
    internal static class ConfigTidy
    {
        private static readonly string[] Bushes = { "RaspberryBush", "BlueberryBush", "CloudberryBush" };

        internal static void Run()
        {
            Try("LetMeTameYou tame list", TameListTabs);
            Try("StarLevelSystem location resets", BerryBushes);
        }

        private static void Try(string what, Action fix)
        {
            try { fix(); }
            catch (Exception e) { Plugin.Log.LogWarning($"Config tidy ({what}) skipped: {e.GetBaseException().Message}"); }
        }

        private static void TameListTabs()
        {
            string path = Path.Combine(Paths.ConfigPath, "LetMeTameYou_DefaultTameList.yml");
            if (!File.Exists(path)) return;
            string text = File.ReadAllText(path);
            if (text.IndexOf('\t') < 0) return;
            var lines = text.Split('\n');
            int changed = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].IndexOf('\t') < 0 || !lines[i].TrimStart(' ', '\t').StartsWith("#")) continue;  // comments only
                lines[i] = lines[i].Replace("\t", "  ");
                changed++;
            }
            if (changed == 0) return;
            File.WriteAllText(path, string.Join("\n", lines));
            Plugin.Log.LogInfo($"Config tidy: replaced tabs with spaces in {changed} comment lines of LetMeTameYou_DefaultTameList.yml, so LetMeTameYou stops warning about them.");
        }

        private static void BerryBushes()
        {
            string path = Path.Combine(Paths.ConfigPath, "StarLevelSystem", "LocationResetSettings.yaml");
            if (!File.Exists(path)) return;
            var lines = File.ReadAllLines(path).ToList();
            // Only when Foraging claims every pickable, so the bushes are certain to stay in it.
            bool foragingClaimsAll = false;
            for (int i = 0; i < lines.Count; i++)
                if (lines[i].Trim() == "Foraging:" && !lines[i].TrimStart().StartsWith("#"))
                    for (int j = i + 1; j < lines.Count && (lines[j].StartsWith("    ") || lines[j].Trim().Length == 0); j++)
                        if (lines[j].Trim() == "- $Pickable") foragingClaimsAll = true;
            if (!foragingClaimsAll) return;
            var removed = new List<string>();
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].Trim() != "Berries:" || lines[i].TrimStart().StartsWith("#")) continue;
                int indent = lines[i].Length - lines[i].TrimStart().Length;
                for (int j = i + 1; j < lines.Count; j++)
                {
                    string line = lines[j];
                    int lineIndent = line.Length - line.TrimStart().Length;
                    if (line.Trim().Length > 0 && lineIndent <= indent) break;  // the next group
                    string item = line.Trim().StartsWith("- ") ? line.Trim().Substring(2).Trim() : null;
                    if (item == null || Array.IndexOf(Bushes, item) < 0) continue;
                    removed.Add(item);
                    lines.RemoveAt(j);
                    j--;
                }
            }
            if (removed.Count == 0) return;
            File.WriteAllLines(path, lines);
            Plugin.Log.LogInfo($"Config tidy: took {string.Join(", ", removed.ToArray())} out of StarLevelSystem's Berries group; its Foraging group already resets them, so nothing changes and it stops warning.");
        }
    }
}
