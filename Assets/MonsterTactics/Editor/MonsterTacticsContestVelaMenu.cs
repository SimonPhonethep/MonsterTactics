using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using MonsterTactics;

namespace MonsterTactics.Editor
{
    /// <summary>
    /// Adds the user's ORAS Contest Vela Collada skin as a NEW trainer option.
    /// Does not touch regular Vela, Ranger, trainer movement, arena or Monster.
    /// VelaFestival uses its own skinned meshes, own materials, and own Animator.
    /// </summary>
    public static class MonsterTacticsContestVelaMenu
    {
        private const string TrainerName = "MonsterTactics Trainer";
        private const string ProxyName = "Contest Vela 3D - Proxy";
        private const string ModelName = "Vela Contest Model (Imported)";
        private const string Root = "Assets/MonsterTactics/Art/Trainer/VelaFestival";
        private const string DaePath = Root + "/rstr0003_00_fi.dae";
        private const string MaterialsPath = Root + "/Materials";
        private const float TargetHeight = 1.55f;

        [MenuItem("MonsterTactics/Trainers/Contest Vela/Install Contest Vela - 5 Skins (U3.7)", priority = 180)]
        public static void InstallContestVela()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("MonsterTactics", "Stop Play mode before installing Contest Vela.", "OK");
                return;
            }

            GameObject trainer = FindTrainerInScene();
            if (trainer == null)
            {
                EditorUtility.DisplayDialog("MonsterTactics", "Open your arena scene with the 'MonsterTactics Trainer' object.", "OK");
                return;
            }
            TrainerSkinController skins = trainer.GetComponent<TrainerSkinController>();
            if (skins == null)
            {
                EditorUtility.DisplayDialog("MonsterTactics", "Install the U2.4/U3.6.2 trainer skin system first.", "OK");
                return;
            }
            if (trainer.GetComponentInChildren<VelaModernClipDriver>(true) == null)
                Debug.LogWarning("MonsterTactics Contest Vela: U3.6 VelaModernClipDriver wasn't found yet. Import the earlier U3.6 animation update first if you get a script error.");

            ConfigureImporter();
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(DaePath);
            if (source == null)
            {
                EditorUtility.DisplayDialog("MonsterTactics", "Contest Vela .dae was not imported: " + DaePath +
                    "\nCheck the Unity Console for model import errors.", "OK");
                return;
            }

            Material body = MakeMaterial("ContestVela_Body", "rstr0003_00_fi_body.png", true);
            Material face = MakeMaterial("ContestVela_Face", "rstr0003_00_fi_face.png", false);
            Material head = MakeMaterial("ContestVela_Head", "rstr0003_00_fi_head.png", false);
            if (body == null || face == null || head == null)
            {
                EditorUtility.DisplayDialog("MonsterTactics", "One or more Contest Vela textures are missing.", "OK");
                return;
            }

            Transform proxy = trainer.transform.Find(ProxyName);
            if (proxy == null)
            {
                GameObject newProxy = new GameObject(ProxyName);
                Undo.RegisterCreatedObjectUndo(newProxy, "Add Contest Vela visual");
                newProxy.transform.SetParent(trainer.transform, false);
                proxy = newProxy.transform;
            }
            // Temporarily enable the proxy so renderer bounds are valid for initial fitting.
            bool previousActive = proxy.gameObject.activeSelf;
            proxy.gameObject.SetActive(true);
            Transform existing = proxy.Find(ModelName);
            if (existing == null)
            {
                GameObject newModel = PrefabUtility.InstantiatePrefab(source) as GameObject;
                if (newModel == null)
                {
                    proxy.gameObject.SetActive(previousActive);
                    EditorUtility.DisplayDialog("MonsterTactics", "Could not instantiate Contest Vela model. Check the .dae in Project.", "OK");
                    return;
                }
                Undo.RegisterCreatedObjectUndo(newModel, "Import Contest Vela model");
                newModel.name = ModelName;
                Undo.SetTransformParent(newModel.transform, proxy, "Parent Contest Vela model");
                newModel.transform.localPosition = Vector3.zero;
                newModel.transform.localRotation = Quaternion.identity;
                newModel.transform.localScale = Vector3.one;
                existing = newModel.transform;
            }
            existing.gameObject.SetActive(true);

            foreach (Renderer renderer in existing.GetComponentsInChildren<Renderer>(true))
            {
                string name = renderer.gameObject.name.ToLowerInvariant();
                Material selected = name.Contains("face") ? face : name.Contains("hair") ? head : body;
                // Source COLLADA binds each mesh to one material; preserve any extra submesh slots.
                Material[] slots = renderer.sharedMaterials;
                if (slots == null || slots.Length == 0) continue;
                for (int i = 0; i < slots.Length; ++i)
                {
                    string matName = slots[i] != null ? slots[i].name.ToLowerInvariant() : "";
                    slots[i] = matName.Contains("face") ? face : matName.Contains("head") ? head : selected;
                }
                Undo.RecordObject(renderer, "Assign Contest Vela materials");
                renderer.sharedMaterials = slots;
                if (renderer is SkinnedMeshRenderer skinned) skinned.updateWhenOffscreen = true;
                EditorUtility.SetDirty(renderer);
            }

