#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

namespace MonsterTactics
{
    /// <summary>Focused smoke test for the choice-capsule runtime (no scene mutation outside Play Mode).</summary>
    public sealed class MonsterTacticsU693CapsuleTester : EditorWindow
    {
        [MenuItem("MonsterTactics/Tools/U6.9.5 Monster Bench Anvil Tester (Play Mode)")]
        private static void Open() => GetWindow<MonsterTacticsU693CapsuleTester>("U6.9.5 Anvils");

        private void OnGUI()
        {
            EditorGUILayout.LabelField("MONSTERTACTICS SUPPLY CAPSULES", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Anvils share the nine Monster bench hexes. Collect with Trainer, DRAG the anvil from a Monster reserve hex to the SHOP / SELL bar, release to open four choices; selected item goes into the Item Bench. If Monster bench is full the anvil stays on the board.", MessageType.Info);
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to inspect and test live item choice rewards.", MessageType.Warning);
                return;
            }
            var shop = FindAnyObjectByType<MonsterTacticsU5ShopManager>();
            var capsules = shop != null ? shop.GetComponent<MonsterTacticsItemCapsules>() : null;
            var loot = FindAnyObjectByType<MonsterTacticsLootController>();
            if (shop == null || !shop.IsShopReady || capsules == null)
            {
                EditorGUILayout.HelpBox("Start the normal MonsterTactics arena. The shop attaches the capsule controller automatically.", MessageType.Warning);
                return;
            }
            EditorGUILayout.LabelField("Anvils occupying Monster Bench", capsules.BenchAnvilCount.ToString());
            EditorGUILayout.LabelField("Free Monster Bench slots", shop.EmptyBenchSlots.ToString());
            if (capsules.CurrentKind.HasValue)
            {
                EditorGUILayout.LabelField("Offer", MonsterTacticsItemCapsules.DisplayName(capsules.CurrentKind.Value));
                var names = new List<string>();
                var seen = new HashSet<MonsterTacticsItemKind>();
                foreach (var item in capsules.CurrentChoices)
                {
                    names.Add(MonsterTacticsItemTypes.Name(item));
                    if (!seen.Add(item)) Debug.LogError("[U6.9.3] Duplicate capsule choice: " + item);
                    if (!MonsterTacticsItemTypes.IsAvailable(item)) Debug.LogError("[U6.9.3] Invalid capsule choice: " + item);
                }
                EditorGUILayout.LabelField("Four unique options", seen.Count == 4 ? "PASS" : "FAIL");
                EditorGUILayout.HelpBox(string.Join("\n", names.ToArray()), MessageType.None);
                if (GUILayout.Button("Show pending choice panel")) capsules.Reopen();
            }
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Grant anvils to Monster Bench", EditorStyles.boldLabel);
            if (GUILayout.Button("Add Crafting Part Anvil")) capsules.GrantCapsule(MonsterTacticsItemCapsuleKind.Parts, "editor-test");
            if (GUILayout.Button("Add Gear Anvil")) capsules.GrantCapsule(MonsterTacticsItemCapsuleKind.Gear, "editor-test");
            EditorGUILayout.Space(10);
            if (loot != null)
            {
                EditorGUILayout.LabelField("Physical board pickups", EditorStyles.boldLabel);
                if (GUILayout.Button("Drop Crafting Part Capsule on arena")) loot.DebugSpawnCapsule(MonsterTacticsItemCapsuleKind.Parts);
                if (GUILayout.Button("Drop Gear Capsule on arena")) loot.DebugSpawnCapsule(MonsterTacticsItemCapsuleKind.Gear);
                if (GUILayout.Button("Collect all waiting loot balls (debug)")) loot.DebugCollectAll();
                var part = loot.PlanFor(4,7);
                var gear5 = loot.PlanFor(5,7);
                var gear6 = loot.PlanFor(6,7);
                bool correct = Has(part, MonsterTacticsItemCapsuleKind.Parts) &&
                               Has(gear5, MonsterTacticsItemCapsuleKind.Gear) &&
                               Has(gear6, MonsterTacticsItemCapsuleKind.Gear);
                EditorGUILayout.LabelField("Scheduled 4-7 / 5-7 / 6-7", correct ? "PASS" : "FAIL");
                if (!correct) EditorGUILayout.HelpBox("One or more scheduled capsule drops are missing.", MessageType.Error);
            }
        }

        private static bool Has(List<MonsterTacticsLootController.Award> list, MonsterTacticsItemCapsuleKind kind)
        {
            foreach (var award in list)
                if (award.IsCapsule && award.CapsuleKind == kind) return true;
            return false;
        }
    }
}
#endif
