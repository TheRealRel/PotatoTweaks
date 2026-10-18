// Potato Tweaks - any item on item stands, with adjustable size, angle and position.
// Adapted from JereKuusela's Item Stand All Items 1.26 (public domain, The Unlicense). Same stand data keys, so stands
// adjusted with that mod keep their adjustments.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HarmonyLib;
using Jotunn.Entities;
using UnityEngine;

namespace PotatoTweaks
{
    internal enum ItemStandItems { All, Compatible, Vanilla }

    // The game only lets item stands hold items that have a stand model ("attach"). This finds a model for any item: the
    // item's own stand model first, else its only model part, else the whole item (with its scripts taken off, so it
    // doesn't fall to the ground as a second copy). What is on a stand is saved the game's own way, so taking it off works
    // with or without this, and enchantments and other item data stay. Placing and taking off is each player's game;
    // the settings come from the server so everyone sees the same.
    internal static class ItemStands
    {
        internal const string OriginalModId = "item_stand_all_items";
        internal static readonly Type[] Patches = { typeof(Stand) };

        internal static readonly int HashHide = "itemstand_hide".GetStableHashCode();
        internal static readonly int HashOffset = "itemstand_offset".GetStableHashCode();
        internal static readonly int HashRotation = "itemstand_rotation".GetStableHashCode();
        internal static readonly int HashScale = "itemstand_scale".GetStableHashCode();

        private static readonly AccessTools.FieldRef<ItemStand, GameObject> VisualItem = AccessTools.FieldRefAccess<ItemStand, GameObject>("m_visualItem");
        private static readonly AccessTools.FieldRef<ItemStand, int> VisualHash = AccessTools.FieldRefAccess<ItemStand, int>("m_visualHash");
        private static readonly AccessTools.FieldRef<ItemStand, int> VisualVariant = AccessTools.FieldRefAccess<ItemStand, int>("m_visualVariant");
        private static readonly AccessTools.FieldRef<ItemStand, int> Orientation = AccessTools.FieldRefAccess<ItemStand, int>("m_orientation");
        private static readonly AccessTools.FieldRef<ItemStand, ZNetView> View = AccessTools.FieldRefAccess<ItemStand, ZNetView>("m_nview");
        private static readonly Action<ItemStand> UpdateVisual = AccessTools.MethodDelegate<Action<ItemStand>>(AccessTools.Method(typeof(ItemStand), "UpdateVisual"));

        internal struct Transformation
        {
            internal Vector3 Position, Rotation, Scale;
        }

        private static HashSet<string> standIds = new HashSet<string>();
        private static Dictionary<int, Transformation> transformations = new Dictionary<int, Transformation>();

        internal static void Initialize()
        {
            ReadSettings();
            void Reread() { ReadSettings(); ReloadAll(); }
            Plugin.ItemStandIds.SettingChanged += (_, __) => Reread();
            Plugin.ItemStandTransformations.SettingChanged += (_, __) => Reread();
            Plugin.ItemStandMode.SettingChanged += (_, __) => ReloadAll();
            Plugin.ItemStandAdjust.SettingChanged += (_, __) => ReloadAll();
            Plugin.ItemStandMaxOffset.SettingChanged += (_, __) => ReloadAll();
            Plugin.ItemStandMaxScale.SettingChanged += (_, __) => ReloadAll();
            Plugin.ItemStandLegacy.SettingChanged += (_, __) => ReloadAll();
            Plugin.ItemStandHide.SettingChanged += (_, __) => ReloadAll();
            Plugin.ItemStandCloser.SettingChanged += (_, __) => ReloadAll();
        }

        private static void ReadSettings()
        {
            standIds = new HashSet<string>(Plugin.ItemStandIds.Value.Split(',').Select(id => id.Trim().ToLowerInvariant()).Where(id => id.Length > 0));
            transformations = ParseTransformations(Plugin.ItemStandTransformations.Value);
        }

        // id,distance,offset_x,offset_y,angle_1,angle_2,angle_3,scale_1,scale_2,scale_3|id,...
        internal static Dictionary<int, Transformation> ParseTransformations(string text)
        {
            var result = new Dictionary<int, Transformation>();
            foreach (var entry in text.Split('|'))
            {
                var args = entry.Split(',').Select(value => value.Trim()).ToList();
                if (args.Count == 0 || args[0].Length == 0) continue;
                float At(int index, float fallback) =>
                    index < args.Count && float.TryParse(args[index], NumberStyles.Float, CultureInfo.InvariantCulture, out float number) ? number : fallback;
                var transformation = new Transformation
                {
                    Position = new Vector3(At(2, 0f), At(1, 0f), At(3, 0f)),
                    Rotation = new Vector3(At(4, 0f), At(5, 0f), At(6, 0f)),
                    Scale = new Vector3(At(7, 1f), At(8, 1f), At(9, 1f)),
                };
                int hash = args[0].GetStableHashCode();
                if (!result.ContainsKey(hash)) result.Add(hash, transformation);
            }
            return result;
        }

