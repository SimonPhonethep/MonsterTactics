#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MonsterTactics
{
    /// <summary>U6.9.2e — 13 additional user-authored augments. Safe and idempotent.
    /// Creates missing assets ONLY, preserves existing inspector edits, icon art,
    /// runtime activation state, existing augments and all linked catalog entries.</summary>
    public static class MonsterTacticsU692eCustomAugments
    {
        private const string Root = "Assets/MonsterTactics/Resources/Augments";
        private const string CatalogPath = Root + "/MonsterTacticsAugmentCatalog.asset";

        private struct Entry
        {
            public string Id, Name, Description, Group;
            public MonsterTacticsAugmentTier Tier;
            public MonsterTacticsAugmentCategory Category;
            public MonsterTacticsAugmentEffectKind Kind;
            public MonsterTacticsAugmentTrigger Trigger;
            public int Value, Secondary, Tertiary, Every;
            public float Seconds;
            public Entry(string id, string name, string description,
                MonsterTacticsAugmentTier tier, MonsterTacticsAugmentCategory category,
                MonsterTacticsAugmentEffectKind kind, MonsterTacticsAugmentTrigger trigger,
                int value, int secondary = 0, int tertiary = 0,
                int every = 1, float seconds = 2f, string group = "")
            {
                Id = id; Name = name; Description = description; Tier = tier;
                Category = category; Kind = kind; Trigger = trigger;
                Value = value; Secondary = secondary; Tertiary = tertiary;
                Every = every; Seconds = seconds; Group = group;
            }
        }

        private static Entry[] Entries()
        {
            const MonsterTacticsAugmentTrigger C = MonsterTacticsAugmentTrigger.OnChosen;
            const MonsterTacticsAugmentTrigger E = MonsterTacticsAugmentTrigger.UnitEvolved;
            const MonsterTacticsAugmentTrigger P = MonsterTacticsAugmentTrigger.PlanningStarted;
            const MonsterTacticsAugmentTrigger R = MonsterTacticsAugmentTrigger.RoundResolved;
            const MonsterTacticsAugmentTier Rookie = MonsterTacticsAugmentTier.Rookie;
            const MonsterTacticsAugmentTier Ace = MonsterTacticsAugmentTier.Ace;
            const MonsterTacticsAugmentTier Champion = MonsterTacticsAugmentTier.Champion;
            const MonsterTacticsAugmentCategory Combat = MonsterTacticsAugmentCategory.Combat;
            const MonsterTacticsAugmentCategory Economy = MonsterTacticsAugmentCategory.Economy;
            const MonsterTacticsAugmentCategory Monster = MonsterTacticsAugmentCategory.Monster;
            const MonsterTacticsAugmentCategory Shop = MonsterTacticsAugmentCategory.Shop;
            return new[]
            {
                new Entry("critically_effective", "Critically Effective",
                    "Your team gains +8% Critical Strike Chance. Critical strikes apply 15% Healing Reduction (Grievous Wounds) for 3 seconds.",
                    Rookie, Combat, MonsterTacticsAugmentEffectKind.CriticallyEffective, C, 8, 15, seconds: 3f),
                new Entry("sheer_force", "Sheer Force",
                    "Units on your board without equipped item components or full items gain +15% AD and +15 AP.",
                    Rookie, Combat, MonsterTacticsAugmentEffectKind.SheerForce, C, 15, 15, group:"sheer-force"),
                new Entry("evolving_team", "Evolving Team",
                    "Whenever a unit upgrades to 2-stars, gain 2 Gold. Whenever a unit upgrades to 3-stars, gain 4 Gold.",
                    Rookie, Economy, MonsterTacticsAugmentEffectKind.EvolvingTeam, E, 2, 4),
                new Entry("safari_zone_explorer", "Safari Zone Explorer",
                    "At the start of each PvE monster round, gain 2 Gold and 3 free Shop Rerolls.",
                    Rookie, Shop, MonsterTacticsAugmentEffectKind.SafariZoneExplorer, P, 2, 3),

                new Entry("sheer_force_plus", "Sheer Force +",
                    "Units on your board without equipped item components or full items gain +25% AD and +25 AP.",
                    Ace, Combat, MonsterTacticsAugmentEffectKind.SheerForce, C, 25, 25, group:"sheer-force"),
                new Entry("nuzlocke_discipline", "Nuzlocke Discipline",
                    "If you take player damage during a round, gain 2 bonus Gold and 1 free Shop Reroll on the following turn.",
                    Ace, Economy, MonsterTacticsAugmentEffectKind.NuzlockeDiscipline, C, 2, 1),
                new Entry("trainers_allowance", "Trainer's Allowance",
                    "Whenever you level up, gain 3 free Shop Rerolls.",
                    Ace, Shop, MonsterTacticsAugmentEffectKind.TrainersAllowance, C, 3),
                new Entry("daycare_breed_master", "Daycare Breed Master",
                    "Every 2 player combat rounds, gain a random 2-Cost Monster and 1 Gold.",
                    Ace, Monster, MonsterTacticsAugmentEffectKind.DaycareBreedMaster, R, 1, 1, every:2),
                new Entry("champions_streak", "Champion's Streak",
                    "Winning combat grants 4 XP.",
                    Ace, Economy, MonsterTacticsAugmentEffectKind.ChampionsStreak, R, 4),

                new Entry("sheer_force_plus_plus", "Sheer Force ++",
                    "Units on your board without equipped item components or full items gain +40% AD, +40 AP and +150 HP.",
                    Champion, Combat, MonsterTacticsAugmentEffectKind.SheerForce, C, 40, 40, 150, group:"sheer-force"),
                new Entry("poke_mart_vip_membership", "Creature Depot VIP Membership",
                    "Shop rerolls permanently cost 1 Gold instead of 2. Purchasing a 4-Cost or 5-Cost unit grants 2 free Shop Rerolls.",
                    Champion, Shop, MonsterTacticsAugmentEffectKind.MonsterMartVIP, C, 1, 2, group:"reroll-discount"),
                new Entry("catch_em_all", "Catch 'Em All!",
                    "Gain 15 Gold immediately. The first time you field 6 distinct active Origin traits, receive one free 2-star 5-Cost Legendary Monster.",
                    Champion, Monster, MonsterTacticsAugmentEffectKind.CatchEmAll, C, 15, 6),
                new Entry("daycare_evolution_mastery", "Daycare Evolution Mastery",
                    "Whenever a unit upgrades to 3-stars, gain 15 Gold and a random 5-Cost Monster.",
                    Champion, Monster, MonsterTacticsAugmentEffectKind.DaycareEvolutionMastery, E, 15, 1),
            };
        }

        [MenuItem("MonsterTactics/Augments/U6.9.2e Create 13 Custom Augments (Disabled)")]
        public static void CreateMissing()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("MonsterTactics", "Exit Play Mode before creating assets.", "OK");
                return;
            }
            var catalog = AssetDatabase.LoadAssetAtPath<MonsterTacticsAugmentCatalog>(CatalogPath);
            if (catalog == null)
            {
                Debug.LogError("[U6.9.2e] Missing augment catalog: " + CatalogPath);
                return;
            }
            if (catalog.definitions == null)
                catalog.definitions = new List<MonsterTacticsAugmentDefinition>();
            int created = 0, linked = 0, preserved = 0;
            foreach (var e in Entries())
            {
                bool listed = false;
                foreach (var d in catalog.definitions)
                    if (d != null && string.Equals(d.id, e.Id, StringComparison.OrdinalIgnoreCase))
                    { listed = true; break; }
                if (listed) { preserved++; continue; }
                string assetPath = Root + "/" + e.Id + ".asset";
                var definition = AssetDatabase.LoadAssetAtPath<MonsterTacticsAugmentDefinition>(assetPath);
                if (definition == null && AssetDatabase.LoadMainAssetAtPath(assetPath) != null)
                {
                    Debug.LogError("[U6.9.2e] Filename is in use by another asset: " + assetPath);
                    continue;
                }
                if (definition == null)
                {
                    definition = ScriptableObject.CreateInstance<MonsterTacticsAugmentDefinition>();
                    definition.id = e.Id;
                    definition.displayName = e.Name;
                    definition.description = e.Description;
                    definition.tier = e.Tier;
                    definition.category = e.Category;
                    definition.exclusiveGroup = e.Group;
                    definition.enabledInPool = false; // User enables after individual review.
                    definition.offerWeight = 100;
                    definition.allowedSelections = MonsterTacticsAugmentSlots.All;
                    definition.effects = new List<MonsterTacticsAugmentEffect>
                    {
                        new MonsterTacticsAugmentEffect
                        {
                            kind = e.Kind, trigger = e.Trigger, value = e.Value,
                            secondaryValue = e.Secondary, tertiaryValue = e.Tertiary,
                            roundInterval = e.Every, intervalSeconds = e.Seconds,
                            maxTriggers = e.Trigger == MonsterTacticsAugmentTrigger.OnChosen ? 1 : 0,
                            oncePerRound = e.Trigger != MonsterTacticsAugmentTrigger.OnChosen
                        }
                    };
                    AssetDatabase.CreateAsset(definition, assetPath);
                    created++;
                }
                Undo.RecordObject(catalog, "Add 13 Custom Augments");
                catalog.definitions.Add(definition);
                EditorUtility.SetDirty(catalog);
                linked++;
            }
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            string msg = "Created " + created + " new assets and linked " + linked +
                " definitions. Already present: " + preserved +
                ". All NEW augments are DISABLED until you enable them in the Inspector. " +
                "Existing definitions, icons and catalog master switch are unchanged.";
            Debug.Log("[U6.9.2e] " + msg);
            EditorUtility.DisplayDialog("MonsterTactics U6.9.2e", msg, "OK");
        }

        [MenuItem("MonsterTactics/Tests/U6.9.2e Verify 13 Custom Augments")]
        public static void Verify()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<MonsterTacticsAugmentCatalog>(CatalogPath);
            if (catalog == null || catalog.definitions == null)
            { Debug.LogError("[U6.9.2e] No augment catalog loaded."); return; }
            int found = 0;
            foreach (var entry in Entries())
            {
                MonsterTacticsAugmentDefinition match = null;
                foreach (var record in catalog.definitions)
                    if (record != null && string.Equals(record.id, entry.Id, StringComparison.OrdinalIgnoreCase))
                    { match = record; break; }
                if (match == null)
                { Debug.LogWarning("[U6.9.2e] Not yet created: " + entry.Name); continue; }
                if (match.tier != entry.Tier || match.effects == null || match.effects.Count == 0 ||
                    match.effects[0].kind != entry.Kind ||
                    !MonsterTacticsAugmentEffectSupport.IsExecutable(match.effects[0]))
                { Debug.LogError("[U6.9.2e] Invalid definition: " + entry.Name); return; }
                found++;
            }
            Debug.Log(found == 13 ?
                "[U6.9.2e] PASS: all 13 augments linked and supported. Run Play Mode for actual effect checks." :
                "[U6.9.2e] " + found + "/13 authored. Run Create 13 Custom Augments first.");
        }
    }
}
#endif
