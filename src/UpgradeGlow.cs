// Potato Tweaks - strength of the glow on gear upgraded past its normal maximum, written from scratch.
using HarmonyLib;
using UnityEngine;

namespace PotatoTweaks
{
    // Valheim (Forge of Potential): weapons and other items carry a ParticleIntensityScaler. At or below the item's
    // normal max level its intensity is 0 (no glow). Past it, intensity = 1 + (level - max) * multiplier, so the
    // particles, glow colour and light grow with every extra level. ApplyIntensity rebuilds the effect from the
    // prefab's cached original values each time, so scaling the intensity here is safe and can change live.
    [HarmonyPatch(typeof(ParticleIntensityScaler), nameof(ParticleIntensityScaler.SetQuality))]
    internal static class UpgradeGlow
    {
        private static readonly AccessTools.FieldRef<ParticleIntensityScaler, int> LastLevel =
            AccessTools.FieldRefAccess<ParticleIntensityScaler, int>("lastItemDropLevel");

        private static float Factor => Plugin.GlowEnabled.Value ? Plugin.GlowStrength.Value : 1f;

        // The game's own value for a level, recomputed every time so the factor is never applied twice.
        private static float GameIntensity(ParticleIntensityScaler scaler, int level) =>
            level > scaler.maxQuality ? 1f + (level - scaler.maxQuality) * scaler.itemDropLevelMultiplier : 0f;

        private static void Postfix(ParticleIntensityScaler __instance, int quality)
        {
            if (quality <= __instance.maxQuality || Mathf.Approximately(Factor, 1f)) return;
            __instance.intensity = GameIntensity(__instance, quality) * Factor;
            __instance.ApplyIntensity();
        }

        // The game re-reads the "original" particle values every time the effect is switched back on, which picks up
        // values it has already scaled (and, at strength 0, zeros the glow for good). Once an item has its real
        // originals, keep them and just re-apply.
        [HarmonyPatch(typeof(ParticleIntensityScaler), "OnEnable")]
        internal static class KeepOriginals
        {
            private static readonly AccessTools.FieldRef<ParticleIntensityScaler, System.Array> Originals =
                AccessTools.FieldRefAccess<ParticleIntensityScaler, System.Array>("originalData");

            private static bool Prefix(ParticleIntensityScaler __instance)
            {
                var originals = Originals(__instance);
                if (originals == null || originals.Length == 0) return true;
                __instance.ApplyIntensity();
                return false;
            }
        }

        // Moving the slider updates every glowing item already in the world and in players' hands.
        internal static void RefreshAll()
        {
            foreach (var scaler in Object.FindObjectsByType<ParticleIntensityScaler>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (scaler == null) continue;
                int level = LastLevel(scaler);
                if (level <= scaler.maxQuality) continue;
                scaler.intensity = GameIntensity(scaler, level) * Factor;
                scaler.ApplyIntensity();
            }
        }
    }
}
