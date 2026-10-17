// Potato Tweaks - catching fish with spears, harpoons, bows and crossbows.
// Adapted from Spearfishing 1.0.5 by karkkant (https://github.com/karkkant/valheim-spearfishing), MIT License,
// Copyright (c) 2022 karkkant. See LICENSE.
using System;
using HarmonyLib;
using UnityEngine;

namespace PotatoTweaks
{
    // A spear, arrow or bolt that hits a fish swimming in the water kills it: the fish floats up and can be picked up like
    // any item. A harpoon pulls it straight into your inventory when there's room.
    [HarmonyPatch(typeof(Projectile), nameof(Projectile.OnHit))]
    internal static class Spearfishing
    {
        internal const string OriginalModId = "org.bepinex.plugins.spearfishing";
        private static readonly int Harpooned = "Harpooned".GetStableHashCode();
        private static readonly AccessTools.FieldRef<Projectile, int> StatusEffect = AccessTools.FieldRefAccess<Projectile, int>("m_statusEffectHash");
        private static readonly AccessTools.FieldRef<Projectile, Character> Owner = AccessTools.FieldRefAccess<Projectile, Character>("m_owner");
        private static readonly AccessTools.FieldRef<ItemDrop, ZNetView> DropView = AccessTools.FieldRefAccess<ItemDrop, ZNetView>("m_nview");
        private static readonly AccessTools.FieldRef<ItemDrop, Floating> DropFloating = AccessTools.FieldRefAccess<ItemDrop, Floating>("m_floating");
        private static readonly Action<Fish, float> Stop = AccessTools.MethodDelegate<Action<Fish, float>>(AccessTools.Method(typeof(Fish), "Stop"));
        private static readonly Func<Fish, Vector3, float> WaterLevel = AccessTools.MethodDelegate<Func<Fish, Vector3, float>>(AccessTools.Method(typeof(Fish), "GetWaterLevel"));

        private static bool Prefix(Projectile __instance, Collider collider)
        {
            var target = collider ? Projectile.FindHitObject(collider) : null;
            var fish = target ? target.GetComponent<Fish>() : null;
            if (!fish || fish.IsOutOfWater() || !(Owner(__instance) is Player player)) return true;
            bool harpoon = StatusEffect(__instance) == Harpooned || __instance.name.IndexOf("chitinharpoon", StringComparison.OrdinalIgnoreCase) >= 0;
            if (!Allowed(__instance, harpoon)) return true;

            // Already caught: let the projectile fly on.
            if (fish.m_speed <= 0f) return false;
            Stop(fish, Time.fixedDeltaTime);
            fish.m_speed = 0f;
            fish.m_acceleration = 0f;
            fish.m_turnRate = 0f;

            if (harpoon && fish.Pickup(player))
            {
                player.Message(MessageHud.MessageType.Center, fish.m_name + " $msg_harpoon_harpooned");
                Blood(fish.transform.position);
                return false;
            }

            // Leave a dead fish floating at the surface for anyone to pick up.
            var drop = fish.m_pickupItem ? fish.m_pickupItem.GetComponent<ItemDrop>() : null;
            if (!drop) return false;
            var floating = drop.GetComponent<Floating>();
            if (!floating) floating = drop.gameObject.AddComponent<Floating>();
            DropView(drop)?.ClaimOwnership();
            floating.m_waterLevelOffset = 0.65f;
            floating.SetLiquidLevel(WaterLevel(fish, fish.transform.position), LiquidType.Water, null);
            DropFloating(drop) = floating;
            UnityEngine.Object.DestroyImmediate(fish);
            __instance.m_hitEffects.Create(drop.transform.position, Quaternion.identity, null, 2f);
            Blood(drop.transform.position);
            drop.transform.Rotate(new Vector3(0f, 0f, UnityEngine.Random.value < 0.5f ? 90f : -90f));
            return false;
        }

        // By the weapon's skill, so modded spears, bows and crossbows count too; the game's own projectiles by name as well.
        private static bool Allowed(Projectile projectile, bool harpoon)
        {
            if (harpoon) return Plugin.FishHarpoons.Value;
            string name = projectile.name;
            if (projectile.m_skill == Skills.SkillType.Spears || Has(name, "spear_projectile") || Has(name, "projectile_splitner") || Has(name, "projectile_wolffang"))
                return Plugin.FishSpears.Value;
            if (projectile.m_skill == Skills.SkillType.Crossbows || Has(name, "arbalest_projectile")) return Plugin.FishCrossbows.Value;
            if (projectile.m_skill == Skills.SkillType.Bows || Has(name, "bow_projectile")) return Plugin.FishBows.Value;
            return false;
        }

        private static bool Has(string name, string part) => name.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0;

        private static void Blood(Vector3 position)
        {
            var prefab = ZNetScene.instance ? ZNetScene.instance.GetPrefab("fx_TickBloodHit") : null;
            if (prefab) UnityEngine.Object.Instantiate(prefab, position, Quaternion.identity);
        }
    }
}
