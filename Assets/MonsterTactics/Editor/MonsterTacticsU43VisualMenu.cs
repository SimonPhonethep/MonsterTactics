using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MonsterTactics.Editor
{
    /// <summary>
    /// Reversible presentation-only adjustments: sprite size and camera zoom.
    /// No tiles, rigging, trainer controls, damage or game systems are modified.
    /// </summary>
    public static class MonsterTacticsU43VisualMenu
    {
        private const string ConfigName = "MonsterTactics U4.3 Visual Settings";
        private const float SpriteScaleFactor = 1.14f;
        private const float CameraZoomFactor = 0.89f;

        [MenuItem("MonsterTactics/Visuals/U4.3 - Apply Arena & Monster Polish", priority = 160)]
        private static void Apply()
        {
            if (!CanEdit()) return;
            GameObject arena = GameObject.Find("MonsterTactics Arena");
            GameObject team = GameObject.Find("MonsterTactics U3 Monster Team");
            Camera camera = Camera.main;
            if (camera == null) camera = Object.FindAnyObjectByType<Camera>();
            if (arena == null || team == null || camera == null)
            {
                EditorUtility.DisplayDialog("MonsterTactics U4.3",
                    "Open your U4.2 scene containing MonsterTactics Arena, the U3 Monster team, and the camera.", "OK");
                return;
            }
            GameObject configObject = GameObject.Find(ConfigName);
            MonsterTacticsU43PresentationSettings state;
            if (configObject == null)
            {
                configObject = new GameObject(ConfigName);
                Undo.RegisterCreatedObjectUndo(configObject, "Create U4.3 visual settings");
                state = Undo.AddComponent<MonsterTacticsU43PresentationSettings>(configObject);
            }
            else
            {
                state = configObject.GetComponent<MonsterTacticsU43PresentationSettings>();
                if (state == null) state = Undo.AddComponent<MonsterTacticsU43PresentationSettings>(configObject);
            }
            Undo.RecordObject(state, "Save original MonsterTactics presentation");
            state.Initialize(camera);
            foreach (MonsterUnit unit in team.GetComponentsInChildren<MonsterUnit>(true))
            {
                SpriteRenderer sprite = unit != null ? unit.GetComponentInChildren<SpriteRenderer>(true) : null;
                if (sprite != null) state.RecordVisual(sprite.transform);
            }

            Camera trackedCamera = state.TargetCamera;
            if (trackedCamera != null && trackedCamera.orthographic)
            {
                Undo.RecordObject(trackedCamera, "Zoom MonsterTactics battlefield");
                trackedCamera.orthographicSize = Mathf.Clamp(
                    state.InitialCameraSize * CameraZoomFactor, 5.7f, 8.5f);
                EditorUtility.SetDirty(trackedCamera);
            }
            foreach (MonsterTacticsU43PresentationSettings.SpriteSize item in state.Sprites)
            {
                if (item.Visual == null) continue;
                Undo.RecordObject(item.Visual, "Resize Monster visual");
                item.Visual.localScale = item.OriginalLocalScale * SpriteScaleFactor;
                EditorUtility.SetDirty(item.Visual);
            }
            EditorUtility.SetDirty(state);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("MonsterTactics U4.3: arena zoomed and Monster sprites enlarged 14%. " +
                "Existing trainers and arena geometry have not changed. Save your scene.");
            Selection.activeObject = configObject;
        }

        [MenuItem("MonsterTactics/Visuals/U4.3 - Restore Original Zoom & Sizes", priority = 161)]
        private static void Restore()
        {
            if (!CanEdit()) return;
            GameObject config = GameObject.Find(ConfigName);
            MonsterTacticsU43PresentationSettings state = config != null ?
                config.GetComponent<MonsterTacticsU43PresentationSettings>() : null;
            if (state == null)
            {
                EditorUtility.DisplayDialog("MonsterTactics U4.3", "No saved U4.3 presentation changes found.", "OK");
                return;
            }
            if (state.TargetCamera != null)
            {
                Undo.RecordObject(state.TargetCamera, "Restore arena camera");
                state.TargetCamera.orthographicSize = state.InitialCameraSize;
                EditorUtility.SetDirty(state.TargetCamera);
            }
            foreach (MonsterTacticsU43PresentationSettings.SpriteSize item in state.Sprites)
            {
                if (item.Visual == null) continue;
                Undo.RecordObject(item.Visual, "Restore Monster visual");
                item.Visual.localScale = item.OriginalLocalScale;
                EditorUtility.SetDirty(item.Visual);
            }
            Undo.DestroyObjectImmediate(config);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("MonsterTactics U4.3: restored pre-U4.3 camera zoom and sprite sizes.");
        }

        private static bool CanEdit()
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode) return true;
            EditorUtility.DisplayDialog("MonsterTactics", "Exit Play mode before changing visuals.", "OK");
            return false;
        }
    }
}
