// Potato Tweaks - an upgradeable Wisplight that clears more mist at each level.
// Written from scratch for this mod; the idea is from Digitalroot's Better Wisps.
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

namespace PotatoTweaks
{
    // The Wisplight (crafted at the workbench) can be upgraded like gear. Level 1 is the game's own; each level above it
    // widens the mist its wisp clears. The level is saved on the wisp itself, so every player sees the same clearing
    // around it. Wisp torches and other mist clearers are left alone.
    internal static class Wisplight
    {
        internal const string OriginalModId = "digitalroot.mods.betterWisps";
        private const string ItemName = "Demister", WispName = "Wisp", SilverName = "Silver", EitrName = "Eitr";
        private static readonly int LevelKey = "PotatoTweaks.WispLevel".GetStableHashCode();

        internal static readonly System.Type[] Patches =
        {
            typeof(Definitions), typeof(Definitions.MenuCopy), typeof(Definitions.WorldStart), typeof(StationLevel), typeof(OwnWisp), typeof(WispCreated),
        };

        private static readonly AccessTools.FieldRef<SE_Demister, GameObject> Ball = AccessTools.FieldRefAccess<SE_Demister, GameObject>("m_ballInstance");
        private static readonly AccessTools.FieldRef<Humanoid, ItemDrop.ItemData> Utility = AccessTools.FieldRefAccess<Humanoid, ItemDrop.ItemData>("m_utilityItem");
        private static readonly ConditionalWeakTable<Recipe, Piece.Requirement[]> OriginalResources = new ConditionalWeakTable<Recipe, Piece.Requirement[]>();
        private static readonly HashSet<string> WispPrefabs = new HashSet<string>();
        private static int originalMaxQuality = -1;
        private static readonly Dictionary<string, int> StationMax = new Dictionary<string, int>();

        internal static void Initialize()
        {
            void Reapply() => Apply(ObjectDB.instance);
            Plugin.WispLevels.SettingChanged += (_, __) => Reapply();
            Plugin.WispWispsPerLevel.SettingChanged += (_, __) => Reapply();
            Plugin.WispSilverPerLevel.SettingChanged += (_, __) => Reapply();
            Plugin.WispEitrPerLevel.SettingChanged += (_, __) => Reapply();
        }

        internal static float Range(float baseRange, int level) => baseRange + Mathf.Max(0, level - 1) * Mathf.Max(0f, Plugin.WispRangePerLevel.Value);

        // ---------------------------------------------------------------- item and recipe
        [HarmonyPatch(typeof(ObjectDB), "Awake")]
        internal static class Definitions
        {
            [HarmonyPriority(Priority.Last)]
            private static void Postfix(ObjectDB __instance) => Apply(__instance);

            [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.CopyOtherDB))]
            internal static class MenuCopy
            {
                [HarmonyPriority(Priority.Last)]
                private static void Postfix(ObjectDB __instance) => Apply(__instance);
            }

