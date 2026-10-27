#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
namespace MonsterTactics.Editor
{
    public sealed class MonsterTacticsU673CraftingTester : EditorWindow
    {
        private MonsterTacticsItemInventory inventory;
        private int standardIndex;
        private Vector2 listScroll;
        private bool showAll;
        [MenuItem("MonsterTactics/Tests/Archive/Items/U6.7.3 Item Crafting and Tools")]
        public static void Open() => GetWindow<MonsterTacticsU673CraftingTester>("U6.7.3 Items");
        private void OnGUI()
        {
            GUILayout.Label("U6.7.3 — 36 Standard Recipes, Remover & Reforger", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Drag two components onto the same owned Monster to combine them. " +
                "Drag Remover or Reforger onto an item-holding Monster to return or reroll its items. " +
                "Tools cannot be used on deployed Monster during combat.", MessageType.Info);
            if(!Application.isPlaying){EditorGUILayout.HelpBox("Enter Play Mode.", MessageType.Warning);return;}
            if(inventory==null)inventory=FindAnyObjectByType<MonsterTacticsItemInventory>();
            if(inventory==null){EditorGUILayout.HelpBox("Wait for the item bench to spawn.",MessageType.Warning);return;}
            GUILayout.Label("Items on bench: " + inventory.Count);
            if(GUILayout.Button("Grant all 8 components"))inventory.GrantAllComponents();
            if(GUILayout.Button("Grant Remover + Reforger"))inventory.GrantTools();
            GUILayout.Space(4);
            foreach(MonsterTacticsItemKind item in MonsterTacticsItemTypes.CraftingComponents)
                if(GUILayout.Button("+ " + MonsterTacticsItemTypes.Name(item)))inventory.Grant(item);
            GUILayout.Space(8);
            standardIndex=EditorGUILayout.IntSlider("Standard",standardIndex,0,35);
            if(GUILayout.Button("Grant crafted " + MonsterTacticsItemTypes.Name(MonsterTacticsItemTypes.Completed[standardIndex])))
                inventory.Grant(MonsterTacticsItemTypes.Completed[standardIndex]);
            GUILayout.Space(8);
            showAll = EditorGUILayout.Foldout(showAll, "Review all 36 completed item passives", true);
            if (showAll)
            {
                listScroll = EditorGUILayout.BeginScrollView(listScroll, GUILayout.Height(270));
                foreach (MonsterTacticsItemKind item in MonsterTacticsItemTypes.Completed)
                {
                    EditorGUILayout.LabelField(MonsterTacticsItemTypes.Name(item), EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(MonsterTacticsItemTypes.BonusSummary(MonsterTacticsItemTypes.Bonus(item)),
                        EditorStyles.miniLabel);
                    EditorGUILayout.LabelField(MonsterTacticsItemPassiveCatalog.Effect(item), EditorStyles.wordWrappedMiniLabel);
                    GUILayout.Space(5);
                }
                EditorGUILayout.EndScrollView();
            }
            GUILayout.Label("Test: Fang + Seed crafts Life Drinker. Remover returns it unchanged.\n"+
                "Reforger returns a different regular completed item. Selling returns held items.",EditorStyles.wordWrappedLabel);
            if(Event.current.type==EventType.Layout)Repaint();
        }
    }
}
#endif
