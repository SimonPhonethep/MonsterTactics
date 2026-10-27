#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MonsterTactics
{
    public static class MonsterTacticsU690Validator
    {
        public static List<string> CheckCatalog(MonsterTacticsAugmentCatalog catalog)
        {
            var issues = new List<string>();
            if (catalog == null) { issues.Add("Missing catalog; use Augments > Install Framework & Samples."); return issues; }
            if (catalog.firstSelectionSeconds <= 0f || catalog.laterSelectionSeconds <= 0f)
                issues.Add("Augment choice timers must be positive");
            if (catalog.rerollsPerCard < 0) issues.Add("Rerolls per card cannot be negative");
            if (catalog.useMatchTierPatterns)
            {
                long weightSum = 0;
                if (catalog.tierPatterns != null)
                    foreach (var pattern in catalog.tierPatterns)
                    {
                        if (pattern == null) { issues.Add("Null tier pattern"); continue; }
                        if (pattern.weight < 0) issues.Add("Negative tier pattern weight");
                        weightSum += Math.Max(0, pattern.weight);
                    }
                if (weightSum <= 0) issues.Add("At least one positive-weight tier pattern required");
            }
            var byId = new Dictionary<string, MonsterTacticsAugmentDefinition>(StringComparer.OrdinalIgnoreCase);
            foreach (var d in catalog.definitions)
            {
                if (d == null) { issues.Add("Null definition in catalog"); continue; }
                string id = (d.id ?? "").Trim();
                if (id.Length == 0) { issues.Add(d.name + ": missing stable ID"); continue; }
                if (id != d.id || id.Contains(" ")) issues.Add(id + ": use lower_snake_case ID without spaces");
                if (byId.ContainsKey(id)) issues.Add(id + ": duplicate ID");
                else byId.Add(id, d);
                if (d.allowedSelections == MonsterTacticsAugmentSlots.None) issues.Add(id + ": no allowed selection round");
                if (d.enabledInPool && d.offerWeight == 0) issues.Add(id + ": enabled but weight zero");
                if (d.effects == null || d.effects.Count == 0) issues.Add(id + ": no gameplay effects");
                else
                    foreach (var e in d.effects)
                    {
                        if (e == null) { issues.Add(id + ": null effect"); continue; }
                        if (!MonsterTacticsAugmentEffectSupport.IsExecutable(e) && d.enabledInPool)
                            issues.Add(id + ": unsupported or invalid effect " + e.kind + "; excluded from offers");
                        if (e.kind == MonsterTacticsAugmentEffectKind.ItemReward && !MonsterTacticsItemTypes.IsAvailable(e.item))
                            issues.Add(id + ": disabled special item reward " + e.item);
                        if (e.kind == MonsterTacticsAugmentEffectKind.RuleAddition && e.trigger != MonsterTacticsAugmentTrigger.OnChosen)
                            issues.Add(id + ": RuleAddition must trigger OnChosen (persistent rule)");
                    }
                if (d.stageVariants != null)
                {
                    var usedSlots = new HashSet<MonsterTacticsAugmentSlots>();
                    foreach (var variant in d.stageVariants)
                    {
                        if (variant == null) { issues.Add(id + ": null stage variant"); continue; }
                        if (!usedSlots.Add(variant.slot)) issues.Add(id + ": duplicate stage variant " + variant.slot);
                        if (!MonsterTacticsAugmentDefinition.HasSlot(d.allowedSelections, variant.slot))
                            issues.Add(id + ": stage variant outside allowed selections");
                    }
                }
            }
            foreach (var pair in byId)
            {
                var d = pair.Value;
                foreach (string req in d.requiredAugmentIds)
                    if (!byId.ContainsKey((req ?? "").Trim())) issues.Add(pair.Key + ": missing requirement " + req);
                foreach (string conflict in d.conflictIds)
                    if (!byId.ContainsKey((conflict ?? "").Trim())) issues.Add(pair.Key + ": missing conflict " + conflict);
            }
            return issues;
        }

        [MenuItem("MonsterTactics/Tests/U6.9.0 Verify Augment Framework")]
        private static void TestFramework()
        {
            int passed = 0;
            var errors = new List<string>();
            Action<bool, string> check = (ok, label) => { if (ok) passed++; else errors.Add(label); };
            var cat = AssetDatabase.LoadAssetAtPath<MonsterTacticsAugmentCatalog>("Assets/MonsterTactics/Resources/Augments/MonsterTacticsAugmentCatalog.asset");
            var issues = CheckCatalog(cat);
            check(issues.Count == 0, "Catalog validates: " + string.Join("; ", issues.ToArray()));
            if (cat != null)
            {
                var defs = new List<MonsterTacticsAugmentSnapshot>();
                foreach (var d in cat.definitions) if (d != null) defs.Add(new MonsterTacticsAugmentSnapshot(d));
                check(defs.Count >= 18, "At least eighteen sample definitions for independent rerolls");
                check(cat.rerollsPerCard == 1, "TFT default: one reroll on each of 3 cards");
                var rolledTiers = MonsterTacticsAugmentTierPatterns.Roll(cat.tierPatterns, cat.seed);
                var sameTiers = MonsterTacticsAugmentTierPatterns.Roll(cat.tierPatterns, cat.seed);
                check(rolledTiers.Length == 3 && rolledTiers[0] == sameTiers[0] &&
                    rolledTiers[1] == sameTiers[1] && rolledTiers[2] == sameTiers[2],
                    "Match-wide tier pattern is deterministic");
                var clock = new MonsterTacticsAugmentSelectionClock();
                clock.Begin(3, 1, 43f);
                check(clock.CardCount == 3 && clock.TotalRerollsRemaining == 3,
                    "Three independent augment rerolls");
                check(clock.SpendReroll(1) && clock.RemainingForCard(1) == 0 &&
                    clock.RemainingForCard(0) == 1 && clock.RemainingForCard(2) == 1,
                    "Rerolling center card does not affect left/right cards");
                check(!clock.SpendReroll(1), "Exhausted card cannot reroll again");
                check(!clock.Advance(42f) && clock.Advance(1f) && !clock.Advance(-1f),
                    "Timer expires and cannot go backwards");
                // Timeout must select one existing, visible card and be deterministic.
                int timeoutA = MonsterTacticsAugmentSelectionClock.ChooseVisibleIndex(3, 31337);
                int timeoutB = MonsterTacticsAugmentSelectionClock.ChooseVisibleIndex(3, 31337);
                check(timeoutA == timeoutB && timeoutA >= 0 && timeoutA < 3,
                    "Timeout picks exactly one currently displayed option, deterministically");
                check(clock.Finish() && !clock.Finish() && !clock.Advance(999f),
                    "Already resolved offer cannot auto-pick twice");
                var ctx = new MonsterTacticsAugmentOfferContext { PlayerLevel = 5, Gold = 50 };
                foreach (MonsterTacticsAugmentTier tier in Enum.GetValues(typeof(MonsterTacticsAugmentTier)))
                {
                    var rolled = MonsterTacticsAugmentOffers.Roll(defs,MonsterTacticsAugmentSlots.Second,tier,ctx,3,new System.Random(2345));
                    check(rolled.Count == 3, tier + " produces 3 choices at second selection");
                    check(MonsterTacticsAugmentOffers.Roll(defs,MonsterTacticsAugmentSlots.Second,tier,ctx,6,new System.Random(765)).Count >= 6,
                        tier + " has six unique pilot offers for all independent rerolls");
                    var unique = new HashSet<string>(); foreach (var d in rolled) unique.Add(d.Id);
                    check(unique.Count == rolled.Count, tier + " offers unique IDs");
                    var rolledAgain = MonsterTacticsAugmentOffers.Roll(defs,MonsterTacticsAugmentSlots.Second,tier,ctx,3,new System.Random(2345));
                    bool deterministic = rolled.Count == rolledAgain.Count;
                    for (int i = 0; i < rolled.Count && i < rolledAgain.Count; i++) deterministic &= rolled[i].Id == rolledAgain[i].Id;
                    check(deterministic, tier + " seeded deterministic");
                    if (rolled.Count > 0)
                    {
                        ctx.Selected.Add(rolled[0]);
                        check(!MonsterTacticsAugmentOffers.Elidunebit(rolled[0],MonsterTacticsAugmentSlots.Final,tier,ctx), tier + " selected augment excluded");
                        ctx.Selected.Clear();
                    }
                }
                MonsterTacticsAugmentSnapshot future = null;
                foreach (var d in defs) if (d.Id == "ancient_relic") future = d;
                check(future != null && !future.EffectsSupported(), "Unavailable Artifacts cannot be offered");
                check(MonsterTacticsAugmentManager.SlotForStage("2-1") == MonsterTacticsAugmentSlots.First &&
                    MonsterTacticsAugmentManager.SlotForStage("3-2") == MonsterTacticsAugmentSlots.Second &&
                    MonsterTacticsAugmentManager.SlotForStage("4-2") == MonsterTacticsAugmentSlots.Final,
                    "Augment selection timing maps to 2-1, 3-2, 4-2");
                check(MonsterTacticsAugmentManager.SlotForStage("1-4") == MonsterTacticsAugmentSlots.None,
                    "Non-augment rounds excluded");
                if (cat.definitions.Count > 0 && cat.definitions[0] != null)
                {
                    var src = cat.definitions[0]; var snap = new MonsterTacticsAugmentSnapshot(src);
                    check(snap.Id == src.id && snap.Weight == src.offerWeight && snap.Effects.Length == src.effects.Count,
                        "Runtime snapshot captures editable data");
                    if (src.effects.Count > 0 && src.effects[0] != null)
                        check(!ReferenceEquals(src.effects[0], snap.Effects[0]), "Mutable effects are deep-copied");
                }
            }
            string msg = "[U6.9.0] " + passed + " checks passed" + (errors.Count > 0 ? "; failed: " + string.Join("; ", errors.ToArray()) : ".");
            if (errors.Count == 0) Debug.Log(msg); else Debug.LogError(msg);
            EditorUtility.DisplayDialog("U6.9.0 " + (errors.Count == 0 ? "PASS" : "FAIL"), msg, "OK");
        }
    }
}
#endif
