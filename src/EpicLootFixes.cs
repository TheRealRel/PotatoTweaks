// Potato Tweaks - fixes for Epic Loot enchantments. Only used when Epic Loot is installed.
// Adapted from EpicLoot Progression Fix 0.1.281 (riches-roll.cs, fire-rate.cs, noise-reduction.cs, ranged-skills.cs,
// mead-cooldown.cs, indestructible-durability.cs, bountiful-harvest.cs, crossbow-reload.cs, near-ground-jumps.cs),
// MIT License, Copyright (c) 2026 EpicLoot Progression Fix contributors. See LICENSE.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using EpicLoot;
using EpicLoot.Magic.MagicItemEffects;
using EpicLoot.MagicItemEffects;
using EpicLoot.MagicItemEffects.Shards;
using HarmonyLib;
using UnityEngine;

namespace PotatoTweaks
{
    internal static class EpicLootFixes
    {
        internal static void Enable(Action<Type> patch, Action<string, Action> run)
        {
            foreach (var type in new[] { typeof(RichesRoll), typeof(RichesTotal), typeof(FireRate), typeof(RangedSkills), typeof(ArmorDurabilityProtection),
                typeof(BrokenIndestructibleItems), typeof(RepairedIndestructibleItems), typeof(CrossbowReload), typeof(NearGroundJumps) })
                patch(type);
            run("Noise fix", NoiseReduction.Initialize);
            run("Meads Cooldown fix", MeadCooldown.Initialize);
            run("Bountiful Harvest fix", BountifulHarvestPicks.Initialize);
        }

        internal static readonly AccessTools.FieldRef<Character, ZNetView> CharacterView = AccessTools.FieldRefAccess<Character, ZNetView>("m_nview");
        internal static readonly AccessTools.FieldRef<Humanoid, Attack> CurrentAttack = AccessTools.FieldRefAccess<Humanoid, Attack>("m_currentAttack");
        internal static readonly AccessTools.FieldRef<SEMan, Character> StatusCharacter = AccessTools.FieldRefAccess<SEMan, Character>("m_character");
    }

    // ------------------------------------------------------------------ Riches
    // Epic Loot 0.14.13 compares its payout roll with a second random number and scales that roll only above a combined
    // Riches of 100%, so below it any Riches at all gave treasure on about half of all deaths and more Riches added nothing.
    // It also truncates each player's total to a whole percent and reuses one total for five seconds of deaths anywhere.
    // Use the combined Riches of the players within Epic Loot's 100 m as the chance, and above 100% raise the payout in
    // proportion. The treasure still comes from Epic Loot's configured table.
    [HarmonyPatch(typeof(Riches_CharacterDrop_GenerateDropList_Patch), "Postfix")]
    internal static class RichesRoll
    {
        private const float Radius = 100f;
        // Each game publishes its own player's unrounded total here.
        internal static readonly int TotalKey = "PotatoTweaks.Riches".GetStableHashCode();

        private static bool Prefix(CharacterDrop __0, ref List<KeyValuePair<GameObject, int>> __1)
        {
            if (__0 == null || __1 == null) return false;
            var players = new List<Player>();
            Player.GetPlayersInRange(__0.transform.position, Radius, players);
            float total = 0f;
            foreach (var player in players)
            {
                var view = EpicLootFixes.CharacterView(player);
                if (view != null && view.IsValid()) total += view.GetZDO().GetFloat(TotalKey);
            }
            float chance = total / 100f;
            if (chance <= 0f || chance < 1f && UnityEngine.Random.value >= chance) return false;

            var table = Riches_CharacterDrop_GenerateDropList_Patch.RichesTable;
            if (table.Count == 0)
            {
                Riches_CharacterDrop_GenerateDropList_Patch.UpdateRichesOnEffectSetup();
                table = Riches_CharacterDrop_GenerateDropList_Patch.RichesTable;
                if (table.Count == 0) return false;
            }
            float budget = UnityEngine.Random.value * 100f * Mathf.Max(1f, chance);
            var treasure = table.ElementAt(UnityEngine.Random.Range(0, table.Count));
            float ratio = budget / treasure.Value;
            int amount = ratio < 1f ? 0 : Mathf.RoundToInt(ratio);
            var prefab = treasure.Key;
            var cheapest = Riches_CharacterDrop_GenerateDropList_Patch.LowestCostEntry;
            if (amount == 0 && cheapest.Key != null)
            {
                amount = Mathf.RoundToInt(budget / cheapest.Value);
                prefab = cheapest.Key;
            }
            if (amount >= 1) __1.Add(new KeyValuePair<GameObject, int>(prefab, amount));
            return false;
        }
    }

