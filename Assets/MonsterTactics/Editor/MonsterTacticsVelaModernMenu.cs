using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MonsterTactics.Editor
{
    /// <summary>
    /// Loads the user-supplied ORAS Vela FBX and textures into the pre-existing U2.4
    /// "Modern Vela 3D - Proxy" visual. Does not replace the TrainerSkinController,
    /// TrainerMovement, the character selection hotkeys, or the arena.
    /// </summary>
    public static class MonsterTacticsVelaModernMenu
    {
        private const string Root = "Assets/MonsterTactics/Art/Trainer/VelaModern";
        private const string ModelPath = Root + "/VelaOW.dae";
        private const string TextureDir = Root + "/images/";
        private const string MaterialsDir = Root + "/Materials";
        private const string TrainerName = "MonsterTactics Trainer";
        private const string ProxyName = "Modern Vela 3D - Proxy";
        private const string ImportedName = "Vela ORAS Model (Imported)";
        private const float TargetHeight = 1.55f; // Arena tile world units

        [MenuItem("MonsterTactics/Trainers/Replace 3D Vela with ORAS Model (U3.3)", priority = 90)]
        public static void InstallVela()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("MonsterTactics", "Stop Play mode before importing Vela.", "OK");
                return;
            }

            GameObject trainer = GameObject.Find(TrainerName);
            if (trainer == null)
            {
                EditorUtility.DisplayDialog("MonsterTactics", "Couldn't find 'MonsterTactics Trainer'. Add the movable trainer first.", "OK");
                return;
            }

            Transform proxy = trainer.transform.Find(ProxyName);
            if (proxy == null)
            {
                EditorUtility.DisplayDialog("MonsterTactics", "Couldn't find 'Modern Vela 3D - Proxy'. Install U2.4 trainer skins first.", "OK");
                return;
            }

            GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (modelAsset == null)
            {
                EditorUtility.DisplayDialog("MonsterTactics", "Missing FBX at " + ModelPath + ". Copy the patch's Assets folder into the project first.", "OK");
                return;
            }

            var body = CreateMaterial("Vela_ORAS_Body", "rstr0001_00_fi_body.png", true);
            var head = CreateMaterial("Vela_ORAS_Head", "rstr0001_00_fi_head.png", false);
            var face = CreateMaterial("Vela_ORAS_Face", "rstr0001_00_fi_face.png", false);
            var accessory = CreateMaterial("Vela_ORAS_Accessories", "rstr0001_00_fi_sw_parts.png", false);
            if (body == null || head == null || face == null || accessory == null)
            {
                EditorUtility.DisplayDialog("MonsterTactics", "One or more Vela texture files could not be imported. Check Console and the images folder.", "OK");
                return;
            }

            // Keep the PROXY object itself, because U2.4 has a serialized reference to it.
            // Only swap its children so skin 3 and existing trainer motion keep working.
            bool wasActive = proxy.gameObject.activeSelf;
            Undo.RecordObject(proxy.gameObject, "Replace Vela 3D appearance");
            proxy.gameObject.SetActive(true);
            for (int i = proxy.childCount - 1; i >= 0; i--)
                Undo.DestroyObjectImmediate(proxy.GetChild(i).gameObject);

            GameObject model = PrefabUtility.InstantiatePrefab(modelAsset) as GameObject;
            if (model == null)
            {
                proxy.gameObject.SetActive(wasActive);
                EditorUtility.DisplayDialog("MonsterTactics", "Unity couldn't instantiate the FBX. Check Model import settings in Inspector.", "OK");
                return;
            }
            Undo.RegisterCreatedObjectUndo(model, "Install ORAS Vela model");
            model.name = ImportedName;
            Undo.SetTransformParent(model.transform, proxy, "Parent imported Vela model");
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;
            model.SetActive(true);

            Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer r in renderers)
            {
                // This file also contains snorkel/goggle and alternate shoe meshes.
                // Hide accessories not used by the normal overworld outfit.
                string meshName = r.gameObject.name.ToLowerInvariant();
                if (meshName.Contains("shoesb") || meshName.Contains("snorkel") || meshName.Contains("goggles"))
                {
                    r.gameObject.SetActive(false);
                    continue;
                }
                AssignMaterials(r, body, head, face, accessory);
                if (r is SkinnedMeshRenderer skinned)
                    skinned.updateWhenOffscreen = true;
            }

            FitToHeightAndFloor(model, proxy, TargetHeight);
            proxy.gameObject.SetActive(wasActive);
            EditorUtility.SetDirty(proxy);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Selection.activeGameObject = model;
            AssetDatabase.SaveAssets();
            Debug.Log("MonsterTactics U3.3: ORAS Vela imported under the original skin-3 proxy. Press Play then 3. If she faces backward, use the Rotate Vela 180° menu. No animation clips were supplied with the model.");
        }

        [MenuItem("MonsterTactics/Trainers/Rotate ORAS Vela Model 180° (U3.3)", priority = 91)]
        public static void RotateVela()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("MonsterTactics", "Stop Play mode first.", "OK");
                return;
            }
            Transform model = FindImportedModel();
            if (model == null) return;
            Undo.RecordObject(model, "Rotate imported Vela model 180 degrees");
            model.localRotation = model.localRotation * Quaternion.Euler(0f, 180f, 0f);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        [MenuItem("MonsterTactics/Trainers/Make ORAS Vela 10% Larger (U3.3)", priority = 92)]
        public static void GrowVela() => ResizeVela(1.10f);

        [MenuItem("MonsterTactics/Trainers/Make ORAS Vela 10% Smaller (U3.3)", priority = 93)]
        public static void ShrinkVela() => ResizeVela(1f / 1.10f);

        private static Transform FindImportedModel()
        {
            GameObject trainer = GameObject.Find(TrainerName);
            Transform proxy = trainer != null ? trainer.transform.Find(ProxyName) : null;
            Transform imported = proxy != null ? proxy.Find(ImportedName) : null;
            if (imported == null)
                EditorUtility.DisplayDialog("MonsterTactics", "Import ORAS Vela (U3.3) first.", "OK");
            return imported;
        }

        private static void ResizeVela(float factor)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Transform model = FindImportedModel();
            if (model == null) return;
            Undo.RecordObject(model, "Resize ORAS Vela model");
            model.localScale *= factor;
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        private static Material CreateMaterial(string name, string textureName, bool alphaCutout)
        {
            string path = TextureDir + textureName;
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                bool changed = importer.textureType != TextureImporterType.Default ||
                               importer.mipmapEnabled ||
                               importer.textureCompression != TextureImporterCompression.Uncompressed ||
                               importer.filterMode != FilterMode.Point;
                if (changed)
                {
                    importer.textureType = TextureImporterType.Default;
                    importer.mipmapEnabled = false;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.filterMode = FilterMode.Point;
                    importer.SaveAndReimport();
                }
            }
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null)
            {
                Debug.LogError("MonsterTactics: Vela texture missing: " + path);
                return null;
            }
            EnsureFolder(MaterialsDir);
            string matPath = MaterialsDir + "/" + name + ".mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                if (shader == null) throw new InvalidOperationException("URP Lit/Standard shader unavailable");
                mat = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(mat, matPath);
            }
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            else if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
            if (mat.HasProperty("_AlphaClip")) mat.SetFloat("_AlphaClip", alphaCutout ? 1f : 0f);
            if (mat.HasProperty("_Cutoff")) mat.SetFloat("_Cutoff", 0.45f);
            if (alphaCutout) mat.EnableKeyword("_ALPHATEST_ON");
            else mat.DisableKeyword("_ALPHATEST_ON");
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void EnsureFolder(string assetPath)
        {
            string[] chunks = assetPath.Split('/');
            string current = chunks[0];
            for (int i = 1; i < chunks.Length; i++)
            {
                string next = current + "/" + chunks[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, chunks[i]);
                current = next;
            }
        }

        private static void AssignMaterials(Renderer r, Material body, Material head, Material face, Material accessory)
        {
            var mats = r.sharedMaterials;
            if (mats == null || mats.Length == 0) return;
            string n = r.gameObject.name.ToLowerInvariant();
            bool mainMesh = n == "mayow" || n.Contains("mayow-mesh") ||
                            (n.Contains("mayow") && !n.Contains("shoes") && !n.Contains("ring") && !n.Contains("goggle") && !n.Contains("snorkel"));
            Material[] applied = new Material[mats.Length];
            for (int i = 0; i < mats.Length; i++)
            {
                string id = mats[i] != null ? mats[i].name.ToLowerInvariant() : "";
                // Asset maps from the Collada sidecar: 19=body,20=head,21=face,22=accessories.
                if (id.Contains("__20") || id.Contains("fi_head")) applied[i] = head;
                else if (id.Contains("__21") || id.Contains("fi_face")) applied[i] = face;
                else if (id.Contains("__22") || id.Contains("sw_parts")) applied[i] = accessory;
                else if (id.Contains("__19") || id.Contains("fi_body")) applied[i] = body;
                else if (mainMesh) applied[i] = i == 1 ? head : i == 2 ? face : body;
                else applied[i] = n.Contains("shoesb") || n.Contains("goggle") || n.Contains("snorkel") ? accessory : body;
            }
            Undo.RecordObject(r, "Assign ORAS Vela textures");
            r.sharedMaterials = applied;
            EditorUtility.SetDirty(r);
        }

        private static void FitToHeightAndFloor(GameObject model, Transform proxy, float targetHeight)
        {
            if (!ComputeBounds(model, out Bounds bounds))
            {
                Debug.LogWarning("MonsterTactics U3.3: Model has no visible renderer bounds. Adjust scale and position in Inspector.");
                return;
            }
            float factor = Mathf.Clamp(targetHeight / Mathf.Max(0.0001f, bounds.size.y), 0.0001f, 500f);
            model.transform.localScale = Vector3.one * factor;
            if (!ComputeBounds(model, out bounds)) return;
            Vector3 fix = new Vector3(proxy.position.x - bounds.center.x,
                                      proxy.position.y - bounds.min.y,
                                      proxy.position.z - bounds.center.z);
            model.transform.position += fix;
        }

        private static bool ComputeBounds(GameObject root, out Bounds combined)
        {
            combined = new Bounds();
            bool first = true;
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                Bounds b = r.bounds;
                if (b.size.sqrMagnitude < 0.0000001f) continue;
                if (first) { combined = b; first = false; }
                else combined.Encapsulate(b);
            }
            return !first;
        }
    }
}
