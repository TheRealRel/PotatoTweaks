// Potato Tweaks - loot multipliers, item weight and stack size, auto-pickup range and floating items.
// Written from scratch for this mod; the same settings were in TastyChickenLegs' DropMoreLoot, which Potato Tweaks
// replaces. Unlike that mod, the game's own drop rules (star levels, world modifiers) stay in charge and only the result
// is multiplied, and weight and stack size are set once on the item definitions, so crafted items and items loaded with
// the character get them too, not only items picked up from the ground.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

namespace PotatoTweaks
{
    internal static class LootAndItems
    {
        internal const string OriginalModId = "TastyChickenLegs.DropMoreLoot";

        internal static readonly Type[] Patches =
        {
            typeof(CreatureDrops), typeof(ResourceDrops), typeof(PickableDrops), typeof(PickableDrops.Scope), typeof(GroundItems),
            typeof(ItemDefinitions), typeof(ItemDefinitions.MenuCopy), typeof(ItemDefinitions.WorldStart), typeof(FloatingItems), typeof(PickupRange),
        };

        private static HashSet<string> only = new HashSet<string>(), never = new HashSet<string>();

        internal static void Initialize()
        {
            ReadLists();
            Plugin.LootOnly.SettingChanged += (_, __) => ReadLists();
            Plugin.LootNever.SettingChanged += (_, __) => ReadLists();
            Plugin.ItemWeight.SettingChanged += (_, __) => ItemDefinitions.Apply(ObjectDB.instance);
            Plugin.ItemStackSize.SettingChanged += (_, __) => ItemDefinitions.Apply(ObjectDB.instance);
            Plugin.PickupRangeSetting.SettingChanged += (_, __) => PickupRange.Apply(Player.m_localPlayer);
        }

        private static HashSet<string> Names(string text) =>
            new HashSet<string>(text.Split(',').Select(name => name.Trim()).Where(name => name.Length > 0), StringComparer.OrdinalIgnoreCase);

        private static void ReadLists()
        {
            only = Names(Plugin.LootOnly.Value);
            never = Names(Plugin.LootNever.Value);
        }

        // Only items are multiplied (a drop list can also spawn creatures), and never trophies unless asked, the
        // never-multiplied list, or items outside the only-these list when it is set.
        internal static bool Multiplies(GameObject prefab)
        {
            if (!prefab) return false;
            var item = prefab.GetComponent<ItemDrop>();
            if (!item) return false;
            string name = prefab.name;
            if (never.Contains(name) || (only.Count > 0 && !only.Contains(name))) return false;
            return Plugin.LootTrophies.Value || item.m_itemData?.m_shared?.m_itemType != ItemDrop.ItemData.ItemType.Trophy;
        }

        // A multiplier of 1.5 gives one and a half times as many on average: the half is a 50% chance of one more.
        internal static int Scale(int amount, float multiplier)
        {
            if (amount <= 0 || multiplier <= 1.0001f) return amount;
            float exact = amount * multiplier;
            int whole = Mathf.FloorToInt(exact);
            return whole + (UnityEngine.Random.value < exact - whole ? 1 : 0);
        }

        // Creature drops: the game rolls them as usual (stars, world modifiers), then the amounts are multiplied. Items
        // dropped once per player (boss keys and the like) stay as they are. Runs before Epic Loot's Lucky Loot, which
        // multiplies on top as it did with DropMoreLoot.
        [HarmonyPatch(typeof(CharacterDrop), nameof(CharacterDrop.GenerateDropList))]
        internal static class CreatureDrops
        {
            [HarmonyPriority(Priority.High)]
            private static void Postfix(CharacterDrop __instance, List<KeyValuePair<GameObject, int>> __result)
            {
                float multiplier = Plugin.LootCreatures.Value;
                if (multiplier <= 1.0001f || __result == null) return;
                HashSet<GameObject> perPlayer = null;
                foreach (var drop in __instance.m_drops)
                    if (drop != null && drop.m_onePerPlayer && drop.m_prefab) (perPlayer ?? (perPlayer = new HashSet<GameObject>())).Add(drop.m_prefab);
                for (int i = 0; i < __result.Count; i++)
                {
                    var prefab = __result[i].Key;
                    if ((perPlayer != null && perPlayer.Contains(prefab)) || !Multiplies(prefab)) continue;
                    __result[i] = new KeyValuePair<GameObject, int>(prefab, Scale(__result[i].Value, multiplier));
                }
            }
        }

        // Trees, logs, rocks, ore, destroyed objects and dungeon loot piles all roll their drops through this method.
        [HarmonyPatch(typeof(DropTable), nameof(DropTable.GetDropList), new Type[0])]
        internal static class ResourceDrops
        {
            [HarmonyPriority(Priority.High)]
            private static void Postfix(List<GameObject> __result)
            {
                float multiplier = Plugin.LootResources.Value;
                if (multiplier <= 1.0001f || __result == null || __result.Count == 0) return;
                int count = __result.Count;
                for (int i = 0; i < count; i++)
                {
                    var prefab = __result[i];
                    if (!Multiplies(prefab)) continue;
                    for (int extra = Scale(1, multiplier) - 1; extra > 0; extra--) __result.Add(prefab);
                }
            }
        }

