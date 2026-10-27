#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace MonsterTactics.EditorTools
{
    public sealed class MonsterTacticsU674ItemArtGallery : EditorWindow
    {
        private Vector2 scroll;
        [MenuItem("MonsterTactics/Tests/Archive/Items/U6.7.4 Item Art Gallery")]
        public static void Open()
        {
            GetWindow<MonsterTacticsU674ItemArtGallery>("U6.7.4 Item Gallery").minSize =
                new Vector2(390f, 380f);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("U6.7.4 - Monster-style item art", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Gray = components. Blue = completed combat items. " +
                "Orange = consumable tools. Actual recipes and game effects are unchanged.",
                MessageType.Info);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            DrawGroup("8 Crafting Components", MonsterTacticsItemTypes.Components);
            DrawGroup("36 Completed Items", MonsterTacticsItemTypes.Completed);
            DrawGroup("2 Utility Tools", new[] { MonsterTacticsItemKind.ItemRemover, MonsterTacticsItemKind.Reforger });
            EditorGUILayout.EndScrollView();
        }

        private static void DrawGroup(string title, MonsterTacticsItemKind[] kinds)
        {
            GUILayout.Space(10f);
            GUILayout.Label(title, EditorStyles.boldLabel);
            for (int i = 0; i < kinds.Length; i += 4)
            {
                GUILayout.BeginHorizontal();
                for (int j = i; j < Mathf.Min(i + 4, kinds.Length); j++)
                {
                    MonsterTacticsItemKind kind = kinds[j];
                    GUILayout.BeginVertical(GUILayout.Width(90f));
                    Rect r = GUILayoutUtility.GetRect(70f, 70f, GUILayout.Width(70f));
                    MonsterTacticsItemArt.Draw(r, kind);
                    GUILayout.Label(MonsterTacticsItemTypes.Name(kind),
                        EditorStyles.miniLabel, GUILayout.Width(90f));
                    GUILayout.EndVertical();
                }
                GUILayout.EndHorizontal();
            }
        }
    }
}
#endif
