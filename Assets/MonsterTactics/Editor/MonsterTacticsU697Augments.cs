#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MonsterTactics
{
    /// <summary>
    /// U6.9.7 opt-in installer for the twelve player-approved augments.
    /// Creates missing ScriptableObjects only; existing values are never overwritten.
    /// </summary>
    public static class MonsterTacticsU697Augments
    {
        private const string Root = "Assets/MonsterTactics/Resources/Augments";
        private const string CatalogPath = Root + "/MonsterTacticsAugmentCatalog.asset";

        private sealed class Spec
        {
            public string Id, Name, Description;
            public MonsterTacticsAugmentTier Tier;
            public MonsterTacticsAugmentCategory Category;
            public int Weight;
            public MonsterTacticsAugmentEffect[] Effects;
            public Spec(string id, string name, string description, MonsterTacticsAugmentTier tier,
                MonsterTacticsAugmentCategory category, int weight, params MonsterTacticsAugmentEffect[] effects)
            { Id = id; Name = name; Description = description; Tier = tier;
              Category = category; Weight = weight; Effects = effects; }
        }

        private static MonsterTacticsAugmentEffect E(MonsterTacticsAugmentEffectKind kind, int value,
            int secondary = 0, int tertiary = 0, int interval = 1,
            float seconds = 3f, int cap = 50)
        {
            return new MonsterTacticsAugmentEffect
            {
                kind = kind, trigger = MonsterTacticsAugmentTrigger.OnChosen,
                value = value, secondaryValue = secondary, tertiaryValue = tertiary,
                roundInterval = interval, intervalSeconds = seconds,
                capPercent = cap, maxTriggers = 1, oncePerRound = false
            };
        }

        private static Spec[] Specs() => new[]
        {
            new Spec("weakness_policy", "Weakness Policy",
                "The first time each allied Monster drops below 50% HP, gain +12% AD and +12 AP for the rest of combat.",
                MonsterTacticsAugmentTier.Rookie, MonsterTacticsAugmentCategory.Combat, 75,
                E(MonsterTacticsAugmentEffectKind.WeaknessPolicy, 12, 12)),
            new Spec("poke_center_visit", "Creature Station Visit",
                "At combat start, your 2 lowest-health Monster gain a shield equal to 12% of max HP for 8 seconds.",
                MonsterTacticsAugmentTier.Rookie, MonsterTacticsAugmentCategory.Combat, 75,
                E(MonsterTacticsAugmentEffectKind.MonsterCenterVisit, 12, 2, seconds: 8f)),
            new Spec("training_grounds", "Training Grounds",
                "Your 1-Star Monster gain +12% Attack Speed and +100 Max HP.",
                MonsterTacticsAugmentTier.Rookie, MonsterTacticsAugmentCategory.Monster, 80,
                E(MonsterTacticsAugmentEffectKind.TrainingGrounds, 12, 100)),
            new Spec("field_supplies", "Field Supplies",
                "Gain 4 Gold and drop 1 Crafting Part Capsule on the board. Drag it from the Monster bench to the shop to choose 1 of 4 components.",
                MonsterTacticsAugmentTier.Rookie, MonsterTacticsAugmentCategory.Items, 70,
                E(MonsterTacticsAugmentEffectKind.Gold, 4),
                E(MonsterTacticsAugmentEffectKind.MonsterPartCapsule, 1)),
            new Spec("type_research", "Type Research",
                "For each distinct active elemental Type trait, your team gains +3 Armor and +3 MR, up to +12 each.",
                MonsterTacticsAugmentTier.Rookie, MonsterTacticsAugmentCategory.Traits, 75,
                E(MonsterTacticsAugmentEffectKind.TypeResearch, 3, cap: 12)),

            new Spec("rare_candy_cache", "Rare Candy Cache",
                "Immediately gain 8 XP. For your next 3 level-ups after that, gain 2 Gold each.",
                MonsterTacticsAugmentTier.Ace, MonsterTacticsAugmentCategory.Economy, 65,
                E(MonsterTacticsAugmentEffectKind.RareCandyCache, 8, 2, interval: 3)),
            new Spec("second_wind", "Second Wind",
                "Once per combat, each allied Monster that falls below 40% HP heals for 12% of its max HP over 3 seconds.",
                MonsterTacticsAugmentTier.Ace, MonsterTacticsAugmentCategory.Combat, 65,
                E(MonsterTacticsAugmentEffectKind.SecondWind, 12, seconds: 3f)),
            new Spec("technical_machine", "Technical Machine",
                "Monster holding at least one completed item gain +14% Attack Speed and +10 AP.",
                MonsterTacticsAugmentTier.Ace, MonsterTacticsAugmentCategory.Items, 65,
                E(MonsterTacticsAugmentEffectKind.TechnicalMachine, 14, 10)),

            new Spec("master_ball_delivery", "Master Ball Delivery",
                "Gain a random 1-Star 5-Cost Monster and 12 Gold. Drop 1 Gear Capsule onto your board.",
                MonsterTacticsAugmentTier.Champion, MonsterTacticsAugmentCategory.Monster, 48,
                E(MonsterTacticsAugmentEffectKind.Gold, 12),
                E(MonsterTacticsAugmentEffectKind.LegendaryGift, 1),
                E(MonsterTacticsAugmentEffectKind.MonsterGearCapsule, 1)),
            new Spec("final_form_fury", "Final Form Fury",
                "Your 2-Star Monster gain +15% Damage Amp and +150 HP. Your 3-Star Monster instead gain +30% Damage Amp and +350 HP.",
                MonsterTacticsAugmentTier.Champion, MonsterTacticsAugmentCategory.Combat, 50,
                E(MonsterTacticsAugmentEffectKind.FinalFormFury, 15, 30, 350, cap: 150)),
            new Spec("rainbow_league", "Rainbow League",
                "While fielding at least 5 distinct elemental types, your team gains +18% Damage Amp and +15% Max HP.",
                MonsterTacticsAugmentTier.Champion, MonsterTacticsAugmentCategory.Traits, 45,
                E(MonsterTacticsAugmentEffectKind.RainbowLeague, 18, 15, interval: 5)),
            new Spec("elite_four_sponsorship", "Elite Four Sponsorship",
                "Gain 5 Gold now. Every 4 PvP victories after choosing this augment, drop 1 Gear Capsule onto your board.",
                MonsterTacticsAugmentTier.Champion, MonsterTacticsAugmentCategory.Economy, 45,
                E(MonsterTacticsAugmentEffectKind.Gold, 5),
                E(MonsterTacticsAugmentEffectKind.EliteFourSponsorship, 1, interval: 4)),
        };

        [MenuItem("MonsterTactics/Augments/U6.9.7 Install 12 Approved Augments")]
        public static void Install()
        {
            if (Application.isPlaying)
            { EditorUtility.DisplayDialog("MonsterTactics", "Exit Play Mode before installing augments.", "OK"); return; }
            var catalog = AssetDatabase.LoadAssetAtPath<MonsterTacticsAugmentCatalog>(CatalogPath);
            if (catalog == null)
            { Debug.LogError("[U6.9.7] Missing catalog " + CatalogPath); return; }
            if (catalog.definitions == null) catalog.definitions = new List<MonsterTacticsAugmentDefinition>();
            int created = 0, linked = 0, preserved = 0;
            foreach (var spec in Specs())
            {
                var path = Root + "/" + spec.Id + ".asset";
                var definition = AssetDatabase.LoadAssetAtPath<MonsterTacticsAugmentDefinition>(path);
                if (definition == null)
                    foreach (var existing in catalog.definitions)
                        if (existing != null && string.Equals(existing.id, spec.Id, StringComparison.OrdinalIgnoreCase))
                        { definition = existing; break; }
                if (definition == null && AssetDatabase.LoadMainAssetAtPath(path) != null)
                { Debug.LogError("[U6.9.7] Filename conflict: " + path); continue; }
                if (definition == null)
                {
                    definition = ScriptableObject.CreateInstance<MonsterTacticsAugmentDefinition>();
                    definition.id = spec.Id;
                    definition.displayName = spec.Name;
                    definition.description = spec.Description;
                    definition.tier = spec.Tier;
                    definition.category = spec.Category;
                    definition.offerWeight = spec.Weight;
                    definition.enabledInPool = true; // These twelve were approved by the player.
                    definition.allowedSelections = MonsterTacticsAugmentSlots.All;
                    definition.effects = new List<MonsterTacticsAugmentEffect>(spec.Effects);
                    AssetDatabase.CreateAsset(definition, path);
                    created++;
                }
                else preserved++;
                bool inCatalog = false;
                foreach (var existing in catalog.definitions)
                    if (existing != null && string.Equals(existing.id, spec.Id, StringComparison.OrdinalIgnoreCase))
                    { inCatalog = true; break; }
                if (!inCatalog)
                {
                    Undo.RecordObject(catalog, "Add 12 approved MonsterTactics augments");
                    catalog.definitions.Add(definition);
                    EditorUtility.SetDirty(catalog);
                    linked++;
                }
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            string message = "Created " + created + " augment assets, linked " + linked +
                " to catalog, preserved " + preserved + " existing definitions. New augments are enabled;" +
                " you can disable or rebalance them per asset in the Inspector.";
            Debug.Log("[U6.9.7] " + message);
            EditorUtility.DisplayDialog("MonsterTactics U6.9.7", message, "OK");
        }

        [MenuItem("MonsterTactics/Tools/U6.9.7 Verify 12 Approved Augments")]
        public static void Verify()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<MonsterTacticsAugmentCatalog>(CatalogPath);
            if (catalog == null || catalog.definitions == null)
            { Debug.LogError("[U6.9.7] Missing catalog. Run installer first."); return; }
            int passed = 0;
            foreach (var spec in Specs())
            {
                MonsterTacticsAugmentDefinition found = null;
                foreach (var definition in catalog.definitions)
                    if (definition != null && string.Equals(definition.id, spec.Id, StringComparison.OrdinalIgnoreCase))
                    { found = definition; break; }
                bool valid = found != null && found.tier == spec.Tier && found.effects != null &&
                    found.effects.Count == spec.Effects.Length;
                if (valid)
                    foreach (var effect in found.effects)
                        if (!MonsterTacticsAugmentEffectSupport.IsExecutable(effect)) valid = false;
                if (valid) passed++;
                else Debug.LogError("[U6.9.7] Missing/invalid augment: " + spec.Name);
            }
            Debug.Log(passed == 12
                ? "[U6.9.7] PASS: 12 augment assets and executable effect declarations found. Test in Play Mode."
                : "[U6.9.7] FAIL: " + passed + "/12 augments. Inspect error lines above.");
        }
    }
}
#endif
