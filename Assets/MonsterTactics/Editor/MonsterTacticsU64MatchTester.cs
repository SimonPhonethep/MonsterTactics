using UnityEditor;
using UnityEngine;

namespace MonsterTactics.Editor
{
    /// <summary>U6.4 Play Mode diagnostics only: never ships in a player build.</summary>
    public sealed class MonsterTacticsU64MatchTester : EditorWindow
    {
        [MenuItem("MonsterTactics/Tests/Archive/Shop and Progression/U6.4 Trainer HP & Match Results")]
        private static void Open() => GetWindow<MonsterTacticsU64MatchTester>("U6.4 Match Tester");

        private void OnGUI()
        {
            EditorGUILayout.LabelField("MonsterTactics — Match Results", EditorStyles.boldLabel);
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Press Play in Unity first.", MessageType.Info);
                return;
            }

            var manager = Object.FindAnyObjectByType<MonsterTacticsU62RoundManager>();
            if (manager == null)
            {
                EditorGUILayout.HelpBox("Round manager not found in the active scene.", MessageType.Warning);
                return;
            }
            EditorGUILayout.LabelField("Stage", manager.StageRound);
            EditorGUILayout.LabelField("Trainer HP", manager.PlayerHP + " / " + manager.MaxPlayerHP);
            EditorGUILayout.LabelField("Last Damage", manager.LastRoundDamage.ToString());
            EditorGUILayout.LabelField("Remaining Foes", manager.LastEnemiesSurvived.ToString());
            EditorGUILayout.Space(8);
            EditorGUI.BeginDisabledGroup(manager.MatchEnded);
            if (GUILayout.Button("Set Trainer HP to 1")) manager.DebugSetPlayerHP(1);
            if (GUILayout.Button("Restore Trainer HP to 100")) manager.DebugSetPlayerHP(manager.MaxPlayerHP);
            if (GUILayout.Button("Preview Elimination Screen")) manager.DebugEndMatch(false);
            if (GUILayout.Button("Preview VortexPrime Victory Screen")) manager.DebugEndMatch(true);
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.Space(8);
            if (GUILayout.Button("Restart Match")) manager.RestartMatch();
            Repaint();
        }
    }
}
