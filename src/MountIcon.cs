// The mount panel by the health and stamina bars shows the icon its saddle carries. Mods that reuse the Lox saddle
// for their own tames (the Odin Horse) show a Lox for every animal. When a mount's icon is the Lox's but the mount
// isn't a Lox, the icon becomes a picture of the animal itself, rendered once from its own model (Jotunn's
// RenderManager, the tool behind the hammer menu's icons) and kept for the session (Valheim, PotatoTweaks).
using System.Collections.Generic;
using HarmonyLib;
using Jotunn.Managers;
using UnityEngine;

namespace PotatoTweaks
{
    internal static class MountIcon
    {
        private static readonly Dictionary<string, Sprite> icons = new Dictionary<string, Sprite>();
        private static Sprite loxIcon;
        private static bool loxLooked;

        private static Sprite LoxIcon()
        {
            if (!loxLooked && ZNetScene.instance)
            {
                loxLooked = true;
                var lox = ZNetScene.instance.GetPrefab("Lox");
                var saddle = lox ? lox.GetComponent<Tameable>()?.m_saddle : null;
                loxIcon = saddle ? saddle.m_mountIcon : null;
            }
            return loxIcon;
        }

        private static Sprite IconFor(Character mount)
        {
            string name = Utils.GetPrefabName(mount.gameObject);
            if (icons.TryGetValue(name, out var icon)) return icon;
            icon = null;
            try
            {
                var prefab = ZNetScene.instance ? ZNetScene.instance.GetPrefab(name) : null;
                // Three-quarter view from the front left, a little from above.
                if (prefab) icon = RenderManager.Instance.Render(prefab, Quaternion.Euler(10f, 0f, 0f) * Quaternion.Euler(0f, 55f, 0f));
            }
            catch (System.Exception e) { Plugin.Log.LogWarning($"Mount icon for {name} couldn't be rendered: {e.GetBaseException().Message}"); }
            icons[name] = icon;
            return icon;
        }

        [HarmonyPatch(typeof(Hud), "UpdateMount")]
        internal static class OwnIcon
        {
            private static readonly AccessTools.FieldRef<Hud, UnityEngine.UI.Image> Icon = AccessTools.FieldRefAccess<Hud, UnityEngine.UI.Image>("m_mountIcon");

            private static void Postfix(Hud __instance, Player player)
            {
                var saddle = player ? player.GetDoodadController() as Sadle : null;
                if (!saddle || !saddle.m_mountIcon || saddle.m_mountIcon != LoxIcon()) return;
                var mount = saddle.GetCharacter();
                if (!mount || Utils.GetPrefabName(mount.gameObject) == "Lox") return;
                var icon = IconFor(mount);
                if (icon) Icon(__instance).overrideSprite = icon;
            }
        }
    }
}
