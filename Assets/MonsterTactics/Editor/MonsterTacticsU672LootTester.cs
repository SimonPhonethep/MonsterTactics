#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace MonsterTactics
{
    public sealed class MonsterTacticsU672LootTester : EditorWindow
    {
        private Vector2 scroll;
        [MenuItem("MonsterTactics/Tests/Archive/Items/U6.7.2 Controlled PvE Loot Tester")]
        public static void Open() => GetWindow<MonsterTacticsU672LootTester>("U6.7.2 Loot");

        private void OnGUI()
        {
            EditorGUILayout.LabelField("MATCH-CONTROLLED LOOT",EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Press Play first. Loot allocation is fixed for the entire match: no per-enemy reroll. " +
                "PvE results release balls onto the board; right-click to move your trainer onto them.",MessageType.Info);
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to test live loot.",MessageType.Warning);
                return;
            }
            var loot=FindAnyObjectByType<MonsterTacticsLootController>();
            var rounds=FindAnyObjectByType<MonsterTacticsU62RoundManager>();
            if(loot==null)
            {
                EditorGUILayout.HelpBox("No loot controller yet. Start the U6.2 round-manager scene.",MessageType.Warning);
                return;
            }
            EditorGUILayout.LabelField("Seed",loot.MatchSeed.ToString());
            EditorGUILayout.LabelField("Scheduled components",loot.PlannedComponents.ToString());
            EditorGUILayout.LabelField("Prismatic planned",loot.MasterBallScheduled?"YES (once maximum)":"NO");
            EditorGUILayout.LabelField("Balls on board",loot.PickupCount.ToString());
            EditorGUILayout.LabelField("Loose gold coins",loot.LooseCoinCount.ToString());
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Generate test loot",EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if(GUILayout.Button("Silver"))loot.DebugSpawn(MonsterTacticsLootController.Tier.Silver);
            if(GUILayout.Button("Blue"))loot.DebugSpawn(MonsterTacticsLootController.Tier.Blue);
            if(GUILayout.Button("Gold"))loot.DebugSpawn(MonsterTacticsLootController.Tier.Gold);
            if(GUILayout.Button("Master"))loot.DebugSpawn(MonsterTacticsLootController.Tier.Prismatic);
            EditorGUILayout.EndHorizontal();
            if(GUILayout.Button("Open all waiting balls"))loot.DebugCollectAll();
            if(GUILayout.Button("Collect all loose coins (+1 gold each)"))loot.DebugCollectAllCoins();
            EditorGUILayout.HelpBox("Silver test ball contains 2 gold. Walk over it to open, then pick up the two separate coins. The gold counter should rise by one for each coin. You can also use the Collect all loose coins button after they land.",MessageType.None);
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Real PvE round",EditorStyles.boldLabel);
            if(rounds!=null)
            {
                EditorGUILayout.LabelField("Current",rounds.StageRound+" / "+rounds.CurrentKind);
                if(GUILayout.Button("Release this round's scheduled loot (once only)"))
                    loot.ReleaseForEncounter(rounds.Stage,rounds.Round,rounds.CurrentKind);
            }
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Preallocated match rewards",EditorStyles.boldLabel);
            scroll=EditorGUILayout.BeginScrollView(scroll,GUILayout.ExpandHeight(true));
            EditorGUILayout.TextArea(loot.PlanSummary(),GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
            if(GUILayout.Button("Print plan to Unity Console"))Debug.Log(loot.PlanSummary());
        }
    }
}
#endif