    // Epic Loot publishes a player's Riches only when equipment changes, so augmenting worn gear or socketing it kept the
    // old value. Check the local player's total once a second and publish it when it changed.
    [HarmonyPatch(typeof(Player), "Update")]
    internal static class RichesTotal
    {
        private static float nextCheck;

        private static void Postfix(Player __instance)
        {
            if (__instance != Player.m_localPlayer || Time.time < nextCheck) return;
            nextCheck = Time.time + 1f;
            var view = EpicLootFixes.CharacterView(__instance);
            if (view == null || !view.IsValid()) return;
            float total = __instance.GetTotalActiveMagicEffectValue(MagicEffectType.Riches);
            var zdo = view.GetZDO();
            if (zdo.GetFloat(RichesRoll.TotalKey) != total) zdo.Set(RichesRoll.TotalKey, total);
        }
    }

    // ------------------------------------------------------------------ Fire Rate
    // Fire Rate only shortened the pause between bursts of an attack that fires several, which no bow, crossbow or staff does
    // except the Staff of Frost's stream, so it did nothing on them. It now also speeds up the attack animation of the weapon
    // it suits, as Attack speed does for melee: a bow's release and next nock, a crossbow's shot and every staff's cast.
    [HarmonyPatch(typeof(ModifyAttackSpeed_ApplyAnimationHandler_Patch), nameof(ModifyAttackSpeed_ApplyAnimationHandler_Patch.ModifyAttackSpeed))]
    internal static class FireRate
    {
        private static string EffectFor(ItemDrop.ItemData weapon)
        {
            if (weapon == null) return null;
            if (weapon.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Bow) return MagicEffectType.ModifyFireRate;
            var skill = weapon.m_shared.m_skillType;
            return skill == Skills.SkillType.ElementalMagic || skill == Skills.SkillType.BloodMagic ? MagicEffectType.ModifyMagicFireRate : null;
        }

        private static void Postfix(Character character, ref double __result)
        {
            if (!(character is Player player) || !player.InAttack()) return;
            string effect = EffectFor(EpicLootFixes.CurrentAttack(player)?.GetWeapon());
            if (effect != null) __result *= 1.0 + player.GetTotalActiveMagicEffectValue(effect, 0.01f);
        }
    }

    // ------------------------------------------------------------------ Noise
    // Noise added its fraction to the noise range in meters, so -30% turned a 30 m noise into 30.3 m. Reduce the range by
    // that share of the base noise instead, as the game's own status effects do, so -30% makes it 21 m; capped at 100%.
    internal static class NoiseReduction
    {
        internal static void Initialize()
        {
            var original = AccessTools.Method(typeof(ModifyNoise.ModifyNoise_SEMan_ModifyNoise_Patch), "Postfix");
            if (original == null) throw new InvalidOperationException("Epic Loot's Noise effect moved.");
            var harmony = Plugin.HarmonyInstance;
            harmony.Patch(original, prefix: new HarmonyMethod(typeof(NoiseReduction), nameof(SkipEpicLoot)));
            harmony.Patch(AccessTools.Method(typeof(SEMan), nameof(SEMan.ModifyNoise)), postfix: new HarmonyMethod(typeof(NoiseReduction), nameof(Reduce)));
        }

        private static bool SkipEpicLoot() => false;

        private static void Reduce(SEMan __instance, float baseNoise, ref float noise)
        {
            if (!(EpicLootFixes.StatusCharacter(__instance) is Player player)) return;
            float reduction = 0f;
            ModifyWithLowHealth.Apply(player, MagicEffectType.ModifyNoise, effect => reduction += player.GetTotalActiveMagicEffectValue(effect, 0.01f));
            if (reduction > 0f) noise = Mathf.Max(0f, noise - baseNoise * Mathf.Clamp01(reduction));
        }
    }

