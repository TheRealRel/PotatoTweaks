// Potato Tweaks - quality of life.
// Grave slot recovery, no auto-equip on pickup, live hammer menu, health bars with build tools, tamed crop protection,
// combat health bars, taming hover, auto-pickup key, swim speed and crafted cart health are adapted from EpicLoot
// Progression Fix 0.1.281, MIT License, Copyright (c) 2026 EpicLoot Progression Fix contributors. See LICENSE.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace PotatoTweaks
{
    internal static class QualityOfLife
    {
        internal const string EquipmentAndQuickSlotsId = "randyknapp.mods.equipmentandquickslots";

        internal static readonly Type[] Patches =
        {
            typeof(NoAutoEquip), typeof(LiveBuildMenu), typeof(BuildToolHealthBars), typeof(TamedCropTrampling), typeof(GrownPlantCreator),
            typeof(CombatHealthBars), typeof(CombatTargets), typeof(CreatureHover), typeof(TamingHoverText), typeof(DistantTamingHover),
            typeof(SwimSpeed), typeof(CraftedVehicleHealth), typeof(CraftedVehicleHealth.Placement),
        };

        internal static readonly Type[] GravePatches =
            { typeof(GraveSlotRecovery.RecordRows), typeof(GraveSlotRecovery.AutomaticPickup), typeof(GraveSlotRecovery.TakeAll) };

        internal static readonly AccessTools.FieldRef<Character, ZNetView> CharacterView = AccessTools.FieldRefAccess<Character, ZNetView>("m_nview");
        internal static readonly AccessTools.FieldRef<Player, int> InteractMask = AccessTools.FieldRefAccess<Player, int>("m_interactMask");
        internal static readonly AccessTools.FieldRef<Aoe, Character> AoeOwner = AccessTools.FieldRefAccess<Aoe, Character>("m_owner");
    }

    // ------------------------------------------------------------------ no auto-equip on pickup
    // Valheim equips a picked-up weapon whenever the right hand is empty, so walking over a club or pickaxe put it in
    // the player's hands. Deliberate equipping and grave recovery use their own paths and are unchanged.
    [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.Pickup))]
    internal static class NoAutoEquip
    {
        private static void Prefix(Humanoid __instance, ref bool autoequip)
        {
            if (__instance is Player && Plugin.QolNoAutoEquip.Value) autoequip = false;
        }
    }

    // ------------------------------------------------------------------ live hammer menu
    // Valheim colors hammer buttons only when the menu opens, so walking into or out of workbench or chest range left them
    // showing the old requirements. Refresh the existing buttons five times a second while the menu is open.
    [HarmonyPatch(typeof(BuildUi), "Update")]
    internal static class LiveBuildMenu
    {
        private static float nextRefresh;

        private static void Postfix(List<BuildUiPieceButton> ___m_pieceButtons)
        {
            if (!Plugin.QolLiveBuildMenu.Value || ___m_pieceButtons == null || Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 0.2f;
            foreach (var button in ___m_pieceButtons)
                if (button && button.gameObject.activeInHierarchy) button.UpdateRequirements();
        }
    }

    // ------------------------------------------------------------------ health bars with build tools
    // Build tools clear the hover target, which also hid the aimed creature's health bar. Find the creature for the health
    // bar only; the hammer's placement and repair controls are unchanged.
    [HarmonyPatch(typeof(Player), nameof(Player.GetHoverCreature))]
    internal static class BuildToolHealthBars
    {
        private delegate void HoverSearch(Player player, out GameObject hoveredObject, out Character hoveredCreature);
        private static readonly HoverSearch FindHoverObject = AccessTools.MethodDelegate<HoverSearch>(AccessTools.Method(typeof(Player), "FindHoverObject"));

        private static void Postfix(Player __instance, ref Character __result)
        {
            if (!Plugin.QolBuildToolHealthBars.Value || __result || !__instance.InPlaceMode() || __instance.IsDead() ||
                __instance.GetDoodadController() != null) return;
            FindHoverObject(__instance, out _, out __result);
        }
    }

    // ------------------------------------------------------------------ tamed animals spare crops
    // A tame's movement damage leaves growing and mature farm crops and cultivator-planted plants alone. Trees and their
    // saplings, wild plants, wild animals and real attacks still do damage.
    [HarmonyPatch(typeof(Aoe), "OnHit")]
    internal static class TamedCropTrampling
    {
        private static ObjectDB cultivatorSource;
        private static readonly HashSet<string> CultivatorPieces = new HashSet<string>();

        private static bool Prefix(Aoe __instance, Collider collider, ref bool __result)
        {
            if (!Plugin.QolTamedCrops.Value) return true;
            var owner = QualityOfLife.AoeOwner(__instance);
            if (!owner || !owner.IsTamed()) return true;
            var movement = owner.GetComponent<MovementDamage>();
            if (!movement || movement.m_runDamageObject != __instance.gameObject) return true;
            var target = Projectile.FindHitObject(collider);
            if (!target) return true;
            var destructible = target.GetComponent<IDestructible>();
            if (destructible != null && (destructible.GetDestructibleType() & DestructibleType.Tree) != 0) return true;
            var plant = target.GetComponent<Plant>();
            var pickable = target.GetComponent<Pickable>();
            var piece = target.GetComponent<Piece>();
            if (!plant && !pickable && (!piece || !CultivatorPlant(target))) return true;
            if (plant)
                foreach (var grown in plant.m_grownPrefabs)
                    if (grown && grown.GetComponent<TreeBase>()) return true;
            var view = target.GetComponent<ZNetView>();
            long creator = piece ? piece.GetCreator() : view && view.IsValid() ? view.GetZDO().GetLong(ZDOVars.s_creator) : 0L;
            // Older mature crops lost their creator on growth; cultivated ground tells a farm from wild flowers and mushrooms.
            if (creator == 0L)
            {
                if (!pickable || !pickable.m_harvestable) return true;
                var ground = Heightmap.FindHeightmap(target.transform.position);
                if (!ground || !ground.IsCultivated(target.transform.position)) return true;
            }
            __result = false;
            return false;
        }

        private static bool CultivatorPlant(GameObject target)
        {
            if (cultivatorSource != ObjectDB.instance)
            {
                cultivatorSource = ObjectDB.instance;
                CultivatorPieces.Clear();
                var cultivator = cultivatorSource ? cultivatorSource.GetItemPrefab("Cultivator") : null;
                var tool = cultivator ? cultivator.GetComponent<ItemDrop>() : null;
                var pieces = tool ? tool.m_itemData.m_shared.m_buildPieces : null;
                if (pieces) foreach (var prefab in pieces.m_pieces) if (prefab) CultivatorPieces.Add(prefab.name);
            }
            return CultivatorPieces.Contains(Utils.GetPrefabName(target));
        }
    }

    // A grown crop keeps its planter, so it stays protected once mature.
    [HarmonyPatch(typeof(Plant), "Grow")]
    internal static class GrownPlantCreator
    {
        private static void Prefix(Plant __instance, out long __state)
        {
            var piece = __instance.GetComponent<Piece>();
            __state = piece ? piece.GetCreator() : 0L;
        }

        private static void Postfix(GameObject __result, long __state)
        {
            if (!__result || __state == 0L || __result.GetComponent<TreeBase>()) return;
            var view = __result.GetComponent<ZNetView>();
            if (view && view.IsValid()) view.GetZDO().Set(ZDOVars.s_creator, __state);
        }
    }

    // ------------------------------------------------------------------ combat health bars
    // Alerted enemies and animals being tamed (or tamed) keep their health bars through mist, trees and buildings; the
    // ground, rock and dungeon walls still hide them. An enemy attacking you within the set range shows through anything.
    [HarmonyPatch(typeof(EnemyHud), "UpdateHuds")]
    internal static class CombatHealthBars
    {
        private static readonly AccessTools.FieldRef<object, float> HoverTimer =
            AccessTools.FieldRefAccess<float>(AccessTools.Inner(typeof(EnemyHud), "HudData"), "m_hoverTimer");
        private static readonly AccessTools.FieldRef<object, GameObject> Display =
            AccessTools.FieldRefAccess<GameObject>(AccessTools.Inner(typeof(EnemyHud), "HudData"), "m_gui");
        private static readonly int ObstructionMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "terrain", "viewblock", "vehicle");
        private static RaycastHit[] obstructionHits = new RaycastHit[32];

        private static bool Obstructed(Vector3 origin, Vector3 destination)
        {
            var direction = destination - origin;
            int count = Physics.RaycastNonAlloc(origin, direction, obstructionHits, direction.magnitude, ObstructionMask, QueryTriggerInteraction.Ignore);
            while (count == obstructionHits.Length)
            {
                Array.Resize(ref obstructionHits, obstructionHits.Length * 2);
                count = Physics.RaycastNonAlloc(origin, direction, obstructionHits, direction.magnitude, ObstructionMask, QueryTriggerInteraction.Ignore);
            }
            for (int index = 0; index < count; index++)
            {
                var collider = obstructionHits[index].collider;
                if (collider.GetComponentInParent<Heightmap>()) return true;
                if (collider.GetComponentInParent<Piece>() || collider.GetComponentInParent<Plant>() || collider.GetComponentInParent<Pickable>()) continue;
                var destructible = collider.GetComponentInParent<IDestructible>();
                if (destructible != null && (destructible.GetDestructibleType() & DestructibleType.Tree) != 0) continue;
                return true;
            }
            return false;
        }

        private static void Prefix(EnemyHud __instance, Player player, IDictionary ___m_huds)
        {
            if (!Plugin.QolCombatHealthBars.Value) return;
            var camera = Utils.GetMainCamera();
            if (!player || player.IsDead() || !camera || ___m_huds == null) return;
            var playerId = player.GetZDOID();
            foreach (DictionaryEntry entry in ___m_huds)
            {
                var character = (Character)entry.Key;
                if (!character || character.IsPlayer() || character.IsBoss() || character.m_hideHud || character.IsDead()) continue;
                var ai = character.GetBaseAI();
                if (!ai || ai.IsSleeping() || !(ai.IsAlerted() && BaseAI.IsEnemy(player, character) || CreatureHover.KnownAnimal(character))) continue;
                var view = QualityOfLife.CharacterView(character);
                var data = view != null ? view.GetZDO() : null;
                bool attacking = !playerId.IsNone() && data != null && data.GetZDOID(CombatTargets.TargetKey) == playerId &&
                    Vector3.Distance(player.transform.position, character.transform.position) <= Plugin.QolAttackerRange.Value;
                bool obstructed = !attacking && Obstructed(camera.transform.position, character.GetCenterPoint()) &&
                    Obstructed(camera.transform.position, character.GetEyePoint());
                HoverTimer(entry.Value) = obstructed ? __instance.m_hoverShowDuration : 0f;
            }
        }

        // Standing right against a big animal can put its normal bar outside the view; keep it above the crosshair.
        private static void Postfix(Player player, IDictionary ___m_huds)
        {
            var character = CreatureHover.BodyHover;
            var camera = Utils.GetMainCamera();
            if (!Plugin.QolTamingHover.Value || !player || player.IsDead() || player.GetDoodadController() != null || !camera || !character ||
                character.IsDead() || character.IsBoss() || character.m_hideHud || player.GetHoverCreature() != character || ___m_huds == null ||
                Vector3.Distance(player.transform.position, character.transform.position) >= EnemyHud.instance.m_maxShowDistance ||
                !___m_huds.Contains(character)) return;
            var display = Display(___m_huds[character]);
            if (!display) return;
            display.transform.position = camera.WorldToScreenPointScaled(camera.transform.position + camera.transform.forward * 2f + camera.transform.up * 0.15f);
            display.SetActive(true);
        }
    }

    // The game shares only whether a creature has a target. Its owner also records which one, so each game can tell an
    // enemy attacking it from one attacking someone else.
    [HarmonyPatch(typeof(BaseAI), "SetTargetInfo")]
    internal static class CombatTargets
    {
        internal static readonly KeyValuePair<int, int> TargetKey = ZDO.GetHashZDOID("PotatoTweaks.CombatTarget");

        private static void Postfix(ZNetView ___m_nview, ZDOID targetID)
        {
            var data = ___m_nview != null ? ___m_nview.GetZDO() : null;
            if (data != null && data.GetZDOID(TargetKey) != targetID) data.Set(TargetKey, targetID);
        }
    }

    // ------------------------------------------------------------------ taming info at range
    // Aiming at an animal being tamed shows its taming percentage across the health-bar range, through mist. Petting and
    // renaming measure to the animal's nearest body surface, so a big animal works when you stand beside it.
    [HarmonyPatch(typeof(Player), "FindHoverObject")]
    internal static class CreatureHover
    {
        private static readonly AccessTools.FieldRef<Player, float> InteractionDistance = AccessTools.FieldRefAccess<Player, float>("m_maxInteractDistance");
        private static readonly int CreatureMask = LayerMask.GetMask("character", "character_net", "character_ghost");
        private static Collider[] overlapping = new Collider[16];
        private static RaycastHit[] rayHits = new RaycastHit[32];
        internal static Character BodyHover;

        internal static bool StartedTaming(Tameable animal)
        {
            var view = animal.GetComponent<ZNetView>();
            return view && view.IsValid() && view.GetZDO().GetFloat(ZDOVars.s_tameTimeLeft, animal.m_tamingTime) < animal.m_tamingTime;
        }

        internal static bool KnownAnimal(Character character)
        {
            var animal = character.GetComponent<Tameable>();
            return animal && (character.IsTamed() || StartedTaming(animal));
        }

        private static bool Available(Character character) => character && !character.IsPlayer() && !character.IsDead() &&
            character.GetHealth() > 0f && (!character.GetBaseAI() || !character.GetBaseAI().IsSleeping());

        private static void Postfix(Player __instance, ref GameObject hover, ref Character hoverCreature)
        {
            BodyHover = null;
            if (!Plugin.QolTamingHover.Value) return;
            var camera = Utils.GetMainCamera();
            if (!camera || __instance.IsDead()) return;
            var origin = camera.transform.position;
            var eye = __instance.GetEyePoint();
            int mask = QualityOfLife.InteractMask(__instance);
            int count = Physics.OverlapSphereNonAlloc(origin, 0.001f, overlapping, mask & CreatureMask, QueryTriggerInteraction.Ignore);
            while (count == overlapping.Length)
            {
                Array.Resize(ref overlapping, overlapping.Length * 2);
                count = Physics.OverlapSphereNonAlloc(origin, 0.001f, overlapping, mask & CreatureMask, QueryTriggerInteraction.Ignore);
            }
            float nearest = float.PositiveInfinity;
            Vector3 contact = default;
            for (int index = 0; index < count; index++)
            {
                var collider = overlapping[index];
                var character = collider.GetComponentInParent<Character>();
                if (!Available(character) || !character.GetComponent<Tameable>() || !Measurable(collider) ||
                    (collider.ClosestPoint(origin) - origin).sqrMagnitude > 0.00000001f) continue;
                var point = collider.ClosestPoint(eye);
                float distance = (point - eye).sqrMagnitude;
                if (distance >= nearest) continue;
                nearest = distance;
                contact = point;
                BodyHover = character;
            }
            // The game's rays skip the collider the camera is inside; bring back that animal in front of the player.
            if (BodyHover)
            {
                hoverCreature = BodyHover;
                hover = Vector3.Distance(eye, contact) < InteractionDistance(__instance) + BodyHover.GetHoverOffset() &&
                    !Physics.Linecast(eye, contact, mask & ~CreatureMask, QueryTriggerInteraction.Ignore) ? BodyHover.gameObject : null;
                return;
            }
            if (hover || !EnemyHud.instance || (hoverCreature && !KnownAnimal(hoverCreature))) return;
            float range = EnemyHud.instance.m_maxShowDistance;
            var ray = new Ray(origin, camera.transform.forward);
            float distanceLimit = range + Vector3.Distance(origin, __instance.transform.position);
            count = Physics.RaycastNonAlloc(ray, rayHits, distanceLimit, mask);
            while (count == rayHits.Length)
            {
                Array.Resize(ref rayHits, rayHits.Length * 2);
                count = Physics.RaycastNonAlloc(ray, rayHits, distanceLimit, mask);
            }
            RaycastHit first = default;
            nearest = float.PositiveInfinity;
            for (int index = 0; index < count; index++)
            {
                var hit = rayHits[index];
                if (hit.collider.GetComponentInParent<Player>() == __instance || hit.distance >= nearest) continue;
                first = hit;
                nearest = hit.distance;
            }
            if (!first.collider) return;
            var known = first.collider.GetComponentInParent<Character>();
            if (!Available(known) || !KnownAnimal(known) || Vector3.Distance(__instance.transform.position, known.transform.position) >= range) return;
            hoverCreature = known;
            contact = Measurable(first.collider) ? first.collider.ClosestPoint(eye) : first.point;
            if (Vector3.Distance(eye, contact) < InteractionDistance(__instance) + known.GetHoverOffset() &&
                !Physics.Linecast(eye, contact, mask & ~CreatureMask, QueryTriggerInteraction.Ignore))
            {
                hover = known.gameObject;
                BodyHover = known;
            }
        }

        // Physics.ClosestPoint measures only these shapes.
        private static bool Measurable(Collider collider) => collider is BoxCollider || collider is SphereCollider ||
            collider is CapsuleCollider || collider is MeshCollider mesh && mesh.convex;
    }

    [HarmonyPatch(typeof(Tameable), nameof(Tameable.GetHoverText))]
    internal static class TamingHoverText
    {
        private static readonly Func<Tameable, int> Tameness = AccessTools.MethodDelegate<Func<Tameable, int>>(AccessTools.Method(typeof(Tameable), "GetTameness"));

        internal static string Progress(Tameable animal) => animal.GetName() + Localization.instance.Localize(
            " ( $hud_tameness " + Tameness(animal) + "%, " + animal.GetStatusString() + " )");

        // The game shows nothing below 1%; show the progress as soon as taming starts.
        private static void Postfix(Tameable __instance, ref string __result)
        {
            if (Plugin.QolTamingHover.Value && !__instance.IsTamed() && CreatureHover.StartedTaming(__instance) && Tameness(__instance) == 0)
                __result = Progress(__instance);
        }
    }

    [HarmonyPatch(typeof(Hud), "UpdateCrosshair")]
    internal static class DistantTamingHover
    {
        private static void Postfix(Hud __instance, Player player)
        {
            if (!Plugin.QolTamingHover.Value || !player || player.IsDead() || player.GetDoodadController() != null || player.GetHoverObject() ||
                (TextViewer.instance && TextViewer.instance.IsVisible())) return;
            var creature = player.GetHoverCreature();
            var animal = creature ? creature.GetComponent<Tameable>() : null;
            if (animal && !animal.IsTamed() && CreatureHover.StartedTaming(animal) && EnemyHud.instance &&
                Vector3.Distance(player.transform.position, creature.transform.position) < EnemyHud.instance.m_maxShowDistance)
                __instance.m_hoverName.text = TamingHoverText.Progress(animal);
        }
    }

    // ------------------------------------------------------------------ swim speed
    // Swimming used a fixed speed while running scales with its skill. Apply the same curve to Swim: +25% at level 100.
    [HarmonyPatch(typeof(Character), "UpdateSwimming")]
    internal static class SwimSpeed
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
            Transpilers.CallInstead(instructions, x => x.LoadsField(AccessTools.Field(typeof(Character), nameof(Character.m_swimSpeed))),
                AccessTools.Method(typeof(SwimSpeed), nameof(WithSkill)), 1, "Valheim's swimming speed read changed.");

        private static float WithSkill(Character character) => character.m_swimSpeed *
            (Plugin.QolSwimSpeed.Value && character.IsPlayer() ? 1f + 0.25f * character.GetSkillFactor(Skills.SkillType.Swim) : 1f);
    }

    // ------------------------------------------------------------------ carts and boats from skilled crafters
    // A newly built cart or boat saves its builder's Crafting skill, and its health is raised by the set bonus times that
    // skill (0-100 % of the way) whenever it loads, so changing the bonus rescales existing ones too. Older carts and boats
    // keep their normal health.
    [HarmonyPatch(typeof(WearNTear), "Awake")]
    internal static class CraftedVehicleHealth
    {
        private const string CraftingKey = "PotatoTweaks.BuilderCrafting";

        private static float Bonus(WearNTear piece) =>
            piece.GetComponent<Vagon>() ? Plugin.CartHealthBonus.Value : piece.GetComponent<Ship>() ? Plugin.BoatHealthBonus.Value : -1f;

        [HarmonyPriority(Priority.First)]
        private static void Prefix(WearNTear __instance)
        {
            float bonus = Bonus(__instance);
            if (bonus <= 0f) return;
            var view = __instance.GetComponent<ZNetView>();
            if (view && view.GetZDO() is ZDO data && data.GetFloat(CraftingKey, out float skill))
                __instance.m_health *= 1f + bonus * Mathf.Clamp01(skill);
        }

        private static void OnPlaced(WearNTear piece)
        {
            float bonus = Bonus(piece);
            if (bonus < 0f) return;
            var data = piece.GetComponent<ZNetView>()?.GetZDO();
            var player = Player.m_localPlayer;
            if (data == null || player == null || data.GetFloat(CraftingKey, out _)) return;
            // The game's skill factor drops partial levels; keep them.
            float level = player.GetSkills().GetSkillList().FirstOrDefault(skill => skill.m_info.m_skill == Skills.SkillType.Crafting)?.m_level ?? 0f;
            player.GetSEMan().ModifySkillLevel(Skills.SkillType.Crafting, ref level);
            float factor = Mathf.Clamp01(level / 100f);
            data.Set(CraftingKey, factor);
            piece.m_health *= 1f + Mathf.Max(0f, bonus) * factor;
            data.Set(ZDOVars.s_health, piece.m_health);
        }

        [HarmonyPatch(typeof(Player), nameof(Player.PlacePiece))]
        internal static class Placement
        {
            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
                Transpilers.CallInstead(instructions, x => x.Calls(AccessTools.Method(typeof(WearNTear), nameof(WearNTear.OnPlaced))),
                    AccessTools.Method(typeof(Placement), nameof(Placed)), 1, "Valheim's piece placement callback changed.");

            private static void Placed(WearNTear piece)
            {
                piece.OnPlaced();
                try { OnPlaced(piece); }
                catch (Exception e) { Plugin.Log.LogWarning("Crafted vehicle health: " + e.Message); }
            }
        }
    }

    // ------------------------------------------------------------------ tombstone items back in their slots
    // Recovering your own grave adds each item at its saved slot, but anything picked up since death may hold that slot and
    // the grave item then lands elsewhere. Just before the transfer, carried items in grave slots move to free cells outside
    // the grave's layout, or join a matching grave stack. If that can't place them all, nothing moves and the inventory-full
    // message shows. With Equipment & Quick Slots, rows bought since death are accounted for.
    internal static class GraveSlotRecovery
    {
        private const string GraveRows = "PotatoTweaks.GraveRows";
        private static readonly Func<TombStone, bool> OwnGrave = AccessTools.MethodDelegate<Func<TombStone, bool>>(AccessTools.Method(typeof(TombStone), "IsOwner"));
        private static readonly Action<Inventory, bool, bool> Changed = AccessTools.MethodDelegate<Action<Inventory, bool, bool>>(AccessTools.Method(typeof(Inventory), "Changed"));
        private static readonly Action<Container> Save = AccessTools.MethodDelegate<Action<Container>>(AccessTools.Method(typeof(Container), "Save"));
        private static readonly Func<Vector2i, object> SlotAt;
        private static readonly Func<int> VisibleRows;
        private static readonly MethodInfo ItemFits;
        private static readonly PropertyInfo Active, QuickSlot, EquipmentSlot;

        static GraveSlotRecovery()
        {
            if (!Chainloader.PluginInfos.TryGetValue(QualityOfLife.EquipmentAndQuickSlotsId, out var plugin)) return;
            var slots = plugin.Instance.GetType().Assembly.GetType("EquipmentAndQuickSlots.Slots", true);
            var slot = slots.GetNestedType("Slot");
            var slotAt = AccessTools.Method(slots, "GetSlotInGrid", new[] { typeof(Vector2i) });
            var visibleRows = AccessTools.PropertyGetter(slots, "VisibleRows");
            ItemFits = slot == null ? null : AccessTools.Method(slot, "ItemFits");
            Active = slot == null ? null : AccessTools.Property(slot, "IsActive");
            QuickSlot = slot == null ? null : AccessTools.Property(slot, "IsQuickSlot");
            EquipmentSlot = slot == null ? null : AccessTools.Property(slot, "IsEquipmentSlot");
            if (slotAt == null || visibleRows == null || ItemFits == null || Active == null || QuickSlot == null || EquipmentSlot == null)
                throw new InvalidOperationException("Equipment & Quick Slots' slot layout changed.");
            SlotAt = AccessTools.MethodDelegate<Func<Vector2i, object>>(slotAt);
            VisibleRows = AccessTools.MethodDelegate<Func<int>>(visibleRows);
        }

        [HarmonyPatch(typeof(TombStone), nameof(TombStone.Setup))]
        internal static class RecordRows
        {
            private static void Postfix(long ownerUID, ZNetView ___m_nview)
            {
                if (VisibleRows != null && ___m_nview != null && ___m_nview.IsValid() && Game.instance && ownerUID == Game.instance.GetPlayerProfile().GetPlayerID())
                    ___m_nview.GetZDO().Set(GraveRows, VisibleRows());
            }
        }

        [HarmonyPatch(typeof(Container), "RPC_TakeAllResponse")]
        internal static class AutomaticPickup
        {
            [HarmonyPriority(Priority.Last)]
            private static bool Prefix(Container __instance, bool granted, bool __runOriginal) =>
                __runOriginal && (!granted || !Plugin.QolGraveSlots.Value || !Player.m_localPlayer || Prepare(__instance));
        }

        [HarmonyPatch(typeof(InventoryGui), "OnTakeAll")]
        internal static class TakeAll
        {
            [HarmonyPriority(Priority.Last)]
            private static bool Prefix(Container ___m_currentContainer, bool __runOriginal) =>
                __runOriginal && (!Plugin.QolGraveSlots.Value || !___m_currentContainer || !Player.m_localPlayer ||
                    Player.m_localPlayer.IsTeleporting() || Prepare(___m_currentContainer));
        }

        // False shows the inventory-full message and leaves the carried items and the grave unchanged.
        private static bool Prepare(Container container)
        {
            var grave = container.GetComponent<TombStone>();
            if (!grave || !OwnGrave(grave)) return true;
            try
            {
                var record = grave.GetComponent<ZNetView>().GetZDO();
                int deathRows = record.GetInt(GraveRows), bought = VisibleRows != null && deathRows > 0 ? VisibleRows() - deathRows : 0;
                if (bought > 0) Lower(container.GetInventory(), deathRows, bought);
                if (!MakeRoom(container))
                {
                    if (bought > 0) Lower(container.GetInventory(), deathRows + bought, -bought);
                    return false;
                }
                if (bought > 0)
                {
                    record.Set(GraveRows, deathRows + bought);
                    Save(container);
                }
            }
            catch (Exception e) { Plugin.Log.LogWarning("Grave slot recovery skipped: " + e.Message); }
            return true;
        }

        private static void Lower(Inventory grave, int firstSlotRow, int rows)
        {
            foreach (var item in grave.GetAllItems())
                if (item.m_gridPos.y >= firstSlotRow) item.m_gridPos = new Vector2i(item.m_gridPos.x, item.m_gridPos.y + rows);
        }

        private static bool MakeRoom(Container container)
        {
            var player = Player.m_localPlayer;
            var inventory = player.GetInventory();
            var layout = new Dictionary<Vector2i, ItemDrop.ItemData>();
            int unplaced = 0;
            foreach (var item in container.GetInventory().GetAllItems())
            {
                var cell = item.m_gridPos;
                if (cell.x >= 0 && cell.y >= 0 && cell.x < inventory.GetWidth() && cell.y < inventory.GetHeight()
                    && !layout.ContainsKey(cell) && Fits(cell, item)) layout[cell] = item;
                else unplaced++;
            }
            var blocking = inventory.GetAllItems().Where(item => layout.ContainsKey(item.m_gridPos))
                .OrderBy(item => item.m_gridPos.y).ThenBy(item => item.m_gridPos.x).ToList();
            if (blocking.Count == 0) return true;

            var free = new List<Vector2i>();
            for (int y = 0; y < inventory.GetHeight(); y++)
                for (int x = 0; x < inventory.GetWidth(); x++)
                {
                    var cell = new Vector2i(x, y);
                    if (!layout.ContainsKey(cell) && inventory.GetItemAt(x, y) == null && Ordinary(cell)) free.Add(cell);
                }
            int spare = free.Count - unplaced;
            var joining = new Dictionary<ItemDrop.ItemData, Vector2i>();
            foreach (var item in blocking)
            {
                if (blocking.Count - joining.Count <= spare) break;
                foreach (var home in layout)
                    if ((home.Key == item.m_gridPos || inventory.GetItemAt(home.Key.x, home.Key.y) == null)
                        && !joining.ContainsValue(home.Key) && Joins(item, home.Value))
                    {
                        joining[item] = home.Key;
                        break;
                    }
            }
            if (blocking.Count - joining.Count > spare)
            {
                player.Message(MessageHud.MessageType.Center, "$inventory_full");
                return false;
            }
            int next = 0;
            foreach (var item in blocking)
            {
                if (!joining.TryGetValue(item, out var cell)) cell = free[next++];
                // Worn armor can't stay worn outside its slot; Equipment & Quick Slots equips the grave's own piece.
                if (cell != item.m_gridPos && player.IsItemEquiped(item) && InEquipmentSlot(item.m_gridPos)) player.UnequipItem(item, false);
                item.m_gridPos = cell;
            }
            Changed(inventory, false, false);
            return true;
        }

        // The grave stacks an item onto a matching carried one only when it fits whole there.
        private static bool Joins(ItemDrop.ItemData carried, ItemDrop.ItemData returning) =>
            carried.m_shared.m_maxStackSize > 1 && carried.m_shared.m_name == returning.m_shared.m_name &&
            carried.m_quality == returning.m_quality && carried.m_worldLevel == returning.m_worldLevel &&
            carried.m_stack + returning.m_stack <= carried.m_shared.m_maxStackSize;

        private static bool Fits(Vector2i cell, ItemDrop.ItemData item)
        {
            var slot = SlotAt?.Invoke(cell);
            return slot == null || (bool)ItemFits.Invoke(slot, new object[] { item });
        }

        private static bool Ordinary(Vector2i cell)
        {
            var slot = SlotAt?.Invoke(cell);
            return slot == null || (bool)QuickSlot.GetValue(slot) && (bool)Active.GetValue(slot);
        }

        private static bool InEquipmentSlot(Vector2i cell)
        {
            var slot = SlotAt?.Invoke(cell);
            return slot != null && (bool)EquipmentSlot.GetValue(slot);
        }
    }

    // ------------------------------------------------------------------ auto-pickup key with Equipment & Quick Slots
    // Equipment & Quick Slots blocks V's auto-pickup toggle only while its quick slot holds an item, so pressing V on an
    // empty slot switched auto-pickup off. When a quick slot uses V, auto-pickup moves to ] (right bracket).
    internal static class AutoPickupKey
    {
        private static readonly ConfigEntry<KeyboardShortcut>[] quickSlotKeys = new ConfigEntry<KeyboardShortcut>[6];

        internal static void Initialize()
        {
            if (!Plugin.QolAutoPickupKey.Value || !Chainloader.PluginInfos.TryGetValue(QualityOfLife.EquipmentAndQuickSlotsId, out var slots)) return;
            for (int index = 0; index < quickSlotKeys.Length; index++)
                if (!slots.Instance.Config.TryGetEntry("Hotkeys", $"Quick slot hotkey {index + 1}", out quickSlotKeys[index]))
                    throw new InvalidOperationException("Equipment & Quick Slots' hotkey settings moved.");
            foreach (string name in new[] { "Load", "Reset", "ResetToDefault", "OnRebindComplete" })
            {
                var method = AccessTools.Method(typeof(ZInput), name);
                if (method == null) throw new MissingMethodException(typeof(ZInput).FullName, name);
                Plugin.HarmonyInstance.Patch(method, postfix: new HarmonyMethod(typeof(AutoPickupKey), nameof(SeparateFromQuickSlot)));
            }
            if (ZInput.instance != null) SeparateFromQuickSlot(ZInput.instance);
        }

        private static void SeparateFromQuickSlot(ZInput __instance)
        {
            if (__instance == null || !quickSlotKeys.Any(key => key != null && key.Value.MainKey == KeyCode.V)) return;
            var button = __instance.GetButtonDef("AutoPickup");
            if (button != null && string.Equals(button.GetActionPath(true), "<Keyboard>/v", StringComparison.OrdinalIgnoreCase))
                button.Rebind("<Keyboard>/rightBracket");
        }
    }

    // ------------------------------------------------------------------ tree-felling dust
    // Scales the particles of trees and logs being hit, falling, landing and breaking: 1 = the game's normal dust, 0 = none.
    // Sounds are kept. Only changes what you see.
    [HarmonyPatch]
    internal static class TreeDust
    {
        [ThreadStatic] private static int scope;

        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(TreeBase), "RPC_Damage");
            yield return AccessTools.Method(typeof(TreeLog), "RPC_Damage");
            yield return AccessTools.Method(typeof(TreeLog), "Destroy");
        }

        private static void Prefix() => scope++;
        private static void Finalizer() => scope--;

        // Logs landing and rolling: their impact effect, only on tree logs.
        [HarmonyPatch(typeof(ImpactEffect), "OnCollisionEnter")]
        internal static class Impact
        {
            private static void Prefix(ImpactEffect __instance, out bool __state)
            {
                __state = __instance && __instance.GetComponent<TreeLog>();
                if (__state) scope++;
            }

            private static void Finalizer(bool __state)
            {
                if (__state) scope--;
            }
        }

        [HarmonyPatch(typeof(EffectList), nameof(EffectList.Create))]
        internal static class Created
        {
            private static void Postfix(GameObject[] __result)
            {
                if (scope <= 0 || __result == null) return;
                float factor = Mathf.Clamp01(Plugin.QolTreeDust.Value);
                if (factor >= 0.999f) return;
                foreach (var effect in __result)
                    if (effect) foreach (var system in effect.GetComponentsInChildren<ParticleSystem>(true)) Scale(system, factor);
            }
        }

        private static void Scale(ParticleSystem system, float factor)
        {
            if (factor <= 0.001f)
            {
                var off = system.emission;
                off.enabled = false;
                system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                return;
            }
            var main = system.main;
            main.maxParticles = Mathf.Max(1, Mathf.RoundToInt(main.maxParticles * factor));
            var emission = system.emission;
            emission.rateOverTimeMultiplier *= factor;
            emission.rateOverDistanceMultiplier *= factor;
            for (int index = 0; index < emission.burstCount; index++)
            {
                var burst = emission.GetBurst(index);
                burst.count = new ParticleSystem.MinMaxCurve(burst.count.constantMin * factor, burst.count.constantMax * factor);
                emission.SetBurst(index, burst);
            }
        }
    }

    // ------------------------------------------------------------------ smelters, kilns and furnaces
    // Capacity multiplies how much ore (or wood, flax, barley, sap) and fuel a smelter-type station holds; speed divides the
    // time per product. Applies to the smelter, blast furnace, charcoal kiln, eitr refinery, spinning wheel and windmill as
    // they load.
    [HarmonyPatch(typeof(Smelter), "Awake")]
    internal static class SmelterSettings
    {
        private static void Prefix(Smelter __instance)
        {
            float capacity = Mathf.Max(0.1f, Plugin.SmelterCapacity.Value), speed = Mathf.Max(0.1f, Plugin.SmelterSpeed.Value);
            __instance.m_maxOre = Mathf.Max(1, Mathf.RoundToInt(__instance.m_maxOre * capacity));
            if (__instance.m_maxFuel > 0) __instance.m_maxFuel = Mathf.Max(1, Mathf.RoundToInt(__instance.m_maxFuel * capacity));
            __instance.m_secPerProduct /= speed;
        }
    }

    // ------------------------------------------------------------------ fermenters
    // Speed divides the fermenting time; output multiplies the meads each batch gives (rounded, at least 1).
    [HarmonyPatch(typeof(Fermenter), "Awake")]
    internal static class FermenterSettings
    {
        private static void Prefix(Fermenter __instance)
        {
            __instance.m_fermentationDuration /= Mathf.Max(0.1f, Plugin.FermenterSpeed.Value);
            float output = Mathf.Max(0.1f, Plugin.FermenterOutput.Value);
            if (Mathf.Approximately(output, 1f) || __instance.m_conversion == null) return;
            // Each fermenter has its own copy of the recipe list, so this never stacks.
            foreach (var conversion in __instance.m_conversion)
                if (conversion != null) conversion.m_producedItems = Mathf.Max(1, Mathf.RoundToInt(conversion.m_producedItems * output));
        }
    }

    // ------------------------------------------------------------------ crafting station range
    // Multiplies a workbench, forge or other crafting station's build range, extensions' bonus range included.
    [HarmonyPatch(typeof(CraftingStation), "GetExtensions")]
    internal static class StationRange
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
            Transpilers.CallInstead(instructions, x => x.LoadsField(AccessTools.Field(typeof(CraftingStation), nameof(CraftingStation.m_rangeBuild))),
                AccessTools.Method(typeof(StationRange), nameof(Range)), 1, "Valheim's crafting station range changed.");

        private static float Range(CraftingStation station) => station.m_rangeBuild * Mathf.Max(0.1f, Plugin.StationRange.Value);
    }

    // ------------------------------------------------------------------ map reveal radius
    // Multiplies how far around you the map is revealed as you explore (game: 100 m).
    [HarmonyPatch(typeof(Minimap), "UpdateExplore")]
    internal static class MapReveal
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
            Transpilers.CallInstead(instructions, x => x.LoadsField(AccessTools.Field(typeof(Minimap), nameof(Minimap.m_exploreRadius))),
                AccessTools.Method(typeof(MapReveal), nameof(Radius)), 1, "Valheim's map explore radius changed.");

        private static float Radius(Minimap map) => map.m_exploreRadius * Mathf.Max(0.1f, Plugin.MapRevealRadius.Value);
    }
}
