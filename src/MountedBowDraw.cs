// Better Archery's crouch bow draw swaps in its own set of character animations while you draw. A riding mod (the
// Odin Horse) swaps its riding animations back in every frame, so on a mount the two took turns every frame and the
// rider flickered and clipped through the horse at the draw (Valheim, PotatoTweaks). While you're mounted, the
// crouch draw sits out and the riding mod's seated bow animations play; on foot it works as before.
using System;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

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

        // While you draw a bow on a mount, the camera can only turn within the rider's aiming arc ahead of the mount
        // (the Odin Horse's Bow Turn Left/Right settings, 45 degrees each way if it isn't there). Aiming behind you
        // from the saddle isn't possible, and the camera no longer looks where the rider can't turn, which made the
        // rider's twist flip back and forth at the limit. Turning the mount turns the arc with it.
        [HarmonyPatch(typeof(Player), nameof(Player.SetMouseLook))]
        internal static class AimArc
        {
            private static readonly AccessTools.FieldRef<Character, Quaternion> LookYaw = AccessTools.FieldRefAccess<Character, Quaternion>("m_lookYaw");
            private static readonly AccessTools.FieldRef<Character, Vector3> LookDir = AccessTools.FieldRefAccess<Character, Vector3>("m_lookDir");
            private static readonly Action<Player> LevelEye = AccessTools.MethodDelegate<Action<Player>>(AccessTools.Method(typeof(Player), "UpdateEyeRotation"));
            private static ConfigEntry<float> left, right;
            private static bool looked;

            private static void Limits(out float l, out float r)
            {
                if (!looked)
                {
                    looked = true;
                    if (Chainloader.PluginInfos.TryGetValue("Raelaziel.OdinHorse", out var horse) && horse.Instance)
                    {
                        horse.Instance.Config.TryGetEntry("Mounted Combat", "Bow Turn Left", out left);
                        horse.Instance.Config.TryGetEntry("Mounted Combat", "Bow Turn Right", out right);
                    }
                }
                l = left != null ? left.Value : 45f;
                r = right != null ? right.Value : 45f;
            }

            private static void Postfix(Player __instance)
            {
                if (__instance != Player.m_localPlayer || SitOut(__instance) || !__instance.IsDrawingBow()) return;
                Limits(out float l, out float r);
                var basis = __instance.GetAttachPoint() ? __instance.GetAttachPoint() : __instance.transform;
                Vector3 ahead = Vector3.ProjectOnPlane(basis.forward, Vector3.up);
                if (ahead.sqrMagnitude < 0.0001f) return;
                float aheadYaw = Quaternion.LookRotation(ahead).eulerAngles.y;
                float delta = Mathf.DeltaAngle(aheadYaw, LookYaw(__instance).eulerAngles.y);
                float clamped = Mathf.Clamp(delta, -l, r);
                if (Mathf.Approximately(clamped, delta)) return;
                LookYaw(__instance) = Quaternion.Euler(0f, aheadYaw + clamped, 0f);
                LevelEye(__instance);
                LookDir(__instance) = __instance.m_eye.forward;
            }
        }
    }
}
