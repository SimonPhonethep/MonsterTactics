using System.Collections.Generic;
using UnityEngine;

namespace MonsterTactics
{
    /// <summary>
    /// U6.6.1: Compact trait panel below the trainer picker. Only traits on
    /// deployed Monster appear; active traits are ordered ahead of inactive ones.
    /// UI is passive: it never blocks world or shop input.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MonsterTacticsTraitHud : MonoBehaviour
    {
        private MonsterPlacementController placement;
        private MonsterTacticsCombatDirector combat;
        private MonsterTacticsU5CanvasHud canvasHud;
        private GUIStyle titleStyle;
        private GUIStyle lineStyle;
        private GUIStyle detailStyle;
        private Vector2 scroll;
        private static readonly Color Active = new Color(.40f, .92f, .65f);
        private const float RowHeight = 43f;

        private void OnGUI()
        {
            if (placement == null) placement = FindAnyObjectByType<MonsterPlacementController>();
            if (placement == null) return;
            if (combat == null) combat = GetComponent<MonsterTacticsCombatDirector>();
            if (canvasHud == null) canvasHud = FindAnyObjectByType<MonsterTacticsU5CanvasHud>();

            // During combat, keep the beginning-of-fight trait roster locked, so
            // the panel doesn't flicker when a deployed Monster faints.
            Dictionary<string, int> counts = combat != null && combat.MatchPhase != "Planning" &&
                combat.PlayerTraitCounts != null
                    ? combat.PlayerTraitCounts
                    : MonsterTacticsTraits.CountDeployed(
                        placement.GetComponentsInChildren<MonsterUnit>(true));

            var present = new List<MonsterTacticsTraits.Definition>();
            foreach (MonsterTacticsTraits.Definition definition in MonsterTacticsTraits.All)
                if (counts.TryGetValue(definition.Name, out int n) && n > 0)
                    present.Add(definition);
            // No deployed units = no floating panel. Activated traits come first.
            if (present.Count == 0) return;
            present.Sort((a, b) =>
            {
                int aTier = MonsterTacticsTraits.ActiveTier(a.Name, counts);
                int bTier = MonsterTacticsTraits.ActiveTier(b.Name, counts);
                if ((aTier > 0) != (bTier > 0)) return aTier > 0 ? -1 : 1;
                return System.Array.IndexOf(MonsterTacticsTraits.All, a).CompareTo(
                    System.Array.IndexOf(MonsterTacticsTraits.All, b));
            });

            if (titleStyle == null)
            {
                titleStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 13, fontStyle = FontStyle.Bold,
                    clipping = TextClipping.Clip
                };
                titleStyle.normal.textColor = new Color(.98f, .81f, .40f);
                lineStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 11, fontStyle = FontStyle.Bold,
                    clipping = TextClipping.Clip
                };
                lineStyle.normal.textColor = Color.white;
                detailStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 9, wordWrap = true,
                    clipping = TextClipping.Clip
                };
                detailStyle.normal.textColor = new Color(.77f, .84f, .91f);

            }

            float top = Screen.height < 720 ? 118f : 167f;
            if (canvasHud != null && canvasHud.TrainerPickerBottomGuiY > 0f)
                top = canvasHud.TrainerPickerBottomGuiY + 12f;

            float width = Mathf.Min(244f, Screen.width * .22f);
            float maximumBottom = Screen.height - 18f;
            if (canvasHud != null && canvasHud.ShopTopScreenY > 0f)
                maximumBottom = Mathf.Min(maximumBottom,
                    Screen.height - canvasHud.ShopTopScreenY - 12f);
            float maxHeight = maximumBottom - top;
            if (width < 125f || maxHeight < 55f) return;
            float height = Mathf.Min(38f + present.Count * RowHeight + 6f, maxHeight);
            Rect panel = new Rect(12f, top, width, height);
            Color saved = GUI.color;
            GUI.color = new Color(.06f, .11f, .16f, .95f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = saved;
            GUI.Label(new Rect(panel.x + 8f, panel.y + 5f, width - 16f, 25f),
                "TYPE & ORIGIN TRAITS", titleStyle);

            Rect viewport = new Rect(panel.x + 4f, panel.y + 32f,
                panel.width - 8f, panel.height - 36f);
            float totalHeight = present.Count * RowHeight + 4f;
            bool scrollNeeded = totalHeight > viewport.height;
            Rect content = new Rect(0f, 0f,
                viewport.width - (scrollNeeded ? 15f : 0f), totalHeight);
            scroll = GUI.BeginScrollView(viewport, scroll, content, false, scrollNeeded);
            float y = 1f;
            foreach (MonsterTacticsTraits.Definition def in present)
            {
                int count = counts[def.Name];
                int tier = MonsterTacticsTraits.ActiveTier(def.Name, counts);
                if (tier > 0)
                {
                    GUI.color = new Color(.14f, .28f, .25f, .70f);
                    GUI.DrawTexture(new Rect(2f, y, content.width - 4f, RowHeight - 2f),
                        Texture2D.whiteTexture);
                    GUI.color = saved;
                }
                MonsterTacticsTraitPresentation.DrawIcon(def.Name,
                    new Rect(6f, y + 6f, 23f, 23f));

                string breakpoints = "";
                foreach (int point in def.Breakpoints)
                {
                    if (breakpoints.Length > 0) breakpoints += "/";
                    breakpoints += point;
                }
                Color previousColor = lineStyle.normal.textColor;
                lineStyle.normal.textColor = tier > 0 ? Active :
                    new Color(.73f, .80f, .88f);
                string countText = def.Breakpoints.Length == 0 ?
                    def.Name + " " + count : def.Name + " " + count + " [" + breakpoints + "]";
                GUI.Label(new Rect(35f, y + 3f, content.width - 41f, 17f), countText, lineStyle);
                lineStyle.normal.textColor = previousColor;
                string description = tier > 0 ? def.Descriptions[tier - 1] :
                    def.Breakpoints.Length == 0 ? "Bonus coming later" : "Not active";
                GUI.Label(new Rect(36f, y + 21f, content.width - 43f, 20f),
                    description, detailStyle);
                y += RowHeight;
            }
            GUI.EndScrollView();

            // IMGUI scroll views clip their children. Calculate hovered trait in
            // screen-space and render the details OUTSIDE the clipped viewport.
            // This is display-only: hover never requires clicking the sidebar.
            Vector2 mouse = Event.current.mousePosition;
            if (viewport.Contains(mouse))
            {
                int index = Mathf.FloorToInt((mouse.y - viewport.y + scroll.y - 1f) / RowHeight);
                if (index >= 0 && index < present.Count)
                    MonsterTacticsTraitPresentation.DrawTooltip(present[index], counts,
                        panel, mouse, false,
                        placement.GetComponentsInChildren<MonsterUnit>(true));
            }
        }

    }
}
