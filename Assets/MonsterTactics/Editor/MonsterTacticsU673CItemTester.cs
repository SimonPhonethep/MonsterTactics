#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MonsterTactics.Editor
{
    /// <summary>U6.7.3c hands-on QA for recipes, per-unit items, and real combat debuffs.</summary>
    public sealed class MonsterTacticsU673CItemTester : EditorWindow
    {
        private MonsterTacticsItemInventory inventory;
        private MonsterUnit chosen;
        private int index;
        private bool showCatalog = true;
        private Vector2 scroll;
        private string report = "";

        [MenuItem("MonsterTactics/Tests/Archive/Items/U6.7.3d Balanced Item QA")]
        public static void Open() => GetWindow<MonsterTacticsU673CItemTester>("Balanced Item QA");

        private void OnGUI()
        {
            GUILayout.Label("U6.7.3d - 36 Standard Item Test Lab", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("All 36 items have unique combat passives. " +
                "Two sources for each Sunder/Shred/Wound; effects refresh, never stack. Special recipes remain disabled. Test inside Play Mode.", MessageType.Info);
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode before granting items.", MessageType.Warning);
                return;
            }

            if (inventory == null) inventory = FindAnyObjectByType<MonsterTacticsItemInventory>();
            if (inventory == null)
            {
                EditorGUILayout.HelpBox("Waiting for shop/item bench initialization.", MessageType.Warning);
                return;
            }

            EditorGUILayout.LabelField("Item bench", inventory.Count + " items");
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Grant 8 components")) inventory.GrantAllComponents();
            if (GUILayout.Button("Grant Remover + Reforger")) inventory.GrantTools();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("+ Fang + Plate (Sunder)")) { inventory.Grant(MonsterTacticsItemKind.AttackFang); inventory.Grant(MonsterTacticsItemKind.IronPlate); }
            if (GUILayout.Button("+ Spoon + Cloak (Shred)")) { inventory.Grant(MonsterTacticsItemKind.MysticSpoon); inventory.Grant(MonsterTacticsItemKind.ResistCloak); }
            EditorGUILayout.EndHorizontal();
            if (GUILayout.Button("+ Plate + Seed (Tank Wound)")) { inventory.Grant(MonsterTacticsItemKind.IronPlate); inventory.Grant(MonsterTacticsItemKind.VitalSeed); }
            GUILayout.Label("Two sources per debuff: offensive hit or frontline aura", EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Grant Sunder pair")) { inventory.Grant(MonsterTacticsItemKind.Craft03); inventory.Grant(MonsterTacticsItemKind.Craft25); }
            if (GUILayout.Button("Grant Shred pair")) { inventory.Grant(MonsterTacticsItemKind.Craft13); inventory.Grant(MonsterTacticsItemKind.Craft33); }
            if (GUILayout.Button("Grant Wound pair")) { inventory.Grant(MonsterTacticsItemKind.Craft11); inventory.Grant(MonsterTacticsItemKind.Craft22); }
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(5);
            index = EditorGUILayout.IntSlider("Crafted item", index, 0, 35);
            MonsterTacticsItemKind current = MonsterTacticsItemTypes.Completed[index];
            GUILayout.Label(MonsterTacticsItemTypes.Name(current), EditorStyles.boldLabel);
            EditorGUILayout.LabelField(MonsterTacticsItemTypes.BonusSummary(MonsterTacticsItemTypes.Bonus(current)), EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField(MonsterTacticsItemPassiveCatalog.Effect(current), EditorStyles.wordWrappedMiniLabel);
            Texture2D art = MonsterTacticsItemArt.Icon(current);
            if (art != null) GUILayout.Label(art, GUILayout.Width(58), GUILayout.Height(58));

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Grant selected completed item")) inventory.Grant(current);
            if (GUILayout.Button("Grant all 36 (bench)"))
                foreach (MonsterTacticsItemKind item in MonsterTacticsItemTypes.Completed) inventory.Grant(item);
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(7);
            chosen = (MonsterUnit)EditorGUILayout.ObjectField("Target Monster", chosen, typeof(MonsterUnit), true);
            EditorGUILayout.HelpBox("Pick one of your own Monster from Hierarchy. " +
                "You can also drag an item from the item bench onto it. This tester only equips in legal phases.", MessageType.None);
            if (GUILayout.Button("Equip selected completed item onto target"))
            {
                var shop = FindAnyObjectByType<MonsterTacticsU5ShopManager>();
                if (chosen == null || shop == null || !shop.CanEquipItemOn(chosen))
                    report = "Select your own Monster; equipping must be allowed this phase.";
                else
                {
                    inventory.Grant(current);
                    report = inventory.TryEquip(inventory.Count - 1, chosen) ? "Equipped " + MonsterTacticsItemTypes.Name(current) : "No free slot; remove an item first.";
                }
            }
            if (chosen != null)
            {
                var held = chosen.GetComponent<MonsterTacticsHeldItems>();
                EditorGUILayout.LabelField("Held", held == null ? "None" : held.Items.Count + "/3 slots");
                if (held != null)
                    foreach (MonsterTacticsItemKind item in held.Items)
                        EditorGUILayout.LabelField("  " + MonsterTacticsItemTypes.Name(item));
                var combat = FindAnyObjectByType<MonsterTacticsCombatDirector>();
                if (combat != null && combat.TryGetSnapshot(chosen, out MonsterTacticsCombatDirector.CombatSnapshot stats))
                {
                    EditorGUILayout.LabelField("Phase", combat.MatchPhase);
                    EditorGUILayout.LabelField("HP / Shield", stats.Health + "/" + stats.MaximumHealth + " | " + stats.Shield);
                    EditorGUILayout.LabelField("AD / AP / AS", stats.Stats.Attack.ToString("0.0") + " | " +
                        stats.Stats.AbilityPower.ToString("0.0") + " | " + stats.Stats.AttackSpeed.ToString("0.00"));
                    EditorGUILayout.LabelField("Armor / MR", stats.EffectiveArmor + " / " + stats.EffectiveMagicResist);
                    EditorGUILayout.LabelField("Sunder / Shred / Wound", stats.SunderRemaining.ToString("0.0") + "s / " +
                        stats.ShredRemaining.ToString("0.0") + "s / " + stats.WoundRemaining.ToString("0.0") + "s");
                    EditorGUILayout.LabelField("Resistance reductions", "Armor " +
                        (stats.ArmorReduction * 100f).ToString("0") + "% / MR " +
                        (stats.MagicResistReduction * 100f).ToString("0") + "% (never exceeds 30% per type)");
                    EditorGUILayout.LabelField("Burn", stats.BurnRemaining.ToString("0.0") + "s; " + stats.BurnDamagePerSecond.ToString("0.0") + " HP/s");
                }
            }
            if (!string.IsNullOrEmpty(report)) EditorGUILayout.HelpBox(report, MessageType.None);

            GUILayout.Space(8);
            showCatalog = EditorGUILayout.Foldout(showCatalog, "Review all 36 effects", true);
            if (showCatalog)
            {
                scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Height(205));
                for (int i = 0; i < 36; i++)
                {
                    MonsterTacticsItemKind item = MonsterTacticsItemTypes.Completed[i];
                    EditorGUILayout.LabelField((i + 1) + ". " + MonsterTacticsItemTypes.Name(item), EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(MonsterTacticsItemPassiveCatalog.Effect(item), EditorStyles.wordWrappedMiniLabel);
                    GUILayout.Space(4);
                }
                EditorGUILayout.EndScrollView();
            }
            if (Event.current.type == EventType.Layout) Repaint();
        }
    }
}
#endif
