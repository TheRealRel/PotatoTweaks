// Potato Tweaks - Inventory Slots Visibility: no inventory slot stays hidden. Written for this mod.
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace PotatoTweaks
{
    // Several mods add slots to the inventory screen (Equipment & Quick Slots' equipment, quick and quiver slots, Adventure
    // Backpacks' backpack in the chest window, more rows from Haldor) and each places its windows on its own. On some
    // screens, or with a taller inventory, a window can end up over another's slots or partly off the screen. When the
    // inventory's layout changes (opening it, opening a chest or backpack, a new row, a new resolution or GUI scale), look
    // where every slot of your inventory and the open chest really is on screen; if one is off screen or under another
    // window, move that window just far enough, staying on screen, and put it back once it isn't needed. Works in the
    // game UI's own screen pixels, so it follows any resolution, aspect ratio (16:9 to 32:9) and GUI scale.
    //
    // Kept light: with the inventory closed it does nothing but one visibility check per frame. While it is open it reads a
    // small layout fingerprint (screen size, GUI scale, the screen's windows and slot counts) four times a second, without
    // allocating, and only when that changes does it look at the slots themselves.
    [HarmonyPatch(typeof(InventoryGui), "Update")]
    internal static class InventorySlotsVisibility
    {
        private const float Interval = 0.25f, Margin = 4f;
        private static readonly AccessTools.FieldRef<InventoryGrid, List<InventoryElement>> Elements =
            AccessTools.FieldRefAccess<InventoryGrid, List<InventoryElement>>("m_elements");
        private static readonly AccessTools.FieldRef<InventoryGui, GameObject> DragItem = AccessTools.FieldRefAccess<InventoryGui, GameObject>("m_dragGo");

        // Where each moved window was before we moved it, and where we put it (so a move by its own mod is respected).
        private sealed class Moved
        {
            internal Vector2 Home, Placed;
        }

        private static readonly ConditionalWeakTable<RectTransform, Moved> MovedWindows = new ConditionalWeakTable<RectTransform, Moved>();
        private static readonly List<RectTransform> Touched = new List<RectTransform>();
        private static readonly List<RectTransform> Scratch = new List<RectTransform>();
        private static readonly List<RectTransform> Windows = new List<RectTransform>();
        private static readonly Dictionary<(RectTransform holder, RectTransform cover), Rect> Hidden = new Dictionary<(RectTransform, RectTransform), Rect>();
        private static readonly Vector3[] Corners = new Vector3[4];
        private static readonly HashSet<string> Reported = new HashSet<string>();
        private static Canvas canvas;
        private static Transform canvasFor;
        private static float nextCheck;
        private static int lastFingerprint;

        [HarmonyPriority(Priority.Last)]
        private static void Postfix(InventoryGui __instance)
        {
            if (!InventoryGui.IsVisible() || !Plugin.InventorySlotsVisibility.Value)
            {
                if (Touched.Count > 0) RestoreAll();
                lastFingerprint = 0;
                return;
            }
            if (Time.unscaledTime < nextCheck) return;
            nextCheck = Time.unscaledTime + Interval;
            var root = __instance.m_inventoryRoot;
            if (!root || DragItem(__instance)) return;
            if (canvasFor != root)
            {
                canvasFor = root;
                canvas = root.GetComponentInParent<Canvas>()?.rootCanvas;
            }
            if (!canvas) return;
            int fingerprint = Fingerprint(__instance, root);
            if (fingerprint == lastFingerprint) return;
            try
            {
                Check(__instance, root);
                lastFingerprint = Fingerprint(__instance, root);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Inventory Slots Visibility stopped: {e.Message}");
                Plugin.InventorySlotsVisibility.Value = false;
            }
        }

        // Changes whenever something that decides where slots end up changes; reading it allocates nothing.
        private static int Fingerprint(InventoryGui gui, Transform root)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + Screen.width;
                hash = hash * 31 + Screen.height;
                hash = hash * 31 + canvas.scaleFactor.GetHashCode();
                for (int i = 0; i < root.childCount; i++)
                {
                    var child = root.GetChild(i);
                    if (!child.gameObject.activeSelf) continue;
                    hash = hash * 31 + i;
                    if (child is RectTransform rect)
                    {
                        hash = hash * 31 + rect.anchoredPosition.GetHashCode();
                        hash = hash * 31 + rect.sizeDelta.GetHashCode();
                    }
                }
                var player = gui.m_playerGrid;
                if (player) hash = hash * 31 + Elements(player).Count;
                var container = gui.ContainerGrid;
                if (container && container.gameObject.activeInHierarchy) hash = hash * 31 + Elements(container).Count + 7;
                return hash == 0 ? 1 : hash;
            }
        }

        private static void Check(InventoryGui gui, Transform root)
        {
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            // The area the game's UI is drawn in, in screen pixels: the whole window at any aspect ratio.
            var screen = canvas.pixelRect;
            screen = new Rect(screen.x + Margin, screen.y + Margin, screen.width - 2 * Margin, screen.height - 2 * Margin);

            // Windows we moved go back home first, so each check starts from the mods' own layout.
            Scratch.Clear();
            Scratch.AddRange(Touched);
            foreach (var window in Scratch)
                if (window && MovedWindows.TryGetValue(window, out var moved))
                {
                    if (window.anchoredPosition != moved.Placed) Forget(window);
                    else window.anchoredPosition = moved.Home;
                }

            FindWindows(root, screen, camera);
            for (int pass = 0; pass < 3; pass++)
                if (!FixOnce(gui, root, screen, camera)) break;

            // Windows that are home again aren't ours any more.
            Scratch.Clear();
            Scratch.AddRange(Touched);
            foreach (var window in Scratch)
                if (!window || (MovedWindows.TryGetValue(window, out var moved) && window.anchoredPosition == moved.Home)) Forget(window);
        }

        // The inventory screen's top-level windows: crafting, your inventory, the chest or backpack, and anything mods add
        // beside them. Screen-wide overlays and invisible holders don't count.
        private static void FindWindows(Transform root, Rect screen, Camera camera)
        {
            Windows.Clear();
            for (int i = 0; i < root.childCount; i++)
            {
                if (!(root.GetChild(i) is RectTransform window) || !window.gameObject.activeInHierarchy) continue;
                var rect = ScreenRect(window, camera);
                if (rect.width <= 1f || rect.height <= 1f || rect.width * rect.height > 0.6f * screen.width * screen.height || !Visible(window)) continue;
                Windows.Add(window);
            }
        }

        private static bool FixOnce(InventoryGui gui, Transform root, Rect screen, Camera camera)
        {
            for (int g = 0; g < 2; g++)
            {
                var grid = g == 0 ? gui.m_playerGrid : gui.ContainerGrid;
                if (!grid || !grid.gameObject.activeInHierarchy) continue;
                Hidden.Clear();
                foreach (var element in Elements(grid))
                {
                    if (!element || !element.gameObject.activeInHierarchy) continue;
                    var holder = TopWindow(element.transform, root);
                    if (!holder) continue;
                    var slot = ScreenRect((RectTransform)element.transform, camera);
                    var center = slot.center;
                    if (Clipped(element.transform, center, camera)) continue;
                    RectTransform cover = null;
                    if (!screen.Contains(center)) cover = holder;
                    else
                        foreach (var window in Windows)
                            if (window != holder && window.GetSiblingIndex() > holder.GetSiblingIndex() && ScreenRect(window, camera).Contains(center)) { cover = window; break; }
                    if (cover == null) continue;
                    var key = (holder, cover);
                    Hidden[key] = Hidden.TryGetValue(key, out var area) ? Union(area, slot) : slot;
                }

                foreach (var pair in Hidden)
                {
                    var (holder, cover) = pair.Key;
                    var slots = pair.Value;
                    if (cover == holder)
                    {
                        if (Move(holder, Inside(slots, screen), camera, $"slots of {holder.name} were off screen")) return true;
                        continue;
                    }
                    // Under another window: move the one that isn't your inventory; your inventory stays put if it can.
                    var mover = holder == gui.m_player ? cover : holder;
                    var shift = mover == holder
                        ? Apart(ScreenRect(holder, camera), slots, ScreenRect(cover, camera), screen)
                        : Apart(ScreenRect(cover, camera), ScreenRect(cover, camera), slots, screen);
                    if (shift == Vector2.zero && mover != holder)
                    {
                        mover = holder;
                        shift = Apart(ScreenRect(holder, camera), slots, ScreenRect(cover, camera), screen);
                    }
                    if (Move(mover, shift, camera, $"{cover.name} covered slots of {holder.name}")) return true;
                }
            }

            // Finally, no window of the inventory screen sticks out past the screen edge.
            foreach (var window in Windows)
                if (Move(window, Inside(ScreenRect(window, camera), screen), camera, $"{window.name} reached past the screen edge")) return true;
            return false;
        }

        // A window counts only if something of it is drawn: its own background or that of a direct child.
        private static bool Visible(RectTransform window)
        {
            for (var parent = window; parent; parent = parent.parent as RectTransform)
            {
                var group = parent.GetComponent<CanvasGroup>();
                if (group && group.alpha < 0.05f) return false;
            }
            if (Drawn(window)) return true;
            for (int i = 0; i < window.childCount; i++)
                if (Drawn(window.GetChild(i))) return true;
            return false;
        }

        private static bool Drawn(Transform transform)
        {
            var graphic = transform.GetComponent<Graphic>();
            return graphic && graphic.enabled && transform.gameObject.activeInHierarchy && graphic.color.a > 0.05f;
        }

        // A slot scrolled out of its list's view is hidden on purpose, not by another window.
        private static bool Clipped(Transform element, Vector2 center, Camera camera)
        {
            for (var parent = element.parent; parent; parent = parent.parent)
                if ((parent.GetComponent<RectMask2D>() || parent.GetComponent<Mask>()) && parent is RectTransform view && !ScreenRect(view, camera).Contains(center))
                    return true;
            return false;
        }

        private static RectTransform TopWindow(Transform transform, Transform root)
        {
            while (transform && transform.parent != root) transform = transform.parent;
            return transform as RectTransform;
        }

        private static Rect ScreenRect(RectTransform rect, Camera camera)
        {
            rect.GetWorldCorners(Corners);
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < 4; i++)
            {
                var point = RectTransformUtility.WorldToScreenPoint(camera, Corners[i]);
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static Rect Union(Rect a, Rect b) =>
            Rect.MinMaxRect(Mathf.Min(a.xMin, b.xMin), Mathf.Min(a.yMin, b.yMin), Mathf.Max(a.xMax, b.xMax), Mathf.Max(a.yMax, b.yMax));

        // The smallest shift that puts the rect inside the screen (or as much of it as fits, top-left first).
        private static Vector2 Inside(Rect rect, Rect screen)
        {
            float x = rect.width > screen.width ? screen.xMin - rect.xMin
                : rect.xMin < screen.xMin ? screen.xMin - rect.xMin : rect.xMax > screen.xMax ? screen.xMax - rect.xMax : 0f;
            float y = rect.height > screen.height ? screen.yMax - rect.yMax
                : rect.yMin < screen.yMin ? screen.yMin - rect.yMin : rect.yMax > screen.yMax ? screen.yMax - rect.yMax : 0f;
            return new Vector2(x, y);
        }

        // The shortest of the four moves (left, right, up, down) that takes `part` (inside `window`) clear of `still`
        // while the whole window stays on screen; zero when none fits.
        private static Vector2 Apart(Rect window, Rect part, Rect still, Rect screen)
        {
            Vector2 best = Vector2.zero;
            float bestLength = float.MaxValue;
            for (int i = 0; i < 4; i++)
            {
                var option = i == 0 ? new Vector2(still.xMin - part.xMax - Margin, 0f)
                    : i == 1 ? new Vector2(still.xMax - part.xMin + Margin, 0f)
                    : i == 2 ? new Vector2(0f, still.yMin - part.yMax - Margin)
                    : new Vector2(0f, still.yMax - part.yMin + Margin);
                var moved = new Rect(window.position + option, window.size);
                if (moved.xMin < screen.xMin || moved.xMax > screen.xMax || moved.yMin < screen.yMin || moved.yMax > screen.yMax) continue;
                if (option.sqrMagnitude < bestLength) { best = option; bestLength = option.sqrMagnitude; }
            }
            return best;
        }

        private static bool Move(RectTransform window, Vector2 screenShift, Camera camera, string reason)
        {
            if (screenShift.sqrMagnitude < 1f || !(window.parent is RectTransform parent)) return false;
            var from = RectTransformUtility.WorldToScreenPoint(camera, window.position);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, from, camera, out var a) ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, from + screenShift, camera, out var b)) return false;
            var moved = MovedWindows.GetValue(window, w => new Moved { Home = w.anchoredPosition });
            window.anchoredPosition += b - a;
            moved.Placed = window.anchoredPosition;
            if (!Touched.Contains(window)) Touched.Add(window);
            string report = $"{reason}; moved {window.name} by {Mathf.RoundToInt(screenShift.x)}, {Mathf.RoundToInt(screenShift.y)} px (screen {Screen.width}x{Screen.height})";
            if (Reported.Add(report)) Plugin.Log.LogInfo("Inventory Slots Visibility: " + report);
            return true;
        }

        private static void Forget(RectTransform window)
        {
            Touched.Remove(window);
            if (window) MovedWindows.Remove(window);
        }

        private static void RestoreAll()
        {
            foreach (var window in Touched)
                if (window && MovedWindows.TryGetValue(window, out var moved) && window.anchoredPosition == moved.Placed) window.anchoredPosition = moved.Home;
            foreach (var window in Touched) if (window) MovedWindows.Remove(window);
            Touched.Clear();
        }
    }
}
