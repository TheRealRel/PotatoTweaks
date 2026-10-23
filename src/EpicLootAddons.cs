// Potato Tweaks - Epic Loot add-ons: the enchanting table using nearby chests, damage enchantments that stack, normal
// Double/Triple Shot cost, the summon limit with Double Magic Shot, Lucky Loot messages and Lucky Loot on birds, the eitr
// cost tooltip, faster chest loading, shardstones that ignore effect clashes and hiding enchantment looks.
// Written from scratch for this mod; the same features were in DigiTeam's Epic Loot Add-ons, which Potato Tweaks
// replaces. Only used when Epic Loot is installed.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;
using BepInEx.Bootstrap;
using EpicLoot;
using EpicLoot.MagicItemEffects;
using EpicLoot_UnityLib;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace PotatoTweaks
{
    internal static class EpicLootAddons
    {
        internal const string OriginalModId = "digi.valheim.epiclootaddons";
        internal const string OriginalModName = "Epic Loot Add-ons";

        // The separate mods Epic Loot Add-ons was made of, under their old and new names.
        private static readonly string[] SeparateMods =
        {
            "digi.valheim.epiclootstoragematerials", "digi.valheim.epiclootfixes", "digi.valheim.enchantglowtoggle", "digi.valheim.shardexclusivity",
            "pieter.valheim.epiclootstoragematerials", "pieter.valheim.epiclootfixes", "pieter.valheim.enchantglowtoggle", "pieter.valheim.shardexclusivity",
        };

        internal static string InstalledOriginal()
        {
            if (Chainloader.PluginInfos.ContainsKey(OriginalModId)) return OriginalModName;
            foreach (var id in SeparateMods)
                if (Chainloader.PluginInfos.TryGetValue(id, out var info)) return info.Metadata.Name;
            return null;
        }

        internal static void Enable(Action<Type> patch, Action<string, Action> run)
        {
            run("Enchanting table chests", ChestMaterials.Initialize);
            patch(typeof(ChestMaterials.Track));
            patch(typeof(ChestMaterials.SacrificeList));
            patch(typeof(ChestMaterials.Checkboxes));
            patch(typeof(SacrificeDoubles));
            run("Damage stacking and Double/Triple Shot cost", EffectRules.Initialize);
            patch(typeof(EffectRules.Reloaded));
            patch(typeof(SummonLimit));
            patch(typeof(ShardClashes));
            patch(typeof(LuckyLootMessage));
            patch(typeof(LuckyLootMessage.Register));
            patch(typeof(LuckyBirds));
            patch(typeof(LuckyBirds.Drops));
            patch(typeof(EitrTooltip));
            // Epic Loot 0.15.0 skips unchanged chests itself.
            if (!EpicLootFeatures.EpicLootHasFixes)
            {
                patch(typeof(FasterChestLoading));
                patch(typeof(FasterChestLoading.Loaded));
            }
            patch(typeof(EnchantLooks.Effects));
            patch(typeof(EnchantLooks.Textures));
            EnchantLooks.Watch();
        }
    }

    // ------------------------------------------------------------------ enchanting table and nearby chests
    // Epic Loot asks registered providers for materials beyond the player's inventory. Answer with the chests near the
    // player: their materials count for every enchanting table cost, and their trophies (and, if wanted, shardstones) show
    // in the Sacrifice tab. Only chests this game owns and nobody has open are used, so two games never take from one chest.
    internal static class ChestMaterials
    {
        private const string ProviderId = "potato.tweaks.nearbychests";
        private static readonly List<Container> Known = new List<Container>();
        private static readonly List<Container> Usable = new List<Container>();
        private static int usableFrame = -1;
        private static bool listingSacrifice;
        private static readonly Func<Container, long, bool> CheckAccess =
            AccessTools.MethodDelegate<Func<Container, long, bool>>(AccessTools.Method(typeof(Container), "CheckAccess", new[] { typeof(long) }));

        internal static void Initialize()
        {
            if (!API.RegisterInventoryProvider(ProviderId, ChestItems, CountItem, RemoveItem, RemoveExactItem))
                throw new Exception("Epic Loot refused the chest provider");
        }

        [HarmonyPatch(typeof(Container), "Awake")]
        internal static class Track
        {
            private static void Postfix(Container __instance) => Known.Add(__instance);
        }

        // Trophies and shardstones from chests only belong in the Sacrifice tab's list, nowhere else Epic Loot lists items.
        [HarmonyPatch]
        internal static class SacrificeList
        {
            private static MethodBase TargetMethod() =>
                AccessTools.Method(AccessTools.TypeByName("EpicLoot.CraftingV2.EnchantingUIController"), "GetSacrificeItems");

            private static void Prefix() => listingSacrifice = true;
            private static void Finalizer() => listingSacrifice = false;
        }

        private static List<Container> Chests()
        {
            if (usableFrame == Time.frameCount) return Usable;
            usableFrame = Time.frameCount;
            Usable.Clear();
            var player = Player.m_localPlayer;
            if (!player || !Plugin.ChestMaterialsEnabled.Value) return Usable;
            Known.RemoveAll(chest => !chest);
            Vector3 origin = player.transform.position;
            float range = Plugin.ChestMaterialsRange.Value * Plugin.ChestMaterialsRange.Value;
            long id = Game.instance.GetPlayerProfile().GetPlayerID();
            foreach (var chest in Known)
                if ((chest.transform.position - origin).sqrMagnitude <= range && CanUse(chest, id)) Usable.Add(chest);
            Usable.Sort((a, b) => (a.transform.position - origin).sqrMagnitude.CompareTo((b.transform.position - origin).sqrMagnitude));
            return Usable;
        }

        private static bool CanUse(Container chest, long playerId)
        {
            if (!chest.isActiveAndEnabled || chest.GetInventory() == null || !chest.IsOwner() || chest.IsInUse()) return false;
            // Built chests only: not dungeon chests, carts or ship storage.
            var piece = chest.GetComponentInParent<Piece>();
            if (!piece || !piece.IsPlacedByPlayer() || chest.m_wagon || chest.GetComponentInParent<Ship>()) return false;
            if (chest.m_checkGuardStone && !PrivateArea.CheckAccess(chest.transform.position, 0f, false)) return false;
            return CheckAccess(chest, playerId);
        }

        private static List<ItemDrop.ItemData> ChestItems()
        {
            var items = new List<ItemDrop.ItemData>();
            if (!listingSacrifice) return items;
            bool trophies = Plugin.ChestTrophies.Value, shardstones = Plugin.ChestShardstones.Value;
            if (!trophies && !shardstones) return items;
            foreach (var chest in Chests())
                foreach (var item in chest.GetInventory().GetAllItems())
                {
                    if (item?.m_shared == null) continue;
                    if (trophies && item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Trophy) items.Add(item);
                    else if (shardstones && item.IsShardStone()) items.Add(item);
                }
            return items;
        }

        private static int CountItem(string name)
        {
            int count = 0;
            foreach (var chest in Chests()) count += chest.GetInventory().CountItems(name);
            return count;
        }

        private static int RemoveItem(string name, int amount)
        {
            int removed = 0;
            foreach (var chest in Chests())
            {
                if (removed >= amount) break;
                var inventory = chest.GetInventory();
                int had = inventory.CountItems(name);
                if (had == 0) continue;
                inventory.RemoveItem(name, Mathf.Min(had, amount - removed));
                removed += had - inventory.CountItems(name);
            }
            return removed;
        }

        private static int RemoveExactItem(ItemDrop.ItemData item, int amount)
        {
            if (item == null || amount <= 0) return 0;
            foreach (var chest in Chests())
            {
                var inventory = chest.GetInventory();
                if (!inventory.ContainsItem(item)) continue;
                int had = item.m_stack;
                inventory.RemoveItem(item, Mathf.Min(amount, had));
                return had - (inventory.ContainsItem(item) ? item.m_stack : 0);
            }
            return 0;
        }

        // "Include trophies" and "Include shardstones" checkboxes above the Sacrifice tab's "Select All".
        [HarmonyPatch(typeof(SacrificeUI), nameof(SacrificeUI.OnEnable))]
        internal static class Checkboxes
        {
            private static readonly MethodInfo Refresh = AccessTools.Method(typeof(SacrificeUI), "RefreshAvailableItems");

            private static void Postfix(SacrificeUI __instance)
            {
                var selectAll = __instance.AvailableItems ? __instance.AvailableItems.SelectAllToggle : null;
                if (!selectAll) return;
                bool shown = Plugin.ChestMaterialsEnabled.Value;
                Add(__instance, selectAll, "PotatoTweaks_ChestTrophies", "Include trophies from chests", 1, Plugin.ChestTrophies, shown);
                Add(__instance, selectAll, "PotatoTweaks_ChestShardstones", "Include shardstones from chests", 2, Plugin.ChestShardstones, shown);
            }

            private static void Add(SacrificeUI ui, Toggle selectAll, string name, string label, int row, BepInEx.Configuration.ConfigEntry<bool> setting, bool shown)
            {
                var parent = selectAll.transform.parent;
                var existing = parent.Find(name);
                if (!shown)
                {
                    if (existing) existing.gameObject.SetActive(false);
                    return;
                }
                Toggle toggle;
                if (existing)
                {
                    existing.gameObject.SetActive(true);
                    toggle = existing.GetComponent<Toggle>();
                }
                else
                {
                    var copy = UnityEngine.Object.Instantiate(selectAll.gameObject, parent, false);
                    copy.name = name;
                    var source = (RectTransform)selectAll.transform;
                    ((RectTransform)copy.transform).anchoredPosition = source.anchoredPosition + new Vector2(0f, row * (source.rect.height + 4f));
                    foreach (var text in copy.GetComponentsInChildren<Text>(true)) text.text = label;
                    toggle = copy.GetComponent<Toggle>();
                    toggle.onValueChanged = new Toggle.ToggleEvent();
                    toggle.interactable = true;
                    toggle.onValueChanged.AddListener(on =>
                    {
                        setting.Value = on;
                        Refresh?.Invoke(ui, null);
                    });
                }
                if (toggle) toggle.SetIsOnWithoutNotify(setting.Value);
            }
        }
    }

    // ------------------------------------------------------------------ Sacrifice: double output per item
    // The table's double-output chance is rolled once for the whole batch: everything doubles or nothing does. Roll it for
    // every sacrificed item instead; the average stays the same.
    [HarmonyPatch(typeof(SacrificeUI), "SacrificeItems")]
    internal static class SacrificeDoubles
    {
        private static readonly MethodInfo ProductsMethod = AccessTools.Method(AccessTools.TypeByName("EpicLoot.CraftingV2.EnchantingUIController"),
            "GetSacrificeProducts", new[] { typeof(List<Tuple<ItemDrop.ItemData, int>>) });
        private static readonly MethodInfo FeatureValue = AccessTools.Method(typeof(EnchantingTable), nameof(EnchantingTable.GetFeatureCurrentValue));

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            if (ProductsMethod == null) throw new Exception("Epic Loot's sacrifice products were not found");
            var code = instructions.ToList();
            int products = code.FindIndex(i => i.Calls(ProductsMethod));
            int feature = code.FindIndex(products + 1, i => i.Calls(FeatureValue));
            if (products < 0 || feature < 0) throw new Exception("Epic Loot's sacrifice code has changed");
            // The batch roll sees no chance while this setting is on; the products are rolled per item instead.
            code[feature] = new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(SacrificeDoubles), nameof(BatchChance))).MoveLabelsFrom(code[feature]);
            code[products] = new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(SacrificeDoubles), nameof(Products))).MoveLabelsFrom(code[products]);
            code.Insert(products, new CodeInstruction(OpCodes.Ldarg_0));
            return code;
        }

        private static Tuple<float, float> BatchChance(EnchantingTable table, EnchantingFeature feature) =>
            Plugin.SacrificePerItem.Value ? Tuple.Create(float.NaN, float.NaN) : table.GetFeatureCurrentValue(feature);

        private static Func<List<Tuple<ItemDrop.ItemData, int>>, List<InventoryItemListElement>> make;

        private static List<InventoryItemListElement> Products(List<Tuple<ItemDrop.ItemData, int>> items, SacrificeUI ui)
        {
            if (make == null) make = AccessTools.MethodDelegate<Func<List<Tuple<ItemDrop.ItemData, int>>, List<InventoryItemListElement>>>(ProductsMethod);
            var table = EnchantingTableUI.instance ? EnchantingTableUI.instance.SourceTable : null;
            if (!Plugin.SacrificePerItem.Value || !table) return make(items);
            float value = table.GetFeatureCurrentValue(EnchantingFeature.Sacrifice).Item1;
            float chance = float.IsNaN(value) ? 0f : value / 100f;
            bool doubled = false;
            var rolled = new List<Tuple<ItemDrop.ItemData, int>>(items.Count);
            foreach (var entry in items)
            {
                int extra = 0;
                for (int i = 0; i < entry.Item2; i++)
                    if (UnityEngine.Random.value < chance) extra++;
                doubled |= extra > 0;
                rolled.Add(Tuple.Create(entry.Item1, entry.Item2 + extra));
            }
            var products = make(rolled);
            if (doubled && products.Count > 0)
            {
                EnchantingTableUI.instance.PlayEnchantBonusSFX();
                if (ui.BonusPanel) ui.BonusPanel.Show();
            }
            return products;
        }
    }

    // ------------------------------------------------------------------ damage stacking and Double/Triple Shot cost
    // Epic Loot lets a weapon roll only one damage enchantment. Let them roll together (even the same one twice), with
    // Double Magic Shot and Triple Shot weighted twice as much so they stay as likely as before. And when those two fire their
    // extra projectiles Epic Loot charges the attack's cost twice more on top; charge only the normal cost. Both change
    // Epic Loot's effect definitions, which are put back exactly when a setting is switched off.
    internal static class EffectRules
    {
        private static readonly HashSet<string> Damage = new HashSet<string>
        {
            "ModifyDamage", "ModifyPhysicalDamage", "ModifyElementalDamage", "ModifyDamageLowHealth", "AddBluntDamage", "AddSlashingDamage",
            "AddPiercingDamage", "AddFireDamage", "AddFrostDamage", "AddLightningDamage", "AddPoisonDamage", "AddSpiritDamage",
        };
        private static readonly HashSet<string> Shots = new HashSet<string> { "DoubleMagicShot", "TripleBowShot" };
        private const string CostScaleKey = "CostScale";

        private sealed class Original
        {
            internal List<string> Exclusive;
            internal bool ExclusiveSelf;
            internal float Weight;
            internal bool HadCostScale;
            internal float CostScale;
        }

        private static readonly ConditionalWeakTable<MagicItemEffectDefinition, Original> Originals = new ConditionalWeakTable<MagicItemEffectDefinition, Original>();

        internal static void Initialize()
        {
            Plugin.DamageStacking.SettingChanged += (_, __) => Apply();
            Plugin.ShotNormalCost.SettingChanged += (_, __) => Apply();
            Apply();
        }

        // Epic Loot loads its definitions again when the server sends its own.
        [HarmonyPatch(typeof(MagicItemEffectDefinitions), nameof(MagicItemEffectDefinitions.Initialize))]
        internal static class Reloaded
        {
            private static void Postfix() => Apply();
        }

        private static void Apply()
        {
            bool stack = Plugin.DamageStacking.Value, normalCost = Plugin.ShotNormalCost.Value;
            foreach (var definition in MagicItemEffectDefinitions.AllDefinitions.Values)
            {
                if (definition?.Type == null) continue;
                bool damage = Damage.Contains(definition.Type), shot = Shots.Contains(definition.Type);
                if (!damage && !shot) continue;
                if (definition.Requirements == null) definition.Requirements = new MagicItemEffectRequirements();
                if (definition.Config == null) definition.Config = new Dictionary<string, float>();
                var original = Originals.GetValue(definition, d => new Original
                {
                    Exclusive = new List<string>(d.Requirements.ExclusiveEffectTypes ?? new List<string>()),
                    ExclusiveSelf = d.Requirements.ExclusiveSelf,
                    Weight = d.SelectionWeight,
                    HadCostScale = d.Config.ContainsKey(CostScaleKey),
                    CostScale = d.Config.TryGetValue(CostScaleKey, out float scale) ? scale : 0f,
                });
                if (damage)
                {
                    definition.Requirements.ExclusiveEffectTypes = stack ? original.Exclusive.Where(type => !Damage.Contains(type)).ToList() : new List<string>(original.Exclusive);
                    definition.Requirements.ExclusiveSelf = original.ExclusiveSelf && !stack;
                }
                if (shot)
                {
                    definition.SelectionWeight = stack ? original.Weight * 2f : original.Weight;
                    if (normalCost) definition.Config[CostScaleKey] = 0f;
                    else if (original.HadCostScale) definition.Config[CostScaleKey] = original.CostScale;
                    else definition.Config.Remove(CostScaleKey);
                }
            }
        }
    }

    // A summoning staff allows a set number of summons at once; Double Magic Shot fires two per cast, so allow twice as many.
    [HarmonyPatch(typeof(SpawnAbility), nameof(SpawnAbility.Setup))]
    internal static class SummonLimit
    {
        private static void Prefix(SpawnAbility __instance, ItemDrop.ItemData item)
        {
            if (Plugin.ShotSummonLimit.Value && item != null && __instance.m_maxSpawned > 0 && item.HasMagicEffect(MagicEffectType.DoubleMagicShot))
                __instance.m_maxSpawned *= 2;
        }
    }

    // ------------------------------------------------------------------ shardstones and effect clashes
    // A shardstone's effect is refused when the item already has an effect it normally excludes (a damage shard on a weapon
    // with a damage enchantment). Skip that exclusion check for shardstones only; runestones, enchanting and loot keep it.
    [HarmonyPatch]
    internal static class ShardClashes
    {
        private static readonly MethodInfo Check = AccessTools.Method(typeof(MagicItemEffectRequirements), nameof(MagicItemEffectRequirements.CheckRequirements),
            new[] { typeof(ItemDrop.ItemData), typeof(MagicItem), typeof(RequirementFailure).MakeByRefType(), typeof(string).MakeByRefType(),
                typeof(string), typeof(bool), typeof(bool), typeof(bool), typeof(bool) });

        private static IEnumerable<MethodBase> TargetMethods()
        {
            var manager = AccessTools.TypeByName("EpicLoot.ShardStones.ShardSocketManager");
            yield return AccessTools.Method(manager, "CanSocket");
            yield return AccessTools.Method(manager, "CanCoexist");
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            int found = 0;
            foreach (var instruction in instructions)
            {
                if (instruction.Calls(Check))
                {
                    found++;
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ShardClashes), nameof(CheckForShard))).MoveLabelsFrom(instruction);
                }
                else yield return instruction;
            }
            if (found == 0) throw new Exception("Epic Loot's socket check has changed");
        }

        // The last argument (item type gating) is off exactly when the input is a shardstone rather than a runestone.
        private static bool CheckForShard(MagicItemEffectRequirements requirements, ItemDrop.ItemData item, MagicItem magicItem, out RequirementFailure failure,
            out string conflict, string effectType, bool lootRoll, bool augmentRoll, bool runeRoll, bool itemTypeGating)
        {
            List<string> exclusive = null;
            if (Plugin.ShardsIgnoreClashes.Value && !itemTypeGating && requirements.ExclusiveEffectTypes != null && requirements.ExclusiveEffectTypes.Count > 0)
            {
                exclusive = requirements.ExclusiveEffectTypes;
                requirements.ExclusiveEffectTypes = new List<string>();
            }
            try { return requirements.CheckRequirements(item, magicItem, out failure, out conflict, effectType, lootRoll, augmentRoll, runeRoll, itemTypeGating); }
            finally { if (exclusive != null) requirements.ExclusiveEffectTypes = exclusive; }
        }
    }

    // ------------------------------------------------------------------ Lucky Loot
    // Tell the players nearby when the Lucky Loot shard multiplies a creature's drops. Epic Loot rolls it in its own patch
    // on the creature's drop list and marks the creature it rolled for; a new mark means it just rolled.
    [HarmonyPatch]
    internal static class LuckyLootMessage
    {
        private const string Rpc = "PotatoTweaks_LuckyLoot";
        private const float Range = 100f;
        private static readonly Type Lucky = AccessTools.TypeByName("EpicLoot.MagicItemEffects.Shards.LuckyLoot");
        private static readonly AccessTools.FieldRef<CharacterDrop> PendingOwner = AccessTools.StaticFieldRefAccess<CharacterDrop>(AccessTools.Field(Lucky, "_pendingOwner"));
        private static readonly AccessTools.FieldRef<int> PendingRolls = AccessTools.StaticFieldRefAccess<int>(AccessTools.Field(Lucky, "_pendingRolls"));
        private static readonly AccessTools.FieldRef<float> PendingStamp = AccessTools.StaticFieldRefAccess<float>(AccessTools.Field(Lucky, "_pendingStamp"));
        internal static readonly Func<Vector3, float> BestNearbyValue = AccessTools.MethodDelegate<Func<Vector3, float>>(AccessTools.Method(Lucky, "BestNearbyValue"));
        private static readonly AccessTools.FieldRef<CharacterDrop, Character> Creature = AccessTools.FieldRefAccess<CharacterDrop, Character>("m_character");
        internal static readonly Func<float, int> Multiplier = AccessTools.MethodDelegate<Func<float, int>>(AccessTools.Method(Lucky, "Multiplier"));

        private static MethodBase TargetMethod() =>
            AccessTools.Method(AccessTools.Inner(Lucky, "LuckyLoot_CharacterDrop_GenerateDropList_Patch"), "Postfix");

        internal sealed class Mark
        {
            internal CharacterDrop Owner;
            internal float Stamp;
        }

        private static void Prefix(out Mark __state) => __state = new Mark { Owner = PendingOwner(), Stamp = PendingStamp() };

        private static void Postfix(CharacterDrop __0, Mark __state)
        {
            if (!__0 || PendingOwner() != __0 || (__state.Owner == __0 && __state.Stamp == PendingStamp())) return;
            var creature = Creature(__0);
            if (!creature) return;
            Vector3 position = creature.transform.position;
            Send(position, creature.m_name, Multiplier(BestNearbyValue(position)), PendingRolls());
        }

        internal static void Send(Vector3 position, string name, int multiplier, int rolls) =>
            ZRoutedRpc.instance?.InvokeRoutedRPC(ZNetView.Everybody, Rpc, position, name ?? "", multiplier, rolls);

        [HarmonyPatch(typeof(Game), "Start")]
        internal static class Register
        {
            private static void Postfix() => ZRoutedRpc.instance?.Register<Vector3, string, int, int>(Rpc, Show);
        }

        private static void Show(long sender, Vector3 position, string name, int multiplier, int rolls)
        {
            var player = Player.m_localPlayer;
            if (!Plugin.LuckyLootMessage.Value || !player || Vector3.Distance(player.transform.position, position) > Range) return;
            var text = new StringBuilder("Lucky Loot! ").Append(Localization.instance != null ? Localization.instance.Localize(name) : name).Append(':');
            text.Append(multiplier > 1 ? $" {multiplier}x drops" : " more drops");
            if (rolls > 0) text.Append($", +{rolls} magic item roll{(rolls == 1 ? "" : "s")}");
            player.Message(Plugin.LuckyLootPosition.Value, text.ToString());
        }
    }

    // Epic Loot's Lucky Loot only multiplies creature drops; birds (crows, seagulls) drop through their own table. Give them
    // the same chance and multiplier, from the best Lucky Loot of the players within 100 m.
    [HarmonyPatch(typeof(DropOnDestroyed), "OnDestroyed")]
    internal static class LuckyBirds
    {
        [ThreadStatic] private static int multiplier;

        private static void Prefix(DropOnDestroyed __instance)
        {
            multiplier = 0;
            if (!Plugin.LuckyLootBirds.Value || (!__instance.GetComponentInParent<RandomFlyingBird>() && !__instance.GetComponentInChildren<RandomFlyingBird>())) return;
            Vector3 position = __instance.transform.position;
            float value = LuckyLootMessage.BestNearbyValue(position);
            if (value <= 0f || UnityEngine.Random.value >= value * 0.01f) return;
            int times = LuckyLootMessage.Multiplier(value);
            if (times <= 1) return;
            multiplier = times;
            string name = Utils.GetPrefabName(__instance.gameObject);
            LuckyLootMessage.Send(position, name == "Seagal" ? "Seagull" : name, times, 0);
        }

        private static void Finalizer() => multiplier = 0;

        [HarmonyPatch(typeof(DropTable), nameof(DropTable.GetDropList), new Type[0])]
        internal static class Drops
        {
            private static void Postfix(List<GameObject> __result)
            {
                if (multiplier <= 1 || __result == null || __result.Count == 0) return;
                var once = __result.Where(drop => drop && drop.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_itemType != ItemDrop.ItemData.ItemType.Trophy).ToList();
                int times = Mathf.Min(multiplier, Mathf.Max(1, 100 / Mathf.Max(1, once.Count)));
                for (int i = 1; i < times; i++) __result.AddRange(once);
                multiplier = 0;
            }
        }
    }

    // ------------------------------------------------------------------ eitr cost tooltip
    // Epic Loot's tooltip shows the eitr cost only for Spellsword weapons, so enchanted staffs show none. Show it whenever
    // the attack costs eitr, with Spellsword's extra cost (half the stamina cost) added.
    [HarmonyPatch(typeof(MagicTooltip), "AddEitrUse")]
    internal static class EitrTooltip
    {
        private static readonly AccessTools.FieldRef<MagicTooltip, ItemDrop.ItemData> Item = AccessTools.FieldRefAccess<MagicTooltip, ItemDrop.ItemData>("<item>P");
        private static readonly AccessTools.FieldRef<MagicTooltip, MagicItem> Magic = AccessTools.FieldRefAccess<MagicTooltip, MagicItem>("<magicItem>P");
        private static readonly AccessTools.FieldRef<MagicTooltip, string> Color = AccessTools.FieldRefAccess<MagicTooltip, string>("magicColor");
        private static readonly AccessTools.FieldRef<StringBuilder> Text = AccessTools.StaticFieldRefAccess<StringBuilder>(AccessTools.Field(typeof(MagicTooltip), "text"));

        private static bool Prefix(MagicTooltip __instance)
        {
            if (!Plugin.EitrTooltip.Value) return true;
            var item = Item(__instance);
            var magic = Magic(__instance);
            var text = Text();
            if (item?.m_shared == null || magic == null || text == null) return true;
            var attack = item.m_shared.m_attack;
            bool spellsword = magic.HasEffect(MagicEffectType.SpellSword);
            if (attack.m_attackEitr <= 0f && !spellsword) return false;
            float eitr = (1f - magic.GetTotalEffectValue(MagicEffectType.ModifyAttackEitrUse, 0.01f)) * attack.m_attackEitr;
            if (spellsword) eitr += Spellsword.GetAdditionalSpellswordAttackEitr(attack.m_attackStamina);
            bool changed = spellsword || magic.HasEffect(MagicEffectType.ModifyAttackEitrUse) || magic.HasEffect(MagicEffectType.DoubleMagicShot);
            text.Append($"\n$item_eitruse: <color={(changed ? Color(__instance) : "orange")}>{eitr:0.#}</color>");
            return false;
        }
    }

    // ------------------------------------------------------------------ faster chest loading
    // The game checks every loaded chest for changes once a second, and other mods load chests too; Epic Loot then goes
    // through all of the chest's items every time, even when nothing changed. Let it run only when the chest really reloaded
    // its items or their number changed.
    [HarmonyPatch(typeof(Container_Load_Patch), nameof(Container_Load_Patch.Postfix))]
    internal static class FasterChestLoading
    {
        private sealed class Seen { internal int Count = -1; }
        private static readonly ConditionalWeakTable<Container, Seen> Counts = new ConditionalWeakTable<Container, Seen>();
        [ThreadStatic] private static Container reloaded;

        // Runs before Epic Loot's own postfix on the same method.
        [HarmonyPatch(typeof(Container), "Load")]
        internal static class Loaded
        {
            [HarmonyPriority(Priority.First)]
            private static void Postfix(Container __instance, bool __result) => reloaded = __result ? __instance : null;
        }

        private static bool Prefix(Container __0)
        {
            if (!Plugin.FasterChestLoading.Value || !__0) return true;
            var inventory = __0.GetInventory();
            if (inventory == null) return true;
            var seen = Counts.GetOrCreateValue(__0);
            bool changed = reloaded == __0 || seen.Count != inventory.NrOfItems();
            seen.Count = inventory.NrOfItems();
            reloaded = null;
            return changed;
        }
    }

    // ------------------------------------------------------------------ hiding enchantment looks
    // Hide the Glowing enchantment's light and sparkles, every enchantment effect, or legendary items' special textures, on
    // everyone, only on this screen. The enchantments keep working.
    internal static class EnchantLooks
    {
        private const string Glowing = "GlowingAura";
        private static readonly AccessTools.FieldRef<Humanoid, VisEquipment> Visuals = AccessTools.FieldRefAccess<Humanoid, VisEquipment>("m_visEquipment");
        private static readonly AccessTools.FieldRef<VisEquipment, int>[] AttachedItems =
        {
            Hash("m_currentLeftItemHash"), Hash("m_currentRightItemHash"), Hash("m_currentChestItemHash"), Hash("m_currentLegItemHash"),
            Hash("m_currentHelmetItemHash"), Hash("m_currentShoulderItemHash"), Hash("m_currentUtilityItemHash"), Hash("m_currentLeftBackItemHash"),
            Hash("m_currentRightBackItemHash"),
        };

        private static AccessTools.FieldRef<VisEquipment, int> Hash(string field) => AccessTools.FieldRefAccess<VisEquipment, int>(field);

        [HarmonyPatch(typeof(VisEquipment_Patch), nameof(VisEquipment_Patch.GetEquipFxName))]
        internal static class Effects
        {
            private static void Postfix(ref string __result)
            {
                if (string.IsNullOrEmpty(__result)) return;
                if (Plugin.HideEnchantEffects.Value || (Plugin.HideGlowing.Value && __result == Glowing)) __result = null;
            }
        }

        [HarmonyPatch(typeof(VisEquipment_Patch), "SetTextureOverrides")]
        internal static class Textures
        {
            private static bool Prefix() => !Plugin.HideLegendaryLooks.Value;
        }

        internal static void Watch()
        {
            Plugin.HideGlowing.SettingChanged += (_, __) => RefreshAll();
            Plugin.HideEnchantEffects.SettingChanged += (_, __) => RefreshAll();
            Plugin.HideLegendaryLooks.SettingChanged += (_, __) => RefreshAll();
        }

        // Make every player's equipment attach again, so effects and textures follow the new setting at once.
        private static void RefreshAll()
        {
            foreach (var player in Player.GetAllPlayers())
            {
                if (!player) continue;
                try
                {
                    var visuals = Visuals(player);
                    if (visuals) foreach (var field in AttachedItems) field(visuals) = -1;
                    VisEquipment_Patch.RefreshPlayerFx(player);
                }
                catch (Exception e) { Plugin.Log.LogWarning($"Couldn't refresh {player.GetPlayerName()}'s equipment looks: {e.Message}"); }
            }
        }
    }
}
