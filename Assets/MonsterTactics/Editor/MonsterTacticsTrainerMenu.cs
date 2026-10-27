using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MonsterTactics.Editor
{
    /// <summary>Adds a 2D trainer to the existing U1 scene. Does NOT regenerate tiles.</summary>
    public static class MonsterTacticsTrainerMenu
    {
        private const string TrainerName = "MonsterTactics Trainer";
        private const string ImageFolder = "Assets/MonsterTactics/Art/Trainer";
        private static readonly string[] Directions = { "front", "back", "left", "right" };

        [MenuItem("MonsterTactics/Add Movable Trainer (U2)", priority = 30)]
        private static void AddTrainer()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("MonsterTactics", "Stop Play mode before adding the trainer.", "OK");
                return;
            }

            GameObject arena = GameObject.Find("MonsterTactics Arena");
            Transform ground = arena != null ? arena.transform.Find("Ground") : null;
            if (ground == null)
            {
                EditorUtility.DisplayDialog("MonsterTactics",
                    "Arena not found. First open the scene with the U1 board, or select MonsterTactics → Create Starter Arena.", "OK");
                return;
            }

            Camera camera = Camera.main;
            if (camera == null) camera = Object.FindAnyObjectByType<Camera>();
            if (camera == null)
            {
                EditorUtility.DisplayDialog("MonsterTactics", "A camera is required. Generate the U1 arena first.", "OK");
                return;
            }

            Sprite[][] spriteSets = new Sprite[Directions.Length][];
            for (int d = 0; d < Directions.Length; d++)
            {
                spriteSets[d] = new Sprite[3];
                for (int frame = 0; frame < 3; frame++)
                {
                    string path = ImageFolder + "/trainer_" + Directions[d] + "_" + frame + ".png";
                    Sprite sprite = ImportPixelSprite(path);
                    if (sprite == null)
                    {
                        EditorUtility.DisplayDialog("Missing MonsterTactics art",
                            "Could not load " + path + ". Copy the entire Art/Trainer folder from the U2 ZIP.", "OK");
                        return;
                    }
                    spriteSets[d][frame] = sprite;
                }
            }

            GameObject trainer = GameObject.Find(TrainerName);
            bool newlyCreated = trainer == null;
            if (newlyCreated)
            {
                trainer = new GameObject(TrainerName);
                Undo.RegisterCreatedObjectUndo(trainer, "Add MonsterTactics trainer");
            }

            // Stand along the near edge of the board, outside the hex tiles.
            if (newlyCreated)
            {
                HexTile frontTile = FindPlayerFrontTile(arena.transform);
                float x = frontTile != null ? frontTile.transform.position.x : 0f;
                float z = frontTile != null ? frontTile.transform.position.z - 1.15f : -5f;
                float y = ground.position.y + 0.032f;
                trainer.transform.position = new Vector3(x, y, z);
            }

            SpriteRenderer spriteRenderer = FindOrCreateSprite(trainer.transform);
            spriteRenderer.sprite = spriteSets[0][0];
            spriteRenderer.sortingOrder = 25;
            spriteRenderer.transform.rotation = camera.transform.rotation;

            // Opaque, flattened oval shadow: no special URP shader or package needed.
            GameObject shadow = FindOrCreateShadow(trainer.transform);
            GameObject marker = FindOrCreateMarker();

            TrainerMovement movement = trainer.GetComponent<TrainerMovement>();
            if (movement == null) movement = trainer.AddComponent<TrainerMovement>();
            movement.Configure(camera, ground, spriteRenderer, marker,
                spriteSets[0], spriteSets[1], spriteSets[2], spriteSets[3]);

            marker.SetActive(false);
            Selection.activeGameObject = trainer;
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();

            Debug.Log("MonsterTactics U2: Trainer added. Save the scene; press Play and RIGHT-click exposed ground to walk. " +
                      "LEFT-click still selects yellow tiles.");
        }

        private static Sprite ImportPixelSprite(string path)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return null;

            // Unity 6.6: spriteAlignment belongs to TextureImporterSettings,
            // not TextureImporter. Set it to BottomCenter so feet touch the ground.
            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            bool needsImport = importer.textureType != TextureImporterType.Sprite ||
                importer.spriteImportMode != SpriteImportMode.Single ||
                importer.spritePixelsPerUnit != 32f ||
                importer.filterMode != FilterMode.Point ||
                importer.mipmapEnabled ||
                !importer.alphaIsTransparency ||
                importer.textureCompression != TextureImporterCompression.Uncompressed ||
                settings.spriteAlignment != (int)SpriteAlignment.BottomCenter;

            if (needsImport)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 32f;
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;

                // Read again after selecting Sprite mode to retain its defaults.
                importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
                settings.spritePivot = new Vector2(0.5f, 0f);
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static HexTile FindPlayerFrontTile(Transform arena)
        {
            HexTile[] tiles = arena.GetComponentsInChildren<HexTile>();
            HexTile result = null;
            foreach (HexTile tile in tiles)
                if (tile.IsPlayerSide && tile.Row == 0 && tile.Column == 3)
                    result = tile;
            return result;
        }

        private static SpriteRenderer FindOrCreateSprite(Transform root)
        {
            Transform existing = root.Find("Pixel Trainer");
            GameObject go;
            if (existing == null)
            {
                go = new GameObject("Pixel Trainer");
                go.transform.SetParent(root, false);
            }
            else go = existing.gameObject;

            // Sprite has a bottom-center pivot, so the trainer's feet align with root Y.
            go.transform.localPosition = new Vector3(0f, 0.035f, 0f);
            go.transform.localScale = Vector3.one * 1.15f;
            SpriteRenderer renderer = go.GetComponent<SpriteRenderer>();
            if (renderer == null) renderer = go.AddComponent<SpriteRenderer>();
            return renderer;
        }

        private static GameObject FindOrCreateShadow(Transform root)
        {
            Transform existing = root.Find("Trainer Shadow");
            if (existing != null) return existing.gameObject;

            GameObject shadow = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            shadow.name = "Trainer Shadow";
            shadow.transform.SetParent(root, false);
            shadow.transform.localPosition = new Vector3(0f, 0.022f, 0f);
            shadow.transform.localScale = new Vector3(0.44f, 0.025f, 0.28f);
            MeshRenderer renderer = shadow.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = GetOrCreateAccentMaterial("Trainer Shadow", new Color(0.08f, 0.17f, 0.19f));
            Object.DestroyImmediate(shadow.GetComponent<Collider>());
            return shadow;
        }

        private static GameObject FindOrCreateMarker()
        {
            GameObject[] roots = SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (GameObject existing in roots)
                if (existing.name == "MonsterTactics Trainer Destination")
                    return existing;

            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = "MonsterTactics Trainer Destination";
            Undo.RegisterCreatedObjectUndo(marker, "Create trainer destination marker");
            // Separate scene root: must remain stationary while the trainer walks.
            marker.transform.localScale = new Vector3(0.18f, 0.01f, 0.18f);
            MeshRenderer renderer = marker.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = GetOrCreateAccentMaterial("Trainer Destination", new Color(0.97f, 0.78f, 0.3f));
            Object.DestroyImmediate(marker.GetComponent<Collider>());
            marker.SetActive(false);
            return marker;
        }

        private static Material GetOrCreateAccentMaterial(string name, Color color)
        {
            const string folder = "Assets/MonsterTactics/Generated";
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder("Assets/MonsterTactics", "Generated");
            string path = folder + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) throw new System.InvalidOperationException("URP shader not found.");
            material = new Material(shader) { name = name, color = color };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
