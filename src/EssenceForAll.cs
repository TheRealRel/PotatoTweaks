// Potato Tweaks - essences for the whole group.
// Adapted from Koen-H's EssenceForAll (MIT License, https://github.com/Koen-H/valheim-mods).
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace PotatoTweaks
{
    // Essences (Thunderblood, Frostfire) always drop from the creatures that carry them, one for each player who was
    // near the creature when it appeared, so the whole group gets one. The count is saved on the creature, so it still
    // counts if someone leaves before the kill; creatures from before this was installed count the players near them
    // when they die. Stars still add the game's extra on top. The creature drop multiplier leaves essences alone.
    // Rolled by the game running the creature, like all drops.
    internal static class EssenceForAll
    {
        internal const string OriginalModId = "com.koendertjh.essenceforall";
        private const string PlayersKey = "PotatoTweaks.EssencePlayers";
        private static HashSet<string> items = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static bool active;

        internal static readonly Type[] Patches = { typeof(Spawned), typeof(Drops), typeof(ListCarriers) };

        internal static void Initialize()
        {
            ReadItems();
            Plugin.EssenceItems.SettingChanged += (_, __) => ReadItems();
            active = true;
        }

        private static void ReadItems() =>
            items = new HashSet<string>(Plugin.EssenceItems.Value.Split(',').Select(name => name.Trim()).Where(name => name.Length > 0),
                StringComparer.OrdinalIgnoreCase);

        private static bool On => active && Plugin.EssenceForEveryone.Value;

        // Also asked by the creature drop multiplier, which leaves essences alone while this is on.
        internal static bool IsEssence(GameObject prefab) => On && prefab && items.Contains(prefab.name);

        private static bool IsEssence(CharacterDrop.Drop drop) => drop != null && IsEssence(drop.m_prefab);

        private static bool CarriesEssence(CharacterDrop drops)
        {
            foreach (var drop in drops.m_drops)
                if (IsEssence(drop)) return true;
            return false;
        }

        private static int CountPlayers(Vector3 position)
        {
            float range = Plugin.EssencePlayerRange.Value;
            if (range <= 0f && ZNet.instance != null) return Mathf.Max(1, ZNet.instance.GetNrOfPlayers());
            int count = 0;
            foreach (var player in Player.GetAllPlayers())
                if (player && (player.transform.position - position).sqrMagnitude <= range * range) count++;
            return Mathf.Max(1, count);
        }

        // The players near a creature when it appears. Character.Awake also runs when a saved creature loads again;
        // the count it already has is kept then.
        [HarmonyPatch(typeof(Character), "Awake")]
        internal static class Spawned
        {
            private static void Postfix(Character __instance)
            {
                if (!On || __instance.IsPlayer()) return;
                var drops = __instance.GetComponent<CharacterDrop>();
                if (!drops || !CarriesEssence(drops)) return;
                var view = __instance.GetComponent<ZNetView>();
                var zdo = view && view.IsValid() ? view.GetZDO() : null;
                if (zdo == null || !view.IsOwner() || zdo.GetInt(PlayersKey) > 0) return;
                zdo.Set(PlayersKey, CountPlayers(__instance.transform.position));
            }
        }

        [HarmonyPatch(typeof(CharacterDrop), nameof(CharacterDrop.GenerateDropList))]
        internal static class Drops
        {
            private static void Prefix(CharacterDrop __instance, out List<KeyValuePair<CharacterDrop.Drop, float>> __state)
            {
                __state = null;
                if (!On) return;
                foreach (var drop in __instance.m_drops)
                {
                    if (!IsEssence(drop)) continue;
                    (__state ?? (__state = new List<KeyValuePair<CharacterDrop.Drop, float>>())).Add(new KeyValuePair<CharacterDrop.Drop, float>(drop, drop.m_chance));
                    drop.m_chance = 1f;
                }
            }

            // The chances go back as they were, whatever happens in between.
            private static void Finalizer(List<KeyValuePair<CharacterDrop.Drop, float>> __state)
            {
                if (__state == null) return;
                foreach (var saved in __state) saved.Key.m_chance = saved.Value;
            }

            private static void Postfix(CharacterDrop __instance, List<KeyValuePair<GameObject, int>> __result, List<KeyValuePair<CharacterDrop.Drop, float>> __state)
            {
                if (__state == null || __result == null) return;
                var view = __instance.GetComponent<ZNetView>();
                int stored = view && view.IsValid() ? view.GetZDO().GetInt(PlayersKey) : 0;
                int players = stored > 0 ? stored : CountPlayers(__instance.transform.position);
                if (players <= 1) return;
                for (int i = 0; i < __result.Count; i++)
                {
                    var prefab = __result[i].Key;
                    var drop = __state.FirstOrDefault(saved => saved.Key.m_prefab == prefab).Key;
                    // Drops made once per player already give everyone one.
                    if (drop == null || drop.m_onePerPlayer) continue;
                    __result[i] = new KeyValuePair<GameObject, int>(prefab, __result[i].Value * players);
                }
            }
        }

        // Which creatures carry essences, written to the log as the game starts, to check the item list against.
        [HarmonyPatch(typeof(ZNetScene), "Awake")]
        internal static class ListCarriers
        {
            private static void Postfix(ZNetScene __instance)
            {
                if (!On) return;
                var text = new StringBuilder();
                foreach (var prefab in __instance.m_prefabs)
                {
                    var drops = prefab ? prefab.GetComponent<CharacterDrop>() : null;
                    if (!drops) continue;
                    string carried = string.Join(", ", drops.m_drops.Where(IsEssence).Select(drop => $"{drop.m_prefab.name} {drop.m_amountMin}-{drop.m_amountMax} at {drop.m_chance:P0}"));
                    if (carried.Length > 0) text.Append($"\n  {prefab.name}: {carried}");
                }
                Plugin.Log.LogInfo(text.Length > 0 ? "Essence for everyone, creatures that carry essences:" + text : "Essence for everyone: no creature carries the listed essences.");
            }
        }
    }
}
