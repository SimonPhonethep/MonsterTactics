#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using Debug = UnityEngine.Debug;

namespace MonsterTactics.Editor
{
    /// <summary>
    /// U6.10.5: opt-in, reversible art migration of stage-one wild and seven solo bosses.
    /// Uses the SAME MonsterAPI sources / 96-PPU point-filtered frame pipeline as U6.10.3/4.
    /// Leaves legacy WildSprites/ and all game/boss configuration assets untouched.
    /// </summary>
    public static class MonsterTacticsU6105EncounterArtMenu
    {
        private const string StaticDir = "Assets/MonsterTactics/Art/MonsterU6105Encounters";
        private const string AnimatedDir = "Assets/MonsterTactics/Art/MonsterU6105Animated";
        private const string CatalogPath = "Assets/MonsterTactics/Resources/MonsterTacticsU6105EncounterArtCatalog.asset";
        private const string SharedDownloader = "Assets/MonsterTactics/Tools/DownloadU610AnimatedSprites.ps1";
        private static readonly (string species, int dex)[] Species =
        {
            ("Bristlet", 13), ("Winglit", 16), ("Skitter", 19),
            ("Crawlet", 10), ("Ashhound", 228), ("Nightfang", 229),
            ("Skyclaw", 142), ("Huggevodrel", 760), ("MonolithKing", 486),
            ("Gloomborne", 491), ("Starshifter", 386), ("Dreadwing", 717), ("VortexPrime", 150)
        };
        private static readonly Queue<(string species, int dex)> pending =
            new Queue<(string species, int dex)>();
        private static UnityWebRequest request;
        private static (string species, int dex) current;
        private static bool fetching;
        private static int failed;
        private static Process animatedDownloader;

        [MenuItem("MonsterTactics/U6.10.5/1. Download Matching PvE and Boss Art")]
        public static void DownloadStatic()
        {
            if (!CanEdit()) return;
            if (fetching) { Debug.LogWarning("[U6.10.5] Sprite import already running."); return; }
            Directory.CreateDirectory(StaticDir);
            AssetDatabase.Refresh();
            pending.Clear();
            foreach (var entry in Species) pending.Enqueue(entry);
            failed = 0;
            fetching = true;
            EditorApplication.update -= TickStatic;
            EditorApplication.update += TickStatic;
            Debug.Log("[U6.10.5] Importing 13 encounter species using U6.10's MonsterAPI artwork source. " +
                "Old PvE sprites remain available until all images are imported.");
        }

        private static void TickStatic()
        {
            if (!fetching) { EditorApplication.update -= TickStatic; return; }
            if (request != null)
            {
                if (!request.isDone) return;
                if (request.result != UnityWebRequest.Result.Success ||
                    request.downloadHandler == null || request.downloadHandler.data == null ||
                    request.downloadHandler.data.Length < 100)
                {
                    failed++;
                    Debug.LogWarning("[U6.10.5] Download failed: " + current.species + ": " + request.error);
                }
                else
                {
                    string path = PathFor(current.species);
                    try
                    {
                        byte[] normalized = NormalizeToEncounterCanvas(request.downloadHandler.data);
                        File.WriteAllBytes(path, normalized);
                        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                        if (ConfigureSprite(path) == null) failed++;
                    }
                    catch (Exception e) { failed++; Debug.LogError("[U6.10.5] " + current.species + ": " + e.Message); }
                }
                request.Dispose(); request = null;
            }
            while (pending.Count > 0)
            {
                current = pending.Dequeue();
                string path = PathFor(current.species);
                if (File.Exists(path))
                {
                    if (ConfigureSprite(path) == null) failed++;
                    continue;
                }
                request = UnityWebRequest.Get(
                    "https://raw.githubusercontent.com/MonsterAPI/sprites/master/sprites/monster/" + current.dex + ".png");
                request.timeout = 22;
                request.SendWebRequest();
                return;
            }
            fetching = false;
            EditorApplication.update -= TickStatic;
            if (failed > 0)
            {
                Debug.LogError("[U6.10.5] " + failed + "/13 art imports failed. " +
                    "No new encounter catalog was activated; retry after checking your connection.");
                return;
            }
            RebuildCatalog();
            Debug.Log("[U6.10.5] Base art imported for all 13 encounter species. " +
                "Optional next step: menu 2 for genuine GIF animations; static art has subtle idle motion.");
        }

