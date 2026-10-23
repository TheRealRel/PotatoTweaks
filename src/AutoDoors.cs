// Potato Tweaks - doors that close themselves, written from scratch.
using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace PotatoTweaks
{
    // A door you open shuts on its own a few seconds later. It waits while anyone, player or creature, stands in the
    // doorway, and a door someone already closed is left alone. Only doors players built are touched;
    // doors that need a key, and doors that can't be closed by hand, are left alone. The game of the player who opened
    // the door does the closing, through a request that only ever closes, so it can't swing a door open by mistake.
    internal static class AutoDoors
    {
        private const string CloseRpc = "PotatoTweaks_CloseDoor";
        internal static readonly Type[] Patches = { typeof(Register), typeof(Opened) };

        private static readonly AccessTools.FieldRef<Door, ZNetView> View = AccessTools.FieldRefAccess<Door, ZNetView>("m_nview");
        private static readonly Func<Door, bool> CanInteract =
            AccessTools.MethodDelegate<Func<Door, bool>>(AccessTools.Method(typeof(Door), "CanInteract"));

        private sealed class Watched
        {
            internal Door door;
            internal float openedAt;
            internal Bounds doorway;
        }

        private static readonly List<Watched> watched = new List<Watched>();
        private static readonly List<Character> nearby = new List<Character>();
        private static float nextCheck;

        private static bool Handles(Door door)
        {
            if (!door || door.m_keyItem != null || door.m_canNotBeClosed) return false;
            var piece = door.GetComponent<Piece>();
            return piece && piece.IsPlacedByPlayer();
        }

        private static int State(Door door)
        {
            var view = View(door);
            return view && view.IsValid() ? view.GetZDO().GetInt(ZDOVars.s_state) : 0;
        }

        [HarmonyPatch(typeof(Door), "Awake")]
        internal static class Register
        {
            private static void Postfix(Door __instance)
            {
                var view = View(__instance);
                if (!view || view.GetZDO() == null) return;
                var door = __instance;
                view.Register(CloseRpc, sender =>
                {
                    if (view.IsValid() && view.IsOwner() && view.GetZDO().GetInt(ZDOVars.s_state) != 0 && CanInteract(door))
                        view.GetZDO().Set(ZDOVars.s_state, 0);
                });
            }
        }

        // A closed door the local player uses is about to open: remember it and where its doorway is.
        [HarmonyPatch(typeof(Door), nameof(Door.Interact))]
        internal static class Opened
        {
            private static void Prefix(Door __instance, Humanoid character, bool hold, ref bool __state)
            {
                __state = !hold && character == Player.m_localPlayer && Plugin.DoorsCloseEnabled.Value && Handles(__instance) && State(__instance) == 0;
                if (!__state) return;
                watched.RemoveAll(w => w.door == __instance);
                watched.Add(new Watched { door = __instance, openedAt = Time.time, doorway = Doorway(__instance) });
            }
        }

        // The closed door's shape, widened by a metre on every side, is the doorway.
        private static Bounds Doorway(Door door)
        {
            var bounds = new Bounds(door.transform.position, Vector3.zero);
            bool any = false;
            foreach (var collider in door.GetComponentsInChildren<Collider>())
            {
                if (collider.isTrigger) continue;
                if (any) bounds.Encapsulate(collider.bounds);
                else { bounds = collider.bounds; any = true; }
            }
            bounds.Expand(new Vector3(2f, 1f, 2f));
            return bounds;
        }

        // Called from Plugin.Update.
        internal static void Update()
        {
            if (watched.Count == 0 || Time.time < nextCheck) return;
            nextCheck = Time.time + 0.25f;
            float wait = Plugin.DoorsCloseSeconds.Value;

            for (int i = watched.Count - 1; i >= 0; i--)
            {
                var w = watched[i];
                if (!w.door || !Plugin.DoorsCloseEnabled.Value)
                {
                    watched.RemoveAt(i);
                    continue;
                }
                int state = State(w.door);
                // Not open yet (the request is on its way), or closed by someone: give it a moment, then let it go.
                if (state == 0)
                {
                    if (Time.time - w.openedAt > 3f) watched.RemoveAt(i);
                    continue;
                }
                if (Time.time - w.openedAt < wait) continue;
                if (SomeoneInDoorway(w.doorway)) continue;
                var view = View(w.door);
                if (view && view.IsValid()) view.InvokeRPC(CloseRpc);
                watched.RemoveAt(i);
            }
        }

        private static bool SomeoneInDoorway(Bounds doorway)
        {
            nearby.Clear();
            Character.GetCharactersInRange(doorway.center, doorway.extents.magnitude, nearby);
            foreach (var character in nearby)
                if (character && doorway.Contains(character.transform.position)) return true;
            return false;
        }
    }
}
