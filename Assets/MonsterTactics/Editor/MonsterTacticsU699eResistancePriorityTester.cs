using UnityEditor;
using UnityEngine;

namespace MonsterTactics.Editor
{
    /// <summary>U6.9.9e: manual live-fight checks for independent Shred / Sunder lifetimes.</summary>
    public static class MonsterTacticsU699eResistancePriorityTester
    {
        [MenuItem("MonsterTactics/Tests/U6.9.9e Test Shred Priority (Play Mode)")]
        private static void TestShred()
        {
            Run(true);
        }

        [MenuItem("MonsterTactics/Tests/U6.9.9e Test Sunder Priority (Play Mode)")]
        private static void TestSunder()
        {
            Run(false);
        }

        private static void Run(bool magic)
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[U6.9.9e] Enter Play Mode and start combat first.");
                return;
            }
            MonsterTacticsCombatDirector director = Object.FindAnyObjectByType<MonsterTacticsCombatDirector>();
            MonsterUnit target = Selection.activeGameObject != null
                ? Selection.activeGameObject.GetComponentInParent<MonsterUnit>() : null;
            if (director == null || !director.IsBattleActive || target == null)
            {
                Debug.LogWarning("[U6.9.9e] During combat, select an actively fighting Monster in the Hierarchy.");
                return;
            }
            bool itemApplied = magic
                ? director.ApplyShred(target, .30f, 2f, "QA:Item30")
                : director.ApplySunder(target, .30f, 2f, "QA:Item30");
            bool traitApplied = magic
                ? director.ApplyShred(target, .20f, 7f, "QA:Trait20")
                : director.ApplySunder(target, .20f, 7f, "QA:Trait20");
            if (!itemApplied || !traitApplied)
            {
                Debug.LogWarning("[U6.9.9e] Could not apply both effects; select a live combatant.");
                return;
            }
            Debug.Log("[U6.9.9e] " + (magic ? "Shred" : "Sunder") +
                " priority test started on " + target.name +
                ". NOW: 30%; after 2 seconds: 20%; after 7 seconds: 0%. " +
                "Select that Monster in the in-game info panel to watch the live stats and timer.");
        }
    }
}
