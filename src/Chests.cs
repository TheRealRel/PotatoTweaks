// Potato Tweaks - chest names and chest contents on hover, written from scratch.
using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PotatoTweaks
{
    // A chest can be given a name with a key while looking at it. The name is kept in the chest's own world data, so
    // everyone sees it, and it shows in place of the chest's kind when you look at it. Under the hover text, a row of
    // small item icons with counts shows what's inside. Private chests show their contents and take a name only for
    // their owner, and a ward keeps both from anyone it doesn't allow. Graves are left alone.
    internal static class Chests
    {
        private const string NameRpc = "PotatoTweaks_ChestName";
        private static readonly int NameKey = "potato_chestname".GetStableHashCode();
        private const int NameLimit = 32;

        internal static readonly Type[] Patches = { typeof(Register), typeof(HoverName), typeof(ContentsRow) };

        private static readonly AccessTools.FieldRef<Container, ZNetView> View = AccessTools.FieldRefAccess<Container, ZNetView>("m_nview");
        private static readonly Func<Container, long, bool> CheckAccess =
            AccessTools.MethodDelegate<Func<Container, long, bool>>(AccessTools.Method(typeof(Container), "CheckAccess"));

        // Whether the local player may see inside and name this chest.
        private static bool Allowed(Container chest)
        {
            if (!chest || chest.GetComponent<TombStone>()) return false;
            var view = View(chest);
            if (!view || !view.IsValid()) return false;
            if (chest.m_checkGuardStone && !PrivateArea.CheckAccess(chest.transform.position, 0f, false)) return false;
            return CheckAccess(chest, Game.instance.GetPlayerProfile().GetPlayerID());
        }

        private static string NameOf(Container chest)
        {
            var view = View(chest);
            return view && view.IsValid() ? view.GetZDO().GetString(NameKey, "") : "";
        }

        private static Container Hovered()
        {
            var hover = Player.m_localPlayer ? Player.m_localPlayer.GetHoverObject() : null;
            return hover ? hover.GetComponentInParent<Container>() : null;
        }

        private static string KeyText(BepInEx.Configuration.KeyboardShortcut key)
        {
            var parts = key.Modifiers.Select(KeyName).ToList();
            parts.Add(KeyName(key.MainKey));
            return string.Join(" + ", parts);
        }

        private static string KeyName(KeyCode key)
        {
            switch (key)
            {
                case KeyCode.LeftControl: case KeyCode.RightControl: return "Ctrl";
                case KeyCode.LeftShift: case KeyCode.RightShift: return "Shift";
                case KeyCode.LeftAlt: case KeyCode.RightAlt: return "Alt";
                default: return key.ToString();
            }
        }

        // The name travels to the chest's owner, who writes it into the world, the same way the game stores a
        // chest's items. Nobody has to take the chest over, so it's safe while someone has it open.
        [HarmonyPatch(typeof(Container), "Awake")]
        internal static class Register
        {
            private static void Postfix(Container __instance)
            {
                var view = View(__instance);
                if (!view || view.GetZDO() == null) return;
                view.Register<string>(NameRpc, (sender, name) =>
                {
                    if (view.IsValid() && view.IsOwner()) view.GetZDO().Set(NameKey, Clean(name));
                });
            }
        }

        private static string Clean(string name)
        {
            name = (name ?? "").Replace("<", "").Replace(">", "").Replace("\n", " ").Trim();
            return name.Length > NameLimit ? name.Substring(0, NameLimit) : name;
        }

        private sealed class Namer : TextReceiver
        {
            private readonly Container chest;
            internal Namer(Container chest) => this.chest = chest;
            public string GetText() => chest ? NameOf(chest) : "";
            public void SetText(string text)
            {
                if (!chest) return;
                var view = View(chest);
                if (view && view.IsValid()) view.InvokeRPC(NameRpc, Clean(text));
            }
        }

        // Called from Plugin.Update: the name key while looking at a chest.
        internal static void Update()
        {
            if (!Plugin.ChestNamesEnabled.Value || !Player.m_localPlayer || !TextInput.instance) return;
            if (!Plugin.ChestNameKey.Value.IsDown()) return;
            if (TextInput.IsVisible() || Console.IsVisible() || Menu.IsVisible() || InventoryGui.IsVisible() || Minimap.IsOpen()
                || (Chat.instance && Chat.instance.HasFocus()) || StoreGui.IsVisible()) return;
            var chest = Hovered();
            if (!Allowed(chest)) return;
            TextInput.instance.RequestText(new Namer(chest), "Chest name", NameLimit);
        }

        // The name replaces the chest's kind on the first line, and a key hint is added for naming it.
        [HarmonyPatch(typeof(Container), nameof(Container.GetHoverText))]
        internal static class HoverName
        {
            private static void Postfix(Container __instance, ref string __result)
            {
                if (!Plugin.ChestNamesEnabled.Value || string.IsNullOrEmpty(__result) || !Allowed(__instance)) return;
                string name = NameOf(__instance);
                if (name.Length > 0)
                {
                    string kind = Localization.instance.Localize(__instance.m_name);
                    if (__result.StartsWith(kind)) __result = name + __result.Substring(kind.Length);
                }
                __result += $"\n[<color=yellow><b>{KeyText(Plugin.ChestNameKey.Value)}</b></color>] Name";
            }
        }

        // A row of small icons under the hover text, built once and reused.
        // Runs last, so the hover text is final (other mods, like Potato Runes, may still change it) before it's measured.
        [HarmonyPatch(typeof(Hud), "UpdateCrosshair")]
        [HarmonyPriority(Priority.Last)]
        internal static class ContentsRow
        {
            private const float Gap = 3f;
            private static RectTransform row;
            private static readonly List<Image> icons = new List<Image>();
            private static readonly List<TextMeshProUGUI> counts = new List<TextMeshProUGUI>();
            private static TextMeshProUGUI more;
            private static float builtSize;
            private static readonly Dictionary<string, KeyValuePair<ItemDrop.ItemData, int>> totals = new Dictionary<string, KeyValuePair<ItemDrop.ItemData, int>>();

            private static void Postfix(Hud __instance)
            {
                try { Show(__instance); }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"Chest contents on hover is disabled: {e.GetBaseException().Message}");
                    Plugin.ChestContentsEnabled.Value = false;
                    if (row) row.gameObject.SetActive(false);
                }
            }

            private static void Show(Hud hud)
            {
                var chest = Plugin.ChestContentsEnabled.Value && !InventoryGui.IsVisible() && hud.m_hoverName.text.Length > 0 ? Hovered() : null;
                var inventory = chest && Allowed(chest) ? chest.GetInventory() : null;
                if (inventory == null || inventory.NrOfItems() == 0)
                {
                    if (row) row.gameObject.SetActive(false);
                    return;
                }

                totals.Clear();
                foreach (var item in inventory.GetAllItems())
                {
                    string key = item.m_shared.m_name;
                    totals[key] = totals.TryGetValue(key, out var have)
                        ? new KeyValuePair<ItemDrop.ItemData, int>(have.Key, have.Value + item.m_stack)
                        : new KeyValuePair<ItemDrop.ItemData, int>(item, item.m_stack);
                }
                var shown = totals.Values.OrderByDescending(t => t.Value).ThenBy(t => Localization.instance.Localize(t.Key.m_shared.m_name)).ToList();

                float size = Plugin.ChestIconSize.Value;
                int most = Plugin.ChestIconCount.Value;
                Build(hud.m_hoverName, size, most);

                // Keep the labels in the font the hover text is drawn in, in case another mod changes it (Potato Runes
                // does it with a font tag, so read the font of the text's first letter).
                hud.m_hoverName.ForceMeshUpdate();
                var drawn = hud.m_hoverName.textInfo;
                var font = drawn.characterCount > 0 && drawn.characterInfo[0].fontAsset ? drawn.characterInfo[0].fontAsset : hud.m_hoverName.font;
                var look = font == hud.m_hoverName.font ? hud.m_hoverName.fontSharedMaterial : drawn.characterInfo[0].material;
                if (!look || look.mainTexture != font.atlasTexture) look = font.material;
                if (more.font != font || more.fontSharedMaterial != look)
                    foreach (var label in counts.Append(more))
                    {
                        label.font = font;
                        label.fontSharedMaterial = look;
                        label.ForceMeshUpdate(true, true);
                    }

                int n = Mathf.Min(most, shown.Count);
                for (int i = 0; i < icons.Count; i++)
                {
                    bool on = i < n;
                    icons[i].gameObject.SetActive(on);
                    if (!on) continue;
                    icons[i].sprite = shown[i].Key.GetIcon();
                    counts[i].text = Count(shown[i].Value);
                }
                more.gameObject.SetActive(shown.Count > n);
                if (shown.Count > n)
                {
                    more.text = $"+{shown.Count - n} more";
                    ((RectTransform)more.transform).localPosition = new Vector3(n * (size + Gap) + 2f, -size / 2f, 0f);
                }

                // Just under the hover text, lined up with its left edge.
                var bounds = hud.m_hoverName.textBounds;
                row.localPosition = new Vector3(bounds.min.x, bounds.min.y - 4f, 0f);
                row.gameObject.SetActive(true);
            }

            private static string Count(int count) =>
                count >= 10000 ? $"{count / 1000}k" : count >= 1000 ? $"{count / 1000f:0.#}k" : count.ToString();

            private static void Build(TextMeshProUGUI text, float size, int most)
            {
                if (row && Mathf.Approximately(builtSize, size) && icons.Count == most) return;
                if (row) UnityEngine.Object.Destroy(row.gameObject);
                icons.Clear();
                counts.Clear();
                builtSize = size;

                row = new GameObject("PotatoChestContents", typeof(RectTransform)).GetComponent<RectTransform>();
                row.SetParent(text.transform, false);
                row.pivot = new Vector2(0f, 1f);
                row.anchorMin = row.anchorMax = new Vector2(0.5f, 0.5f);
                row.sizeDelta = new Vector2(most * (size + Gap), size);

                float fontSize = Mathf.Max(10f, size * 0.5f);
                for (int i = 0; i < most; i++)
                {
                    var slot = new GameObject("Item", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
                    slot.SetParent(row, false);
                    slot.pivot = new Vector2(0f, 1f);
                    slot.anchorMin = slot.anchorMax = new Vector2(0f, 1f);
                    slot.sizeDelta = new Vector2(size, size);
                    slot.localPosition = new Vector3(i * (size + Gap), 0f, 0f);
                    var image = slot.GetComponent<Image>();
                    image.preserveAspect = true;
                    image.raycastTarget = false;
                    icons.Add(image);

                    var count = Label(text, slot, fontSize, TextAlignmentOptions.BottomRight);
                    var rect = (RectTransform)count.transform;
                    rect.anchorMin = Vector2.zero;
                    rect.anchorMax = Vector2.one;
                    rect.offsetMin = new Vector2(-6f, -3f);
                    rect.offsetMax = new Vector2(3f, 0f);
                    counts.Add(count);
                }

                more = Label(text, row, fontSize, TextAlignmentOptions.MidlineLeft);
                var moreRect = (RectTransform)more.transform;
                moreRect.pivot = new Vector2(0f, 0.5f);
                moreRect.anchorMin = moreRect.anchorMax = new Vector2(0f, 1f);
                moreRect.sizeDelta = new Vector2(120f, size);
            }

            private static TextMeshProUGUI Label(TextMeshProUGUI like, Transform parent, float fontSize, TextAlignmentOptions alignment)
            {
                var label = new GameObject("Text", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
                label.transform.SetParent(parent, false);
                label.font = like.font;
                label.fontSharedMaterial = like.fontSharedMaterial;
                label.fontSize = fontSize;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.overflowMode = TextOverflowModes.Overflow;
                label.alignment = alignment;
                label.color = Color.white;
                label.raycastTarget = false;
                return label;
            }
        }
    }
}
