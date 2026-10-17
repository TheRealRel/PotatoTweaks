// Potato Tweaks - skip the night when enough players want to sleep, not only when everyone is in bed.
// Written from scratch for this mod.
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace PotatoTweaks
{
    internal enum SleepVotes { AnyEmote, SittingOnly, BedsOnly }

    // The game skips the night only when every player is in bed. This also skips it when a share of the players are in
    // bed or, as set, emoting (any emote, or sitting) while standing still. Runs on the server, so players need nothing.
    // Only the players who voted sleep through it; everyone else keeps playing, as the night passes around them.
    internal static class SleepVote
    {
        internal static readonly System.Type[] Patches = { typeof(Count), typeof(OnlyVoters) };

        // Server devcommands' ghost mode can mark an admin as not counting for sleep.
        private static readonly int IgnoreSleep = "DEV_GhostIgnoreSleep".GetHashCode();
        private const float MessageEvery = 30f;

        private static readonly HashSet<ZDOID> Voters = new HashSet<ZDOID>();
        private static readonly Dictionary<ZDOID, Emote> Emotes = new Dictionary<ZDOID, Emote>();
        private const float StillWithin = 1.5f;

        // Where a player was when they used their latest emote, and whether they used it while the vote was watching.
        private struct Emote
        {
            internal int Id;
            internal Vector3 Where;
            internal bool Fresh;
        }
        private static bool voted;
        private static string lastMessage;
        private static float nextMessage, lastTally = float.NegativeInfinity;

        // A postfix, so the game's own rule (everyone in bed) and other mods' rules still skip the night as before;
        // this only adds the vote.
        [HarmonyPatch(typeof(Game), "EverybodyIsTryingToSleep")]
        internal static class Count
        {
            private static void Postfix(ref bool __result)
            {
                voted = false;
                if (__result || !ZNet.instance) return;
                var mode = Plugin.SleepVoteMode.Value;
                int needed = Tally(ZNet.instance.GetAllCharacterZDOS(), mode, Plugin.SleepShare.Value, Plugin.SleepInBed.Value, Time.unscaledTime, out int total);
                if (needed <= 0) lastMessage = null;
                else if (Voters.Count >= needed)
                {
                    __result = voted = true;
                    lastMessage = null;
                }
                else Announce(Voters.Count, total, needed, mode);
            }
        }

        // Fills Voters and returns how many votes are needed, or 0 when nobody (or too few) is in bed yet.
        internal static int Tally(IEnumerable<ZDO> players, SleepVotes mode, int share, int minInBed, float now, out int total)
        {
            // The game asks every frame from the afternoon on. After a gap (daytime, or the night just skipped), emotes
            // already on players are from before and don't count.
            if (now - lastTally > 5f)
            {
                Watched.Clear();
                Emotes.Clear();
            }
            lastTally = now;
            total = 0;
            int inBed = 0;
            Voters.Clear();
            foreach (var zdo in players)
            {
                if (zdo == null || zdo.GetBool(IgnoreSleep)) continue;
                total++;
                bool emoting = Emoting(zdo, mode);
                if (zdo.GetBool(ZDOVars.s_inBed))
                {
                    inBed++;
                    Voters.Add(zdo.m_uid);
                }
                else if (emoting) Voters.Add(zdo.m_uid);
            }
            if (total == 0 || inBed < Mathf.Max(1, minInBed)) return 0;
            return Mathf.Max(1, Mathf.CeilToInt(total * Mathf.Clamp(share, 1, 100) / 100f));
        }

        // Sitting is held as a pose, and the game ends it as soon as the player moves. Any other emote plays once, and the
        // game keeps its name on the player afterward, even while they run around, until their next emote. So those count
        // only when started while the vote is watching (afternoon or night), and only while the player stays where they
        // used it.
        private static bool Emoting(ZDO zdo, SleepVotes mode)
        {
            bool known = !Watched.Add(zdo.m_uid);
            string emote = mode == SleepVotes.BedsOnly ? null : zdo.GetString(ZDOVars.s_emote);
            if (string.IsNullOrEmpty(emote))
            {
                Emotes.Remove(zdo.m_uid);
                return false;
            }
            if (emote == "sit") return true;
            if (mode == SleepVotes.SittingOnly) return false;
            int id = zdo.GetInt(ZDOVars.s_emoteID);
            Vector3 position = zdo.GetPosition();
            if (!Emotes.TryGetValue(zdo.m_uid, out var seen) || seen.Id != id)
            {
                // A new emote from a player the vote was already watching was just used; one already on a player the
                // vote sees for the first time is from before.
                seen = new Emote { Id = id, Where = position, Fresh = known };
                Emotes[zdo.m_uid] = seen;
            }
            float dx = position.x - seen.Where.x, dz = position.z - seen.Where.z;
            return seen.Fresh && dx * dx + dz * dz <= StillWithin * StillWithin;
        }

        // Players the vote has looked at since it started watching this afternoon (cleared after each gap, so it stays small).
        private static readonly HashSet<ZDOID> Watched = new HashSet<ZDOID>();

        // Shown to everyone when the count changes, and every 30 seconds while it stays the same.
        private static void Announce(int voters, int total, int needed, SleepVotes mode)
        {
            string join = mode == SleepVotes.AnyEmote ? "Lie in a bed, or use any emote and stay put, to join them."
                : mode == SleepVotes.SittingOnly ? "Lie in a bed or sit down to join them."
                : "Lie in a bed to join them.";
            string text = $"{voters} of {total} players want to sleep ({needed} needed). {join}";
            if (text == lastMessage && Time.unscaledTime < nextMessage) return;
            lastMessage = text;
            nextMessage = Time.unscaledTime + MessageEvery;
            ZRoutedRpc.instance?.InvokeRoutedRPC(ZNetView.Everybody, "ShowMessage", (int)MessageHud.MessageType.Center, text);
        }

        // When the vote skipped the night, the "fall asleep" signal goes only to the players who voted. The others keep
        // control instead of fading out mid-fight. When everyone was in bed it goes to everyone, as in the game.
        [HarmonyPatch(typeof(Game), "UpdateSleeping")]
        internal static class OnlyVoters
        {
            private static readonly MethodInfo Invoke = AccessTools.Method(typeof(ZRoutedRpc), nameof(ZRoutedRpc.InvokeRoutedRPC),
                new[] { typeof(long), typeof(string), typeof(object[]) });

            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var code = instructions.ToList();
                var matches = new List<CodeInstruction>();
                for (int i = 0; i < code.Count; i++)
                {
                    if (!code[i].Calls(Invoke)) continue;
                    for (int back = 1; back <= 4 && i - back >= 0; back++)
                        if (code[i - back].opcode == System.Reflection.Emit.OpCodes.Ldstr && (string)code[i - back].operand == "SleepStart")
                        {
                            matches.Add(code[i]);
                            break;
                        }
                }
                if (matches.Count != 1) throw new System.InvalidOperationException("Valheim's sleep start changed.");
                matches[0].opcode = System.Reflection.Emit.OpCodes.Call;
                matches[0].operand = AccessTools.Method(typeof(OnlyVoters), nameof(SleepStart));
                return code;
            }

            private static void SleepStart(ZRoutedRpc rpc, long target, string method, object[] parameters)
            {
                if (!voted || ZNet.instance == null)
                {
                    rpc.InvokeRoutedRPC(target, method, parameters);
                    return;
                }
                voted = false;
                if (Voters.Contains(ZNet.instance.LocalPlayerCharacterID)) rpc.InvokeRoutedRPC(ZDOMan.GetSessionID(), method, parameters);
                foreach (var peer in ZNet.instance.GetPeers())
                    if (peer != null && peer.IsReady() && Voters.Contains(peer.m_characterID)) rpc.InvokeRoutedRPC(peer.m_uid, method, parameters);
            }
        }
    }
}
