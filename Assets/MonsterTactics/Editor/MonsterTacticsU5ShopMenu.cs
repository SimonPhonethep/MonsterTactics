using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace MonsterTactics.Editor
{
    /// <summary>Installs U5 onto the existing U4.3 scene. Never rewrites the U3 team or tiles.</summary>
    public static class MonsterTacticsU5ShopMenu
    {
        private const string RootName = "MonsterTactics U5 Shop + Bench";
        private const string ArtRoot = "Assets/MonsterTactics/Art/Evolutions/";

        [MenuItem("MonsterTactics/Shop + Bench/U5/1 - Install Shop, Bench & Evolution", priority = 210)]
        public static void Install()
        {
            if (!CanEdit()) return;
            GameObject team = GameObject.Find("MonsterTactics U3 Monster Team");
            GameObject arena = GameObject.Find("MonsterTactics Arena");
            MonsterTacticsCombatDirector combat = Object.FindAnyObjectByType<MonsterTacticsCombatDirector>();
            if (team == null || arena == null || combat == null ||
                team.GetComponent<MonsterPlacementController>() == null)
            {
                EditorUtility.DisplayDialog("MonsterTactics U5",
                    "Open your working U4.3 scene with a Monster team, hex arena and combat director. " +
                    "Do NOT regenerate your arena or run the original U3 installer.", "OK");
                return;
            }
            Camera camera = Camera.main;
            if (camera == null) camera = Object.FindAnyObjectByType<Camera>();
            if (camera == null)
            {
                EditorUtility.DisplayDialog("MonsterTactics U5", "Missing Main Camera", "OK");
                return;
            }
            GameObject root = GameObject.Find(RootName);
            if (root == null)
            {
                root = new GameObject(RootName);
                Undo.RegisterCreatedObjectUndo(root, "Install MonsterTactics U5");
            }
            MonsterTacticsU5ShopManager manager = root.GetComponent<MonsterTacticsU5ShopManager>();
            if (manager == null) manager = Undo.AddComponent<MonsterTacticsU5ShopManager>(root);
            manager.Configure(team.GetComponent<MonsterPlacementController>(), combat,
                arena.transform, camera, LoadEvolutionArt());
            EditorUtility.SetDirty(manager);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Selection.activeGameObject = root;
            Debug.Log("MonsterTactics U5: Installed shop and bench. The ten existing Monster " +
                "are migrated into four deployed and six benched at runtime. Save the scene.");
            EditorUtility.DisplayDialog("MonsterTactics U5",
                "U5 installed. Save the scene, then Play. Four of your current ten Monster " +
                "will start deployed; six will be on your bench.\n\n" +
                "Optional: download all 20 evolution forms using menu step 2, then run step 3 to link them.", "OK");
        }

        [MenuItem("MonsterTactics/Shop + Bench/U5/2 - Download Matching Evolution Sprites (Windows)", priority = 211)]
        public static void DownloadSprites()
        {
            if (!CanEdit()) return;
            if (Application.platform != RuntimePlatform.WindowsEditor)
            {
                EditorUtility.DisplayDialog("MonsterTactics U5", "This optional downloader uses Windows PowerShell. " +
                    "On other platforms place frame_000.png in Art/Evolutions/<form> manually, then run menu 3.", "OK");
                return;
            }
            string script = Path.Combine(Application.dataPath, "MonsterTactics", "Tools",
                "DownloadEvolutionSprites.ps1");
            if (!File.Exists(script))
            {
                EditorUtility.DisplayDialog("MonsterTactics U5", "Missing download script at " + script, "OK");
                return;
            }
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            try
            {
                EditorUtility.DisplayProgressBar("MonsterTactics U5", "Downloading animated evolution sprites...", .3f);
                var startInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-NoProfile -ExecutionPolicy Bypass -File " + Quote(script) +
                        " -ProjectRoot " + Quote(project),
                    WorkingDirectory = project,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using (Process process = Process.Start(startInfo))
                {
                    if (process == null) throw new InvalidOperationException("Could not start PowerShell.");
                    string standardOut = process.StandardOutput.ReadToEnd();
                    string standardError = process.StandardError.ReadToEnd();
                    if (!process.WaitForExit(480000)) { process.Kill(); throw new TimeoutException("Download timed out."); }
                    if (!string.IsNullOrEmpty(standardOut)) Debug.Log("U5 evolution sprites:\n" + standardOut);
                    if (process.ExitCode != 0) Debug.LogWarning("U5 sprite downloader: " + standardError);
                }
            }
            catch (Exception ex) { Debug.LogWarning("U5 evolution sprites: " + ex.Message); }
            finally { EditorUtility.ClearProgressBar(); }
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            LinkEvolutionSprites();
        }

        [MenuItem("MonsterTactics/Shop + Bench/U5/3 - Link Local Evolution Sprites", priority = 212)]
        public static void LinkEvolutionSprites()
        {
            if (!CanEdit()) return;
            GameObject root = GameObject.Find(RootName);
            MonsterTacticsU5ShopManager manager = root != null ? root.GetComponent<MonsterTacticsU5ShopManager>() : null;
            if (manager == null)
            {
                EditorUtility.DisplayDialog("MonsterTactics U5", "Install Shop & Bench first (menu option 1).", "OK");
                return;
            }
            GameObject team = GameObject.Find("MonsterTactics U3 Monster Team");
            GameObject arena = GameObject.Find("MonsterTactics Arena");
            MonsterTacticsCombatDirector combat = Object.FindAnyObjectByType<MonsterTacticsCombatDirector>();
            Camera camera = Camera.main;
            if (camera == null) camera = Object.FindAnyObjectByType<Camera>();
            manager.Configure(team.GetComponent<MonsterPlacementController>(), combat,
                arena.transform, camera, LoadEvolutionArt());
            EditorUtility.SetDirty(manager);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("U5 linked evolution sprites. Save your scene before Play.");
        }

        [MenuItem("MonsterTactics/Shop + Bench/U5/4 - Verify Roster & Evolution Rules", priority = 213)]
        public static void Verify()
        {
            if (MonsterTacticsU5Roster.All.Length != 10)
                throw new InvalidOperationException("Expected 10 families.");
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (MonsterTacticsU5Roster.Family family in MonsterTacticsU5Roster.All)
            {
                if (family.Forms.Length != 3 || family.Bestiary.Length != 3 || family.Types.Length != 3)
                    throw new InvalidOperationException("Incomplete evolution line.");
                if (family.Cost < 1 || family.Cost > 4) throw new InvalidOperationException("Invalid cost.");
                for (int i = 0; i < 3; i++)
                {
                    if (!seen.Add(family.Forms[i])) throw new InvalidOperationException("Duplicate " + family.Forms[i]);
                    if (MonsterTacticsU5Roster.Find(family.Forms[i]) != family)
                        throw new InvalidOperationException("Lookup mismatch: " + family.Forms[i]);
                }
            }
            if (seen.Count != 30 || MonsterTacticsU5Roster.TierWeights(4).Sum() != 100)
                throw new InvalidOperationException("Roster or shop odds wrong.");
            Debug.Log("MonsterTactics U5 PASS: 10 families, 30 forms, stage lookup, costs and shop odds.");
            EditorUtility.DisplayDialog("MonsterTactics U5", "Roster rules passed. Next test buying 3 matching copies in Play mode.", "OK");
        }

        [MenuItem("MonsterTactics/Shop + Bench/U5/Play Test - Give 2 Matching Copies", priority = 214)]
        public static void TestTwoCopies()
        {
            if (!EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("MonsterTactics U5", "Press Play and select a Monster first.", "OK");
                return;
            }
            MonsterTacticsU5ShopManager manager = Object.FindAnyObjectByType<MonsterTacticsU5ShopManager>();
            if (manager != null) manager.GrantTwoTestCopies();
        }

        private static MonsterTacticsU5ShopManager.FormArt[] LoadEvolutionArt()
        {
            var art = new List<MonsterTacticsU5ShopManager.FormArt>();
            foreach (MonsterTacticsU5Roster.Family family in MonsterTacticsU5Roster.All)
                for (int stage = 2; stage <= 3; stage++)
                {
                    string form = family.Form(stage);
                    string folder = ArtRoot + form.ToLowerInvariant();
                    string disk = Path.Combine(Application.dataPath, "MonsterTactics", "Art",
                        "Evolutions", form.ToLowerInvariant());
                    if (!Directory.Exists(disk)) continue;
                    string[] pngs = Directory.GetFiles(disk, "frame_*.png")
                        .OrderBy(v => v, StringComparer.Ordinal).ToArray();
                    if (pngs.Length == 0) continue;
                    var frames = new List<Sprite>();
                    foreach (string png in pngs)
                    {
                        string path = folder + "/" + Path.GetFileName(png);
                        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                        if (importer == null) continue;
                        if (importer.textureType != TextureImporterType.Sprite ||
                            importer.spriteImportMode != SpriteImportMode.Single ||
                            !Mathf.Approximately(importer.spritePixelsPerUnit, 96f) ||
                            importer.filterMode != FilterMode.Point || importer.mipmapEnabled ||
                            importer.textureCompression != TextureImporterCompression.Uncompressed)
                        {
                            importer.textureType = TextureImporterType.Sprite;
                            importer.spriteImportMode = SpriteImportMode.Single;
                            importer.spritePixelsPerUnit = 96f;
                            importer.filterMode = FilterMode.Point;
                            importer.mipmapEnabled = false;
                            importer.alphaIsTransparency = true;
                            importer.wrapMode = TextureWrapMode.Clamp;
                            importer.textureCompression = TextureImporterCompression.Uncompressed;
                            importer.SaveAndReimport();
                        }
                        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                        if (sprite != null) frames.Add(sprite);
                    }
                    if (frames.Count == 0) continue;
                    float[] seconds = Enumerable.Repeat(.11f, frames.Count).ToArray();
                    string timing = Path.Combine(disk, "timings.txt");
                    if (File.Exists(timing))
                    {
                        string[] lines = File.ReadAllLines(timing);
                        for (int i = 0; i < lines.Length && i < seconds.Length; i++)
                            if (float.TryParse(lines[i], NumberStyles.Float, CultureInfo.InvariantCulture,
                                out float parsed)) seconds[i] = Mathf.Clamp(parsed, .055f, 1f);
                    }
                    art.Add(new MonsterTacticsU5ShopManager.FormArt
                    { Form = form, Frames = frames.ToArray(), Seconds = seconds });
                }
            Debug.Log("MonsterTactics U5: " + art.Count + "/20 evolution art sets linked.");
            return art.ToArray();
        }
        private static string Quote(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";
        private static bool CanEdit()
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode) return true;
            EditorUtility.DisplayDialog("MonsterTactics U5", "Stop Play mode first.", "OK");
            return false;
        }
    }
}