    // ------------------------------------------------------------------ +Ranged Skills
    // Epic Loot's skill bonus counted every skill effect except the ranged shardstone's +Ranged Skills, so it did nothing.
    // Add it to Bows and Crossbows like +Melee Skills.
    [HarmonyPatch(typeof(AddSkillLevel_Skills_GetSkillFactor_Patch), nameof(AddSkillLevel_Skills_GetSkillFactor_Patch.SkillIncrease))]
    internal static class RangedSkills
    {
        private static void Postfix(Player player, Skills.SkillType skillType, ref int __result)
        {
            if (player != null && (skillType == Skills.SkillType.Bows || skillType == Skills.SkillType.Crossbows))
                __result += (int)player.GetTotalActiveMagicEffectValue(MagicEffectType.IncreaseRangedSkills);
        }
    }

    // ------------------------------------------------------------------ Meads Cooldown
    // Meads Cooldown shortens a restoring mead's lifetime (its cooldown), but Instant Mead moved the restoration up front
    // first, after which Epic Loot no longer saw a restoring mead, and Potion Duration then lengthened the lifetime again,
    // which only lengthened the cooldown. Meads Cooldown now applies with Instant Mead too, and Potion Duration leaves
    // restoring meads alone; lasting buffs and resistances keep their longer duration.
    internal static class MeadCooldown
    {
        internal static void Initialize()
        {
            var modify = AccessTools.Method(typeof(ModifyMeads), "ModifyMead");
            var efficiencies = AccessTools.TypeByName("EpicLoot.src.Magic.MagicItemEffects.Shards.PotionEfficiencies");
            var duration = AccessTools.Method(AccessTools.Inner(AccessTools.Inner(efficiencies, "PotionEfficacy"), "Player_ConsumeItem_Patch"), "Postfix");
            if (modify == null || duration == null) throw new InvalidOperationException("Epic Loot's mead effects moved.");
            var harmony = Plugin.HarmonyInstance;
            harmony.Patch(modify, postfix: new HarmonyMethod(typeof(MeadCooldown), nameof(ShortenCooldown)));
            harmony.Patch(duration, prefix: new HarmonyMethod(typeof(MeadCooldown), nameof(LastingEffectsOnly)));
        }

        private static bool Restores(StatusEffect effect) =>
            effect is SE_Stats stats && (stats.m_healthOverTime > 0f || stats.m_staminaOverTime > 0f || stats.m_eitrOverTime > 0f);

        private static StatusEffect Prefab(StatusEffect effect) => ObjectDB.instance.GetStatusEffect(effect.NameHash()) ?? effect;

        private static void ShortenCooldown(StatusEffect original, ref StatusEffect __result)
        {
            var player = Player.m_localPlayer;
            if (player == null || original == null || !(__result is SE_Stats drink)) return;
            var prefab = Prefab(original);
            if (!Restores(prefab) || !player.HasActiveMagicEffect(MagicEffectType.DecreaseMeadCooldown, out float reduction, 0.01f)) return;
            float cooldown = prefab.m_ttl * Mathf.Clamp01(1f - reduction);
            if (Mathf.Approximately(drink.m_ttl, cooldown)) return;
            // The game adds a copy of the effect it is given, but the prefab itself must stay unchanged.
            if (drink == prefab || drink == original) __result = drink = (SE_Stats)drink.Clone();
            drink.m_ttl = cooldown;
        }

        private static bool LastingEffectsOnly(ItemDrop.ItemData item) =>
            item?.m_shared?.m_consumeStatusEffect == null || !Restores(Prefab(item.m_shared.m_consumeStatusEffect));
    }

