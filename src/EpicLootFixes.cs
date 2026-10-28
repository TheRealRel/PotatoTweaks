// Potato Tweaks - fixes for Epic Loot enchantments. Only used when Epic Loot is installed.
// Adapted from EpicLoot Progression Fix 0.1.281 (indestructible-durability.cs, near-ground-jumps.cs),
// MIT License, Copyright (c) 2026 EpicLoot Progression Fix contributors. See LICENSE.
// Epic Loot 0.15.0 took in the other fixes from the same mod (Riches, Fire Rate, +Ranged Skills, Noise, armor wear with
// Indestructible, Bountiful Harvest, Quick Draw), and Epic Loot 0.15.4 its Meads Cooldown fix, so those are no longer here.
using System;
using System.Reflection;
using EpicLoot;
using EpicLoot.MagicItemEffects;
using HarmonyLib;
using UnityEngine;

namespace PotatoTweaks
{
    internal static class EpicLootFixes
    {
        internal static void Enable(Action<Type> patch)
        {
            foreach (var type in new[] { typeof(BrokenIndestructibleItems), typeof(RepairedIndestructibleItems), typeof(NearGroundJumps) }) patch(type);
        }
    }

    // ------------------------------------------------------------------ Indestructible
    // Indestructible switched off durability altogether, which also skipped the game's broken-item and repair checks for
    // gear that was already broken.
    [HarmonyPatch(typeof(Indestructible), nameof(Indestructible.Sync))]
    internal static class BrokenIndestructibleItems
    {
        // Keep the broken-item and repair checks active until an actual repair restores positive durability.
        private static void Postfix(ItemDrop.ItemData item)
        {
            if (item != null && item.m_durability <= 0f && Indestructible.OriginallyUsesDurability(item))
                Indestructible.Revert(item);
        }
    }

    [HarmonyPatch(typeof(InventoryGui), "RepairOneItem")]
    internal static class RepairedIndestructibleItems
    {
        private static void Postfix()
        {
            var player = Player.m_localPlayer;
            if (player == null) return;
            foreach (var item in player.GetInventory().GetAllItems())
                if (item.HasMagicEffect(MagicEffectType.Indestructible)) Indestructible.Sync(item);
        }
    }

    // ------------------------------------------------------------------ Air Jump near the ground
    // Epic Loot spent the Air Jump on any press the game counts as airborne: 0.2 s after leaving the ground, as over a crest
    // or bump, or on the physics step that touches down before the landing registers. Such a press is a ground jump
    // instead, so the Air Jump stays for the second press.
    [HarmonyPatch(typeof(Character), nameof(Character.Jump))]
    internal static class NearGroundJumps
    {
        private const float GroundRange = 1f;
        private static readonly AccessTools.FieldRef<Character, float> SinceGround = AccessTools.FieldRefAccess<Character, float>("m_lastGroundTouch");
        private static readonly AccessTools.FieldRef<Character, float> SinceJump = AccessTools.FieldRefAccess<Character, float>("m_jumpTimer");
        private static readonly AccessTools.FieldRef<Character, bool> Touching = AccessTools.FieldRefAccess<Character, bool>("m_groundContact");
        private static readonly AccessTools.FieldRef<Character, CapsuleCollider> Body = AccessTools.FieldRefAccess<Character, CapsuleCollider>("m_collider");
        private static readonly FieldInfo GroundMask = AccessTools.Field(typeof(Character), "s_groundRayMask");

        // Runs before Epic Loot's prefix, which reads IsOnGround to choose between the ground and the air jump.
        [HarmonyPriority(Priority.First)]
        private static void Prefix(Character __instance, out float __state)
        {
            __state = -1f;
            if (__instance != Player.m_localPlayer || __instance.IsOnGround() || GrapplingPoint.m_localGrappler != null) return;
            bool nearGround = Touching(__instance)
                || !__instance.IsSwimming() && SinceJump(__instance) > SinceGround(__instance) && GroundBelow(__instance);
            if (!nearGround || !Player.m_localPlayer.HasActiveMagicEffect(MagicEffectType.DoubleJump)) return;
            __state = SinceGround(__instance);
            SinceGround(__instance) = 0f;
        }

        // ForceJump sets the time since ground contact to 1 s; a press that launched nothing keeps its airborne time.
        private static void Postfix(Character __instance, float __state)
        {
            if (__state >= 0f && SinceGround(__instance) == 0f) SinceGround(__instance) = __state;
        }

        private static bool GroundBelow(Character character)
        {
            float radius = Body(character).radius * 0.9f;
            var from = character.transform.position + Vector3.up * (radius + 0.1f);
            return Physics.SphereCast(from, radius, Vector3.down, out _, GroundRange + 0.1f, (int)GroundMask.GetValue(null), QueryTriggerInteraction.Ignore);
        }
    }
}