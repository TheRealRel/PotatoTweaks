// Potato Tweaks - a "Remove death markers" button on the large map.
// Written from scratch for this mod.
using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PotatoTweaks
{
    // The game leaves a skull on the map at every death and only removes them one at a time (right-click). This adds a
    // button next to the map's skull icon that removes them all. It asks for a second click first, and only touches
    // death markers: your own pins, boss and location icons stay. Personal and client-side: nothing is sent to the server.
    [HarmonyPatch(typeof(Minimap), "Update")]
    internal static class DeathMarkers
    {
        private const float CheckInterval = 0.25f, ConfirmTime = 3f, Gap = 6f;
        private static readonly AccessTools.FieldRef<Minimap, List<Minimap.PinData>> Pins = AccessTools.FieldRefAccess<Minimap, List<Minimap.PinData>>("m_pins");
        private static readonly List<Minimap.PinData> Removing = new List<Minimap.PinData>();
        private static readonly Vector3[] Corners = new Vector3[4];

        private static Button button;
        private static TMP_Text label;
        private static float nextCheck, confirmUntil;
        private static int shown = -1;

        private static void Postfix(Minimap __instance)
        {
            if (!__instance.m_largeRoot || !__instance.m_largeRoot.activeInHierarchy)
            {
                confirmUntil = 0f;
                return;
            }
            if (Time.unscaledTime < nextCheck) return;
            nextCheck = Time.unscaledTime + CheckInterval;

            int count = Plugin.DeathMarkerButton.Value ? Count(__instance) : 0;
            if (count == 0)
            {
                if (button) button.gameObject.SetActive(false);
                return;
            }
            if (!button && !Create(__instance)) return;
            button.gameObject.SetActive(true);
            Place(__instance);
            bool confirming = Time.unscaledTime < confirmUntil;
            int key = confirming ? -count : count;
            if (key == shown) return;
            shown = key;
            label.text = confirming ? $"Click again: remove {count}" : $"Remove death markers ({count})";
        }

        private static int Count(Minimap map)
        {
            var pins = Pins(map);
            int count = 0;
            if (pins != null)
                foreach (var pin in pins)
                    if (pin != null && pin.m_type == Minimap.PinType.Death) count++;
            return count;
        }

        // A copy of the inventory's Take all button, so it looks like the game's own, without its gamepad key or
        // translation helper (the text is set here).
        private static bool Create(Minimap map)
        {
            var source = InventoryGui.instance ? InventoryGui.instance.m_takeAllButton : null;
            if (!source) return false;
            var copy = Object.Instantiate(source.gameObject, map.m_largeRoot.transform, false);
            copy.name = "PotatoTweaks_RemoveDeathMarkers";
            foreach (var pad in copy.GetComponentsInChildren<UIGamePad>(true))
            {
                if (pad.m_hint) Object.Destroy(pad.m_hint);
                Object.Destroy(pad);
            }
            foreach (var localize in copy.GetComponentsInChildren<Localize>(true)) Object.Destroy(localize);
            foreach (var tooltip in copy.GetComponentsInChildren<UITooltip>(true)) Object.Destroy(tooltip);
            (copy.GetComponent<LayoutElement>() ?? copy.AddComponent<LayoutElement>()).ignoreLayout = true;

            button = copy.GetComponent<Button>();
            label = copy.GetComponentInChildren<TMP_Text>(true);
            if (!button || !label)
            {
                Object.Destroy(copy);
                button = null;
                return false;
            }
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(Clicked);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            var rect = (RectTransform)copy.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(Mathf.Max(rect.sizeDelta.x, 240f), rect.sizeDelta.y);
            shown = -1;
            return true;
        }

        // Just above the map's skull icon (below it if the icons sit in the top half), kept inside the map window.
        private static void Place(Minimap map)
        {
            var root = (RectTransform)map.m_largeRoot.transform;
            var rect = (RectTransform)button.transform;
            var icon = map.m_selectedIconDeath ? map.m_selectedIconDeath.rectTransform : null;
            var target = icon ? (icon.GetComponentInParent<Button>()?.transform as RectTransform ?? icon) : null;
            Rect area = root.rect;
            Vector2 size = rect.rect.size;
            Vector2 position;
            if (target)
            {
                target.GetWorldCorners(Corners);
                Vector3 low = root.InverseTransformPoint(Corners[0]), high = root.InverseTransformPoint(Corners[2]);
                float x = (low.x + high.x) * 0.5f;
                position = (low.y + high.y) * 0.5f < area.center.y
                    ? new Vector2(x, high.y + Gap + size.y * 0.5f)
                    : new Vector2(x, low.y - Gap - size.y * 0.5f);
            }
            else position = new Vector2(area.center.x, area.yMin + Gap + size.y * 0.5f);
            position.x = Mathf.Clamp(position.x, area.xMin + size.x * 0.5f, area.xMax - size.x * 0.5f);
            position.y = Mathf.Clamp(position.y, area.yMin + size.y * 0.5f, area.yMax - size.y * 0.5f);
            rect.localPosition = new Vector3(position.x, position.y, 0f);
        }

        private static void Clicked()
        {
            var map = Minimap.instance;
            if (!map) return;
            if (Time.unscaledTime >= confirmUntil)
            {
                confirmUntil = Time.unscaledTime + ConfirmTime;
                nextCheck = 0f;
                return;
            }
            confirmUntil = 0f;
            nextCheck = 0f;
            Removing.Clear();
            var pins = Pins(map);
            if (pins != null)
                foreach (var pin in pins)
                    if (pin != null && pin.m_type == Minimap.PinType.Death) Removing.Add(pin);
            foreach (var pin in Removing) map.RemovePin(pin);
            if (Removing.Count > 0)
                MessageHud.instance?.ShowMessage(MessageHud.MessageType.TopLeft, Removing.Count == 1 ? "Removed 1 death marker" : $"Removed {Removing.Count} death markers");
            Removing.Clear();
        }
    }
}
