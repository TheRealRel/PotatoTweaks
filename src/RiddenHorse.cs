// A ridden Odin Horse rears to attack whatever is near whenever it can, which throws the rider around mid-fight
// (Valheim, PotatoTweaks). While someone rides it, the horse leaves the fighting to the rider. Other mounts are left
// alone (some mods have the rider command the mount's attacks).
using HarmonyLib;

namespace PotatoTweaks
{
    internal static class RiddenHorse
    {
        [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.StartAttack))]
        internal static class HoldsBack
        {
            private static bool Prefix(Humanoid __instance, ref bool __result)
            {
                if (__instance is Player || !__instance.HaveRider()) return true;
                if (!Utils.GetPrefabName(__instance.gameObject).StartsWith("rae_OdinHorse", System.StringComparison.OrdinalIgnoreCase)) return true;
                __result = false;
                return false;
            }
        }
    }
}
