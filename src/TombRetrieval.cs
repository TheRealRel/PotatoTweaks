// Potato Tweaks - tombstone retrieval, written from scratch.
using System;
using System.Collections.Generic;
using HarmonyLib;
using Jotunn.Entities;
using UnityEngine;

namespace PotatoTweaks
{
    // Console:  retrievetomb   (or gettomb)      Chat:  /retrievetomb
    // The player's game asks the server; the server moves that player's own tombstones next to them.
    //
    // Why the old Tomb Retriever's tombstones "vanished": Valheim saves where each tombstone first landed (ZDO
    // "SpawnPoint"), and TombStone.PositionCheck snaps a tombstone back there whenever it is more than 4 m away.
    // Moving only the position therefore sent it straight back to the death site as soon as a player's game took it
    // over. This version moves the saved spawn point together with the tombstone, so it stays where it was put.
    internal static class TombRetrieval
    {
        private const string RequestRpc = "PotatoTweaks_RetrieveTomb";
        private const string ReplyRpc = "PotatoTweaks_RetrieveTombReply";
        private const string TombstonePrefab = "Player_tombstone";
        private const float AlreadyHere = 3f;      // meters; closer than this counts as already retrieved
        private const float RingRadius = 1.5f;     // several tombstones are set in a small ring around the player
        private const float ReplyTimeout = 8f;

        private static readonly Dictionary<long, DateTime> LastUse = new Dictionary<long, DateTime>();
        private static float waitingSince = -1f;

        internal static bool Active;

        internal sealed class Command : ConsoleCommand
        {
            private readonly string name;
            public Command(string name) { this.name = name; }
            public override string Name => name;
            public override string Help => "Brings your tombstone(s) to where you are standing.";
            public override void Run(string[] args) => Request();
        }

        // ZRoutedRpc is created anew for every world session, so the handlers are registered as each game starts.
        [HarmonyPatch(typeof(Game), "Start")]
        internal static class RegisterRpcs
        {
            private static void Postfix()
            {
                if (!Active || ZRoutedRpc.instance == null) return;
                ZRoutedRpc.instance.Register(RequestRpc, new Action<long>(OnRequest));
                ZRoutedRpc.instance.Register(ReplyRpc, new Action<long, ZPackage>(OnReply));
            }
        }

        internal static void Update()
        {
            if (waitingSince >= 0f && Time.realtimeSinceStartup - waitingSince > ReplyTimeout)
            {
                waitingSince = -1f;
                Say("No answer from the server. Potato Tweaks has to be installed on the server too.");
            }
        }

        // ---------------------------------------------------------------- player's game
        private static void Request()
        {
            var player = Player.m_localPlayer;
            if (player == null || player.IsDead() || ZRoutedRpc.instance == null || ZNet.instance == null)
            {
                Say("You need to be in a world, alive, to retrieve your tombstone.");
                return;
            }
            if (waitingSince >= 0f) { Say("Still waiting for the server..."); return; }
            waitingSince = Time.realtimeSinceStartup;
            Say("Looking for your tombstone...");
            ZRoutedRpc.instance.InvokeRoutedRPC(ServerPeerId(), RequestRpc);
        }

        private static long ServerPeerId()
        {
            if (ZNet.instance.IsServer()) return ZDOMan.GetSessionID();
            var server = ZNet.instance.GetServerPeer();
            return server != null ? server.m_uid : ZDOMan.GetSessionID();
        }

        private static void OnReply(long sender, ZPackage package)
        {
            waitingSince = -1f;
            string message = package.ReadString();
            int count = package.ReadInt();
            // If this game already has a moved tombstone loaded, put the loaded copy in place right away as well.
            for (int i = 0; i < count; i++)
            {
                var id = package.ReadZDOID();
                var position = package.ReadVector3();
                var instance = ZNetScene.instance != null ? ZNetScene.instance.FindInstance(id) : null;
                if (instance != null) Place(instance, position);
            }
            Say(message);
        }

