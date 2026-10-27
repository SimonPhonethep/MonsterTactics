#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace MonsterTactics
{
    public static class MonsterTacticsU692a2StartupVerification
    {
        [MenuItem("MonsterTactics/Tests/Archive/Augments/U6.9.2a.2 Verify Augment Startup (Play Mode)")]
        private static void Verify()
        {
            if (!Application.isPlaying)
            {
                EditorUtility.DisplayDialog("U6.9.2a.2", "Start Play Mode before verifying.", "OK");
                return;
            }
            string report;
            var manager = MonsterTacticsAugmentRuntimeBootstrap.EnsureActiveManager(out report);
            bool passed = manager != null && manager.IsActive && manager.DefinitionCount > 0;
            if (passed)
            {
                Debug.Log("[U6.9.2a.2] PASS: " + report +
                    " Runtime startup wiring present; this test grants no rewards.", manager);
                EditorUtility.DisplayDialog("U6.9.2a.2", "PASS: " + report +
                    "\nNow run Preview Augment Choice.", "OK");
            }
            else
            {
                Debug.LogError("[U6.9.2a.2] FAIL: " + report);
                EditorUtility.DisplayDialog("U6.9.2a.2", "FAIL: " + report, "OK");
            }
        }
    }
}
#endif
