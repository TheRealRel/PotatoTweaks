// Adapted from EpicLoot Progression Fix 0.1.281 (boat-camera.cs, dropped-item-lifetime.cs, ship-and-cart-removal.cs,
// transpilers.cs). MIT License, Copyright (c) 2026 EpicLoot Progression Fix contributors. See LICENSE.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace PotatoTweaks
{
    internal static class Transpilers
    {
        // Turns the instructions that match into calls of the replacement, a static method that takes what each of them
        // took from the stack and leaves what it left. Any other number of matches than expected means the game or a mod
        // changed, so the patch fails at startup with the given reason instead of misbehaving.
        internal static List<CodeInstruction> CallInstead(IEnumerable<CodeInstruction> instructions, Func<CodeInstruction, bool> match,
            MethodInfo replacement, int expected, string changed)
        {
            var code = instructions.ToList();
            var matches = code.Where(match).ToList();
            if (matches.Count != expected) throw new InvalidOperationException(changed);
            foreach (var instruction in matches)
            {
                instruction.opcode = OpCodes.Call;
                instruction.operand = replacement;
            }
            return code;
        }
    }

    // Farther camera zoom: 12 m on land by default (game: 8 m), 24 m for anyone on a ship.
    [HarmonyPatch(typeof(GameCamera), "UpdateCamera")]
    internal static class CameraZoom
    {
        private static readonly AccessTools.FieldRef<GameCamera, float> Distance =
            AccessTools.FieldRefAccess<GameCamera, float>("m_distance");

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var landLimit = AccessTools.Field(typeof(GameCamera), nameof(GameCamera.m_maxDistance));
            var boatLimit = AccessTools.Field(typeof(GameCamera), nameof(GameCamera.m_maxDistanceBoat));
            var code = instructions.ToList();
            var land = code.Where(x => x.LoadsField(landLimit)).ToList();
            var boat = code.Where(x => x.LoadsField(boatLimit)).ToList();
            if (land.Count != 1 || boat.Count != 1) throw new InvalidOperationException("Valheim's camera zoom limits changed.");
            land[0].opcode = OpCodes.Call; land[0].operand = AccessTools.Method(typeof(CameraZoom), nameof(LandLimit));
            boat[0].opcode = OpCodes.Call; boat[0].operand = AccessTools.Method(typeof(CameraZoom), nameof(BoatLimit));
            // The game clamps zoom only while it accepts input. Clamp again afterward, so leaving a ship restores the
            // land limit even while an inventory, menu or build selection is open.
            int afterInput = code.FindIndex(x => x.Calls(AccessTools.Method(typeof(Character), nameof(Character.IsDead)))) - 1;
            if (afterInput < 0) throw new InvalidOperationException("Valheim's camera update after zoom input changed.");
            var clamp = new CodeInstruction(OpCodes.Ldarg_0).MoveLabelsFrom(code[afterInput]);
            code.InsertRange(afterInput, new[] { clamp,
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(CameraZoom), nameof(ClampMaximumDistance))) });
            return code;
        }

        private static float LandLimit(GameCamera camera) => Plugin.CameraEnabled.Value ? MaximumDistance(camera) : camera.m_maxDistance;
        private static float BoatLimit(GameCamera camera) => Plugin.CameraEnabled.Value ? MaximumDistance(camera) : camera.m_maxDistanceBoat;

        private static void ClampMaximumDistance(GameCamera camera)
        {
            if (Plugin.CameraEnabled.Value) Distance(camera) = Mathf.Min(Distance(camera), MaximumDistance(camera));
        }

        // The game's boat field stays unchanged because camera tilt also uses it. A ship's onboard volume includes
        // seated and standing passengers, and stays active during a jump on deck.
        private static float MaximumDistance(GameCamera camera)
        {
            var player = Player.m_localPlayer;
            bool onShip = Ship.GetLocalShip() != null || (player != null && player.GetControlledShip() != null);
            return onShip ? Plugin.ShipZoomDistance.Value : camera.m_maxDistance * Plugin.LandZoomMultiplier.Value;
        }
    }

    // The game removes a dropped item once it is an hour of world time old, unless it lies inside a base, a player is
    // within 25 m, or it is in tar or part of a build. Sleeping skips the night's world time at once, so loot dropped
    // shortly before bed vanished. Only the age limit changes; the other rules stay the game's own.
    [HarmonyPatch(typeof(ItemDrop), "TimedDestruction")]
    internal static class DroppedItemLifetime
    {
        internal const double NativeSeconds = 3600.0;

        private static double Seconds() =>
            Plugin.DroppedItemsEnabled.Value ? Plugin.DroppedItemLifetimeHours.Value * 3600.0 : NativeSeconds;

        // Valheim: if (!(GetTimeSinceSpawned() < 3600.0) && !IsInsideBase() && ...) m_nview.Destroy();
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
            Transpilers.CallInstead(instructions, x => x.opcode == OpCodes.Ldc_R8 && Equals(x.operand, NativeSeconds),
                AccessTools.Method(typeof(DroppedItemLifetime), nameof(Seconds)), 1,
                "Valheim's dropped item lifetime changed; expected one 3600 s limit.");
    }

    // The hammer builds rafts, karves, longships, drakkars and carts, but the game marks none of them removable, so the
    // remove button does nothing on them. This lets it take one apart while Ctrl is held, so a plain click still cannot.
    // The game's other removal checks stay, and it refunds every material and drops the cargo in crates as when it breaks.
    [HarmonyPatch(typeof(Player), "RemovePiece")]
    internal static class ShipAndCartRemoval
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
            Transpilers.CallInstead(instructions, x => x.LoadsField(AccessTools.Field(typeof(Piece), nameof(Piece.m_canBeRemoved))),
                AccessTools.Method(typeof(ShipAndCartRemoval), nameof(CanBeRemoved)), 1, "The game's piece removal check changed.");

        private static bool CanBeRemoved(Piece piece)
        {
            if (piece.m_canBeRemoved) return true;
            if (!Plugin.ShipRemovalEnabled.Value) return false;
            // A cart is a wagon with a hold, so the battering ram and catapult stay protected.
            var wagon = piece.GetComponent<Vagon>();
            bool shipOrCart = piece.GetComponent<Ship>() != null || (wagon != null && wagon.m_container != null);
            return shipOrCart && (ZInput.GetKey(KeyCode.LeftControl) || ZInput.GetKey(KeyCode.RightControl));
        }
    }
}
