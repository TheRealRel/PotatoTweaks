// Potato Tweaks - keeps Quick Stack Store's sorting and stacking out of Equipment and Quick Slots' slots.
// Written for this mod.
using System;
using System.Reflection;
using BepInEx.Bootstrap;
using HarmonyLib;
using UnityEngine;

namespace PotatoTweaks
{
    // Equipment and Quick Slots keeps its quick slots (Z, V, B) and equipment slots as extra rows below the inventory.
    // Quick Stack Store knows several other slot mods but not this one, so its sort, quick stack and store all treated
    // those rows as ordinary inventory: sorting moved items into and out of the quick and equipment slots. This tells it
    // that every cell from those rows on is a slot to leave alone. Quick slots can still be topped up by its restock.
    internal static class QuickStackSlots
    {
        internal const string QuickStackId = "goldenrevolver.quick_stack_store";
        private const string SlotsId = "randyknapp.mods.equipmentandquickslots";

        private static Func<int> visibleRows;
        private static MethodInfo slotAt;
        private static PropertyInfo isQuickSlot;

        internal static bool Wanted() => Chainloader.PluginInfos.ContainsKey(QuickStackId) && Chainloader.PluginInfos.ContainsKey(SlotsId);

        internal static void Enable(Harmony harmony)
        {
            var api = AccessTools.TypeByName("EquipmentAndQuickSlots.API");
            var rows = api != null ? AccessTools.Method(api, "GetVisibleRows") : null;
            var check = AccessTools.Method(AccessTools.TypeByName("QuickStackStore.CompatibilitySupport"), "InternalIsEquipOrQuickSlot");
            if (rows == null || check == null) throw new MissingMemberException("Quick Stack Store or Equipment and Quick Slots changed.");
            visibleRows = AccessTools.MethodDelegate<Func<int>>(rows);
            var slots = AccessTools.TypeByName("EquipmentAndQuickSlots.Slots");
            slotAt = slots != null ? AccessTools.Method(slots, "GetSlotInGrid", new[] { typeof(Vector2i) }) : null;
            isQuickSlot = slotAt != null ? AccessTools.Property(slotAt.ReturnType, "IsQuickSlot") : null;
            harmony.Patch(check, postfix: new HarmonyMethod(typeof(QuickStackSlots), nameof(Postfix)));
        }

        // Quick Stack Store only asks this about the player's own inventory.
        private static void Postfix(Vector2i itemPos, bool includeRestockableSlots, ref bool __result)
        {
            if (__result || visibleRows == null || itemPos.y < visibleRows()) return;
            // Its restock asks without restockable slots: a quick slot's stack may still be topped up.
            if (!includeRestockableSlots && IsQuickSlot(itemPos)) return;
            __result = true;
        }

        private static bool IsQuickSlot(Vector2i position)
        {
            if (slotAt == null || isQuickSlot == null) return false;
            try
            {
                var slot = slotAt.Invoke(null, new object[] { position });
                return slot != null && (bool)isQuickSlot.GetValue(slot);
            }
            catch
            {
                return false;
            }
        }
    }
}
