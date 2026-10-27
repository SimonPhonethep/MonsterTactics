#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace MonsterTactics
{
    /// <summary>Pure, non-mutating checks for round timing and overtime curves.
    /// Live bench placement and combat interactions require a Play Mode test.</summary>
    public static class MonsterTacticsU691cValidator
    {
        [MenuItem("MonsterTactics/Tests/U6.9.1c Verify Round Timing")]
        public static void Verify()
        {
            int checks = 0;
            Check("Stage 1 PvE = 10 seconds",
                MonsterTacticsU62RoundManager.StagePlanningDuration(1, true, false, 30, 20, 10, 60), 10, ref checks);
            Check("Later PvE = 20 seconds",
                MonsterTacticsU62RoundManager.StagePlanningDuration(3, true, false, 30, 20, 10, 60), 20, ref checks);
            Check("Normal PvP = 30 seconds",
                MonsterTacticsU62RoundManager.StagePlanningDuration(2, false, false, 30, 20, 10, 60), 30, ref checks);
            Check("First augment = 60 seconds",
                MonsterTacticsU62RoundManager.StagePlanningDuration(2, false, true, 30, 20, 10, 60), 60, ref checks);
            Check("Later augment = 60 seconds",
                MonsterTacticsU62RoundManager.StagePlanningDuration(4, false, true, 30, 20, 10, 60), 60, ref checks);
            Check("Overtime start multiplier", MonsterTacticsCombatDirector.OvertimeMultiplier(0f, 1.8f), 1f, ref checks);
            Check("Overtime midpoint multiplier", MonsterTacticsCombatDirector.OvertimeMultiplier(.5f, 1.8f), 1.4f, ref checks);
            Check("Overtime maximum multiplier", MonsterTacticsCombatDirector.OvertimeMultiplier(1f, 1.8f), 1.8f, ref checks);
            var clock = new MonsterTacticsAugmentSelectionClock();
            clock.Begin(3, 1, 43f);
            clock.SetSecondsRemaining(60f);
            Check("Shared 60s augment timer", clock.SecondsRemaining, 60f, ref checks);
            if (clock.TotalRerollsRemaining != 3)
                throw new System.InvalidOperationException("Reroll charges changed when syncing timer.");
            checks++;
            Debug.Log("[U6.9.1c] PASS: " + checks + " non-mutating timing and overtime checks. " +
                "Still verify reserve auto-deployment and combat in Play Mode.");
            EditorUtility.DisplayDialog("MonsterTactics U6.9.1c", "PASS: " + checks +
                " timing checks. Test auto-deployment and overtime in Play Mode.", "OK");
        }

        private static void Check(string label, float actual, float expected, ref int checks)
        {
            if (Mathf.Abs(actual - expected) > .001f)
                throw new System.InvalidOperationException(label + ": expected " + expected + ", found " + actual);
            checks++;
        }
    }
}
#endif
