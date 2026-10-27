using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace MonsterTactics.Editor
{
    /// <summary>
    /// U6.2.6 one-click sprite setup, matching the original U4.2 workflow.
    /// The user never needs to open PowerShell or type their project path.
    /// </summary>
    public static class MonsterTacticsLegendarySpriteSetupMenu
    {
        private const string ScriptRelativePath = "MonsterTactics/Tools/DownloadLegendarySprites.ps1";
        private const string AssetsRelativePath = "MonsterTactics/Resources/LegendarySprites";
        private static readonly string[] Names = { "voltrix", "pyrothane", "aqualith" };

        [MenuItem("MonsterTactics/Monster/U6.2.6/Install Legendary Animated Sprites", priority = 216)]
        public static void Install()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("MonsterTactics", "Stop Play Mode before importing sprites.", "OK");
                return;
            }
            if (Application.platform != RuntimePlatform.WindowsEditor)
            {
                EditorUtility.DisplayDialog("MonsterTactics", "This matching sprite importer uses Windows PowerShell. Open the project with Unity on Windows, then run this menu command.", "OK");
                return;
            }
            string script = Path.Combine(Application.dataPath, ScriptRelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(script))
            {
                EditorUtility.DisplayDialog("MonsterTactics", "The sprite downloader wasn't found. Re-extract the complete U6.2.6 ZIP into Assets/MonsterTactics.\n\n" + script, "OK");
                return;
            }
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            if (!EditorUtility.DisplayDialog("MonsterTactics - Legendary Sprites",
                "Download matching Black/White-style animated sprites for Voltrix, Pyrothane and Aqualith from MonsterAPI and import them into the project?\n\nThe files are saved inside Assets/MonsterTactics/Resources/LegendarySprites.",
                "Download + Import", "Cancel")) return;

            try
            {
                EditorUtility.DisplayProgressBar("MonsterTactics U6.2.6", "Downloading and converting legendary sprites...", .4f);
                var startInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-NoProfile -ExecutionPolicy Bypass -File " + Quote(script) +
                                " -ProjectRoot " + Quote(projectRoot),
                    WorkingDirectory = projectRoot,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                int exit;
                string output;
                string errors;
                using (Process process = Process.Start(startInfo))
                {
                    if (process == null) throw new IOException("Couldn't launch the included sprite importer.");
                    // The script only emits a handful of status lines; buffer remains small.
                    if (!process.WaitForExit(180000))
                    {
                        process.Kill();
                        throw new TimeoutException("Sprite download took too long.");
                    }
                    output = process.StandardOutput.ReadToEnd();
                    errors = process.StandardError.ReadToEnd();
                    exit = process.ExitCode;
                }
                if (!string.IsNullOrEmpty(output)) Debug.Log("MonsterTactics legendary sprites:\n" + output);
                if (exit != 0) Debug.LogWarning("Legendary sprite downloader reported an issue:\n" + errors);
            }
            catch (Exception ex)
            {
                Debug.LogError("MonsterTactics sprite importer: " + ex.Message);
                EditorUtility.DisplayDialog("MonsterTactics", "Couldn't download the animations: " + ex.Message + "\n\nYour game can still use the fallback sprites until this succeeds.", "OK");
                return;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            int ready = 0;
            foreach (string name in Names)
            {
                string path = "Assets/" + AssetsRelativePath + "/" + name + "/frame_000.png";
                if (AssetDatabase.LoadAssetAtPath<Sprite>(path) != null) ready++;
            }
            EditorUtility.DisplayDialog("MonsterTactics - Legendary Sprites",
                ready == Names.Length
                    ? "Voltrix, Pyrothane and Aqualith are ready! All three animations will automatically play in the shop, on the bench, and in combat.\n\nPress Play to test."
                    : "Imported " + ready + " of 3 legendary animations. Check the Console for the reason any sprite could not be downloaded.", "OK");
        }

        [MenuItem("MonsterTactics/Monster/U6.2.6/Verify Legendary Sprite Imports", priority = 217)]
        public static void Verify()
        {
            AssetDatabase.Refresh();
            string summary = string.Join("\n", Names.Select(name =>
            {
                string folder = "Assets/" + AssetsRelativePath + "/" + name;
                string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { folder });
                return name + ": " + guids.Length + " sprite asset(s)";
            }));
            EditorUtility.DisplayDialog("MonsterTactics - Legendary Sprites", summary, "OK");
        }

        private static string Quote(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";
    }
}
