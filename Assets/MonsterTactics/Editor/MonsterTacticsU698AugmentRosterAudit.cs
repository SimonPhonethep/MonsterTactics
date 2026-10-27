#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MonsterTactics
{
    /// <summary>Show the actual catalog counts (not hypothetical installer totals).</summary>
    public static class MonsterTacticsU698AugmentRosterAudit
    {
        [MenuItem("MonsterTactics/Augments/U6.9.8 Count Installed and Enabled Augments")]
        public static void Count()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<MonsterTacticsAugmentCatalog>(
                "Assets/MonsterTactics/Resources/Augments/MonsterTacticsAugmentCatalog.asset");
            if (catalog == null)
            {
                Debug.LogError("[U6.9.8] Missing saved Augment Catalog.");
                return;
            }
            int[] installed = new int[3], enabled = new int[3];
            int duplicateIds = 0, missingIds = 0;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (catalog.definitions != null)
                foreach (var def in catalog.definitions)
                {
                    if (def == null || string.IsNullOrWhiteSpace(def.id))
                    { missingIds++; continue; }
                    if (!seen.Add(def.id.Trim())) { duplicateIds++; continue; }
                    int tier = (int)def.tier;
                    if (tier < 0 || tier > 2) continue;
                    installed[tier]++;
                    if (def.enabledInPool) enabled[tier]++;
                }
            string report = "Current Unity augment catalog (unique IDs):\n" +
                "Rookie: " + installed[0] + " installed / " + enabled[0] + " enabled\n" +
                "Ace: " + installed[1] + " installed / " + enabled[1] + " enabled\n" +
                "Champion: " + installed[2] + " installed / " + enabled[2] + " enabled\n\n" +
                "TOTAL: " + (installed[0]+installed[1]+installed[2]) + " installed / " +
                (enabled[0]+enabled[1]+enabled[2]) + " enabled" +
                (duplicateIds > 0 ? "\nDuplicate IDs ignored: " + duplicateIds : "") +
                (missingIds > 0 ? "\nMissing/null definitions: " + missingIds : "") +
                "\n\nNew definitions in earlier U6.9 installers only count after those installers run.";
            Debug.Log("[U6.9.8] " + report.Replace("\n", " | "));
            EditorUtility.DisplayDialog("MonsterTactics Augment Roster", report, "OK");
        }
    }
}
#endif
