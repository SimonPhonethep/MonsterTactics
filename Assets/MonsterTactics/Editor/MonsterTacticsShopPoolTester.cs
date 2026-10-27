#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace MonsterTactics.Editor
{
    /// <summary>
    /// Temporary Play Mode diagnostics for U6.2.6. No scene or game-build changes.
    /// Compatible with the existing U6.2.4 finite shop pool and U6.2.6 roster.
    /// </summary>
    public sealed class MonsterTacticsShopPoolTester : EditorWindow
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private const BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic;
        private Vector2 scroll;
        private string report = "Press Play, then run a test. Changes made in Play Mode are temporary.";
        private int shopSamples = 100;

        [MenuItem("MonsterTactics/Tests/Archive/Shop and Progression/U6.2.6 Shop & Pool Tester", priority = 220)]
        private static void Open()
        {
            GetWindow<MonsterTacticsShopPoolTester>("MonsterTactics Shop Test").minSize = new Vector2(470f, 350f);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("U6.2.6 — Shop Odds & Finite Pool", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Use while the game is running. Set a test level, reroll for free, or sample many shops. The tester restores the original shop after bulk sampling. Stopping Play Mode resets the test economy.", MessageType.Info);

            using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Level 7 + 300g", GUILayout.Height(31))) SetTestLevel(7);
                if (GUILayout.Button("Level 10 + 300g", GUILayout.Height(31))) SetTestLevel(10);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("One Free Reroll", GUILayout.Height(28))) OneFreeReroll();
                if (GUILayout.Button("Check Current Pool", GUILayout.Height(28))) ValidatePool();
                EditorGUILayout.EndHorizontal();

                shopSamples = EditorGUILayout.IntSlider("Test rerolls", shopSamples, 10, 250);
                if (GUILayout.Button("Sample Shop Odds (no gold spent)", GUILayout.Height(30))) SampleRolls();
            }
            EditorGUILayout.Space(6);
            if (GUILayout.Button("Check Roster + Cost Mapping", GUILayout.Height(25))) VerifyRoster();
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Results", EditorStyles.boldLabel);
            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.ExpandHeight(true));
            EditorGUILayout.TextArea(report, EditorStyles.wordWrappedLabel, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        private static MonsterTacticsU5ShopManager FindShop()
        {
            MonsterTacticsU5ShopManager manager = UnityEngine.Object.FindAnyObjectByType<MonsterTacticsU5ShopManager>();
            if (manager == null || !manager.IsShopReady)
                throw new InvalidOperationException("Enter Play Mode in the gameplay scene and let the shop initialize first.");
            return manager;
        }

        private static FieldInfo Field(string name)
        {
            FieldInfo value = typeof(MonsterTacticsU5ShopManager).GetField(name, PrivateInstance);
            if (value == null) throw new MissingFieldException("Shop manager field not found: " + name);
            return value;
        }

        private static MethodInfo Method(string name)
        {
            MethodInfo value = typeof(MonsterTacticsU5ShopManager).GetMethod(name, PrivateInstance);
            if (value == null) throw new MissingMethodException("Shop manager method not found: " + name);
            return value;
        }

        private void Run(Action action)
        {
            try
            {
                action();
                Repaint();
            }
            catch (Exception ex)
            {
                report = "Test could not run: " + (ex.InnerException != null ? ex.InnerException.Message : ex.Message);
                Debug.LogError("MonsterTactics Shop Pool Tester: " + ex);
            }
        }

        private void SetTestLevel(int level)
        {
            Run(() =>
            {
                MonsterTacticsU5ShopManager manager = FindShop();
                Field("gold").SetValue(manager, 300);
                Field("playerLevel").SetValue(manager, level);
                Field("currentXp").SetValue(manager, 0);
                Field("teamLimit").SetValue(manager, level);
                Method("RollShop").Invoke(manager, null);
                report = "Play Mode: set Level " + level + ", 300 gold and rerolled the shop.\n" +
                    "Per-slot odds: " + OddsText(level) + "\n" +
                    "Five-cost Monster may appear only from Level 7.\n" +
                    "Current offers: " + CurrentOffers(manager);
            });
        }

        private void OneFreeReroll()
        {
            Run(() =>
            {
                MonsterTacticsU5ShopManager manager = FindShop();
                Method("RollShop").Invoke(manager, null);
                report = "Free test reroll at Level " + manager.PlayerLevel + ".\n" +
                    "Offers: " + CurrentOffers(manager) + "\n" +
                    "Live pool stock and odds were respected. No gold spent.";
            });
        }

        private static string CurrentOffers(MonsterTacticsU5ShopManager shop)
        {
            string[] names = new string[5];
            for (int i = 0; i < names.Length; i++)
            {
                MonsterTacticsU5Roster.Family family = shop.OfferForHud(i);
                names[i] = family == null ? "Empty" : family.Forms[0] + " (" + family.Cost + "g)";
            }
            return string.Join(", ", names);
        }

        private static string OddsText(int level)
        {
            int[] odds = MonsterTacticsU5Roster.TierWeights(level);
            return string.Join("  |  ", Enumerable.Range(0, odds.Length).Select(i => (i + 1) + "g " + odds[i] + "%"));
        }

        private void VerifyRoster()
        {
            Run(() =>
            {
                var all = MonsterTacticsU5Roster.All;
                var issues = new List<string>();
                var expectedLegendaries = new[] { "Voltrix", "Pyrothane", "Aqualith" };
                foreach (string legendary in expectedLegendaries)
                {
                    MonsterTacticsU5Roster.Family f = MonsterTacticsU5Roster.Find(legendary);
                    if (f == null || f.Cost != 5) issues.Add(legendary + " is not in the 5-cost tier.");
                    else if (MonsterTacticsU5Roster.HasDistinctEvolution(f)) issues.Add(legendary + " should be single-form.");
                }
                int total = 0;
                foreach (MonsterTacticsU5Roster.Family family in all)
                {
                    int copies = CopiesPerFamily(family.Cost);
                    total += copies;
                    if (copies < 1) issues.Add(family.Forms[0] + " has no valid pool size.");
                }
                for (int level = 1; level <= 10; level++)
                    if (MonsterTacticsU5Roster.TierWeights(level).Sum() != 100)
                        issues.Add("Shop probabilities do not total 100 at Level " + level + ".");

                report = "Families: " + all.Length + " | Full stock: " + total + " copies\n" +
                    "Tier mapping (per family): 1g 30, 2g 25, 3g 18, 4g 10, 5g 9\n\n" +
                    string.Join("\n", all.Select(f => f.Forms[0] + " — " + f.Cost + "g × " + CopiesPerFamily(f.Cost))) +
                    "\n\n" + (issues.Count == 0 ? "PASS: Cost tiers, legendary forms and odds totals are valid." : "ISSUES:\n" + string.Join("\n", issues));
            });
        }

        private static int CopiesPerFamily(int cost)
        {
            MethodInfo method = typeof(MonsterTacticsU5ShopManager).GetMethod("CopiesPerFamily", PrivateStatic);
            if (method == null) throw new MissingMethodException("CopiesPerFamily missing in U6.2.4+ shop manager.");
            return (int)method.Invoke(null, new object[] { cost });
        }

        private void ValidatePool()
        {
            Run(() =>
            {
                MonsterTacticsU5ShopManager manager = FindShop();
                MethodInfo availableMethod = Method("AvailableCopies");
                IEnumerable<MonsterUnit> live = (IEnumerable<MonsterUnit>)Method("LiveUnits").Invoke(manager, null);
                MonsterUnit[] owned = live.ToArray();
                var lines = new List<string>();
                bool valid = true;
                foreach (MonsterTacticsU5Roster.Family family in MonsterTacticsU5Roster.All)
                {
                    int total = CopiesPerFamily(family.Cost);
                    int available = (int)availableMethod.Invoke(manager, new object[] { family });
                    int shopCount = Enumerable.Range(0, 5).Count(i => manager.OfferForHud(i) == family);
                    int ownedCopies = 0;
                    foreach (MonsterUnit monster in owned)
                    {
                        MonsterTacticsU5Progression p = monster.GetComponent<MonsterTacticsU5Progression>();
                        if (p != null && string.Equals(p.FamilyName, family.Forms[0], StringComparison.OrdinalIgnoreCase))
                            ownedCopies += p.Stage == 1 ? 1 : p.Stage == 2 ? 3 : 9;
                    }
                    bool okay = available + shopCount + ownedCopies == total;
                    if (!okay) valid = false;
                    lines.Add((okay ? "OK" : "CHECK") + " " + family.Forms[0] + " [" + family.Cost + "g]: " +
                        available + " available + " + shopCount + " shop + " + ownedCopies + " owned = " +
                        (available + shopCount + ownedCopies) + "/" + total);
                }
                report = "Current finite-pool conservation check — " + (valid ? "PASS" : "CHECK FAILED") +
                    "\n\n" + string.Join("\n", lines) +
                    "\n\nReminder: The Evolution Test shortcut creates free copies, which can make this diagnostic show a mismatch. For a clean test, restart Play Mode and buy units normally.";
            });
        }

        private void SampleRolls()
        {
            Run(() =>
            {
                MonsterTacticsU5ShopManager manager = FindShop();
                var field = Field("shop");
                var slots = (MonsterTacticsU5Roster.Family[])field.GetValue(manager);
                var original = (MonsterTacticsU5Roster.Family[])slots.Clone();
                int[] counts = new int[5];
                int empty = 0;
                var legendary = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                {
                    {"Voltrix", 0}, {"Pyrothane", 0}, {"Aqualith", 0}
                };
                try
                {
                    MethodInfo roll = Method("RollShop");
                    for (int i = 0; i < shopSamples; i++)
                    {
                        roll.Invoke(manager, null);
                        for (int j = 0; j < slots.Length; j++)
                        {
                            MonsterTacticsU5Roster.Family f = slots[j];
                            if (f == null) empty++;
                            else
                            {
                                counts[f.Cost - 1]++;
                                if (legendary.ContainsKey(f.Forms[0])) legendary[f.Forms[0]]++;
                            }
                        }
                    }
                }
                finally
                {
                    // Restore all original offer reservations; no lasting pool or economy change.
                    Array.Copy(original, slots, slots.Length);
                }
                int count = shopSamples * slots.Length;
                report = shopSamples + " test shops, " + count + " slot samples at Level " + manager.PlayerLevel + ".\n" +
                    "Configured cost-tier odds: " + OddsText(manager.PlayerLevel) + "\n\n" +
                    string.Join("\n", Enumerable.Range(0, 5).Select(i =>
                        (i + 1) + "g: " + counts[i] + " appearances (" + (counts[i] * 100f / count).ToString("0.0") + "%)")) +
                    "\nEmpty slots: " + empty +
                    "\n\n5-cost observations: Voltrix " + legendary["Voltrix"] +
                    ", Pyrothane " + legendary["Pyrothane"] + ", Aqualith " + legendary["Aqualith"] +
                    "\n\nSamples are random; percentages will fluctuate. The original shop is restored after testing.";
            });
        }
    }
}
#endif
