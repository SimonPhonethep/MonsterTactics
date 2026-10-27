#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace MonsterTactics.Editor
{
    public static class MonsterTacticsU678HoldCraftTester
    {
        [MenuItem("MonsterTactics/Tests/Archive/Items/U6.7.8 Verify Hold-to-Combine")]
        public static void Run()
        {
            GameObject temp = new GameObject("U6.7.8 validation (temporary)");
            try
            {
                MonsterTacticsCraftHoldGate hold = new MonsterTacticsCraftHoldGate();
                const float start = 10f;
                hold.UpdateHover(0, 1, start);
                Ensure(hold.Active && hold.Progress(start) == 0f, "Hold did not begin at zero");
                Ensure(!hold.IsComplete(start + MonsterTacticsCraftHoldGate.RequiredSeconds - .01f),
                    "Craft happened before the bar finished");
                hold.Cancel(); // MouseUp early
                Ensure(!hold.Active && hold.Progress(start + 10f) == 0f &&
                    !hold.IsComplete(start + 10f), "MouseUp cancel retained progress");
                hold.UpdateHover(0, 1, 20f);
                hold.UpdateHover(0, 2, 20.5f); // new hovered target
                Ensure(hold.Progress(20.5f) == 0f, "Changing targets did not reset progress");
                hold.UpdateHover(0, 1, 21f);
                Ensure(!hold.IsComplete(21.8f), "Returning to target reused old progress");
                // The test should not probe an exact floating-point boundary.
                // Unity float timestamps can round elapsed time slightly below 0.85f.
                Ensure(hold.IsComplete(21f + MonsterTacticsCraftHoldGate.RequiredSeconds + .01f),
                    "Complete hold did not reach the threshold");
                hold.Cancel();
                Ensure(!hold.Active, "Timer did not clear after craft");

                MonsterTacticsItemInventory bench = temp.AddComponent<MonsterTacticsItemInventory>();
                int recipes = 0;
                foreach (MonsterTacticsItemKind source in MonsterTacticsItemTypes.Components)
                foreach (MonsterTacticsItemKind target in MonsterTacticsItemTypes.Components)
                {
                    bench.ClearTestInventory();
                    bench.Grant(source);
                    bench.Grant(target);
                    MonsterTacticsItemKind expected;
                    Ensure(MonsterTacticsItemTypes.TryRecipe(source, target, out expected),
                        "Missing component recipe: " + source + " + " + target);
                    // Early release never invokes TryCombineBenchItems; no items lost.
                    hold.UpdateHover(0, 1, 5f);
                    hold.Cancel();
                    Ensure(bench.Count == 2, "Release/cancel consumed an item");
                    hold.UpdateHover(0, 1, 5f);
                    // Float32: (5f + 0.85f) - 5f can equal 0.8499999f.
                    // Use a 0.01s test margin, without changing runtime hold duration.
                    Ensure(!hold.IsComplete(5f + MonsterTacticsCraftHoldGate.RequiredSeconds - .02f),
                        "Hold completed early for " + source + " + " + target);
                    Ensure(hold.IsComplete(5f + MonsterTacticsCraftHoldGate.RequiredSeconds + .01f),
                        "Hold not completed for " + source + " + " + target);
                    MonsterTacticsItemKind actual;
                    Ensure(bench.TryCombineBenchItems(0, 1, out actual) && actual == expected &&
                        bench.Count == 1 && bench.Items[0] == expected,
                        "Craft output mismatch: " + source + " + " + target);
                    hold.Cancel();
                    recipes++;
                }

                Debug.Log("[U6.7.8] PASS: hold start/cancel/reset/threshold and " + recipes +
                          " directional component recipes.");
                EditorUtility.DisplayDialog("MonsterTactics U6.7.8",
                    "PASS: hold timing, early release cancellation, target switching, and " +
                    recipes + " component combinations.", "OK");
            }
            catch (Exception e)
            {
                Debug.LogError("[U6.7.8] FAIL: " + e);
                EditorUtility.DisplayDialog("MonsterTactics U6.7.8",
                    "Validation failed: " + e.Message + "\n\nSee the Unity Console for details.", "OK");
            }
            finally { UnityEngine.Object.DestroyImmediate(temp); }
        }

        private static void Ensure(bool ok, string why)
        {
            if (!ok) throw new Exception(why);
        }
    }
}
#endif
