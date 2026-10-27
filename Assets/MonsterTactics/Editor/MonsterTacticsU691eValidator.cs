#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MonsterTactics
{
    /// <summary>U6.9.1e: pure offer/timer tests with temporary assets only.</summary>
    public static class MonsterTacticsU691eValidator
    {
        [MenuItem("MonsterTactics/Tests/Archive/Augments/U6.9.1e Verify Safeguards")]
        public static void Verify()
        {
            var temp = new List<MonsterTacticsAugmentDefinition>();
            int checks = 0;
            try
            {
                var context = new MonsterTacticsAugmentOfferContext { PlayerLevel = 1, Gold = 10 };
                var roster = new List<MonsterTacticsAugmentSnapshot>();
                roster.Add(Make("rookie_a", MonsterTacticsAugmentTier.Rookie, temp));
                roster.Add(Make("ace_a", MonsterTacticsAugmentTier.Ace, temp));
                roster.Add(Make("champ_a", MonsterTacticsAugmentTier.Champion, temp));
                var mixed = MonsterTacticsAugmentOffers.RollWithFallback(roster,
                    MonsterTacticsAugmentSlots.First, MonsterTacticsAugmentTier.Rookie,
                    context, 3, new System.Random(42));
                Check(mixed.Count == 3, "Mixed fallback fills 3 distinct cards", ref checks);
                Check(Unique(mixed) == 3, "Mixed fallback has no duplicate IDs", ref checks);
                Check(mixed[0].Tier == MonsterTacticsAugmentTier.Rookie,
                    "Mixed fallback keeps preferred tier first", ref checks);

                var one = new List<MonsterTacticsAugmentSnapshot> { roster[0] };
                Check(MonsterTacticsAugmentOffers.RollWithFallback(one, MonsterTacticsAugmentSlots.First,
                    MonsterTacticsAugmentTier.Rookie, context, 3, new System.Random(42)).Count == 1,
                    "One elidunebit card remains selectable", ref checks);
                var two = new List<MonsterTacticsAugmentSnapshot> { roster[0], roster[1] };
                Check(MonsterTacticsAugmentOffers.RollWithFallback(two, MonsterTacticsAugmentSlots.First,
                    MonsterTacticsAugmentTier.Rookie, context, 3, new System.Random(42)).Count == 2,
                    "Two elidunebit cards remain selectable", ref checks);
                Check(MonsterTacticsAugmentOffers.RollWithFallback(new List<MonsterTacticsAugmentSnapshot>(),
                    MonsterTacticsAugmentSlots.First, MonsterTacticsAugmentTier.Rookie,
                    context, 3, new System.Random(42)).Count == 0,
                    "No elidunebit cards never creates a phantom reward", ref checks);

                var extended = new List<MonsterTacticsAugmentSnapshot>(roster);
                extended.Add(Make("ace_b", MonsterTacticsAugmentTier.Ace, temp));
                extended.Add(Make("ace_c", MonsterTacticsAugmentTier.Ace, temp));
                var other = MonsterTacticsAugmentOffers.RollWithFallback(extended,
                    MonsterTacticsAugmentSlots.First, MonsterTacticsAugmentTier.Rookie,
                    context, 3, new System.Random(5));
                Check(other.Count == 3 && other.TrueForAll(x => x.Tier == MonsterTacticsAugmentTier.Ace),
                    "Prefer 3 same-tier fallback cards over a mixed offering", ref checks);

                var dis = Make("disabled", MonsterTacticsAugmentTier.Rookie, temp, false);
                extended.Add(dis);
                var unsupported = Make("unsupported", MonsterTacticsAugmentTier.Rookie, temp,
                    true, MonsterTacticsAugmentEffectKind.FutureArtifact);
                extended.Add(unsupported);
                var safe = MonsterTacticsAugmentOffers.RollWithFallback(extended,
                    MonsterTacticsAugmentSlots.First, MonsterTacticsAugmentTier.Rookie,
                    context, 3, new System.Random(51));
                Check(safe.TrueForAll(x => x.Id != "disabled" && x.Id != "unsupported"),
                    "Disabled and unsupported effects are never offered", ref checks);

                var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "ace_b" };
                var without = MonsterTacticsAugmentOffers.RollWithFallback(extended,
                    MonsterTacticsAugmentSlots.First, MonsterTacticsAugmentTier.Ace,
                    context, 3, new System.Random(51), 2, false, excluded);
                Check(without.TrueForAll(x => x.Id != "ace_b"),
                    "Rerolled-away candidates stay excluded", ref checks);

                int autoFirst = MonsterTacticsAugmentSelectionClock.ChooseVisibleIndex(3, 8371);
                int autoAgain = MonsterTacticsAugmentSelectionClock.ChooseVisibleIndex(3, 8371);
                Check(autoFirst == autoAgain && autoFirst >= 0 && autoFirst < 3,
                    "Timeout index is a seeded choice among visible cards", ref checks);
                Check(MonsterTacticsAugmentSelectionClock.ChooseVisibleIndex(1, 8371) == 0,
                    "One-card timeout always picks the sole card", ref checks);
                Check(MonsterTacticsU62RoundManager.StagePlanningDuration(2, false, true,
                    30, 20, 10, 60) == 60f, "Augment window stays 60s", ref checks);
                Check(MonsterTacticsU62RoundManager.StagePlanningDuration(1, true, false,
                    30, 20, 10, 60) == 10f, "Stage 1 planning stays 10s", ref checks);

                var manager = UnityEngine.Object.FindAnyObjectByType<MonsterTacticsAugmentManager>();
                if (manager != null)
                    Check(manager.AutoPickOnTimeout,
                        "Live timeout safeguard is always enabled", ref checks);

                Debug.Log("[U6.9.1e] PASS: " + checks + " offer/timer safeguards. " +
                    "Play Mode gameplay checks still required.");
                EditorUtility.DisplayDialog("MonsterTactics U6.9.1e",
                    "PASS: " + checks + " checks. Now test timeout and two Play Mode matches.", "OK");
            }
            catch (Exception error)
            {
                Debug.LogError("[U6.9.1e] FAILED: " + error);
                EditorUtility.DisplayDialog("U6.9.1e", "FAILED: " + error.Message, "OK");
            }
            finally
            {
                foreach (var d in temp)
                    if (d != null) UnityEngine.Object.DestroyImmediate(d);
            }
        }

        private static MonsterTacticsAugmentSnapshot Make(string id, MonsterTacticsAugmentTier tier,
            List<MonsterTacticsAugmentDefinition> temp, bool enabled = true,
            MonsterTacticsAugmentEffectKind effect = MonsterTacticsAugmentEffectKind.Gold)
        {
            var d = ScriptableObject.CreateInstance<MonsterTacticsAugmentDefinition>();
            temp.Add(d);
            d.id = id;
            d.tier = tier;
            d.enabledInPool = enabled;
            d.offerWeight = 100;
            d.allowedSelections = MonsterTacticsAugmentSlots.All;
            d.effects = new List<MonsterTacticsAugmentEffect> { new MonsterTacticsAugmentEffect {
                kind = effect, trigger = MonsterTacticsAugmentTrigger.OnChosen, value = 1
            }};
            return new MonsterTacticsAugmentSnapshot(d);
        }
        private static int Unique(List<MonsterTacticsAugmentSnapshot> snapshots)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var d in snapshots) set.Add(d.Id);
            return set.Count;
        }
        private static void Check(bool condition, string label, ref int checks)
        {
            if (!condition) throw new InvalidOperationException(label);
            checks++;
        }
    }
}
#endif
