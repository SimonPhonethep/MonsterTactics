using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MonsterTactics.Editor
{
    /// <summary>U2.3 adds cosmetic skins without regenerating the board or replacing movement.</summary>
    public static class MonsterTacticsTrainerVisualsMenu
    {
        private const string ArtRoot = "Assets/MonsterTactics/Art/Trainer";
        private static readonly string[] Directions = { "front", "back", "left", "right" };
        private const string GeneratedFolder = "Assets/MonsterTactics/Generated";

        [MenuItem("MonsterTactics/Trainers/Restore Ranger (U2.4)", priority = 80)]
        private static void InstallSkins()
        {
            if (!ReadyToEdit()) return;
            GameObject trainer = GameObject.Find("MonsterTactics Trainer");
            if (trainer == null)
            {
                EditorUtility.DisplayDialog("MonsterTactics", "No existing trainer found. Install U2 first.", "OK");
                return;
            }
            TrainerMovement movement = trainer.GetComponent<TrainerMovement>();
            Transform pixel = trainer.transform.Find("Pixel Trainer");
            SpriteRenderer spriteRenderer = pixel != null ? pixel.GetComponent<SpriteRenderer>() : null;
            if (movement == null || spriteRenderer == null)
            {
                EditorUtility.DisplayDialog("MonsterTactics", "The U2 trainer needs a TrainerMovement and Pixel Trainer sprite child.", "OK");
                return;
            }

            // Validate all art FIRST; avoid leaving a half-installed 3D model.
            Sprite[][] starter = LoadFourDirections(ArtRoot, "trainer");
            Sprite[][] classicVela = LoadFourDirections(ArtRoot + "/Vela", "may");
            Sprite[][] adaman = LoadFourDirections(ArtRoot + "/Ranger", "adaman");
            if (starter == null || classicVela == null || adaman == null)
            {
                EditorUtility.DisplayDialog("MonsterTactics — sprites missing",
                    "This update uses your existing U2 and U2.1 PNG files, including all 12 Ranger frames. Install the complete U2.1 trainer choices art first. " +
                    "Do NOT run an older U2.1 installer script over the Unity 6.6 fixes.", "OK");
                return;
            }

            // Create once; running the command again updates the existing skin controller.
            Transform model = trainer.transform.Find("Modern Vela 3D - Proxy");
            if (model == null) model = BuildChibiProxy(trainer.transform);

            TrainerCharacterSwitcher oldPicker = trainer.GetComponent<TrainerCharacterSwitcher>();
            if (oldPicker != null)
            {
                Undo.RecordObject(oldPicker, "Disable old three-sprite picker");
                oldPicker.enabled = false; // Prevent duplicate hotkeys/overlapping UI.
                EditorUtility.SetDirty(oldPicker);
            }

            TrainerSkinController hybrid = trainer.GetComponent<TrainerSkinController>();
            if (hybrid == null) hybrid = Undo.AddComponent<TrainerSkinController>(trainer);
            int skinToKeep = hybrid.CurrentSkin;
            Undo.RecordObject(hybrid, "Install hybrid trainer skins");
            Undo.RecordObject(spriteRenderer.transform, "Resize pixel trainer");
            spriteRenderer.transform.localScale = Vector3.one * 1.55f;
            spriteRenderer.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            hybrid.Configure(movement, spriteRenderer,
                starter[0], starter[1], starter[2], starter[3],
                classicVela[0], classicVela[1], classicVela[2], classicVela[3],
                adaman[0], adaman[1], adaman[2], adaman[3],
                model.gameObject, skinToKeep);

            EditorUtility.SetDirty(hybrid);
            EditorUtility.SetDirty(movement);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Selection.activeGameObject = trainer;
            Debug.Log("MonsterTactics U2.4: 1 Starter, 2 Classic Vela, 3 Modern Vela proxy, 4 Ranger. Right-click moves; left-click selects hexes. Save the scene.");
        }

        [MenuItem("MonsterTactics/Camera/Use Balanced Arena View (U2.3)", priority = 64)]
        private static void SetBalancedCamera()
        {
            if (!ReadyToEdit()) return;
            Camera camera = Camera.main;
            if (camera == null) camera = Object.FindAnyObjectByType<Camera>();
            if (camera == null)
            {
                EditorUtility.DisplayDialog("MonsterTactics", "No camera found in this scene.", "OK");
                return;
            }
            Undo.RecordObject(camera, "Set balanced arena camera");
            Undo.RecordObject(camera.transform, "Set balanced arena camera");
            camera.orthographic = true;
            camera.orthographicSize = 7.5f;
            camera.transform.rotation = Quaternion.Euler(43f, 0f, 0f);
            // Same world-space focus as U1/U2.2, so clicks still map correctly.
            camera.transform.position = -camera.transform.forward * 18f;
            EditorUtility.SetDirty(camera);
            EditorUtility.SetDirty(camera.transform);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("MonsterTactics: applied a centered 43° camera. Save the scene. Undo with Ctrl+Z or use the U2.2 view menu.");
        }

        private static bool ReadyToEdit()
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode) return true;
            EditorUtility.DisplayDialog("MonsterTactics", "Please stop Play mode before changing the scene.", "OK");
            return false;
        }

        private static Sprite[][] LoadFourDirections(string folder, string prefix)
        {
            Sprite[][] frames = new Sprite[4][];
            for (int direction = 0; direction < 4; direction++)
            {
                frames[direction] = new Sprite[3];
                for (int frame = 0; frame < 3; frame++)
                {
                    string path = folder + "/" + prefix + "_" + Directions[direction] + "_" + frame + ".png";
                    frames[direction][frame] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                    if (frames[direction][frame] == null)
                    {
                        Debug.LogWarning("MonsterTactics U2.4: Missing imported sprite " + path);
                        return null;
                    }
                }
            }
            return frames;
        }

        private static Transform BuildChibiProxy(Transform trainer)
        {
            GameObject root = new GameObject("Modern Vela 3D - Proxy");
            Undo.RegisterCreatedObjectUndo(root, "Create 3D trainer proxy");
            root.transform.SetParent(trainer, false);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;

            // Stylized low-poly proxy only: NOT an official ORAS model or final asset.
            Material skin = MaterialFor("Vela Proxy Skin", new Color(1f, 0.78f, 0.61f));
            Material hair = MaterialFor("Vela Proxy Hair", new Color(0.32f, 0.23f, 0.19f));
            Material bandana = MaterialFor("Vela Proxy Bandana", new Color(0.83f, 0.19f, 0.28f));
            Material shirt = MaterialFor("Vela Proxy Shirt", new Color(0.17f, 0.35f, 0.62f));
            Material skirt = MaterialFor("Vela Proxy Skirt", new Color(0.77f, 0.23f, 0.32f));
            Material dark = MaterialFor("Vela Proxy Dark", new Color(0.12f, 0.14f, 0.23f));
            Material white = MaterialFor("Vela Proxy White", new Color(0.95f, 0.94f, 0.89f));
            Material accent = MaterialFor("Vela Proxy Accent", new Color(0.95f, 0.78f, 0.38f));
            Material cheeks = MaterialFor("Vela Proxy Cheeks", new Color(0.97f, 0.53f, 0.57f));

            AddPart(root.transform, "Body", PrimitiveType.Capsule, shirt, new Vector3(0, 0.64f, 0), new Vector3(0.38f, 0.29f, 0.28f));
            AddPart(root.transform, "Skirt", PrimitiveType.Cube, skirt, new Vector3(0, 0.43f, 0), new Vector3(0.45f, 0.21f, 0.34f));
            AddPart(root.transform, "Leg L", PrimitiveType.Capsule, skin, new Vector3(-0.12f, 0.23f, 0), new Vector3(0.105f, 0.21f, 0.11f));
            AddPart(root.transform, "Leg R", PrimitiveType.Capsule, skin, new Vector3(0.12f, 0.23f, 0), new Vector3(0.105f, 0.21f, 0.11f));
            AddPart(root.transform, "Shoe L", PrimitiveType.Cube, bandana, new Vector3(-0.12f, 0.07f, -0.07f), new Vector3(0.17f, 0.14f, 0.27f));
            AddPart(root.transform, "Shoe R", PrimitiveType.Cube, bandana, new Vector3(0.12f, 0.07f, -0.07f), new Vector3(0.17f, 0.14f, 0.27f));
            AddPart(root.transform, "Arm L", PrimitiveType.Capsule, skin, new Vector3(-0.31f, 0.65f, 0), new Vector3(0.13f, 0.34f, 0.14f));
            AddPart(root.transform, "Arm R", PrimitiveType.Capsule, skin, new Vector3(0.31f, 0.65f, 0), new Vector3(0.13f, 0.34f, 0.14f));
            AddPart(root.transform, "Backpack", PrimitiveType.Cube, dark, new Vector3(0, 0.65f, 0.20f), new Vector3(0.34f, 0.42f, 0.17f));
            AddPart(root.transform, "Neck", PrimitiveType.Cylinder, skin, new Vector3(0, 0.91f, 0), new Vector3(0.10f, 0.07f, 0.10f));
            AddPart(root.transform, "Hair", PrimitiveType.Sphere, hair, new Vector3(0, 1.13f, 0.065f), new Vector3(0.62f, 0.69f, 0.57f));
            AddPart(root.transform, "Head", PrimitiveType.Sphere, skin, new Vector3(0, 1.11f, -0.06f), new Vector3(0.58f, 0.58f, 0.53f));
            AddPart(root.transform, "Left Hair", PrimitiveType.Sphere, hair, new Vector3(-0.27f, 0.96f, 0.015f), new Vector3(0.17f, 0.31f, 0.23f));
            AddPart(root.transform, "Right Hair", PrimitiveType.Sphere, hair, new Vector3(0.27f, 0.96f, 0.015f), new Vector3(0.17f, 0.31f, 0.23f));
            AddPart(root.transform, "Bandana", PrimitiveType.Sphere, bandana, new Vector3(0, 1.39f, -0.015f), new Vector3(0.66f, 0.22f, 0.62f));
            AddPart(root.transform, "Bandana Bow", PrimitiveType.Cube, bandana, new Vector3(0.29f, 1.41f, 0.20f), new Vector3(0.25f, 0.15f, 0.14f));
            AddPart(root.transform, "Headband Mark", PrimitiveType.Cube, white, new Vector3(0, 1.46f, -0.22f), new Vector3(0.14f, 0.07f, 0.045f));
            AddPart(root.transform, "Eye L", PrimitiveType.Sphere, dark, new Vector3(-0.12f, 1.13f, -0.31f), new Vector3(0.056f, 0.074f, 0.038f));
            AddPart(root.transform, "Eye R", PrimitiveType.Sphere, dark, new Vector3(0.12f, 1.13f, -0.31f), new Vector3(0.056f, 0.074f, 0.038f));
            AddPart(root.transform, "Blush L", PrimitiveType.Sphere, cheeks, new Vector3(-0.225f, 1.04f, -0.265f), new Vector3(0.066f, 0.040f, 0.024f));
            AddPart(root.transform, "Blush R", PrimitiveType.Sphere, cheeks, new Vector3(0.225f, 1.04f, -0.265f), new Vector3(0.066f, 0.040f, 0.024f));
            AddPart(root.transform, "Belt", PrimitiveType.Cube, white, new Vector3(0, 0.48f, -0.145f), new Vector3(0.42f, 0.09f, 0.08f));
            AddPart(root.transform, "Bag Emblem", PrimitiveType.Sphere, accent, new Vector3(0, 0.68f, 0.30f), new Vector3(0.14f, 0.14f, 0.06f));
            root.SetActive(false);
            return root.transform;
        }

        private static GameObject AddPart(Transform parent, string label, PrimitiveType primitive, Material material, Vector3 localPos, Vector3 size)
        {
            GameObject part = GameObject.CreatePrimitive(primitive);
            part.name = label;
            Undo.RegisterCreatedObjectUndo(part, "Create trainer proxy part");
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPos;
            part.transform.localScale = size;
            MeshRenderer renderer = part.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            Collider collider = part.GetComponent<Collider>();
            if (collider != null) Object.DestroyImmediate(collider);
            return part;
        }

        private static Material MaterialFor(string label, Color color)
        {
            if (!AssetDatabase.IsValidFolder(GeneratedFolder))
                AssetDatabase.CreateFolder("Assets/MonsterTactics", "Generated");
            string path = GeneratedFolder + "/" + label + ".mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) throw new System.InvalidOperationException("Neither URP/Lit nor Standard shader was found.");
            Material result = new Material(shader) { name = label };
            if (result.HasProperty("_BaseColor")) result.SetColor("_BaseColor", color);
            else result.color = color;
            AssetDatabase.CreateAsset(result, path);
            return result;
        }
    }
}
