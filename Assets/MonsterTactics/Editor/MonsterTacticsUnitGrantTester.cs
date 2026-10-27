#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MonsterTactics.Editor
{
    /// <summary>Play Mode only: grants a real, free Monster for isolated combat tests.</summary>
    public sealed class MonsterTacticsUnitGrantTester : EditorWindow
    {
        private string search = "";
        private string selectedFamily = "";
        private int stars = 1;
        private int copies = 1;
        private string lastResult = "Pick a Monster, then press Grant. No Creature Orbs or shop rerolls involved.";
        private MessageType lastResultType = MessageType.Info;

        [MenuItem("MonsterTactics/Tests/Unit Grant Tester (Play Mode)", priority = 130)]
        private static void Open()
        {
            MonsterTacticsUnitGrantTester window = GetWindow<MonsterTacticsUnitGrantTester>("Grant Monster");
            window.minSize = new Vector2(385f, 325f);
            window.Show();
        }

        private void OnInspectorUpdate() { Repaint(); }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("UNIT GRANT TESTER", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Add an existing enabled Monster to your real 9-slot reserve bench. " +
                "Pick its star form and test animations, deployment, traits and abilities. " +
                "Play Mode changes disappear when you stop the game.", MessageType.Info);

            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("Press Play in the MonsterTactics arena to grant units.", MessageType.Warning);
                return;
            }
            MonsterTacticsU5ShopManager shop = UnityEngine.Object.FindAnyObjectByType<MonsterTacticsU5ShopManager>();
            if (shop == null || !shop.IsShopReady)
            {
                EditorGUILayout.HelpBox("Waiting for the MonsterTactics shop/bench to initialize in the arena scene.", MessageType.Warning);
                return;
            }

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Available bench slots", shop.EmptyBenchSlots.ToString());
            EditorGUILayout.LabelField("Current phase", shop.PhaseForHud);

            var families = MonsterTacticsU5Roster.ActiveFamilies
                .Where(f => f != null && f.Forms != null && f.Forms.Length == 3)
                .OrderBy(f => f.Cost)
                .ThenBy(f => f.Forms[0], StringComparer.OrdinalIgnoreCase)
                .ToArray();
            search = EditorGUILayout.TextField("Search Monster", search);
            var matches = families.Where(f => string.IsNullOrWhiteSpace(search) ||
                    f.Forms.Any(form => form.IndexOf(search.Trim(), StringComparison.OrdinalIgnoreCase) >= 0))
                .ToArray();
            if (matches.Length == 0)
            {
                EditorGUILayout.HelpBox("No enabled Monster matches this search. New proposed families will appear after they are added to the Content Catalog.", MessageType.Warning);
                return;
            }

            int current = Array.FindIndex(matches, f => f.Forms[0] == selectedFamily);
            if (current < 0) current = 0;
            string[] labels = matches.Select(f =>
                f.Cost + "g  |  " + f.Forms[0] +
                (MonsterTacticsU5Roster.HasDistinctEvolution(f) ? " → " + f.Forms[2] : "") +
                "  (" + f.Role + ")").ToArray();
            current = EditorGUILayout.Popup("Monster", current, labels);
            var family = matches[Mathf.Clamp(current, 0, matches.Length - 1)];
            selectedFamily = family.Forms[0];
            stars = EditorGUILayout.IntSlider("Star Level", stars, 1, 3);
            copies = EditorGUILayout.IntPopup("Copies", copies,
                new[] { "1 copy", "2 copies", "3 copies" }, new[] { 1, 2, 3 });
            EditorGUILayout.HelpBox(
                "Selected form: " + family.Form(stars) + " (" + stars + "★)" +
                "\nCost: FREE (test-only)  •  Tier: " + family.Cost + "g  •  Role: " + family.Role +
                "\n3 identical copies use the normal evolution system.", MessageType.None);

            using (new EditorGUI.DisabledScope(!shop.CanManageBoard || shop.EmptyBenchSlots < copies))
            {
                if (GUILayout.Button("Grant to Bench", GUILayout.Height(34)))
                    Grant(shop, family.Forms[0], false);
                using (new EditorGUI.DisabledScope(copies != 1))
                {
                    if (GUILayout.Button("Grant and Deploy to Free Hex", GUILayout.Height(30)))
                        Grant(shop, family.Forms[0], true);
                }
            }
            if (!shop.CanManageBoard)
                EditorGUILayout.HelpBox("Grants are available in Planning only. Finish the current combat/choice screen first.", MessageType.Warning);
            else if (shop.EmptyBenchSlots < copies)
                EditorGUILayout.HelpBox("Not enough empty bench hexes. Free up space or lower the copy count.", MessageType.Warning);

            EditorGUILayout.Space(5);
            EditorGUILayout.HelpBox(lastResult, lastResultType);
        }

        private void Grant(MonsterTacticsU5ShopManager shop, string family, bool deploy)
        {
            try
            {
                bool success = shop.TryGrantTestMonster(family, stars, copies, deploy, out string result);
                lastResult = result;
                lastResultType = success ? MessageType.Info : MessageType.Warning;
                Repaint();
            }
            catch (Exception ex)
            {
                lastResult = "Grant failed: " + ex.Message;
                lastResultType = MessageType.Error;
                Debug.LogException(ex);
            }
        }
    }
}
#endif
