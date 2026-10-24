// Better Archery's crouch bow draw swaps in its own set of character animations while you draw. A riding mod (the
// Odin Horse) swaps its riding animations back in every frame, so on a mount the two took turns every frame and the
// rider flickered and clipped through the horse at the draw (Valheim, PotatoTweaks). While you're mounted, the
// crouch draw sits out and the riding mod's seated bow animations play; on foot it works as before.
using System;
using BepInEx.Bootstrap;
using HarmonyLib;

namespace PotatoTweaks
{
    internal static class MountedBowDraw
    {
        internal const string BetterArcheryId = "ishid4.mods.betterarchery";

        internal static void Enable(Harmony harmony)
        {
            if (!Chainloader.PluginInfos.ContainsKey(BetterArcheryId)) return;
            var type = AccessTools.TypeByName("BetterArchery.CrouchBowDrawOverlay");
            var update = type == null ? null : AccessTools.Method(type, "Update", new[] { typeof(Player) });
            if (update == null) throw new InvalidOperationException("Better Archery's crouch bow draw changed.");
            harmony.Patch(update, prefix: new HarmonyMethod(AccessTools.Method(typeof(MountedBowDraw), nameof(SitOut))));
        }

        private static bool SitOut(Player player) => !(player && player.IsAttached() && !player.IsAttachedToShip());
    }
}
