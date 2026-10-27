using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MonsterTactics.Editor
{
    public static class MonsterTacticsU62Menu
    {
        [MenuItem("MonsterTactics/U6.2/Install Automatic Rounds", priority = 210)]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("MonsterTactics U6.2", "Exit Play mode before installing.", "OK");
                return;
            }
            var combat = Object.FindAnyObjectByType<MonsterTacticsCombatDirector>();
            if (combat == null)
            {
                EditorUtility.DisplayDialog("MonsterTactics U6.2", "Install U4 combat in this scene first.", "OK");
                return;
            }
            var existing = Object.FindAnyObjectByType<MonsterTacticsU62RoundManager>();
            if (existing == null)
            {
                Undo.AddComponent<MonsterTacticsU62RoundManager>(combat.gameObject);
            }
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorUtility.DisplayDialog("MonsterTactics U6.2", "Automatic rounds installed. Save the scene, then press Play.", "OK");
        }
    }
}
