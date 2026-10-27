#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace MonsterTactics.Editor
{
    /// <summary>
    /// Only Stormlynx. Uses the same 96x96 PowerShell GIF normalization and
    /// MonsterFrameAnimator catalog used by every other U6.10.3 family.
    /// Does not re-download or overwrite other families' artwork.
    /// </summary>
    public static class MonsterTacticsU6111StormlynxFixMenu
    {
        private const string Root = "Assets/MonsterTactics/Art/MonsterU610Animated/stormlynx";
        private const string AnimationScript = "Assets/MonsterTactics/Tools/DownloadU610AnimatedSprites.ps1";
        private const string CatalogPath = "Assets/MonsterTactics/Resources/Content/MonsterTacticsContentCatalog.asset";
        private static Process process;

        [MenuItem("MonsterTactics/U6.11.1 Stormlynx Fix/1. Download Matching Stormlynx Animation")]
        public static void DownloadStormlynxFrames()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[U6.11.1] Exit Play Mode first.");
                return;
            }
            if (!MonsterTacticsU611StormlynxInstaller.IsInstalled)
            {
                Debug.LogError("[U6.11.1] Install Stormlynx with the U6.11 menu first.");
                return;
            }
            if (Application.platform != RuntimePlatform.WindowsEditor)
            {
                Debug.LogWarning("[U6.11.1] Windows PowerShell is required for automatic GIF extraction. " +
                    "On other OSes, supply frame_000.png, frame_001.png, ... in " + Root +
                    " and run menu option 2.");
                return;
            }
            if (process != null && !process.HasExited)
            {
                Debug.LogWarning("[U6.11.1] Stormlynx importer already running.");
                return;
            }
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string script = Path.Combine(project, AnimationScript.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(script))
            {
                Debug.LogError("[U6.11.1] Missing existing U6.10.3 animation extraction script.");
                return;
            }
            string manifest = Path.Combine(project, "Library", "MonsterTacticsU6111Stormlynx.csv");
            Directory.CreateDirectory(Path.GetDirectoryName(manifest));
            File.WriteAllText(manifest, "name,id" + Environment.NewLine + "stormlynx,807" + Environment.NewLine);
            try
            {
                process = Process.Start(new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-NoProfile -ExecutionPolicy Bypass -File " + Quote(script) +
                        " -ProjectRoot " + Quote(project) + " -ManifestPath " + Quote(manifest),
                    WorkingDirectory = project,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                if (process == null) throw new InvalidOperationException("Could not launch PowerShell.");
                EditorApplication.update -= Poll;
                EditorApplication.update += Poll;
                Debug.Log("[U6.11.1] Importing genuine Stormlynx animation through the shared U6.10.3 pipeline.");
            }
            catch (Exception exception)
            {
                Debug.LogError("[U6.11.1] Failed to run animation importer: " + exception.Message);
                if (process != null) { process.Dispose(); process = null; }
            }
        }

        private static string Quote(string value) { return "\"" + value.Replace("\"", "\\\"") + "\""; }

        private static void Poll()
        {
            if (process == null) { EditorApplication.update -= Poll; return; }
            if (!process.HasExited) return;
            int exit = process.ExitCode;
            process.Dispose();
            process = null;
            EditorApplication.update -= Poll;
            Debug.Log("[U6.11.1] GIF extraction process exited with code " + exit);
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            Rebuild();
        }

        [MenuItem("MonsterTactics/U6.11.1 Stormlynx Fix/2. Rebuild Animation Catalog")]
        public static void Rebuild()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            { Debug.LogWarning("[U6.11.1] Exit Play Mode first."); return; }
            // Rebuild existing catalog for ALL local frames (without downloading
            // or modifying them), including the newly extracted Stormlynx frames.
            MonsterTacticsU6103AnimatedSpritesMenu.Rebuild();
            bool available = FrameCount() >= 2;
            Debug.Log(available ?
                "[U6.11.1] Stormlynx frames detected. Restart Play Mode to use genuine animation." :
                "[U6.11.1] No Stormlynx multi-frame GIF available. Static art remains the fallback. " +
                "Check MonsterAPI source availability or manually install compatible frames.");
        }

        [MenuItem("MonsterTactics/U6.11.1 Stormlynx Fix/3. Verify Stormlynx Fix")]
        public static void Verify()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<MonsterTacticsContentCatalog>(CatalogPath);
            var stormlynx = catalog != null && catalog.monster != null ?
                catalog.monster.Find(p => p != null && p.enabled && p.displayName == "Stormlynx") : null;
            var sprite = stormlynx != null && stormlynx.forms.Count > 0 ? stormlynx.forms[0].sprite : null;
            MonsterTacticsU610AnimatedArtCatalog.FormAnimation animation;
            bool animated = sprite != null &&
                MonsterTacticsU610AnimatedArtCatalog.TryGet("Stormlynx", sprite, out animation);
            Debug.Log("[U6.11.1] Stormlynx roster=" + (stormlynx != null) +
                ", sprite=" + (sprite != null) + ", animation frames=" + FrameCount() +
                ", matching shared animation catalog=" + animated +
                ", chain max=2 extra targets within 2 hexes, opening target=nearest, " +
                "Plasma Fists uses visible short lunge on every cast.");
        }

        private static int FrameCount()
        {
            return Directory.Exists(Root) ? Directory.GetFiles(Root, "frame_*.png").Length : 0;
        }
    }
}
#endif
