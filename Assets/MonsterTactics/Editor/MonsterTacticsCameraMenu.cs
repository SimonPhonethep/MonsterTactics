using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MonsterTactics.Editor
{
    /// <summary>
    /// Updates the existing scene's camera without rebuilding the arena.
    /// Both commands can be undone with Ctrl+Z.
    /// </summary>
    public static class MonsterTacticsCameraMenu
    {
        [MenuItem("MonsterTactics/Camera/Use TFT-Style View (U2.2)", priority = 60)]
        private static void ApplyTftView()
        {
            SetCamera(50f, 0f, 7.25f);
        }

        [MenuItem("MonsterTactics/Camera/Restore Original Diagonal View", priority = 61)]
        private static void RestoreOriginalView()
        {
            SetCamera(55f, -30f, 8.2f);
        }

        private static void SetCamera(float pitch, float yaw, float size)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("MonsterTactics", "Stop Play mode before changing the camera.", "OK");
                return;
            }

            Camera camera = Camera.main;
            if (camera == null) camera = Object.FindAnyObjectByType<Camera>();
            if (camera == null)
            {
                EditorUtility.DisplayDialog("MonsterTactics", "No camera found. Open your MainArena scene first.", "OK");
                return;
            }

            Undo.RecordObject(camera, "MonsterTactics camera view");
            Undo.RecordObject(camera.transform, "MonsterTactics camera view");

            camera.orthographic = true;
            camera.orthographicSize = size;
            camera.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
            camera.transform.position = new Vector3(0f, 0f, 0f) - camera.transform.forward * 18f;

            EditorUtility.SetDirty(camera);
            EditorUtility.SetDirty(camera.transform);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("MonsterTactics: updated camera view. Save the scene (Ctrl+S)." );
        }
    }
}