    // ------------------------------------------------------------------ Indestructible
    // Armor hit by combat wore down even when Indestructible, and Indestructible switched off durability altogether, which
    // also skipped the game's broken-item and repair checks for gear that was already broken.
    [HarmonyPatch(typeof(Player), "DamageArmorDurability")]
    internal static class ArmorDurabilityProtection
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var durability = AccessTools.Field(typeof(ItemDrop.ItemData), nameof(ItemDrop.ItemData.m_durability));
            int writes = 0;
            foreach (var instruction in instructions)
            {
                if (!instruction.StoresField(durability)) { yield return instruction; continue; }
                writes++;
                yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ArmorDurabilityProtection), nameof(WriteDurability)))
                    .MoveLabelsFrom(instruction).MoveBlocksFrom(instruction);
            }
            if (writes != 1) throw new InvalidOperationException("The game's armor durability write moved.");
        }

        private static void WriteDurability(ItemDrop.ItemData item, float durability)
        {
            if (item.m_shared.m_useDurability) item.m_durability = durability;
        }
    }

    [HarmonyPatch(typeof(Indestructible), nameof(Indestructible.Sync))]
    internal static class BrokenIndestructibleItems
    {
        // Keep the broken-item and repair checks active until an actual repair restores positive durability.
        private static void Postfix(ItemDrop.ItemData item)
        {
            if (item != null && item.m_durability <= 0f && Indestructible.OriginallyUsesDurability(item))
                Indestructible.Revert(item);
        }
    }

    [HarmonyPatch(typeof(InventoryGui), "RepairOneItem")]
    internal static class RepairedIndestructibleItems
    {
        private static void Postfix()
        {
            var player = Player.m_localPlayer;
            if (player == null) return;
            foreach (var item in player.GetInventory().GetAllItems())
                if (item.HasMagicEffect(MagicEffectType.Indestructible)) Indestructible.Sync(item);
        }
    }

    // ------------------------------------------------------------------ Bountiful Harvest
    // Epic Loot rolled Bountiful Harvest only when the plant was already picked right after the interaction, which happens
    // only on the game that controls the plant, so picking another player's plants never gave the extra item. The extra
    // item now joins the pick request's bonus, so whichever game controls the plant drops it with the rest.
    internal static class BountifulHarvestPicks
    {
        internal static void Initialize()
        {
            var epicLoot = AccessTools.Method(AccessTools.Inner(typeof(BountifulHarvest), "Pickable_Interact_Patch"), "Postfix");
            if (epicLoot == null) throw new InvalidOperationException("Epic Loot's Bountiful Harvest moved.");
            var harmony = Plugin.HarmonyInstance;
            harmony.Patch(epicLoot, prefix: new HarmonyMethod(typeof(BountifulHarvestPicks), nameof(SkipEpicLoot)));
            harmony.Patch(AccessTools.Method(typeof(Pickable), nameof(Pickable.Interact)),
                transpiler: new HarmonyMethod(typeof(BountifulHarvestPicks), nameof(AddToPick)));
        }

        private static bool SkipEpicLoot() => false;

        // The bonus is the last value the interaction sends with its pick request.
        internal static IEnumerable<CodeInstruction> AddToPick(IEnumerable<CodeInstruction> instructions)
        {
            var matcher = new CodeMatcher(instructions).MatchStartForward(new CodeMatch(OpCodes.Ldstr, "RPC_Pick"))
                .MatchStartForward(new CodeMatch(OpCodes.Box, typeof(int)));
            if (matcher.IsInvalid) throw new InvalidOperationException("The game's pick request changed.");
            return matcher.InsertAndAdvance(new CodeInstruction(OpCodes.Ldarg_0), new CodeInstruction(OpCodes.Ldarg_1), new CodeInstruction(OpCodes.Ldarg_2),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(BountifulHarvestPicks), nameof(Bonus)))).InstructionEnumeration();
        }

        private static int Bonus(int bonus, Pickable pickable, Humanoid character, bool repeat)
        {
            var player = Player.m_localPlayer;
            if (repeat || player == null || character != player || pickable.GetPicked() || pickable.m_itemPrefab == null) return bonus;
            float chance = player.GetTotalActiveMagicEffectValue(MagicEffectType.BountifulHarvest, 0.01f);
            return chance > 0f && UnityEngine.Random.value < chance ? bonus + 1 : bonus;
        }
    }

    // ------------------------------------------------------------------ crossbow reload
    // With Quick Draw, Epic Loot replaced the game's skill-adjusted reload time with the raw base time, so Quick Draw could
    // make reloading slower. Reduce the game's own reload time instead.
    [HarmonyPatch(typeof(Quickdraw_Player_GetWeaponLoadingTime), "Postfix")]
    internal static class CrossbowReload
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var code = instructions.ToList();
            var duration = generator.DeclareLocal(typeof(float));
            var shared = AccessTools.Field(typeof(ItemDrop.ItemData), nameof(ItemDrop.ItemData.m_shared));
            var primary = AccessTools.Field(typeof(ItemDrop.ItemData.SharedData), nameof(ItemDrop.ItemData.SharedData.m_attack));
            var secondary = AccessTools.Field(typeof(ItemDrop.ItemData.SharedData), nameof(ItemDrop.ItemData.SharedData.m_secondaryAttack));
            var reloadTime = AccessTools.Field(typeof(Attack), nameof(Attack.m_reloadTime));
            int changes = 0;
            for (int index = 3; index < code.Count; index++)
            {
                if (!code[index].LoadsField(reloadTime) || code[index - 3].opcode != OpCodes.Ldarg_0 ||
                    !code[index - 2].LoadsField(shared) ||
                    !(code[index - 1].LoadsField(primary) || code[index - 1].LoadsField(secondary))) continue;
                code[index - 3] = new CodeInstruction(code[index - 3]) { opcode = OpCodes.Ldloc, operand = duration };
                code.RemoveRange(index - 2, 3);
                index -= 3;
                changes++;
            }
            if (changes != 2) throw new InvalidOperationException("Epic Loot's crossbow reload calculation changed.");
            code.InsertRange(0, new[] { new CodeInstruction(OpCodes.Ldarg_1), new CodeInstruction(OpCodes.Ldind_R4),
                new CodeInstruction(OpCodes.Stloc, duration) });
            return code;
        }
    }

    // ------------------------------------------------------------------ Air Jump near the ground
    // Epic Loot spent the Air Jump on any press the game counts as airborne: 0.2 s after leaving the ground, as over a crest
    // or bump, or on the physics step that touches down before the landing registers. Such a press is a ground jump
    // instead, so the Air Jump stays for the second press.
    [HarmonyPatch(typeof(Character), nameof(Character.Jump))]
    internal static class NearGroundJumps
    {
        private const float GroundRange = 1f;
        private static readonly AccessTools.FieldRef<Character, float> SinceGround = AccessTools.FieldRefAccess<Character, float>("m_lastGroundTouch");
        private static readonly AccessTools.FieldRef<Character, float> SinceJump = AccessTools.FieldRefAccess<Character, float>("m_jumpTimer");
        private static readonly AccessTools.FieldRef<Character, bool> Touching = AccessTools.FieldRefAccess<Character, bool>("m_groundContact");
        private static readonly AccessTools.FieldRef<Character, CapsuleCollider> Body = AccessTools.FieldRefAccess<Character, CapsuleCollider>("m_collider");
        private static readonly FieldInfo GroundMask = AccessTools.Field(typeof(Character), "s_groundRayMask");

        // Runs before Epic Loot's prefix, which reads IsOnGround to choose between the ground and the air jump.
        [HarmonyPriority(Priority.First)]
        private static void Prefix(Character __instance, out float __state)
        {
            __state = -1f;
            if (__instance != Player.m_localPlayer || __instance.IsOnGround() || GrapplingPoint.m_localGrappler != null) return;
            bool nearGround = Touching(__instance)
                || !__instance.IsSwimming() && SinceJump(__instance) > SinceGround(__instance) && GroundBelow(__instance);
            if (!nearGround || !Player.m_localPlayer.HasActiveMagicEffect(MagicEffectType.DoubleJump)) return;
            __state = SinceGround(__instance);
            SinceGround(__instance) = 0f;
        }

        // ForceJump sets the time since ground contact to 1 s; a press that launched nothing keeps its airborne time.
        private static void Postfix(Character __instance, float __state)
        {
            if (__state >= 0f && SinceGround(__instance) == 0f) SinceGround(__instance) = __state;
        }

        private static bool GroundBelow(Character character)
        {
            float radius = Body(character).radius * 0.9f;
            var from = character.transform.position + Vector3.up * (radius + 0.1f);
            return Physics.SphereCast(from, radius, Vector3.down, out _, GroundRange + 0.1f, (int)GroundMask.GetValue(null), QueryTriggerInteraction.Ignore);
        }
    }
}
