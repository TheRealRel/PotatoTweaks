// Potato Tweaks - keep every inventory slot visible, written for this mod.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace PotatoTweaks
{
    // Several mods add slots to the inventory screen (Equipment & Quick Slots' equipment, quick and quiver slots, Adventure
    // Backpacks' backpack in the chest window, larger inventories from Haldor's rows) and each places its windows on its
    // own. On some screen sizes or with a taller inventory one window can end up over another's slots, or partly off the
    // screen. While the inventory is open, check where every slot of your inventory and the open chest or backpack really
    // is on screen; when one is off screen or under another window, move that window just far enough, staying inside
    // the screen, and put it back once it isn't needed. Works in screen pixels of the game's own UI canvas, so it follows
    // any resolution, aspect ratio (16:9 to 32:9) and the game's GUI scale. Only moves windows; never changes items.
    [HarmonyPatch(typeof(InventoryGui), "Update")]
    internal static class InventoryLayout
    {
        private const float Interval = 0.15f, Margin = 4f;
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
        private static readonly Vector3[] Corners = new Vector3[4];
        private static readonly HashSet<string> Reported = new HashSet<string>();
        private static float nextCheck;

        [HarmonyPriority(Priority.Last)]
        private static void Postfix(InventoryGui __instance)
        {
            if (!Plugin.InventoryLayoutGuard.Value)
            {
                if (Touched.Count > 0) RestoreAll();
                return;
            }
            if (Time.unscaledTime < nextCheck) return;
            nextCheck = Time.unscaledTime + Interval;
            if (!InventoryGui.IsVisible() || !__instance.m_inventoryRoot || DragItem(__instance))
            {
                if (Touched.Count > 0 && !InventoryGui.IsVisible()) RestoreAll();
                return;
            }
            try { Check(__instance); }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Inventory layout check stopped: {e.Message}");
                Plugin.InventoryLayoutGuard.Value = false;
            }
        }

        private static void Check(InventoryGui gui)
        {
            var root = gui.m_inventoryRoot;
            var canvas = root.GetComponentInParent<Canvas>()?.rootCanvas;
            if (!canvas) return;
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            // The area the game's UI is drawn in, in screen pixels: the whole window at any aspect ratio.
            var screen = canvas.pixelRect;
            screen = new Rect(screen.x + Margin, screen.y + Margin, screen.width - 2 * Margin, screen.height - 2 * Margin);

            // Windows already moved by us go back home first, so each check starts from the mods' own layout.
            foreach (var window in Touched.ToList())
                if (window && MovedWindows.TryGetValue(window, out var moved))
                {
                    if (window.anchoredPosition != moved.Placed) Forget(window);
                    else window.anchoredPosition = moved.Home;
                }

            var windows = Windows(root, screen, camera);
            for (int pass = 0; pass < 3; pass++)
            {
                if (!FixOnce(gui, root, windows, screen, camera)) break;
            }

            // Windows home again and not needed elsewhere this time are no longer ours.
            foreach (var window in Touched.ToList())
                if (!window || (MovedWindows.TryGetValue(window, out var moved) && window.anchoredPosition == moved.Home)) Forget(window);
        }

        // The inventory screen's top-level windows: crafting, your inventory, the chest or backpack, and anything mods add
        // beside them. Screen-wide overlays don't count as windows.
        private static List<RectTransform> Windows(Transform root, Rect screen, Camera camera)
        {
            var windows = new List<RectTransform>();
            foreach (Transform child in root)
            {
                if (!(child is RectTransform window) || !child.gameObject.activeInHierarchy) continue;
                var rect = ScreenRect(window, camera);
                if (rect.width <= 1f || rect.height <= 1f || rect.width * rect.height > 0.6f * screen.width * screen.height || !Visible(window)) continue;
                windows.Add(window);
            }
            return windows;
        }

        private static bool FixOnce(InventoryGui gui, Transform root, List<RectTransform> windows, Rect screen, Camera camera)
        {
            foreach (var grid in new[] { gui.m_playerGrid, gui.ContainerGrid })
            {
                if (!grid || !grid.gameObject.activeInHierarchy) continue;
                var hidden = new Dictionary<(RectTransform holder, RectTransform cover), Rect>();
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
                        foreach (var window in windows)
                            if (window != holder && DrawnAbove(window, holder) && ScreenRect(window, camera).Contains(center)) { cover = window; break; }
                    if (cover == null) continue;
                    var key = (holder, cover);
                    hidden[key] = hidden.TryGetValue(key, out var area) ? Union(area, slot) : slot;
                }

                foreach (var pair in hidden)
                {
                    var (holder, cover) = pair.Key;
                    var slots = pair.Value;
                    if (cover == holder)
                    {
                        // Off screen: bring the window holding these slots back inside.
                        if (Move(holder, Inside(slots, screen), camera, $"slots of {holder.name} were off screen")) return true;
                        continue;
                    }
                    // Under another window: move the one that isn't your inventory; your inventory stays put if it can.
                    var mover = holder == gui.m_player ? cover : holder;
                    var still = mover == holder ? ScreenRect(cover, camera) : slots;
                    var moving = ScreenRect(mover, camera);
                    var shift = Apart(moving, mover == holder ? slots : moving, still, screen);
                    if (shift == Vector2.zero && mover != holder)
                    {
                        mover = holder;
                        shift = Apart(ScreenRect(holder, camera), slots, ScreenRect(cover, camera), screen);
                    }
                    if (Move(mover, shift, camera, $"{cover.name} covered slots of {holder.name}")) return true;
                }
            }

            // Finally, no window of the inventory screen sticks out of the screen.
            foreach (var window in windows)
                if (Move(window, Inside(ScreenRect(window, camera), screen), camera, $"{window.name} reached past the screen edge")) return true;
            return false;
        }

        // A window counts only if something of it is drawn: a background or content that isn't fully transparent.
        private static bool Visible(RectTransform window)
        {
            foreach (var group in window.GetComponentsInParent<CanvasGroup>())
                if (group.alpha < 0.05f) return false;
            foreach (var graphic in window.GetComponentsInChildren<Graphic>())
                if (graphic.enabled && graphic.color.a > 0.05f) return true;
            return false;
        }

        // A slot scrolled out of its list's view is hidden on purpose, not by another window.
        private static bool Clipped(Transform element, Vector2 center, Camera camera)
        {
            for (var parent = element.parent; parent; parent = parent.parent)
            {
                bool masks = parent.GetComponent<RectMask2D>() || parent.GetComponent<Mask>();
                if (masks && parent is RectTransform view && !ScreenRect(view, camera).Contains(center)) return true;
            }
            return false;
        }

        private static RectTransform TopWindow(Transform transform, Transform root)
        {
            while (transform && transform.parent != root) transform = transform.parent;
            return transform as RectTransform;
        }

        private static bool DrawnAbove(Transform a, Transform b) => a.GetSiblingIndex() > b.GetSiblingIndex();

        private static Rect ScreenRect(RectTransform rect, Camera camera)
        {
            rect.GetWorldCorners(Corners);
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
            foreach (var corner in Corners)
            {
                var point = RectTransformUtility.WorldToScreenPoint(camera, corner);
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
            var options = new[]
            {
                new Vector2(still.xMin - part.xMax - Margin, 0f), new Vector2(still.xMax - part.xMin + Margin, 0f),
                new Vector2(0f, still.yMin - part.yMax - Margin), new Vector2(0f, still.yMax - part.yMin + Margin),
            };
            Vector2 best = Vector2.zero;
            float bestLength = float.MaxValue;
            foreach (var option in options)
            {
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
            if (Reported.Add(report)) Plugin.Log.LogInfo("Inventory layout: " + report);
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
