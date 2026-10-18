// Potato Tweaks - what would raise your comfort.
// Adapted from Koen-H's ComfortHelper (MIT License, https://github.com/Koen-H/valheim-mods).
//
// Comfort is 1, +1 under a roof, plus the comfort of the pieces within 10 m: only the best piece of each comfort
// group counts (one bed, one chair...), and each other piece once (SE_Rested.CalculateComfortLevel).
// 1. When you start resting, a message lists the pieces you know that would raise your comfort here.
// 2. The /comfort chat command (or comfort in the F5 console) shows the same, any time.
// 3. A Comfort tab in the build menu lists every comfort piece by comfort group, with check marks on what is
//    within 10 m, and comfort pieces say in their description what they would add here.
// Personal: only changes what you see.
using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PotatoTweaks
{
    internal static class ComfortHelper
    {
        internal const string OriginalModId = "com.koendertjh.comforthelper";

        internal static readonly Type[] Patches = { typeof(ComfortTabPatches), typeof(RestingMessage) };

        internal static void RegisterCommand()
        {
            new Terminal.ConsoleCommand("comfort", "Your comfort here and the pieces that would raise it (Potato Tweaks)", (Terminal.ConsoleEvent)ComfortCommand.Run);
        }

        // The best comfort level reached so far, as the Comfort is King achievement counts it.
        internal static float MaxComfort()
        {
            Achievement found = null;
            if (Achievements.m_instance != null)
                foreach (AchievementList list in Achievements.m_instance.m_achievementLists)
                    foreach (Achievement achievement in list.m_achievements)
                        if (achievement != null && achievement.m_id == "GrindComfort") found = achievement;
            PlayerProfile profile = Game.instance?.GetPlayerProfile();
            if (profile == null || found == null) return 0f;
            PlayerProfile.PlayerStats stats = profile.m_playerStats[(int)found.m_difficultyRequirement];
            return stats != null && stats.m_stats.TryGetValue(PlayerStatType.MaxComfort, out float value) ? value : 0f;
        }
    }

    internal class Suggestion
    {
        public Piece Piece;
        public int Gain;
        public Piece Replaces;
    }

    internal class ComfortReport
    {
        public bool InShelter;
        public int Current;
        public int BestKnown;
        public int Locked;
        public float MaxReached;
        public readonly List<Suggestion> Suggestions = new List<Suggestion>();
    }

    internal static class Comfort
    {
        private const float Radius = 10f;
        private static readonly List<Piece> s_nearby = new List<Piece>();
        private static List<Piece> s_comfortPrefabs;
        private static ZNetScene s_scene;

        // ComfortGroup names, so grouping doesn't turn the enum into a new string for every piece.
        private static readonly string[] GroupNames = System.Enum.GetNames(typeof(Piece.ComfortGroup));

        /// <summary>Pieces of one group don't stack; pieces without a group count once per kind.</summary>
        internal static string GroupOf(Piece piece) =>
            piece.m_comfortGroup != Piece.ComfortGroup.None ? GroupNames[(int)piece.m_comfortGroup] : piece.m_name;

        /// <summary>The piece that counts for each group within 10 m, as the game counts it.</summary>
        internal static Dictionary<string, Piece> BestNearby(Vector3 position)
        {
            var best = new Dictionary<string, Piece>();
            Nearby(position, best, null);
            return best;
        }

        /// <summary>
        /// One look around: the piece that counts for each group within 10 m, and the names of all comfort pieces
        /// there (counting or not). Either collection may be null.
        /// </summary>
        internal static void Nearby(Vector3 position, Dictionary<string, Piece> best, HashSet<string> names)
        {
            s_nearby.Clear();
            Piece.GetAllComfortPiecesInRadius(position, Radius, s_nearby);
            foreach (Piece piece in s_nearby)
            {
                names?.Add(piece.m_name);
                if (best == null)
                    continue;
                string group = GroupOf(piece);
                if (!best.TryGetValue(group, out Piece current) || piece.GetComfort() > current.GetComfort())
                    best[group] = piece;
            }
        }

        internal static ComfortReport Build(Player player)
        {
            var report = new ComfortReport
            {
                InShelter = player.InShelter(),
                Current = SE_Rested.CalculateComfortLevel(player),
                MaxReached = ComfortHelper.MaxComfort()
            };

            Dictionary<string, Piece> best = BestNearby(player.transform.position);

            // Best known and best overall piece of each group.
            var bestKnown = new Dictionary<string, Piece>();
            var bestAny = new Dictionary<string, int>();
            foreach (Piece prefab in ComfortPrefabs())
            {
                string group = GroupOf(prefab);
                if (!bestAny.TryGetValue(group, out int any) || prefab.m_comfort > any)
                    bestAny[group] = prefab.m_comfort;
                if (player.IsRecipeKnown(prefab.m_name) &&
                    (!bestKnown.TryGetValue(group, out Piece knownPiece) || prefab.m_comfort > knownPiece.m_comfort))
                    bestKnown[group] = prefab;
            }

            int known = 0;
            foreach (KeyValuePair<string, Piece> pair in bestKnown)
            {
                known += pair.Value.m_comfort;
                best.TryGetValue(pair.Key, out Piece here);
                int gain = pair.Value.m_comfort - (here != null ? here.GetComfort() : 0);
                if (gain > 0)
                    report.Suggestions.Add(new Suggestion { Piece = pair.Value, Gain = gain, Replaces = here });
            }
            report.BestKnown = 2 + known;
            report.Locked = bestAny.Sum(p => p.Value) - known;
            report.Suggestions.Sort((a, b) => b.Gain.CompareTo(a.Gain));
            return report;
        }

        private static List<Piece> ComfortPrefabs()
        {
            if (s_comfortPrefabs != null && s_scene == ZNetScene.instance)
                return s_comfortPrefabs;

            s_scene = ZNetScene.instance;
            s_comfortPrefabs = new List<Piece>();
            if (s_scene == null || ObjectDB.instance == null)
                return s_comfortPrefabs;

            // Only pieces a tool can build (hammer, cultivator...), not dungeon furniture.
            var names = new HashSet<string>();
            foreach (GameObject item in ObjectDB.instance.m_items)
            {
                PieceTable table = item != null ? item.GetComponent<ItemDrop>()?.m_itemData.m_shared.m_buildPieces : null;
                if (table == null)
                    continue;
                foreach (GameObject prefab in table.m_pieces)
                {
                    Piece piece = prefab != null ? prefab.GetComponent<Piece>() : null;
                    if (piece != null && piece.m_comfort > 0 && names.Add(piece.m_name))
                        s_comfortPrefabs.Add(piece);
                }
            }
            return s_comfortPrefabs;
        }

        internal static string Describe(Suggestion suggestion)
        {
            string name = Localization.instance.Localize(suggestion.Piece.m_name);
            return suggestion.Replaces == null
                ? $"+{suggestion.Gain} {name}"
                : $"+{suggestion.Gain} {name} (instead of {Localization.instance.Localize(suggestion.Replaces.m_name)})";
        }
    }

    internal static class ComfortGroups
    {
        internal static string Name(Piece.ComfortGroup group)
        {
            switch (group)
            {
                case Piece.ComfortGroup.Fire: return "Fires";
                case Piece.ComfortGroup.Bed: return "Beds";
                case Piece.ComfortGroup.Banner: return "Banners";
                case Piece.ComfortGroup.Chair: return "Seats";
                case Piece.ComfortGroup.Table: return "Tables";
                case Piece.ComfortGroup.Carpet: return "Rugs";
                case Piece.ComfortGroup.Display: return "Displays";
                case Piece.ComfortGroup.Decor: return "Decorations";
                case Piece.ComfortGroup.Garland: return "Garlands";
                case Piece.ComfortGroup.Lantern: return "Lanterns";
                case Piece.ComfortGroup.Leisure: return "Leisure";
                case Piece.ComfortGroup.None: return "Each counts";
                default: return group.ToString();
            }
        }
    }

    /// <summary>Build menu list with the comfort pieces of the tool, one category per comfort group.</summary>
    internal sealed class ComfortPieceList : IPieceList
    {
        private readonly Piece.ComfortGroup[] _groups = (Piece.ComfortGroup[])Enum.GetValues(typeof(Piece.ComfortGroup));
        private readonly List<int> _availableTags = new List<int>();

        public ComfortPieceList(string displayName) => DisplayName = displayName;

        public string DisplayName { get; }

        public bool ShowTags => true;

        public bool CanCustomizeTags => false;

        public int TagCount => _availableTags.Count;

        public int TagSeparatorIndex => -1;

        // "Seats +2" in green when a seat already counts here.
        public string GetTagDisplayName(int index)
        {
            Piece.ComfortGroup group = _groups[_availableTags[index]];
            string name = ComfortGroups.Name(group);
            int comfort = NearbyState.GroupComfort(group);
            return comfort > 0 ? $"<color=#7CFC7C>{name} +{comfort}</color>" : name;
        }

        public int GetTagIdByIndex(int index) => _availableTags[index];

        public void UpdateAvailableTags(PieceTable pieceTable)
        {
            var used = new HashSet<Piece.ComfortGroup>();
            foreach (Piece piece in pieceTable.m_availablePieces)
            {
                if (piece.m_comfort > 0)
                    used.Add(piece.m_comfortGroup);
            }

            _availableTags.Clear();
            for (int i = 0; i < _groups.Length; i++)
            {
                // "Each counts" last: the stacking groups are what this tab is about.
                if (_groups[i] != Piece.ComfortGroup.None && used.Contains(_groups[i]))
                    _availableTags.Add(i);
            }
            int none = Array.IndexOf(_groups, Piece.ComfortGroup.None);
            if (used.Contains(Piece.ComfortGroup.None))
                _availableTags.Add(none);
        }

        public void GetAvailablePiecesWithTag(int tagId, PieceTable pieceTable, IList<Piece> resultOut)
        {
            IEnumerable<Piece> pieces = pieceTable.m_availablePieces.Where(p =>
                p.m_comfort > 0 && (tagId < 0 || tagId >= _groups.Length || p.m_comfortGroup == _groups[tagId]));
            // Best first: that is the one that counts.
            foreach (Piece piece in pieces.OrderByDescending(p => p.m_comfort))
                resultOut.Add(piece);
        }
    }

    /// <summary>What is within 10 m of the player, refreshed about once a second.</summary>
    internal static class NearbyState
    {
        private static Dictionary<string, Piece> s_best = new Dictionary<string, Piece>();
        private static HashSet<string> s_names = new HashSet<string>();
        private static Dictionary<string, Piece> s_nextBest = new Dictionary<string, Piece>();
        private static HashSet<string> s_nextNames = new HashSet<string>();
        private static float s_nextUpdate;

        /// <summary>Goes up whenever what's nearby changes; caches built from this state compare against it.</summary>
        internal static int Version { get; private set; }

        /// <summary>True when what's nearby changed since the last update.</summary>
        internal static bool Update(Player player, bool force = false)
        {
            if (!force && Time.time < s_nextUpdate)
                return false;
            s_nextUpdate = Time.time + 1f;

            // Fill the spare collections, compare, then swap: no new collections every second.
            s_nextBest.Clear();
            s_nextNames.Clear();
            Comfort.Nearby(player.transform.position, s_nextBest, s_nextNames);
            bool changed = !s_nextNames.SetEquals(s_names) || !SameCounting(s_nextBest, s_best);
            (s_best, s_nextBest) = (s_nextBest, s_best);
            (s_names, s_nextNames) = (s_nextNames, s_names);
            if (changed)
                Version++;
            return changed;
        }

        private static bool SameCounting(Dictionary<string, Piece> a, Dictionary<string, Piece> b)
        {
            if (a.Count != b.Count)
                return false;
            foreach (KeyValuePair<string, Piece> pair in a)
            {
                if (!b.TryGetValue(pair.Key, out Piece other) || other.m_name != pair.Value.m_name)
                    return false;
            }
            return true;
        }

        internal static bool IsHere(Piece piece) => s_names.Contains(piece.m_name);

        internal static bool Counts(Piece piece) =>
            s_best.TryGetValue(Comfort.GroupOf(piece), out Piece best) && best.m_name == piece.m_name;

        internal static Piece Counting(Piece piece) =>
            s_best.TryGetValue(Comfort.GroupOf(piece), out Piece best) ? best : null;

        internal static int GroupComfort(Piece.ComfortGroup group)
        {
            if (group == Piece.ComfortGroup.None)
                return s_best.Values.Where(p => p.m_comfortGroup == Piece.ComfortGroup.None).Sum(p => p.GetComfort());
            return s_best.TryGetValue(group.ToString(), out Piece best) ? best.GetComfort() : 0;
        }
    }

    [HarmonyPatch]
    internal static class ComfortTabPatches
    {
        private const string CheckName = "PotatoTweaks_ComfortCheck";
        private static readonly Color Green = new Color(0.45f, 0.95f, 0.45f);
        private static readonly Color Grey = new Color(0.62f, 0.62f, 0.62f);

        private static readonly AccessTools.FieldRef<BuildUi, RectTransform> TabContainerRef =
            AccessTools.FieldRefAccess<BuildUi, RectTransform>("m_tabContainer");
        private static readonly AccessTools.FieldRef<BuildUi, TabHandler> TabHandlerRef =
            AccessTools.FieldRefAccess<BuildUi, TabHandler>("m_tabHandler");
        private static readonly AccessTools.FieldRef<BuildUi, List<IPieceList>> PieceListsRef =
            AccessTools.FieldRefAccess<BuildUi, List<IPieceList>>("m_pieceLists");
        private static readonly AccessTools.FieldRef<BuildUi, int> AvailablePieceCountRef =
            AccessTools.FieldRefAccess<BuildUi, int>("m_availablePieceCount");
        private static readonly AccessTools.FieldRef<BuildUi, int> CurrentPieceListRef =
            AccessTools.FieldRefAccess<BuildUi, int>("m_currentPieceList");
        private static readonly AccessTools.FieldRef<BuildUi, List<BuildUiPieceButton>> PieceButtonsRef =
            AccessTools.FieldRefAccess<BuildUi, List<BuildUiPieceButton>>("m_pieceButtons");

        // ---- the tab ----

        [HarmonyPrefix]
        [HarmonyPatch(typeof(BuildUi), "Awake")]
        private static void BeforeAwake(BuildUi __instance, out bool __state)
        {
            __state = false;
            if (!Plugin.ComfortTab.Value)
                return;

            RectTransform container = TabContainerRef(__instance);
            TabHandler tabHandler = TabHandlerRef(__instance);
            // Every tab must map 1:1 to a piece list; bail out if a game update changes that.
            if (container == null || tabHandler == null || container.childCount < 2 ||
                tabHandler.m_tabs.Count != container.childCount)
            {
                Plugin.Log.LogWarning("Build menu tabs have an unexpected layout; the Comfort tab is disabled.");
                return;
            }

            RectTransform last = (RectTransform)container.GetChild(container.childCount - 1);
            RectTransform previous = (RectTransform)container.GetChild(container.childCount - 2);
            RectTransform tab = UnityEngine.Object.Instantiate(last, container);
            tab.name = "PotatoTweaks_ComfortTab";

            // Without a layout group the tabs are placed by hand; continue the row with the same spacing.
            if (container.GetComponent<LayoutGroup>() == null)
                tab.anchoredPosition = last.anchoredPosition + (last.anchoredPosition - previous.anchoredPosition);

            foreach (TMP_Text text in tab.GetComponentsInChildren<TMP_Text>(true))
                text.text = Plugin.ComfortTabName.Value;

            Button button = tab.GetComponent<Button>();
            button.onClick = new Button.ButtonClickedEvent();
            tabHandler.m_tabs.Add(new TabHandler.Tab { m_button = button });
            __state = true;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(BuildUi), "Awake")]
        private static void AfterAwake(BuildUi __instance, bool __state)
        {
            if (__state)
                PieceListsRef(__instance).Add(new ComfortPieceList(Plugin.ComfortTabName.Value));
        }

        private static bool ComfortTabShown(BuildUi buildUi)
        {
            List<IPieceList> lists = PieceListsRef(buildUi);
            int index = CurrentPieceListRef(buildUi);
            return index >= 0 && index < lists.Count && lists[index] is ComfortPieceList;
        }

        // ---- check marks ----

        // Runs whenever the visible pieces change. Buttons are pooled and shared by all tabs, so every button
        // gets its mark set or cleared.
        [HarmonyPostfix]
        [HarmonyPatch(typeof(BuildUi), "UpdatePieceButtons")]
        private static void AfterUpdatePieceButtons(BuildUi __instance)
        {
            Player player = Player.m_localPlayer;
            bool show = player != null && (ComfortTabShown(__instance) || Plugin.ComfortChecksEverywhere.Value);
            if (show)
                NearbyState.Update(player, force: true);

            foreach (BuildUiPieceButton button in PieceButtonsRef(__instance))
            {
                Piece piece = button.Piece;
                bool here = show && piece != null && piece.m_comfort > 0 && NearbyState.IsHere(piece);
                Transform check = button.transform.Find(CheckName);
                if (check == null)
                {
                    if (!here)
                        continue;
                    check = CreateCheck(button.transform);
                }
                check.gameObject.SetActive(here);
                if (here)
                {
                    Color color = NearbyState.Counts(piece) ? Green : Grey;
                    foreach (Image bar in check.GetComponentsInChildren<Image>(true))
                        bar.color = color;
                }
            }
        }

        // Keeps the marks right while you walk around or build, with the Comfort tab open.
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Hud), "Update")]
        private static void AfterHudUpdate()
        {
            BuildUi buildUi = Hud.instance != null ? Hud.instance.m_buildUi : null;
            Player player = Player.m_localPlayer;
            if (buildUi == null || player == null || !buildUi.isActiveAndEnabled)
                return;
            if (!ComfortTabShown(buildUi) && !Plugin.ComfortChecksEverywhere.Value)
                return;
            if (NearbyState.Update(player))
                AvailablePieceCountRef(buildUi) = -1;
        }

        // A check mark from two bars, so it doesn't depend on the font having a check glyph.
        private static Transform CreateCheck(Transform button)
        {
            var go = new GameObject(CheckName, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(button, false);
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-3f, -3f);
            rt.sizeDelta = new Vector2(18f, 16f);
            rt.SetAsLastSibling();

            Bar(rt, new Vector2(5f, 6f), new Vector2(9f, 4f), -45f);
            Bar(rt, new Vector2(11f, 8f), new Vector2(15f, 4f), 50f);
            go.SetActive(false);
            return rt;
        }

        private static void Bar(RectTransform parent, Vector2 center, Vector2 size, float angle)
        {
            var go = new GameObject("bar", typeof(RectTransform), typeof(Image), typeof(Outline));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = center;
            rt.sizeDelta = size;
            rt.localRotation = Quaternion.Euler(0f, 0f, angle);
            go.GetComponent<Image>().raycastTarget = false;
            Outline outline = go.GetComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(1f, -1f);
        }

        // ---- hover text ----

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Hud), "SetupPieceInfo")]
        private static void AfterSetupPieceInfo(Hud __instance, Piece piece)
        {
            Player player = Player.m_localPlayer;
            if (!Plugin.ComfortHoverInfo.Value || piece == null || piece.m_comfort <= 0 || player == null)
                return;

            // Runs every frame in build mode: the text is built once per piece and nearby state.
            NearbyState.Update(player);
            if (s_hoverVersion != NearbyState.Version)
            {
                s_hoverTexts.Clear();
                s_hoverVersion = NearbyState.Version;
            }
            if (!s_hoverTexts.TryGetValue(piece.m_name, out string text))
                s_hoverTexts[piece.m_name] = text = HoverText(piece);
            __instance.m_pieceDescription.text += text;
        }

        // Piece name -> its comfort lines, for the current nearby state.
        private static readonly Dictionary<string, string> s_hoverTexts = new Dictionary<string, string>();
        private static int s_hoverVersion = -1;

        private static string HoverText(Piece piece)
        {
            Localization loc = Localization.instance;
            string text = $"\n\n<color=#ffb347>Comfort +{piece.m_comfort}</color> ";
            text += piece.m_comfortGroup == Piece.ComfortGroup.None
                ? "(counts once, next to everything else)"
                : $"({ComfortGroups.Name(piece.m_comfortGroup)}: only the best one nearby counts)";

            Piece counting = NearbyState.Counting(piece);
            if (counting == null)
                text += $"\nNothing like it within 10 m: <color=#7CFC7C>+{piece.m_comfort} here</color>.";
            else if (counting.m_name == piece.m_name)
                text += "\n<color=#7CFC7C>Already counts here.</color>";
            else if (piece.m_comfort > counting.GetComfort())
                text += $"\n{loc.Localize(counting.m_name)} counts here now: <color=#7CFC7C>+{piece.m_comfort - counting.GetComfort()} more</color> with this one.";
            else
                text += $"\n{loc.Localize(counting.m_name)} already counts here; this one adds nothing.";
            return text;
        }
    }

    [HarmonyPatch(typeof(Player), "UpdateEnvStatusEffects")]
    internal static class RestingMessage
    {
        private static bool s_wasResting;
        private static float s_lastShown = -10000f;

        private static void Postfix(Player __instance)
        {
            if (__instance != Player.m_localPlayer)
                return;

            bool resting = __instance.GetSEMan().HaveStatusEffect(SEMan.s_statusEffectResting);
            bool started = resting && !s_wasResting;
            s_wasResting = resting;
            if (!started || !Plugin.ComfortRestingMessage.Value ||
                Time.time - s_lastShown < Plugin.ComfortMessageCooldown.Value)
                return;

            s_lastShown = Time.time;
            ComfortReport report = Comfort.Build(__instance);
            string text = $"Comfort {report.Current} (best so far {report.MaxReached:0}, Comfort is King needs 20)";
            if (report.Suggestions.Count > 0)
            {
                text += "\nTo raise it: " + string.Join(", ",
                    report.Suggestions.Take(Plugin.ComfortSuggestions.Value).Select(Comfort.Describe));
                if (report.Suggestions.Count > Plugin.ComfortSuggestions.Value)
                    text += ", ... (/comfort lists all)";
            }
            __instance.Message(MessageHud.MessageType.TopLeft, text);
        }
    }

    internal static class ComfortCommand
    {
        internal static void Run(Terminal.ConsoleEventArgs args)
        {
            Player player = Player.m_localPlayer;
            if (player == null)
            {
                Say(args, "Load into a world with a character first.");
                return;
            }

            ComfortReport report = Comfort.Build(player);
            Say(args, $"Comfort here {report.Current}, best so far {report.MaxReached:0} " +
                      "(Comfort is King needs 20).");
            if (!report.InShelter)
                Say(args, "  You are not under a roof: pieces only count in a shelter.");
            Say(args, $"  With the pieces you know, the most you can reach is {report.BestKnown}." +
                      (report.Locked > 0 ? $" Pieces you haven't unlocked yet add up to {report.Locked} more." : ""));

            if (report.Suggestions.Count == 0)
            {
                Say(args, "  Nothing you know would raise it here.");
                return;
            }
            Say(args, "  Within 10 m, add:");
            foreach (Suggestion suggestion in report.Suggestions)
                Say(args, "    " + Comfort.Describe(suggestion));
        }

        private static void Say(Terminal.ConsoleEventArgs args, string text)
        {
            if (args?.Context != null) args.Context.AddString(text);
            else Plugin.Log.LogInfo(text);
        }
    }
}
