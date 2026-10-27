#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace MonsterTactics
{
    [CustomEditor(typeof(MonsterTacticsAugmentDefinition))]
    public sealed class MonsterTacticsU692aAugmentInspector : global::UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.LabelField("AUGMENT ARTWORK  •  U6.9.2a", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Assign a PNG imported as Sprite (2D and UI). Tier borders are always " +
                "Rookie / Ace / Champion. All artwork is optional; missing sprites use a category fallback. " +
                "Visual changes affect the next match.", MessageType.Info);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("icon"),
                new GUIContent("Main Icon / Logo"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("cardArtwork"),
                new GUIContent("Faded Card Artwork"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("categoryBadge"),
                new GUIContent("Category Badge (optional)"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("useCustomAccent"),
                new GUIContent("Custom Accent (not tier)"));
            if (serializedObject.FindProperty("useCustomAccent").boolValue)
                EditorGUILayout.PropertyField(serializedObject.FindProperty("accentColor"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("artworkOpacity"),
                new GUIContent("Card Artwork Opacity"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("showGlow"),
                new GUIContent("Tier Frame Glow"));
            serializedObject.ApplyModifiedProperties();
            MonsterTacticsU692aIconTools.DrawEditorPreview((MonsterTacticsAugmentDefinition)target);
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("AUGMENT SETTINGS", EditorStyles.boldLabel);
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "m_Script", "icon", "cardArtwork", "categoryBadge",
                "useCustomAccent", "accentColor", "artworkOpacity", "showGlow");
            serializedObject.ApplyModifiedProperties();
        }
    }

    public static class MonsterTacticsU692aIconTools
    {
        private const string ArtRoot = "Assets/MonsterTactics/Art/Augments/StarterIcons";
        private const string CatalogPath = "Assets/MonsterTactics/Resources/Augments/MonsterTacticsAugmentCatalog.asset";
        public static Color TierColor(MonsterTacticsAugmentTier tier) =>
            tier == MonsterTacticsAugmentTier.Rookie ? new Color(.67f, .76f, .85f) :
            tier == MonsterTacticsAugmentTier.Ace ? new Color(.96f, .74f, .35f) :
            new Color(.71f, .50f, .97f);
        public static void DrawEditorIcon(Rect rect, MonsterTacticsAugmentDefinition def)
        {
            Color tier = TierColor(def.tier);
            EditorGUI.DrawRect(rect, tier);
            Rect inner = new Rect(rect.x + 2f, rect.y + 2f, rect.width - 4f, rect.height - 4f);
            EditorGUI.DrawRect(inner, new Color(.075f, .11f, .17f));
            if (!DrawSprite(def.icon, new Rect(inner.x + 2f, inner.y + 2f,
                inner.width - 4f, inner.height - 4f)))
                GUI.Label(inner, def.category.ToString().Substring(0, 1), EditorStyles.centeredGreyMiniLabel);
        }
        private static bool DrawSprite(Sprite sprite, Rect r)
        {
            if (sprite == null || sprite.texture == null) return false;
            try
            {
                Rect tr = sprite.textureRect;
                var tex = sprite.texture;
                if (tr.width <= 0f || tr.height <= 0f) return false;
                float aspect = tr.width / tr.height;
                if (aspect > r.width / Mathf.Max(1f, r.height))
                {
                    float h = r.width / aspect;
                    r.y += (r.height - h) / 2f; r.height = h;
                }
                else
                {
                    float w = r.height * aspect;
                    r.x += (r.width - w) / 2f; r.width = w;
                }
                GUI.DrawTextureWithTexCoords(r, tex, new Rect(tr.x / tex.width,
                    tr.y / tex.height, tr.width / tex.width, tr.height / tex.height));
                return true;
            }
            catch (UnityException) { return false; }
        }
        public static void DrawEditorPreview(MonsterTacticsAugmentDefinition def)
        {
            EditorGUILayout.LabelField("Card / HUD artwork preview", EditorStyles.miniBoldLabel);
            Rect r = GUILayoutUtility.GetRect(230f, 142f, GUILayout.ExpandWidth(true));
            var tier = TierColor(def.tier);
            EditorGUI.DrawRect(r, new Color(.08f, .11f, .19f));
            EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, 4f), tier);
            var art = new Rect(r.x + 8f, r.y + 9f, r.width - 16f, r.height - 17f);
            if (def.cardArtwork != null)
            {
                var prior = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp(def.artworkOpacity, 0f, .65f));
                DrawSprite(def.cardArtwork, art);
                GUI.color = prior;
            }
            var icon = new Rect(r.center.x - 39f, r.y + 11f, 78f, 78f);
            DrawEditorIcon(icon, def);
            GUI.Label(new Rect(r.x + 8f, r.y + 92f, r.width - 16f, 19f),
                def.displayName, EditorStyles.centeredGreyMiniLabel);
            GUI.Label(new Rect(r.x + 8f, r.y + 114f, r.width - 16f, 18f),
                def.tier + "  •  " + def.category, EditorStyles.centeredGreyMiniLabel);
        }

        [MenuItem("MonsterTactics/Augments/U6.9.2a Assign Starter Icons (Missing Only)")]
        public static void AssignStarterIcons()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<MonsterTacticsAugmentCatalog>(CatalogPath);
            if (catalog == null)
            {
                EditorUtility.DisplayDialog("Augment Icons", "No augment catalog found. Install the framework first.", "OK");
                return;
            }
            int changed = 0, imported = 0;
            foreach (MonsterTacticsAugmentCategory category in Enum.GetValues(typeof(MonsterTacticsAugmentCategory)))
            {
                string path = ArtRoot + "/" + category + ".png";
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;
                if (importer.textureType != TextureImporterType.Sprite ||
                    importer.spriteImportMode != SpriteImportMode.Single || importer.mipmapEnabled)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.mipmapEnabled = false;
                    importer.alphaIsTransparency = true;
                    importer.SaveAndReimport();
                    imported++;
                }
            }
            foreach (var aug in catalog.definitions)
            {
                if (aug == null || aug.icon != null) continue; // Never overwrite user art.
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArtRoot + "/" + aug.category + ".png");
                if (sprite == null) continue;
                Undo.RecordObject(aug, "Assign Augment Starter Icon");
                aug.icon = sprite;
                EditorUtility.SetDirty(aug);
                changed++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[U6.9.2a] Assigned " + changed + " missing augment icons; " +
                imported + " image(s) configured as Sprite. Existing logos preserved.");
            EditorUtility.DisplayDialog("U6.9.2a Augment Icons",
                "Assigned " + changed + " missing icons. Existing custom icons were not overwritten. " +
                "Changes take effect next match.", "OK");
        }
    }
}
#endif