            [HarmonyPatch(typeof(Game), "Start")]
            internal static class WorldStart
            {
                private static void Postfix()
                {
                    StationMax.Clear();
                    Apply(ObjectDB.instance);
                }
            }
        }

        // Max quality and the upgrade costs, always worked out from the game's own recipe so a changed setting (or the
        // server's, on joining) is applied cleanly.
        private static void Apply(ObjectDB db)
        {
            if (!db || db.m_items == null) return;
            foreach (var effect in db.m_StatusEffects)
                if (effect is SE_Demister demister && demister.m_ballPrefab) WispPrefabs.Add(demister.m_ballPrefab.name);

            var prefab = db.GetItemPrefab(ItemName);
            var shared = prefab ? prefab.GetComponent<ItemDrop>()?.m_itemData?.m_shared : null;
            if (shared == null) return;
            if (originalMaxQuality < 0) originalMaxQuality = shared.m_maxQuality;
            int levels = Mathf.Clamp(Plugin.WispLevels.Value, 1, 10);
            shared.m_maxQuality = levels > 1 ? levels : originalMaxQuality;

            var wisp = db.GetItemPrefab(WispName)?.GetComponent<ItemDrop>();
            var silver = db.GetItemPrefab(SilverName)?.GetComponent<ItemDrop>();
            var eitr = db.GetItemPrefab(EitrName)?.GetComponent<ItemDrop>();
            foreach (var recipe in db.m_recipes)
            {
                if (!recipe || !recipe.m_item || recipe.m_item.gameObject.name != ItemName) continue;
                var original = OriginalResources.GetValue(recipe, r => r.m_resources.Select(Copy).ToArray());
                var resources = original.Select(Copy).ToList();
                if (levels > 1)
                {
                    foreach (var requirement in resources)
                    {
                        if (requirement.m_resItem == wisp) requirement.m_amountPerLevel = Mathf.Max(0, Plugin.WispWispsPerLevel.Value);
                        else if (requirement.m_resItem == silver) requirement.m_amountPerLevel = Mathf.Max(0, Plugin.WispSilverPerLevel.Value);
                        else if (requirement.m_resItem == eitr) requirement.m_amountPerLevel = Mathf.Max(0, Plugin.WispEitrPerLevel.Value);
                    }
                    if (eitr && Plugin.WispEitrPerLevel.Value > 0 && resources.All(r => r.m_resItem != eitr))
                        resources.Add(new Piece.Requirement { m_resItem = eitr, m_amount = 0, m_amountPerLevel = Plugin.WispEitrPerLevel.Value, m_recover = true });
                }
                recipe.m_resources = resources.ToArray();
            }
        }

        private static Piece.Requirement Copy(Piece.Requirement r) => new Piece.Requirement
        {
            m_resItem = r.m_resItem, m_amount = r.m_amount, m_extraAmountOnlyOneIngredient = r.m_extraAmountOnlyOneIngredient,
            m_amountPerLevel = r.m_amountPerLevel, m_upgraderResource = r.m_upgraderResource, m_recover = r.m_recover,
        };

        // Each upgrade level needs one station level more. If the levels set go past what the station can reach with
        // all its extensions, the last upgrades ask for its highest level instead of one that can't be built.
        [HarmonyPatch(typeof(Recipe), nameof(Recipe.GetRequiredStationLevel))]
        internal static class StationLevel
        {
            private static void Postfix(Recipe __instance, ref int __result)
            {
                if (__result <= 1 || !__instance.m_item || __instance.m_item.gameObject.name != ItemName || !__instance.m_craftingStation) return;
                int max = MaxLevel(__instance.m_craftingStation);
                if (max > 0 && __result > max) __result = max;
            }
        }

        // 1 + one level per kind of extension built for the station; 0 (no cap) when an extension can stack or the
        // scene isn't ready.
        private static int MaxLevel(CraftingStation station)
        {
            if (StationMax.TryGetValue(station.m_name, out int max)) return max;
            if (!ZNetScene.instance) return 0;
            int kinds = 0;
            bool stacks = false;
            foreach (var prefab in ZNetScene.instance.m_prefabs)
            {
                var extension = prefab ? prefab.GetComponent<StationExtension>() : null;
                if (!extension || !extension.m_craftingStation || extension.m_craftingStation.m_name != station.m_name) continue;
                kinds++;
                stacks |= extension.m_stack;
            }
            max = stacks ? 0 : 1 + kinds;
            StationMax[station.m_name] = max;
            return max;
        }

        // ---------------------------------------------------------------- the wisp
        // The wearer's game writes its Wisplight's level on the wisp it sends out.
        [HarmonyPatch(typeof(SE_Demister), nameof(SE_Demister.UpdateStatusEffect))]
        internal static class OwnWisp
        {
            private static void Postfix(SE_Demister __instance)
            {
                var ball = Ball(__instance);
                if (!ball || !(__instance.m_character is Humanoid wearer)) return;
                var view = ball.GetComponent<ZNetView>();
                if (!view || !view.IsValid() || !view.IsOwner()) return;
                var item = Utility(wearer);
                int level = item != null && item.m_dropPrefab && item.m_dropPrefab.name == ItemName ? Mathf.Max(1, item.m_quality) : 1;
                var zdo = view.GetZDO();
                if (zdo.GetInt(LevelKey, 1) != level) zdo.Set(LevelKey, level);
            }
        }

        // Every game gives each wisp the clearing of its level, its own player's or another's.
        [HarmonyPatch(typeof(Demister), "Awake")]
        internal static class WispCreated
        {
            private static void Postfix(Demister __instance)
            {
                var view = __instance.GetComponentInParent<ZNetView>();
                if (!view || !WispPrefabs.Contains(Utils.GetPrefabName(view.gameObject)) || __instance.GetComponent<WispRange>()) return;
                __instance.gameObject.AddComponent<WispRange>().Setup(__instance, view);
            }
        }

        internal sealed class WispRange : MonoBehaviour
        {
            // Epic Loot's "wisp range" enchantment multiplies the clearing and keeps its bonus on the wisp under this key;
            // it is applied on top of the level, so both count.
            private static readonly int EpicLootBonus = "el-demist".GetStableHashCode();
            private Demister demister;
            private ZNetView view;
            private float baseRange, nextCheck;

            internal void Setup(Demister owner, ZNetView network)
            {
                demister = owner;
                view = network;
                // The prefab's own range: by now another mod may already have changed this copy's.
                var prefab = ZNetScene.instance ? ZNetScene.instance.GetPrefab(Utils.GetPrefabName(network.gameObject)) : null;
                var field = prefab ? prefab.GetComponentInChildren<ParticleSystemForceField>(true) : null;
                baseRange = field ? field.endRange : demister.m_forceField ? demister.m_forceField.endRange : 0f;
            }

            private void Update()
            {
                if (Time.time < nextCheck) return;
                nextCheck = Time.time + 0.5f;
                if (!demister || !demister.m_forceField || !view || !view.IsValid() || baseRange <= 0f) return;
                var zdo = view.GetZDO();
                int level = Plugin.WispLevels.Value > 1 ? zdo.GetInt(LevelKey, 1) : 1;
                float range = Range(baseRange, level) * (1f + Mathf.Max(0f, zdo.GetFloat(EpicLootBonus, 0f)));
                if (!Mathf.Approximately(demister.m_forceField.endRange, range)) demister.m_forceField.endRange = range;
            }
        }
    }
}
