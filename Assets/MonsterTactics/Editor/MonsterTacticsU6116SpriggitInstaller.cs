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
    /// U6.11.6: one-family installer. Works with the existing Content Catalog,
    /// sprite importer, GIF frame pipeline, shop, evolution and tester.
    /// Animation is downloaded separately (Windows PowerShell), not faked.
    /// </summary>
    public static class MonsterTacticsU6116SpriggitInstaller
    {
        private const string CatalogPath = "Assets/MonsterTactics/Resources/Content/MonsterTacticsContentCatalog.asset";
        private const string SpriteDir = "Assets/MonsterTactics/Art/MonsterU610";
        private const string FrameDir = "Assets/MonsterTactics/Art/MonsterU610Animated";
        private const string BackupDir = "Assets/MonsterTactics/Art/U6116SpriggitBackup";
        private const string BackupPath = BackupDir + "/MonsterTacticsContentCatalog.before-spriggit.asset";
        private const string AnimationScript = "Assets/MonsterTactics/Tools/DownloadU610AnimatedSprites.ps1";
        private static readonly string[] Names = { "Spriggit", "Branchblade", "Verdantis" };
        private static readonly int[] Dex = { 252, 253, 254 };
        private static readonly Dictionary<int, Sprite> Loaded = new Dictionary<int, Sprite>();
        private static Queue<int> pendingDex;
        private static UnityWebRequest request;
        private static int activeDex;
        private static bool active;
        private static Process process;

        public static bool IsInstalled
        {
            get
            {
                MonsterTacticsContentCatalog c = AssetDatabase.LoadAssetAtPath<MonsterTacticsContentCatalog>(CatalogPath);
                return c != null && c.monster != null && c.monster.Any(p => p != null && p.enabled &&
                    string.Equals(p.id, "family_spriggit", StringComparison.OrdinalIgnoreCase));
            }
        }

        [MenuItem("MonsterTactics/U6.11.6 Spriggit/1. Install Spriggit Family + Static Sprites")]
        public static void Install()
        {
            if (!Ready()) return;
            if (active) { Debug.LogWarning("[U6.11.6] Sprite download already running."); return; }
            Directory.CreateDirectory(SpriteDir);
            Loaded.Clear();
            pendingDex = new Queue<int>(Dex);
            active = true;
            EditorApplication.update -= DownloadTick;
            EditorApplication.update += DownloadTick;
            Debug.Log("[U6.11.6] Loading Spriggit/Branchblade/Verdantis sprites; only this family will be added.");
        }

        private static bool Ready()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            { Debug.LogWarning("[U6.11.6] Exit Play Mode first."); return false; }
            var c = AssetDatabase.LoadAssetAtPath<MonsterTacticsContentCatalog>(CatalogPath);
            if (c == null || c.readOnlyLegacyRuntime)
            { Debug.LogError("[U6.11.6] Active Content Catalog is required. No changes made."); return false; }
            return true;
        }

        private static void DownloadTick()
        {
            if (!active) { EditorApplication.update -= DownloadTick; return; }
            if (request != null)
            {
                if (!request.isDone) return;
                try
                {
                    if (request.result != UnityWebRequest.Result.Success ||
                        request.downloadHandler.data == null || request.downloadHandler.data.Length < 100)
                        throw new IOException("Sprite download failed for Dex " + activeDex + ": " + request.error);
                    File.WriteAllBytes(SpriteDir + "/monster_" + activeDex + ".png", request.downloadHandler.data);
                    Loaded[activeDex] = ImportSprite(activeDex);
                    if (Loaded[activeDex] == null)
                        throw new IOException("Unity could not import sprite " + activeDex);
                }
                catch (Exception ex) { Fail("[U6.11.6] " + ex.Message); return; }
                finally { if (request != null) { request.Dispose(); request = null; } }
            }
            if (pendingDex.Count > 0)
            {
                activeDex = pendingDex.Dequeue();
                string path = SpriteDir + "/monster_" + activeDex + ".png";
                if (File.Exists(path))
                {
                    Loaded[activeDex] = ImportSprite(activeDex);
                    if (Loaded[activeDex] == null) Fail("[U6.11.6] Could not import " + path);
                    return;
                }
                request = UnityWebRequest.Get("https://raw.githubusercontent.com/MonsterAPI/sprites/master/sprites/monster/" + activeDex + ".png");
                request.timeout = 20;
                request.SendWebRequest();
                return;
            }
            active = false;
            EditorApplication.update -= DownloadTick;
            if (Dex.Any(d => !Loaded.ContainsKey(d) || Loaded[d] == null))
            { Debug.LogError("[U6.11.6] Missing static sprites; catalog unchanged."); return; }
            Apply();
        }

        private static void Fail(string message)
        {
            Debug.LogError(message + " Catalog unchanged; use menu 1 to retry.");
            active = false;
            EditorApplication.update -= DownloadTick;
            if (request != null) { request.Dispose(); request = null; }
        }

        private static Sprite ImportSprite(int dex)
        {
            string path = SpriteDir + "/monster_" + dex + ".png";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return null;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 96;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void Apply()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<MonsterTacticsContentCatalog>(CatalogPath);
            if (catalog == null || catalog.readOnlyLegacyRuntime) return;
            Directory.CreateDirectory(BackupDir);
            AssetDatabase.Refresh();
            if (AssetDatabase.LoadAssetAtPath<MonsterTacticsContentCatalog>(BackupPath) == null &&
                !AssetDatabase.CopyAsset(CatalogPath, BackupPath))
            { Fail("[U6.11.6] Could not back up Content Catalog."); return; }
            Undo.RecordObject(catalog, "Install Spriggit family");
            MonsterTacticsMonsterRecord p = catalog.monster.Find(x => x != null &&
                string.Equals(x.id, "family_spriggit", StringComparison.OrdinalIgnoreCase));
            if (p == null) { p = new MonsterTacticsMonsterRecord(); catalog.monster.Add(p); }
            p.id = "family_spriggit";
            p.displayName = "Spriggit";
            p.enabled = true;
            p.shopCost = 2;
            p.combatRole = "Assassin";
            p.baseHp = 510;
            if (p.forms == null) p.forms = new List<MonsterTacticsMonsterFormRecord>();
            p.forms.Clear();
            for (int i = 0; i < Names.Length; i++)
                p.forms.Add(new MonsterTacticsMonsterFormRecord
                { name = Names[i], types = "Grass", bestiaryNumber = Dex[i], sprite = Loaded[Dex[i]] });
            if (p.origins == null) p.origins = new List<string>();
            p.origins.Clear();
            p.origins.Add("First Partners");
            p.stats = new MonsterTacticsMonsterStatRecord
            {
                attack = 61f, attackSpeed = .88f, abilityPower = 100f,
                criticalChance = .10f, criticalMultiplier = 1.5f,
                armor = 28, magicResist = 27, attackRange = 1,
                manaCost = 70, moveInterval = .42f
            };
            p.abilityId = "u6116_leaf_blade";
            p.designerNotes = "U6.11.6 2g Grass / First Partners Assassin. Leaf Blade: " +
                "175/265/420% AD physical, +25% damage from behind the target. " +
                "Uses standard combat evolution and 2g shop finite pool.";
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            MonsterTacticsU686SpriteBoundsBuilder.Build();
            Debug.Log("[U6.11.6] Installed only Spriggit family. Re-enter Play Mode for " +
                "Grant Tester, then run menu 2 to import authentic animation frames.");
        }

        [MenuItem("MonsterTactics/U6.11.6 Spriggit/2. Download All Three Animations (Windows)")]
        public static void DownloadAnimations()
        {
            if (!Ready() || !IsInstalled) { Debug.LogError("[U6.11.6] Install static sprites first."); return; }
            if (Application.platform != RuntimePlatform.WindowsEditor)
            { Debug.LogWarning("[U6.11.6] Automatic GIF extractor requires Windows/PowerShell. " +
                "Provide frames under " + FrameDir + "/{spriggit,branchblade,verdantis} and run menu 3."); return; }
            if (process != null && !process.HasExited)
            { Debug.LogWarning("[U6.11.6] Animation extractor already running."); return; }
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string script = Path.Combine(project, AnimationScript.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(script)) { Debug.LogError("[U6.11.6] Missing shared GIF importer script."); return; }
            string manifest = Path.Combine(project, "Library", "MonsterTacticsU6116Spriggit.csv");
            Directory.CreateDirectory(Path.GetDirectoryName(manifest));
            File.WriteAllText(manifest, "name,id" + Environment.NewLine +
                "spriggit,252" + Environment.NewLine +
                "branchblade,253" + Environment.NewLine +
                "verdantis,254" + Environment.NewLine);
            try
            {
                process = Process.Start(new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-NoProfile -ExecutionPolicy Bypass -File " + Quote(script) +
                        " -ProjectRoot " + Quote(project) + " -ManifestPath " + Quote(manifest),
                    WorkingDirectory = project, UseShellExecute = false, CreateNoWindow = true
                });
                if (process == null) throw new InvalidOperationException("PowerShell could not launch.");
                EditorApplication.update -= AnimationTick;
                EditorApplication.update += AnimationTick;
                Debug.Log("[U6.11.6] Importing 3 authentic frame animations through the existing pipeline.");
            }
            catch (Exception ex) { Debug.LogError("[U6.11.6] Animation import failed: " + ex.Message); }
        }

        private static string Quote(string value) { return "\"" + value.Replace("\"", "\\\"") + "\""; }
        private static void AnimationTick()
        {
            if (process == null) { EditorApplication.update -= AnimationTick; return; }
            if (!process.HasExited) return;
            int code = process.ExitCode;
            process.Dispose(); process = null;
            EditorApplication.update -= AnimationTick;
            Debug.Log("[U6.11.6] Animation extractor exit: " + code);
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            RebuildAnimations();
        }

        [MenuItem("MonsterTactics/U6.11.6 Spriggit/3. Rebuild Animation Catalog")]
        public static void RebuildAnimations()
        {
            if (!Ready()) return;
            MonsterTacticsU6103AnimatedSpritesMenu.Rebuild();
            Verify();
        }

        [MenuItem("MonsterTactics/U6.11.6 Spriggit/4. Verify Family and Animation")]
        public static void Verify()
        {
            var c = AssetDatabase.LoadAssetAtPath<MonsterTacticsContentCatalog>(CatalogPath);
            var p = c != null ? c.monster.Find(x => x != null && x.id == "family_spriggit" && x.enabled) : null;
            var library = AssetDatabase.LoadAssetAtPath<MonsterTacticsU610AnimatedArtCatalog>(
                "Assets/MonsterTactics/Resources/MonsterTacticsU610AnimatedArtCatalog.asset");
            MonsterTacticsCombatRules.AbilityProfile profile;
            bool ability = MonsterTacticsU610Abilities.TryProfile("Spriggit", 1,
                MonsterTacticsCombatRules.ForMonster("Spriggit"), out profile) && profile.Name == "Leaf Blade";
            int animated = 0;
            for (int i = 0; i < Names.Length; i++)
            {
                var f = p != null && p.forms != null && p.forms.Count == 3 ? p.forms[i] : null;
                bool ok = f != null && f.sprite != null && library != null && library.Forms != null &&
                    library.Forms.Any(x => x != null && x.Form == Names[i] && x.StaticSource == f.sprite &&
                        x.Frames != null && x.Frames.Length >= 2);
                if (ok) animated++;
                else Debug.LogWarning("[U6.11.6] No genuine animation for " + Names[i] +
                    "; static sprite + existing idle motion are fallback.");
            }
            bool ready = p != null && p.shopCost == 2 && p.combatRole == "Assassin" &&
                p.origins.Contains("First Partners") && ability;
            Debug.Log("[U6.11.6] " + (ready && animated == 3 ? "PASS" : "CHECK") +
                " Spriggit 2g/Grass/First Partners/Assassin; Leaf Blade=" + ability +
                "; genuine animated forms=" + animated + "/3; Grant Tester visible after re-entering Play Mode.");
        }
    }
}
#endif