        private static bool Enabled(ItemStand stand) =>
            stand && View(stand) && standIds.Contains(Utils.GetPrefabName(stand.transform.root.gameObject).ToLowerInvariant());

        // The item's stand model, its only model part, or the whole item.
        private static GameObject FindModel(GameObject item)
        {
            if (!item) return null;
            var attach = item.transform.Find("attach");
            if (attach) return attach.gameObject;
            if (Plugin.ItemStandMode.Value != ItemStandItems.All) return null;
            if (Plugin.ItemStandLegacy.Value)
            {
                var collider = item.GetComponentInChildren<Collider>();
                return collider ? collider.gameObject : null;
            }
            return OnlyChild(item) ?? item;
        }

        private static GameObject OnlyChild(GameObject item)
        {
            GameObject only = null;
            // The item's own layer first: some items have extra parts for being worn.
            foreach (Transform child in item.transform)
            {
                if (child.gameObject.layer != item.layer) continue;
                if (only) return null;
                only = child.gameObject;
            }
            if (only) return only;
            foreach (Transform child in item.transform)
            {
                if (only) return null;
                only = child.gameObject;
            }
            return only;
        }

        [HarmonyPatch(typeof(ItemStand))]
        internal static class Stand
        {
            // The game's own model lookup has no stand to go by, so the stand being drawn is noted here.
            [ThreadStatic] private static ItemStand drawing;
            [ThreadStatic] private static bool wasDisabled;

            [HarmonyPatch("CanAttach"), HarmonyPostfix]
            private static void CanAttach(ItemStand __instance, ItemDrop.ItemData item, ref bool __result)
            {
                if (__result || item == null || Plugin.ItemStandMode.Value == ItemStandItems.Vanilla || !Enabled(__instance)) return;
                __result = FindModel(item.m_dropPrefab) != null;
            }

            [HarmonyPatch(nameof(ItemStand.GetAttachPrefab)), HarmonyPostfix]
            private static void GetAttachPrefab(GameObject item, ref GameObject __result)
            {
                if (__result || !drawing || Plugin.ItemStandMode.Value == ItemStandItems.Vanilla) return;
                __result = FindModel(item);
            }

            [HarmonyPatch("SetVisualItem"), HarmonyPrefix]
            private static void Before(ItemStand __instance, int itemHash, int variant, int orientation, out bool __state)
            {
                __state = VisualHash(__instance) == itemHash && VisualVariant(__instance) == variant && Orientation(__instance) == orientation;
                if (!Enabled(__instance)) return;
                drawing = __instance;
                // A whole item has its own network object; this keeps the copy on the stand from becoming a second item.
                wasDisabled = ZNetView.m_forceDisableInit;
                ZNetView.m_forceDisableInit = true;
            }

            [HarmonyPatch("SetVisualItem"), HarmonyPostfix]
            private static void After(ItemStand __instance, bool __state)
            {
                if (drawing != __instance) return;
                drawing = null;
                ZNetView.m_forceDisableInit = wasDisabled;
                if (!__state) Refresh(__instance);
            }

            // Whatever happens, the game's object creation goes back to how it was.
            [HarmonyPatch("SetVisualItem"), HarmonyFinalizer]
            private static void Always(ItemStand __instance)
            {
                if (drawing != __instance) return;
                drawing = null;
                ZNetView.m_forceDisableInit = wasDisabled;
            }
        }

        private static void Refresh(ItemStand stand)
        {
            ReplaceWholeItem(stand);
            Show(stand);
            Adjust(stand);
            HideStand(stand);
        }

