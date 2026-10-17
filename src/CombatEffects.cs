// Potato Tweaks - dimmer combat effects, written from scratch.
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace PotatoTweaks
{
    // Particles that cover a creature in a fight (the flames of a burning enemy, frost and poison clouds and their tick
    // bursts, blood and spark bursts when it is hit, your weapon's impact burst, your arrows', bolts', staff shots' and
    // area attacks' impacts) hide its attack wind-up. Make those particles fainter, darker or smaller as they are created.
    // Sounds and every other effect stay as they are; only changes what you see.
    internal static class CombatEffects
    {
        private enum Source { None, Status, CreatureHit, Weapon, Impact }

        // What the game is doing while it creates effects, so a created effect can be traced to its source.
        [ThreadStatic] private static Character hitCharacter;
        [ThreadStatic] private static StatusEffect status;
        [ThreadStatic] private static Attack attack;
        [ThreadStatic] private static Projectile projectile;
        [ThreadStatic] private static Aoe area;

        private static readonly AccessTools.FieldRef<Projectile, Character> ProjectileOwner = AccessTools.FieldRefAccess<Projectile, Character>("m_owner");
        private static readonly AccessTools.FieldRef<Aoe, Character> AreaOwner = AccessTools.FieldRefAccess<Aoe, Character>("m_owner");
        private static readonly AccessTools.FieldRef<Attack, Humanoid> Attacker = AccessTools.FieldRefAccess<Attack, Humanoid>("m_character");
        private static readonly Dictionary<Type, FieldInfo> TickFields = new Dictionary<Type, FieldInfo>();

        internal static readonly Type[] Patches =
            { typeof(CreatureHits), typeof(StatusStart), typeof(StatusTick), typeof(Weapons), typeof(Projectiles), typeof(Areas), typeof(Created) };

        private static bool Counts(Character character) =>
            character && (!character.IsPlayer() || Plugin.DimOnPlayers.Value);

        private static bool Active =>
            Plugin.DimEnabled.Value && (Plugin.DimOpacity.Value < 0.999f || Plugin.DimBrightness.Value < 0.999f || Plugin.DimSize.Value < 0.999f);

        [HarmonyPatch]
        internal static class CreatureHits
        {
            private static IEnumerable<MethodBase> TargetMethods()
            {
                yield return AccessTools.Method(typeof(Character), "ApplyDamage");
                yield return AccessTools.Method(typeof(Character), "RPC_Damage");
            }

            private static void Prefix(Character __instance, out Character __state)
            {
                __state = hitCharacter;
                hitCharacter = __instance;
            }

            private static void Finalizer(Character __state) => hitCharacter = __state;
        }

        [HarmonyPatch(typeof(StatusEffect), "TriggerStartEffects")]
        internal static class StatusStart
        {
            private static void Prefix(StatusEffect __instance, out StatusEffect __state)
            {
                __state = status;
                status = __instance;
            }

            private static void Finalizer(StatusEffect __state) => status = __state;
        }

        // Burning, poison and other damage over time spawn a burst on every tick.
        [HarmonyPatch]
        internal static class StatusTick
        {
            private static IEnumerable<MethodBase> TargetMethods()
            {
                yield return AccessTools.Method(typeof(SE_Burning), nameof(SE_Burning.UpdateStatusEffect));
                yield return AccessTools.Method(typeof(SE_Poison), nameof(SE_Poison.UpdateStatusEffect));
                yield return AccessTools.Method(typeof(SE_Stats), nameof(SE_Stats.UpdateStatusEffect));
            }

            private static void Prefix(StatusEffect __instance, out StatusEffect __state)
            {
                __state = status;
                status = __instance;
            }

            private static void Finalizer(StatusEffect __state) => status = __state;
        }

        [HarmonyPatch]
        internal static class Weapons
        {
            private static IEnumerable<MethodBase> TargetMethods()
            {
                yield return AccessTools.Method(typeof(Attack), "DoMeleeAttack");
                yield return AccessTools.Method(typeof(Attack), "DoAreaAttack");
            }

            private static void Prefix(Attack __instance, out Attack __state)
            {
                __state = attack;
                attack = __instance;
            }

            private static void Finalizer(Attack __state) => attack = __state;
        }

        [HarmonyPatch(typeof(Projectile), "OnHit")]
        internal static class Projectiles
        {
            private static void Prefix(Projectile __instance, out Projectile __state)
            {
                __state = projectile;
                projectile = __instance;
            }

            private static void Finalizer(Projectile __state) => projectile = __state;
        }

        [HarmonyPatch(typeof(Aoe), "OnHit")]
        internal static class Areas
        {
            private static void Prefix(Aoe __instance, out Aoe __state)
            {
                __state = area;
                area = __instance;
            }

            private static void Finalizer(Aoe __state) => area = __state;
        }

        private static EffectList TickEffect(StatusEffect effect)
        {
            var type = effect.GetType();
            if (!TickFields.TryGetValue(type, out var field))
                TickFields[type] = field = AccessTools.Field(type, "m_tickEffect");
            return field?.GetValue(effect) as EffectList;
        }

        private static Source Of(EffectList list)
        {
            if (status != null && Counts(status.m_character) && (list == status.m_startEffects || list == TickEffect(status)))
                return Plugin.DimStatus.Value ? Source.Status : Source.None;
            if (hitCharacter && Counts(hitCharacter) &&
                (list == hitCharacter.m_hitEffects || list == hitCharacter.m_critHitEffects || list == hitCharacter.m_backstabHitEffects))
                return Plugin.DimCreatureHits.Value ? Source.CreatureHit : Source.None;
            if (attack != null && Attacker(attack) is Player && (list == attack.m_hitEffect || list == attack.GetWeapon()?.m_shared.m_hitEffect))
                return Plugin.DimWeapons.Value ? Source.Weapon : Source.None;
            if (projectile && ProjectileOwner(projectile) is Player && list == projectile.m_hitEffects)
                return Plugin.DimImpacts.Value ? Source.Impact : Source.None;
            if (area && AreaOwner(area) is Player && list == area.m_hitEffects)
                return Plugin.DimImpacts.Value ? Source.Impact : Source.None;
            return Source.None;
        }

        [HarmonyPatch(typeof(EffectList), nameof(EffectList.Create))]
        internal static class Created
        {
            private static void Postfix(EffectList __instance, GameObject[] __result)
            {
                if (__result == null || __result.Length == 0 || !Active) return;
                if (status == null && !hitCharacter && attack == null && !projectile && !area) return;
                if (Of(__instance) == Source.None) return;
                float opacity = Mathf.Clamp01(Plugin.DimOpacity.Value), brightness = Mathf.Clamp01(Plugin.DimBrightness.Value),
                    size = Mathf.Clamp(Plugin.DimSize.Value, 0.1f, 1f);
                foreach (var effect in __result)
                {
                    if (!effect) continue;
                    foreach (var system in effect.GetComponentsInChildren<ParticleSystem>(true)) Dim(system, opacity, brightness, size);
                    foreach (var light in effect.GetComponentsInChildren<Light>(true)) light.intensity *= opacity * brightness;
                }
            }
        }

        private static void Dim(ParticleSystem system, float opacity, float brightness, float size)
        {
            var main = system.main;
            main.startColor = Scale(main.startColor, opacity, brightness);
            main.startSizeMultiplier *= size;
        }

        private static Color Scale(Color color, float opacity, float brightness) =>
            new Color(color.r * brightness, color.g * brightness, color.b * brightness, color.a * opacity);

        private static Gradient Scale(Gradient gradient, float opacity, float brightness)
        {
            if (gradient == null) return null;
            var colors = gradient.colorKeys;
            for (int i = 0; i < colors.Length; i++) colors[i].color = Scale(colors[i].color, 1f, brightness);
            var alphas = gradient.alphaKeys;
            for (int i = 0; i < alphas.Length; i++) alphas[i].alpha *= opacity;
            var copy = new Gradient { mode = gradient.mode };
            copy.SetKeys(colors, alphas);
            return copy;
        }

        private static ParticleSystem.MinMaxGradient Scale(ParticleSystem.MinMaxGradient value, float opacity, float brightness)
        {
            switch (value.mode)
            {
                case ParticleSystemGradientMode.Color:
                    return new ParticleSystem.MinMaxGradient(Scale(value.color, opacity, brightness));
                case ParticleSystemGradientMode.TwoColors:
                    return new ParticleSystem.MinMaxGradient(Scale(value.colorMin, opacity, brightness), Scale(value.colorMax, opacity, brightness));
                case ParticleSystemGradientMode.Gradient:
                    return new ParticleSystem.MinMaxGradient(Scale(value.gradient, opacity, brightness));
                case ParticleSystemGradientMode.TwoGradients:
                    return new ParticleSystem.MinMaxGradient(Scale(value.gradientMin, opacity, brightness), Scale(value.gradientMax, opacity, brightness));
                case ParticleSystemGradientMode.RandomColor:
                    return new ParticleSystem.MinMaxGradient(Scale(value.gradient, opacity, brightness)) { mode = ParticleSystemGradientMode.RandomColor };
                default:
                    return value;
            }
        }
    }
}
