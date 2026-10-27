using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MonsterTactics.Editor
{
    /// <summary>
    /// Installs the U3 Monster on the EXISTING U1-U2 arena.
    /// Only creates/replaces the standalone U3 team root. It never
    /// rebuilds the board, changes the camera, or touches trainer skins.
    /// </summary>
    public static class MonsterTacticsMonsterMenu
    {
        private const string RootName = "MonsterTactics U3 Monster Team";
        private const string ArtFolder = "Assets/MonsterTactics/Art/Monster/";

        [MenuItem("MonsterTactics/Monster/Install U3 Team (5 Monster)", priority = 70)]
        public static void InstallU3Team()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("MonsterTactics U3", "Stop Play mode before installing Monster.", "OK");
                return;
            }

            GameObject arena = GameObject.Find("MonsterTactics Arena");
            if (arena == null)
            {
                EditorUtility.DisplayDialog("MonsterTactics U3", "Cannot find 'MonsterTactics Arena'. Open your saved U1/U2 arena scene first. Do not regenerate it.", "OK");
                return;
            }

            HexTile[] tiles = arena.GetComponentsInChildren<HexTile>(true);
            var positions = new Dictionary<Vector2Int, HexTile>();
            foreach (HexTile hex in tiles) positions[new Vector2Int(hex.Column, hex.Row)] = hex;
            if (positions.Count != 64)
            {
                EditorUtility.DisplayDialog("MonsterTactics U3", "Expected 64 U1 hex tiles, but found " + positions.Count + ". Check which scene is open.", "OK");
                return;
            }

            Camera camera = Camera.main;
            if (camera == null) camera = Object.FindAnyObjectByType<Camera>();
            if (camera == null)
            {
                EditorUtility.DisplayDialog("MonsterTactics U3", "No camera found in your scene.", "OK");
                return;
            }

            GameObject previous = GameObject.Find(RootName);
            if (previous != null)
            {
                if (!EditorUtility.DisplayDialog(
                        "Replace existing U3 team?",
                        "This will replace only the five U3 Monster and reset their tile positions. Your arena, trainer skins, and movement stay untouched.",
                        "Replace U3 Team", "Cancel"))
                    return;
            }

            Species[] roster =
            {
                new Species("Ripplet", "Water", "Fighter", 650, "ripplet.png", 1, 1),
                new Species("Emberlet", "Fire", "Mage", 550, "emberlet.png", 3, 1),
                new Species("Budhorn", "Grass / Poison", "Tank", 750, "budhorn.png", 5, 1),
                new Species("Flarbit", "Fire", "Ranged", 430, "flarbit.png", 2, 2),
                new Species("Mistrip", "Water", "Assassin", 500, "mistrip.png", 4, 2)
            };

            // Validate and import all sprites BEFORE modifying the scene.
            var sprites = new List<Sprite>();
            foreach (Species entry in roster)
            {
                string path = ArtFolder + entry.FileName;
                Sprite sprite = PrepareSprite(path);
                if (sprite == null)
                {
                    EditorUtility.DisplayDialog("MonsterTactics U3", "Missing or invalid sprite:\n" + path + "\n\nCopy the ZIP's Art/Monster folder into Assets/MonsterTactics/Art/Monster first.", "OK");
                    return;
                }
                if (!positions.TryGetValue(new Vector2Int(entry.Column, entry.Row), out HexTile tile) || !tile.IsPlayerSide)
                {
                    EditorUtility.DisplayDialog("MonsterTactics U3", "Friendly tile missing at (" + entry.Column + ", " + entry.Row + ").", "OK");
                    return;
                }
                sprites.Add(sprite);
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Install MonsterTactics U3 Monster");
            if (previous != null) Undo.DestroyObjectImmediate(previous);

            GameObject teamRoot = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(teamRoot, "Install MonsterTactics U3 Team");
            var units = new List<MonsterUnit>();

            for (int i = 0; i < roster.Length; i++)
            {
                Species entry = roster[i];
                GameObject monster = new GameObject(entry.Name);
                monster.transform.SetParent(teamRoot.transform, false);

                GameObject visual = new GameObject("Pixel Sprite (camera-facing)");
                visual.transform.SetParent(monster.transform, false);
                visual.transform.localPosition = new Vector3(0f, 0.68f, 0f);
                visual.transform.localScale = Vector3.one * 1.40f;

                SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
                renderer.sprite = sprites[i];
                renderer.sortingOrder = 30;
                renderer.color = Color.white;

                BoxCollider hitbox = monster.AddComponent<BoxCollider>();
                hitbox.center = new Vector3(0f, 0.68f, 0f);
                hitbox.size = new Vector3(0.79f, 1.19f, 0.79f);
                hitbox.isTrigger = false;

                MonsterUnit unit = monster.AddComponent<MonsterUnit>();
                HexTile tile = positions[new Vector2Int(entry.Column, entry.Row)];
                unit.Configure(entry.Name, entry.Type, entry.Role, entry.HP, tile, renderer, camera);
                units.Add(unit);
            }

            MonsterPlacementController controller = teamRoot.AddComponent<MonsterPlacementController>();
            controller.Configure(camera, units.ToArray());

            Selection.activeGameObject = teamRoot;
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log("MonsterTactics U3: Installed Ripplet, Emberlet, Budhorn, Flarbit, Mistrip. " +
                      "Play → left-click Monster to inspect; drag them between green tiles. U2 trainers unchanged.");
        }

        private static Sprite PrepareSprite(string path)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return null;

            bool changed = importer.textureType != TextureImporterType.Sprite ||
                           importer.spriteImportMode != SpriteImportMode.Single ||
                           !Mathf.Approximately(importer.spritePixelsPerUnit, 96f) ||
                           importer.filterMode != FilterMode.Point || importer.mipmapEnabled ||
                           importer.textureCompression != TextureImporterCompression.Uncompressed;

            if (changed)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 96f;
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private struct Species
        {
            public readonly string Name;
            public readonly string Type;
            public readonly string Role;
            public readonly int HP;
            public readonly string FileName;
            public readonly int Column;
            public readonly int Row;

            public Species(string name, string type, string role, int hp, string fileName, int column, int row)
            {
                Name = name;
                Type = type;
                Role = role;
                HP = hp;
                FileName = fileName;
                Column = column;
                Row = row;
            }
        }
    }
}
