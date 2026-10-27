#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MonsterTactics
{
    /// <summary>U6.9.1 self-tests and manual preview, with no serialized scene edits.</summary>
    public static class MonsterTacticsU691Tests
    {
        [MenuItem("MonsterTactics/Tests/U6.9.1 Verify Augment UI Rules")]
        public static void Verify()
        {
            int passed = 0;
            var failures = new List<string>();
            Action<bool, string> check = (ok, label) =>
            {
                if (ok) passed++;
                else failures.Add(label);
            };
            var clock = new MonsterTacticsAugmentSelectionClock();
            clock.Begin(3, 1, 43f);
            check(clock.IsOpen && clock.TotalRerollsRemaining == 3, "3 individual rerolls");
            check(clock.SpendReroll(1) && clock.RemainingForCard(1) == 0,
                "middle card consumes only its own reroll");
            check(clock.RemainingForCard(0) == 1 && clock.RemainingForCard(2) == 1,
                "other two rerolls unaffected");
            check(!clock.Advance(42.5f) && clock.IsOpen,
                "timer stays open before deadline");
            check(clock.Advance(0.5f) && clock.SecondsRemaining == 0f,
                "timeout reached at deadline");
            int a = MonsterTacticsAugmentSelectionClock.ChooseVisibleIndex(3, 8128);
            int b = MonsterTacticsAugmentSelectionClock.ChooseVisibleIndex(3, 8128);
            check(a == b && a >= 0 && a < 3, "timeout picks one of current three deterministically");
            check(clock.Finish() && !clock.Finish(), "selection resolves only once");
            check(MonsterTacticsAugmentManager.SlotForStage("2-1") == MonsterTacticsAugmentSlots.First &&
                MonsterTacticsAugmentManager.SlotForStage("3-2") == MonsterTacticsAugmentSlots.Second &&
                MonsterTacticsAugmentManager.SlotForStage("4-2") == MonsterTacticsAugmentSlots.Final,
                "selection stages map correctly");
            check(MonsterTacticsAugmentManager.SlotForStage("1-4") == MonsterTacticsAugmentSlots.None,
                "other rounds don't open augments");
            var catalog = AssetDatabase.LoadAssetAtPath<MonsterTacticsAugmentCatalog>(
                "Assets/MonsterTactics/Resources/Augments/MonsterTacticsAugmentCatalog.asset");
            check(catalog != null, "catalog exists");
            if (catalog != null)
            {
                var defs = new List<MonsterTacticsAugmentSnapshot>();
                foreach (var d in catalog.definitions) if (d != null) defs.Add(new MonsterTacticsAugmentSnapshot(d));
                foreach (MonsterTacticsAugmentTier tier in Enum.GetValues(typeof(MonsterTacticsAugmentTier)))
                {
                    var offers = MonsterTacticsAugmentOffers.Roll(defs,MonsterTacticsAugmentSlots.First,tier,
                        new MonsterTacticsAugmentOfferContext { PlayerLevel = 1, Gold = 10 },3,
                        new System.Random(777));
                    check(offers.Count == 3, tier + " has 3 elidunebit opening cards");
                }
            }
            var manager = UnityEngine.Object.FindAnyObjectByType<MonsterTacticsAugmentManager>();
            check(manager != null, "scene contains AugmentManager");
            if (failures.Count == 0)
            {
                Debug.Log("[U6.9.1] PASS: " + passed + " checks. Run a Play Mode selection to test the UI.");
                EditorUtility.DisplayDialog("MonsterTactics U6.9.1", "PASS: " + passed + " framework/UI rule checks.\nNow test manually in Play Mode.", "OK");
            }
            else
            {
                Debug.LogError("[U6.9.1] FAIL (" + failures.Count + "): " + string.Join(" | ", failures));
                EditorUtility.DisplayDialog("U6.9.1 Validation", "FAILED:\n" + string.Join("\n", failures), "OK");
            }
        }

        [MenuItem("MonsterTactics/Tests/Archive/Augments/U6.9.1a Verify Repeatable Preview (Play Mode)")]
        public static void VerifyRepeatablePreview()
        {
            if (!Application.isPlaying)
            { EditorUtility.DisplayDialog("U6.9.1a", "Enter Play Mode first.", "OK"); return; }
            var manager = UnityEngine.Object.FindAnyObjectByType<MonsterTacticsAugmentManager>();
            var shop = UnityEngine.Object.FindAnyObjectByType<MonsterTacticsU5ShopManager>();
            if (manager == null || !manager.IsActive || shop == null || manager.IsSelectionOpen)
            {
                EditorUtility.DisplayDialog("U6.9.1a", "An active manager and shop are required, with no selection currently open.", "OK");
                return;
            }
            var slot = MonsterTacticsAugmentSlots.First;
            int selectedBefore = manager.Selected.Count;
            int goldBefore = shop.Gold;
            bool resolvedBefore = manager.HasResolved(slot);
            string failed = null;
            for (int i = 0; i < 2; i++)
            {
                if (!manager.OpenSandboxPreview(slot, manager.TierForSlot(slot)))
                { failed = "Could not open sandbox: " + manager.LastNotice; break; }
                if (!manager.IsSandboxPreview || manager.Offered.Count != 3 ||
                    !manager.ChooseVisible(0) || manager.IsSelectionOpen)
                { failed = "Sandbox selection failed on attempt " + (i + 1); break; }
                if (manager.Selected.Count != selectedBefore || shop.Gold != goldBefore ||
                    manager.HasResolved(slot) != resolvedBefore)
                { failed = "Sandbox changed a real match value on attempt " + (i + 1); break; }
            }
            if (manager.IsSandboxPreview) manager.CloseSandboxPreview();
            if (failed == null)
            {
                Debug.Log("[U6.9.1a] PASS: repeated UI previews did not grant rewards or consume slots.");
                EditorUtility.DisplayDialog("U6.9.1a", "PASS: Both UI previews completed. Selected augments, gold and First-slot state stayed unchanged.", "OK");
            }
            else
            {
                Debug.LogError("[U6.9.1a] FAIL: " + failed);
                EditorUtility.DisplayDialog("U6.9.1a", "FAILED: " + failed, "OK");
            }
        }

        [MenuItem("MonsterTactics/Tests/Archive/Augments/U6.9.1 Preview Real Augment Reward (Consumes Slot)")]
        public static void PreviewReward()
        {
            if (!Application.isPlaying)
            { EditorUtility.DisplayDialog("U6.9.1", "Enter Play Mode first.", "OK"); return; }
            string wiring;
            var manager = MonsterTacticsAugmentRuntimeBootstrap.EnsureActiveManager(out wiring);
            if (manager == null || !manager.IsActive)
            { EditorUtility.DisplayDialog("U6.9.1", "Augment manager unavailable: " + wiring, "OK"); return; }
            if (manager.IsSelectionOpen) return;

            // Real reward tests can only consume each selection slot once.
            var round = UnityEngine.Object.FindAnyObjectByType<MonsterTacticsU62RoundManager>();
            var preferred = round != null ?
                MonsterTacticsAugmentManager.SlotForStage(round.StageRound) : MonsterTacticsAugmentSlots.None;
            var slots = new[] { preferred, MonsterTacticsAugmentSlots.First,
                MonsterTacticsAugmentSlots.Second, MonsterTacticsAugmentSlots.Final };
            foreach (var slot in slots)
            {
                if (slot == MonsterTacticsAugmentSlots.None || manager.HasResolved(slot)) continue;
                if (manager.OpenOffer(slot, manager.TierForSlot(slot), false, true))
                {
                    Debug.LogWarning("[U6.9.1a] REAL reward preview for " + slot +
                        ". This choice changes your current match and consumes that augment slot.");
                    return;
                }
                EditorUtility.DisplayDialog("U6.9.1", "Could not open reward preview: " + manager.LastNotice, "OK");
                return;
            }
            EditorUtility.DisplayDialog("U6.9.1", "All three real augment slots are already used. Restart Play Mode for a fresh match, or use the repeatable UI-only preview.", "OK");
        }

        [MenuItem("MonsterTactics/Tests/Archive/Augments/U6.9.1 Preview Augment Choice (Play Mode)")]
        public static void Preview()
        {
            if (!Application.isPlaying)
            { EditorUtility.DisplayDialog("U6.9.1", "Enter Play Mode first.", "OK"); return; }
            string wiring;
            var manager = MonsterTacticsAugmentRuntimeBootstrap.EnsureActiveManager(out wiring);
            if (manager == null || !manager.IsActive)
            {
                EditorUtility.DisplayDialog("U6.9.1", "Augment manager unavailable: " + wiring, "OK");
                return;
            }
            if (manager.IsSelectionOpen) return;
            // Sandbox mode: repeatable even if the player has already chosen
            // an augment for First/Second/Final. No gold/items/rules are changed.
            var round = UnityEngine.Object.FindAnyObjectByType<MonsterTacticsU62RoundManager>();
            var slot = round != null ? MonsterTacticsAugmentManager.SlotForStage(round.StageRound) : MonsterTacticsAugmentSlots.None;
            if (slot == MonsterTacticsAugmentSlots.None) slot = MonsterTacticsAugmentSlots.First;
            if (!manager.OpenSandboxPreview(slot, manager.TierForSlot(slot)))
                EditorUtility.DisplayDialog("U6.9.1", "Could not open UI-only preview: " + manager.LastNotice, "OK");
        }
    }
}
#endif
