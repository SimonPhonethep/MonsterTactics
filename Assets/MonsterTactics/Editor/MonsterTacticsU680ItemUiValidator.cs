#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MonsterTactics
{
    /// <summary>Checks invariant data and inventory transactions, not Unity mouse rendering.</summary>
    public static class MonsterTacticsU680ItemUiValidator
    {
        [MenuItem("MonsterTactics/Tests/Archive/Items/U6.8.0 Verify TFT-Style Items")]
        public static void Verify()
        {
            int checks = 0;
            var problems = new List<string>();
            void Check(bool ok, string name)
            {
                checks++;
                if (!ok) problems.Add(name);
            }
            Check(MonsterTacticsItemTypes.Components.Length == 8, "Exactly eight components");
            Check(MonsterTacticsItemTypes.Completed.Length == 36, "Exactly 36 standard items");
            Check(MonsterTacticsHeldItems.MaxSlots == 3, "Equipped capacity of three");
            var unique = new HashSet<MonsterTacticsItemKind>();
            for (int a = 0; a < 8; a++)
                for (int b = a; b < 8; b++)
                {
                    MonsterTacticsItemKind result, reverse;
                    bool valid = MonsterTacticsItemTypes.TryRecipe(MonsterTacticsItemTypes.Components[a],
                        MonsterTacticsItemTypes.Components[b], out result);
                    bool symmetric = MonsterTacticsItemTypes.TryRecipe(MonsterTacticsItemTypes.Components[b],
                        MonsterTacticsItemTypes.Components[a], out reverse);
                    Check(valid && symmetric && result == reverse,
                        "Recipe matches in either order: " + a + "/" + b);
                    if (valid) unique.Add(result);
                }
            Check(unique.Count == 36, "All 36 completed items can be crafted");
            var gate = new MonsterTacticsCraftHoldGate();
            gate.UpdateHover(0,1,5f);
            Check(!gate.IsComplete(5.5f), "Early release must not craft");
            Check(gate.IsComplete(5.86f), "Holding 0.85+ seconds is ready");
            gate.UpdateHover(0,2,5.87f);
            Check(!gate.IsComplete(5.88f), "Changing target resets timer");
            gate.Cancel();
            Check(!gate.Active, "Cancel resets progress");

            GameObject temp = new GameObject("U6.8.0 Item UI Test (temporary)");
            try
            {
                MonsterTacticsItemInventory items = temp.AddComponent<MonsterTacticsItemInventory>();
                items.Grant(MonsterTacticsItemKind.AttackFang);
                items.Grant(MonsterTacticsItemKind.VitalSeed);
                MonsterTacticsItemKind combined;
                Check(items.TryCraftFromRecipe(0,MonsterTacticsItemKind.VitalSeed,out combined) &&
                    items.Count == 1 && MonsterTacticsItemTypes.IsCompleted(combined),
                    "Craft from right-click recipe with two components");
                items.ClearTestInventory();
                items.Grant(MonsterTacticsItemKind.Craft00);
                items.Grant(MonsterTacticsItemKind.Craft01);
                Check(items.TryReorderBenchItems(0,1) &&
                    items.Items[0] == MonsterTacticsItemKind.Craft01 &&
                    items.Items[1] == MonsterTacticsItemKind.Craft00,
                    "Reorder two non-recipe items without crafting");
                MonsterTacticsHeldItems held = temp.AddComponent<MonsterTacticsHeldItems>();
                Check(held.TryAdd(MonsterTacticsItemKind.Craft00) &&
                    held.TryAdd(MonsterTacticsItemKind.Craft01) &&
                    held.TryAdd(MonsterTacticsItemKind.Craft02) && held.IsFull &&
                    !held.TryAdd(MonsterTacticsItemKind.Craft03),
                    "Three equipped slots prevent a fourth item");
                Check(!held.TryAdd(MonsterTacticsItemKind.ItemRemover),
                    "Consumables aren't ordinary equipped items");
            }
            finally { Object.DestroyImmediate(temp); }

            string summary = "U6.8.0: " + (checks - problems.Count) + "/" + checks +
                " data/transaction checks passed.\n";
            if (problems.Count > 0)
            {
                summary += string.Join("\n",problems.ToArray());
                Debug.LogError("[U6.8.0] " + summary);
                EditorUtility.DisplayDialog("MonsterTactics U6.8.0", summary, "OK");
            }
            else
            {
                Debug.Log("[U6.8.0] " + summary +
                    "Run Play Mode for hover, release, and world bench positions.");
                EditorUtility.DisplayDialog("MonsterTactics U6.8.0", summary +
                    "\nNow test manual drag/hover interactions in Play Mode.", "OK");
            }
        }
    }
}
#endif
