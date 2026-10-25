// Two small forge-side additions (Valheim, Potato Tweaks).
//
// Blast furnace takes all: the blast furnace also smelts everything the regular smelter does (copper, tin, iron and
// silver ore, the scraps, and any ore another mod adds to the smelter). Each blast furnace, as it loads, gets the
// smelter's conversions it doesn't already have, read from the smelter prefab itself, so nothing is hard-coded. Idea
// from Blast Furnace Takes All by TastyChickenLegs (after Tueman's original); with that mod installed, Potato Tweaks
// leaves it to it.
//
// Craftable chain: Chain can be made at the forge from 2 Iron, one per craft. Added through Jotunn as a recipe that the
// server setting switches on and off live (Recipe.m_enabled). Idea from Chain Manager by Digitalroot; with that mod
// installed, Potato Tweaks doesn't add a second recipe.
using System.Linq;
using HarmonyLib;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace PotatoTweaks
{
    internal static class Forging
    {
        internal const string BlastFurnaceModId = "tastychickenlegs.BlastFurnaceTakesAll", ChainModId = "digitalroot.mods.chainmanager";

        [HarmonyPatch(typeof(Smelter), "Awake")]
        internal static class BlastFurnaceTakesAll
        {
            [HarmonyPriority(Priority.Low)]
            private static void Prefix(Smelter __instance)
            {
                if (!Plugin.BlastFurnaceTakesAll.Value || Utils.GetPrefabName(__instance.gameObject) != "blastfurnace") return;
                var smelter = ZNetScene.instance ? ZNetScene.instance.GetPrefab("smelter")?.GetComponent<Smelter>() : null;
                if (!smelter || smelter.m_conversion == null) return;
                foreach (var conversion in smelter.m_conversion)
                {
                    if (conversion?.m_from == null || conversion.m_to == null) continue;
                    if (__instance.m_conversion.Any(c => c?.m_from != null && c.m_from.gameObject.name == conversion.m_from.gameObject.name)) continue;
                    __instance.m_conversion.Add(new Smelter.ItemConversion { m_from = conversion.m_from, m_to = conversion.m_to });
                }
            }
        }

        private static CustomRecipe chain;

        internal static void AddChainRecipe()
        {
            chain = new CustomRecipe(new RecipeConfig
            {
                Name = "Recipe_PotatoTweaks_Chain",
                Item = "Chain",
                Amount = 1,
                CraftingStation = "forge",
                MinStationLevel = 1,
                Requirements = new[] { new RequirementConfig { Item = "Iron", Amount = 2 } },
            });
            ItemManager.Instance.AddRecipe(chain);
            ItemManager.OnItemsRegistered += ApplyChain;
            Plugin.CraftableChain.SettingChanged += (_, __) => ApplyChain();
        }

        private static void ApplyChain()
        {
            if (chain?.Recipe) chain.Recipe.m_enabled = Plugin.CraftableChain.Value;
        }
    }
}
