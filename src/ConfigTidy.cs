// Small clean-ups in other mods' config files that make them warn on every launch (Valheim, PotatoTweaks). Each fix
// keeps the file's meaning exactly as it was; it only stops the warning. Runs once at startup, writes a file only
// when it needs the change, and says what it changed in the log.
//   LetMeTameYou_DefaultTameList.yml: tab characters (only ever in comment lines) become spaces. LetMeTameYou warns
//     "Config contains TAB indentation" and converts them in memory on every launch.
using System;
using System.IO;
using BepInEx;

namespace PotatoTweaks
{
    internal static class ConfigTidy
    {
        internal static void Run()
        {
            Try("LetMeTameYou tame list", TameListTabs);
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
    }
}
