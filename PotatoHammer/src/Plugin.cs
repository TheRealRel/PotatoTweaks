// Potato Hammer - every mod's build pieces on a tab of their own, written from scratch.
using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Jotunn.Managers;
using Jotunn.Utils;
using UnityEngine;

namespace PotatoHammer
{
    // Mods put their build pieces on the hammer wherever they like: in several of the game's tabs, in no tab, or in the
    // wrong one. This moves each mod's hammer pieces to a tab named after the mod and leaves the game's own pieces where
    // they are. Jotunn tells which mod added each piece. Only the hammer is sorted; mods with their own hammer, the hoe,
    // the cultivator and the serving tray are left alone. It only changes your own build menu.
    [BepInPlugin(Id, "Potato Hammer", Version)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    [NetworkCompatibility(CompatibilityLevel.NotEnforced, VersionStrictness.None)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Id = "potato.hammer", Version = "1.0.1";
        private const string TabsSection = "2 - Tabs";

        internal static ManualLogSource Log;
        private static Plugin instance;
        private static ConfigEntry<bool> sortOn;
        private static ConfigEntry<string> leaveAlone, extraPieces;
        private static readonly Dictionary<string, ConfigEntry<string>> TabNames = new Dictionary<string, ConfigEntry<string>>();
        // Each piece's own tab, from before it was first moved, so switching a mod off puts its pieces back.
        private static readonly Dictionary<Piece, Piece.PieceCategory> Original = new Dictionary<Piece, Piece.PieceCategory>();
        private static readonly Dictionary<Piece, Piece.UsageTagFlags> OriginalUsage = new Dictionary<Piece, Piece.UsageTagFlags>();
        private static string lastSummary;

        private void Awake()
        {
            instance = this;
            Log = Logger;
            sortOn = Config.Bind("1 - General", "Sort the hammer", true,
                "Move each mod's hammer pieces to a tab named after the mod. The game's own pieces stay where they are. Takes effect the next time you take out the hammer.");
            leaveAlone = Config.Bind("1 - General", "Mods to leave alone", "",
                "Mods whose pieces stay where the mod put them, separated by commas (mod name as in the tab list below, or its id).");
            extraPieces = Config.Bind("1 - General", "Extra pieces", "",
                "Pieces whose mod can't be told (they stay where they are; the log lists the hammer's pieces by tab), put on a tab by name: piece=Tab, separated by commas. Example: piece_mytable=My Mod.");
            // Jotunn's record of which mod added each prefab, kept only when a mod asks for it.
            ModQuery.Enable();
            new Harmony(Id).PatchAll(typeof(Plugin).Assembly);
        }

        private static PieceTable Hammer()
        {
            var hammer = ObjectDB.instance ? ObjectDB.instance.GetItemPrefab("Hammer") : null;
            return hammer ? hammer.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_buildPieces : null;
        }

        // The mod that added a piece, or null for the game's own pieces (and pieces whose mod can't be told).
        private static BepInPlugin SourceOf(GameObject prefab, Dictionary<string, BepInPlugin> jotunnPieces)
        {
            try
            {
                var mod = ModQuery.GetPrefab(prefab.name)?.SourceMod;
                if (mod != null) return mod;
            }
            catch (Exception) { }
            return jotunnPieces.TryGetValue(prefab.name, out var owner) ? owner : null;
        }

        private static string Clean(string name)
        {
            var chars = name.Where(c => c != '=' && c != '\n' && c != '\t' && c != '\\' && c != '"' && c != '\'' && c != '[' && c != ']').ToArray();
            return new string(chars).Trim();
        }

        private static string TabFor(BepInPlugin mod)
        {
            string key = Clean(mod.Name);
            if (key.Length == 0) key = Clean(mod.GUID);
            if (!TabNames.TryGetValue(key, out var entry))
            {
                entry = instance.Config.Bind(TabsSection, key, Spaced(key),
                    $"Tab for {mod.Name}'s hammer pieces ({mod.GUID}). Mods with the same tab name share it; empty = leave its pieces where the mod put them.");
                TabNames[key] = entry;
            }
            var skip = leaveAlone.Value.Split(',').Select(part => part.Trim()).Where(part => part.Length > 0);
            if (skip.Any(part => part.Equals(key, StringComparison.OrdinalIgnoreCase) || part.Equals(mod.GUID, StringComparison.OrdinalIgnoreCase))) return null;
            string tab = entry.Value.Trim();
            // Left at the mod's own name (the first version's default): shown with spaces, "Boat Additions".
            if (tab == key) tab = Spaced(key);
            return tab.Length > 0 ? tab : null;
        }

        // "BoatAdditions" -> "Boat Additions"; names that already have spaces stay as they are.
        private static string Spaced(string name)
        {
            if (name.Contains(" ")) return name;
            var text = new System.Text.StringBuilder();
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (c == '_' || c == '-') { text.Append(' '); continue; }
                if (i > 0 && char.IsUpper(c) && (char.IsLower(name[i - 1]) || (i + 1 < name.Length && char.IsLower(name[i + 1]) && char.IsUpper(name[i - 1]))))
                    text.Append(' ');
                text.Append(c);
            }
            return text.ToString().Trim();
        }

        // A build menu tab Jotunn lists. If another mod already made a category with that name, Jotunn reuses it
        // without listing it, so the tab gets a name of its own that looks the same (an invisible character added).
        private static Piece.PieceCategory Category(string tab)
        {
            var category = PieceManager.Instance.AddPieceCategory(tab);
            if (Listed(tab)) return category;
            var own = PieceManager.Instance.AddPieceCategory(tab + "\u200B");
            return Listed(tab + "\u200B") ? own : category;
        }

        private static bool Listed(string name)
        {
            try
            {
                var names = Traverse.Create(PieceManager.Instance).Field("PieceCategories").GetValue() as Dictionary<string, Piece.PieceCategory>;
                return names == null || names.ContainsKey(name);
            }
            catch (Exception) { return true; }
        }

        private static Dictionary<string, string> Extras()
        {
            var extras = new Dictionary<string, string>();
            foreach (var part in extraPieces.Value.Split(','))
            {
                int split = part.IndexOf('=');
                if (split <= 0) continue;
                string piece = part.Substring(0, split).Trim(), tab = part.Substring(split + 1).Trim();
                if (piece.Length > 0 && tab.Length > 0) extras[piece] = tab;
            }
            return extras;
        }

        internal static void Sort(PieceTable table)
        {
            var jotunnPieces = new Dictionary<string, BepInPlugin>();
            try
            {
                foreach (var custom in ModRegistry.GetPieces())
                    if (custom?.PiecePrefab && custom.SourceMod != null) jotunnPieces[custom.PiecePrefab.name] = custom.SourceMod;
            }
            catch (Exception) { }
            var extras = Extras();
            var summary = new SortedDictionary<string, int>();
            var unknown = new List<string>();
            foreach (var prefab in table.m_pieces)
            {
                var piece = prefab ? prefab.GetComponent<Piece>() : null;
                if (!piece) continue;
                if (!Original.TryGetValue(piece, out var own)) Original[piece] = own = piece.m_category;
                if (!OriginalUsage.TryGetValue(piece, out var ownUsage)) OriginalUsage[piece] = ownUsage = piece.m_usage;
                string tab = null;
                if (sortOn.Value)
                {
                    if (extras.TryGetValue(prefab.name, out var extra)) tab = extra;
                    else
                    {
                        var mod = SourceOf(prefab, jotunnPieces);
                        if (mod == null) unknown.Add(prefab.name);
                        else if (mod.GUID != Jotunn.Main.ModGuid) tab = TabFor(mod);
                    }
                }
                if (tab == null)
                {
                    piece.m_category = own;
                    piece.m_usage = ownUsage;
                    continue;
                }
                // The build menu's categories list a piece under every tag it carries. Mods tag their pieces with the
                // game's categories (Walls, Building...) and with tags of their own, and mods built on the same piece
                // library all give their own tag the same number, so their pieces showed up under each other's names
                // (CollectorSeries, BoatAdditions, SimpleElevators). A moved piece keeps only its mod's tab.
                piece.m_category = Category(tab);
                piece.m_usage = 0;
                summary.TryGetValue(tab, out int inTab);
                summary[tab] = inTab + 1;
            }
            string text = summary.Count == 0 ? "no mod pieces moved" : string.Join(", ", summary.Select(pair => $"{pair.Key} ({pair.Value})"));
            if (text != lastSummary)
            {
                lastSummary = text;
                Log.LogInfo("Hammer tabs: " + text);
                Log.LogInfo("Pieces from the game or from a mod that can't be told, left where they were (use \"Extra pieces\" for a mod's): " + string.Join(", ", unknown));
            }
        }

        // Before the build menu is set up for the hammer; Jotunn then makes the tabs.
        [HarmonyPatch(typeof(Player), "SetPlaceMode")]
        internal static class TakeOutHammer
        {
            private static void Prefix(PieceTable buildPieces)
            {
                if (!buildPieces || buildPieces != Hammer()) return;
                try { Sort(buildPieces); }
                catch (Exception e) { Log.LogWarning("Couldn't sort the hammer: " + e); }
            }
        }
    }
}
