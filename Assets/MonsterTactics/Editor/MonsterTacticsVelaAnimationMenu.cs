using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MonsterTactics.Editor
{
    /// <summary>Upgrades the ONE existing Vela animation component; never spawns trainers.</summary>
    public static class MonsterTacticsVelaAnimationMenu
    {
        private const string RootName = "MonsterTactics Trainer";
        private const string SkinName = "Modern Vela 3D - Proxy";
        private const string ModelName = "Vela ORAS Model (Imported)";

        [MenuItem("MonsterTactics/Trainers/Apply Vela Animation Polish (U3.5)", priority = 93)]
        private static void Apply() { Configure(false); }

        [MenuItem("MonsterTactics/Trainers/Test Vela Walk In Place (U3.5)", priority = 94)]
        private static void Test() { Configure(true); }

        [MenuItem("MonsterTactics/Trainers/Stop Vela Walk Test (U3.5)", priority = 95)]
        private static void StopTest() { Configure(false); }

        private static void Configure(bool test)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("MonsterTactics U3.5", "Stop Play mode before changing Vela's walk settings.", "OK");
                return;
            }

            GameObject trainer = GameObject.Find(RootName);
            Transform proxy = trainer != null ? trainer.transform.Find(SkinName) : null;
            Transform mayModel = proxy != null ? proxy.Find(ModelName) : null;
            if (mayModel == null)
            {
                EditorUtility.DisplayDialog("MonsterTactics U3.5",
                    "Vela's imported model was not found in the current scene. Expected:\n" +
                    RootName + " / " + SkinName + " / " + ModelName +
                    "\n\nOpen your saved arena scene. Do NOT regenerate the board or reinstall Vela.", "OK");
                return;
            }

            VelaModernProceduralWalk animator = mayModel.GetComponent<VelaModernProceduralWalk>();
            if (animator == null)
                animator = Undo.AddComponent<VelaModernProceduralWalk>(mayModel.gameObject);

            Undo.RecordObject(animator, "Configure U3.5 ORAS Vela Animation");
            bool found = animator.ConfigurePolishedWalk(test);
            EditorUtility.SetDirty(animator);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Selection.activeGameObject = mayModel.gameObject;

            if (!found)
            {
                EditorUtility.DisplayDialog("MonsterTactics U3.5",
                    "Some required bones or TrainerMovement were not found. " +
                    "Check the imported Vela model contains LThigh, RThigh, LShoulder and RShoulder. " +
                    "Also ensure the model is a child of your active MonsterTactics Trainer.", "OK");
                return;
            }

            Debug.Log("MonsterTactics U3.5: Existing Vela walk upgraded; " +
                      (test ? "walk-in-place enabled" : "normal walking enabled") + ".", mayModel.gameObject);
            EditorUtility.DisplayDialog("MonsterTactics U3.5",
                test
                    ? "Walk-in-place enabled. Save, press Play, select Vela with 3. " +
                      "She should swing her arms, step with both legs, and bend her knees without clicking. " +
                      "Stop Play and run Stop Vela Walk Test afterward."
                    : "Walking polish installed. Save, press Play, select Vela with 3, " +
                      "then right-click across the arena. Her existing facing direction is preserved.", "OK");
        }
    }
}
