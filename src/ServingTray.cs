// Potato Tweaks - every crafted food on the serving tray, written from scratch.
using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace PotatoTweaks
{
    // The serving tray (a build tool) can only set out the game's feasts: each is its own piece in the tray's piece table.
    // This adds a piece for every crafted food that doesn't have one yet, vanilla or modded, made from one of the game's
    // feast pieces: the feast's portions are swapped for copies of the dish, which disappear as servings are eaten. A tray
    // takes a number of the dish and gives that many servings. Raw food and food cooked on the cooking station have no
    // recipe and are left out. Every player has Potato Tweaks, so every game builds the same pieces in the same order.
    internal static class ServingTray
    {
        private const string TableName = "_FeasterPieceTable", Prefix = "PotatoTweaks_Tray_";
        internal static readonly Type[] Patches = { typeof(SceneReady), typeof(ItemsReady), typeof(LateFoods) };

        private static GameObject holder;
        private static readonly Dictionary<string, GameObject> Made = new Dictionary<string, GameObject>();
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
            try { AddPieces(db, scene); }
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

        private static void AddPieces(ObjectDB db, ZNetScene scene)
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
                var feast = piece ? piece.GetComponentInChildren<Feast>(true) : null;
                var food = feast ? (feast.m_foodItem ? feast.m_foodItem : piece.GetComponent<ItemDrop>()) : null;
                if (food) covered.Add(food.m_itemData.m_shared.m_name);
                // A piece of its own (not an item placed as is), with portions that can be swapped.
                if (feast && !piece.GetComponent<ItemDrop>() && piece.GetComponent<Piece>() && feast.m_feastParts.Any(level => level.m_onAboveEquals) &&
                    (!template || piece.name.IndexOf("Meadows", StringComparison.OrdinalIgnoreCase) >= 0))
                    template = piece;
            }
            if (!template)
            {
                Plugin.Log.LogWarning("Serving tray: no feast piece to build from; no foods added.");
                return;
            }
            var foods = db.m_recipes
                .Where(recipe => recipe && recipe.m_enabled && recipe.m_item && IsFood(recipe.m_item))
                .Select(recipe => recipe.m_item)
                .Where(item => !covered.Contains(item.m_itemData.m_shared.m_name))
                .GroupBy(item => item.gameObject.name).Select(group => group.First())
                .OrderBy(item => item.gameObject.name, StringComparer.Ordinal)
                .ToList();
            var namedPrefabs = Traverse.Create(scene).Field<Dictionary<int, GameObject>>("m_namedPrefabs").Value;
            var added = new List<string>();
            foreach (var food in foods)
            {
                if (table.m_pieces.Any(piece => piece && piece.name == Prefix + food.gameObject.name)) continue;
                var piece = Made.TryGetValue(food.gameObject.name, out var made) && made ? made : Create(template, food);
                if (!piece) continue;
                Made[food.gameObject.name] = piece;
                table.m_pieces.Add(piece);
                int hash = piece.name.GetStableHashCode();
                if (!scene.m_prefabs.Contains(piece)) scene.m_prefabs.Add(piece);
                if (namedPrefabs != null) namedPrefabs[hash] = piece;
                added.Add(food.gameObject.name);
            }
            if (added.Count > 0) Plugin.Log.LogInfo($"Serving tray: added {added.Count} foods (from {template.name}): {string.Join(", ", added)}");
        }

        // The dish as the item stands show it: its stand model, its only model part, or the whole item.
        private static GameObject Model(GameObject item)
        {
            var attach = item.transform.Find("attach");
            if (attach) return attach.gameObject;
            GameObject only = null;
            foreach (Transform child in item.transform)
            {
                if (child.gameObject.layer != item.layer) continue;
                if (only) { only = null; break; }
                only = child.gameObject;
            }
            return only ?? item;
        }

        private static GameObject Holder()
        {
            if (holder) return holder;
            holder = new GameObject("PotatoTweaks_TrayPieces");
            holder.SetActive(false);
            UnityEngine.Object.DontDestroyOnLoad(holder);
            return holder;
        }

        private static GameObject Create(GameObject template, ItemDrop food)
        {
            var model = Model(food.gameObject);
            if (!model || !Parts(model).Any()) return null;
            int servings = Mathf.Clamp(Plugin.TrayServings.Value, 1, 50);
            var piece = UnityEngine.Object.Instantiate(template, Holder().transform, false);
            piece.name = Prefix + food.gameObject.name;
            var feast = piece.GetComponentInChildren<Feast>(true);
            feast.m_foodItem = food;
            feast.m_eatStacks = servings;
            // Each portion of the feast shows a copy of the dish instead, sized to the portion. The portion itself stays
            // (the feast switches it on and off as servings are eaten); only its own meshes go.
            var keep = new HashSet<Transform>();
            foreach (var level in feast.m_feastParts)
                if (level.m_onBelow) keep.Add(level.m_onBelow.transform);
            var done = new HashSet<GameObject>();
            foreach (var level in feast.m_feastParts)
            {
                var portion = level.m_onAboveEquals;
                if (!portion || !done.Add(portion)) continue;
                var bounds = MeshBounds(portion, portion.transform);
                if (!bounds.HasValue) continue;
                foreach (var renderer in portion.GetComponentsInChildren<Renderer>(true))
                {
                    if (Kept(renderer.transform, portion.transform, keep)) continue;
                    var filter = renderer.GetComponent<MeshFilter>();
                    UnityEngine.Object.DestroyImmediate(renderer);
                    if (filter) UnityEngine.Object.DestroyImmediate(filter);
                }
                var spot = new Vector3(bounds.Value.center.x, bounds.Value.min.y, bounds.Value.center.z);
                Copy(model, portion.transform, spot, Mathf.Max(bounds.Value.size.x, bounds.Value.size.z));
            }
            var info = piece.GetComponent<Piece>();
            var shared = food.m_itemData.m_shared;
            info.m_name = shared.m_name;
            info.m_description = shared.m_description;
            if (shared.m_icons != null && shared.m_icons.Length > 0) info.m_icon = shared.m_icons[0];
            var first = info.m_resources != null && info.m_resources.Length > 0 ? info.m_resources[0] : null;
            info.m_resources = new[]
            {
                new Piece.Requirement { m_resItem = food, m_amount = servings, m_amountPerLevel = 0, m_recover = first?.m_recover ?? true },
            };
            return piece;
        }

        // Whether a part belongs to another stage of the feast (an empty plate) rather than to this portion.
        private static bool Kept(Transform part, Transform portion, HashSet<Transform> keep)
        {
            for (var at = part; at && at != portion; at = at.parent)
                if (keep.Contains(at)) return true;
            return keep.Contains(portion);
        }

        // The dish's meshes, only the most detailed ones when it has several levels of detail.
        private static IEnumerable<MeshFilter> Parts(GameObject model)
        {
            var hidden = new HashSet<Renderer>();
            foreach (var group in model.GetComponentsInChildren<LODGroup>(true))
            {
                var lods = group.GetLODs();
                for (int i = 1; i < lods.Length; i++)
                    foreach (var renderer in lods[i].renderers)
                        if (renderer) hidden.Add(renderer);
                if (lods.Length > 0)
                    foreach (var renderer in lods[0].renderers)
                        if (renderer) hidden.Remove(renderer);
            }
            return model.GetComponentsInChildren<MeshFilter>(true)
                .Where(filter => filter.sharedMesh && filter.GetComponent<MeshRenderer>() && !hidden.Contains(filter.GetComponent<MeshRenderer>()));
        }

        private static Bounds? MeshBounds(GameObject root, Transform space, IEnumerable<MeshFilter> filters = null)
        {
            Bounds? total = null;
            foreach (var filter in filters ?? root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!filter || !filter.sharedMesh) continue;
                var local = filter.sharedMesh.bounds;
                var matrix = space.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                var corners = new[] { new Vector3(-1, -1, -1), new Vector3(-1, -1, 1), new Vector3(-1, 1, -1), new Vector3(-1, 1, 1),
                    new Vector3(1, -1, -1), new Vector3(1, -1, 1), new Vector3(1, 1, -1), new Vector3(1, 1, 1) };
                foreach (var corner in corners)
                {
                    var point = matrix.MultiplyPoint3x4(local.center + Vector3.Scale(local.extents, corner));
                    if (total.HasValue) { var grown = total.Value; grown.Encapsulate(point); total = grown; }
                    else total = new Bounds(point, Vector3.zero);
                }
            }
            return total;
        }

        // A plain copy of the dish's meshes (no item, physics or network parts), resting on the portion's spot and as wide.
        private static void Copy(GameObject model, Transform parent, Vector3 position, float size)
        {
            var copy = new GameObject("PotatoTweaks_Dish");
            copy.layer = parent.gameObject.layer;
            copy.transform.SetParent(parent, false);
            copy.transform.localPosition = position;
            var inner = new GameObject("model");
            inner.transform.SetParent(copy.transform, false);
            foreach (var filter in Parts(model))
            {
                var renderer = filter.GetComponent<MeshRenderer>();
                var part = new GameObject(filter.name);
                part.layer = parent.gameObject.layer;
                part.transform.SetParent(inner.transform, false);
                var matrix = model.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                part.transform.localPosition = matrix.GetColumn(3);
                part.transform.localRotation = Quaternion.LookRotation(matrix.GetColumn(2), matrix.GetColumn(1));
                part.transform.localScale = new Vector3(matrix.GetColumn(0).magnitude, matrix.GetColumn(1).magnitude, matrix.GetColumn(2).magnitude);
                part.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                part.AddComponent<MeshRenderer>().sharedMaterials = renderer.sharedMaterials;
            }
            // Sized to the portion it replaces, and resting on the spot rather than centred in it.
            var bounds = MeshBounds(inner, copy.transform);
            if (bounds.HasValue)
            {
                float own = Mathf.Max(bounds.Value.size.x, bounds.Value.size.z);
                float scale = own > 0.0001f && size > 0.0001f ? Mathf.Clamp(size / own * Mathf.Max(0.1f, Plugin.TrayDishSize.Value), 0.05f, 20f) : 1f;
                inner.transform.localScale = Vector3.one * scale;
                var centre = bounds.Value.center * scale;
                inner.transform.localPosition = new Vector3(-centre.x, -(bounds.Value.min.y * scale), -centre.z);
            }
        }
    }
}
