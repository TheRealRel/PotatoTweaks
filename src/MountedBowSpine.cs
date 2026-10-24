// Better Archery turns the spine toward where you look at the end of every frame while you draw a bow (no setting
// for it). On a mount, the riding mod (the Odin Horse) twists the same spine to aim and checks each frame whether the
// spine still holds what it wrote; Better Archery's change made it twist again on top, so the rider snapped back and
// forth every frame. Measured with a probe on 2026-10-24: the spine was rewritten after the riding mod's twist in
// 1,038 of 1,211 frames. While mounted, Better Archery's spine turn is skipped (its nocked arrow still updates);
// on foot nothing changes (Valheim, PotatoTweaks).
using System;
using BepInEx.Bootstrap;
using HarmonyLib;

namespace PotatoTweaks
{
    internal static class MountedBowSpine
    {
        private const string BetterArcheryId = "ishid4.mods.betterarchery";
        private static Action<Player> nockedArrow;

        internal static void Enable(Harmony harmony)
        {
            if (!Chainloader.PluginInfos.ContainsKey(BetterArcheryId)) return;
            var patch = AccessTools.TypeByName("BetterArchery.Player_LateUpdate_Patch");
            var postfix = patch == null ? null : AccessTools.Method(patch, "Postfix");
            var arrow = AccessTools.TypeByName("BetterArchery.NockedArrow");
            var arrowUpdate = arrow == null ? null : AccessTools.Method(arrow, "LateUpdate", new[] { typeof(Player) });
            if (postfix == null || arrowUpdate == null) throw new InvalidOperationException("Better Archery's body turn changed.");
            nockedArrow = AccessTools.MethodDelegate<Action<Player>>(arrowUpdate);
            harmony.Patch(postfix, prefix: new HarmonyMethod(AccessTools.Method(typeof(MountedBowSpine), nameof(SkipWhileMounted))));
        }

        private static bool SkipWhileMounted(Player __0)
        {
            if (!__0 || __0 != Player.m_localPlayer || !__0.IsAttached() || __0.IsAttachedToShip()) return true;
            nockedArrow(__0);
            return false;
        }
    }
}