        // Berries, mushrooms, crops and other pickables: each picked item comes as a bigger stack.
        [HarmonyPatch(typeof(Pickable), "Drop")]
        internal static class PickableDrops
        {
            [ThreadStatic] private static Pickable picking;

            [HarmonyPatch(typeof(Pickable), "RPC_Pick")]
            internal static class Scope
            {
                private static void Prefix(Pickable __instance, out Pickable __state)
                {
                    __state = picking;
                    picking = __instance;
                }

                private static void Finalizer(Pickable __state) => picking = __state;
            }

            private static void Prefix(Pickable __instance, GameObject prefab, ref int stack)
            {
                if (picking != __instance || prefab != __instance.m_itemPrefab || !Multiplies(prefab)) return;
                stack = Scale(stack, Plugin.LootPickables.Value);
            }
        }

        // Flint, stones, branches and other items lying on the ground (the pick-up count shows in their name too).
        [HarmonyPatch(typeof(PickableItem), "GetStackSize")]
        internal static class GroundItems
        {
            private static readonly AccessTools.FieldRef<PickableItem, int> Saved = AccessTools.FieldRefAccess<PickableItem, int>("m_stack");
            private static readonly ConditionalWeakTable<PickableItem, StrongBox<int>> Rolled = new ConditionalWeakTable<PickableItem, StrongBox<int>>();

            private static void Postfix(PickableItem __instance, ref int __result)
            {
                float multiplier = Plugin.LootPickables.Value;
                if (multiplier <= 1.0001f || !__instance.m_itemPrefab || !Multiplies(__instance.m_itemPrefab.gameObject)) return;
                // The same item always shows and gives the same count, even with a fractional multiplier.
                var rolled = Rolled.GetValue(__instance, _ => new StrongBox<int>(-1));
                if (rolled.Value < 0) rolled.Value = Scale(__result, multiplier);
                __result = Mathf.Clamp(rolled.Value, 1, __instance.m_itemPrefab.m_itemData.m_shared.m_maxStackSize);
            }
        }

        // Weight and stack size, set on the item definitions themselves so every copy of an item agrees: dropped,
        // crafted, bought or loaded with the character. Each definition remembers its own values so a changed setting
        // (or the server's, on joining) is applied from those, and a value another mod changes meanwhile is kept as the new base.
        [HarmonyPatch(typeof(ObjectDB), "Awake")]
        internal static class ItemDefinitions
        {
            private sealed class Base
            {
                internal float Weight, SetWeight = float.NaN;
                internal int Stack, SetStack = -1;
            }

            private static readonly ConditionalWeakTable<ItemDrop.ItemData.SharedData, Base> Bases = new ConditionalWeakTable<ItemDrop.ItemData.SharedData, Base>();

            [HarmonyPriority(Priority.Last)]
            private static void Postfix(ObjectDB __instance) => Apply(__instance);

            [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.CopyOtherDB))]
            internal static class MenuCopy
            {
                [HarmonyPriority(Priority.Last)]
                private static void Postfix(ObjectDB __instance) => Apply(__instance);
            }

            // Mods that add their items late are covered once the world has started.
            [HarmonyPatch(typeof(Game), "Start")]
            internal static class WorldStart
            {
                private static void Postfix() => Apply(ObjectDB.instance);
            }

            internal static void Apply(ObjectDB db)
            {
                if (!db || db.m_items == null) return;
                float weight = Mathf.Clamp01(Plugin.ItemWeight.Value);
                int stackMultiplier = Mathf.Clamp(Plugin.ItemStackSize.Value, 1, 10);
                foreach (var prefab in db.m_items)
                {
                    var shared = prefab ? prefab.GetComponent<ItemDrop>()?.m_itemData?.m_shared : null;
                    if (shared == null) continue;
                    var values = Bases.GetValue(shared, s => new Base { Weight = s.m_weight, Stack = s.m_maxStackSize });
                    if (!float.IsNaN(values.SetWeight) && !Mathf.Approximately(shared.m_weight, values.SetWeight)) values.Weight = shared.m_weight;
                    if (values.SetStack >= 0 && shared.m_maxStackSize != values.SetStack) values.Stack = shared.m_maxStackSize;
                    shared.m_weight = values.SetWeight = values.Weight * weight;
                    shared.m_maxStackSize = values.SetStack = values.Stack > 1 ? values.Stack * stackMultiplier : values.Stack;
                }
            }
        }

        [HarmonyPatch(typeof(ItemDrop), "Awake")]
        internal static class FloatingItems
        {
            private static void Prefix(ItemDrop __instance)
            {
                if (!Plugin.ItemsFloat.Value || __instance.GetComponent<Floating>() || !__instance.GetComponent<Rigidbody>()) return;
                if (__instance.m_itemData?.m_shared?.m_name == "fireballattack") return;
                __instance.gameObject.AddComponent<Floating>().m_waterLevelOffset = 0.5f;
            }
        }

        [HarmonyPatch(typeof(Player), "Awake")]
        internal static class PickupRange
        {
            private static void Postfix(Player __instance) => Apply(__instance);

            internal static void Apply(Player player)
            {
                if (player) player.m_autoPickupRange = Mathf.Clamp(Plugin.PickupRangeSetting.Value, 1f, 10f);
            }
        }
    }
}
