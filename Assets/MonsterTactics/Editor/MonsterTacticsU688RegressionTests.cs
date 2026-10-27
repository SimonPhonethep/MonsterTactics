#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MonsterTactics
{
    /// <summary>
    /// U6.8.8 non-destructive regression tests before enabling augments.
    /// The Edit Mode suite creates only in-memory rules and a temporary hidden
    /// GameObject. The Play Mode probe changes *rule modifiers only* and restores
    /// them in finally; it never buys, sells, rolls, or grants anything.
    /// </summary>
    public static class MonsterTacticsU688RegressionTests
    {
        private const string Root = "MonsterTactics/Tests/";
        private const string ProbeSource = "__U688_TEMP_RULE_PROBE__";
        private static int checks;

        [MenuItem(Root + "U6.8.8 Run Regression Suite")]
        public static void RunEditModeSuite()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("U6.8.8 Regression Suite",
                    "Exit Play Mode to run the isolated regression suite.", "OK");
                return;
            }

            checks = 0;
            try
            {
                CheckDefaultRules();
                CheckModifiersAndReversibility();
                CheckInterest();
                CheckShopOdds();
                CheckGameplayEvents();
                CheckItemCatalog();
                CheckEquipmentSlots();
                Debug.Log("[U6.8.8] PASS: " + checks +
                    " assertions. Rulebook, offers, item recipes and slots remain consistent.");
                EditorUtility.DisplayDialog("U6.8.8 Regression Suite",
                    "PASS: " + checks + " checks.\n\n" +
                    "Now enter Play Mode and run:\n" +
                    "MonsterTactics > Tests > U6.8.8 Verify Live Shop Bindings", "OK");
            }
            catch (Exception ex)
            {
                Debug.LogError("[U6.8.8] FAIL after " + checks + " assertions: " + ex);
                EditorUtility.DisplayDialog("U6.8.8 Regression Suite",
                    "FAILED: " + ex.Message + "\nSee the first [U6.8.8] error in Console.", "OK");
            }
        }

        [MenuItem("MonsterTactics/Tests/Archive/Shop and Progression/U6.8.8 Verify Live Shop Bindings")]
        public static void VerifyLiveShop()
        {
            if (!Application.isPlaying)
            {
                EditorUtility.DisplayDialog("U6.8.8 Live Shop Bindings",
                    "Enter Play Mode first. This probe does not spend gold or change shop units.", "OK");
                return;
            }
            var shop = UnityEngine.Object.FindAnyObjectByType<MonsterTacticsU5ShopManager>();
            if (shop == null || !shop.IsShopReady)
            {
                EditorUtility.DisplayDialog("U6.8.8 Live Shop Bindings",
                    "Shop manager is not initialized yet. Start a match, then retry.", "OK");
                return;
            }

            checks = 0;
            int startingGold = shop.Gold;
            int startingFree = shop.FreeRerolls;
            int startingTeamLimit = shop.TeamLimit;
            int startingLevel = shop.PlayerLevel;
            int startingXP = shop.CurrentXP;
            int startingReroll = shop.CurrentRerollCost;
            int[] startingOdds = shop.ShopOddsForHud;
            int startingInterest = shop.InterestNextRound;
            int startingXPCost = shop.BuyXPCost;
            var rules = shop.Rules;
            try
            {
                Check(shop.CurrentRerollCost == (shop.FreeRerolls > 0 ? 0 : rules.Get(MonsterTacticsRuleId.RerollCost)),
                    "Live reroll cost uses rulebook (and free charges)");
                Check(shop.InterestNextRound == rules.InterestForGold(shop.Gold),
                    "Live interest uses rulebook");
                Check(shop.InterestCap == rules.Get(MonsterTacticsRuleId.InterestCap),
                    "Live interest cap uses rulebook");
                Check(shop.BuyXPCost == rules.Get(MonsterTacticsRuleId.XPBuyCost),
                    "Live XP cost uses rulebook");
                Check(shop.XPPerPurchase == rules.Get(MonsterTacticsRuleId.XPPerPurchase),
                    "Live XP amount uses rulebook");
                Check(ArraysEqual(shop.ShopOddsForHud,
                    rules.ShopOdds(MonsterTacticsU5Roster.TierWeights(shop.PlayerLevel))),
                    "Live HUD shop odds match calculated rules");

                // These temporary modifiers are removed even if a test fails.
                // Never grant free rerolls here: that would change match state.
                rules.SetAddition(ProbeSource, MonsterTacticsRuleId.RerollCost, -1);
                Check(shop.CurrentRerollCost == (shop.FreeRerolls > 0 ? 0 : rules.Get(MonsterTacticsRuleId.RerollCost)),
                    "Live reroll discount reflected in shop");
                rules.SetAddition(ProbeSource, MonsterTacticsRuleId.InterestCap, 2);
                Check(shop.InterestCap == rules.Get(MonsterTacticsRuleId.InterestCap),
                    "Live interest cap modifier reflected in shop");
                rules.SetAddition(ProbeSource, MonsterTacticsRuleId.TeamSizeBonus, 1);
                Check(shop.TeamLimit == Mathf.Clamp(startingTeamLimit + 1, 1, 12),
                    "Live team capacity modifier reflected in shop");
                rules.SetAddition(ProbeSource, MonsterTacticsRuleId.ShopTier5Weight, 25);
                Check(ArraysEqual(shop.ShopOddsForHud,
                    rules.ShopOdds(MonsterTacticsU5Roster.TierWeights(shop.PlayerLevel))),
                    "Live shop odds change consistently");
                Check(shop.ShopOddsForHud[4] >= startingOdds[4],
                    "Tier-5 weight can be increased by a rule source");
            }
            catch (Exception ex)
            {
                Debug.LogError("[U6.8.8] LIVE FAIL after " + checks + " assertions: " + ex);
                EditorUtility.DisplayDialog("U6.8.8 Live Shop Bindings",
                    "FAILED: " + ex.Message + "\nTemporary modifiers will be removed.", "OK");
                return;
            }
            finally
            {
                rules.RemoveSource(ProbeSource);
                // Warn if anything changed in gameplay beyond temporary rules.
                if (shop.Gold != startingGold || shop.FreeRerolls != startingFree ||
                    shop.PlayerLevel != startingLevel || shop.CurrentXP != startingXP)
                    Debug.LogWarning("[U6.8.8] Match state changed during probe; inspect other running systems.");
            }

            try
            {
                Check(shop.CurrentRerollCost == startingReroll, "Reroll cost restored");
                Check(shop.TeamLimit == startingTeamLimit, "Capacity restored");
                Check(shop.InterestNextRound == startingInterest, "Interest restored");
                Check(shop.BuyXPCost == startingXPCost, "XP purchase restored");
                Check(ArraysEqual(shop.ShopOddsForHud, startingOdds), "Shop odds restored");
                Check(shop.Gold == startingGold, "Gold not spent by tests");
                Check(shop.FreeRerolls == startingFree, "Free-reroll charges not consumed");
                Debug.Log("[U6.8.8] LIVE PASS: " + checks +
                    " assertions. Test modifiers removed; gold/XP/shop rolls untouched.");
                EditorUtility.DisplayDialog("U6.8.8 Live Shop Bindings",
                    "PASS: " + checks + " checks.\nTemporary rule changes were removed.\n" +
                    "Now manually test combat items, round income and bench evolution.", "OK");
            }
            catch (Exception ex)
            {
                Debug.LogError("[U6.8.8] LIVE RESTORE FAIL: " + ex);
                EditorUtility.DisplayDialog("U6.8.8 Live Shop Bindings",
                    "RESTORE CHECK FAILED: " + ex.Message + "\nSee Console.", "OK");
            }
        }

        private static void CheckDefaultRules()
        {
            var rules = new MonsterTacticsRuleBook();
            Check(rules.Get(MonsterTacticsRuleId.RerollCost) == 2, "Default reroll = 2g");
            Check(rules.Get(MonsterTacticsRuleId.XPBuyCost) == 4, "Default XP purchase = 4g");
            Check(rules.Get(MonsterTacticsRuleId.XPPerPurchase) == 4, "Default XP amount = 4");
            Check(rules.Get(MonsterTacticsRuleId.BaseRoundIncome) == 5, "Default income = 5g");
            Check(rules.Get(MonsterTacticsRuleId.InterestCap) == 5, "Interest cap = 5g");
            Check(rules.Get(MonsterTacticsRuleId.InterestGoldStep) == 10, "Interest every 10g");
            Check(rules.Get(MonsterTacticsRuleId.TeamSizeBonus) == 0, "No baseline capacity bonus");
            Check(rules.Get(MonsterTacticsRuleId.PvpWinGold) == 1, "PvP bonus = 1g");
        }

        private static void CheckModifiersAndReversibility()
        {
            var r = new MonsterTacticsRuleBook();
            int changed = 0;
            r.Changed += () => changed++;
            r.SetAddition("a", MonsterTacticsRuleId.RerollCost, -1);
            Check(r.Get(MonsterTacticsRuleId.RerollCost) == 1, "Single reroll discount");
            Check(changed == 1, "Modifier emits Changed");
            r.SetAddition("a", MonsterTacticsRuleId.RerollCost, -1);
            Check(changed == 1, "Identical modifier is idempotent");
            r.SetAddition("b", MonsterTacticsRuleId.RerollCost, -1);
            Check(r.Get(MonsterTacticsRuleId.RerollCost) == 0, "Independent sources stack");
            r.SetAddition("c", MonsterTacticsRuleId.RerollCost, -20);
            Check(r.Get(MonsterTacticsRuleId.RerollCost) == 0, "Reroll never negative");
            r.RemoveSource("c"); r.RemoveSource("b");
            Check(r.Get(MonsterTacticsRuleId.RerollCost) == 1, "Removing sources preserves other source");
            r.RemoveSource("a");
            Check(r.Get(MonsterTacticsRuleId.RerollCost) == 2, "Removing all sources restores baseline");
            r.SetAddition("economy", MonsterTacticsRuleId.XPPerPurchase, -10);
            Check(r.Get(MonsterTacticsRuleId.XPPerPurchase) == 1, "XP reward floor >= 1");
            r.SetAddition("economy", MonsterTacticsRuleId.InterestGoldStep, -100);
            Check(r.Get(MonsterTacticsRuleId.InterestGoldStep) == 1, "Interest step floor >= 1");
            r.RemoveSource("economy");
            Check(r.Get(MonsterTacticsRuleId.InterestGoldStep) == 10, "Source cleanup across rules");
            r.SetAddition("capacity", MonsterTacticsRuleId.TeamSizeBonus, 99);
            Check(r.Get(MonsterTacticsRuleId.TeamSizeBonus) <= 999, "Rule numeric safety bound");
            r.Clear();
            Check(r.Get(MonsterTacticsRuleId.TeamSizeBonus) == 0, "Clear resets all");
            Check(changed > 3, "Changed notifications observed");
        }

        private static void CheckInterest()
        {
            var r = new MonsterTacticsRuleBook();
            for (int gold = 0; gold <= 100; gold++)
            {
                int expected = Mathf.Min(5, gold / 10);
                Check(r.InterestForGold(gold) == expected, "Interest at " + gold + "g");
                int toNext = expected == 5 ? 0 : 10 - gold % 10;
                Check(r.GoldToNextInterest(gold) == toNext, "Next-interest threshold at " + gold + "g");
            }
            r.SetAddition("extra-cap", MonsterTacticsRuleId.InterestCap, 2);
            Check(r.InterestForGold(69) == 6, "Extra interest at 69g");
            Check(r.InterestForGold(70) == 7, "New interest cap at 70g");
            Check(r.GoldToNextInterest(70) == 0, "New interest threshold stops at cap");
        }

        private static void CheckShopOdds()
        {
            var r = new MonsterTacticsRuleBook();
            for (int level = 1; level <= 10; level++)
            {
                var baseline = MonsterTacticsU5Roster.TierWeights(level);
                int[] untouched = r.ShopOdds(baseline);
                Check(ArraysEqual(baseline, untouched), "Baseline shop odds at level " + level);
                Check(Sum(untouched) == 100, "Baseline odds total 100 at level " + level);
                r.SetAddition("tier5", MonsterTacticsRuleId.ShopTier5Weight, 15);
                int[] adjusted = r.ShopOdds(baseline);
                Check(Sum(adjusted) == 100, "Modified shop odds total 100 at level " + level);
                for (int i = 0; i < 5; i++)
                    Check(adjusted[i] >= 0, "Nonnegative odds for level " + level + " tier " + (i + 1));
                r.RemoveSource("tier5");
            }
            int[] degenerate = { 0, 0, 0, 0, 100 };
            r.SetAddition("disable", MonsterTacticsRuleId.ShopTier5Weight, -100);
            Check(Sum(r.ShopOdds(degenerate)) == 100, "Empty modified odds fallback to baseline");
        }

        private static void CheckGameplayEvents()
        {
            var bus = new MonsterTacticsGameplayEvents();
            int received = 0;
            Action<MonsterTacticsGameplayEvent> listener = evt =>
            {
                Check(evt.StageRound == "3-2", "Event stage round payload");
                Check(evt.Amount == 4, "Event amount payload");
                if (evt.Kind == MonsterTacticsGameplayEventKind.XPGranted) received++;
            };
            bus.Raised += listener;
            bus.Publish(new MonsterTacticsGameplayEvent(MonsterTacticsGameplayEventKind.XPGranted, "regression", 4, "", "3-2"));
            Check(received == 1, "Event delivered once");
            bus.Raised -= listener;
            bus.Publish(new MonsterTacticsGameplayEvent(MonsterTacticsGameplayEventKind.XPGranted, "regression", 4, "", "3-2"));
            Check(received == 1, "Unsubscribed event not delivered");
        }

        private static void CheckItemCatalog()
        {
            var components = MonsterTacticsItemTypes.Components;
            var crafted = MonsterTacticsItemTypes.Completed;
            Check(components.Length == 8, "Eight crafting components");
            Check(crafted.Length == 36, "36 completed recipes");
            Check(new HashSet<MonsterTacticsItemKind>(components).Count == 8, "Components have unique IDs");
            var recipes = new HashSet<MonsterTacticsItemKind>();
            for (int i = 0; i < components.Length; i++)
            {
                Check(MonsterTacticsItemTypes.IsAvailable(components[i]), "Component available " + i);
                for (int j = i; j < components.Length; j++)
                {
                    MonsterTacticsItemKind a;
                    MonsterTacticsItemKind b;
                    Check(MonsterTacticsItemTypes.TryRecipe(components[i], components[j], out a),
                        "Craft recipe exists " + i + "+" + j);
                    Check(MonsterTacticsItemTypes.TryRecipe(components[j], components[i], out b) && a == b,
                        "Craft recipe symmetric " + i + "+" + j);
                    Check(MonsterTacticsItemTypes.IsCompleted(a), "Craft result completed " + i + "+" + j);
                    Check(recipes.Add(a), "Recipe result unique " + i + "+" + j);
                    Check(!string.IsNullOrWhiteSpace(MonsterTacticsItemTypes.Name(a)), "Craft result named " + i + "+" + j);
                }
            }
            Check(recipes.Count == 36, "Exactly 36 unique combinations");
            foreach (var kind in crafted)
                Check(recipes.Contains(kind), "Catalog completed item is craftable: " + kind);
            Check(!MonsterTacticsItemTypes.IsAvailable(MonsterTacticsItemKind.TrainerCrest), "Special crest still disabled");
            Check(!MonsterTacticsItemTypes.IsAvailable(MonsterTacticsItemKind.TacticsPan), "Special pan still disabled");
            Check(!MonsterTacticsItemTypes.IsAvailable(MonsterTacticsItemKind.Special00), "Special recipes not enabled yet");
            Check(MonsterTacticsItemTypes.IsAvailable(MonsterTacticsItemKind.ItemRemover), "Item Remover enabled");
            Check(MonsterTacticsItemTypes.IsAvailable(MonsterTacticsItemKind.Reforger), "Reforger enabled");
            MonsterTacticsItemKind invalid;
            Check(!MonsterTacticsItemTypes.TryRecipe(MonsterTacticsItemKind.ItemRemover,
                MonsterTacticsItemKind.VitalSeed, out invalid), "Consumables cannot craft");
        }

        private static void CheckEquipmentSlots()
        {
            GameObject temporary = new GameObject("__U688_ItemSlots_Regression__");
            temporary.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                var held = temporary.AddComponent<MonsterTacticsHeldItems>();
                Check(held.TryAdd(MonsterTacticsItemTypes.Completed[0]), "Equip first completed item");
                Check(held.TryAdd(MonsterTacticsItemTypes.Completed[1]), "Equip second completed item");
                Check(held.TryAdd(MonsterTacticsItemTypes.Completed[2]), "Equip third completed item");
                Check(held.Items.Count == 3 && held.IsFull, "Three-item equipment cap");
                Check(!held.TryAdd(MonsterTacticsItemTypes.Completed[3]), "Fourth completed item rejected");
                Check(!held.TryAdd(MonsterTacticsItemKind.ItemRemover), "Consumable cannot equip");
                Check(held.TakeAll().Length == 3 && held.Items.Count == 0, "Removing items returns all three");
                Check(held.TryAdd(MonsterTacticsItemKind.AttackFang), "Equip a component");
                Check(held.TryAdd(MonsterTacticsItemKind.AttackFang), "Combine two equipped components");
                Check(held.Items.Count == 1 && MonsterTacticsItemTypes.IsCompleted(held.Items[0]),
                    "Component combination occupies one slot");
            }
            finally
            {
                if (temporary != null) UnityEngine.Object.DestroyImmediate(temporary);
            }
        }

        private static bool ArraysEqual(int[] left, int[] right)
        {
            if (left == null || right == null || left.Length != right.Length) return false;
            for (int i = 0; i < left.Length; i++) if (left[i] != right[i]) return false;
            return true;
        }
        private static int Sum(int[] values)
        {
            int total = 0;
            foreach (int value in values) total += value;
            return total;
        }
        private static void Check(bool condition, string message)
        {
            checks++;
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
#endif
