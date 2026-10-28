// Potato Tweaks - trophy pity and trophy list, written from scratch.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PotatoTweaks
{
    // Every kill of a creature that drops no trophy makes its next trophy more likely ("pity"); the count resets when the
    // trophy drops. The counts are per character and kept in BepInEx/config/potato.tweaks.trophies.txt. The roll happens on
    // the game that runs the creature when it dies, as in the game, with the killer's pity; the kill counts for the killer.
    // The Trophies tab shows each trophy's next chance, and can show trophies you haven't collected yet in their places.
    internal static class Trophies
    {
        internal const string OriginalModId = "com.koendertjh.trophyassister";
        private static readonly string FilePath = Path.Combine(Paths.ConfigPath, "potato.tweaks.trophies.txt");
        private static readonly AccessTools.FieldRef<Character, HitData> LastHit = AccessTools.FieldRefAccess<Character, HitData>("m_lastHit");
        private static readonly AccessTools.FieldRef<InventoryGui, List<GameObject>> TrophyList = AccessTools.FieldRefAccess<InventoryGui, List<GameObject>>("m_trophyList");
        private static readonly AccessTools.FieldRef<InventoryGui, float> ListBaseSize = AccessTools.FieldRefAccess<InventoryGui, float>("m_trophieListBaseSize");

        // character -> creature prefab -> dry kills; character -> creatures killed.
        private static readonly Dictionary<string, Dictionary<string, int>> Dry = new Dictionary<string, Dictionary<string, int>>();
        private static readonly Dictionary<string, HashSet<string>> Killed = new Dictionary<string, HashSet<string>>();
        private static bool loaded;
        private static Dictionary<string, List<KeyValuePair<string, float>>> sourcesByTrophy;

        internal static readonly Type[] Patches = { typeof(Roll), typeof(TrophyTab), typeof(PublishOnSpawn), typeof(PublishOnTrophy), typeof(RegisterRpc) };

        private static string CharacterKey()
        {
            var profile = Game.instance != null ? Game.instance.GetPlayerProfile() : null;
            return profile != null ? profile.GetFilename() : null;
        }

        // ---------------------------------------------------------------- saved counts
        private static void Load()
        {
            if (loaded) return;
            loaded = true;
            try
            {
                if (!File.Exists(FilePath)) return;
                foreach (var line in File.ReadAllLines(FilePath))
                {
                    var parts = line.Split('|');
                    if (parts.Length != 3) continue;
                    if (parts[1] == "killed") Set(Killed, parts[0]).Add(parts[2]);
                    else if (parts[1] == "dry")
                    {
                        var pair = parts[2].Split('=');
                        if (pair.Length == 2 && int.TryParse(pair[1], out int count)) Set(Dry, parts[0])[pair[0]] = count;
                    }
                }
            }
            catch (Exception e) { Plugin.Log.LogWarning("Couldn't read the trophy counts: " + e.Message); }
        }

        private static void Save()
        {
            try
            {
                var lines = new List<string>();
                foreach (var character in Dry) foreach (var entry in character.Value) lines.Add($"{character.Key}|dry|{entry.Key}={entry.Value}");
                foreach (var character in Killed) foreach (var creature in character.Value) lines.Add($"{character.Key}|killed|{creature}");
                File.WriteAllLines(FilePath, lines);
            }
            catch (Exception e) { Plugin.Log.LogWarning("Couldn't save the trophy counts: " + e.Message); }
        }

        private static T Set<T>(Dictionary<string, T> map, string key) where T : new()
        {
            if (!map.TryGetValue(key, out var value)) map[key] = value = new T();
            return value;
        }

        // Switching from the TrophyAssister mod: its counts are keyed by its own character number, so the counts of the
        // character with the most entries move to the first character that loads here without any counts of its own.
        internal static void ImportFromTrophyAssister(string character)
        {
            string old = Path.Combine(Paths.ConfigPath, "TrophyAssister_pity.txt");
            string done = old + ".imported";
            if (!File.Exists(old) || File.Exists(done) || Dry.ContainsKey(character)) return;
            try
            {
                var byOwner = new Dictionary<string, Dictionary<string, int>>();
                foreach (var line in File.ReadAllLines(old))
                {
                    var bar = line.Split('|');
                    if (bar.Length != 2) continue;
                    var pair = bar[1].Split('=');
                    if (pair.Length == 2 && int.TryParse(pair[1], out int count)) Set(byOwner, bar[0])[pair[0]] = count;
                }
                var best = byOwner.OrderByDescending(entry => entry.Value.Count).FirstOrDefault();
                if (best.Value == null) return;
                Dry[character] = best.Value;
                foreach (var creature in best.Value.Keys) Set(Killed, character).Add(creature);
                File.WriteAllText(done, "Copied into potato.tweaks.trophies.txt for " + character);
                Save();
                Plugin.Log.LogInfo($"Copied {best.Value.Count} trophy pity counts from TrophyAssister to {character}.");
            }
            catch (Exception e) { Plugin.Log.LogWarning("Couldn't copy TrophyAssister's counts: " + e.Message); }
        }

        // ---------------------------------------------------------------- the chance
        private static float Boosted(float baseChance, int dry)
        {
            float chance = baseChance * Mathf.Max(0f, Plugin.TrophyChanceMultiplier.Value);
            int guarantee = Plugin.TrophyGuarantee.Value;
            if (guarantee > 0 && dry >= guarantee) return 1f;
            if (!Plugin.TrophyPity.Value || dry <= 0) return Mathf.Min(1f, chance);
            if (Plugin.TrophyFlatMode.Value) return Mathf.Min(1f, chance + Plugin.TrophyFlatBonus.Value * dry);
            float growth = baseChance <= Plugin.TrophyRareThreshold.Value ? Plugin.TrophyRareGrowth.Value : Plugin.TrophyGrowth.Value;
            return Mathf.Min(1f, chance * Mathf.Pow(1f + Mathf.Max(0f, growth), dry));
        }

        private static bool IsTrophy(GameObject prefab) =>
            prefab && prefab.TryGetComponent<ItemDrop>(out var item) && item.m_itemData.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Trophy;

        private static bool Collected(string trophy) => Player.m_localPlayer && Player.m_localPlayer.GetTrophies().Contains(trophy);

        // ---------------------------------------------------------------- whose kill it is
        // A creature is run by one player's game, and only that game rolls its drops, so the kill is often someone
        // else's. Each player's game publishes its dry kills (and, with "Only uncollected trophies", its collected
        // trophies) on its player, so the game running the creature rolls the trophy with the killer's pity, through
        // the game's usual drops. It then tells the killer's game, which counts the kill. A killer whose game doesn't
        // publish (an older version) gets the plain roll, and the kill counts for no one.
        private const string PublishKey = "PotatoTweaks.TrophyPity", ResultRpc = "PotatoTweaks_TrophyKill";
        private static string published;

        internal sealed class Killer
        {
            internal bool Local;
            internal long Peer;
            internal Dictionary<string, int> Dry;
            internal HashSet<string> Collected;
        }

        // The player who gets the kill: the last hit (burning and poison after a hit count), or the player whose tames
        // and summons made it.
        internal static Player KillerOf(Character dead)
        {
            var attacker = LastHit(dead)?.GetAttacker();
            if (!attacker) return null;
            if (attacker is Player player) return player;
            var ai = attacker.GetBaseAI() as MonsterAI;
            var followed = ai ? ai.GetFollowTarget() : null;
            return followed ? followed.GetComponent<Player>() : null;
        }

        // Who the kill counts for: this game's character, another player's (with their published pity), or no one.
        // A kill with no player behind it counts for this game's character only with "Only count my kills" off.
        private static Killer Find(Character dead)
        {
            var player = KillerOf(dead);
            var local = Player.m_localPlayer;
            if (player && player == local) return new Killer { Local = true };
            if (!player) return Plugin.TrophyOnlyMine.Value || !local ? null : new Killer { Local = true };
            var zdo = player.GetComponent<ZNetView>()?.GetZDO();
            string data = zdo?.GetString(PublishKey, "") ?? "";
            if (data.Length == 0 || zdo.GetOwner() == 0L) return null;
            return Parse(data, zdo.GetOwner());
        }

        internal static Killer Parse(string data, long peer)
        {
            var killer = new Killer { Peer = peer, Dry = new Dictionary<string, int>(), Collected = new HashSet<string>() };
            var parts = data.Split('|');
            if (parts.Length > 1)
                foreach (var entry in parts[1].Split(','))
                {
                    var pair = entry.Split('=');
                    if (pair.Length == 2 && int.TryParse(pair[1], out int count)) killer.Dry[pair[0]] = count;
                }
            if (parts.Length > 2)
                foreach (var trophy in parts[2].Split(','))
                    if (trophy.Length > 0) killer.Collected.Add(trophy);
            return killer;
        }

        internal static string Format(Dictionary<string, int> dry, IEnumerable<string> collected) =>
            "1|" + string.Join(",", dry.Where(entry => entry.Value > 0).Select(entry => entry.Key + "=" + entry.Value)) + "|" + string.Join(",", collected);

        // This game's dry kills (and collected trophies, when only uncollected ones get pity) on its player.
        internal static void Publish()
        {
            var player = Player.m_localPlayer;
            var zdo = player ? player.GetComponent<ZNetView>()?.GetZDO() : null;
            string character = CharacterKey();
            if (zdo == null || character == null) return;
            Load();
            ImportFromTrophyAssister(character);
            string data = Format(Set(Dry, character), Plugin.TrophyOnlyUncollected.Value ? player.GetTrophies() : new List<string>());
            if (data == published && zdo.GetString(PublishKey, "") == data) return;
            published = data;
            zdo.Set(PublishKey, data);
        }

        [HarmonyPatch(typeof(Player), nameof(Player.OnSpawned))]
        internal static class PublishOnSpawn
        {
            private static void Postfix(Player __instance)
            {
                if (__instance != Player.m_localPlayer) return;
                published = null;
                Publish();
            }
        }

        [HarmonyPatch(typeof(Player), "AddTrophy")]
        internal static class PublishOnTrophy
        {
            private static void Postfix(Player __instance)
            {
                if (__instance == Player.m_localPlayer && Plugin.TrophyOnlyUncollected.Value) Publish();
            }
        }

        // ZRoutedRpc is created anew for every world session, so the handler is registered as each game starts.
        [HarmonyPatch(typeof(Game), "Start")]
        internal static class RegisterRpc
        {
            private static void Postfix() => ZRoutedRpc.instance?.Register<ZPackage>(ResultRpc, OnResult);
        }

        // The game running the creature rolled our kill.
        private static void OnResult(long sender, ZPackage package)
        {
            try
            {
                string creature = package.ReadString();
                int stars = package.ReadInt();
                bool pity = package.ReadBool(), dropped = package.ReadBool();
                float baseChance = package.ReadSingle();
                string character = CharacterKey();
                if (character == null || string.IsNullOrEmpty(creature)) return;
                Load();
                ImportFromTrophyAssister(character);
                Record(character, creature, stars, pity, dropped, baseChance);
            }
            catch (Exception e) { Plugin.Log.LogWarning("Trophy kill from another player's game: " + e.Message); }
        }

        // Counts a kill for this game's character: its dry kills go up, or reset when a trophy dropped.
        private static void Record(string character, string creature, int stars, bool pity, bool dropped, float baseChance)
        {
            Set(Killed, character).Add(creature);
            if (pity)
            {
                var dryKills = Set(Dry, character);
                if (dropped)
                {
                    dryKills.Remove(creature);
                    if (Plugin.TrophyNotify.Value) Player.m_localPlayer?.Message(MessageHud.MessageType.TopLeft, "Trophy dropped!");
                }
                else
                {
                    int add = 1 + Mathf.Max(0, stars) * Mathf.Max(0, Plugin.TrophyExtraKillsPerStar.Value);
                    dryKills.TryGetValue(creature, out int count);
                    dryKills[creature] = count + add;
                    if (Plugin.TrophyShowProgress.Value)
                    {
                        float chance = Boosted(baseChance, count + add);
                        Player.m_localPlayer?.Message(MessageHud.MessageType.TopLeft, $"No trophy: {count + add} dry kills, next {chance * 100f:0.#}%");
                    }
                }
            }
            Save();
            Publish();
        }

        [HarmonyPatch(typeof(CharacterDrop), nameof(CharacterDrop.GenerateDropList))]
        internal static class Roll
        {
            private sealed class State
            {
                internal string Creature, Character;
                internal long Peer;
                internal List<KeyValuePair<CharacterDrop.Drop, float>> Changed;
            }

            private static void Prefix(CharacterDrop __instance, out State __state)
            {
                __state = null;
                var dead = __instance.GetComponent<Character>();
                if (!dead || dead.IsPlayer()) return;
                var killer = Find(dead);
                if (killer == null) return;
                string character = null;
                Dictionary<string, int> dryKills = killer.Dry;
                if (killer.Local)
                {
                    character = CharacterKey();
                    if (character == null) return;
                    Load();
                    ImportFromTrophyAssister(character);
                    dryKills = Set(Dry, character);
                }
                string creature = Utils.GetPrefabName(dead.gameObject);
                int dry = dryKills.TryGetValue(creature, out int count) ? count : 0;
                __state = new State { Creature = creature, Character = character, Peer = killer.Peer, Changed = new List<KeyValuePair<CharacterDrop.Drop, float>>() };
                foreach (var drop in __instance.m_drops)
                {
                    if (drop == null || !IsTrophy(drop.m_prefab)) continue;
                    if (Plugin.TrophyOnlyUncollected.Value && (killer.Local ? Collected(drop.m_prefab.name) : killer.Collected.Contains(drop.m_prefab.name))) continue;
                    __state.Changed.Add(new KeyValuePair<CharacterDrop.Drop, float>(drop, drop.m_chance));
                    drop.m_chance = Boosted(drop.m_chance, dry);
                }
            }

            private static void Finalizer(State __state)
            {
                if (__state?.Changed == null) return;
                foreach (var change in __state.Changed) change.Key.m_chance = change.Value;
            }

            private static void Postfix(CharacterDrop __instance, List<KeyValuePair<GameObject, int>> __result, State __state)
            {
                if (__state?.Changed == null) return;
                var dead = __instance.GetComponent<Character>();
                int stars = dead ? Mathf.Max(0, dead.GetLevel() - 1) : 0;
                bool pity = __state.Changed.Count > 0;
                bool dropped = pity && __result != null && __result.Any(entry => IsTrophy(entry.Key));
                float baseChance = pity ? __state.Changed[0].Value : 0f;
                if (__state.Character != null)
                {
                    Record(__state.Character, __state.Creature, stars, pity, dropped, baseChance);
                    return;
                }
                if (ZRoutedRpc.instance == null || __state.Peer == 0L) return;
                var package = new ZPackage();
                package.Write(__state.Creature);
                package.Write(stars);
                package.Write(pity);
                package.Write(dropped);
                package.Write(baseChance);
                ZRoutedRpc.instance.InvokeRoutedRPC(__state.Peer, ResultRpc, package);
            }
        }

        // ---------------------------------------------------------------- the Trophies tab
        // Which creatures drop each trophy, and at what base chance.
        private static Dictionary<string, List<KeyValuePair<string, float>>> Sources()
        {
            if (sourcesByTrophy != null || ZNetScene.instance == null) return sourcesByTrophy;
            sourcesByTrophy = new Dictionary<string, List<KeyValuePair<string, float>>>();
            foreach (var prefab in ZNetScene.instance.m_prefabs)
            {
                if (!prefab || !prefab.TryGetComponent<CharacterDrop>(out var drops) || !prefab.GetComponent<Character>()) continue;
                foreach (var drop in drops.m_drops)
                    if (drop != null && IsTrophy(drop.m_prefab))
                        Set(sourcesByTrophy, drop.m_prefab.name).Add(new KeyValuePair<string, float>(prefab.name, drop.m_chance));
            }
            return sourcesByTrophy;
        }

        private static string ChanceLine(string trophy, string character)
        {
            var sources = Sources();
            if (sources == null || !sources.TryGetValue(trophy, out var list) || list.Count == 0) return null;
            var dryKills = Set(Dry, character);
            string bestCreature = null;
            float best = -1f;
            int bestDry = 0;
            foreach (var source in list)
            {
                dryKills.TryGetValue(source.Key, out int dry);
                float chance = Boosted(source.Value, dry);
                if (chance > best) { best = chance; bestCreature = source.Key; bestDry = dry; }
            }
            string color = best >= 0.5f ? "#7CFC00" : best >= 0.2f ? "#FFD700" : "#FFA500";
            string from = list.Count > 1 ? $" ({CreatureName(bestCreature)})" : "";
            return $"<color={color}>Next drop: {best * 100f:0.#}% - {bestDry} dry kill{(bestDry == 1 ? "" : "s")}{from}</color>";
        }

        private static string CreatureName(string prefab)
        {
            var character = ZNetScene.instance?.GetPrefab(prefab)?.GetComponent<Character>();
            return character ? Localization.instance.Localize(character.m_name) : prefab;
        }

        [HarmonyPatch(typeof(InventoryGui), "UpdateTrophyList")]
        internal static class TrophyTab
        {
            private static void Postfix(InventoryGui __instance)
            {
                try { Decorate(__instance); }
                catch (Exception e) { Plugin.Log.LogWarning("Trophy list: " + e.Message); }
            }
        }

        private static void Decorate(InventoryGui gui)
        {
            var player = Player.m_localPlayer;
            string character = CharacterKey();
            if (!player || character == null || ObjectDB.instance == null) return;
            Load();
            ImportFromTrophyAssister(character);
            var elements = TrophyList(gui);
            var collected = player.GetTrophies();
            // The game makes one element per collected trophy, in the same order, skipping unknown ones.
            if (Plugin.TrophyShowChances.Value)
            {
                int index = 0;
                foreach (var trophy in collected)
                {
                    if (!ObjectDB.instance.GetItemPrefab(trophy)) continue;
                    if (index >= elements.Count) break;
                    AddLine(elements[index++], ChanceLine(trophy, character));
                }
            }
            if (!Plugin.TrophyShowMissing.Value) return;
            var hidden = new HashSet<string>(Plugin.TrophyHidden.Value.Split(',').Select(name => name.Trim()), StringComparer.OrdinalIgnoreCase);
            var killed = Set(Killed, character);
            var sources = Sources();
            float lowest = 0f;
            foreach (var element in elements) if (element) lowest = Mathf.Min(lowest, element.GetComponent<RectTransform>().anchoredPosition.y - gui.m_trophieListSpace);
            foreach (var prefab in ObjectDB.instance.m_items)
            {
                if (!IsTrophy(prefab) || collected.Contains(prefab.name) || hidden.Contains(prefab.name)) continue;
                var item = prefab.GetComponent<ItemDrop>().m_itemData;
                var element = UnityEngine.Object.Instantiate(gui.m_trophieElementPrefab, gui.m_trophieListRoot);
                element.SetActive(true);
                var rect = element.GetComponent<RectTransform>();
                rect.anchoredPosition = new Vector2(item.m_shared.m_trophyPos.x * gui.m_trophieListSpace, item.m_shared.m_trophyPos.y * -gui.m_trophieListSpace);
                lowest = Mathf.Min(lowest, rect.anchoredPosition.y - gui.m_trophieListSpace);
                bool known = !Plugin.TrophySpoilerFree.Value ||
                    sources != null && sources.TryGetValue(prefab.name, out var list) && list.Any(source => killed.Contains(source.Key));
                var icon = element.transform.Find("icon_bkg/icon")?.GetComponent<Image>();
                if (icon)
                {
                    icon.sprite = item.GetIcon();
                    icon.color = known ? new Color(0.35f, 0.35f, 0.35f, 0.8f) : new Color(0f, 0f, 0f, 0.85f);
                }
                string name = Localization.instance.Localize(item.m_shared.m_name);
                if (name.EndsWith(" trophy", StringComparison.OrdinalIgnoreCase)) name = name.Remove(name.Length - " trophy".Length);
                var label = element.transform.Find("name")?.GetComponent<TMP_Text>();
                if (label) label.text = known ? $"<color=#888888>{name}</color>" : "<color=#888888>??????</color>";
                var description = element.transform.Find("description")?.GetComponent<TMP_Text>();
                if (description) description.text = known ? "<color=#888888>Not collected yet.</color>" : "<color=#888888>Not found yet.</color>";
                if (known && Plugin.TrophyShowChances.Value) AddLine(element, ChanceLine(prefab.name, character));
                elements.Add(element);
            }
            gui.m_trophieListRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(ListBaseSize(gui), -lowest));
        }

        private static void AddLine(GameObject element, string line)
        {
            if (!element || line == null) return;
            var description = element.transform.Find("description")?.GetComponent<TMP_Text>();
            if (description) description.text = line + "\n" + description.text;
        }
    }
}
