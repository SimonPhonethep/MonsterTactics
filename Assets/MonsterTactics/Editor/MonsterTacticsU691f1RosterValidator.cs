#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MonsterTactics
{
    /// <summary>Non-destructive editor checks. Leaves enabled flags unchanged.</summary>
    public static class MonsterTacticsU691f1RosterValidator
    {
        [MenuItem("MonsterTactics/Tests/Archive/Content Studio/U6.9.1f.1 Verify Live Roster Migration")]
        private static void Verify()
        {
            var catalog = MonsterTacticsContentStudioImporter.FindCatalog();
            if (catalog == null) throw new InvalidOperationException("Import the U6.9.1f.0 snapshot first");
            var report = MonsterTacticsContentStudioValidator.Check(catalog);
            if (!report.Passed) throw new InvalidOperationException(report.Summary +
                "\n" + String.Join("\n", report.errors.ToArray()));
            int enabled = 0;
            var seenForms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in catalog.monster)
            {
                if (p == null || !p.enabled) continue;
                enabled++;
                if (p.forms == null || p.forms.Count != 3) throw new InvalidOperationException("Invalid forms: " + p.id);
                foreach (var f in p.forms)
                    if (f != null) seenForms.Add(f.name);
            }
            if (enabled == 0) throw new InvalidOperationException("At least one enabled family required");
            var live = MonsterTacticsU5Roster.ActiveFamilies;
            if (catalog.readOnlyLegacyRuntime)
            {
                if (live.Length != MonsterTacticsU5Roster.All.Length)
                    throw new InvalidOperationException("Legacy mode changed the number of families");
            }
            else
            {
                if (!MonsterTacticsContentRosterRuntime.IsUsingCatalog)
                    throw new InvalidOperationException("Catalog mode requested but runtime fell back to legacy (see Console)");
                if (live.Length != enabled)
                    throw new InvalidOperationException("Active families " + live.Length + " != enabled " + enabled);
                foreach (var family in live)
                {
                    if (family.Cost < 1 || family.Cost > 5 || family.BaseHP < 1)
                        throw new InvalidOperationException("Invalid live family: " + family.Forms[0]);
                    if (MonsterTacticsU5Roster.Find(family.Forms[0]) == null)
                        throw new InvalidOperationException("Live lookup missing " + family.Forms[0]);
                }
            }
            for (int level = 1; level <= 10; level++)
            {
                int total = 0;
                foreach (int amount in MonsterTacticsU5Roster.TierWeights(level)) total += amount;
                if (total != 100) throw new InvalidOperationException("Shop tier odds must total 100 at level " + level);
            }
            Debug.Log("[U6.9.1f.1] PASS: catalog validation; " + enabled +
                " enabled families, " + seenForms.Count + " unique form names, 10 shop odds rows. " +
                (catalog.readOnlyLegacyRuntime ? "Legacy fallback ON" : "Content roster ON") +
                ". Live Unity gameplay still needs Play Mode verification.");
            EditorUtility.DisplayDialog("MonsterTactics U6.9.1f.1",
                "PASS: " + enabled + " enabled catalog families; 10 shop odds rows.\n" +
                (catalog.readOnlyLegacyRuntime ? "Legacy roster active" : "Content roster active") +
                ".\nNow test shop costs, stats, sprite overrides and disabled species in Play Mode.", "OK");
        }
    }
}
#endif
