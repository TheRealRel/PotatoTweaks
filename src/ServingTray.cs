// Potato Tweaks - every crafted food on the serving tray, written from scratch.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace PotatoTweaks
{
    // The serving tray (a build tool) sets out the game's feasts and many of its dishes. A dish is the food item itself,
    // placed as a piece: using it eats it and it's gone. This makes every other crafted food placeable the same way,
    // vanilla or modded, by giving the food the same piece parts as one of the game's placeable dishes and adding it to
    // the tray. Raw food and food cooked on the cooking station have no recipe and are left out. Feasts stay as they are.
    // Every player has Potato Tweaks, so every game adds the same foods.
    internal static class ServingTray
    {
        private const string TableName = "_FeasterPieceTable";
        internal static readonly Type[] Patches = { typeof(SceneReady), typeof(ItemsReady), typeof(LateFoods) };

        private static ObjectDB builtFor;
        private static ZNetScene builtIn;

        [HarmonyPatch(typeof(ZNetScene), "Awake")]
        internal static class SceneReady
        {
            [HarmonyPriority(Priority.Last)]
            private static void Postfix() => Build();
        }

        [HarmonyPatch(typeof(ObjectDB), "Awake")]
        internal static class ItemsReady
        {
            [HarmonyPriority(Priority.Last)]
            private static void Postfix() => Build();
        }

        // Catches foods that mods add after the game's own setup; foods already on the tray are skipped.
        [HarmonyPatch(typeof(Player), nameof(Player.OnSpawned))]
        internal static class LateFoods
        {
            private static void Postfix(Player __instance)
            {
                if (__instance == Player.m_localPlayer) Build(again: true);
            }
        }

        // Runs once both the item list and the network prefab list of this world are ready, whichever comes second.
        private static void Build(bool again = false)
        {
            var db = ObjectDB.instance;
            var scene = ZNetScene.instance;
            if (!Plugin.TrayEnabled.Value || !db || !scene || db.m_items.Count == 0 || (!again && builtFor == db && builtIn == scene)) return;
            builtFor = db;
            builtIn = scene;
            try { AddFoods(db); }
            catch (Exception e) { Plugin.Log.LogWarning("Serving tray: couldn't add foods: " + e); }
        }

        private static PieceTable Table(ObjectDB db)
        {
            foreach (var item in db.m_items)
            {
                var table = item ? item.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_buildPieces : null;
                if (table && table.name == TableName) return table;
            }
            return null;
        }

        private static bool IsFood(ItemDrop item)
        {
            var shared = item?.m_itemData?.m_shared;
            return shared != null && shared.m_itemType == ItemDrop.ItemData.ItemType.Consumable &&
                (shared.m_food > 0f || shared.m_foodStamina > 0f || shared.m_foodEitr > 0f);
        }

        private static void AddFoods(ObjectDB db)
        {
            var table = Table(db);
            if (!table)
            {
                Plugin.Log.LogWarning("Serving tray: the tray's piece table wasn't found; no foods added.");
                return;
            }
            var covered = new HashSet<string>();
            GameObject template = null;
            foreach (var piece in table.m_pieces)
            {
                if (!piece) continue;
                var item = piece.GetComponent<ItemDrop>();
                var feast = piece.GetComponentInChildren<Feast>(true);
                var food = item ? item : feast ? feast.m_foodItem : null;
                if (food) covered.Add(food.m_itemData.m_shared.m_name);
                // One of the game's dishes: a food item that is placed as is.
                if (!template && item && IsFood(item) && piece.GetComponent<Piece>() && piece.GetComponent<WearNTear>() && !feast)
                    template = piece;
            }
            if (!template)
            {
                Plugin.Log.LogWarning("Serving tray: no placeable dish to copy from; no foods added.");
                return;
            }
            var foods = db.m_recipes
                .Where(recipe => recipe && recipe.m_enabled && recipe.m_item && IsFood(recipe.m_item))
                .Select(recipe => recipe.m_item)
                .Where(item => !covered.Contains(item.m_itemData.m_shared.m_name) && !table.m_pieces.Contains(item.gameObject))
                .GroupBy(item => item.gameObject.name).Select(group => group.First())
                .OrderBy(item => item.gameObject.name, StringComparer.Ordinal)
                .ToList();
            var added = new List<string>();
            foreach (var food in foods)
            {
                try
                {
                    if (!MakePlaceable(template, food)) continue;
                    table.m_pieces.Add(food.gameObject);
                    added.Add(food.gameObject.name);
                }
                catch (Exception e) { Plugin.Log.LogWarning($"Serving tray: couldn't add {food.gameObject.name}: {e.Message}"); }
            }
            if (added.Count > 0) Plugin.Log.LogInfo($"Serving tray: added {added.Count} foods (like {template.name}): {string.Join(", ", added)}");
        }

        // Gives the food the piece parts the game's dishes have: a Piece (what the tray shows and charges: the food
        // itself) and a WearNTear, which the item keeps switched off until it's placed. Settings come from the game's dish.
        private static bool MakePlaceable(GameObject template, ItemDrop food)
        {
            var prefab = food.gameObject;
            if (!prefab.GetComponent<ZNetView>() || !prefab.GetComponentInChildren<Collider>(true)) return false;
            var piece = prefab.GetComponent<Piece>();
            if (!piece)
            {
                piece = prefab.AddComponent<Piece>();
                CopyFields(template.GetComponent<Piece>(), piece, template.transform);
            }
            if (!prefab.GetComponent<WearNTear>())
                CopyFields(template.GetComponent<WearNTear>(), prefab.AddComponent<WearNTear>(), template.transform);
            var shared = food.m_itemData.m_shared;
            piece.m_name = shared.m_name;
            piece.m_description = shared.m_description;
            if (shared.m_icons != null && shared.m_icons.Length > 0) piece.m_icon = shared.m_icons[0];
            var first = template.GetComponent<Piece>().m_resources?.FirstOrDefault();
            piece.m_resources = new[]
            {
                new Piece.Requirement { m_resItem = food, m_amount = 1, m_amountPerLevel = 0, m_recover = first?.m_recover ?? true },
            };
            return true;
        }

        // Copies a component's settings. Anything pointing into the dish's own model (its worn and broken looks, for
        // example) is left empty, since the food has its own model.
        private static void CopyFields(Component from, Component to, Transform owner)
        {
            if (!from || !to) return;
            for (var type = from.GetType(); type != null && type != typeof(MonoBehaviour); type = type.BaseType)
            {
                foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (!field.IsPublic && field.GetCustomAttribute<SerializeField>() == null) continue;
                    if (field.IsInitOnly || field.IsLiteral) continue;
                    var value = field.GetValue(from);
                    field.SetValue(to, Inside(value, owner) ? Empty(field.FieldType) : value);
                }
            }
        }

        private static bool Inside(object value, Transform owner)
        {
            switch (value)
            {
                case GameObject gameObject: return gameObject && gameObject.transform.IsChildOf(owner);
                case Component component: return component && component.transform.IsChildOf(owner);
                case IEnumerable list when !(value is string):
                    foreach (var entry in list) if (Inside(entry, owner)) return true;
                    return false;
                default: return false;
            }
        }

        private static object Empty(Type type)
        {
            if (type.IsArray) return Array.CreateInstance(type.GetElementType(), 0);
            if (typeof(IList).IsAssignableFrom(type) && !type.IsAbstract) return Activator.CreateInstance(type);
            return type.IsValueType ? Activator.CreateInstance(type) : null;
        }
    }
}
