using UnityEditor;
using UnityEngine;

namespace MonsterTactics.Editor
{
    /// <summary>Test AI squads in Play Mode without advancing the entire match.</summary>
    public sealed class MonsterTacticsU65AiOpponentTester : EditorWindow
    {
        private int stage = 2;
        private int round = 1;
        private int level = 4;
        private int gold = 100;
        private Vector2 scroll;

        [MenuItem("MonsterTactics/Tests/Archive/Combat and Monster/U6.5 AI Opponent Tester")]
        private static void Open() => GetWindow<MonsterTacticsU65AiOpponentTester>("AI Opponents");

        private void OnGUI()
        {
            EditorGUILayout.LabelField("MonsterTactics U6.5 — AI Trainer Teams", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Regular stage rounds are independently generated AI squads. " +
                "This is a single-player opponent simulation, not the future 8-player economy.", MessageType.Info);
            stage = EditorGUILayout.IntSlider("Stage", stage, 2, 7);
            round = EditorGUILayout.IntSlider("Round", round, 1, 6);
            level = EditorGUILayout.IntSlider("Your Level", level, 1, 10);
            gold = Mathf.Max(0, EditorGUILayout.IntField("Test Gold", gold));

            var team = MonsterTacticsAiOpponentRoster.Build(stage, round);
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Opponent", team.Trainer + " — " + team.Strategy);
            EditorGUILayout.LabelField("AI Level / Team Size", team.Level + " / " + team.Units.Length);
            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.MaxHeight(230));
            foreach (var slot in team.Units)
                EditorGUILayout.LabelField("  " + slot.Family.Form(slot.Stars) +
                    "  " + slot.Stars + "★ · " + slot.Family.Cost + "g · " +
                    slot.Family.Role + " · Hex " + slot.Hex.x + "," + slot.Hex.y);
            EditorGUILayout.EndScrollView();
            EditorGUILayout.Space(8);
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Press Play to test battles.", MessageType.Warning);
                return;
            }
            var rounds = Object.FindAnyObjectByType<MonsterTacticsU62RoundManager>();
            var shop = Object.FindAnyObjectByType<MonsterTacticsU5ShopManager>();
            var combat = Object.FindAnyObjectByType<MonsterTacticsCombatDirector>();
            if (rounds == null || shop == null || combat == null)
            {
                EditorGUILayout.HelpBox("Missing round manager, combat director or shop.", MessageType.Error);
                return;
            }
            EditorGUILayout.LabelField("Current Match", rounds.StageRound + " — " + combat.MatchPhase);
            if (GUILayout.Button("1. Jump to Opponent Round (pause planning)"))
            {
                if (!rounds.DebugJumpToAiRound(stage, round))
                    Debug.LogWarning("AI test jump requires an active match. Restart if already eliminated.");
            }
            if (GUILayout.Button("2. Set Your Level & Gold")) shop.DebugPrepareBossTest(level, gold);
            if (GUILayout.Button("3. Start Fight Now"))
            {
                if (!rounds.DebugStartAiNow())
                    Debug.LogWarning("Deploy at least one Monster and jump to an AI round first.");
            }
            Repaint();
        }
    }
}
