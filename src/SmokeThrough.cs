// Smoke passes through open structures (Valheim, PotatoTweaks): cage floors and walls, iron gates, the darkwood
// divider, stairs and ladders. The smoke drifts up through them instead of pooling under them, and a fire under one
// no longer counts as covered and smothered. From Smoke Collision by JereKuusela (public domain), without its smoke
// size and speed settings.
using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace PotatoTweaks
{
    internal static class SmokeThrough
    {
        internal const string OriginalModId = "smoke_collision";
        internal static readonly Type[] Patches = { typeof(SmokeSpawned), typeof(FireCover), typeof(SpawnerCover) };
        private static HashSet<string> ids = new HashSet<string>();
        private static string parsed;

        private static HashSet<string> Ids()
        {
            string value = Plugin.SmokeThroughPieces.Value ?? "";
            if (value != parsed)
            {
                parsed = value;
                ids = new HashSet<string>(value.Split(',').Select(s => s.Trim().ToLowerInvariant()).Where(s => s.Length > 0));
            }
            return ids;
        }

        internal static bool Ignored(Collider collider) =>
            collider && Ids().Count > 0 && Ids().Contains(Utils.GetPrefabName(collider.transform.root.gameObject).ToLowerInvariant());

        private sealed class PassThrough : MonoBehaviour
        {
            private Collider own;
            private void Awake() => own = GetComponent<Collider>();

            private void OnCollisionEnter(Collision collision)
            {
                if (own && Ignored(collision.collider)) Physics.IgnoreCollision(own, collision.collider, true);
            }
        }

        [HarmonyPatch(typeof(Smoke), "Awake")]
        private static class SmokeSpawned
        {
            private static void Postfix(Smoke __instance) => __instance.gameObject.AddComponent<PassThrough>();
        }

        // The fire's "is something right above me" check skips these pieces.
        [HarmonyPatch(typeof(Fireplace), "CheckUnderTerrain")]
        private static class FireCover
        {
            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
                Transpilers.CallInstead(instructions,
                    x => x.Calls(AccessTools.Method(typeof(Physics), nameof(Physics.Raycast),
                        new[] { typeof(Vector3), typeof(Vector3), typeof(RaycastHit).MakeByRefType(), typeof(float), typeof(int) })),
                    AccessTools.Method(typeof(FireCover), nameof(Raycast)), 1, "Valheim's fireplace cover check changed.");

            private static bool Raycast(Vector3 origin, Vector3 direction, out RaycastHit hit, float distance, int mask)
            {
                hit = default;
                foreach (var h in Physics.RaycastAll(origin, direction, distance, mask))
                    if (!Ignored(h.collider)) { hit = h; return true; }
                return false;
            }
        }

        // The smoke spawner's "is there room for smoke" check skips them too (a fire that can't make smoke goes out).
        [HarmonyPatch(typeof(SmokeSpawner), "TestBlocked")]
        private static class SpawnerCover
        {
            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
                Transpilers.CallInstead(instructions,
                    x => x.Calls(AccessTools.Method(typeof(Physics), nameof(Physics.CheckSphere), new[] { typeof(Vector3), typeof(float), typeof(int) })),
                    AccessTools.Method(typeof(SpawnerCover), nameof(CheckSphere)), 1, "Valheim's smoke spawner check changed.");

            private static bool CheckSphere(Vector3 position, float radius, int mask)
            {
                foreach (var c in Physics.OverlapSphere(position, radius, mask))
                    if (!Ignored(c)) return true;
                return false;
            }
        }
    }
}
