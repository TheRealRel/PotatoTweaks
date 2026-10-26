// Potato Tweaks - fixes for bugs in Valheim itself.
// Adapted from EpicLoot Progression Fix 0.1.281 (ship-deck-melee.cs, shaman-shield.cs, plant-growth-checks.cs,
// ore-support.cs, pickable-regrowth.cs), MIT License,
// Copyright (c) 2026 EpicLoot Progression Fix contributors. See LICENSE.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

namespace PotatoTweaks
{
    internal static class GameFixes
    {
        internal static readonly Type[] Patches =
        {
            typeof(ShipDeckMelee), typeof(ShamanShield),
            typeof(PlantGrowthChecks), typeof(FirstPlantGrowthCheck), typeof(OreSupport), typeof(PickableRegrowth),
        };
    }

    // ------------------------------------------------------------------ fighting on a ship's deck
    // Most one-handed swings also sweep for short creatures from just above the attacker's feet. On a ship that sweep starts
    // inside the deck, so the deck is reported at distance 0, sorts before every real target and ends the swing: a log or
    // creature on deck could not be hit and every swing damaged the ship instead. Drop only those start overlaps with the
    // ship the attacker stands on; its mast, sides and posts, met along a swing, still stop the swing.
    [HarmonyPatch(typeof(Attack), "DoMeleeAttack")]
    internal static class ShipDeckMelee
    {
        private static readonly AccessTools.FieldRef<Attack, Humanoid> Attacker = AccessTools.FieldRefAccess<Attack, Humanoid>("m_character");

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.ToList();
            var sort = AccessTools.Method(typeof(List<RaycastHit>), nameof(List<RaycastHit>.Sort), new[] { typeof(Comparison<RaycastHit>) });
            int index = code.FindIndex(instruction => instruction.Calls(sort));
            if (index < 0 || code.FindLastIndex(instruction => instruction.Calls(sort)) != index)
                throw new InvalidOperationException("Valheim's melee hit sorting changed.");
            code.InsertRange(index + 1, new[]
            {
                new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(Attack), "s_hitList")),
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ShipDeckMelee), nameof(SkipDeckUnderfoot))),
            });
            return code;
        }

        private static void SkipDeckUnderfoot(List<RaycastHit> hits, Attack attack)
        {
            var attacker = Attacker(attack);
            var ship = attacker ? attacker.GetStandingOnShip() : null;
            if (!ship) return;
            for (int index = hits.Count - 1; index >= 0; index--)
            {
                var hit = hits[index];
                if (hit.distance == 0f && hit.point == Vector3.zero && Projectile.FindHitObject(hit.collider) == ship.gameObject)
                    hits.RemoveAt(index);
            }
        }
    }

    // ------------------------------------------------------------------ fuling shaman shield
    // A shaman's shield absorbs every hit until the next status update removes it once broken, so arrows landing together
    // stopped on a shield the first had already broken. Let hits through a shaman shield that is already broken. The hit
    // that breaks it is still absorbed, and the game still removes the shield with its break effect. Player bubbles unchanged.
    [HarmonyPatch(typeof(SE_Shield), nameof(SE_Shield.OnDamaged))]
    internal static class ShamanShield
    {
        private static readonly int Shield = "GoblinShaman_shield".GetStableHashCode();

        private static bool Prefix(SE_Shield __instance, float ___m_damage, float ___m_totalAbsorbDamage) =>
            ___m_damage <= ___m_totalAbsorbDamage || __instance.NameHash() != Shield;
    }

    // ------------------------------------------------------------------ plant growth
    // Valheim plans a plant's next growth check 10 seconds after each check, but checked the plant only until that time
    // instead of from it. A gap over 10 seconds between two passes, such as a frame-rate drop with many objects loaded,
    // ended the plant's checks for good: it never grew until its area reloaded. Each plant is now checked once its time
    // comes, every 10 seconds, however late the loop reaches it.
    [HarmonyPatch(typeof(Plant), nameof(Plant.SUpdate))]
    internal static class PlantGrowthChecks
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.ToList();
            var nextCheck = AccessTools.Field(typeof(Plant), "m_updateTime");
            int index = code.FindIndex(x => x.LoadsField(nextCheck));
            // if (time > m_updateTime) return;   becomes   if (time < m_updateTime) return;
            if (index < 2 || code.FindLastIndex(x => x.LoadsField(nextCheck)) != index || index + 1 >= code.Count ||
                !code[index - 2].IsLdarg(1) || !code[index - 1].IsLdarg(0) ||
                (code[index + 1].opcode != OpCodes.Ble_Un && code[index + 1].opcode != OpCodes.Ble_Un_S))
                throw new InvalidOperationException("Valheim's plant growth checks changed; expected one check time comparison.");
            code[index + 1].opcode = code[index + 1].opcode == OpCodes.Ble_Un ? OpCodes.Bge_Un : OpCodes.Bge_Un_S;
            return code;
        }
    }

    // A newly loaded plant is checked on the first pass, so it shows its current look and health right away. Growing still
    // waits 10 seconds after loading, as in the game.
    [HarmonyPatch(typeof(Plant), "Start")]
    internal static class FirstPlantGrowthCheck
    {
        private static readonly AccessTools.FieldRef<Plant, float> NextCheck = AccessTools.FieldRefAccess<Plant, float>("m_updateTime");

        private static void Postfix(Plant __instance) => NextCheck(__instance) = 0f;
    }

    // ------------------------------------------------------------------ ore veins
    // A long buried vein can need more than the game's three passes to reach its terrain-supported tips, so parts of it
    // fell or broke off while still buried. Keep every contact rule, but continue until a pass adds no supported sections.
    [HarmonyPatch(typeof(MineRock5), "UpdateSupport")]
    internal static class OreSupport
    {
        private static readonly FieldInfo Areas = AccessTools.Field(typeof(MineRock5), "m_hitAreas");
        private static readonly FieldInfo Supported = AccessTools.Field(AccessTools.Inner(typeof(MineRock5), "HitArea"), "m_supported");

        private static bool Continue(MineRock5 rock, ref int previous)
        {
            int count = 0;
            foreach (var area in (IEnumerable)Areas.GetValue(rock))
                if ((bool)Supported.GetValue(area)) count++;
            bool changed = count != previous;
            previous = count;
            return changed;
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var code = instructions.ToList();
            int limit = code.FindIndex(instruction => instruction.LoadsConstant(3));
            if (limit < 1 || code.FindLastIndex(instruction => instruction.LoadsConstant(3)) != limit ||
                !code[limit - 1].IsLdloc() || (code[limit + 1].opcode != OpCodes.Blt && code[limit + 1].opcode != OpCodes.Blt_S))
                throw new InvalidOperationException("Valheim's ore support loop changed; expected the three-pass limit.");
            var previous = generator.DeclareLocal(typeof(int));
            code[limit - 1].opcode = OpCodes.Ldarg_0;
            code[limit - 1].operand = null;
            code[limit].opcode = OpCodes.Ldloca;
            code[limit].operand = previous;
            code[limit + 1].opcode = OpCodes.Brtrue;
            code.Insert(limit + 1, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(OreSupport), nameof(Continue))));
            code.InsertRange(0, new[] { new CodeInstruction(OpCodes.Ldc_I4_M1), new CodeInstruction(OpCodes.Stloc, previous) });
            return code;
        }
    }

    // ------------------------------------------------------------------ berry bushes and other pickables
    // A pickable checks regrowth only once a minute, and its check on load returns while nobody owns it yet, so an overdue
    // bush stayed empty until someone stood next to it long enough. Run the game's own check on load, when this game takes
    // the bush over, and when an overdue bush is hovered or harvested. The owner still decides and shares the result.
    [HarmonyPatch]
    internal static class PickableRegrowth
    {
        private const string CheckRpc = "PotatoTweaks_CheckRegrowth";
        private static readonly Action<Pickable> UpdateRespawn = AccessTools.MethodDelegate<Action<Pickable>>(
            AccessTools.Method(typeof(Pickable), "UpdateRespawn"));
        private static readonly List<ZDO> Acquired = new List<ZDO>();
        private sealed class RegrowthRequest { internal float NextRequest; }
        private static readonly ConditionalWeakTable<Pickable, RegrowthRequest> Requests = new ConditionalWeakTable<Pickable, RegrowthRequest>();

        [HarmonyPostfix, HarmonyPatch(typeof(Pickable), "Awake")]
        private static void OnLoad(Pickable __instance, ZNetView ___m_nview)
        {
            if (__instance.m_respawnTimeMinutes <= 0f || ___m_nview == null || !___m_nview.IsValid()) return;
            ___m_nview.Register(CheckRpc, (long sender) => UpdateRespawn(__instance));
            UpdateRespawn(__instance);
        }

        [HarmonyPrefix, HarmonyPatch(typeof(ZDO), nameof(ZDO.SetOwnerInternal))]
        private static void OnOwnerChange(ZDO __instance, long uid)
        {
            if (uid != 0L && uid == ZDOMan.GetSessionID() && !__instance.IsOwner()) Acquired.Add(__instance);
        }

        [HarmonyPostfix, HarmonyPatch(typeof(ZDOMan), "RPC_ZDOData")]
        private static void OnDataApplied()
        {
            if (Acquired.Count == 0) return;
            foreach (var data in Acquired)
            {
                if (!data.IsOwner() || !(ZNetScene.instance?.FindInstance(data) is ZNetView instance) || instance == null) continue;
                var pickable = instance.GetComponent<Pickable>();
                if (pickable != null && pickable.m_respawnTimeMinutes > 0f && pickable.GetPicked()) UpdateRespawn(pickable);
            }
            Acquired.Clear();
        }

        // A bush loaded before anyone picked it has no harvest time of its own, so its first check as owner replaced the saved
        // harvest time and restarted the whole regrowth timer. Start from the saved time instead.
        [HarmonyPrefix, HarmonyPatch(typeof(Pickable), "UpdateRespawn")]
        private static void KeepHarvestTime(ZNetView ___m_nview, ref long ___m_pickedTime)
        {
            if (___m_pickedTime == 0L && ___m_nview != null && ___m_nview.IsValid() && ___m_nview.IsOwner())
                ___m_pickedTime = ___m_nview.GetZDO().GetLong(ZDOVars.s_pickedTime, 0L);
        }

        [HarmonyPrefix, HarmonyPatch(typeof(Pickable), nameof(Pickable.GetHoverText))]
        private static void OnHover(Pickable __instance, ZNetView ___m_nview, bool ___m_picked) => CheckOnUse(__instance, ___m_nview, ___m_picked);

        [HarmonyPrefix, HarmonyPatch(typeof(Pickable), nameof(Pickable.Interact))]
        private static void OnInteract(Pickable __instance, ZNetView ___m_nview, bool ___m_picked) => CheckOnUse(__instance, ___m_nview, ___m_picked);

        private static void CheckOnUse(Pickable pickable, ZNetView view, bool picked)
        {
            if (!picked || pickable.m_respawnTimeMinutes <= 0f || pickable.GetEnabled == 0 || view == null || !view.IsValid()) return;
            long pickedTime = view.GetZDO().GetLong(ZDOVars.s_pickedTime, 0L);
            if (pickedTime > 1 && (ZNet.instance.GetTime() - new DateTime(pickedTime)).TotalMinutes <= pickable.m_respawnTimeMinutes) return;
            if (view.IsOwner())
            {
                UpdateRespawn(pickable);
                return;
            }
            var request = Requests.GetValue(pickable, _ => new RegrowthRequest());
            if (Time.time < request.NextRequest) return;
            request.NextRequest = Time.time + 1f;
            view.InvokeRPC(CheckRpc);
        }
    }
}
