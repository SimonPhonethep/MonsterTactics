#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace MonsterTactics.Editor
{
    /// <summary>
    /// Opt-in, reversible visual migration for the 13 original playable families.
    /// Uses the exact MonsterAPI static sprite endpoint/TextureImporter settings
    /// already used by U6.10. Does not change gameplay records or the 18 new families.
    /// Original animated assets and scene files are never overwritten.
    /// </summary>
    public static class MonsterTacticsU6104UnifiedArtMenu
    {
        private const string CatalogPath = "Assets/MonsterTactics/Resources/Content/MonsterTacticsContentCatalog.asset";
        private const string BackupPath = "Assets/MonsterTactics/Art/U6104Backup/MonsterTacticsContentCatalog.before-unified.asset";
        private const string StaticFolder = "Assets/MonsterTactics/Art/MonsterU610Unified";
        private const string SettingsPath = "Assets/MonsterTactics/Resources/MonsterTacticsU6104ArtSettings.asset";
        private static readonly Queue<int> downloads = new Queue<int>();
        private static readonly Dictionary<int, Sprite> loaded = new Dictionary<int, Sprite>();
        private static UnityWebRequest request;
        private static int activeDex;
        private static int errors;
        private static bool downloading;

        public static IEnumerable<KeyValuePair<string,int>> EnumerateAllFormDex()
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (MonsterTacticsU5Roster.Family family in MonsterTacticsU5Roster.All)
                for (int i = 0; i < 3; ++i)
                    if (seen.Add(family.Forms[i]))
                        yield return new KeyValuePair<string,int>(family.Forms[i], family.Bestiary[i]);
            foreach (var form in MonsterTacticsU610RosterInstaller.EnumerateFormDex())
                if (seen.Add(form.Key)) yield return form;
            // The U6.10.3 animation importer now recognizes standalone U6.11 Stormlynx.
            if (MonsterTacticsU611StormlynxInstaller.IsInstalled && seen.Add("Stormlynx"))
                yield return new KeyValuePair<string,int>("Stormlynx", 807);
            // U6.11.4: the exact same U6.10.3 96x96 animation and HP-bound pipeline.
            if (MonsterTacticsU612EonDuoInstaller.IsInstalled("Skyweave") && seen.Add("Skyweave"))
                yield return new KeyValuePair<string,int>("Skyweave", 380);
            if (MonsterTacticsU612EonDuoInstaller.IsInstalled("Skyshard") && seen.Add("Skyshard"))
                yield return new KeyValuePair<string,int>("Skyshard", 381);
            // U6.11.6: dynamically include only this installed Assassin family.
            if (MonsterTacticsU6116SpriggitInstaller.IsInstalled)
            {
                if (seen.Add("Spriggit")) yield return new KeyValuePair<string,int>("Spriggit", 252);
                if (seen.Add("Branchblade")) yield return new KeyValuePair<string,int>("Branchblade", 253);
                if (seen.Add("Verdantis")) yield return new KeyValuePair<string,int>("Verdantis", 254);
            }
            // U6.11.7: use the existing animated-art importer for this family.
            if (MonsterTacticsU6117TrickletInstaller.IsInstalled)
            {
                if (seen.Add("Tricklet")) yield return new KeyValuePair<string,int>("Tricklet", 859);
                if (seen.Add("Trickshade")) yield return new KeyValuePair<string,int>("Trickshade", 860);
                if (seen.Add("Duskwraith")) yield return new KeyValuePair<string,int>("Duskwraith", 861);
            }
        }

        [MenuItem("MonsterTactics/U6.10.4/1. Download Matching Original Roster Art")]
        public static void DownloadOriginalArt()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            { Debug.LogWarning("[U6.10.4] Exit Play Mode first."); return; }
            if (downloading) { Debug.LogWarning("[U6.10.4] Import is already running."); return; }
            var catalog = AssetDatabase.LoadAssetAtPath<MonsterTacticsContentCatalog>(CatalogPath);
            if (catalog == null || catalog.readOnlyLegacyRuntime)
            { Debug.LogError("[U6.10.4] Active Content Catalog is required. Install U6.10 first."); return; }
            if (!Directory.Exists(StaticFolder)) Directory.CreateDirectory(StaticFolder);
            downloads.Clear(); loaded.Clear(); errors = 0;
            var ids = new HashSet<int>();
            foreach (MonsterTacticsU5Roster.Family family in MonsterTacticsU5Roster.All)
                foreach (int id in family.Bestiary)
                    if (ids.Add(id)) downloads.Enqueue(id);
            downloading = true;
            EditorApplication.update -= Poll;
            EditorApplication.update += Poll;
            Debug.Log("[U6.10.4] Importing " + ids.Count + " original-family sprite forms from the same source as U6.10. " +
                "Existing art stays untouched until all files are ready.");
        }

        private static void Poll()
        {
            if (!downloading) { EditorApplication.update -= Poll; return; }
            if (request != null)
            {
                if (!request.isDone) return;
                if (request.result != UnityWebRequest.Result.Success || request.downloadHandler.data == null ||
                    request.downloadHandler.data.Length < 100)
                { ++errors; Debug.LogWarning("[U6.10.4] Could not download " + activeDex + ": " + request.error); }
                else
                {
                    string path = StaticFolder + "/monster_" + activeDex + ".png";
                    try
                    {
                        File.WriteAllBytes(path, request.downloadHandler.data);
                        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                        loaded[activeDex] = ImportSprite(path);
                        if (loaded[activeDex] == null) ++errors;
                    }
                    catch (Exception ex) { ++errors; Debug.LogError("[U6.10.4] " + ex.Message); }
                }
                request.Dispose(); request = null;
            }
            if (downloads.Count > 0)
            {
                activeDex = downloads.Dequeue();
                string path = StaticFolder + "/monster_" + activeDex + ".png";
                if (File.Exists(path))
                {
                    loaded[activeDex] = ImportSprite(path);
                    if (loaded[activeDex] == null) ++errors;
                    return;
                }
                request = UnityWebRequest.Get("https://raw.githubusercontent.com/MonsterAPI/sprites/master/sprites/monster/" + activeDex + ".png");
                request.timeout = 20;
                request.SendWebRequest();
                return;
            }
            downloading = false;
            EditorApplication.update -= Poll;
            if (errors > 0)
            {
                Debug.LogError("[U6.10.4] " + errors + " sprites are missing. Original catalog unchanged. " +
                    "Check internet and rerun the installer.");
                return;
            }
            Apply();
        }

        private static Sprite ImportSprite(string path)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
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
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void Apply()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<MonsterTacticsContentCatalog>(CatalogPath);
            if (catalog == null) return;
            if (!AssetDatabase.IsValidFolder("Assets/MonsterTactics/Art/U6104Backup"))
                AssetDatabase.CreateFolder("Assets/MonsterTactics/Art", "U6104Backup");
            if (AssetDatabase.LoadAssetAtPath<MonsterTacticsContentCatalog>(BackupPath) == null &&
                !AssetDatabase.CopyAsset(CatalogPath, BackupPath))
            { Debug.LogError("[U6.10.4] Could not back up original Content Catalog. Stopping."); return; }
            Undo.RecordObject(catalog, "Unify original Monster artwork");
            int updated = 0;
            foreach (MonsterTacticsU5Roster.Family original in MonsterTacticsU5Roster.All)
            {
                MonsterTacticsMonsterRecord family = catalog.monster.Find(p => p != null && p.forms != null &&
                    p.forms.Count == 3 && string.Equals(p.forms[0].name, original.Forms[0],
                        StringComparison.OrdinalIgnoreCase));
                if (family == null) { Debug.LogWarning("[U6.10.4] Missing catalog family " + original.Forms[0]); continue; }
                for (int i=0; i<3; ++i)
                {
                    Sprite sprite;
                    if (loaded.TryGetValue(original.Bestiary[i], out sprite) && sprite != null)
                    { family.forms[i].sprite = sprite; ++updated; }
                }
            }
            EditorUtility.SetDirty(catalog);
            var settings = AssetDatabase.LoadAssetAtPath<MonsterTacticsU6104ArtSettings>(SettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<MonsterTacticsU6104ArtSettings>();
                AssetDatabase.CreateAsset(settings, SettingsPath);
            }
            settings.UnifiedLegacyArt = true;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            MonsterTacticsU686SpriteBoundsBuilder.Build();
            Debug.Log("[U6.10.4] Matched " + updated + "/33 original forms to U6.10's static art source. " +
                "Original catalog backed up. Now run U6.10.3 animation downloader, then verify U6.10.4.");
        }

        [MenuItem("MonsterTactics/U6.10.4/2. Verify Unified Playable Roster")]
        public static void Verify()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<MonsterTacticsContentCatalog>(CatalogPath);
            var settings = AssetDatabase.LoadAssetAtPath<MonsterTacticsU6104ArtSettings>(SettingsPath);
            if (catalog == null) { Debug.LogError("[U6.10.4] Catalog unavailable."); return; }
            int legacy = 0, newFamilies = 0, missing = 0;
            foreach (MonsterTacticsU5Roster.Family original in MonsterTacticsU5Roster.All)
            {
                var record = catalog.monster.Find(p => p != null && p.forms != null &&
                    p.forms.Count == 3 && string.Equals(p.forms[0].name, original.Forms[0], StringComparison.OrdinalIgnoreCase));
                if (record != null && record.forms.All(form => form.sprite != null)) ++legacy;
                else { ++missing; Debug.LogWarning("[U6.10.4] Original family lacks unified art: " + original.Forms[0]); }
            }
            foreach (var family in catalog.monster)
                if (family != null && family.enabled && family.forms != null && family.forms.Count == 3 &&
                    !MonsterTacticsU5Roster.All.Any(old => string.Equals(old.Forms[0], family.forms[0].name,
                        StringComparison.OrdinalIgnoreCase))) ++newFamilies;
            bool passed = settings != null && settings.UnifiedLegacyArt && legacy == 13 && newFamilies >= 18;
            Debug.Log("[U6.10.4] " + (passed ? "PASS" : "CHECK") +
                ": original families with new static art=" + legacy + "/13; U6.10 families=" + newFamilies +
                "; expected at least 31 playable; missing=" + missing + "; visual scale original=" +
                (settings != null ? settings.OriginalPlayableScale.ToString("0.00") : "not installed") +
                ", new=" + (settings != null ? settings.NewPlayableScale.ToString("0.00") : "not installed") +
                ". Run U6.10.3 > 3. Verify Animation Integration for animated-frame counts.");
        }

        [MenuItem("MonsterTactics/U6.10.4/3. Restore Original Playable Art")]
        public static void RestoreOriginal()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            { Debug.LogWarning("[U6.10.4] Exit Play Mode first."); return; }
            var catalog = AssetDatabase.LoadAssetAtPath<MonsterTacticsContentCatalog>(CatalogPath);
            var backup = AssetDatabase.LoadAssetAtPath<MonsterTacticsContentCatalog>(BackupPath);
            if (catalog == null || backup == null)
            { Debug.LogWarning("[U6.10.4] Original catalog backup not available."); return; }
            Undo.RecordObject(catalog, "Restore legacy Monster artwork");
            foreach (MonsterTacticsU5Roster.Family original in MonsterTacticsU5Roster.All)
            {
                var oldRecord = backup.monster.Find(p => p != null && p.forms != null &&
                    p.forms.Count == 3 && string.Equals(p.forms[0].name, original.Forms[0], StringComparison.OrdinalIgnoreCase));
                var current = catalog.monster.Find(p => p != null && p.forms != null &&
                    p.forms.Count == 3 && string.Equals(p.forms[0].name, original.Forms[0], StringComparison.OrdinalIgnoreCase));
                if (oldRecord == null || current == null) continue;
                for (int i=0; i<3; ++i) current.forms[i].sprite = oldRecord.forms[i].sprite;
            }
            EditorUtility.SetDirty(catalog);
            var settings = AssetDatabase.LoadAssetAtPath<MonsterTacticsU6104ArtSettings>(SettingsPath);
            if (settings != null) { settings.UnifiedLegacyArt = false; EditorUtility.SetDirty(settings); }
            AssetDatabase.SaveAssets();
            MonsterTacticsU686SpriteBoundsBuilder.Build();
            Debug.Log("[U6.10.4] Restored original 13-family art references. Legacy frames were never removed. " +
                "Restart Play Mode to see restored assets.");
        }
    }
}
#endif