        // A whole item carries ItemDrop and physics; its model parts are moved to a plain object instead.
        private static void ReplaceWholeItem(ItemStand stand)
        {
            var item = VisualItem(stand);
            if (!item || !item.GetComponent<ItemDrop>()) return;
            var parent = item.transform.parent;
            var plain = new GameObject("PotatoTweaks_StandItem") { layer = item.layer };
            plain.transform.SetParent(parent, false);
            // Keeps the place, turn and size the game gave the item on this stand.
            plain.transform.localPosition = item.transform.localPosition;
            plain.transform.localRotation = item.transform.localRotation;
            plain.transform.localScale = item.transform.localScale;
            var parts = new List<Transform>();
            foreach (Transform child in item.transform)
                if (child.gameObject.layer == plain.layer) parts.Add(child);
            foreach (var part in parts) part.SetParent(plain.transform, false);
            ZNetScene.instance.Destroy(item);
            VisualItem(stand) = plain;
        }

        // Some stand models are switched off in the item (and some are the worn model, kept inactive).
        private static void Show(ItemStand stand)
        {
            var item = VisualItem(stand);
            if (!item || item.activeSelf) return;
            var worn = item.transform.Find("equiped");
            if (worn)
            {
                item.transform.localPosition = Vector3.zero;
                worn.gameObject.SetActive(true);
                if (worn.TryGetComponent<Rigidbody>(out var body)) body.isKinematic = true;
            }
            item.SetActive(Plugin.ItemStandMode.Value == ItemStandItems.All);
        }

        // Default adjustments per item from the settings, then the stand's own (set with the commands), within the limits.
        private static void Adjust(ItemStand stand)
        {
            var item = VisualItem(stand);
            if (!item) return;
            var zdo = View(stand).GetZDO();
            if (Plugin.ItemStandAdjust.Value && zdo != null)
            {
                transformations.TryGetValue(VisualHash(stand), out var preset);
                bool hasPreset = transformations.ContainsKey(VisualHash(stand));
                var transform = item.transform;
                if (hasPreset) transform.localPosition += preset.Position;
                var offset = Limit(zdo.GetVec3(HashOffset, Vector3.zero), Plugin.ItemStandMaxOffset.Value);
                transform.localPosition += offset;
                if (hasPreset) transform.localRotation *= Quaternion.Euler(preset.Rotation);
                var rotation = zdo.GetVec3(HashRotation, Vector3.zero);
                if (rotation != Vector3.zero) transform.localRotation *= Quaternion.Euler(rotation);
                if (hasPreset) transform.localScale = Vector3.Scale(transform.localScale, preset.Scale);
                var scale = zdo.GetVec3(HashScale, Vector3.one);
                if (scale != Vector3.one) transform.localScale = Vector3.Scale(transform.localScale, LimitScale(scale, Plugin.ItemStandMaxScale.Value));
            }
            if (Plugin.ItemStandCloser.Value)
            {
                // The stand's attach point is turned, so its height is the distance from the wall.
                var attach = item.transform.parent.localPosition;
                item.transform.localPosition -= new Vector3(attach.y, attach.z, attach.x);
            }
        }

        // Each side between a tenth and the maximum multiplier (0 = no maximum).
        private static Vector3 LimitScale(Vector3 scale, float max)
        {
            float top = max > 0f ? max : float.MaxValue;
            return new Vector3(Mathf.Clamp(scale.x, 0.1f, top), Mathf.Clamp(scale.y, 0.1f, top), Mathf.Clamp(scale.z, 0.1f, top));
        }

        // Offsets longer than the maximum distance are shortened to it (0 = no maximum).
        private static Vector3 Limit(Vector3 value, float max) =>
            max > 0f && value.sqrMagnitude > max * max ? value * (max / value.magnitude) : value;

        // Hides the stand itself (not the item on it) when it holds an item: per stand with the command, else the setting.
        private static void HideStand(ItemStand stand)
        {
            var zdo = View(stand).GetZDO();
            int hide = zdo != null ? zdo.GetInt(HashHide, 0) : 0;
            if (hide == 0) hide = Plugin.ItemStandHide.Value ? 1 : -1;
            bool show = hide < 1 || VisualHash(stand) == 0;
            var item = VisualItem(stand);
            foreach (var renderer in stand.GetComponentsInChildren<MeshRenderer>())
            {
                if (item && renderer.gameObject.layer != stand.gameObject.layer) continue;
                if (renderer.enabled != show) renderer.enabled = show;
            }
        }

