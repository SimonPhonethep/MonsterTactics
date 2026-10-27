using UnityEditor;
using UnityEngine;

namespace MonsterTactics.Editor
{
    /// <summary>Play Mode only. Does not modify scenes or serialized balance settings.</summary>
    public sealed class MonsterTacticsBossTesterWindow : EditorWindow
    {
        private readonly string[] bossNames =
        {
            "2-7 • Skyclaw", "3-7 • Huggevodrel", "4-7 • MonolithKing",
            "5-7 • Gloomborne", "6-7 • Starshifter", "7-7 • Dreadwing", "8-1 • VortexPrime"
        };
        private int selected;
        private int targetLevel = 4;
        private int targetGold = 200;

        [MenuItem("MonsterTactics/Tests/Archive/Combat and Monster/U6.3.2 Solo Boss Tester", priority = 233)]
        public static void ShowWindow()
        {
            GetWindow<MonsterTacticsBossTesterWindow>("MonsterTactics Boss Tester");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("U6.3.2 · Solo Boss Encounters", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Play Mode only. Jump straight to any boss, buy/deploy your team at your own pace, then start combat. PvE does not change PvP streaks.", MessageType.Info);
            int choice = EditorGUILayout.Popup("Encounter", selected, bossNames);
            if (choice != selected)
            {
                selected = choice;
                targetLevel = Mathf.Min(10, selected + 4);
            }
            targetLevel = EditorGUILayout.IntSlider("Test player level", targetLevel, 1, 10);
            targetGold = Mathf.Max(0, EditorGUILayout.IntField("Test gold", targetGold));

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Press Play in Unity before using this tester.", MessageType.Warning);
                return;
            }
            MonsterTacticsU62RoundManager rounds = Object.FindAnyObjectByType<MonsterTacticsU62RoundManager>();
            MonsterTacticsU5ShopManager shop = Object.FindAnyObjectByType<MonsterTacticsU5ShopManager>();
            MonsterTacticsCombatDirector combat = Object.FindAnyObjectByType<MonsterTacticsCombatDirector>();
            if (rounds == null || shop == null || combat == null || !shop.IsShopReady)
            {
                EditorGUILayout.HelpBox("Round manager, shop, or combat director is not ready. Open your U6.3.1 scene and press Play.", MessageType.Error);
                return;
            }
            int stage = selected + 2;
            int round = stage == 8 ? 1 : 7;
            bool spriteReady = MonsterTacticsBossEncounters.SpriteReady(stage, round);
            if (!spriteReady)
                EditorGUILayout.HelpBox("Sprite not installed: " + MonsterTacticsBossEncounters.All[selected].Species +
                    ". Stop Play and use MonsterTactics > Monster > U6.3.2 > Install Boss Animated Sprites.", MessageType.Warning);

            EditorGUILayout.Space(8);
            if (GUILayout.Button("Jump to Boss + Set Level & Gold", GUILayout.Height(34)))
            {
                if (rounds.DebugJumpToBoss(stage))
                    shop.DebugPrepareBossTest(targetLevel, targetGold);
                else Debug.LogWarning("MonsterTactics: Could not jump to boss; check round-manager component.");
            }
            EditorGUILayout.HelpBox("After jumping, the planning timer is PAUSED. Use the shop and hex board to build your team, then start the fight here.", MessageType.None);
            EditorGUI.BeginDisabledGroup(!spriteReady || combat.MatchPhase != "Planning" ||
                rounds.Stage != stage || rounds.Round != round || !combat.HasDeployedTeam);
            if (GUILayout.Button("Start Fight Now", GUILayout.Height(34)))
                if (!rounds.DebugStartBossNow())
                    Debug.LogWarning("MonsterTactics boss test could not start. Deploy at least one unit and verify boss sprites.");
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.Space(7);
            EditorGUILayout.LabelField("Current match: " + rounds.StageRound + "  |  " + combat.MatchPhase);
            EditorGUILayout.LabelField("Your team: " + shop.DeployedCountForHud + "/" + shop.TeamLimit +
                "  |  Gold: " + shop.Gold + "  |  Level: " + shop.PlayerLevel);
            EditorGUILayout.LabelField("PvP streak: " + shop.WinStreak + " wins / " + shop.LossStreak + " losses");
            if (combat.MatchPhase == "Results")
                EditorGUILayout.LabelField("Result: " + combat.LastResult, EditorStyles.boldLabel);
            if (Event.current.type == EventType.Repaint) Repaint();
        }
    }
}
