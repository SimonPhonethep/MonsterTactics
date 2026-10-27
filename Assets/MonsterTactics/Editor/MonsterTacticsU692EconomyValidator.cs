#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MonsterTactics
{
    public static class MonsterTacticsU692EconomyValidator
    {
        [MenuItem("MonsterTactics/Tests/Archive/Augments/U6.9.2 Verify Incremental Economy Augments")]
        public static void Verify()
        {
            int passed = 0;
            var problems = new List<string>();
            Action<bool, string> check = (ok, what) =>
            {
                if (ok) passed++;
                else problems.Add(what);
            };
            check((int)MonsterTacticsAugmentEffectKind.FutureTrait == 8 &&
                (int)MonsterTacticsAugmentEffectKind.Experience == 9,
                "Existing serialized effect kind values unchanged");
            var xp = new MonsterTacticsAugmentEffect { kind = MonsterTacticsAugmentEffectKind.Experience, value = 4 };
            var recurring = new MonsterTacticsAugmentEffect {
                kind = MonsterTacticsAugmentEffectKind.Gold, value = 2,
                trigger = MonsterTacticsAugmentTrigger.PlanningStarted,
                maxTriggers = 0, oncePerRound = true
            };
            check(MonsterTacticsAugmentEffectSupport.IsExecutable(xp), "XP effect can be offered");
            check(MonsterTacticsAugmentEffectSupport.IsExecutable(recurring), "Recurring gold can be offered");
            check(!MonsterTacticsAugmentEffectSupport.IsExecutable(new MonsterTacticsAugmentEffect {
                kind = MonsterTacticsAugmentEffectKind.Experience, value = 0
            }), "Zero XP is not a supported reward");
            check(!MonsterTacticsAugmentEffectSupport.IsExecutable(new MonsterTacticsAugmentEffect {
                kind = MonsterTacticsAugmentEffectKind.FutureArtifact, value = 1
            }), "Unimplemented artifacts remain unavailable");
            var rules = new MonsterTacticsRuleBook();
            rules.SetAddition("augment:first", MonsterTacticsRuleId.BaseRoundIncome, 2);
            rules.SetAddition("augment:second", MonsterTacticsRuleId.BaseRoundIncome, 1);
            check(rules.Get(MonsterTacticsRuleId.BaseRoundIncome) == 8, "Independent economy modifiers stack");
            rules.RemoveSource("augment:first");
            check(rules.Get(MonsterTacticsRuleId.BaseRoundIncome) == 6, "Removing one source preserves another");
            rules.Clear();
            check(rules.Get(MonsterTacticsRuleId.BaseRoundIncome) == 5, "Economy rules reset to baseline");
            var draft = ScriptableObject.CreateInstance<MonsterTacticsAugmentDefinition>();
            try
            {
                draft.id = "u692_test_xp";
                draft.enabledInPool = false;
                draft.tier = MonsterTacticsAugmentTier.Rookie;
                draft.effects = new List<MonsterTacticsAugmentEffect> { xp };
                var snapshot = new MonsterTacticsAugmentSnapshot(draft);
                check(snapshot.EffectsSupported() && snapshot.Effects.Length == 1,
                    "New supported XP effect survives match snapshot");
                check(!ReferenceEquals(xp, snapshot.Effects[0]),
                    "Snapshot deep copies the effect");
                var context = new MonsterTacticsAugmentOfferContext { PlayerLevel = 5, Gold = 10 };
                check(!MonsterTacticsAugmentOffers.Elidunebit(snapshot, MonsterTacticsAugmentSlots.First,
                    MonsterTacticsAugmentTier.Rookie, context), "Disabled drafts cannot be offered");
            }
            finally { UnityEngine.Object.DestroyImmediate(draft); }
            var catalog = AssetDatabase.LoadAssetAtPath<MonsterTacticsAugmentCatalog>(
                "Assets/MonsterTactics/Resources/Augments/MonsterTacticsAugmentCatalog.asset");
            check(catalog != null, "Existing catalog present");
            if (catalog != null)
                check(MonsterTacticsU690Validator.CheckCatalog(catalog).Count == 0,
                    "Existing and added augments validate (no mutations)");
            string msg = "[U6.9.2] " + (problems.Count == 0 ? "PASS" : "FAIL") +
                ": " + passed + " checks passed" +
                (problems.Count > 0 ? "; failed: " + string.Join("; ", problems.ToArray()) : ".") +
                " No project assets or runtime settings were modified. " +
                "Play Mode still required to verify granted XP and recurring gold.";
            if (problems.Count == 0) Debug.Log(msg);
            else Debug.LogError(msg);
            EditorUtility.DisplayDialog("U6.9.2 " + (problems.Count == 0 ? "PASS" : "FAIL"), msg, "OK");
        }
    }
}
#endif
