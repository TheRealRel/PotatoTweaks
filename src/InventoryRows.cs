// Potato Tweaks - Haldor's inventory rows: earlier unlocks and a third row.
// Adapted from EpicLoot Progression Fix 0.1.281 (inventory upgrade progression), MIT License, Copyright (c) 2026
// EpicLoot Progression Fix contributors. See LICENSE.
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;

namespace PotatoTweaks
{
    // Haldor sells two inventory rows (one each, saved on the character). Optionally unlock them after the Elder and
    // Bonemass instead of Moder and the Queen, and add a third, Roomier pockets, after Yagluth. The third uses the same
    // purchase key as Progression Fix, so a character that already bought it there can't buy it twice.
    [HarmonyPatch(typeof(Trader), nameof(Trader.GetAvailableItems))]
    internal static class InventoryRows
    {
        private const string RowsKey = "invrows";
        internal const string ThirdRowKey = "valheim.epiclootprogressionfix.invslot3";
        private static readonly ConditionalWeakTable<Trader.TradeItem, string> OriginalRequirement = new ConditionalWeakTable<Trader.TradeItem, string>();

        private static void Prefix(Trader __instance)
        {
            if (__instance.m_name != "$npc_haldor" || __instance.m_items == null) return;
            Trader.TradeItem second = null, third = null;
            foreach (var item in __instance.m_items)
            {
                if (item == null || item.m_incrementKey != RowsKey) continue;
                if (item.m_buyKey == ThirdRowKey) { third = item; continue; }
                string original = OriginalRequirement.GetValue(item, i => i.m_requiredGlobalKey ?? "");
                if (item.m_buyKey == "invslot1") item.m_requiredGlobalKey = Plugin.RowsEarlier.Value ? "defeated_gdking" : original;
                else if (item.m_buyKey == "invslot2")
                {
                    item.m_requiredGlobalKey = Plugin.RowsEarlier.Value ? "defeated_bonemass" : original;
                    second = item;
                }
            }

            if (!Plugin.RowsThird.Value)
            {
                if (third != null) __instance.m_items.Remove(third);
                return;
            }
            if (third == null && second != null)
            {
                third = new Trader.TradeItem
                {
                    m_levelUpEffect = second.m_levelUpEffect,
                    m_buyPlayerEffects = second.m_buyPlayerEffects,
                    m_icon = second.m_icon,
                    m_name = "Roomier pockets",
                    m_tooltip = "Permanently adds one inventory row (8 slots).",
                    m_buyKey = ThirdRowKey,
                    m_incrementKey = RowsKey,
                    m_incrementAmount = 1,
                };
                __instance.m_items.Add(third);
            }
            if (third != null)
            {
                third.m_price = Plugin.RowsThirdPrice.Value;
                third.m_requiredGlobalKey = Plugin.RowsThirdBoss.Value;
            }
        }
    }
}
