#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace MonsterTactics.Editor
{
    /// <summary>
    /// Optional Windows GIF download + per-form frame import. Preserves the existing
    /// U6.10 catalog and does not touch stats, scenes, shop pools or battle rules.
    /// Non-Windows users can populate the same frame folders and choose Rebuild.
    /// </summary>
    public static class MonsterTacticsU6103AnimatedSpritesMenu
    {
        private const string AnimatedFolder = "Assets/MonsterTactics/Art/MonsterU610Animated";
        private const string LibraryPath = "Assets/MonsterTactics/Resources/MonsterTacticsU610AnimatedArtCatalog.asset";
        private const string CatalogPath = "Assets/MonsterTactics/Resources/Content/MonsterTacticsContentCatalog.asset";
        private const string ScriptRelative = "Assets/MonsterTactics/Tools/DownloadU610AnimatedSprites.ps1";
        private static Process downloader;

        [MenuItem("MonsterTactics/U6.10.3/1. Download Genuine Animation Frames + Install")]
        private static void DownloadAndInstall()
        {
            if (!CanRun()) return;
            if (Application.platform != RuntimePlatform.WindowsEditor)
            {
                EditorUtility.DisplayDialog("MonsterTactics U6.10.3",
                    "Automatic GIF conversion requires Windows PowerShell. Supply extracted PNG frames in " +
                    AnimatedFolder + "/<species>/ and run option 2 instead.", "OK");
                return;
            }
            if (downloader != null && !downloader.HasExited)
            {
                Debug.LogWarning("[U6.10.3] Animation importer already running.");
                return;
            }
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string script = Path.Combine(root, ScriptRelative.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(script))
            {
                Debug.LogError("[U6.10.3] Missing " + ScriptRelative + ". Extract the patch into the project root.");
                return;
            }
            string manifest = Path.Combine(root, "Library", "MonsterTacticsU6103AnimationRoster.csv");
            Directory.CreateDirectory(Path.GetDirectoryName(manifest));
            using (var writer = new StreamWriter(manifest, false))
            {
                writer.WriteLine("name,id");
                foreach (var entry in MonsterTacticsU6104UnifiedArtMenu.EnumerateAllFormDex())
                    writer.WriteLine(entry.Key.ToLowerInvariant() + "," + entry.Value);
            }
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-NoProfile -ExecutionPolicy Bypass -File " + Quote(script) +
                        " -ProjectRoot " + Quote(root) + " -ManifestPath " + Quote(manifest),
                    WorkingDirectory = root,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                downloader = Process.Start(psi);
                if (downloader == null) throw new InvalidOperationException("Could not start PowerShell.");
                EditorApplication.update -= PollDownload;
                EditorApplication.update += PollDownload;
                Debug.Log("[U6.10.3] Started optional multi-frame GIF imports for the unified playable roster. " +
                    "Keep Unity open. Use menu option 2 if an Editor reload interrupts completion.");
            }
            catch (Exception ex)
            {
                Debug.LogError("[U6.10.3] GIF importer could not start: " + ex.Message);
                downloader = null;
            }
        }

        private static string Quote(string path) { return "\"" + path.Replace("\"", "\\\"") + "\""; }

        private static void PollDownload()
        {
            if (downloader == null) { EditorApplication.update -= PollDownload; return; }
            if (!downloader.HasExited) return;
            int code = downloader.ExitCode;
            downloader.Dispose();
            downloader = null;
            EditorApplication.update -= PollDownload;
            Debug.Log("[U6.10.3] Animation extraction finished (exit " + code + "). Importing assets...");
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            Rebuild();
        }

        [MenuItem("MonsterTactics/U6.10.3/2. Rebuild Animations from Local Frames")]
        public static void Rebuild()
        {
            if (!CanRun()) return;
            MonsterTacticsContentCatalog roster = AssetDatabase.LoadAssetAtPath<MonsterTacticsContentCatalog>(CatalogPath);
            if (roster == null)
            {
                Debug.LogError("[U6.10.3] U6.10 Content Catalog missing. Install U6.10 first.");
                return;
            }
            MonsterTacticsU610AnimatedArtCatalog library =
                AssetDatabase.LoadAssetAtPath<MonsterTacticsU610AnimatedArtCatalog>(LibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<MonsterTacticsU610AnimatedArtCatalog>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }
            Undo.RecordObject(library, "Build U6.10.3 animation library");
            var clips = new List<MonsterTacticsU610AnimatedArtCatalog.FormAnimation>();
            int species = 0;
            foreach (var item in MonsterTacticsU6104UnifiedArtMenu.EnumerateAllFormDex())
            {
                string folder = AnimatedFolder + "/" + item.Key.ToLowerInvariant();
                if (!Directory.Exists(folder)) continue;
                string[] files = Directory.GetFiles(folder, "frame_*.png")
                    .OrderBy(f => f, StringComparer.Ordinal).ToArray();
                if (files.Length < 2) continue;
                Sprite original = FindSourceSprite(roster, item.Key);
                if (original == null) continue;
                var frames = new List<Sprite>();
                foreach (string file in files)
                {
                    string path = folder + "/" + Path.GetFileName(file);
                    Sprite frame = ImportFrame(path);
                    if (frame != null) frames.Add(frame);
                }
                if (frames.Count < 2) continue;
                float[] durations = ReadTimings(folder + "/timings.txt", frames.Count);
                clips.Add(new MonsterTacticsU610AnimatedArtCatalog.FormAnimation
                {
                    Form = item.Key, StaticSource = original,
                    Frames = frames.ToArray(), Durations = durations
                });
                species++;
            }
            library.Forms = clips;
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            // Keep the U6.10.2 HP-bar visible-alpha bounds accurate for ALL frames.
            MonsterTacticsU686SpriteBoundsBuilder.Build();
            int totalForms = MonsterTacticsU6104UnifiedArtMenu.EnumerateAllFormDex().Count();
            Debug.Log("[U6.10.4] Imported " + species + "/" + totalForms + " genuine multi-frame forms. " +
                (totalForms - species) + " use fallback motion. Restart Play Mode to see animations.");
        }

        private static Sprite FindSourceSprite(MonsterTacticsContentCatalog catalog, string form)
        {
            if (catalog.monster == null) return null;
            foreach (MonsterTacticsMonsterRecord family in catalog.monster)
            {
                if (family == null || family.forms == null) continue;
                foreach (MonsterTacticsMonsterFormRecord stage in family.forms)
                    if (stage != null && string.Equals(stage.name, form, StringComparison.OrdinalIgnoreCase))
                        return stage.sprite;
            }
            return null;
        }

        private static Sprite ImportFrame(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
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
                importer.spritePixelsPerUnit = 96;
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static float[] ReadTimings(string path, int count)
        {
            var durations = Enumerable.Repeat(0.12f, count).ToArray();
            if (!File.Exists(path)) return durations;
            string[] lines = File.ReadAllLines(path);
            for (int i = 0; i < count && i < lines.Length; i++)
            {
                float value;
                if (float.TryParse(lines[i], NumberStyles.Float,
                    CultureInfo.InvariantCulture, out value))
                    durations[i] = Mathf.Clamp(value, 0.055f, 1f);
            }
            return durations;
        }

        [MenuItem("MonsterTactics/U6.10.3/3. Verify Animation Integration")]
        public static void Verify()
        {
            var lib = AssetDatabase.LoadAssetAtPath<MonsterTacticsU610AnimatedArtCatalog>(LibraryPath);
            var catalog = AssetDatabase.LoadAssetAtPath<MonsterTacticsContentCatalog>(CatalogPath);
            if (catalog == null) { Debug.LogError("[U6.10.3] FAIL: roster not installed."); return; }
            int animated = 0, bad = 0;
            var actual = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (lib != null && lib.Forms != null)
                foreach (var entry in lib.Forms)
                {
                    if (entry == null || entry.Frames == null || entry.Frames.Length < 2 ||
                        entry.StaticSource == null || entry.Durations == null ||
                        entry.Durations.Length != entry.Frames.Length ||
                        entry.Frames.Any(f => f == null)) { bad++; continue; }
                    animated++;
                    actual.Add(entry.Form);
                }
            int count = MonsterTacticsU6104UnifiedArtMenu.EnumerateAllFormDex().Count();
            Debug.Log("[U6.10.3] " + (bad > 0 ? "FAIL" : animated > 0 ? "PASS" : "FALLBACK ONLY") +
                " animation registry: " + animated + "/" + count +
                " multi-frame species, fallback " + (count - animated) +
                ", invalid entries " + bad + ". Playback uses MonsterFrameAnimator; " +
                "U6.10.3 fallback applies to all 18 families.");
        }

        private static bool CanRun()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            { Debug.LogWarning("[U6.10.3] Exit Play Mode before updating animation assets."); return false; }
            return true;
        }
    }
}
#endif
