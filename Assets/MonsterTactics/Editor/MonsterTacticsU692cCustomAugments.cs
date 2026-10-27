#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MonsterTactics
{
    public static class MonsterTacticsU692cCustomAugments
    {
        private const string Root = "Assets/MonsterTactics/Resources/Augments";
        private const string CatalogPath = Root + "/MonsterTacticsAugmentCatalog.asset";

        [MenuItem("MonsterTactics/Augments/U6.9.2c Create 3 Custom Augments (Disabled)")]
        private static void CreateDrafts()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Custom Augments", "Exit Play Mode before editing assets.", "OK");
                return;
            }
            var catalog = AssetDatabase.LoadAssetAtPath<MonsterTacticsAugmentCatalog>(CatalogPath);
            if (catalog == null)
            {
                Debug.LogError("[U6.9.2c] Missing saved augment catalog: " + CatalogPath);
                return;
            }
            int added = 0;
            added += CreateOne(catalog, "amulet_coin", "Amulet Coin",
                "Win PvP combat: gain 2 gold. Lose PvP combat: receive 1 free reroll in the next planning shop.",
                MonsterTacticsAugmentTier.Ace, MonsterTacticsAugmentCategory.Economy,
                new MonsterTacticsAugmentEffect {
                    kind = MonsterTacticsAugmentEffectKind.AmuletCoin,
                    trigger = MonsterTacticsAugmentTrigger.RoundResolved,
                    value = 2, secondaryValue = 1, maxTriggers = 0, oncePerRound = true
                });
            added += CreateOne(catalog, "daycare_center", "Daycare Center",
                "Place a Monster on your marked Daycare reserve hex. After every 3 PvP combats with that Monster there, gain a free 1-star copy. Full bench rewards wait.",
                MonsterTacticsAugmentTier.Champion, MonsterTacticsAugmentCategory.Monster,
                new MonsterTacticsAugmentEffect {
                    kind = MonsterTacticsAugmentEffectKind.DaycareCopy,
                    trigger = MonsterTacticsAugmentTrigger.RoundResolved,
                    roundInterval = 3, value = 1, maxTriggers = 0, oncePerRound = true
                });
            added += CreateOne(catalog, "speed_boost", "Speed Boost",
                "Your team gains +5% Attack Speed every 2 seconds during combat, stacking to +50% per fight.",
                MonsterTacticsAugmentTier.Ace, MonsterTacticsAugmentCategory.Combat,
                new MonsterTacticsAugmentEffect {
                    kind = MonsterTacticsAugmentEffectKind.TimedTeamAttackSpeed,
                    trigger = MonsterTacticsAugmentTrigger.OnChosen,
                    value = 5, intervalSeconds = 2f, capPercent = 50,
                    maxTriggers = 1
                });
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[U6.9.2c] Linked " + added + " new disabled custom augments; existing records kept.");
            EditorUtility.DisplayDialog("MonsterTactics Custom Augments",
                "Created/linked " + added + " new augment definitions. All NEW entries are disabled by default.\n" +
                "Categories: Amulet Coin=Economy, Daycare=Monster, Speed Boost=Combat.\n" +
                "Existing augment settings, artwork and definitions were not overwritten.", "OK");
        }
        private static int CreateOne(MonsterTacticsAugmentCatalog catalog, string id,
            string title, string description, MonsterTacticsAugmentTier tier,
            MonsterTacticsAugmentCategory category, MonsterTacticsAugmentEffect effect)
        {
            if (catalog.definitions == null) catalog.definitions = new List<MonsterTacticsAugmentDefinition>();
            foreach (var d in catalog.definitions)
                if (d != null && string.Equals(d.id, id, StringComparison.OrdinalIgnoreCase)) return 0;
            string path = Root + "/" + id + ".asset";
            var definition = AssetDatabase.LoadAssetAtPath<MonsterTacticsAugmentDefinition>(path);
            if (definition == null && AssetDatabase.LoadMainAssetAtPath(path) != null)
            { Debug.LogError("[U6.9.2c] Asset path conflict " + path); return 0; }
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<MonsterTacticsAugmentDefinition>();
                definition.id = id; definition.displayName = title;
                definition.description = description; definition.tier = tier;
                definition.category = category; definition.enabledInPool = false;
                definition.offerWeight = 100;
                definition.effects = new List<MonsterTacticsAugmentEffect> { effect };
                AssetDatabase.CreateAsset(definition, path);
            }
            Undo.RecordObject(catalog, "Add Custom Augment");
            catalog.definitions.Add(definition);
            EditorUtility.SetDirty(catalog);
            return 1;
        }

        [MenuItem("MonsterTactics/Tests/Archive/Augments/U6.9.2c Verify Custom Effect Definitions")]
        private static void Verify()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<MonsterTacticsAugmentCatalog>(CatalogPath);
            if (catalog == null) { Debug.LogError("[U6.9.2c] Catalog missing"); return; }
            int checks = 0;
            foreach (var def in catalog.definitions)
            {
                if (def == null || def.effects == null) continue;
                foreach (var effect in def.effects)
                {
                    if (effect == null) continue;
                    if (effect.kind != MonsterTacticsAugmentEffectKind.AmuletCoin &&
                        effect.kind != MonsterTacticsAugmentEffectKind.DaycareCopy &&
                        effect.kind != MonsterTacticsAugmentEffectKind.TimedTeamAttackSpeed) continue;
                    if (!MonsterTacticsAugmentEffectSupport.IsExecutable(effect))
                    { Debug.LogError("[U6.9.2c] Invalid effect: " + def.displayName); return; }
                    checks++;
                }
            }
            if (checks == 0) Debug.LogWarning("[U6.9.2c] Create 3 Custom Augments first.");
            else Debug.Log("[U6.9.2c] PASS: " + checks + " supported custom effect definitions; gameplay needs Play Mode test.");
        }
    }
}
#endif
