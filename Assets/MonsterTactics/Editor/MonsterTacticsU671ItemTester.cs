#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace MonsterTactics.Editor
{
    /// <summary>Convenient debug inventory, without requiring PvE loot tables yet.</summary>
    public sealed class MonsterTacticsU671ItemTester : EditorWindow
    {
        private MonsterTacticsItemInventory inventory;

        [MenuItem("MonsterTactics/Tests/Archive/Items/U6.7.1 Item Bench Tester")]
        public static void ShowTester() => GetWindow<MonsterTacticsU671ItemTester>("Item Bench Tester");

        private void OnGUI()
        {
            GUILayout.Label("U6.7.1 — Items & Equipping", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Enter Play Mode. Grant components, drag one from the bottom-left ITEM BENCH " +
                "onto a player Monster on the arena or hex reserve bench. Max 3 per Monster. " +
                "Selling returns items; evolving transfers them (extra items return to inventory). " +
                "Crafting and combat stat effects are scheduled for U6.7.3.",
                MessageType.Info);
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Press Play before using the tester.", MessageType.Warning);
                return;
            }
            if (inventory == null)
                inventory = FindAnyObjectByType<MonsterTacticsItemInventory>();
            if (inventory == null)
            {
                EditorGUILayout.HelpBox("Start a MonsterTactics match so the shop can create its item bench.", MessageType.Warning);
                if (GUILayout.Button("Refresh")) Repaint();
                return;
            }
            GUILayout.Label("Items in inventory: " + inventory.Count);
            if (GUILayout.Button("Grant one of each (8 standard components)"))
                inventory.GrantAllComponents();
            GUILayout.Space(6f);
            foreach (MonsterTacticsItemKind kind in MonsterTacticsItemTypes.Components)
                if (GUILayout.Button("+ " + MonsterTacticsItemTypes.Name(kind))) inventory.Grant(kind);
            GUILayout.Space(6f);
            if (GUILayout.Button("Clear test inventory")) inventory.ClearTestInventory();
            GUILayout.Space(8f);
            GUILayout.Label("Test cases", EditorStyles.boldLabel);
            GUILayout.Label("1. Equip three components to one Monster; fourth is rejected.", EditorStyles.wordWrappedLabel);
            GUILayout.Label("2. Sell that Monster; all components return to the item bench.", EditorStyles.wordWrappedLabel);
            GUILayout.Label("3. Put items on three matching copies, then combine; overflow returns.", EditorStyles.wordWrappedLabel);
            GUILayout.Label("4. Move a Monster board ↔ bench; items remain attached.", EditorStyles.wordWrappedLabel);
            if (Event.current.type == EventType.Layout) Repaint();
        }
    }
}
#endif
