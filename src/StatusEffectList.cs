// Potato Tweaks - a compact status effect list under the minimap.
// Written from scratch for this mod; the idea is from RandyKnapp's Minimal Status Effects.
using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace PotatoTweaks
{
    // The game shows status effects as a row of icons beside the minimap, with the names under them. This turns them into
    // a list under the minimap, one per line: icon, name, then the time left. It follows the minimap's actual size and
    // position, so it fits any resolution and GUI scale, and sits in the top-right corner when the world has no map.
    // While sailing, the wind and speed indicators move aside just enough to stay clear of it. Personal.
    [HarmonyPatch(typeof(Hud), "UpdateStatusEffects")]
    internal static class StatusEffectList
    {
        private const float Gap = 8f, TimeWidth = 70f, MinWidth = 240f;
        private static readonly AccessTools.FieldRef<Hud, List<RectTransform>> Entries = AccessTools.FieldRefAccess<Hud, List<RectTransform>>("m_statusEffects");
        private static readonly Vector3[] Corners = new Vector3[4];

        private struct Home
        {
            internal bool Saved;
            internal Vector2 AnchorMin, AnchorMax, Pivot, Position, Size;
        }

        private static Hud shownOn;
        private static Home rootHome;
        private static RectTransform windPiece, speedPiece;
        private static Vector3 windHome, speedHome;
        private static bool applied;
        private static int lastLayout;
        private static RectTransform firstIcon;
        private static Vector2 iconSize;

        private static void Postfix(Hud __instance)
        {
            var entries = Entries(__instance);
            var root = __instance.m_statusEffectListRoot;
            if (!root || entries == null) return;
            if (!Plugin.StatusEffectListEnabled.Value)
            {
                if (applied) Restore(__instance, root, entries);
                return;
            }
            if (shownOn != __instance)
            {
                // A new HUD (another world, a relog): start over.
                shownOn = __instance;
                rootHome = default;
                lastLayout = 0;
            }
            if (!rootHome.Saved) rootHome = Save(root);
            applied = true;
            int layout = Fingerprint(__instance, entries);
            // The game only places entries when it creates them; this also notices anything else moving them.
            bool disturbed = entries.Count > 0 && (!firstIcon || firstIcon.sizeDelta != iconSize || firstIcon.anchoredPosition != Vector2.zero);
            if (layout == lastLayout && !disturbed) return;
            lastLayout = layout;
            Layout(__instance, root, entries);
        }

        // Everything the layout depends on. The game rebuilds the entries when their number changes.
        private static int Fingerprint(Hud hud, List<RectTransform> entries)
        {
            unchecked
            {
                int hash = entries.Count * 31 + (entries.Count > 0 && entries[0] ? entries[0].GetInstanceID() : 0);
                hash = hash * 31 + Screen.width;
                hash = hash * 31 + Screen.height;
                hash = hash * 31 + (Game.m_noMap ? 1 : 0);
                hash = hash * 31 + (hud.m_shipHudRoot && hud.m_shipHudRoot.activeSelf ? 1 : 0);
                hash = hash * 31 + Plugin.StatusEffectIconSize.Value.GetHashCode();
                hash = hash * 31 + Plugin.StatusEffectTextSize.Value;
                hash = hash * 31 + Plugin.StatusEffectSpacing.Value.GetHashCode();
                if (MapBottomLeft(out Vector3 corner, out _)) hash = hash * 31 + corner.GetHashCode();
                return hash;
            }
        }

        // The small map's lower-left corner and width, frame included, in world space.
        private static bool MapBottomLeft(out Vector3 corner, out float width)
        {
            corner = default;
            width = 0f;
            var map = Minimap.instance;
            if (Game.m_noMap || !map || !map.m_mapImageSmall) return false;
            var image = map.m_mapImageSmall.rectTransform;
            var frame = image.parent as RectTransform;
            var area = frame && frame.rect.width < image.rect.width * 1.5f && frame.rect.height < image.rect.height * 1.5f ? frame : image;
            area.GetWorldCorners(Corners);
            corner = Corners[0];
            width = Corners[3].x - Corners[0].x;
            return width > 0f;
        }

        private static void Layout(Hud hud, RectTransform root, List<RectTransform> entries)
        {
            float icon = Mathf.Clamp(Plugin.StatusEffectIconSize.Value, 16f, 64f);
            float spacing = Mathf.Max(Plugin.StatusEffectSpacing.Value, icon + 2f);
            int font = Mathf.Clamp(Plugin.StatusEffectTextSize.Value, 10, 40);
            iconSize = new Vector2(icon, icon);

            var parent = root.parent as RectTransform;
            if (!parent) return;
            Rect area = parent.rect;
            float width = MinWidth;
            Vector2 topLeft;
            if (MapBottomLeft(out Vector3 corner, out float mapWidth))
            {
                Vector3 local = parent.InverseTransformPoint(corner);
                float scale = parent.lossyScale.x > 0f ? parent.lossyScale.x : 1f;
                width = Mathf.Max(MinWidth, mapWidth / scale);
                topLeft = new Vector2(local.x, local.y - Gap);
            }
            else topLeft = new Vector2(area.xMax - MinWidth - 2f * Gap, area.yMax - 2f * Gap);
            topLeft.x = Mathf.Clamp(topLeft.x, area.xMin + Gap, area.xMax - width - Gap);

            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0f, 1f);
            root.sizeDelta = new Vector2(width, Mathf.Max(1f, entries.Count * spacing));
            root.localPosition = new Vector3(topLeft.x, topLeft.y, 0f);

            firstIcon = null;
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (!entry) continue;
                Place(entry, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(width, spacing), new Vector2(0f, -i * spacing));
                var iconRect = entry.Find("Icon") as RectTransform;
                if (iconRect)
                {
                    Place(iconRect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), iconSize, Vector2.zero);
                    if (i == 0) firstIcon = iconRect;
                }
                if (entry.Find("Cooldown") is RectTransform cooldown)
                    Place(cooldown, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(16f, 16f), new Vector2(icon - 4f, 4f - icon * 0.5f));
                var time = entry.Find("TimeText") as RectTransform;
                if (time)
                {
                    Place(time, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(TimeWidth, icon), Vector2.zero);
                    Text(time, font, TextAlignmentOptions.MidlineRight);
                }
                var name = entry.Find("Name") as RectTransform;
                if (name)
                {
                    name.anchorMin = new Vector2(0f, 0.5f);
                    name.anchorMax = new Vector2(1f, 0.5f);
                    name.pivot = new Vector2(0f, 0.5f);
                    name.offsetMin = new Vector2(icon + Gap, -icon * 0.5f);
                    name.offsetMax = new Vector2(-TimeWidth - 4f, icon * 0.5f);
                    Text(name, font, TextAlignmentOptions.MidlineLeft);
                }
            }
            Ship(hud, root, entries.Count);
        }

        private static void Place(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 size, Vector2 position)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }

        private static void Text(RectTransform rect, int size, TextAlignmentOptions alignment)
        {
            var text = rect.GetComponent<TMP_Text>();
            if (!text) return;
            text.enableAutoSizing = false;
            text.fontSize = size;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
        }

        // While sailing, the wind and speed indicators go back to where the game put them, then move left only if the
        // list would cover them.
        private static void Ship(Hud hud, RectTransform root, int count)
        {
            var wind = hud.m_shipWindIndicatorRoot;
            var speed = hud.m_rudder ? hud.m_rudder.transform.parent as RectTransform : null;
            if (wind && wind != windPiece) { windPiece = wind; windHome = wind.localPosition; }
            if (speed && speed != speedPiece) { speedPiece = speed; speedHome = speed.localPosition; }
            if (windPiece) windPiece.localPosition = windHome;
            if (speedPiece) speedPiece.localPosition = speedHome;
            if (count == 0 || !hud.m_shipHudRoot || !hud.m_shipHudRoot.activeSelf) return;
            Rect list = WorldRect(root);
            float gap = Gap * (root.lossyScale.x > 0f ? root.lossyScale.x : 1f);
            if (windPiece) Clear(windPiece, list, gap);
            if (speedPiece) Clear(speedPiece, list, gap);
        }

        private static void Clear(RectTransform piece, Rect list, float gap)
        {
            Rect rect = WorldRect(piece);
            if (!rect.Overlaps(list)) return;
            piece.position += new Vector3(list.xMin - gap - rect.xMax, 0f, 0f);
        }

        // The wind indicator turns with the ship, so its box is taken around all four corners.
        private static Rect WorldRect(RectTransform rect)
        {
            rect.GetWorldCorners(Corners);
            float xMin = Corners[0].x, xMax = xMin, yMin = Corners[0].y, yMax = yMin;
            for (int i = 1; i < 4; i++)
            {
                xMin = Mathf.Min(xMin, Corners[i].x);
                xMax = Mathf.Max(xMax, Corners[i].x);
                yMin = Mathf.Min(yMin, Corners[i].y);
                yMax = Mathf.Max(yMax, Corners[i].y);
            }
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        private static Home Save(RectTransform rect) => new Home
        {
            Saved = true, AnchorMin = rect.anchorMin, AnchorMax = rect.anchorMax, Pivot = rect.pivot, Position = rect.anchoredPosition, Size = rect.sizeDelta,
        };

        // Switched off: the list goes back where the game had it, and the game rebuilds its entries from its own template.
        private static void Restore(Hud hud, RectTransform root, List<RectTransform> entries)
        {
            applied = false;
            lastLayout = 0;
            firstIcon = null;
            if (rootHome.Saved)
            {
                root.anchorMin = rootHome.AnchorMin;
                root.anchorMax = rootHome.AnchorMax;
                root.pivot = rootHome.Pivot;
                root.sizeDelta = rootHome.Size;
                root.anchoredPosition = rootHome.Position;
            }
            if (windPiece) windPiece.localPosition = windHome;
            if (speedPiece) speedPiece.localPosition = speedHome;
            foreach (var entry in entries)
                if (entry) Object.Destroy(entry.gameObject);
            entries.Clear();
        }
    }
}
