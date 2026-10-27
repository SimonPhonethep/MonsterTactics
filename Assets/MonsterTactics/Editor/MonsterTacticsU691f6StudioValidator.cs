#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace MonsterTactics
{
    /// <summary>Non-mutating checks for the Studio routing state and inspector serialization.</summary>
    public static class MonsterTacticsU691f6StudioValidator
    {
        [MenuItem("MonsterTactics/Tests/Archive/Content Studio/U6.9.1f.6 Verify Studio Configuration")]
        private static void Verify()
        {
            MonsterTacticsContentCatalog catalog = MonsterTacticsContentStudioImporter.FindCatalog();
            if (catalog == null)
            {
                EditorUtility.DisplayDialog("MonsterTactics U6.9.1f.6", "Catalog not found. Import missing legacy data first.", "OK");
                return;
            }
            int checks = 0;
            var serialized = new SerializedObject(catalog);
            foreach (string field in new[] { "monster", "traits", "abilities",
                "readOnlyLegacyRuntime", "enableLiveTraits", "enableLiveAbilities" })
            {
                if (serialized.FindProperty(field) == null)
                    throw new InvalidOperationException("Missing catalog field: " + field);
                checks++;
            }
            int authored = 0, legacy = 0;
            foreach (var ability in catalog.abilities)
            {
                if (ability == null || !ability.enabled) continue;
                if (catalog.enableLiveAbilities && ability.useEffectBlocks) authored++;
                else legacy++;
            }
            checks++;
            Debug.Log("[U6.9.1f.6] PASS: " + checks + " source/serialization checks; " +
                catalog.monster.Count + " Monster, " + catalog.traits.Count + " traits, " +
                authored + " authored / " + legacy + " legacy enabled abilities. " +
                "No runtime switches or data were modified.");
            EditorUtility.DisplayDialog("MonsterTactics U6.9.1f.6", "PASS: " + checks +
                " checks. Catalog data and gameplay routing were not changed.", "OK");
        }
    }
}
#endif
