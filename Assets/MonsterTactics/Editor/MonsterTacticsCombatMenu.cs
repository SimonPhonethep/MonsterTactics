using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MonsterTactics.Editor
{
    /// <summary>
    /// U4 is additive: never regenerates the arena, Monster, models or trainers.
    /// </summary>
    public static class MonsterTacticsCombatMenu
    {
        private const string DirectorName = "MonsterTactics U4 Combat Director";

        [MenuItem("MonsterTactics/Combat/Install U4 Auto-Combat", priority = 95)]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("MonsterTactics U4", "Stop Play mode before installing combat.", "OK");
                return;
            }
            GameObject arena = GameObject.Find("MonsterTactics Arena");
            GameObject team = GameObject.Find("MonsterTactics U3 Monster Team");
            MonsterPlacementController placement = team != null
                ? team.GetComponent<MonsterPlacementController>() : null;
            if (arena == null || placement == null ||
                arena.GetComponentsInChildren<HexTile>(true).Length != 64 ||
                team.GetComponentsInChildren<MonsterUnit>(true).Length == 0)
            {
                EditorUtility.DisplayDialog("MonsterTactics U4",
                    "The scene needs the existing U1 arena (64 tiles) and your U3 Monster team. " +
                    "Open the scene where they already work; do not regenerate it.", "OK");
                return;
            }
            Camera camera = Camera.main;
            if (camera == null) camera = Object.FindAnyObjectByType<Camera>();
            if (camera == null)
            {
                EditorUtility.DisplayDialog("MonsterTactics U4", "No camera found in this scene.", "OK");
                return;
            }

            GameObject existing = GameObject.Find(DirectorName);
            GameObject directorObject = existing;
            if (directorObject == null)
            {
                directorObject = new GameObject(DirectorName);
                Undo.RegisterCreatedObjectUndo(directorObject, "Create MonsterTactics Combat Director");
            }
            MonsterTacticsCombatDirector director = directorObject.GetComponent<MonsterTacticsCombatDirector>();
            if (director == null) director = Undo.AddComponent<MonsterTacticsCombatDirector>(directorObject);

            Undo.RecordObject(director, "Configure MonsterTactics U4 Combat");
            SerializedObject serialized = new SerializedObject(director);
            serialized.FindProperty("arenaRoot").objectReferenceValue = arena.transform;
            serialized.FindProperty("placementController").objectReferenceValue = placement;
            serialized.FindProperty("sceneCamera").objectReferenceValue = camera;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(director);
            Selection.activeGameObject = directorObject;
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("MonsterTactics U4 installed. Save the scene, press Play, then click START BATTLE (bottom-right)." +
                " Trainer skins and controls are untouched.");
        }

        [MenuItem("MonsterTactics/Combat/Run U4 Rules Self-Check", priority = 96)]
        public static void RulesSelfCheck()
        {
            Assert(MonsterTacticsCombatRules.HexDistance(new Vector2Int(2, 2), new Vector2Int(2, 2)) == 0,
                "Distance to self must be 0");
            for (int row = 1; row < 7; row++)
            {
                Vector2Int origin = new Vector2Int(3, row);
                int neighbors = 0;
                foreach (Vector2Int neighbor in MonsterTacticsCombatRules.Neighbors(origin))
                {
                    Assert(MonsterTacticsCombatRules.HexDistance(origin, neighbor) == 1,
                        "Every hex neighbor must be distance 1");
                    neighbors++;
                }
                Assert(neighbors == 6, "Interior tile must have six neighbors");
            }
            Assert(Mathf.Approximately(MonsterTacticsCombatRules.Mitigate(150f, 50), 100f),
                "Armor/MR mitigation failed");
            Assert(MonsterTacticsCombatRules.ForMonster("Ripplet").Ability == "Aqua Jet", "Ripplet ability missing");
            Assert(MonsterTacticsCombatRules.ForMonster("Emberlet").Range == 3, "Ranged stats missing");
            Debug.Log("MonsterTactics U4: combat rules self-check PASSED (hex neighbors, distances, mitigation, roster).");
        }

        private static void Assert(bool condition, string error)
        {
            if (!condition) throw new System.InvalidOperationException("MonsterTactics U4 rules check: " + error);
        }
    }
}
