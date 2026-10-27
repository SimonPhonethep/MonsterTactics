#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace MonsterTactics.EditorTools
{
    /// <summary>
    /// U6.7.6: Match item sprites to REAL MonsterTactics item names from MonsterTacticsItemTypes.
    /// Reads an editable CSV. Downloads first and applies only if all 46 are valid.
    /// Replaces the older U6.7.5 importer (same source filepath) so the old
    /// index-based mapping cannot accidentally be imported again.
    /// </summary>
    internal static class MonsterTacticsU675MonsterItemSpriteImporter
    {
        private const string RemoteBase = "https://raw.githubusercontent.com/MonsterAPI/sprites/master/sprites/items/";
        private const string LocalDir = "Assets/MonsterTactics/Resources/ItemIcons";
        internal const string CsvPath = "Assets/MonsterTactics/Editor/MonsterTacticsItemSpriteMap_U676.csv";
        private static bool running;
        internal class Entry
        {
            internal MonsterTacticsItemKind Kind;
            internal string Name;
            internal string Slug;
            internal Entry(MonsterTacticsItemKind kind, string name, string slug)
            {
                Kind = kind; Name = name; Slug = slug;
            }
        }

        internal static List<Entry> ReadAndValidateMapping()
        {
            if (!File.Exists(CsvPath)) throw new FileNotFoundException("Missing icon map: " + CsvPath);
            var lines = File.ReadAllLines(CsvPath);
            var result = new List<Entry>();
            var seenKinds = new HashSet<MonsterTacticsItemKind>();
            var seenSlugs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                // All first 3 CSV fields intentionally omit commas, so this parser can be simple.
                var cells = line.Split(',');
                if (cells.Length < 3) throw new InvalidDataException("CSV line " + (i + 1) + " needs 3 fields.");
                var kindName = cells[0].Trim().Trim('\"');
                var displayName = cells[1].Trim().Trim('\"');
                var slug = cells[2].Trim().Trim('\"');
                MonsterTacticsItemKind kind;
                if (!Enum.TryParse(kindName, false, out kind) || !MonsterTacticsItemTypes.IsAvailable(kind))
                    throw new InvalidDataException("Invalid or unavailable game item at CSV line " + (i + 1) + ": " + kindName);
                string actualName = MonsterTacticsItemTypes.Name(kind);
                if (!string.Equals(displayName, actualName, StringComparison.Ordinal))
                    throw new InvalidDataException("Name mismatch in CSV line " + (i + 1) + ". " +
                        kind + " is actually '" + actualName + "', not '" + displayName + "'.");
                if (slug.Length == 0 || !System.Text.RegularExpressions.Regex.IsMatch(slug, "^[a-z0-9-]+$"))
                    throw new InvalidDataException("Invalid Monster item PNG slug at CSV line " + (i + 1) + ": " + slug);
                if (!seenKinds.Add(kind)) throw new InvalidDataException("Duplicate game item in CSV: " + kind);
                if (!seenSlugs.Add(slug)) throw new InvalidDataException("Duplicate Monster icon in CSV: " + slug);
                result.Add(new Entry(kind, displayName, slug));
            }
            var expected = new HashSet<MonsterTacticsItemKind>();
            foreach (var kind in MonsterTacticsItemTypes.Components) expected.Add(kind);
            foreach (var kind in MonsterTacticsItemTypes.Completed) expected.Add(kind);
            expected.Add(MonsterTacticsItemKind.ItemRemover);
            expected.Add(MonsterTacticsItemKind.Reforger);
            if (result.Count != expected.Count)
                throw new InvalidDataException("Expected " + expected.Count + " item sprites but CSV contains " + result.Count + ".");
            foreach (var kind in expected)
                if (!seenKinds.Contains(kind)) throw new InvalidDataException("Missing item from CSV: " + kind);
            return result;
        }

        private static bool LooksLikePng(byte[] data)
        {
            if (data == null || data.Length < 40) return false;
            byte[] magic = { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a };
            for (int i = 0; i < magic.Length; i++) if (data[i] != magic[i]) return false;
            return true;
        }

        [MenuItem("MonsterTactics/Art/U6.7.6 Repair Item Names + PNGs")]
        public static async void Import()
        {
            if (running) return;
            if (!Directory.Exists(LocalDir))
            {
                EditorUtility.DisplayDialog("Missing MonsterTactics icons", "Could not find " + LocalDir + ".", "OK");
                return;
            }
            List<Entry> entries;
            try { entries = ReadAndValidateMapping(); }
            catch (Exception ex)
            {
                Debug.LogError("[MonsterTactics U6.7.6] Mapping invalid: " + ex.Message);
                EditorUtility.DisplayDialog("Item icon mapping error", ex.Message, "OK");
                return;
            }
            if (!EditorUtility.DisplayDialog("Repair item artwork?",
                "Replace " + entries.Count + " icons using a corrected item-name mapping?\n\n" +
                "Downloads the real copyrighted Monster inventory art for LOCAL use. " +
                "Backs up the old icons and does NOT modify gameplay.\n\n" +
                "If ANY of the downloads fail, nothing will be replaced.", "Repair 46 icons", "Cancel")) return;
            running = true;
            var downloads = new Dictionary<MonsterTacticsItemKind, byte[]>();
            string backupDir = null;
            try
            {
                using (var http = new HttpClient())
                {
                    http.Timeout = TimeSpan.FromSeconds(22);
                    http.DefaultRequestHeaders.UserAgent.ParseAdd("MonsterTactics-Item-Name-Art-Fix-U676/1.0");
                    for (int i = 0; i < entries.Count; i++)
                    {
                        Entry item = entries[i];
                        if (EditorUtility.DisplayCancelableProgressBar("U6.7.6: checking actual Monster icons",
                            item.Name + " -> " + item.Slug, (float)i / entries.Count))
                            throw new OperationCanceledException("Canceled before any icons were replaced.");
                        try
                        {
                            var bytes = await http.GetByteArrayAsync(RemoteBase + item.Slug + ".png");
                            if (!LooksLikePng(bytes)) throw new InvalidDataException("Invalid PNG signature");
                            downloads.Add(item.Kind, bytes);
                        }
                        catch (Exception ex)
                        {
                            throw new IOException("Could not download the '" + item.Slug + "' image for '" +
                                item.Name + "'. No files were changed. Details: " + ex.Message, ex);
                        }
                    }
                }
                // Only write after ALL 46 have downloaded and passed PNG signature check.
                backupDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../MonsterTacticsItemArtBackup_U676_" +
                    DateTime.Now.ToString("yyyyMMdd_HHmmss")));
                Directory.CreateDirectory(backupDir);
                var oldBytes = new Dictionary<MonsterTacticsItemKind, byte[]>();
                foreach (var e in entries)
                {
                    string file = Path.Combine(LocalDir, e.Kind + ".png");
                    if (File.Exists(file))
                    {
                        var previous = File.ReadAllBytes(file);
                        oldBytes.Add(e.Kind, previous);
                        File.WriteAllBytes(Path.Combine(backupDir, e.Kind + ".png"), previous);
                    }
                }
                var changed = new List<MonsterTacticsItemKind>();
                try
                {
                    foreach (var e in entries)
                    {
                        string path = Path.Combine(LocalDir, e.Kind + ".png");
                        File.WriteAllBytes(path, downloads[e.Kind]);
                        changed.Add(e.Kind);
                    }
                }
                catch
                {
                    foreach (var kind in changed)
                    {
                        string target = Path.Combine(LocalDir, kind + ".png");
                        byte[] old;
                        if (oldBytes.TryGetValue(kind, out old)) File.WriteAllBytes(target, old);
                        else if (File.Exists(target)) File.Delete(target);
                    }
                    throw;
                }
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                foreach (var e in entries)
                {
                    string path = LocalDir + "/" + e.Kind + ".png";
                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer == null) throw new InvalidDataException("Unity couldn't import PNG: " + path);
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.filterMode = FilterMode.Point;
                    importer.mipmapEnabled = false;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.alphaIsTransparency = true;
                    importer.npotScale = TextureImporterNPOTScale.None;
                    importer.SaveAndReimport();
                }
                AssetDatabase.Refresh();
                MonsterTacticsItemArt.ClearCache();
                Debug.Log("[MonsterTactics U6.7.6] Corrected " + entries.Count + " name/icon mappings. Backup: " + backupDir);
                EditorUtility.DisplayDialog("U6.7.6 item art repaired",
                    entries.Count + "/" + entries.Count + " icons corrected.\n" +
                    "Old PNGs saved to: " + backupDir + "\n\n" +
                    "Open MonsterTactics > Tests > U6.7.6 Item Art Audit to review each name.", "OK");
            }
            catch (Exception ex)
            {
                Debug.LogError("[MonsterTactics U6.7.6] " + ex);
                EditorUtility.DisplayDialog("U6.7.6 import not completed", ex.Message + "\n\n" +
                    "If it failed during download, nothing changed. " +
                    (backupDir != null ? "Previous art backup: " + backupDir : ""), "OK");
            }
            finally { EditorUtility.ClearProgressBar(); running = false; }
        }
    }
}
#endif
