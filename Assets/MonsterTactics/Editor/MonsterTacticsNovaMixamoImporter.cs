#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace MonsterTactics.Editor
{
    // Generates a 1-state walk controller from the Mixamo FBX already containing
    // a compatible skinned Nova mesh and walking clip. No manual Animator setup.
    public sealed class MonsterTacticsNovaMixamoImporter : AssetPostprocessor
    {
        private const string FbxPath = "Assets/MonsterTactics/Resources/Trainers/NovaMixamo/NovaRigged.dae";
        private const string ControllerPath = "Assets/MonsterTactics/Resources/Trainers/NovaMixamo/NovaWalk.controller";

        private void OnPreprocessModel()
        {
            if (!string.Equals(assetPath, FbxPath, StringComparison.OrdinalIgnoreCase)) return;
            ModelImporter importer = assetImporter as ModelImporter;
            if (importer == null) return;
            // We use the model and clip from the same Mixamo skeleton, so Generic
            // preserves the original bone animation without any retargeting.
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.importAnimation = true;
            ModelImporterClipAnimation[] clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0)
                clips = importer.defaultClipAnimations;
            if (clips != null && clips.Length > 0)
            {
                foreach (ModelImporterClipAnimation clip in clips)
                {
                    clip.loopTime = true;
                    clip.loopPose = true;
                    clip.lockRootPositionXZ = true;
                    clip.lockRootHeightY = true;
                }
                importer.clipAnimations = clips;
            }
        }

        private static void OnPostprocessAllAssets(string[] importedAssets,
            string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            if (!importedAssets.Any(p => string.Equals(p, FbxPath, StringComparison.OrdinalIgnoreCase)))
                return;
            EditorApplication.delayCall += InstallController;
        }

        [InitializeOnLoadMethod]
        private static void AfterEditorReload()
        {
            // The model may import before Editor scripts finish compiling.
            EditorApplication.delayCall += EnsureLoopingWalk;
        }

        // Already-installed projects may have imported the FBX before the loop
        // flags were set. Run once after an editor reload and repair only when
        // necessary. This doesn't touch the model's mesh, materials or bones.
        private static void EnsureLoopingWalk()
        {
            ModelImporter importer = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
            if (importer == null) return;
            ModelImporterClipAnimation[] clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0)
                clips = importer.defaultClipAnimations;
            if (clips == null || clips.Length == 0) return;
            bool changed = false;
            foreach (ModelImporterClipAnimation clip in clips)
            {
                if (!clip.loopTime || !clip.loopPose ||
                    !clip.lockRootPositionXZ || !clip.lockRootHeightY)
                {
                    clip.loopTime = true;
                    clip.loopPose = true;
                    clip.lockRootPositionXZ = true;
                    clip.lockRootHeightY = true;
                    changed = true;
                }
            }
            if (!changed) return;
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
            Debug.Log("MonsterTactics Nova: Reimported Mixamo walk clip with looping enabled.");
        }

        [MenuItem("MonsterTactics/Trainers/Repair Nova Mixamo Walk Controller")]
        public static void InstallController()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath) == null) return;
            // Never overwrite a user's edited animation controller.
            if (AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath) != null) return;
            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(FbxPath)
                .OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase) && c.length > 0.01f)
                .OrderByDescending(c => c.length)
                .FirstOrDefault();
            if (clip == null)
            {
                Debug.LogWarning("MonsterTactics Nova: FBX imported without a usable walking clip. " +
                    "Select NovaRigged.dae, enable Import Animation, and reimport.");
                return;
            }
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            AnimatorState walk = stateMachine.AddState("Walk");
            walk.motion = clip;
            stateMachine.defaultState = walk;
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            Debug.Log("MonsterTactics Nova: Created NovaWalk.controller using animation clip '" + clip.name + "'.");
        }
    }
}
#endif
