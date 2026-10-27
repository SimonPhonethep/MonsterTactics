#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace MonsterTactics
{
    public static class MonsterTacticsU687Tests
    {
        private const string Menu = "MonsterTactics/Tests/Archive/Augments/U6.8.7 Verify Augment Readiness";

        [MenuItem(Menu)]
        public static void Verify()
        {
            int checks = 0;
            try
            {
                var book = new MonsterTacticsRuleBook();
                Expect(book.Get(MonsterTacticsRuleId.RerollCost) == 2, "Default reroll cost", ref checks);
                Expect(book.Get(MonsterTacticsRuleId.XPBuyCost) == 4, "Default XP cost", ref checks);
                Expect(book.Get(MonsterTacticsRuleId.XPPerPurchase) == 4, "Default XP amount", ref checks);
                Expect(book.Get(MonsterTacticsRuleId.BaseRoundIncome) == 5, "Default round income", ref checks);
                Expect(book.InterestForGold(49) == 4, "Interest at 49 gold", ref checks);
                Expect(book.InterestForGold(55) == 5 && book.GoldToNextInterest(55) == 0,
                    "Interest 5 cap", ref checks);
                Expect(book.GoldToNextInterest(49) == 1, "Interest threshold", ref checks);
                book.SetAddition("test.discount", MonsterTacticsRuleId.RerollCost, -1);
                Expect(book.Get(MonsterTacticsRuleId.RerollCost) == 1, "Reroll discount", ref checks);
                book.SetAddition("test.discount", MonsterTacticsRuleId.RerollCost, -1);
                Expect(book.Get(MonsterTacticsRuleId.RerollCost) == 1, "Idempotent modifier", ref checks);
                book.SetAddition("test.free", MonsterTacticsRuleId.RerollCost, -3);
                Expect(book.Get(MonsterTacticsRuleId.RerollCost) == 0, "Rerolls never cost negative", ref checks);
                book.RemoveSource("test.free");
                book.RemoveSource("test.discount");
                Expect(book.Get(MonsterTacticsRuleId.RerollCost) == 2, "Removal restores default", ref checks);
                book.SetAddition("test.interest", MonsterTacticsRuleId.InterestCap, 2);
                Expect(book.InterestForGold(75) == 7, "Interest cap modifier", ref checks);
                book.Clear();
                Expect(book.InterestForGold(75) == 5, "Clear restores baseline", ref checks);
                int[] normalOdds = { 50, 30, 15, 5, 0 };
                int[] baseline = book.ShopOdds(normalOdds);
                Expect(ArrayEquals(normalOdds, baseline), "Original shop odds unchanged", ref checks);
                book.SetAddition("test.odds", MonsterTacticsRuleId.ShopTier5Weight, 20);
                int[] adjusted = book.ShopOdds(normalOdds);
                int sum = 0;
                foreach (int value in adjusted) { sum += value; Expect(value >= 0, "Nonnegative odds", ref checks); }
                Expect(sum == 100 && adjusted[4] > 0, "Shop odds normalize to 100", ref checks);
                var bus = new MonsterTacticsGameplayEvents();
                int calls = 0;
                bus.Raised += evt => { if (evt.Kind == MonsterTacticsGameplayEventKind.ShopRerolled) calls++; };
                bus.Publish(new MonsterTacticsGameplayEvent(MonsterTacticsGameplayEventKind.ShopRerolled, "test", 2, "", "2-1"));
                Expect(calls == 1, "Scoped event delivery", ref checks);
                Debug.Log("[U6.8.7] PASS: " + checks + " checks, baseline economy unchanged.");
                EditorUtility.DisplayDialog("U6.8.7 Augment Readiness", "PASS: " + checks + " checks.\nRules, shop odds, and event dispatch validated.", "OK");
            }
            catch (Exception ex)
            {
                Debug.LogError("[U6.8.7] FAIL: " + ex);
                EditorUtility.DisplayDialog("U6.8.7 Augment Readiness", "FAILED: " + ex.Message + "\nSee Unity Console.", "OK");
            }
        }
        private static bool ArrayEquals(int[] a, int[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }
        private static void Expect(bool pass, string name, ref int checks)
        {
            checks++;
            if (!pass) throw new InvalidOperationException("Failed: " + name);
        }
    }

    public sealed class MonsterTacticsU687DeveloperPanel : EditorWindow
    {
        private const string Source = "U6.8.7-dev-test";
        [MenuItem("MonsterTactics/Tests/Archive/Augments/U6.8.7 Developer Rules Panel")]
        public static void Open() => GetWindow<MonsterTacticsU687DeveloperPanel>("Augment Readiness");

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("Play Mode only. Developer effects use source ID '" + Source +
                "' and do not create augments or modify balance assets.", MessageType.Info);
            if (!Application.isPlaying)
            {
                EditorGUILayout.LabelField("Enter Play Mode to test runtime rule changes.");
                return;
            }
            var shop = UnityEngine.Object.FindAnyObjectByType<MonsterTacticsU5ShopManager>();
            if (shop == null || !shop.IsShopReady)
            {
                EditorGUILayout.LabelField("No active shop manager yet.");
                return;
            }
            EditorGUILayout.LabelField("Gold", shop.Gold.ToString());
            EditorGUILayout.LabelField("Reroll cost", shop.CurrentRerollCost + "g");
            EditorGUILayout.LabelField("Free rerolls", shop.FreeRerolls.ToString());
            EditorGUILayout.LabelField("Interest", shop.InterestNextRound + "g / cap " + shop.InterestCap);
            EditorGUILayout.LabelField("Board capacity", shop.TeamLimit.ToString());
            if (GUILayout.Button("Grant 10 test gold")) shop.GrantGold(10, Source);
            if (GUILayout.Button("Grant 1 free reroll")) shop.GrantFreeRerolls(1, Source);
            if (GUILayout.Button("Apply -1 reroll cost"))
                shop.Rules.SetAddition(Source, MonsterTacticsRuleId.RerollCost, -1);
            if (GUILayout.Button("Apply +2 max interest"))
                shop.Rules.SetAddition(Source, MonsterTacticsRuleId.InterestCap, 2);
            if (GUILayout.Button("Apply +1 board capacity"))
                shop.Rules.SetAddition(Source, MonsterTacticsRuleId.TeamSizeBonus, 1);
            if (GUILayout.Button("Remove developer modifiers")) shop.Rules.RemoveSource(Source);
            Repaint();
        }
    }
}
#endif
