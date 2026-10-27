#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MonsterTactics
{
    public static class MonsterTacticsU691f4aHudTest
    {
        [MenuItem("MonsterTactics/Tests/Archive/Shop and Progression/U6.9.1f.4a Verify Shop Type Icons (Play Mode)")]
        private static void Verify()
        {
            if (!Application.isPlaying)
            {
                EditorUtility.DisplayDialog("U6.9.1f.4a HUD Test",
                    "Enter Play Mode first, then run this test.", "OK");
                return;
            }
            MonsterTacticsU5CanvasHud hud = Object.FindAnyObjectByType<MonsterTacticsU5CanvasHud>();
            if (hud == null || !hud.IsReady)
            {
                Debug.LogError("[U6.9.1f.4a] HUD not found or not ready.");
                return;
            }
            var icons = hud.GetComponentsInChildren<RawImage>(true);
            int count = 0;
            foreach (var icon in icons)
            {
                if (icon != null && icon.name == "Monster type icon") count++;
            }
            // The shop uses 5 cards x 2 type icons. The HUD Canvas is built
            // on a separate root, so search that Canvas, not the HUD owner.
            if (count < 10)
            {
                Canvas[] canvases = Object.FindObjectsByType<Canvas>();
                foreach (Canvas canvas in canvases)
                {
                    if (canvas == null || !canvas.name.Contains("MonsterTactics U5.2")) continue;
                    count = 0;
                    foreach (var icon in canvas.GetComponentsInChildren<RawImage>(true))
                    {
                        if (icon != null && icon.name == "Monster type icon") count++;
                    }
                    break;
                }
            }
            if (count == 10)
                Debug.Log("[U6.9.1f.4a] PASS: all 10 shop type icons exist.");
            else
                Debug.LogError("[U6.9.1f.4a] FAIL: found " + count +
                    " of 10 shop type icons. Check if the shop HUD built completely.");
        }
    }
}
#endif
