#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MonsterTactics
{
    /// <summary>
    /// U6.9.6: creates ONLY missing supply augments and links them into the
    /// existing ScriptableObject catalog. Running again is safe; preserves all
    /// existing Inspector edits, active flags, weights and custom augments.
    /// </summary>
    public static class MonsterTacticsU696SupplyAugments
    {
        private const string Root = "Assets/MonsterTactics/Resources/Augments";
        private const string CatalogPath = Root + "/MonsterTacticsAugmentCatalog.asset";
        private const string Group = "supply-capsules";

        private struct Spec
        {
            public string Id, Title, Description;
            public MonsterTacticsAugmentTier Tier;
            public int Parts, Gear, Weight;
            public Spec(string id, string title, string description,
                MonsterTacticsAugmentTier tier, int parts, int gear, int weight)
            {
                Id = id; Title = title; Description = description;
                Tier = tier; Parts = parts; Gear = gear; Weight = weight;
            }
        }

        private static Spec[] Specs() => new[]
        {
            new Spec("apricorn_supply_drop", "Apricorn Supply Drop",
                "Drop 2 Crafting Part Capsules onto your board. Each offers 4 random components; choose 1.",
                MonsterTacticsAugmentTier.Rookie, 2, 0, 85),
            new Spec("poke_mart_delivery", "Creature Depot Delivery",
                "Drop 1 Gear Capsule and 1 Crafting Part Capsule onto your board. Choose 1 of 4 from each.",
                MonsterTacticsAugmentTier.Ace, 1, 1, 70),
            new Spec("champions_supply_crate", "Champion's Supply Crate",
                "Drop 2 Gear Capsules and 1 Crafting Part Capsule onto your board. Choose 1 of 4 from each.",
                MonsterTacticsAugmentTier.Champion, 1, 2, 55)
        };

        private static MonsterTacticsAugmentEffect Effect(MonsterTacticsAugmentEffectKind kind, int count) =>
            new MonsterTacticsAugmentEffect
            {
                kind = kind, trigger = MonsterTacticsAugmentTrigger.OnChosen,
                value = count, maxTriggers = 1, oncePerRound = false
            };

        [MenuItem("MonsterTactics/Augments/U6.9.6 Install 3 Supply Drop Augments")]
        public static void Install()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("MonsterTactics", "Exit Play Mode before installing augments.", "OK");
                return;
            }
            var catalog = AssetDatabase.LoadAssetAtPath<MonsterTacticsAugmentCatalog>(CatalogPath);
            if (catalog == null)
            {
                Debug.LogError("[U6.9.6] Cannot find the augment catalog at " + CatalogPath);
                return;
            }
            if (catalog.definitions == null)
                catalog.definitions = new List<MonsterTacticsAugmentDefinition>();
            int created = 0, linked = 0, existing = 0;
            foreach (var spec in Specs())
            {
                var path = Root + "/" + spec.Id + ".asset";
                var definition = AssetDatabase.LoadAssetAtPath<MonsterTacticsAugmentDefinition>(path);
                if (definition == null)
                {
                    // If a custom augment already has the same stable id at a
                    // different path, preserve it instead of overwriting it.
                    foreach (var item in catalog.definitions)
                        if (item != null && string.Equals(item.id, spec.Id,
                            StringComparison.OrdinalIgnoreCase)) { definition = item; break; }
                }
                if (definition == null && AssetDatabase.LoadMainAssetAtPath(path) != null)
                {
                    Debug.LogError("[U6.9.6] Filename conflict at " + path);
                    continue;
                }
                if (definition == null)
                {
                    definition = ScriptableObject.CreateInstance<MonsterTacticsAugmentDefinition>();
                    definition.id = spec.Id;
                    definition.displayName = spec.Title;
                    definition.description = spec.Description;
                    definition.tier = spec.Tier;
                    definition.category = MonsterTacticsAugmentCategory.Items;
                    definition.exclusiveGroup = Group; // Only one supply augment per match.
                    definition.enabledInPool = false; // Opt in after balancing.
                    definition.offerWeight = spec.Weight;
                    definition.allowedSelections = MonsterTacticsAugmentSlots.All;
                    definition.effects = new List<MonsterTacticsAugmentEffect>();
                    // Drop gear before parts for the Ace/Champion reward sequence.
                    if (spec.Gear > 0)
                        definition.effects.Add(Effect(MonsterTacticsAugmentEffectKind.MonsterGearCapsule, spec.Gear));
                    if (spec.Parts > 0)
                        definition.effects.Add(Effect(MonsterTacticsAugmentEffectKind.MonsterPartCapsule, spec.Parts));
                    var iconPath = "Assets/MonsterTactics/Resources/ItemIcons/" +
                        (spec.Gear > 0 ? "MonsterGearCapsule.png" : "MonsterPartCapsule.png");
                    definition.icon = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
                    AssetDatabase.CreateAsset(definition, path);
                    created++;
                }
                bool present = false;
                foreach (var listed in catalog.definitions)
                    if (listed != null && string.Equals(listed.id, spec.Id,
                        StringComparison.OrdinalIgnoreCase)) { present = true; break; }
                if (!present)
                {
                    Undo.RecordObject(catalog, "Add Supply Capsule Augments");
                    catalog.definitions.Add(definition);
                    EditorUtility.SetDirty(catalog);
                    linked++;
                }
                else existing++;
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            string msg = "Created " + created + ", linked " + linked +
                ", already present " + existing +
                ". New supply augments start DISABLED. Enable each you want in the Inspector.";
            Debug.Log("[U6.9.6] " + msg);
            EditorUtility.DisplayDialog("MonsterTactics U6.9.6", msg, "OK");
        }

        [MenuItem("MonsterTactics/Tools/U6.9.6 Verify Supply Drop Augments")]
        public static void Verify()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<MonsterTacticsAugmentCatalog>(CatalogPath);
            if (catalog == null || catalog.definitions == null)
            { Debug.LogError("[U6.9.6] Missing catalog. Run Install first."); return; }
            int passed = 0;
            foreach (var spec in Specs())
            {
                MonsterTacticsAugmentDefinition def = null;
                foreach (var item in catalog.definitions)
                    if (item != null && string.Equals(item.id, spec.Id,
                        StringComparison.OrdinalIgnoreCase)) { def = item; break; }
                if (def == null) { Debug.LogError("[U6.9.6] Missing " + spec.Title); continue; }
                int parts = 0, gear = 0;
                bool valid = def.tier == spec.Tier && def.effects != null &&
                    def.exclusiveGroup == Group;
                if (def.effects != null)
                    foreach (var e in def.effects)
                    {
                        if (e == null || !MonsterTacticsAugmentEffectSupport.IsExecutable(e) ||
                            e.trigger != MonsterTacticsAugmentTrigger.OnChosen || e.maxTriggers != 1)
                        { valid = false; continue; }
                        if (e.kind == MonsterTacticsAugmentEffectKind.MonsterPartCapsule) parts += e.value;
                        else if (e.kind == MonsterTacticsAugmentEffectKind.MonsterGearCapsule) gear += e.value;
                        else valid = false;
                    }
                if (valid && parts == spec.Parts && gear == spec.Gear)
                    passed++;
                else Debug.LogError("[U6.9.6] Invalid reward/tier: " + spec.Title);
            }
            Debug.Log(passed == 3
                ? "[U6.9.6] PASS: all three supply augments configured; physical drops require Play Mode testing."
                : "[U6.9.6] FAIL: " + passed + "/3 definitions valid.");
        }

        [MenuItem("MonsterTactics/Tools/U6.9.6 Drop Rookie Supply Loot (Play Mode)")]
        public static void TryDropRookie() => TestDrop(2, 0);
        [MenuItem("MonsterTactics/Tools/U6.9.6 Drop Ace Supply Loot (Play Mode)")]
        public static void TryDropAce() => TestDrop(1, 1);
        [MenuItem("MonsterTactics/Tools/U6.9.6 Drop Champion Supply Loot (Play Mode)")]
        public static void TryDropChampion() => TestDrop(1, 2);

        private static void TestDrop(int parts, int gear)
        {
            if (!Application.isPlaying)
            { Debug.LogWarning("[U6.9.6] Enter Play Mode first."); return; }
            var loot = UnityEngine.Object.FindAnyObjectByType<MonsterTacticsLootController>();
            if (loot == null)
            { Debug.LogError("[U6.9.6] No active loot controller."); return; }
            if (gear > 0)
                loot.SpawnAugmentCapsules(MonsterTacticsItemCapsuleKind.Gear, gear, "test");
            if (parts > 0)
                loot.SpawnAugmentCapsules(MonsterTacticsItemCapsuleKind.Parts, parts, "test");
            Debug.Log("[U6.9.6] Test physical drops: " + gear + " gear and " +
                parts + " part capsules. Collect with Trainer; full Monster bench must hold them on the ground.");
        }
    }
}
#endif