            if (existing.localScale == Vector3.one && existing.localPosition == Vector3.zero)
                FitToHeightAndFloor(existing.gameObject, proxy, TargetHeight);

            Undo.RecordObject(skins, "Add Contest Vela skin slot");
            skins.ConfigureContestVela(proxy.gameObject);
            // Skin controller owns final activation state (and keeps any active Ranger selection).
            EditorUtility.SetDirty(skins);

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = existing.gameObject;
            MonsterTacticsContestVelaClipMenu.InstallContestAnimations();
            Debug.Log("MonsterTactics U3.7: Contest Vela installed as key 4; Ranger moved to key 5. " +
                "Existing regular Vela (key 3) and all movement systems preserved.", trainer);
        }

        [MenuItem("MonsterTactics/Trainers/Contest Vela/Rotate Contest Vela 180 Degrees", priority = 190)]
        private static void FlipFacing()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            GameObject trainer = FindTrainerInScene();
            Transform proxy = trainer != null ? trainer.transform.Find(ProxyName) : null;
            Transform model = proxy != null ? proxy.Find(ModelName) : null;
            if (model == null)
            {
                EditorUtility.DisplayDialog("MonsterTactics", "Install Contest Vela first.", "OK");
                return;
            }
            Undo.RecordObject(model, "Rotate Contest Vela model");
            model.localRotation *= Quaternion.Euler(0f, 180f, 0f);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        private static GameObject FindTrainerInScene()
        {
            foreach (GameObject obj in SceneManager.GetActiveScene().GetRootGameObjects())
                if (obj.name == TrainerName) return obj;
            return null;
        }

        private static void ConfigureImporter()
        {
            var importer = AssetImporter.GetAtPath(DaePath) as ModelImporter;
            if (importer == null) return;
            bool modified = false;
            if (importer.animationType != ModelImporterAnimationType.Generic)
            {
                importer.animationType = ModelImporterAnimationType.Generic;
                modified = true;
            }
            if (importer.optimizeGameObjects)
            {
                importer.optimizeGameObjects = false; // Needed for exact bone paths in editable clips.
                modified = true;
            }
            if (modified) importer.SaveAndReimport();
        }

        private static Material MakeMaterial(string label, string textureFile, bool cutout)
        {
            string texturePath = Root + "/" + textureFile;
            var importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if (importer != null)
            {
                bool change = importer.textureType != TextureImporterType.Default ||
                              importer.mipmapEnabled || importer.textureCompression != TextureImporterCompression.Uncompressed ||
                              importer.filterMode != FilterMode.Point;
                if (change)
                {
                    importer.textureType = TextureImporterType.Default;
                    importer.mipmapEnabled = false;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.filterMode = FilterMode.Point;
                    importer.SaveAndReimport();
                }
            }
            Texture2D image = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (image == null) return null;
            if (!AssetDatabase.IsValidFolder(MaterialsPath)) AssetDatabase.CreateFolder(Root, "Materials");
            string materialPath = MaterialsPath + "/" + label + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                if (shader == null) throw new InvalidOperationException("URP Lit/Standard shader not found.");
                material = new Material(shader) { name = label };
                AssetDatabase.CreateAsset(material, materialPath);
            }
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", image);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", image);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
            else if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);
            if (material.HasProperty("_AlphaClip")) material.SetFloat("_AlphaClip", cutout ? 1f : 0f);
            if (material.HasProperty("_Cutoff")) material.SetFloat("_Cutoff", .45f);
            if (cutout) material.EnableKeyword("_ALPHATEST_ON");
            else material.DisableKeyword("_ALPHATEST_ON");
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void FitToHeightAndFloor(GameObject model, Transform proxy, float targetHeight)
        {
            if (!TryBounds(model, out Bounds bounds))
            {
                Debug.LogWarning("MonsterTactics Contest Vela: Couldn't read renderer bounds; adjust imported model scale manually.");
                return;
            }
            float scale = Mathf.Clamp(targetHeight / Mathf.Max(.0001f, bounds.size.y), .0001f, 500f);
            model.transform.localScale = Vector3.one * scale;
            if (!TryBounds(model, out bounds)) return;
            model.transform.position += new Vector3(proxy.position.x - bounds.center.x,
                                                     proxy.position.y - bounds.min.y,
                                                     proxy.position.z - bounds.center.z);
        }

        private static bool TryBounds(GameObject model, out Bounds bounds)
        {
            bounds = new Bounds();
            bool found = false;
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                if (!found) { bounds = renderer.bounds; found = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
            return found && bounds.size.sqrMagnitude > .0000001f;
        }
    }
}
