#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace MonsterTactics.Editor
{
    /// <summary>Exercises inventory-only crafting without needing a running match.</summary>
    public static class MonsterTacticsU677BenchCraftingTester
    {
        [MenuItem("MonsterTactics/Tests/Archive/Items/U6.7.7 Verify Bench Crafting")]
        public static void Run()
        {
            GameObject go = new GameObject("MonsterTactics U6.7.7 crafting validation (temporary)");
            try
            {
                MonsterTacticsItemInventory bench = go.AddComponent<MonsterTacticsItemInventory>();
                int checkedCases = 0;

                // All 64 directional component pairings, including 8 duplicates.
                foreach (MonsterTacticsItemKind a in MonsterTacticsItemTypes.Components)
                foreach (MonsterTacticsItemKind b in MonsterTacticsItemTypes.Components)
                {
                    bench.ClearTestInventory();
                    bench.Grant(a);
                    bench.Grant(b);
                    if (!MonsterTacticsItemTypes.TryRecipe(a, b, out MonsterTacticsItemKind expected))
                        throw new Exception("No recipe for " + a + " + " + b);
                    if (!bench.TryCombineBenchItems(0, 1, out MonsterTacticsItemKind actual) ||
                        actual != expected || bench.Count != 1 || bench.Items[0] != expected)
                        throw new Exception("Failed left-to-right: " + a + " + " + b);
                    checkedCases++;
                }

                // Dropping the last slot onto the first must preserve the first slot.
                bench.ClearTestInventory();
                bench.Grant(MonsterTacticsItemKind.VitalSeed);
                bench.Grant(MonsterTacticsItemKind.SwiftFeather);
                bench.Grant(MonsterTacticsItemKind.AttackFang);
                MonsterTacticsItemTypes.TryRecipe(MonsterTacticsItemKind.AttackFang,
                    MonsterTacticsItemKind.VitalSeed, out MonsterTacticsItemKind expectedReverse);
                if (!bench.TryCombineBenchItems(2, 0, out MonsterTacticsItemKind reverse) ||
                    reverse != expectedReverse || bench.Count != 2 ||
                    bench.Items[0] != expectedReverse || bench.Items[1] != MonsterTacticsItemKind.SwiftFeather)
                    throw new Exception("Reverse slot drop/replacement failed");
                checkedCases++;

                // Dropping an earlier slot onto a later slot should adjust its index.
                bench.ClearTestInventory();
                bench.Grant(MonsterTacticsItemKind.AttackFang);
                bench.Grant(MonsterTacticsItemKind.SwiftFeather);
                bench.Grant(MonsterTacticsItemKind.VitalSeed);
                if (!bench.TryCombineBenchItems(0, 2, out MonsterTacticsItemKind forward) ||
                    forward != expectedReverse || bench.Count != 2 ||
                    bench.Items[0] != MonsterTacticsItemKind.SwiftFeather || bench.Items[1] != expectedReverse)
                    throw new Exception("Forward slot drop/replacement failed");
                checkedCases++;

                // Guard against consuming a completed item or an item tool.
                bench.ClearTestInventory();
                bench.Grant(MonsterTacticsItemKind.Craft00);
                bench.Grant(MonsterTacticsItemKind.AttackFang);
                if (bench.TryCombineBenchItems(0, 1, out _) || bench.Count != 2)
                    throw new Exception("Completed item was wrongly consumed");
                checkedCases++;
                bench.ClearTestInventory();
                bench.Grant(MonsterTacticsItemKind.ItemRemover);
                bench.Grant(MonsterTacticsItemKind.AttackFang);
                if (bench.TryCombineBenchItems(0, 1, out _) || bench.Count != 2)
                    throw new Exception("Utility item was wrongly consumed");
                checkedCases++;

                // Source on first page, target on second; no item may be lost.
                bench.ClearTestInventory();
                bench.Grant(MonsterTacticsItemKind.AttackFang);
                for (int i = 0; i < 9; ++i) bench.Grant(MonsterTacticsItemKind.SwiftFeather);
                bench.Grant(MonsterTacticsItemKind.VitalSeed);
                if (!bench.TryCombineBenchItems(0, 10, out MonsterTacticsItemKind paged) ||
                    paged != expectedReverse || bench.Count != 10 || bench.Items[9] != paged)
                    throw new Exception("Cross-page crafting failed");
                checkedCases++;

                Debug.Log("[MonsterTactics U6.7.7] PASS — " + checkedCases +
                          " bench crafting test cases (64 ordered recipes + index and invalid tests).");
                EditorUtility.DisplayDialog("MonsterTactics U6.7.7",
                    "Bench crafting validation passed: " + checkedCases + " cases.", "OK");
            }
            catch (Exception ex)
            {
                Debug.LogError("[MonsterTactics U6.7.7] FAIL: " + ex);
                EditorUtility.DisplayDialog("MonsterTactics U6.7.7", "Crafting test failed. See Unity Console.", "OK");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}
#endif
