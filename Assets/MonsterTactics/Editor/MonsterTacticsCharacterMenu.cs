using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MonsterTactics.Editor
{
    /// <summary>
    /// Connects the original U2 trainer plus two new fan-art sprite sets.
    /// Does not create duplicate trainers, edit hex tiles, or regenerate the board.
    /// </summary>
    public static class MonsterTacticsCharacterMenu
    {
        private const string TrainerName = "MonsterTactics Trainer";
        private const string ArtRoot = "Assets/MonsterTactics/Art/Trainer";
        private static readonly string[] Directions = { "front", "back", "left", "right" };
        private static readonly string[] Names = { "Original", "Vela", "Ranger" };
        private static readonly string[] Prefixes = { "trainer", "may", "adaman" };
        private static readonly string[] Folders = { "", "/Vela", "/Ranger" };

        [MenuItem("MonsterTactics/Install Trainer Choices (U2.1)", priority = 31)]
        public static void InstallChoices()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("MonsterTactics", "Stop Play mode before installing character choices.", "OK");
                return;
            }

            GameObject trainer = GameObject.Find(TrainerName);
            if (trainer == null || trainer.GetComponent<TrainerMovement>() == null)
            {
                EditorUtility.DisplayDialog("MonsterTactics — trainer missing",
                    "First open the arena scene and choose MonsterTactics → Add Movable Trainer (U2). " +
                    "Then return here to add Vela and Ranger.", "OK");
                return;
            }

            TrainerCharacterSwitcher.TrainerAppearance[] entries =
                new TrainerCharacterSwitcher.TrainerAppearance[Names.Length];

            // Complete loading BEFORE updating the scene; no half-configured picker.
            for (int n = 0; n < Names.Length; n++)
            {
                var appearance = new TrainerCharacterSwitcher.TrainerAppearance
                {
                    displayName = Names[n],
                    front = LoadFrames(n, "front"),
                    back = LoadFrames(n, "back"),
                    left = LoadFrames(n, "left"),
                    right = LoadFrames(n, "right")
                };

                if (appearance.front == null || appearance.back == null ||
                    appearance.left == null || appearance.right == null)
                    return;
                entries[n] = appearance;
            }

            TrainerCharacterSwitcher chooser = trainer.GetComponent<TrainerCharacterSwitcher>();
            int existingIndex = chooser != null ? chooser.SelectedIndex : 0;
            if (chooser == null)
                chooser = Undo.AddComponent<TrainerCharacterSwitcher>(trainer);

            Undo.RecordObject(chooser, "Install MonsterTactics trainer choices");
            chooser.Configure(trainer.GetComponent<TrainerMovement>(), entries, existingIndex);
            EditorUtility.SetDirty(chooser);
            EditorUtility.SetDirty(trainer.GetComponent<TrainerMovement>());
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Selection.activeGameObject = trainer;

            Debug.Log("MonsterTactics U2.1: Choose Original, Vela, or Ranger in Game view. " +
                      "Press 1 / 2 / 3 to change appearance. F1 hides the picker. " +
                      "RIGHT-click to walk, LEFT-click to select hexes.");
        }

        private static Sprite[] LoadFrames(int character, string direction)
        {
            Sprite[] frames = new Sprite[3];
            for (int i = 0; i < frames.Length; i++)
            {
                string path = ArtRoot + Folders[character] + "/" +
                              Prefixes[character] + "_" + direction + "_" + i + ".png";
                frames[i] = ImportPixelSprite(path);
                if (frames[i] == null)
                {
                    EditorUtility.DisplayDialog("MonsterTactics — missing sprite",
                        "Could not find " + path + ". Extract the full U2.1 ZIP's Assets folder.", "OK");
                    return null;
                }
            }
            return frames;
        }

        private static Sprite ImportPixelSprite(string path)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return null;

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

                importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
                settings.spritePivot = new Vector2(0.5f, 0f);
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
