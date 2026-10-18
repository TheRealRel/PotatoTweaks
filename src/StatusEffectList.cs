// Potato Tweaks - status effect icons without their names.
// Written from scratch for this mod; the idea is from RandyKnapp's Minimal Status Effects.
using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace PotatoTweaks
{
    // The game shows status effects as a row of icons beside the minimap, with names under them that get cut short.
    // This hides the names and leaves everything else as the game has it: same place, same icons, and the time left
    // (or the comfort level while resting) on each icon. Personal.
    //
    // The name is hidden by switching off its text, not its object: the game looks for each entry's first visible text
    // every frame, and an entry with neither a name nor a timer showing (shelter, warmth) would leave it nothing to find,
    // stopping the rest of the HUD from updating (hover prompts, the piece health bar, stamina).
    [HarmonyPatch(typeof(Hud), "UpdateStatusEffects")]
    internal static class StatusEffectList
    {
        private static readonly AccessTools.FieldRef<Hud, List<RectTransform>> Entries = AccessTools.FieldRefAccess<Hud, List<RectTransform>>("m_statusEffects");

        private static void Postfix(Hud __instance)
        {
            var entries = Entries(__instance);
            if (entries == null) return;
            bool show = !Plugin.StatusEffectNamesHidden.Value;
            foreach (var entry in entries)
            {
                if (!entry) continue;
                var name = entry.Find("Name");
                var text = name ? name.GetComponent<TMP_Text>() : null;
                if (text && text.enabled != show) text.enabled = show;
            }
        }
    }
}
