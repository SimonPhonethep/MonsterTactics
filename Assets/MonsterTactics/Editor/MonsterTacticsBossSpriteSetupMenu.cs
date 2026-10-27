using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace MonsterTactics.Editor
{
    /// <summary>One-click, Windows Unity editor importer, same workflow as Stage 1.</summary>
    public static class MonsterTacticsBossSpriteSetupMenu
    {
        private const string ScriptPath = "MonsterTactics/Tools/DownloadBossSprites.ps1";
        private const string SpriteRoot = "Assets/MonsterTactics/Resources/WildSprites";
        private static readonly string[] Bosses =
        { "skyclaw", "huggevodrel", "monolithking", "gloomborne", "starshifter", "dreadwing", "vortexprime" };

        [MenuItem("MonsterTactics/Monster/U6.3.2/Install Boss Animated Sprites", priority = 220)]
        public static void Import()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("MonsterTactics", "Stop Play Mode to import boss sprites.", "OK");
                return;
            }
            if (Application.platform != RuntimePlatform.WindowsEditor)
            {
                EditorUtility.DisplayDialog("MonsterTactics", "The importer runs in Unity on Windows.", "OK");
                return;
            }
            string script = Path.Combine(Application.dataPath,
                ScriptPath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(script))
            {
                EditorUtility.DisplayDialog("MonsterTactics", "Boss downloader missing. Extract the complete patch into Assets/MonsterTactics.", "OK");
                return;
            }
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            if (!EditorUtility.DisplayDialog("MonsterTactics - Solo Boss Sprites",
                "Download and convert seven boss sprites into animated Unity frames?\n" +
                "Skyclaw, Huggevodrel, MonolithKing, Gloomborne, Starshifter, Dreadwing, VortexPrime\n\n" +
                "Uses MonsterAPI/Sprites and Showdown-style sprites where BW art isn't available.",
                "Download + Import", "Cancel")) return;
            try
            {
                EditorUtility.DisplayProgressBar("MonsterTactics U6.3.2", "Importing boss sprite animations...", .5f);
                var start = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-NoProfile -ExecutionPolicy Bypass -File " + Quote(script) +
                                " -ProjectRoot " + Quote(root),
                    WorkingDirectory = root,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using (Process proc = Process.Start(start))
                {
                    if (proc == null) throw new IOException("Couldn't start boss sprite installer.");
                    if (!proc.WaitForExit(240000))
                    {
                        proc.Kill();
                        throw new TimeoutException("Sprite importer timed out.");
                    }
                    string output = proc.StandardOutput.ReadToEnd();
                    string errors = proc.StandardError.ReadToEnd();
                    if (!string.IsNullOrEmpty(output)) Debug.Log("MonsterTactics boss sprites:\n" + output);
                    if (proc.ExitCode != 0) Debug.LogWarning("Boss importer warning: " + errors);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("MonsterTactics boss sprite installer: " + ex);
                EditorUtility.DisplayDialog("MonsterTactics", "Import failed: " + ex.Message, "OK");
                return;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            int count = Bosses.Count(b => AssetDatabase.LoadAssetAtPath<Sprite>(
                SpriteRoot + "/" + b + "/frame_000.png") != null);
            EditorUtility.DisplayDialog("MonsterTactics Boss Sprites",
                count == Bosses.Length
                    ? "All 7 solo boss sprite sets imported. Press Play, then use MonsterTactics > Tests > U6.3.2 Solo Boss Tester."
                    : count + "/7 boss sprite sets imported. Open Console to see which downloads failed; retry import if needed.", "OK");
        }

        [MenuItem("MonsterTactics/Monster/U6.3.2/Verify Boss Sprites", priority = 221)]
        public static void Verify()
        {
            AssetDatabase.Refresh();
            string result = string.Join("\n", Bosses.Select(b => b + ": " +
                AssetDatabase.FindAssets("t:Sprite", new[] { SpriteRoot + "/" + b }).Length + " frame(s)"));
            EditorUtility.DisplayDialog("MonsterTactics - Boss Sprites", result, "OK");
        }

        private static string Quote(string path) => "\"" + path.Replace("\"", "\\\"") + "\"";
    }
}
