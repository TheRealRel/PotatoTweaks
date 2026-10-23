// Potato Tweaks - station timers on hover, written from scratch.
using System;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace PotatoTweaks
{
    // Looking at a working station adds a line under its name with the time left:
    // - smelters, kilns, furnaces, spinning wheels, windmills and the like: the next item, everything queued, and when
    //   how long the fuel lasts (windmills count the wind they're getting);
    // - fermenters: until the mead is ready;
    // - cooking stations and ovens: each item until it's done, then until it burns.
    // The times follow the station's own speed, so other settings that speed stations up are counted.
    internal static class StationTimers
    {
        internal static readonly Type[] Patches = { typeof(SmelterOre), typeof(SmelterFuel), typeof(Ferment), typeof(Cooking) };

        private static readonly Func<Smelter, float> BakeTimer = AccessTools.MethodDelegate<Func<Smelter, float>>(AccessTools.Method(typeof(Smelter), "GetBakeTimer"));
        private static readonly Func<Smelter, int> QueueSize = AccessTools.MethodDelegate<Func<Smelter, int>>(AccessTools.Method(typeof(Smelter), "GetQueueSize"));
        private static readonly Func<Smelter, float> SmelterFuelLeft = AccessTools.MethodDelegate<Func<Smelter, float>>(AccessTools.Method(typeof(Smelter), "GetFuel"));
        private static readonly AccessTools.FieldRef<Smelter, ZNetView> SmelterView = AccessTools.FieldRefAccess<Smelter, ZNetView>("m_nview");
        private static readonly Func<Fermenter, double> FermentTime = AccessTools.MethodDelegate<Func<Fermenter, double>>(AccessTools.Method(typeof(Fermenter), "GetFermentationTime"));
        private static readonly AccessTools.FieldRef<Fermenter, ZNetView> FermenterView = AccessTools.FieldRefAccess<Fermenter, ZNetView>("m_nview");
        private static readonly AccessTools.FieldRef<Fermenter, bool> HasRoof = AccessTools.FieldRefAccess<Fermenter, bool>("m_hasRoof");
        private static readonly AccessTools.FieldRef<Fermenter, bool> Exposed = AccessTools.FieldRefAccess<Fermenter, bool>("m_exposed");
        private static readonly AccessTools.FieldRef<CookingStation, ZNetView> CookingView = AccessTools.FieldRefAccess<CookingStation, ZNetView>("m_nview");
        private static readonly Func<CookingStation, bool> FireLit = AccessTools.MethodDelegate<Func<CookingStation, bool>>(AccessTools.Method(typeof(CookingStation), "IsFireLit"));
        private static readonly Func<CookingStation, float> CookingFuel = AccessTools.MethodDelegate<Func<CookingStation, float>>(AccessTools.Method(typeof(CookingStation), "GetFuel"));

        private static readonly StringBuilder sb = new StringBuilder();

        internal static string Time(double seconds)
        {
            var t = TimeSpan.FromSeconds(Math.Max(0, Math.Ceiling(seconds)));
            return t.TotalHours >= 1 ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}" : $"{t.Minutes}:{t.Seconds:00}";
        }

        // The timer goes on its own line under the station's name.
        private static string Insert(string text, string line)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(line)) return text;
            int end = text.IndexOf('\n');
            return end < 0 ? text + "\n" + line : text.Insert(end, "\n" + line);
        }

        private static string SmelterLine(Smelter smelter)
        {
            var view = SmelterView(smelter);
            if (!Plugin.StationTimersEnabled.Value || !view || !view.IsValid()) return null;
            int queued = QueueSize(smelter);
            if (queued <= 0 || smelter.m_secPerProduct <= 0f) return null;
            float power = smelter.m_windmill ? smelter.m_windmill.GetPowerOutput() : 1f;
            if (smelter.m_windmill && power <= 0.01f) return "No wind";
            if (!smelter.IsActive()) return null;

            float next = Mathf.Max(0f, smelter.m_secPerProduct - BakeTimer(smelter)) / power;
            float all = next + (queued - 1) * smelter.m_secPerProduct / power;
            sb.Clear();
            sb.Append("Next in ").Append(Time(next));
            if (queued > 1) sb.Append(", all done in ").Append(Time(all));
            if (smelter.m_maxFuel > 0 && smelter.m_fuelPerProduct > 0)
            {
                float fuelLasts = SmelterFuelLeft(smelter) * (smelter.m_secPerProduct / smelter.m_fuelPerProduct) / power;
                string fuel = smelter.m_fuelItem ? Localization.instance.Localize(smelter.m_fuelItem.m_itemData.m_shared.m_name) : "Fuel";
                sb.Append('\n').Append(fuel).Append(" lasts ").Append(Time(fuelLasts));
                if (fuelLasts < all) sb.Append(", runs out first");
            }
            return sb.ToString();
        }

        [HarmonyPatch(typeof(Smelter), "OnHoverAddOre")]
        internal static class SmelterOre
        {
            private static void Postfix(Smelter __instance, ref string __result) => __result = Insert(__result, SmelterLine(__instance));
        }

        [HarmonyPatch(typeof(Smelter), "OnHoverAddFuel")]
        internal static class SmelterFuel
        {
            private static void Postfix(Smelter __instance, ref string __result) => __result = Insert(__result, SmelterLine(__instance));
        }

        [HarmonyPatch(typeof(Fermenter), nameof(Fermenter.GetHoverText))]
        internal static class Ferment
        {
            private static void Postfix(Fermenter __instance, ref string __result)
            {
                var view = FermenterView(__instance);
                if (!Plugin.StationTimersEnabled.Value || !view || !view.IsValid() || !HasRoof(__instance) || Exposed(__instance)) return;
                if (view.GetZDO().GetString(ZDOVars.s_content, "") == "") return;
                double time = FermentTime(__instance);
                if (time < 0 || time >= __instance.m_fermentationDuration) return;
                __result = Insert(__result, "Ready in " + Time(__instance.m_fermentationDuration - time));
            }
        }

        // The cooking station keeps its hover text up to date once a second, through this.
        [HarmonyPatch(typeof(CookingStation), "HoverText")]
        internal static class Cooking
        {
            private static void Postfix(CookingStation __instance, ref string __result)
            {
                var view = CookingView(__instance);
                if (!Plugin.StationTimersEnabled.Value || !view || !view.IsValid() || __instance.m_slots == null) return;
                bool cooking = (__instance.m_requireFire && FireLit(__instance)) || (__instance.m_useFuel && CookingFuel(__instance) > 0f);
                if (!cooking) return;

                var zdo = view.GetZDO();
                sb.Clear();
                for (int i = 0; i < __instance.m_slots.Length; i++)
                {
                    string item = zdo.GetString("slot" + i, "");
                    if (item == "") continue;
                    int status = zdo.GetInt("slotstatus" + i);
                    if (status == 2) continue; // burnt
                    var conversion = __instance.m_conversion.Find(c => c.m_from && c.m_to && (c.m_from.gameObject.name == item || c.m_to.gameObject.name == item));
                    if (conversion == null) continue;
                    float cooked = zdo.GetFloat("slot" + i);
                    string line;
                    if (status == 0)
                        line = Localization.instance.Localize(conversion.m_from.m_itemData.m_shared.m_name) + " done in " + Time(conversion.m_cookTime - cooked);
                    else if (__instance.m_canOvercookItems)
                        line = Localization.instance.Localize(conversion.m_to.m_itemData.m_shared.m_name) + " burns in " + Time(conversion.m_cookTime * 2f - cooked);
                    else continue;
                    if (sb.Length > 0) sb.Append('\n');
                    sb.Append(line);
                }
                if (sb.Length > 0) __result = Insert(__result, sb.ToString());
            }
        }
    }
}
