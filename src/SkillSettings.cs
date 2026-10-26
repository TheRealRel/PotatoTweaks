// Potato Tweaks - skill loss on death and XP multipliers per skill, written from scratch.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;

namespace PotatoTweaks
{
    internal static class SkillSettings
    {
        internal static ConfigEntry<bool> LossEnabled, XpEnabled;
        internal static ConfigEntry<float> OtherLoss, OtherXp;
        private static readonly Dictionary<Skills.SkillType, ConfigEntry<float>> Loss = new Dictionary<Skills.SkillType, ConfigEntry<float>>();
        private static readonly Dictionary<Skills.SkillType, ConfigEntry<float>> Xp = new Dictionary<Skills.SkillType, ConfigEntry<float>>();

        private const string LossSection = "6 - Skill loss on death";   // "5 - Skill loss on death" before 1.5.17 (ConfigLayout)
        private const string XpSection = "7 - Skill XP";               // "6 - Skill XP" before 1.5.17

        // Every built-in skill, read from the game itself, so a skill added by a future update is listed automatically.
        private static IEnumerable<Skills.SkillType> VanillaSkills() =>
            Enum.GetValues(typeof(Skills.SkillType)).Cast<Skills.SkillType>()
                .Where(s => s != Skills.SkillType.None && s != Skills.SkillType.All);

        // "WoodCutting" -> "Wood Cutting", the same names the SkillLoss mod used.
        private static string Label(Skills.SkillType skill) => Regex.Replace(skill.ToString(), "(?<=[a-z])(?=[A-Z])", " ");

        internal static void Bind(ConfigFile config, Func<string, AcceptableValueBase, ConfigDescription> synced)
        {
            string text = File.Exists(config.ConfigFilePath) ? File.ReadAllText(config.ConfigFilePath) : "";
            bool fresh = !text.Contains("[" + LossSection + "]") && !text.Contains("[5 - Skill loss on death]");
            var imported = fresh ? ReadSkillLossConfig() : null;

            LossEnabled = config.Bind(LossSection, "Enabled", true,
                synced("Use the per-skill amounts in this section when a player dies, instead of the game's own skill loss.", null));
            XpEnabled = config.Bind(XpSection, "Enabled", true,
                synced("Use the per-skill XP multipliers in this section.", null));

            foreach (var skill in VanillaSkills())
            {
                string name = Label(skill);
                Loss[skill] = config.Bind(LossSection, name, Imported(imported, "Loss", name, 1f),
                    synced("Share of the game's death skill loss. 0 = no loss, 0.5 = half, 1 = the game's normal loss.", new AcceptableValueRange<float>(0f, 2f)));
                Xp[skill] = config.Bind(XpSection, name, Imported(imported, "XP", name, 1f),
                    synced("XP multiplier. 1 = normal, 2 = double, 5 = five times.", new AcceptableValueRange<float>(0f, 10f)));
            }
            OtherLoss = config.Bind(LossSection, "Other skills", 1f,
                synced("Skills added by other mods. 0 = no loss, 1 = the game's normal loss.", new AcceptableValueRange<float>(0f, 2f)));
            OtherXp = config.Bind(XpSection, "Other skills", 1f,
                synced("XP multiplier for skills added by other mods.", new AcceptableValueRange<float>(0f, 10f)));

            if (imported != null && imported.Count > 0)
                Plugin.Log.LogInfo($"Copied {imported.Count} skill settings from Mirfin.SkillLoss.cfg into this config.");
        }

        private static float LossFor(Skills.SkillType skill) => Loss.TryGetValue(skill, out var e) ? e.Value : OtherLoss.Value;
        private static float XpFor(Skills.SkillType skill) => Xp.TryGetValue(skill, out var e) ? e.Value : OtherXp.Value;

        // Reads the SkillLoss mod's config, when present, so its values become this config's starting values.
        private static Dictionary<string, float> ReadSkillLossConfig()
        {
            var values = new Dictionary<string, float>();
            string path = Path.Combine(Paths.ConfigPath, "Mirfin.SkillLoss.cfg");
            if (!File.Exists(path)) return values;
            string kind = null;
            foreach (var raw in File.ReadAllLines(path))
            {
                string line = raw.Trim();
                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    string section = line.Substring(1, line.Length - 2);
                    kind = section.StartsWith("Loss") ? "Loss" : section.StartsWith("XP") ? "XP" : null;
                    continue;
                }
                if (kind == null || line.StartsWith("#")) continue;
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                if (float.TryParse(line.Substring(eq + 1).Trim(), System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out float value))
                    values[kind + "|" + line.Substring(0, eq).Trim()] = value;
            }
            return values;
        }

        private static float Imported(Dictionary<string, float> imported, string kind, string name, float fallback) =>
            imported != null && imported.TryGetValue(kind + "|" + name, out float value) ? value : fallback;

        // ---------------------------------------------------------------- patches
        private static readonly AccessTools.FieldRef<Skills, Dictionary<Skills.SkillType, Skills.Skill>> SkillData =
            AccessTools.FieldRefAccess<Skills, Dictionary<Skills.SkillType, Skills.Skill>>("m_skillData");
        private static readonly AccessTools.FieldRef<Skills, Player> Owner =
            AccessTools.FieldRefAccess<Skills, Player>("m_player");

        // Valheim: every skill loses level * factor and its progress toward the next level, then a message is shown.
        // Here each skill's loss is scaled by its own setting; a skill set to 0 keeps both its level and its progress.
        [HarmonyPatch(typeof(Skills), nameof(Skills.LowerAllSkills))]
        internal static class DeathLoss
        {
            private static bool Prefix(Skills __instance, float factor)
            {
                if (!LossEnabled.Value) return true;
                bool lostAny = false;
                foreach (var entry in SkillData(__instance))
                {
                    float share = LossFor(entry.Key);
                    if (share <= 0f) continue;
                    var skill = entry.Value;
                    skill.m_level -= skill.m_level * factor * share;
                    skill.m_accumulator = 0f;
                    lostAny = true;
                }
                var player = Owner(__instance);
                if (lostAny && player != null) player.Message(MessageHud.MessageType.TopLeft, "$msg_skills_lowered");
                return false;
            }
        }

        [HarmonyPatch(typeof(Skills), nameof(Skills.RaiseSkill))]
        internal static class XpGain
        {
            private static void Prefix(Skills.SkillType skillType, ref float factor)
            {
                if (XpEnabled.Value) factor *= XpFor(skillType);
            }
        }
    }
}
