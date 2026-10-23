// Potato Tweaks - fixes for Epic Loot bounties. Only used when Epic Loot is installed.
// Adapted from EpicLoot Progression Fix 0.1.281 (bounty-*.cs, creature-loot-scope.cs, star-level-system.cs,
// queued-bow-cancellation.cs), MIT License, Copyright (c) 2026 EpicLoot Progression Fix contributors. See LICENSE.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using EpicLoot;
using EpicLoot.Adventure;
using EpicLoot.Adventure.Feature;
using EpicLoot.MagicItemEffects;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.AI;

namespace PotatoTweaks
{
    internal static class EpicLootFeatures
    {
        internal const string EpicLootId = "randyknapp.mods.epicloot";
        internal const string StarLevelSystemId = "MidnightsFX.StarLevelSystem";

        internal static ManualLogSource Log => Plugin.Log;

        // Epic Loot 0.15.0 took in several fixes Potato Tweaks made (from EpicLoot Progression Fix); those stay off there.
        internal static bool EpicLootHasFixes =>
            Chainloader.PluginInfos.TryGetValue(EpicLootId, out var info) && info.Metadata.Version >= new System.Version(0, 15, 0);

        // Called only when Epic Loot is loaded, so nothing here touches Epic Loot's types otherwise.
        internal static void Enable(Action<Type> patch, Action<string, Action> run)
        {
            Recycling.Enchanted = item => item != null && item.IsMagic();
            patch(typeof(TripleShotCancel));
            patch(typeof(BountyCompletion));
            patch(typeof(ExistingBountyCompletion));
            patch(typeof(BountyObjectiveText));
            patch(typeof(BountyHealthBars));
            patch(typeof(BountyRouteRetry));
            run("Bounty placement", BountyPlacement.Initialize);
            run("Bounty loot", BountyLoot.Initialize);
            if (Chainloader.PluginInfos.ContainsKey(StarLevelSystemId)) run("Bounty strength with StarLevelSystem", BountyTargetStrength.Initialize);
        }
    }

    // Epic Loot's Triple Shot runs its own prefix on the attack's hit; an attack abandoned by blocking must not fire a volley.
    [HarmonyPatch(typeof(MultiShot), nameof(MultiShot.Attack_OnAttackTrigger_Prefix))]
    internal static class TripleShotCancel
    {
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(Attack __0) => !AttackCancel.IsCancelled(__0);
    }

    // The bounty data Epic Loot saves on a bounty's creatures, under the names of BountyTargetComponent's internal constants.
    internal static class BountyCreature
    {
        internal static readonly int IdKey = "BountyID".GetStableHashCode();
        internal static readonly int DataKey = "BountyData".GetStableHashCode();
        internal static readonly int IsAddKey = "IsAdd".GetStableHashCode();

        // The bounty's target, not one of its adds.
        internal static bool IsMainTarget(ZDO zdo) => zdo.GetString(IdKey).Length > 0 && !zdo.GetBool(IsAddKey);

        internal static bool TryRead(ZDO zdo, out BountyInfo bounty)
        {
            bounty = null;
            string data = zdo.GetString(DataKey);
            if (data.Length == 0) return false;
            try { bounty = BountyInfo.FromPackage(new ZPackage(data)); }
            catch (Exception) { return false; }
            return true;
        }
    }