        // Keep encounter sprites on a consistent centered, feet-aligned 96x96 canvas.
        // Source artwork is identical to U6.10; this adapts to the existing PvE
        // world placement without modifying Monster coordinates or boss hitboxes.
        private static byte[] NormalizeToEncounterCanvas(byte[] png)
        {
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            var dest = new Texture2D(96, 96, TextureFormat.RGBA32, false);
            try
            {
                if (!ImageConversion.LoadImage(source, png, false))
                    throw new IOException("MonsterAPI returned invalid PNG bytes.");
                Color32[] input = source.GetPixels32();
                int minX = source.width, maxX = -1, minY = source.height, maxY = -1;
                for (int y = 0; y < source.height; y++)
                    for (int x = 0; x < source.width; x++)
                    {
                        if (input[y * source.width + x].a <= 16) continue;
                        minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
                        minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
                    }
                if (maxX < minX || maxY < minY) throw new IOException("Transparent or empty PNG.");
                int width = maxX - minX + 1, height = maxY - minY + 1;
                float ratio = Mathf.Min(1f, Mathf.Min(88f / width, 88f / height));
                int w = Mathf.Max(1, Mathf.RoundToInt(width * ratio));
                int h = Mathf.Max(1, Mathf.RoundToInt(height * ratio));
                int offsetX = (96 - w) / 2, offsetY = 3;
                var pixels = new Color32[96 * 96];
                for (int y = 0; y < h; y++)
                {
                    int sy = minY + Mathf.Min(height - 1, Mathf.FloorToInt(y / ratio));
                    for (int x = 0; x < w; x++)
                    {
                        int sx = minX + Mathf.Min(width - 1, Mathf.FloorToInt(x / ratio));
                        pixels[(offsetY + y) * 96 + offsetX + x] = input[sy * source.width + sx];
                    }
                }
                dest.SetPixels32(pixels);
                dest.Apply(false, false);
                return ImageConversion.EncodeToPNG(dest);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
                UnityEngine.Object.DestroyImmediate(dest);
            }
        }

