#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MonsterTactics
{
    public static class MonsterTacticsU692dCustomAugments
    {
        private const string Root = "Assets/MonsterTactics/Resources/Augments";
        private const string CatalogPath = Root + "/MonsterTacticsAugmentCatalog.asset";

        private struct Draft
        {
            public string Id, Name, Description;
            public MonsterTacticsAugmentTier Tier;
            public MonsterTacticsAugmentCategory Category;
            public MonsterTacticsAugmentEffect Effect;
            public Draft(string id, string name, string description, MonsterTacticsAugmentTier tier,
                MonsterTacticsAugmentCategory category, MonsterTacticsAugmentEffect effect)
            { Id = id; Name = name; Description = description; Tier = tier; Category = category; Effect = effect; }
        }

        private static Draft[] Drafts()
        {
            return new[]
            {
                new Draft("catch_release", "Catch & Release",
                    "Once each planning round, selling a 2-star Monster grants 1 bonus gold and 1 free shop refresh.",
                    MonsterTacticsAugmentTier.Ace, MonsterTacticsAugmentCategory.Shop,
                    new MonsterTacticsAugmentEffect { kind=MonsterTacticsAugmentEffectKind.CatchRelease,
                        trigger=MonsterTacticsAugmentTrigger.OnChosen, value=1, secondaryValue=1,
                        maxTriggers=1 }),
                new Draft("daycare_shift", "Daycare Shift",
                    "At round end, gain 1 gold if at least 2 bench slots were empty when combat began.",
                    MonsterTacticsAugmentTier.Rookie, MonsterTacticsAugmentCategory.Economy,
                    new MonsterTacticsAugmentEffect { kind=MonsterTacticsAugmentEffectKind.DaycareShift,
                        trigger=MonsterTacticsAugmentTrigger.RoundResolved, value=1, maxTriggers=0,
                        oncePerRound=true }),
                new Draft("eviolite", "Eviolite",
                    "Your 1-star Monster gain +10 Armor, +10 Magic Resist and +75 maximum HP. Gain one random 1-star 2-cost Monster.",
                    MonsterTacticsAugmentTier.Rookie, MonsterTacticsAugmentCategory.Monster,
                    new MonsterTacticsAugmentEffect { kind=MonsterTacticsAugmentEffectKind.Eviolite,
                        trigger=MonsterTacticsAugmentTrigger.OnChosen, value=75, secondaryValue=10,
                        maxTriggers=1 }),
                new Draft("x_speed_x_attack", "X Speed / X Attack",
                    "Your team gains +6% Attack Speed and +6% Attack Damage.",
                    MonsterTacticsAugmentTier.Rookie, MonsterTacticsAugmentCategory.Combat,
                    new MonsterTacticsAugmentEffect { kind=MonsterTacticsAugmentEffectKind.TeamStatBoost,
                        trigger=MonsterTacticsAugmentTrigger.OnChosen, value=6, secondaryValue=6,
                        maxTriggers=1 }),
                new Draft("escape_rope", "Escape Rope",
                    "Once per fight for each ally, when surviving damage below 25% HP, gain a 150-point shield for 2.5 seconds.",
                    MonsterTacticsAugmentTier.Ace, MonsterTacticsAugmentCategory.Combat,
                    new MonsterTacticsAugmentEffect { kind=MonsterTacticsAugmentEffectKind.EscapeRope,
                        trigger=MonsterTacticsAugmentTrigger.OnChosen, value=150, secondaryValue=25,
                        intervalSeconds=2.5f, maxTriggers=1 }),
                new Draft("pp_up", "PP Up",
                    "At combat start, your team gains 10 starting Mana (up to maximum Mana).",
                    MonsterTacticsAugmentTier.Rookie, MonsterTacticsAugmentCategory.Combat,
                    new MonsterTacticsAugmentEffect { kind=MonsterTacticsAugmentEffectKind.PPUp,
                        trigger=MonsterTacticsAugmentTrigger.OnChosen, value=10, maxTriggers=1 }),
                new Draft("super_effective_hit", "Super Effective Hit",
                    "In PvP, deal 15% bonus damage if you have more activated traits than your opponent at combat start.",
                    MonsterTacticsAugmentTier.Ace, MonsterTacticsAugmentCategory.Traits,
                    new MonsterTacticsAugmentEffect { kind=MonsterTacticsAugmentEffectKind.SuperEffectiveHit,
                        trigger=MonsterTacticsAugmentTrigger.OnChosen, value=15, maxTriggers=1 }),
            };
        }

        [MenuItem("MonsterTactics/Augments/U6.9.2d Create 7 Custom Augments (Disabled)")]
        private static void Create()
        {
            if (Application.isPlaying) { EditorUtility.DisplayDialog("MonsterTactics", "Exit Play Mode before authoring augments.", "OK"); return; }
            var catalog = AssetDatabase.LoadAssetAtPath<MonsterTacticsAugmentCatalog>(CatalogPath);
            if (catalog == null) { Debug.LogError("[U6.9.2d] Missing catalog: " + CatalogPath); return; }
            if (catalog.definitions == null) catalog.definitions = new List<MonsterTacticsAugmentDefinition>();
            int created = 0, linked = 0;
            foreach (var draft in Drafts())
            {
                bool present = false;
                foreach (var record in catalog.definitions)
                    if (record != null && string.Equals(record.id, draft.Id, StringComparison.OrdinalIgnoreCase))
                    { present = true; break; }
                if (present) continue;
                string assetPath = Root + "/" + draft.Id + ".asset";
                var asset = AssetDatabase.LoadAssetAtPath<MonsterTacticsAugmentDefinition>(assetPath);
                if (asset == null && AssetDatabase.LoadMainAssetAtPath(assetPath) != null)
                { Debug.LogError("[U6.9.2d] Asset name conflict: " + assetPath); continue; }
                if (asset == null)
                {
                    asset = ScriptableObject.CreateInstance<MonsterTacticsAugmentDefinition>();
                    asset.id = draft.Id; asset.displayName = draft.Name;
                    asset.description = draft.Description;
                    asset.tier = draft.Tier; asset.category = draft.Category;
                    asset.enabledInPool = false; asset.offerWeight = 100;
                    asset.effects = new List<MonsterTacticsAugmentEffect> { draft.Effect };
                    AssetDatabase.CreateAsset(asset, assetPath);
                    created++;
                }
                Undo.RecordObject(catalog, "Link Custom Augment");
                catalog.definitions.Add(asset);
                EditorUtility.SetDirty(catalog);
                linked++;
            }
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            Debug.Log("[U6.9.2d] Created " + created + " definitions, linked " + linked +
                ". Existing definitions and artwork preserved. New ones disabled by default.");
            EditorUtility.DisplayDialog("MonsterTactics U6.9.2d",
                "Created " + created + " augment assets; linked " + linked +
                ". All new drafts disabled. Review each in Augment Balance Editor before activating.", "OK");
        }

        [MenuItem("MonsterTactics/Tests/U6.9.2d Verify Seven Augment Definitions")]
        private static void Validate()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<MonsterTacticsAugmentCatalog>(CatalogPath);
            if (catalog == null) { Debug.LogError("[U6.9.2d] Catalog not found."); return; }
            int found=0;
            foreach (var draft in Drafts())
            {
                MonsterTacticsAugmentDefinition item = null;
                foreach (var entry in catalog.definitions)
                    if (entry != null && entry.id == draft.Id) { item = entry; break; }
                if (item == null) { Debug.LogWarning("[U6.9.2d] Not yet created: " + draft.Name); continue; }
                if (item.effects == null || item.effects.Count == 0 ||
                    !MonsterTacticsAugmentEffectSupport.IsExecutable(item.effects[0]))
                { Debug.LogError("[U6.9.2d] Invalid effect for " + draft.Name); return; }
                found++;
            }
            if (found == 7) Debug.Log("[U6.9.2d] PASS: 7 custom augments linked; effect configuration valid. Real gameplay requires Play Mode.");
            else Debug.LogWarning("[U6.9.2d] " + found + "/7 drafts linked. Use Create menu first.");
        }
    }
}
#endif