    // ------------------------------------------------------------------ completion on the main kill
    // Keeps Epic Loot's owner, contract and main-target checks, completion message, pin removal and save. Only the
    // "every minion slain too" requirement can be waived; surviving escorts stay in the world.
    [HarmonyPatch(typeof(BountiesAdventureFeature), "OnBountyTargetSlain",
        new[] { typeof(AdventureSaveData), typeof(string), typeof(string), typeof(bool) })]
    internal static class BountyCompletion
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.ToList();
            var slain = AccessTools.Field(typeof(BountyInfo), nameof(BountyInfo.Slain));
            int targetCheck = code.FindIndex(instruction => instruction.LoadsField(slain));
            int sum = code.FindIndex(instruction => instruction.operand is MethodInfo method &&
                method.DeclaringType == typeof(Enumerable) && method.Name == nameof(Enumerable.Sum) &&
                method.ReturnType == typeof(int) && method.IsGenericMethod &&
                method.GetGenericArguments().SequenceEqual(new[] { typeof(BountyTargetInfo) }));
            if (targetCheck < 0 || sum <= targetCheck + 2 || sum + 3 >= code.Count ||
                !(code[targetCheck + 1].opcode == OpCodes.Brfalse || code[targetCheck + 1].opcode == OpCodes.Brfalse_S) ||
                code[sum + 1].opcode != OpCodes.Ldc_I4_0 || code[sum + 2].opcode != OpCodes.Ceq ||
                !(code[sum + 3].opcode == OpCodes.Br || code[sum + 3].opcode == OpCodes.Br_S))
                throw new InvalidOperationException("Epic Loot's bounty completion condition changed.");
            // "minions remaining == 0" becomes "minions remaining == 0 || main kill completes".
            code.Insert(sum + 3, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(BountyCompletion), nameof(Decide))));
            return code;
        }

        private static bool Decide(bool minionsSlain) => minionsSlain || Plugin.BountyMainKillOnly.Value;
    }

    // Contracts accepted earlier may already record their main target as slain. Finish those on the owner's next lookup,
    // keeping the save format. Claimed and abandoned records stay as they are.
    [HarmonyPatch(typeof(PlayerExtensions_Adventure), nameof(PlayerExtensions_Adventure.GetAdventureSaveData))]
    internal static class ExistingBountyCompletion
    {
        private static readonly Action<BountyInfo> RemovePin = AccessTools.MethodDelegate<Action<BountyInfo>>(
            AccessTools.Method(typeof(BountiesAdventureFeature), "RemoveMinimapPin"));

        private static void Postfix(Player player, AdventureSaveData __result)
        {
            if (!Plugin.BountyMainKillOnly.Value || __result == null) return;
            if (player != Player.m_localPlayer || ZNet.instance == null || ZNet.instance.GetWorldUID() == 0) return;
            long playerId = player.GetPlayerID();
            bool changed = false;
            foreach (var bounty in __result.Bounties)
            {
                if (bounty.PlayerID != playerId || bounty.State != BountyState.InProgress || !bounty.Slain) continue;
                bounty.State = BountyState.Complete;
                RemovePin(bounty);
                changed = true;
            }
            if (changed) player.SaveAdventureSaveData();
        }
    }

    // The bounty board says "slay the target" instead of "slay all", once minions are optional.
    [HarmonyPatch(typeof(BountyListElement), "GetTooltip")]
    internal static class BountyObjectiveText
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            int changes = 0;
            var choose = AccessTools.Method(typeof(BountyObjectiveText), nameof(Text));
            foreach (var instruction in instructions)
            {
                yield return instruction;
                if (instruction.opcode == OpCodes.Ldstr && (string)instruction.operand == "$mod_epicloot_bounties_tooltip_slaymultiple")
                {
                    yield return new CodeInstruction(OpCodes.Call, choose);
                    changes++;
                }
            }
            if (changes != 1) throw new InvalidOperationException("Epic Loot's bounty objective text changed.");
        }

        private static string Text(string original) => Plugin.BountyMainKillOnly.Value ? "$mod_epicloot_bounties_tooltip_slay" : original;
    }

    // ------------------------------------------------------------------ overhead health bar
    // Valheim gives each creature either a boss HUD or an overhead HUD. Keep Epic Loot's boss HUD and add a small overhead
    // name and health bar for bounty targets, without changing their boss flag or combat.
    [HarmonyPatch(typeof(BountyTarget), nameof(BountyTarget.Reinitialize))]
    internal static class BountyHealthBars
    {
        private static void Postfix(BountyTarget __instance)
        {
            if (ZNet.instance != null && ZNet.instance.IsDedicated()) return;
            var character = __instance.GetComponent<Character>();
            if (character && character.m_boss && !__instance.GetComponent<BountyHealthBar>())
                __instance.gameObject.AddComponent<BountyHealthBar>();
        }
    }

    internal sealed class BountyHealthBar : MonoBehaviour
    {
        private Character character;
        private GameObject display;
        private GuiBar healthFast;
        private GuiBar healthSlow;
        private TextMeshProUGUI label;

        private void Awake() => character = GetComponent<Character>();

        private void Hide()
        {
            if (display) display.SetActive(false);
        }

        private void LateUpdate()
        {
            var hud = EnemyHud.instance;
            var player = Player.m_localPlayer;
            var camera = Utils.GetMainCamera();
            if (!Plugin.BountyHealthBar.Value || !character || !character.m_boss || character.m_hideHud || character.IsDead() || !hud || !player || !camera ||
                (character.transform.position - player.transform.position).sqrMagnitude >= hud.m_maxShowDistance * hud.m_maxShowDistance)
            {
                Hide();
                return;
            }
            var screenPosition = Utils.WorldToScreenPointScaled(camera, character.GetTopPoint());
            if (screenPosition.z <= 0f)
            {
                Hide();
                return;
            }
            if (!display)
            {
                display = Instantiate(hud.m_baseHud, hud.m_hudRoot.transform);
                display.name = "Bounty overhead health";
                // Level and modifier details stay on the full boss HUD; this one shows only the name and health.
                foreach (Transform child in display.transform)
                    child.gameObject.SetActive(child.name == "Health" || child.name == "Name");
                healthFast = display.transform.Find("Health/health_fast").GetComponent<GuiBar>();
                healthSlow = display.transform.Find("Health/health_slow").GetComponent<GuiBar>();
                var friendly = display.transform.Find("Health/health_fast_friendly");
                if (friendly) friendly.gameObject.SetActive(false);
                label = display.transform.Find("Name").GetComponent<TextMeshProUGUI>();
            }
            display.SetActive(true);
            display.transform.position = screenPosition;
            label.text = Localization.instance.Localize(character.m_name);
            float health = character.GetHealthPercentage();
            healthFast.SetValue(health);
            healthSlow.SetValue(health);
        }

        private void OnDestroy()
        {
            if (display) Destroy(display);
        }

        private void OnDisable() => Hide();
    }

    // ------------------------------------------------------------------ route retry
    // A bounty spawns where no creature has walked lately, so the navigation mesh around it is often still being built when
    // it first asks for a route. The game then keeps the failed route for five seconds and counts it as arrival, so a new
    // bounty stood still in front of a player fighting it. When a bounty creature's route fails because the mesh is still
    // being built, ask again after the game's one-second limit and keep the destination. Everything else is unchanged.
    [HarmonyPatch]
    internal static class BountyRouteRetry
    {
        private static readonly FieldInfo Tiles = AccessTools.Field(typeof(Pathfinding), "m_tiles");
        private static readonly FieldInfo TileMesh = AccessTools.Field(AccessTools.Inner(typeof(Pathfinding), "NavMeshTile"), "m_instance");
        private static readonly MethodInfo TileId = AccessTools.Method(typeof(Pathfinding), "GetTile",
            new[] { typeof(Vector3), typeof(Pathfinding.AgentType) });
        private static readonly ConditionalWeakTable<BaseAI, object> Waiting = new ConditionalWeakTable<BaseAI, object>();
        private static readonly object Marker = new object();
        private static bool keepDestination;

        private static bool Unbuilt(Vector3 point, Pathfinding.AgentType agent)
        {
            object tile = ((IDictionary)Tiles.GetValue(Pathfinding.instance))[TileId.Invoke(Pathfinding.instance, new object[] { point, agent })];
            return tile == null || !((NavMeshDataInstance)TileMesh.GetValue(tile)).valid;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(BaseAI), "FindPath")]
        private static void AskAgain(BaseAI __instance, bool ___m_lastFindPathResult, ref float ___m_lastFindPathTime)
        {
            if (!___m_lastFindPathResult && Time.time - ___m_lastFindPathTime >= 1f && Waiting.TryGetValue(__instance, out _))
                ___m_lastFindPathTime -= 5f;
        }

        [HarmonyPostfix, HarmonyPatch(typeof(BaseAI), "FindPath")]
        private static void NoteResult(BaseAI __instance, Vector3 target, ZNetView ___m_nview, float ___m_lastFindPathTime, bool __result)
        {
            if (___m_lastFindPathTime == Time.time)
            {
                Waiting.Remove(__instance);
                if (!__result && ___m_nview != null && ___m_nview.GetZDO() is ZDO data && data.GetString(BountyCreature.IdKey).Length > 0 &&
                    (Unbuilt(__instance.transform.position, __instance.m_pathAgentType) || Unbuilt(target, __instance.m_pathAgentType)))
                    Waiting.Add(__instance, Marker);
            }
            keepDestination = !__result && Waiting.TryGetValue(__instance, out _);
        }

        [HarmonyPrefix, HarmonyPatch(typeof(BaseAI), "MoveTo")]
        private static void BeforeMove() => keepDestination = false;

        [HarmonyPostfix, HarmonyPatch(typeof(BaseAI), "MoveTo")]
        private static void AfterMove(ref bool __result)
        {
            if (keepDestination) __result = false;
            keepDestination = false;
        }
    }

    // ------------------------------------------------------------------ placement
    // Epic Loot places a new bounty by snapping a random point in the biome's distance band to the nearest suitable cell, so
    // a lone patch beside a wide stretch of sea collects most points, and it keeps a new bounty clear only of the same
    // player's last one. Different players' bounties piled onto the same few spots. Choose evenly among the band's suitable
    // cells instead, at least the spacing from every open bounty and treasure chest in the world, which the server reports.
    internal static class BountyPlacement
    {
        private const float ReplySeconds = 10f;
        private const float IndexSeconds = 30f;
        private const int ChecksPerFrame = 64;
        private const int CellsPerFrame = 512;
        private const string RequestRpc = "PotatoTweaks_BountySpots";
        private const string ReplyRpc = "PotatoTweaks_BountySpotsReply";
        private static readonly int SpawnerPrefab = "EL_SpawnController".GetStableHashCode();
        private static readonly int ChestPrefab = "loot_chest_stone".GetStableHashCode();
        private static readonly int ChestBiomeKey = "TreasureMapChest.Biome".GetStableHashCode();
        private static readonly AccessTools.FieldRef<ZDOMan, Dictionary<ZDOID, ZDO>> Objects =
            AccessTools.FieldRefAccess<ZDOMan, Dictionary<ZDOID, ZDO>>("m_objectsByID");
        private static readonly Dictionary<int, List<Vector2>> replies = new Dictionary<int, List<Vector2>>();
        private static int lastRequest;
        private static HashSet<int> creatures;

        private delegate bool CandidateCheck(Heightmap.Biome biome, Vector2 candidate, float minRadius, float maxRadius, bool requireBand,
            bool requireZoneAgreement, ref int candidatesTried, out Vector3 point);
        private delegate void BandLookup(Heightmap.Biome biome, out float min, out float max);
        private static Action ensureBuilt;
        private static Func<object> indexState;
        private static Func<float> cellSize;
        private static Func<IDictionary> buckets;
        private static AccessTools.FieldRef<object, float[]> radii, eastings, northings;
        private static CandidateCheck check;
        private static BandLookup band;
        private static Func<Heightmap.Biome, AdventureSaveData, Action<bool, Vector3>, IEnumerator> epicLootSearch;
        private static Action<BountyInfo, Vector3, Vector3> spawn;

        private static float Spacing => Plugin.BountySpacing.Value;

        internal static void Initialize()
        {
            var assembly = typeof(BountyInfo).Assembly;
            var index = assembly.GetType("EpicLoot.Adventure.WorldBiomeIndex", true);
            var cache = assembly.GetType("EpicLoot.Adventure.Feature.BountyLocationEarlyCache", true);
            var bucket = AccessTools.Inner(index, "BiomeBucket");
            var state = AccessTools.PropertyGetter(index, "State");
            var size = AccessTools.PropertyGetter(index, "CellSize");
            var all = AccessTools.Field(index, "Buckets");
            if (state == null || size == null || all == null || bucket == null)
                throw new InvalidOperationException("Epic Loot's world biome index changed.");
            indexState = () => state.Invoke(null, null);
            cellSize = () => (float)size.Invoke(null, null);
            buckets = () => (IDictionary)all.GetValue(null);
            radii = AccessTools.FieldRefAccess<float[]>(bucket, "Radius");
            eastings = AccessTools.FieldRefAccess<float[]>(bucket, "X");
            northings = AccessTools.FieldRefAccess<float[]>(bucket, "Z");
            ensureBuilt = AccessTools.MethodDelegate<Action>(AccessTools.Method(index, "EnsureBuilt"));
            check = AccessTools.MethodDelegate<CandidateCheck>(AccessTools.Method(index, "TryCandidate"));
            band = AccessTools.MethodDelegate<BandLookup>(AccessTools.Method(cache, "GetRadiusBand"));
            epicLootSearch = AccessTools.MethodDelegate<Func<Heightmap.Biome, AdventureSaveData, Action<bool, Vector3>, IEnumerator>>(
                AccessTools.Method(cache, "TryGetBiomePoint"));
            spawn = AccessTools.MethodDelegate<Action<BountyInfo, Vector3, Vector3>>(
                AccessTools.Method(typeof(BountiesAdventureFeature), "SpawnBountyInitilizer"));
            var harmony = Plugin.HarmonyInstance;
            harmony.Patch(AccessTools.Method(typeof(BountiesAdventureFeature), nameof(BountiesAdventureFeature.AcceptBounty)),
                prefix: new HarmonyMethod(typeof(BountyPlacement), nameof(Accept)));
            harmony.Patch(AccessTools.Method(typeof(ZNet), "OnNewConnection"), postfix: new HarmonyMethod(typeof(BountyPlacement), nameof(Register)));
        }

        private static bool Accept(Player player, BountyInfo bounty, Action<bool, Vector3> callback, ref IEnumerator __result)
        {
            if (!Plugin.BountySpreadOut.Value) return true;
            __result = Place(player, bounty, callback);
            return false;
        }

        // Epic Loot's AcceptBounty with the spot chosen here. Epic Loot's own search still handles an empty band.
        private static IEnumerator Place(Player player, BountyInfo bounty, Action<bool, Vector3> callback)
        {
            player.Message(MessageHud.MessageType.Center, "$mod_epicloot_bounties_locatingmsg");
            var saveData = player.GetAdventureSaveData();
            var spots = new List<Vector2>();
            yield return CollectSpots(saveData, spots);
            bool found = false;
            var point = Vector3.zero;
            yield return Pick(bounty.Biome, spots, (success, chosen) => { found = success; point = chosen; });
            if (!found)
            {
                EpicLootFeatures.Log.LogWarning($"No suitable {bounty.Biome} spot in its band; Epic Loot places this bounty without keeping it clear of others.");
                yield return epicLootSearch(bounty.Biome, saveData, (success, chosen) => { found = success; point = chosen; });
            }
            if (found)
            {
                if (!saveData.AcceptedBounty(bounty, point, Vector3.zero))
                {
                    EpicLootFeatures.Log.LogWarning($"Save data refused bounty {bounty.ID} ({bounty.Biome}, state={bounty.State}); nothing was spawned.");
                    callback?.Invoke(false, point);
                    yield break;
                }
                saveData.NumberOfTreasureMapsOrBountiesStarted++;
                player.SaveAdventureSaveData();
                spawn(bounty, point, Vector3.zero);
            }
            callback?.Invoke(found, point);
        }

        // This player's own open bounties and unfound treasure, then everyone's from the server.
        private static IEnumerator CollectSpots(AdventureSaveData saveData, List<Vector2> spots)
        {
            foreach (var known in saveData.Bounties)
                if (known.State == BountyState.InProgress) spots.Add(Flat((Vector3)known.Position + (Vector3)known.MinimapCircleOffset));
            foreach (var map in saveData.TreasureMaps)
                if (map.State == TreasureMapState.Purchased) spots.Add(Flat((Vector3)map.Position + (Vector3)map.MinimapCircleOffset));
            if (ZNet.instance.IsServer())
            {
                spots.AddRange(WorldSpots());
                yield break;
            }
            var server = ZNet.instance.GetServerPeer();
            if (server == null) yield break;
            int id = ++lastRequest;
            replies[id] = null;
            server.m_rpc.Invoke(RequestRpc, id);
            float deadline = Time.realtimeSinceStartup + ReplySeconds;
            while (replies[id] == null && Time.realtimeSinceStartup < deadline) yield return null;
            var reply = replies[id];
            replies.Remove(id);
            if (reply == null)
                EpicLootFeatures.Log.LogWarning($"The server did not report open bounties within {ReplySeconds:0} seconds; this bounty keeps clear of the player's own only.");
            else spots.AddRange(reply);
        }

        // Evenly among the band's suitable cells at least Spacing from every spot, or failing that the one farthest from them.
        private static IEnumerator Pick(Heightmap.Biome biome, List<Vector2> spots, Action<bool, Vector3> onComplete)
        {
            ensureBuilt();
            float deadline = Time.unscaledTime + IndexSeconds;
            while (indexState().ToString() == "Building" && Time.unscaledTime < deadline) yield return null;
            var all = indexState().ToString() == "Ready" ? buckets() : null;
            var cells = all != null && all.Contains(biome) ? all[biome] : null;
            if (cells == null)
            {
                onComplete(false, Vector3.zero);
                yield break;
            }
            band(biome, out float min, out float max);
            float[] radius = radii(cells), x = eastings(cells), z = northings(cells);
            int first = LowerBound(radius, min), count = UpperBound(radius, max) - first;
            var clearance = new float[Math.Max(count, 0)];
            var clear = new List<int>();
            for (int i = 0; i < count; i++)
            {
                if (i > 0 && i % CellsPerFrame == 0) yield return null;
                clearance[i] = Clearance(new Vector2(x[first + i], z[first + i]), spots);
                if (clearance[i] >= Spacing) clear.Add(i);
            }
            for (int i = clear.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (clear[i], clear[j]) = (clear[j], clear[i]);
            }
            int checks = 0, mixed = -1;
            foreach (int i in clear)
            {
                if (++checks % ChecksPerFrame == 0) yield return null;
                var cell = new Vector2(x[first + i], z[first + i]);
                if (Suitable(biome, cell, min, max, true, out var center))
                {
                    var point = Refine(biome, cell, center, min, max, true, spots);
                    EpicLootFeatures.Log.LogInfo($"Placed a {biome} bounty at ({point.x:0}, {point.z:0}), {Describe(Clearance(Flat(point), spots))} from the nearest of {spots.Count} open spots.");
                    onComplete(true, point);
                    yield break;
                }
                if (mixed < 0 && Suitable(biome, cell, min, max, false, out _)) mixed = i;
            }
            if (mixed >= 0)
            {
                var cell = new Vector2(x[first + mixed], z[first + mixed]);
                Suitable(biome, cell, min, max, false, out var center);
                onComplete(true, Refine(biome, cell, center, min, max, false, spots));
                yield break;
            }
            var farthest = Enumerable.Range(0, count).ToArray();
            Array.Sort(clearance.Select(value => -value).ToArray(), farthest);
            foreach (int i in farthest)
            {
                if (++checks % ChecksPerFrame == 0) yield return null;
                if (!Suitable(biome, new Vector2(x[first + i], z[first + i]), min, max, false, out var point)) continue;
                EpicLootFeatures.Log.LogWarning($"No {biome} spot is {Spacing:0} m from all {spots.Count} open bounty and treasure spots; used the farthest, {clearance[i]:0} m away.");
                onComplete(true, point);
                yield break;
            }
            onComplete(false, Vector3.zero);
        }

        private static Vector3 Refine(Heightmap.Biome biome, Vector2 cell, Vector3 center, float min, float max, bool ownZone, List<Vector2> spots)
        {
            float reach = cellSize() * 0.5f;
            for (int attempt = 0; attempt < 6; attempt++)
            {
                var candidate = cell + UnityEngine.Random.insideUnitCircle * reach;
                if (Clearance(candidate, spots) >= Spacing && Suitable(biome, candidate, min, max, ownZone, out var point)) return point;
            }
            return center;
        }

        // Epic Loot's own rules: in the band and the biome, not deep underwater or on lava, optionally the whole zone in the biome.
        private static bool Suitable(Heightmap.Biome biome, Vector2 candidate, float min, float max, bool ownZone, out Vector3 point)
        {
            int tried = 0;
            return check(biome, candidate, min, max, true, ownZone, ref tried, out point);
        }

        private static float Clearance(Vector2 point, List<Vector2> spots)
        {
            float nearest = float.PositiveInfinity;
            foreach (var spot in spots) nearest = Math.Min(nearest, (spot - point).sqrMagnitude);
            return Mathf.Sqrt(nearest);
        }

        private static string Describe(float clearance) => float.IsPositiveInfinity(clearance) ? "any distance" : $"{clearance:0} m";

        private static Vector2 Flat(Vector3 position) => new Vector2(position.x, position.z);

        private static int LowerBound(float[] sorted, float value)
        {
            int low = 0, high = sorted.Length;
            while (low < high)
            {
                int middle = low + (high - low) / 2;
                if (sorted[middle] < value) low = middle + 1;
                else high = middle;
            }
            return low;
        }

        private static int UpperBound(float[] sorted, float value)
        {
            int low = 0, high = sorted.Length;
            while (low < high)
            {
                int middle = low + (high - low) / 2;
                if (sorted[middle] <= value) low = middle + 1;
                else high = middle;
            }
            return low;
        }

        private static void Register(ZNet __instance, ZNetPeer peer)
        {
            if (__instance.IsServer()) peer.m_rpc.Register<int>(RequestRpc, (rpc, id) => rpc.Invoke(ReplyRpc, id, Pack(WorldSpots())));
            else peer.m_rpc.Register<int, ZPackage>(ReplyRpc, Receive);
        }

        private static void Receive(ZRpc rpc, int id, ZPackage package)
        {
            if (!replies.TryGetValue(id, out var pending) || pending != null) return;
            int count = package.ReadInt();
            var spots = new List<Vector2>(count);
            for (int i = 0; i < count; i++) spots.Add(new Vector2(package.ReadSingle(), package.ReadSingle()));
            replies[id] = spots;
        }

        private static ZPackage Pack(List<Vector2> spots)
        {
            var package = new ZPackage();
            package.Write(spots.Count);
            foreach (var spot in spots)
            {
                package.Write(spot.x);
                package.Write(spot.y);
            }
            return package;
        }

        // Spawners still waiting for a player, living bounty creatures with their bounty's own spot, and unopened treasure chests.
        private static List<Vector2> WorldSpots()
        {
            var watch = Stopwatch.StartNew();
            creatures ??= new HashSet<int>(ZNetScene.instance.m_prefabs.Where(prefab => prefab.GetComponent<Character>()).Select(prefab => prefab.name.GetStableHashCode()));
            var spots = new List<Vector2>();
            var bounties = new HashSet<string>();
            foreach (var zdo in Objects(ZDOMan.instance).Values)
            {
                int prefab = zdo.GetPrefab();
                if (prefab == SpawnerPrefab || prefab == ChestPrefab && zdo.GetString(ChestBiomeKey).Length > 0) spots.Add(Flat(zdo.GetPosition()));
                else if (creatures.Contains(prefab))
                {
                    string id = zdo.GetString(BountyCreature.IdKey);
                    if (id.Length == 0) continue;
                    spots.Add(Flat(zdo.GetPosition()));
                    if (bounties.Add(id) && BountyCreature.TryRead(zdo, out var bounty))
                        spots.Add(Flat((Vector3)bounty.Position + (Vector3)bounty.MinimapCircleOffset));
                }
            }
            EpicLootFeatures.Log.LogInfo($"Reported {spots.Count} open bounty and treasure spots in {watch.ElapsedMilliseconds} ms.");
            return spots;
        }
    }

    // ------------------------------------------------------------------ loot
    // A bounty's main target repeats each enchanted equipment drop (by default once more), each copy with its own
    // enchantments, and always drops at least one enchanted item from its own equipment loot. Other drops are unchanged.
    internal static class BountyLoot
    {
        private sealed class Reward { internal bool HasEquipment; }
        private static readonly int CorpseKey = "PotatoTweaks.BountyLoot".GetStableHashCode();
        // Epic Loot 0.15.0 spawns equipment into a private "loot output" (a list of spawned objects or of items) and gives
        // creature loot an extra "allow bonus rolls" switch; 0.14.13 used a plain object list. Both are handled.
        private static readonly MethodInfo SpawnNormalItem = AccessTools.Method(typeof(LootRoller), "SpawnNormalItem");
        private static readonly bool OutputObject = SpawnNormalItem != null && SpawnNormalItem.GetParameters().Length == 5;
        private static readonly Type OutputType = OutputObject ? SpawnNormalItem.GetParameters()[4].ParameterType : null;
        private static readonly FieldInfo OutputObjects = OutputType != null ? AccessTools.Field(OutputType, "Objects") : null;
        private static readonly MethodInfo NewObjectOutput = OutputType != null ? AccessTools.Method(OutputType, "ForObjects") : null;
        private static readonly MethodInfo RollLoot = AccessTools.Method(typeof(LootRoller), nameof(LootRoller.RollLootTableAndSpawnObjects),
            new[] { typeof(List<LootTable>), typeof(int), typeof(string), typeof(Vector3) });
        private static readonly MethodInfo RollLootWithBonus = AccessTools.Method(typeof(LootRoller), nameof(LootRoller.RollLootTableAndSpawnObjects),
            new[] { typeof(List<LootTable>), typeof(int), typeof(string), typeof(Vector3), typeof(bool) });

        // The spawned objects an output holds: the list itself (0.14.13) or its Objects (0.15.0; null when it holds items).
        private static List<GameObject> Spawned(object output) => OutputObject ? OutputObjects?.GetValue(output) as List<GameObject> : output as List<GameObject>;

        private static void SpawnEquipment(LootDrop lootDrop, string objectName, Vector3 dropPoint, float luckFactor, object output) =>
            SpawnNormalItem.Invoke(null, OutputObject
                ? new[] { lootDrop, objectName, dropPoint, luckFactor, output }
                : new[] { lootDrop, objectName, dropPoint, luckFactor, true, output });
        [ThreadStatic] private static Reward current;
        [ThreadStatic] private static bool bonus;
        [ThreadStatic] private static bool guaranteeing;

        internal static void Initialize()
        {
            var harmony = Plugin.HarmonyInstance;
            var self = typeof(BountyLoot);
            HarmonyMethod Hook(string name) => new HarmonyMethod(AccessTools.Method(self, name));
            // Epic Loot rolls a creature's loot in its death callback, or later in its corpse's when the corpse holds the drop.
            harmony.Patch(AccessTools.Method(typeof(CharacterDrop_OnDeath_Patch), nameof(CharacterDrop_OnDeath_Patch.Postfix)),
                prefix: Hook(nameof(BeginDeath)), finalizer: Hook(nameof(EndDeath)));
            harmony.Patch(AccessTools.Method(typeof(Ragdoll_Setup_Patch), nameof(Ragdoll_Setup_Patch.Postfix)), postfix: Hook(nameof(RememberCorpse)));
            harmony.Patch(AccessTools.Method(typeof(Ragdoll_SpawnLoot_Patch), nameof(Ragdoll_SpawnLoot_Patch.Postfix)),
                prefix: Hook(nameof(BeginCorpse)), finalizer: Hook(nameof(EndDeath)));
            if (SpawnNormalItem == null || (OutputObject && (OutputObjects == null || NewObjectOutput == null)))
                throw new InvalidOperationException("Epic Loot's equipment spawning changed.");
            harmony.Patch(SpawnNormalItem, prefix: Hook(nameof(BeforeEquipment)), postfix: Hook(nameof(DoubleEquipment)));
            var helper = typeof(LootRoller).Assembly.GetType("EpicLoot.EpicLootDropsHelper", true);
            var withBonus = AccessTools.Method(helper, "OnCharacterDeath", new[] { typeof(string), typeof(int), typeof(Vector3), typeof(bool) });
            harmony.Patch(withBonus ?? AccessTools.Method(helper, "OnCharacterDeath", new[] { typeof(string), typeof(int), typeof(Vector3) }),
                transpiler: Hook(nameof(GuaranteeTranspiler)));
        }

        private static bool MainTarget(CharacterDrop drop)
        {
            var character = drop ? drop.GetComponent<Character>() : null;
            var view = character && !character.IsPlayer() && !character.IsTamed() ? character.GetComponent<ZNetView>() : null;
            var data = view && view.IsValid() ? view.GetZDO() : null;
            return data != null && BountyCreature.IsMainTarget(data);
        }

        private static void BeginDeath(CharacterDrop __0, out Reward __state)
        {
            __state = current;
            current = MainTarget(__0) ? new Reward() : null;
        }

        private static void RememberCorpse(Ragdoll __0, CharacterDrop __1)
        {
            var view = __0 ? __0.GetComponent<ZNetView>() : null;
            if (view && view.IsValid()) view.GetZDO().Set(CorpseKey, MainTarget(__1));
        }

        private static void BeginCorpse(Ragdoll __0, out Reward __state)
        {
            __state = current;
            var view = __0 ? __0.GetComponent<ZNetView>() : null;
            current = view && view.IsValid() && view.GetZDO().GetBool(CorpseKey) ? new Reward() : null;
        }

        private static void EndDeath(Reward __state) => current = __state;

        private static bool Enchanted(GameObject item) => item && item.TryGetComponent<ItemDrop>(out var drop) &&
            global::EpicLoot.EpicLoot.IsAllowedMagicItemType(drop.m_itemData) && drop.m_itemData.IsMagic();

        // The output is the method's last argument in both versions.
        private static void BeforeEquipment(object[] __args, out int __state) => __state = Spawned(__args[__args.Length - 1])?.Count ?? 0;

        private static void DoubleEquipment(LootDrop lootDrop, string objectName, Vector3 dropPoint, float luckFactor, object[] __args, int __state)
        {
            var output = __args[__args.Length - 1];
            var results = Spawned(output);
            if (current == null || bonus || results == null || !results.Skip(__state).Any(Enchanted)) return;
            current.HasEquipment = true;
            if (guaranteeing) return;
            bonus = true;
            try
            {
                for (int copy = 0; copy < Plugin.BountyExtraCopies.Value; copy++)
                    SpawnEquipment(lootDrop, objectName, dropPoint, luckFactor, output);
            }
            finally { bonus = false; }
        }

        private static IEnumerable<CodeInstruction> GuaranteeTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.ToList();
            var roll = RollLootWithBonus ?? RollLoot;
            var matches = code.Where(x => x.Calls(roll)).ToList();
            if (matches.Count != 1) throw new InvalidOperationException("Epic Loot's creature loot entry point changed; expected one loot roll.");
            matches[0].opcode = OpCodes.Call;
            matches[0].operand = AccessTools.Method(typeof(BountyLoot), RollLootWithBonus != null ? nameof(RollCreatureLootWithBonus) : nameof(RollCreatureLoot));
            return code;
        }

        private static List<GameObject> RollCreatureLootWithBonus(List<LootTable> tables, int level, string creatureName, Vector3 dropPoint, bool allowBonusRolls)
        {
            var result = (List<GameObject>)RollLootWithBonus.Invoke(null, new object[] { tables, level, creatureName, dropPoint, allowBonusRolls });
            try { EnsureEquipment(tables, level, creatureName, dropPoint, result); }
            catch (Exception e) { EpicLootFeatures.Log.LogError($"Bounty creature {creatureName}'s guaranteed enchanted item failed: {e.Message}"); }
            return result;
        }

        private static List<GameObject> RollCreatureLoot(List<LootTable> tables, int level, string creatureName, Vector3 dropPoint)
        {
            var result = LootRoller.RollLootTableAndSpawnObjects(tables, level, creatureName, dropPoint);
            try { EnsureEquipment(tables, level, creatureName, dropPoint, result); }
            catch (Exception e) { EpicLootFeatures.Log.LogError($"Bounty creature {creatureName}'s guaranteed enchanted item failed: {e.Message}"); }
            return result;
        }

        // An equipment branch holds equipment, an equipment set or a reference to a biome/star equipment table.
        private static bool EquipmentBranch(LootDrop source)
        {
            if (source == null || string.IsNullOrEmpty(source.Item)) return false;
            var prefab = ObjectDB.instance.GetItemPrefab(source.Item);
            if (prefab && prefab.TryGetComponent<ItemDrop>(out var drop))
                return global::EpicLoot.EpicLoot.IsAllowedMagicItemType(drop.m_itemData);
            if (LootRoller.ItemSets.TryGetValue(source.Item, out var set)) return set.Loot.Any(EquipmentBranch);
            var reference = source.Item.Split('.');
            return reference.Length == 2 && int.TryParse(reference[1], out int level) &&
                LootRoller.GetLootTable(reference[0]).SelectMany(table => LootRoller.GetLootForLevel(table, level)).Any(EquipmentBranch);
        }

        private static void EnsureEquipment(List<LootTable> tables, int level, string creatureName, Vector3 dropPoint, List<GameObject> results)
        {
            if (current == null || current.HasEquipment || !Plugin.BountyGuaranteedItem.Value || tables == null || results == null) return;
            float luck = LootRoller.GetLuckFactor(dropPoint);
            var sources = tables.SelectMany(table => LootRoller.GetLootForLevel(table, level)).Where(EquipmentBranch).ToList();
            if (sources.Count == 0)
            {
                EpicLootFeatures.Log.LogWarning($"Bounty creature {creatureName} has no equipment loot; its guaranteed enchanted item was skipped.");
                return;
            }
            var pool = new WeightedRandomCollection<LootDrop>();
            pool.Setup(sources, source => source.Weight);
            var equipment = LootRoller.ResolveLootDrop(pool.Roll(), luck, false);
            guaranteeing = true;
            try
            {
                // 0.15.0 spawns into its own output object; its objects then join the creature's loot.
                var output = OutputObject ? NewObjectOutput.Invoke(null, null) : results;
                SpawnEquipment(equipment, creatureName, dropPoint, luck, output);
                if (OutputObject && Spawned(output) is List<GameObject> spawned) results.AddRange(spawned);
                if (!current.HasEquipment) EpicLootFeatures.Log.LogWarning($"Bounty creature {creatureName} did not produce its guaranteed enchanted item.");
            }
            finally { guaranteeing = false; }
        }
    }

    // ------------------------------------------------------------------ strength with StarLevelSystem
    // Epic Loot gives a bounty's creatures more health as they spawn, through StarLevelSystem: three times for a gold
    // bounty's target, twice for an iron one's and one and a half times for adds. StarLevelSystem kept that only in the
    // stats the spawning game worked out, so a creature lost it once reloaded or handed to another player. Epic Loot also
    // marks the target a boss (for its boss health bar), and StarLevelSystem then scaled it as a boss: +2% damage per star
    // instead of +20%, boss modifiers and, once reloaded, less health per star. Give the bonus back from the bounty the
    // creature carries, and let StarLevelSystem scale the target as the ordinary creature it is. The boss health bar stays.
    internal static class BountyTargetStrength
    {
        private static readonly Func<BountyInfo, bool, float> HealthModifier =
            AccessTools.MethodDelegate<Func<BountyInfo, bool, float>>(AccessTools.Method(typeof(BountyTarget), "GetMaxHealthModifier"));
        private static readonly AccessTools.FieldRef<Character, ZNetView> CharacterView = AccessTools.FieldRefAccess<Character, ZNetView>("m_nview");
        private static PropertyInfo baseValueModifiers;
        private static object baseHealth;

        internal static void Initialize()
        {
            var assembly = Chainloader.PluginInfos[EpicLootFeatures.StarLevelSystemId].Instance.GetType().Assembly;
            var cache = assembly.GetType("StarLevelSystem.Data.CompositeLazyCache", true);
            var entry = AccessTools.Method(cache, "GetCacheEntry", new[] { typeof(Character) })?.ReturnType;
            baseValueModifiers = AccessTools.Property(entry, "CreatureBaseValueModifiers");
            var attribute = assembly.GetType("StarLevelSystem.common.DataObjects+CreatureBaseAttribute");
            baseHealth = attribute != null && Enum.IsDefined(attribute, "BaseHealth") ? Enum.Parse(attribute, "BaseHealth") : null;
            var damage = AccessTools.Method(assembly.GetType("StarLevelSystem.modules.Damage.DamagePatches+ModifyDamagePerLevel"),
                "DetermineDamageFactor", new[] { typeof(Character) });
            if (entry == null || baseValueModifiers == null || baseHealth == null || damage?.ReturnType != typeof(float))
                throw new InvalidOperationException("StarLevelSystem's creature cache or damage factor changed.");
            var stats = assembly.GetType("StarLevelSystem.modules.CreatureSetup.CreatureStatOverrides", true);
            var modifiers = assembly.GetType("StarLevelSystem.modules.Modifiers.CreatureModifiers");
            var restore = AccessTools.Method(cache, "ApplyPersistedStatOverrides", new[] { typeof(ZDO), entry });
            var scaling = new[]
            {
                AccessTools.Method(cache, "GetAndSetLocalCache"),
                damage,
                AccessTools.Method(modifiers, "SelectModifiersForCreature"),
                AccessTools.Method(modifiers, "TryRollEvolutionModifier"),
                AccessTools.Method(assembly.GetType("StarLevelSystem.modules.LevelSystem.LevelSelection"), "GetMaxCreatureLevel"),
                AccessTools.Method(stats, "Apply"),
            };
            var setStats = AccessTools.GetDeclaredMethods(stats).SingleOrDefault(method => method.Name == "SetInEntry" &&
                method.GetParameters().Length == 4 && method.GetParameters()[0].ParameterType == typeof(Character));
            if (restore == null || setStats == null || scaling.Any(method => method == null || method.GetParameters().FirstOrDefault()?.ParameterType != typeof(Character)))
                throw new InvalidOperationException("StarLevelSystem's creature stats changed.");
            var harmony = Plugin.HarmonyInstance;
            var self = typeof(BountyTargetStrength);
            harmony.Patch(restore, postfix: new HarmonyMethod(self, nameof(RestoreBonus)));
            harmony.Patch(setStats, postfix: new HarmonyMethod(self, nameof(RestoreStatBonus)));
            foreach (var method in scaling)
                harmony.Patch(method, prefix: new HarmonyMethod(self, nameof(AsOrdinary)), finalizer: new HarmonyMethod(self, nameof(AsBoss)));
        }

        private static void RestoreStatBonus(Character __0, object __1) => RestoreBonus(__0 ? CharacterView(__0)?.GetZDO() : null, __1);

        // Like Epic Loot's, the bonus replaces the creature's base health multiplier.
        private static void RestoreBonus(ZDO __0, object __1)
        {
            if (!Plugin.BountyNormalScaling.Value || __0 == null || __1 == null || !BountyCreature.TryRead(__0, out var bounty)) return;
            if (baseValueModifiers.GetValue(__1) is IDictionary values) values[baseHealth] = HealthModifier(bounty, __0.GetBool(BountyCreature.IsAddKey));
        }

        // Epic Loot marks only a bounty's target a boss; its adds stay ordinary.
        private static void AsOrdinary(Character __0, out bool __state)
        {
            __state = false;
            if (!Plugin.BountyNormalScaling.Value || __0 == null || !__0.m_boss) return;
            var view = CharacterView(__0);
            var zdo = view != null ? view.GetZDO() : null;
            __state = zdo != null && zdo.GetString(BountyCreature.IdKey).Length > 0;
            if (__state) __0.m_boss = false;
        }

        private static void AsBoss(Character __0, bool __state)
        {
            if (__state) __0.m_boss = true;
        }
    }
}
