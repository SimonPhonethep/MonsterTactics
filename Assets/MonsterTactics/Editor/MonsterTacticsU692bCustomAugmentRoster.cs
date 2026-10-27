#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MonsterTactics
{
    /// <summary>
    /// U6.9.2b: Explicit, backed-up removal of the original sample/draft
    /// augment assets. Never runs on import, reload, or Play Mode entry.
    /// New authored augment definitions are never deleted by this command.
    /// </summary>
    public static class MonsterTacticsU692bCustomAugmentRoster
    {
        private const string Root = "Assets/MonsterTactics/Resources/Augments";
        private const string CatalogPath = Root + "/MonsterTacticsAugmentCatalog.asset";
        private const string MeowthId = "thank_you_meowth";

        // The original 20 U6.9.0 examples and two optional U6.9.2 economy
        // drafts. Only these exact IDs are elidunebit for deletion.
        private static readonly HashSet<string> TemporaryIds =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "starter_allowance", "pocket_reroll", "vital_supplies",
                "rich_trainer", "bargain_hunt", "field_commander",
                "champion_reserves", "grand_income", "royal_treasure",
                "training_fund", "mana_supplies", "fang_supplies",
                "extra_interest", "advance_allowance", "field_supplies",
                "grand_bounty", "deep_reserves", "champion_cache",
                "ancient_relic", "elemental_crest",
                "u692_training_notes", "u692_steady_allowance"
            };

        private static MonsterTacticsAugmentCatalog OpenCatalog()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("MonsterTactics Custom Augments",
                    "Stop Play Mode before modifying augment assets.", "OK");
                return null;
            }
            var catalog = AssetDatabase.LoadAssetAtPath<MonsterTacticsAugmentCatalog>(CatalogPath);
            if (catalog == null)
                EditorUtility.DisplayDialog("MonsterTactics Custom Augments",
                    "The saved augment catalog was not found at:\n" + CatalogPath +
                    "\n\nOpen the scene/project containing your existing augment catalog before running this tool.", "OK");
            return catalog;
        }

        private static List<MonsterTacticsAugmentDefinition> FindTemporary(
            MonsterTacticsAugmentCatalog catalog)
        {
            var result = new List<MonsterTacticsAugmentDefinition>();
            if (catalog == null || catalog.definitions == null) return result;
            var seen = new HashSet<MonsterTacticsAugmentDefinition>();
            foreach (var def in catalog.definitions)
                if (def != null && TemporaryIds.Contains(def.id ?? "") && seen.Add(def))
                    result.Add(def);
            return result;
        }

        private static void BackupFile(string projectRoot, string backupRoot, string unityPath)
        {
            string local = unityPath.Replace('/', Path.DirectorySeparatorChar);
            string from = Path.Combine(projectRoot, local);
            if (!File.Exists(from))
                throw new IOException("Missing file needed for backup: " + from);
            string to = Path.Combine(backupRoot, local);
            string folder = Path.GetDirectoryName(to);
            if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
            File.Copy(from, to, false);
            if (File.Exists(from + ".meta")) File.Copy(from + ".meta", to + ".meta", false);
        }

        /// <summary>Back up the catalog AND all linked definition assets, not
        /// just samples. Leaves textures, scenes and other catalog types alone.</summary>
        private static string CreateExternalBackup(MonsterTacticsAugmentCatalog catalog)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string backupRoot = Path.Combine(projectRoot, "MonsterTacticsAugmentBackups",
                "Before_Custom_Roster_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff"));
            Directory.CreateDirectory(backupRoot);
            BackupFile(projectRoot, backupRoot, CatalogPath);
            var copied = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (catalog.definitions != null)
                foreach (var def in catalog.definitions)
                {
                    if (def == null) continue;
                    string path = AssetDatabase.GetAssetPath(def);
                    if (!string.IsNullOrEmpty(path) && path.StartsWith("Assets/", StringComparison.Ordinal) &&
                        copied.Add(path))
                        BackupFile(projectRoot, backupRoot, path);
                }
            File.WriteAllText(Path.Combine(backupRoot, "READ_ME_FIRST.txt"),
                "MonsterTactics augment assets backed up BEFORE clearing temporary examples.\r\n" +
                "The original .asset and .meta files are copied under Assets/ here.\r\n" +
                "Do not overwrite an active Unity project without first backing up its newer files.\r\n" +
                "Restore only with Unity closed, or copy the desired definition assets to a safe new path.\r\n" +
                "The catalog itself was also saved so the old 22-definition list can be recovered.\r\n");
            return backupRoot;
        }

        [MenuItem("MonsterTactics/Augments/U6.9.2b Delete Temporary Augments (Back Up First)")]
        public static void DeleteTemporaryAugments()
        {
            var catalog = OpenCatalog();
            if (catalog == null) return;
            var temporary = FindTemporary(catalog);
            int customCount = catalog.definitions == null ? 0 : catalog.definitions.Count - temporary.Count;
            if (temporary.Count == 0)
            {
                EditorUtility.DisplayDialog("MonsterTactics Custom Augments",
                    "No known temporary sample augments are linked. The tool has not deleted anything.\n" +
                    "Any already-created custom augments have been preserved.", "OK");
                return;
            }
            if (!EditorUtility.DisplayDialog("DELETE TEMPORARY AUGMENTS?",
                "Found " + temporary.Count + " of the known starter/sample/economy-draft augments.\n\n" +
                "This will:\n" +
                "• Back up the catalog and every linked augment definition OUTSIDE Assets.\n" +
                "• Permanently delete only these " + temporary.Count + " temporary definition assets from Assets.\n" +
                "• Preserve " + Mathf.Max(0, customCount) + " other catalog entries and all icons/handlers.\n" +
                "• Switch Master Enabled OFF until a new playable roster is ready.\n" +
                "• Block the legacy Create Samples buttons.\n\n" +
                "Unity Undo cannot undo deleted assets. Continue?",
                "Back Up & Delete", "Cancel")) return;

            string backupRoot;
            try { backupRoot = CreateExternalBackup(catalog); }
            catch (Exception error)
            {
                Debug.LogError("[U6.9.2b] Backup failed, so no definitions were deleted: " + error);
                EditorUtility.DisplayDialog("Augment Backup Failed",
                    "Nothing was deleted. Check the Console for the backup error.", "OK");
                return;
            }

            // Remove catalog references first, then delete exact linked assets.
            // Never delete art, scripts, external definitions, or any custom ID.
            Undo.RecordObject(catalog, "Clear temporary augment references");
            catalog.definitions.RemoveAll(def => def != null && TemporaryIds.Contains(def.id ?? ""));
            catalog.masterEnabled = false;
            catalog.suppressLegacySampleCreation = true;
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            int deleted = 0, unlinkedOnly = 0;
            foreach (var def in temporary)
            {
                string path = AssetDatabase.GetAssetPath(def);
                if (!string.IsNullOrEmpty(path) && path.StartsWith(Root + "/", StringComparison.Ordinal) &&
                    path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase) &&
                    AssetDatabase.LoadMainAssetAtPath(path) == def &&
                    AssetDatabase.DeleteAsset(path)) deleted++;
                else unlinkedOnly++;
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            string summary = "Removed " + temporary.Count + " sample definitions from catalog, " +
                deleted + " original asset files deleted" + (unlinkedOnly > 0 ?
                ", " + unlinkedOnly + " unlinked only (outside sample folder or protected)" : "") +
                ".\n\nFull pre-deletion backup:\n" + backupRoot +
                "\n\nMaster Enabled is OFF until you choose to test the new roster.";
            Debug.Log("[U6.9.2b] " + summary);
            EditorUtility.DisplayDialog("Temporary Augments Cleared", summary, "OK");
        }

        [MenuItem("MonsterTactics/Augments/U6.9.2b Create Thank You Meowth (Disabled)")]
        public static void CreateMeowth()
        {
            var catalog = OpenCatalog();
            if (catalog == null) return;
            if (FindTemporary(catalog).Count > 0)
            {
                EditorUtility.DisplayDialog("Custom Augment Roster",
                    "Temporary examples are still linked. Run the U6.9.2b Delete Temporary Augments command first.", "OK");
                return;
            }
            foreach (var existing in catalog.definitions)
                if (existing != null && string.Equals(existing.id, MeowthId, StringComparison.OrdinalIgnoreCase))
                {
                    Selection.activeObject = existing;
                    EditorUtility.DisplayDialog("Thank You, Meowth!",
                        "This augment already exists. I selected the existing record instead of overwriting your edits.", "OK");
                    return;
                }
            string path = Root + "/" + MeowthId + ".asset";
            var conflict = AssetDatabase.LoadMainAssetAtPath(path);
            if (conflict != null && !(conflict is MonsterTacticsAugmentDefinition))
            {
                Debug.LogError("[U6.9.2b] ID path occupied by a different asset: " + path);
                return;
            }
            MonsterTacticsAugmentDefinition augment = conflict as MonsterTacticsAugmentDefinition;
            if (augment == null)
            {
                augment = ScriptableObject.CreateInstance<MonsterTacticsAugmentDefinition>();
                augment.id = MeowthId;
                augment.displayName = "Thank You, Meowth!";
                augment.description = "Gain 22 gold immediately.";
                augment.tier = MonsterTacticsAugmentTier.Ace;
                augment.category = MonsterTacticsAugmentCategory.Economy;
                augment.enabledInPool = false;
                augment.offerWeight = 100;
                augment.allowedSelections = MonsterTacticsAugmentSlots.All;
                augment.effects = new List<MonsterTacticsAugmentEffect>
                {
                    new MonsterTacticsAugmentEffect
                    {
                        kind = MonsterTacticsAugmentEffectKind.Gold,
                        trigger = MonsterTacticsAugmentTrigger.OnChosen,
                        value = 22,
                        maxTriggers = 1
                    }
                };
                AssetDatabase.CreateAsset(augment, path);
            }
            Undo.RecordObject(catalog, "Link Thank You Meowth augment");
            catalog.definitions.Add(augment);
            catalog.suppressLegacySampleCreation = true;
            // Deliberately leave masterEnabled and augment enabled flags OFF.
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Selection.activeObject = augment;
            Debug.Log("[U6.9.2b] Created Thank You, Meowth! as a DISABLED Ace augment. " +
                "Uses the existing one-time 22 Gold handler. Configure artwork and enable when ready.");
        }

        [MenuItem("MonsterTactics/Tests/Archive/Augments/U6.9.2b Verify Custom Augment Roster")]
        public static void VerifyCustomRoster()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<MonsterTacticsAugmentCatalog>(CatalogPath);
            if (catalog == null)
            {
                Debug.LogError("[U6.9.2b] FAIL: catalog missing.");
                return;
            }
            var temporary = FindTemporary(catalog);
            if (temporary.Count != 0)
            {
                Debug.LogError("[U6.9.2b] FAIL: " + temporary.Count + " temporary sample augments are still linked.");
                return;
            }
            int custom = 0;
            foreach (var def in catalog.definitions)
            {
                if (def == null) continue;
                custom++;
                if (!string.Equals(def.id, MeowthId, StringComparison.OrdinalIgnoreCase)) continue;
                if (def.tier != MonsterTacticsAugmentTier.Ace || def.effects == null || def.effects.Count != 1 ||
                    def.effects[0].kind != MonsterTacticsAugmentEffectKind.Gold ||
                    def.effects[0].trigger != MonsterTacticsAugmentTrigger.OnChosen ||
                    def.effects[0].value != 22 || !MonsterTacticsAugmentEffectSupport.IsExecutable(def.effects[0]))
                {
                    Debug.LogError("[U6.9.2b] FAIL: Thank You, Meowth! does not grant 22 gold on selection.");
                    return;
                }
            }
            if (!catalog.suppressLegacySampleCreation)
            {
                Debug.LogError("[U6.9.2b] FAIL: legacy sample generation was not locked.");
                return;
            }
            Debug.Log("[U6.9.2b] PASS: no temporary definitions linked; " + custom +
                " custom definitions; sample generation locked. " +
                "Actual gold payouts still require a Play Mode selection test.");
        }
    }
}
#endif