        private static Sprite ConfigureSprite(string path)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                importer = AssetImporter.GetAtPath(path) as TextureImporter;
            }
            if (importer == null) return null;
            if (importer.textureType != TextureImporterType.Sprite ||
                importer.spriteImportMode != SpriteImportMode.Single ||
                !Mathf.Approximately(importer.spritePixelsPerUnit, 96f) ||
                importer.filterMode != FilterMode.Point || importer.mipmapEnabled ||
                importer.textureCompression != TextureImporterCompression.Uncompressed)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 96f;
                importer.spritePivot = new Vector2(.5f, .5f);
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        [MenuItem("MonsterTactics/U6.10.5/2. Download Genuine PvE and Boss Animations")]
        public static void DownloadAnimations()
        {
            if (!CanEdit()) return;
            if (Application.platform != RuntimePlatform.WindowsEditor)
            {
                Debug.LogWarning("[U6.10.5] Automatic GIF conversion requires Windows PowerShell. " +
                    "Import frame_###.png and timings.txt manually into " + AnimatedDir + "/<species> and use menu 3.");
                return;
            }
            if (animatedDownloader != null && !animatedDownloader.HasExited)
            { Debug.LogWarning("[U6.10.5] Animation download already in progress."); return; }
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string script = Path.Combine(root, SharedDownloader.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(script)) { Debug.LogError("[U6.10.5] Missing U6.10.3 shared downloader: " + script); return; }
            string manifest = Path.Combine(root, "Library", "MonsterTacticsU6105EncounterRoster.csv");
            Directory.CreateDirectory(Path.GetDirectoryName(manifest));
            using (var stream = new StreamWriter(manifest, false))
            {
                stream.WriteLine("name,id");
                foreach (var entry in Species)
                    stream.WriteLine(entry.species.ToLowerInvariant() + "," + entry.dex);
            }
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe", WorkingDirectory = root,
                    Arguments = "-NoProfile -ExecutionPolicy Bypass -File " + Quote(script) +
                        " -ProjectRoot " + Quote(root) + " -ManifestPath " + Quote(manifest) +
                        " -OutputFolder MonsterU6105Animated -CacheFolder U6105",
                    UseShellExecute = false, CreateNoWindow = true
                };
                animatedDownloader = Process.Start(psi);
                if (animatedDownloader == null) throw new IOException("Could not start PowerShell downloader.");
                EditorApplication.update -= TickAnimated;
                EditorApplication.update += TickAnimated;
                Debug.Log("[U6.10.5] Downloading available animated GIFs for wild Monster and bosses. " +
                    "The editor remains responsive; missing GIFs use modern static artwork.");
            }
            catch (Exception e) { Debug.LogError("[U6.10.5] " + e.Message); animatedDownloader = null; }
        }

        private static void TickAnimated()
        {
            if (animatedDownloader == null) { EditorApplication.update -= TickAnimated; return; }
            if (!animatedDownloader.HasExited) return;
            int exit = animatedDownloader.ExitCode;
            animatedDownloader.Dispose(); animatedDownloader = null;
            EditorApplication.update -= TickAnimated;
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            Debug.Log("[U6.10.5] GIF import finished with exit code " + exit + ". Rebuilding encounter art catalog.");
            RebuildCatalog();
        }

        [MenuItem("MonsterTactics/U6.10.5/3. Rebuild Encounter Art from Local Files")]
        public static void RebuildCatalog()
        {
            if (!CanEdit()) return;
            var entries = new List<MonsterTacticsU6105EncounterArtCatalog.Entry>();
            int animated = 0;
            foreach (var item in Species)
            {
                Sprite sprite = ConfigureSprite(PathFor(item.species));
                if (sprite == null)
                {
                    Debug.LogError("[U6.10.5] Missing base sprite " + item.species +
                        ". Install all 13 art files before activating the unified art catalog.");
                    return;
                }
                string folder = AnimatedDir + "/" + item.species.ToLowerInvariant();
                var sprites = new List<Sprite>();
                if (Directory.Exists(folder))
                    foreach (string file in Directory.GetFiles(folder, "frame_*.png").OrderBy(f => f, StringComparer.Ordinal))
                    {
                        Sprite frame = ConfigureSprite(file.Replace('\\', '/'));
                        if (frame != null) sprites.Add(frame);
                    }
                float[] times = sprites.Count >= 2 ? ReadTimings(folder + "/timings.txt", sprites.Count) : new float[0];
                if (sprites.Count >= 2) animated++;
                entries.Add(new MonsterTacticsU6105EncounterArtCatalog.Entry
                {
                    Species = item.species, StaticSprite = sprite,
                    AnimationFrames = sprites.Count >= 2 ? sprites.ToArray() : new Sprite[0],
                    FrameDurations = times
                });
            }
            var catalog = AssetDatabase.LoadAssetAtPath<MonsterTacticsU6105EncounterArtCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<MonsterTacticsU6105EncounterArtCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            Undo.RecordObject(catalog, "Rebuild U6.10.5 encounter art catalog");
            catalog.Entries = entries;
            catalog.UseUnifiedArt = true; // restore available under menu 5
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            MonsterTacticsU686SpriteBoundsBuilder.Build();
            Debug.Log("[U6.10.5] Imported all 13 encounter species, " + animated +
                " animated, " + (13 - animated) + " modern static fallbacks. " +
                "Existing WildSprites assets and boss-specific size multipliers remain intact.");
        }

        [MenuItem("MonsterTactics/U6.10.5/4. Verify Encounter Art")]
        public static void Verify()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<MonsterTacticsU6105EncounterArtCatalog>(CatalogPath);
            if (catalog == null || catalog.Entries == null)
            { Debug.LogWarning("[U6.10.5] Not installed. PvE and bosses are still using legacy art."); return; }
            int available = 0, animated = 0, invalid = 0;
            foreach (var species in Species)
            {
                var entry = catalog.Entries.FirstOrDefault(e => e != null &&
                    string.Equals(e.Species, species.species, StringComparison.OrdinalIgnoreCase));
                if (entry == null || entry.StaticSprite == null) { invalid++; continue; }
                available++;
                if (entry.AnimationFrames != null && entry.AnimationFrames.Length >= 2 &&
                    entry.FrameDurations != null && entry.FrameDurations.Length == entry.AnimationFrames.Length &&
                    entry.AnimationFrames.All(f => f != null)) animated++;
            }
            Debug.Log("[U6.10.5] " + (invalid == 0 && available == 13 ? "PASS" : "CHECK") +
                " modern encounter sprites=" + available + "/13; animated=" + animated +
                "; modern static fallback=" + (available - animated) +
                "; missing/invalid=" + invalid + "; active=" + catalog.UseUnifiedArt +
                "; stage-one wild/boss=6, solo bosses=7. " +
                "Visual multipliers wild=" + catalog.WildScaleMultiplier.ToString("0.00", CultureInfo.InvariantCulture) +
                ", Stage1 Nightfang=" + catalog.StageOneBossScaleMultiplier.ToString("0.00", CultureInfo.InvariantCulture) +
                ", solo boss=" + catalog.SoloBossScaleMultiplier.ToString("0.00", CultureInfo.InvariantCulture) + ".");
        }

        [MenuItem("MonsterTactics/U6.10.5/5. Restore Legacy PvE and Boss Art")]
        public static void Restore()
        {
            if (!CanEdit()) return;
            var catalog = AssetDatabase.LoadAssetAtPath<MonsterTacticsU6105EncounterArtCatalog>(CatalogPath);
            if (catalog == null) { Debug.Log("[U6.10.5] Legacy encounter art is already in use."); return; }
            Undo.RecordObject(catalog, "Restore original PvE artwork");
            catalog.UseUnifiedArt = false;
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log("[U6.10.5] Legacy PvE and boss art restored. All new imports remain untouched. " +
                "Restart Play Mode. Rebuild via menu 3 to enable unified art again.");
        }

        private static float[] ReadTimings(string path, int count)
        {
            float[] times = Enumerable.Repeat(.12f, count).ToArray();
            if (!File.Exists(path)) return times;
            string[] lines = File.ReadAllLines(path);
            for (int i = 0; i < count && i < lines.Length; i++)
                if (float.TryParse(lines[i], NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
                    times[i] = Mathf.Clamp(value, .055f, 1f);
            return times;
        }

        private static string PathFor(string species) => StaticDir + "/" + species.ToLowerInvariant() + ".png";
        private static string Quote(string path) => "\"" + path.Replace("\"", "\\\"") + "\"";
        private static bool CanEdit()
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode) return true;
            Debug.LogWarning("[U6.10.5] Exit Play Mode first.");
            return false;
        }
    }
}
#endif
