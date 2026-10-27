#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MonsterTactics.Editor
{
    /// <summary>
    /// Read alpha from the project PNG files in the Editor and serialize visible
    /// bounds keyed to the Sprite references. No Read/Write changes are needed.
    /// Does a one-time build automatically after installation, and exposes a
    /// manual menu command for newly added Monster art.
    /// </summary>
    [InitializeOnLoad]
    public static class MonsterTacticsU686SpriteBoundsBuilder
    {
        private const string DatabasePath = "Assets/MonsterTactics/Resources/MonsterTacticsSpriteVisualBounds.asset";
        private static readonly string[] ArtFolders =
        {
            "Assets/MonsterTactics/Art/MonsterAnimated",
            "Assets/MonsterTactics/Art/Evolutions",
            "Assets/MonsterTactics/Art/MonsterU610",
            "Assets/MonsterTactics/Art/MonsterU610Animated",
            "Assets/MonsterTactics/Art/MonsterU610Unified",
            "Assets/MonsterTactics/Art/MonsterU6105Encounters",
            "Assets/MonsterTactics/Art/MonsterU6105Animated",
            "Assets/MonsterTactics/Art/Monster"
        };

        static MonsterTacticsU686SpriteBoundsBuilder()
        {
            EditorApplication.delayCall += AutoBuildIfMissing;
        }

        private static void AutoBuildIfMissing()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += AutoBuildIfMissing;
                return;
            }
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (AssetDatabase.LoadAssetAtPath<MonsterTacticsSpriteVisualBoundsDatabase>(DatabasePath) == null)
                Build();
        }

        [MenuItem("MonsterTactics/Art/U6.8.6 Rebuild Visible Sprite Bounds")]
        internal static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[U6.8.6] Exit Play Mode before rebuilding sprite bounds.");
                return;
            }
            try
            {
                var list = new List<MonsterTacticsSpriteVisualBoundsDatabase.SpriteBounds>();
                string[] guids = AssetDatabase.FindAssets("", ArtFolders);
                Array.Sort(guids, StringComparer.Ordinal);
                int skipped = 0;
                for (int i = 0; i < guids.Length; ++i)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    if (!path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) continue;
                    Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                    if (sprite == null) { skipped++; continue; }
                    try
                    {
                        byte[] png = File.ReadAllBytes(Path.GetFullPath(path));
                        Texture2D readable = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                        try
                        {
                            if (!ImageConversion.LoadImage(readable, png, false)) { skipped++; continue; }
                            Color32[] pixels = readable.GetPixels32();
                            int minX = readable.width, minY = readable.height, maxX = -1, maxY = -1;
                            for (int y = 0; y < readable.height; ++y)
                                for (int x = 0; x < readable.width; ++x)
                                {
                                    if (pixels[y * readable.width + x].a <= 16) continue;
                                    minX = Mathf.Min(minX, x);
                                    minY = Mathf.Min(minY, y);
                                    maxX = Mathf.Max(maxX, x);
                                    maxY = Mathf.Max(maxY, y);
                                }
                            if (maxX < minX || maxY < minY) { skipped++; continue; }
                            list.Add(new MonsterTacticsSpriteVisualBoundsDatabase.SpriteBounds
                            {
                                Sprite = sprite,
                                VisibleRect = new Rect(minX / (float)readable.width,
                                    minY / (float)readable.height,
                                    (maxX - minX + 1f) / readable.width,
                                    (maxY - minY + 1f) / readable.height)
                            });
                        }
                        finally { UnityEngine.Object.DestroyImmediate(readable); }
                    }
                    catch (Exception e)
                    {
                        skipped++;
                        Debug.LogWarning("[U6.8.6] Could not process " + path + ": " + e.Message);
                    }
                }

                if (list.Count == 0)
                {
                    Debug.LogError("[U6.8.6] No PNG sprites found. Check Assets/MonsterTactics/Art before running.");
                    return;
                }
                var db = AssetDatabase.LoadAssetAtPath<MonsterTacticsSpriteVisualBoundsDatabase>(DatabasePath);
                if (db == null)
                {
                    // U6.8.7a: An older U6.8.6 build could create a database with
                    // an invalid MonoScript reference (the type was declared in a
                    // different-named file). Only this *generated* cache is replaced;
                    // no sprites, scenes, items or gameplay assets are touched.
                    if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(DatabasePath)))
                    {
                        if (!AssetDatabase.DeleteAsset(DatabasePath))
                        {
                            Debug.LogError("[U6.8.7a] Unable to replace the old bounds cache at " +
                                DatabasePath + ". Delete that generated .asset manually and rebuild.");
                            return;
                        }
                        Debug.Log("[U6.8.7a] Replaced outdated sprite-bounds cache with a correctly linked script.");
                    }
                    db = ScriptableObject.CreateInstance<MonsterTacticsSpriteVisualBoundsDatabase>();
                    AssetDatabase.CreateAsset(db, DatabasePath);
                }
                db.Entries = list;
                EditorUtility.SetDirty(db);
                AssetDatabase.SaveAssets();
                MonsterTacticsSpriteScreenBounds.InvalidateCache();
                Debug.Log("[U6.8.6] Saved " + list.Count +
                    " visible sprite rectangles (skipped " + skipped + ").");
            }
            catch (Exception error)
            {
                Debug.LogError("[U6.8.6] Bounds import failed: " + error);
            }
        }
    }
}
#endif
