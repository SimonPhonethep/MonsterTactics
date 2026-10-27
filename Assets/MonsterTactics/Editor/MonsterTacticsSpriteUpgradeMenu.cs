using System;
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
    /// Download optional Monster fan-game sprite animations on the user's own PC,
    /// then apply them to already-placed U3 units without reinstalling the team.
    /// Windows Unity Editor only; sources are credited in the README.
    /// </summary>
    public static class MonsterTacticsSpriteUpgradeMenu
    {
        private const string TeamRootName = "MonsterTactics U3 Monster Team";
        private const string AssetFolder = "Assets/MonsterTactics/Art/MonsterAnimated";
        private const string ScriptRelativePath = "MonsterTactics/Tools/DownloadAndExtractMonsterSprites.ps1";

        [MenuItem("MonsterTactics/Monster/Download + Apply Animated Sprites (U3.1)", false, 82)]
        private static void DownloadAndApply()
        {
            if (!CanModifyScene()) return;
            if (Application.platform != RuntimePlatform.WindowsEditor)
            {
                EditorUtility.DisplayDialog("MonsterTactics U3.1", "Automatic GIF conversion uses Windows PowerShell. On other platforms, supply PNG frames manually and run Apply Downloaded Frames.", "OK");
                return;
            }
            string script = Path.Combine(Application.dataPath, ScriptRelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(script))
            {
                EditorUtility.DisplayDialog("MonsterTactics U3.1", "Missing script at:\n" + script + "\nCopy the ZIP's Tools folder into Assets/MonsterTactics/.", "OK");
                return;
            }
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            try
            {
                EditorUtility.DisplayProgressBar("MonsterTactics U3.1", "Downloading and converting Monster sprite GIFs...", 0.25f);
                var startInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-NoProfile -ExecutionPolicy Bypass -File " + Quote(script) + " -ProjectRoot " + Quote(projectRoot),
                    WorkingDirectory = projectRoot,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using (Process process = Process.Start(startInfo))
                {
                    if (process == null) throw new InvalidOperationException("Could not start Windows PowerShell.");
                    if (!process.WaitForExit(150000))
                    {
                        process.Kill();
                        throw new TimeoutException("Sprite download exceeded 150 seconds. Check your connection and retry.");
                    }
                    string standardOutput = process.StandardOutput.ReadToEnd();
                    string errors = process.StandardError.ReadToEnd();
                    if (!string.IsNullOrEmpty(standardOutput)) Debug.Log("MonsterTactics U3.1:\n" + standardOutput);
                    if (process.ExitCode != 0)
                        Debug.LogWarning("MonsterTactics U3.1 sprite download: " + errors + "\nSome species may have failed; attempting available frames.");
                }
            }
            catch (Exception error)
            {
                Debug.LogWarning("MonsterTactics U3.1 download failed: " + error.Message + "\nYou can supply PNG sprite frames manually and use Apply Downloaded Frames.");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            ApplyFrames();
        }

        [MenuItem("MonsterTactics/Monster/Apply Downloaded Frames (U3.1)", false, 83)]
        private static void ApplyFrames()
        {
            if (!CanModifyScene()) return;
            GameObject team = GameObject.Find(TeamRootName);
            if (team == null)
            {
                EditorUtility.DisplayDialog("MonsterTactics U3.1", "U3 Monster team not found. Open the saved arena scene and install the U3 team first.", "OK");
                return;
            }

            MonsterUnit[] units = team.GetComponentsInChildren<MonsterUnit>(true);
            int upgraded = 0;
            int totalSprites = 0;

            foreach (MonsterUnit unit in units)
            {
                if (unit == null) continue;
                string speciesFolder = unit.MonsterName.ToLowerInvariant();
                string folder = AssetFolder + "/" + speciesFolder;
                string absoluteFolder = Path.Combine(Application.dataPath, "MonsterTactics", "Art", "MonsterAnimated", speciesFolder);
                if (!Directory.Exists(absoluteFolder)) continue;
                string[] files = Directory.GetFiles(absoluteFolder, "frame_*.png").OrderBy(f => f, StringComparer.Ordinal).ToArray();
                if (files.Length == 0) continue;

                Sprite[] frames = files.Select(file => PrepareSprite(folder + "/" + Path.GetFileName(file)))
                    .Where(sprite => sprite != null).ToArray();
                if (frames.Length == 0) continue;

                SpriteRenderer renderer = unit.GetComponentInChildren<SpriteRenderer>(true);
                if (renderer == null)
                {
                    Debug.LogWarning("MonsterTactics U3.1: No SpriteRenderer for " + unit.MonsterName);
                    continue;
                }

                float[] timings = ReadTimings(Path.Combine(absoluteFolder, "timings.txt"), frames.Length);
                MonsterFrameAnimator animator = renderer.GetComponent<MonsterFrameAnimator>();
                if (animator == null) animator = Undo.AddComponent<MonsterFrameAnimator>(renderer.gameObject);
                Undo.RecordObject(renderer, "Apply better Monster sprite");
                Undo.RecordObject(renderer.transform, "Set Monster sprite size");
                Undo.RecordObject(animator, "Apply Monster idle animation");
                animator.SetAnimation(renderer, frames, timings);
                renderer.transform.localScale = Vector3.one * 1.6f;

                EditorUtility.SetDirty(renderer);
                EditorUtility.SetDirty(animator);
                upgraded++;
                totalSprites += frames.Length;
            }

            if (upgraded > 0)
            {
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                AssetDatabase.SaveAssets();
                Debug.Log("MonsterTactics U3.1: Upgraded " + upgraded + "/5 Monster with " + totalSprites + " frames. Saved tile positions, names, HP bars, trainer skins and camera.");
                EditorUtility.DisplayDialog("MonsterTactics U3.1", "Updated " + upgraded + " of 5 Monster (" + totalSprites + " animation frames).\nSave the scene (Ctrl+S) and press Play.", "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("MonsterTactics U3.1", "No frame_*.png images were found.\n\nRun the Download + Apply command while connected to the internet, or add your own PNG frames under:\n" + AssetFolder + "/<monster>/", "OK");
            }
        }

        private static bool CanModifyScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("MonsterTactics U3.1", "Stop Play mode before changing Monster art.", "OK");
                return false;
            }
            return true;
        }

        private static Sprite PrepareSprite(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return null;

            bool needsUpdate = importer.textureType != TextureImporterType.Sprite ||
                               importer.spriteImportMode != SpriteImportMode.Single ||
                               !Mathf.Approximately(importer.spritePixelsPerUnit, 96f) ||
                               importer.filterMode != FilterMode.Point ||
                               importer.mipmapEnabled ||
                               importer.textureCompression != TextureImporterCompression.Uncompressed;
            if (needsUpdate)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 96f;
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        }

        private static float[] ReadTimings(string path, int count)
        {
            var timings = Enumerable.Repeat(0.11f, count).ToArray();
            if (!File.Exists(path)) return timings;
            string[] lines = File.ReadAllLines(path);
            for (int i = 0; i < count && i < lines.Length; i++)
                if (float.TryParse(lines[i], NumberStyles.Float, CultureInfo.InvariantCulture, out float duration))
                    timings[i] = Mathf.Clamp(duration, 0.055f, 1f);
            return timings;
        }

        private static string Quote(string path) => "\"" + path.Replace("\"", "\\\"") + "\"";
    }
}
