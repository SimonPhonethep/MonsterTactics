#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MonsterTactics.EditorTools
{
    public sealed class MonsterTacticsU676ItemArtAudit : EditorWindow
    {
        private Vector2 scroll;
        private Dictionary<MonsterTacticsItemKind, string> sources;
        [MenuItem("MonsterTactics/Tests/Archive/Items/U6.7.6 Item Art Audit")]
        public static void Open()
        {
            GetWindow<MonsterTacticsU676ItemArtAudit>("U6.7.6 Item Art Audit").minSize = new Vector2(580, 430);
        }
        private void OnEnable() { ReadLabels(); }
        private void ReadLabels()
        {
            sources = new Dictionary<MonsterTacticsItemKind, string>();
            try
            {
                foreach (var e in MonsterTacticsU675MonsterItemSpriteImporter.ReadAndValidateMapping())
                    sources[e.Kind] = e.Slug;
            }
            catch (Exception ex) { Debug.LogWarning("[MonsterTactics art audit] " + ex.Message); }
        }
        private void OnGUI()
        {
            EditorGUILayout.LabelField("U6.7.6 - Actual Item Name vs. Monster Sprite", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Compare the MonsterTactics item NAME above the Monster item icon source. " +
                "Gray = component; blue = completed; orange = utility. " +
                "U6.7.6 does not change item names or passive effects.", MessageType.Info);
            if (GUILayout.Button("Refresh Mapping and Previews"))
            {
                ReadLabels(); MonsterTacticsItemArt.ClearCache(); Repaint();
            }
            scroll = EditorGUILayout.BeginScrollView(scroll);
            Group("Components", MonsterTacticsItemTypes.Components);
            Group("Completed (36)", MonsterTacticsItemTypes.Completed);
            Group("Utility", new[] { MonsterTacticsItemKind.ItemRemover, MonsterTacticsItemKind.Reforger });
            EditorGUILayout.EndScrollView();
        }
        private void Group(string label, MonsterTacticsItemKind[] kinds)
        {
            GUILayout.Space(10); GUILayout.Label(label, EditorStyles.boldLabel);
            for (int i = 0; i < kinds.Length; i += 4)
            {
                GUILayout.BeginHorizontal();
                for (int j = i; j < Math.Min(i + 4, kinds.Length); j++)
                {
                    var kind = kinds[j];
                    GUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(136));
                    GUILayout.Label(MonsterTacticsItemTypes.Name(kind), EditorStyles.boldLabel, GUILayout.Width(126));
                    Rect r = GUILayoutUtility.GetRect(90f, 90f, GUILayout.Width(90f));
                    MonsterTacticsItemArt.Draw(r, kind);
                    string src;
                    GUILayout.Label(sources != null && sources.TryGetValue(kind, out src) ? "Monster: " + src : "No source entry",
                        EditorStyles.miniLabel, GUILayout.Width(126));
                    if (MonsterTacticsItemTypes.IsCompleted(kind))
                    {
                        // A recipe-only visual aid: calculate using project-defined crafting rules.
                        string recipe = "";
                        foreach (var a in MonsterTacticsItemTypes.Components)
                            foreach (var b in MonsterTacticsItemTypes.Components)
                            {
                                MonsterTacticsItemKind crafted;
                                if (MonsterTacticsItemTypes.TryRecipe(a, b, out crafted) && crafted == kind)
                                { recipe = MonsterTacticsItemTypes.Name(a) + " + " + MonsterTacticsItemTypes.Name(b); break; }
                            }
                        GUILayout.Label(recipe, EditorStyles.wordWrappedMiniLabel, GUILayout.Width(126));
                    }
                    GUILayout.EndVertical();
                }
                GUILayout.EndHorizontal();
            }
        }
    }
}
#endif