        private static void ReloadAll()
        {
            if (!ZNet.instance || ZNet.instance.HaveStopped) return;
            foreach (var stand in UnityEngine.Object.FindObjectsByType<ItemStand>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (Enabled(stand)) Reload(stand);
        }

        private static void Reload(ItemStand stand)
        {
            var item = VisualItem(stand);
            if (item) UnityEngine.Object.Destroy(item);
            VisualHash(stand) = 0;
            UpdateVisual(stand);
        }

        // ---------------------------------------------------------------- commands, on the stand you look at
        internal static IEnumerable<ConsoleCommand> Commands()
        {
            yield return new Command("itemstand_offset", "[forward,right,up=0,0,0] - Moves the item on the item stand you look at.", (stand, zdo, args) =>
            {
                zdo.Set(HashOffset, args.Length > 0 ? ReadVector(args[0]) : Vector3.zero);
                return "Offset set.";
            });
            yield return new Command("itemstand_rotation", "[roll,pitch,yaw=0,0,0] - Turns the item on the item stand you look at.", (stand, zdo, args) =>
            {
                zdo.Set(HashRotation, args.Length > 0 ? ReadVector(args[0]) : Vector3.zero);
                return "Rotation set.";
            });
            yield return new Command("itemstand_scale", "[x,y,z=1,1,1] - Resizes the item on the item stand you look at (one number for all sides).", (stand, zdo, args) =>
            {
                zdo.Set(HashScale, args.Length > 0 ? ReadScale(args[0]) : Vector3.one);
                return "Scale set.";
            });
            yield return new Command("itemstand_hide", "[-1/0/1] - Whether the item stand you look at hides while holding an item: 1 hide, -1 show, 0 the setting.", (stand, zdo, args) =>
            {
                int value = args.Length > 0 && int.TryParse(args[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int number) ? Mathf.Clamp(number, -1, 1) : 0;
                zdo.Set(HashHide, value);
                return "Hiding set.";
            });
            yield return new Command("itemstand_info", "- Shows the item, offset, rotation, scale and hiding of the item stand you look at.", (stand, zdo, args) =>
            {
                var offset = zdo.GetVec3(HashOffset, Vector3.zero);
                var rotation = zdo.GetVec3(HashRotation, Vector3.zero);
                var scale = zdo.GetVec3(HashScale, Vector3.one);
                var prefab = ObjectDB.instance ? ObjectDB.instance.GetItemPrefab(VisualHash(stand)) : null;
                return $"Item: {(prefab ? prefab.name : "none")}\nOffset: {offset.y}, {offset.x}, {offset.z}\nRotation: {rotation.y}, {rotation.x}, {rotation.z}\n" +
                       $"Scale: {scale.y}, {scale.x}, {scale.z}\nHide: {zdo.GetInt(HashHide, 0)}";
            }, changes: false);
        }

        // "forward,right,up": stored the way the original mod stores it, so its stands read the same.
        private static Vector3 ReadVector(string text)
        {
            var parts = text.Split(',');
            float At(int index) => index < parts.Length && float.TryParse(parts[index], NumberStyles.Float, CultureInfo.InvariantCulture, out float value) ? value : 0f;
            return new Vector3(At(1), At(0), At(2));
        }

        private static Vector3 ReadScale(string text)
        {
            var scale = ReadVector(text);
            if (scale.y == 0f) scale.y = 1f;
            if (scale.x == 0f) scale.x = scale.y;
            if (scale.z == 0f) scale.z = scale.y;
            return scale;
        }

        private sealed class Command : ConsoleCommand
        {
            private readonly string name, help;
            private readonly Func<ItemStand, ZDO, string[], string> action;
            private readonly bool changes;

            internal Command(string name, string help, Func<ItemStand, ZDO, string[], string> action, bool changes = true)
            {
                this.name = name;
                this.help = help;
                this.action = action;
                this.changes = changes;
            }

            public override string Name => name;
            public override string Help => help;

            public override void Run(string[] args)
            {
                var player = Player.m_localPlayer;
                var stand = player && player.GetHoverObject() ? player.GetHoverObject().GetComponentInParent<ItemStand>() : null;
                var zdo = stand && Enabled(stand) ? View(stand).GetZDO() : null;
                if (zdo == null)
                {
                    Say("Look at an item stand first.");
                    return;
                }
                if (changes && !Plugin.ItemStandAdjust.Value && name != "itemstand_hide")
                {
                    Say("Item stand adjustments are turned off on this server.");
                    return;
                }
                // Someone else's stand inside their ward stays as they set it.
                if (changes && !PrivateArea.CheckAccess(stand.transform.position, 0f, true))
                    return;
                string message = action(stand, zdo, args ?? new string[0]);
                if (changes) Reload(stand);
                Say(message);
            }

            private static void Say(string text)
            {
                Console.instance?.Print(text);
                Player.m_localPlayer?.Message(MessageHud.MessageType.TopLeft, text);
            }
        }
    }
}
