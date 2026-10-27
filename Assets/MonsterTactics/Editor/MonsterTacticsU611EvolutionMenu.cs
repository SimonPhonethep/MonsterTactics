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
    /// <summary>
    /// U6.1.1: imports all evolved forms with the U3.1/U4.2 frame convention.
    /// Does not invoke the old U5 installer or rebuild the team/arena.
    /// </summary>
    public static class MonsterTacticsU611EvolutionMenu
    {
        private const string ArtFolder = "Assets/MonsterTactics/Art/Evolutions/";
        private const string ScriptPath = "MonsterTactics/Tools/DownloadEvolutionSprites.ps1";

        [MenuItem("MonsterTactics/Evolution/U6.1.1/1 - Download + Link 20 Evolution Sprites", priority = 340)]
        public static void DownloadAndLink()
        {
            if (!ReadyToEdit()) return;
            if (Application.platform != RuntimePlatform.WindowsEditor)
            {
                EditorUtility.DisplayDialog("MonsterTactics U6.1.1",
                    "This automatic downloader runs on Windows. On macOS/Linux, " +
                    "place frame_000.png files in Assets/MonsterTactics/Art/Evolutions/<form>/ " +
                    "and then run menu option 2.", "OK");
                return;
            }

            string script = Path.Combine(Application.dataPath, ScriptPath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(script))
            {
                EditorUtility.DisplayDialog("MonsterTactics U6.1.1", "Missing tool: " + script, "OK");
                return;
            }
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            bool completed = false;
            try
            {
                EditorUtility.DisplayProgressBar("MonsterTactics U6.1.1", "Downloading 20 evolved Monster sprite sets...", .2f);
                var processInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-NoProfile -ExecutionPolicy Bypass -File " + Quote(script) +
                        " -ProjectRoot " + Quote(projectRoot),
                    WorkingDirectory = projectRoot,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using (Process process = Process.Start(processInfo))
                {
                    if (process == null) throw new InvalidOperationException("Unable to start PowerShell.");
                    string output = process.StandardOutput.ReadToEnd();
                    string errors = process.StandardError.ReadToEnd();
                    if (!process.WaitForExit(480000))
                    {
                        process.Kill();
                        throw new TimeoutException("Sprite download exceeded 8 minutes.");
                    }
                    if (!string.IsNullOrEmpty(output)) Debug.Log("MonsterTactics evolution downloader:\n" + output);
                    if (!string.IsNullOrEmpty(errors)) Debug.LogWarning("MonsterTactics downloader output:\n" + errors);
                    completed = process.ExitCode == 0;
                    if (!completed)
                        Debug.LogWarning("Sprite downloader exited with code " + process.ExitCode +
                            ". Existing files will still be linked if present.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("MonsterTactics evolution download failed: " + ex.Message);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            LinkExisting();
            if (!completed)
                EditorUtility.DisplayDialog("MonsterTactics U6.1.1",
                    "Some downloads may be missing. Existing sprites were linked where available. " +
                    "See Console for failed forms, then retry menu option 1. " +
                    "Missing sprites fall back to the previous form.", "OK");
        }

        [MenuItem("MonsterTactics/Evolution/U6.1.1/2 - Link Local Evolution Sprites", priority = 341)]
        public static void LinkExisting()
        {
            if (!ReadyToEdit()) return;
            MonsterTacticsU5ShopManager manager = Object.FindAnyObjectByType<MonsterTacticsU5ShopManager>();
            if (manager == null)
            {
                EditorUtility.DisplayDialog("MonsterTactics U6.1.1",
                    "Can't find the current MonsterTactics U5 Shop Manager in the open scene. " +
                    "Open your working U6.1 scene; do not rerun U5 installers.", "OK");
                return;
            }
            var art = LoadAll();
            Undo.RecordObject(manager, "Link MonsterTactics evolved Monster sprites");
            manager.SetEvolutionArt(art);
            EditorUtility.SetDirty(manager);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("MonsterTactics U6.1.1: linked " + art.Length + "/20 evolved sprites to current scene. Save with Ctrl+S.");
            EditorUtility.DisplayDialog("MonsterTactics U6.1.1",
                "Linked " + art.Length + " of 20 evolved forms.\n" +
                (art.Length < 20 ? "See Console for missing forms. They will use the previous sprite.\n" : "All evolved forms are ready.\n") +
                "Save the scene (Ctrl+S), then press Play.", "OK");
        }

        [MenuItem("MonsterTactics/Evolution/U6.1.1/3 - Verify Evolution Sprite Coverage", priority = 342)]
        public static void Verify()
        {
            int available = 0;
            var missing = new List<string>();
            foreach (MonsterTacticsU5Roster.Family family in MonsterTacticsU5Roster.All)
                for (int stage = 2; stage <= 3; stage++)
                {
                    string name = family.Form(stage);
                    string[] frames = FindPngs(name);
                    if (frames.Length > 0) available++;
                    else missing.Add(name);
                }
            Debug.Log("MonsterTactics U6.1.1 sprite audit: " + available + "/20 ready. Missing: " +
                (missing.Count == 0 ? "none" : string.Join(", ", missing.ToArray())));
            EditorUtility.DisplayDialog("MonsterTactics U6.1.1",
                available + "/20 sprite sets available.\n" +
                (missing.Count == 0 ? "Mistveil and Nightstream included!" :
                 "Missing: " + string.Join(", ", missing.ToArray())), "OK");
        }

        [MenuItem("MonsterTactics/Evolution/U6.1.1/Play Test - Evolve Selected Monster", priority = 343)]
        public static void TestEvolve()
        {
            if (!EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("MonsterTactics U6.1.1",
                    "Enter Play mode and select a 1★ or 2★ Monster first.", "OK");
                return;
            }
            MonsterTacticsU5ShopManager manager = Object.FindAnyObjectByType<MonsterTacticsU5ShopManager>();
            if (manager != null) manager.GrantTwoTestCopies();
        }

        private static MonsterTacticsU5ShopManager.FormArt[] LoadAll()
        {
            var assets = new List<MonsterTacticsU5ShopManager.FormArt>();
            var missing = new List<string>();
            foreach (MonsterTacticsU5Roster.Family family in MonsterTacticsU5Roster.All)
                for (int stage = 2; stage <= 3; stage++)
                {
                    string form = family.Form(stage);
                    string[] files = FindPngs(form);
                    var sprites = new List<Sprite>();
                    foreach (string diskPath in files)
                    {
                        string assetPath = ArtFolder + form.ToLowerInvariant() + "/" + Path.GetFileName(diskPath);
                        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                        if (importer == null) continue;
                        if (NeedsSpriteImport(importer))
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
                        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                        if (sprite != null) sprites.Add(sprite);
                    }
                    if (sprites.Count == 0)
                    {
                        missing.Add(form);
                        continue;
                    }
                    float[] frameSeconds = Enumerable.Repeat(.11f, sprites.Count).ToArray();
                    string durations = Path.Combine(Application.dataPath, "MonsterTactics", "Art", "Evolutions",
                        form.ToLowerInvariant(), "timings.txt");
                    if (File.Exists(durations))
                    {
                        string[] lines = File.ReadAllLines(durations);
                        for (int i = 0; i < Math.Min(lines.Length, frameSeconds.Length); i++)
                            if (float.TryParse(lines[i], NumberStyles.Float, CultureInfo.InvariantCulture,
                                out float seconds)) frameSeconds[i] = Mathf.Clamp(seconds, .055f, 1f);
                    }
                    assets.Add(new MonsterTacticsU5ShopManager.FormArt
                    {
                        Form = form,
                        Frames = sprites.ToArray(),
                        Seconds = frameSeconds
                    });
                }
            if (missing.Count > 0)
                Debug.LogWarning("MonsterTactics U6.1.1 missing evolved sprites: " + string.Join(", ", missing.ToArray()));
            return assets.ToArray();
        }

        private static bool NeedsSpriteImport(TextureImporter importer) =>
            importer.textureType != TextureImporterType.Sprite ||
            importer.spriteImportMode != SpriteImportMode.Single ||
            !Mathf.Approximately(importer.spritePixelsPerUnit, 96f) ||
            importer.filterMode != FilterMode.Point ||
            importer.mipmapEnabled ||
            importer.textureCompression != TextureImporterCompression.Uncompressed;

        private static string[] FindPngs(string form)
        {
            string folder = Path.Combine(Application.dataPath, "MonsterTactics", "Art", "Evolutions",
                form.ToLowerInvariant());
            return Directory.Exists(folder)
                ? Directory.GetFiles(folder, "frame_*.png").OrderBy(v => v, StringComparer.Ordinal).ToArray()
                : new string[0];
        }

        private static bool ReadyToEdit()
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode) return true;
            EditorUtility.DisplayDialog("MonsterTactics U6.1.1", "Stop Play mode before importing art.", "OK");
            return false;
        }

        private static string Quote(string path) => "\"" + path.Replace("\"", "\\\"") + "\"";
    }
}
