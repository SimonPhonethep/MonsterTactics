using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MonsterTactics.Editor
{
    /// <summary>
    /// Optional art-only upgrade for the existing U2.4 TrainerSkinController.
    /// Supports one PNG per direction, or 3 walking frames per direction.
    /// Never creates trainers, edits movement/camera scripts, or regenerates the board.
    /// </summary>
    public static class MonsterTacticsTrainerArtOverridesMenu
    {
        private const string Root = "Assets/MonsterTactics/Art/TrainerOverrides";
        private static readonly string[] Folders = { "Starter", "VelaClassic", "Ranger" };
        private static readonly string[] Directions = { "front", "back", "left", "right" };
        private static readonly string[,] Properties =
        {
            { "starterFront", "starterBack", "starterLeft", "starterRight" },
            { "mayFront", "mayBack", "mayLeft", "mayRight" },
            { "adamanFront", "adamanBack", "adamanLeft", "adamanRight" }
        };

        [MenuItem("MonsterTactics/Trainers/Create Art Override Folders (U3.2)", priority = 100)]
        private static void CreateFolders()
        {
            EnsureFolder("Assets/MonsterTactics/Art");
            EnsureFolder(Root);
            foreach (string folder in Folders) EnsureFolder(Root + "/" + folder);
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("MonsterTactics - Trainer art", "Folders created at " + Root +
                ". Put your cropped transparent PNGs inside the corresponding trainer folder, then run Apply Trainer Art Overrides (U3.2).", "OK");
            Debug.Log("MonsterTactics U3.2: Created trainer art override folders.");
        }

        [MenuItem("MonsterTactics/Trainers/Apply Trainer Art Overrides (U3.2)", priority = 101)]
        private static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("MonsterTactics", "Stop Play mode before changing trainer sprites.", "OK");
                return;
            }

            GameObject root = GameObject.Find("MonsterTactics Trainer");
            TrainerSkinController controller = root != null ? root.GetComponent<TrainerSkinController>() : null;
            if (controller == null)
            {
                EditorUtility.DisplayDialog("MonsterTactics", "TrainerSkinController not found on 'MonsterTactics Trainer'. Install U2.4 first; don't regenerate the arena.", "OK");
                return;
            }
            SerializedObject serialized = new SerializedObject(controller);
            serialized.Update();
            int changed = 0;
            int partial = 0;
            for (int skin = 0; skin < Folders.Length; skin++)
            {
                string folder = Root + "/" + Folders[skin];
                for (int dir = 0; dir < Directions.Length; dir++)
                {
                    SerializedProperty array = serialized.FindProperty(Properties[skin, dir]);
                    if (array == null || !array.isArray)
                    {
                        Debug.LogWarning("MonsterTactics U3.2: U2.4 controller is missing property '" + Properties[skin, dir] + "'.");
                        continue;
                    }
                    Sprite[] replacements = ReadFrames(folder, Directions[dir]);
                    if (replacements == null) continue;
                    bool hasIncompleteFrames = false;
                    Sprite firstValid = null;
                    for (int f = 0; f < replacements.Length; f++)
                        if (replacements[f] != null) { firstValid = replacements[f]; break; }
                    if (firstValid == null) continue;

                    Undo.RecordObject(controller, "Change MonsterTactics trainer art");
                    if (array.arraySize < 3) array.arraySize = 3;
                    for (int f = 0; f < 3; f++)
                    {
                        Sprite target = replacements[f];
                        if (target == null)
                        {
                            hasIncompleteFrames = true;
                            // Missing frames retain existing animation if possible.
                            target = array.GetArrayElementAtIndex(f).objectReferenceValue as Sprite;
                            if (target == null) target = firstValid;
                        }
                        array.GetArrayElementAtIndex(f).objectReferenceValue = target;
                    }
                    if (hasIncompleteFrames) partial++;
                    changed++;
                }
            }
            if (changed == 0)
            {
                EditorUtility.DisplayDialog("MonsterTactics", "No PNG overrides found. Run 'Create Art Override Folders', then add front.png (or front_0.png, front_1.png, front_2.png) and other directions.", "OK");
                return;
            }
            serialized.ApplyModifiedProperties();
            controller.SelectSkin(controller.CurrentSkin);
            EditorUtility.SetDirty(controller);
            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.IsValid()) EditorSceneManager.MarkSceneDirty(activeScene);
            Debug.Log("MonsterTactics U3.2: Updated " + changed + " direction sets. " +
                "Current sprite/skin switching, trainer movement, camera, and hex selection preserved.");
            EditorUtility.DisplayDialog("MonsterTactics - Art updated",
                "Updated " + changed + " directional sprite sets." +
                (partial > 0 ? "\n" + partial + " sets have missing frames; existing frames were retained where possible." : "") +
                "\nSave the scene (Ctrl+S), press Play, and test keys 1, 2, and 4.", "OK");
        }

        private static Sprite[] ReadFrames(string folder, string direction)
        {
            string singlePath = folder + "/" + direction + ".png";
            Sprite single = ImportSprite(singlePath);
            if (single != null) return new[] { single, single, single };
            Sprite[] frames = new Sprite[3];
            bool any = false;
            for (int f = 0; f < frames.Length; f++)
            {
                frames[f] = ImportSprite(folder + "/" + direction + "_" + f + ".png");
                if (frames[f] != null) any = true;
            }
            return any ? frames : null;
        }

        private static Sprite ImportSprite(string path)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return null;
            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            bool update = importer.textureType != TextureImporterType.Sprite ||
                importer.spriteImportMode != SpriteImportMode.Single ||
                Mathf.Abs(importer.spritePixelsPerUnit - 32f) > 0.001f ||
                importer.filterMode != FilterMode.Point || importer.mipmapEnabled ||
                !importer.alphaIsTransparency ||
                importer.textureCompression != TextureImporterCompression.Uncompressed ||
                settings.spriteAlignment != (int)SpriteAlignment.BottomCenter;
            if (update)
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

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