        // ---------------------------------------------------------------- server (or host)
        private static void OnRequest(long sender)
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer()) return;
            var reply = new ZPackage();
            var moved = new List<KeyValuePair<ZDOID, Vector3>>();
            string message;
            try { message = Retrieve(sender, moved); }
            catch (Exception e)
            {
                Plugin.Log.LogError("Retrieving a tombstone failed: " + e);
                message = "Something went wrong on the server. See the server log.";
                moved.Clear();
            }
            reply.Write(message);
            reply.Write(moved.Count);
            foreach (var entry in moved) { reply.Write(entry.Key); reply.Write(entry.Value); }
            ZRoutedRpc.instance.InvokeRoutedRPC(sender, ReplyRpc, reply);
        }

        private static string Retrieve(long sender, List<KeyValuePair<ZDOID, Vector3>> moved)
        {
            if (!Plugin.TombEnabled.Value) return "Tombstone retrieval is turned off on this server.";
            var zdoMan = ZDOMan.instance;
            if (zdoMan == null) return "The world is not ready yet. Try again in a moment.";

            long playerId = 0;
            Vector3 playerPosition = Vector3.zero;
            var peer = ZNet.instance.GetPeer(sender);
            if (peer != null)
            {
                var character = peer.m_characterID.IsNone() ? null : zdoMan.GetZDO(peer.m_characterID);
                if (character != null)
                {
                    playerId = character.GetLong(ZDOVars.s_playerID, 0L);
                    playerPosition = character.GetPosition();
                }
            }
            else if (sender == ZDOMan.GetSessionID() && Player.m_localPlayer != null && !Player.m_localPlayer.IsDead())
            {
                playerId = Player.m_localPlayer.GetPlayerID();      // the host's own player
                playerPosition = Player.m_localPlayer.transform.position;
            }
            if (playerId == 0L) return "You need to be alive in the world first. Respawn, then try again.";

            int cooldown = Plugin.TombCooldownSeconds.Value;
            if (cooldown > 0 && LastUse.TryGetValue(playerId, out var last))
            {
                double wait = cooldown - (DateTime.UtcNow - last).TotalSeconds;
                if (wait > 0) return $"Wait {Math.Ceiling(wait):0} more second(s) before trying again.";
            }

            var tombstones = new List<ZDO>();
            int index = 0;
            for (int guard = 0; guard < 1000000 && !zdoMan.GetAllZDOsWithPrefabIterative(TombstonePrefab, tombstones, ref index); guard++) { }

            int found = 0, alreadyHere = 0;
            float farthest = 0f;
            foreach (var zdo in tombstones)
            {
                if (zdo == null || zdo.GetLong(ZDOVars.s_owner, 0L) != playerId) continue;
                found++;
                float distance = Utils.DistanceXZ(zdo.GetPosition(), playerPosition);
                if (distance <= AlreadyHere) { alreadyHere++; continue; }
                farthest = Mathf.Max(farthest, distance);

                float angle = moved.Count * Mathf.PI / 3f;
                var target = playerPosition + new Vector3(Mathf.Cos(angle) * RingRadius, 0.5f, Mathf.Sin(angle) * RingRadius);
                zdo.SetPosition(target);
                zdo.Set(ZDOVars.s_spawnPoint, target);    // the fix: the game would otherwise send it back
                zdo.SetOwner(sender);                     // the requesting player's game takes it over at once
                zdoMan.ForceSendZDO(zdo.m_uid);
                var view = ZNetScene.instance != null ? ZNetScene.instance.FindInstance(zdo) : null;
                if (view != null) Place(view.gameObject, target);
                moved.Add(new KeyValuePair<ZDOID, Vector3>(zdo.m_uid, target));
            }

            if (found == 0) return "No tombstone of yours exists in this world.";
            if (moved.Count == 0) return "Your tombstone is already right here.";
            LastUse[playerId] = DateTime.UtcNow;
            string what = moved.Count == 1 ? "your tombstone" : moved.Count + " tombstones";
            Plugin.Log.LogInfo($"Moved {moved.Count} tombstone(s) of player {playerId} to {playerPosition} (farthest {farthest:0} m).");
            return $"Brought {what} to you from {farthest:0} m away.";
        }

        private static void Place(GameObject instance, Vector3 position)
        {
            instance.transform.position = position;
            var body = instance.GetComponent<Rigidbody>();
            if (body != null) { body.position = position; body.linearVelocity = Vector3.zero; }
            var view = instance.GetComponent<ZNetView>();
            if (view != null && view.IsValid() && view.IsOwner()) view.GetZDO().Set(ZDOVars.s_spawnPoint, position);
        }

        private static void Say(string text)
        {
            if (Player.m_localPlayer != null) Player.m_localPlayer.Message(MessageHud.MessageType.Center, text);
            if (Console.instance != null) Console.instance.Print("[Potato Tweaks] " + text);
        }
    }
}
