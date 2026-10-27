#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;

namespace MonsterTactics.Editor
{
    /// <summary>
    /// Read-only scene diagnostic. Never changes combat or trainer objects.
    /// </summary>
    public static class MonsterTacticsHudDiagnosticMenu
    {
        [MenuItem("MonsterTactics/UI/Check Duplicate HUD Components (U5.1.10)")]
        private static void CheckDuplicates()
        {
            var directors = Object.FindObjectsByType<MonsterTacticsCombatDirector>(
                FindObjectsInactive.Include);
            var shops = Object.FindObjectsByType<MonsterTacticsU5ShopManager>(
                FindObjectsInactive.Include);
            var placements = Object.FindObjectsByType<MonsterPlacementController>(
                FindObjectsInactive.Include);
            var sb = new StringBuilder("MonsterTactics HUD check (read-only):\n");
            Append(sb, "Combat Director", directors);
            Append(sb, "Shop Manager", shops);
            Append(sb, "Placement Controller", placements);
            if (directors.Length > 1)
                sb.AppendLine("WARNING: More than one Combat Director exists; this may cause stacked top-center planning banners and duplicate combat behavior. Inspect before deleting anything.");
            if (shops.Length > 1)
                sb.AppendLine("WARNING: More than one U5 Shop Manager exists; this may cause duplicate shop panels or conflicting drag interactions.");
            Debug.Log(sb.ToString());
            EditorUtility.DisplayDialog("MonsterTactics HUD Check",
                directors.Length + " combat director(s), " + shops.Length +
                " shop manager(s), " + placements.Length +
                " placement controller(s).\nSee Console for the exact objects. Nothing was changed.", "OK");
        }

        private static void Append<T>(StringBuilder sb, string title, T[] objects) where T : Behaviour
        {
            sb.AppendLine(title + ": " + objects.Length);
            foreach (var component in objects)
            {
                if (component == null) continue;
                sb.AppendLine("  - " + HierarchyPath(component.transform) +
                    (component.enabled && component.gameObject.activeInHierarchy ? " [active]" : " [inactive]"));
            }
        }

        private static string HierarchyPath(Transform t)
        {
            string path = t.name;
            while (t.parent != null)
            {
                t = t.parent;
                path = t.name + "/" + path;
            }
            return path;
        }
    }
}
#endif
