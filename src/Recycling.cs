// Potato Tweaks - recycling at the station an item is made at, written from scratch.
using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace PotatoTweaks
{
    // With a crafting station open, Ctrl + right-click an item in your inventory to take it apart there and get back a
    // share of what went into it (half by default, upgrades included). It has to be the station the item is made at, at
    // the level its recipe needs; items made by hand can be taken apart with just the inventory open. Equipped items,
    // food and potions, and items whose recipe takes only one of several ingredients can't be recycled. The tooltip of a
    // recyclable item says what it gives back. Enchanted items, and items on your hotbar (if set), ask first.
    // Each player's own game does it, with the server's settings.
    internal static class Recycling
    {
        internal static readonly Type[] Patches = { typeof(RightClick), typeof(Tooltip), typeof(KeyHint) };

        // Set when Epic Loot is installed; otherwise any Epic Loot data on the item counts as enchanted.
        internal static Func<ItemDrop.ItemData, bool> Enchanted = item =>
            item?.m_customData != null && item.m_customData.Keys.Any(key => key.IndexOf("epicloot", StringComparison.OrdinalIgnoreCase) >= 0);

        private static bool popupOpen;

        private sealed class Plan
        {
            internal Recipe Recipe;
            internal int Batch;
            internal List<KeyValuePair<ItemDrop, int>> Returns = new List<KeyValuePair<ItemDrop, int>>();
            internal string Problem;
        }

        private static bool ModifierHeld() => ZInput.GetKey(Plugin.RecycleKey.Value) ||
            (Plugin.RecycleKey.Value == KeyCode.LeftControl && ZInput.GetKey(KeyCode.RightControl));

        // What recycling this item here would give, or why it can't be recycled here (null Plan: not recyclable at all).
        private static Plan Check(Player player, ItemDrop.ItemData item)
        {
            if (!Plugin.RecycleEnabled.Value || player == null || item == null || ObjectDB.instance == null) return null;
            var type = item.m_shared.m_itemType;
            if (item.m_shared.m_questItem || type == ItemDrop.ItemData.ItemType.Consumable || type == ItemDrop.ItemData.ItemType.Trophy) return null;
            var recipe = ObjectDB.instance.GetRecipe(item);
            if (recipe == null || !recipe.m_item || recipe.m_resources == null || recipe.m_resources.Length == 0 || recipe.m_requireOnlyOneIngredient) return null;
            var plan = new Plan { Recipe = recipe, Batch = Mathf.Max(1, recipe.m_amount) };

            int quality = Mathf.Max(1, item.m_quality);
            var required = recipe.GetRequiredStation(1);
            var current = player.GetCurrentCraftingStation();
            if (required)
            {
                if (!current || current.m_name != required.m_name) return null;
                int level = recipe.GetRequiredStationLevel(Mathf.Min(quality, Mathf.Max(1, item.m_shared.m_maxQuality)));
                if (current.GetLevel() < level) plan.Problem = $"needs a level {level} {Localization.instance.Localize(required.m_name)}";
            }
            else if (current) return null;

            if (player.IsItemEquiped(item)) plan.Problem = "unequip it first";
            else if (item.m_stack < plan.Batch) plan.Problem = $"needs {plan.Batch} to recycle";

            // Everything that went into it: the recipe, plus every upgrade when upgrades count. The idols and other
            // upgrader-only materials of the Forge of Potential aren't returned.
            int levels = Plugin.RecycleUpgrades.Value ? Mathf.Min(quality, Mathf.Max(1, item.m_shared.m_maxQuality)) : 1;
            float share = Mathf.Clamp(Plugin.RecycleShare.Value, 0f, 100f) / 100f;
            KeyValuePair<ItemDrop, int> main = default;
            int mainTotal = 0;
            foreach (var requirement in recipe.m_resources)
            {
                if (requirement?.m_resItem == null || requirement.m_upgraderResource) continue;
                int total = 0;
                for (int level = 1; level <= levels; level++) total += Mathf.Max(0, requirement.GetAmount(level));
                if (total > mainTotal) { mainTotal = total; main = new KeyValuePair<ItemDrop, int>(requirement.m_resItem, 0); }
                int back = Mathf.FloorToInt(total * share + 0.0001f);
                if (back > 0) plan.Returns.Add(new KeyValuePair<ItemDrop, int>(requirement.m_resItem, back));
            }
            // At least one of the main material, so nothing is recycled for nothing (unless the share is 0).
            if (plan.Returns.Count == 0 && main.Key != null && share > 0f) plan.Returns.Add(new KeyValuePair<ItemDrop, int>(main.Key, 1));
            if (plan.Returns.Count == 0 && plan.Problem == null) plan.Problem = "gives nothing back";
            return plan;
        }

        private static string Describe(Plan plan) =>
            string.Join(", ", plan.Returns.Select(entry => $"{entry.Value} {Localization.instance.Localize(entry.Key.m_itemData.m_shared.m_name)}"));

        private static string KeyName() => Plugin.RecycleKey.Value == KeyCode.LeftControl ? "Ctrl" : Plugin.RecycleKey.Value.ToString();

        private static void Request(Player player, ItemDrop.ItemData item)
        {
            var plan = Check(player, item);
            if (plan == null) return;
            if (plan.Problem != null)
            {
                player.Message(MessageHud.MessageType.Center, "Can't recycle: " + plan.Problem);
                return;
            }
            string name = Localization.instance.Localize(item.m_shared.m_name);
            bool enchanted = Enchanted(item);
            bool hotbar = Plugin.RecycleConfirmHotbar.Value && item.m_gridPos.y == 0 && player.GetInventory().ContainsItem(item);
            if (!enchanted && !hotbar)
            {
                Recycle(player, item);
                return;
            }
            if (popupOpen) return;
            popupOpen = true;
            string text = enchanted
                ? $"{name} is enchanted. Recycling it destroys the enchantment and gives back only {Describe(plan)}."
                : $"{name} is on your hotbar. Recycle it for {Describe(plan)}?";
            UnifiedPopup.Push(new YesNoPopup(enchanted ? "Recycle an enchanted item?" : "Recycle?", text,
                () => { Close(); Recycle(Player.m_localPlayer, item); },
                Close, localizeText: false));
        }

        private static void Close()
        {
            popupOpen = false;
            UnifiedPopup.Pop();
        }

        // Checked again when it happens, since a confirmation can come after things changed.
        private static void Recycle(Player player, ItemDrop.ItemData item)
        {
            if (!player || !player.GetInventory().ContainsItem(item)) return;
            var plan = Check(player, item);
            if (plan == null || plan.Problem != null)
            {
                if (plan?.Problem != null) player.Message(MessageHud.MessageType.Center, "Can't recycle: " + plan.Problem);
                return;
            }
            var inventory = player.GetInventory();
            string name = Localization.instance.Localize(item.m_shared.m_name);
            bool removed = item.m_stack > plan.Batch ? inventory.RemoveItem(item, plan.Batch) : inventory.RemoveItem(item);
            if (!removed) return;
            foreach (var entry in plan.Returns)
            {
                // Added to the inventory; whatever doesn't fit drops at your feet.
                var added = inventory.AddItem(entry.Key.gameObject.name, entry.Value, 1, 0, 0L, "", false);
                if (added == null)
                {
                    var drop = entry.Key.m_itemData.Clone();
                    drop.m_dropPrefab = entry.Key.gameObject;
                    ItemDrop.DropItem(drop, entry.Value, player.transform.position + player.transform.forward + Vector3.up, Quaternion.identity);
                }
            }
            var station = player.GetCurrentCraftingStation();
            if (station) station.m_craftItemDoneEffects.Create(player.transform.position, Quaternion.identity);
            player.Message(MessageHud.MessageType.TopLeft, $"Recycled {(plan.Batch > 1 ? plan.Batch + " " : "")}{name}: {Describe(plan)}");
        }

        // Ctrl + right-click (by default) on an item in your own inventory.
        [HarmonyPatch(typeof(InventoryGui), "OnRightClickItem")]
        internal static class RightClick
        {
            [HarmonyPriority(Priority.High)]
            private static bool Prefix(InventoryGui __instance, InventoryGrid grid, ItemDrop.ItemData item)
            {
                var player = Player.m_localPlayer;
                // With a chest open, right-click keeps moving items, as with Adventure Backpacks' quick transfer.
                if (!Plugin.RecycleEnabled.Value || item == null || !player || !ModifierHeld() || grid == null || grid.GetInventory() != player.GetInventory() ||
                    __instance.IsContainerOpen()) return true;
                if (Check(player, item) == null) return true;
                Request(player, item);
                return false;
            }
        }

        // The item tooltip says what it gives back here.
        [HarmonyPatch(typeof(ItemDrop.ItemData), nameof(ItemDrop.ItemData.GetTooltip),
            new[] { typeof(ItemDrop.ItemData), typeof(int), typeof(bool), typeof(float), typeof(int), typeof(bool) })]
        internal static class Tooltip
        {
            private static void Postfix(ItemDrop.ItemData item, bool crafting, ref string __result)
            {
                if (crafting || !Plugin.RecycleEnabled.Value || !InventoryGui.IsVisible()) return;
                var player = Player.m_localPlayer;
                if (!player || !player.GetInventory().ContainsItem(item)) return;
                var plan = Check(player, item);
                if (plan == null) return;
                __result += plan.Problem != null
                    ? $"\n\n<color=#999999>Recycle here: {plan.Problem}</color>"
                    : $"\n\n<color=orange>{KeyName()} + right-click: recycle{(plan.Batch > 1 ? " " + plan.Batch : "")} for {Describe(plan)}</color>";
            }
        }

        // A hint with the game's other inventory hints at the bottom of the screen, while a crafting station is open.
        [HarmonyPatch(typeof(KeyHints), "UpdateHints")]
        internal static class KeyHint
        {
            private const string Name = "PotatoTweaks_Recycle";
            private static GameObject hint;
            private static bool failed;

            private static void Postfix(KeyHints __instance)
            {
                if (failed) return;
                if (!hint && !Create(__instance)) return;
                var player = Player.m_localPlayer;
                bool show = Plugin.RecycleEnabled.Value && player && InventoryGui.IsVisible() && !InventoryGui.instance.IsContainerOpen() &&
                    player.GetCurrentCraftingStation() && __instance.m_inventoryHints.activeSelf;
                if (hint.activeSelf != show) hint.SetActive(show);
            }

            // A copy of one of the game's keyboard hints (a key box and its text).
            private static bool Create(KeyHints hints)
            {
                var keyboard = hints.m_inventoryHints ? hints.m_inventoryHints.GetComponent<UIInputHint>()?.m_mouseKeyboardHint : null;
                Transform source = null;
                if (keyboard)
                    foreach (Transform child in keyboard.transform)
                        if (child.Find("key_bkg/Key") && child.Find("Text")) { source = child; break; }
                if (!source)
                {
                    failed = true;
                    Plugin.Log.LogWarning("Recycling: the inventory key hints changed; the recycle hint stays off (the item tooltips still show it).");
                    return false;
                }
                hint = UnityEngine.Object.Instantiate(source.gameObject, source.parent, false);
                hint.name = Name;
                foreach (var localize in hint.GetComponentsInChildren<Localize>(true)) UnityEngine.Object.Destroy(localize);
                hint.transform.Find("key_bkg/Key").GetComponent<TMP_Text>().text = KeyName() + " + Right click";
                hint.transform.Find("Text").GetComponent<TMP_Text>().text = "Recycle";
                hint.transform.SetAsLastSibling();
                hint.SetActive(false);
                return true;
            }
        }
    }
}
