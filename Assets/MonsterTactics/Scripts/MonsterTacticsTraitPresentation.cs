using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace MonsterTactics
{
    /// <summary>
    /// U6.6.4: Shared presentation for the team synergy sidebar and Monster inspector.
    /// Pulls all tiers and bonuses from MonsterTacticsTraits.All; only affects UI.
    /// </summary>
    public static class MonsterTacticsTraitPresentation
    {
        private static readonly Dictionary<string, Texture2D> Icons =
            new Dictionary<string, Texture2D>();
        private static GUIStyle tooltipText;
        private static GUIStyle tooltipTitle;
        private static GUIStyle tooltipHint;
        private static GUIStyle traitLabel;
        private static readonly Color TooltipBackground = new Color(.065f, .105f, .17f, .99f);
        private static readonly Color Gold = new Color(.98f, .81f, .43f);

        public static MonsterTacticsTraits.Definition Find(string trait)
        {
            foreach (MonsterTacticsTraits.Definition def in MonsterTacticsTraits.All)
                if (string.Equals(def.Name, trait, System.StringComparison.OrdinalIgnoreCase))
                    return def;
            return null;
        }

        public static Texture2D Icon(string trait)
        {
            if (string.IsNullOrEmpty(trait)) return null;
            if (Icons.TryGetValue(trait, out Texture2D icon)) return icon;
            MonsterTacticsTraits.Definition definition = Find(trait);
            bool origin = definition != null && definition.Origin;
            string path = origin ? "OriginIcons/" + trait.Replace(" ", "") :
                "TypeIcons/" + trait;
            icon = Resources.Load<Texture2D>(path);
            Icons[trait] = icon;
            return icon;
        }

        public static void DrawIcon(string trait, Rect rect)
        {
            Texture2D icon = Icon(trait);
            if (icon != null) GUI.DrawTexture(rect, icon, ScaleMode.ScaleToFit, true);
            else
            {
                EnsureStyles();
                GUI.Label(rect, "*", traitLabel);
            }
        }

        public static string BreakpointText(MonsterTacticsTraits.Definition def)
        {
            if (def == null || def.Breakpoints.Length == 0) return "Type only";
            StringBuilder text = new StringBuilder();
            for (int i = 0; i < def.Breakpoints.Length; i++)
            {
                if (i != 0) text.Append(" / ");
                text.Append(def.Breakpoints[i]);
            }
            return text.ToString();
        }

        private static string Summary(string name)
        {
            switch (name)
            {
                case "Fire": return "Fire Monster gain Attack Damage and Ability Power.";
                case "Water": return "Water Monster generate extra mana.";
                case "Grass": return "Grass Monster gain maximum health.";
                case "Dragon": return "Dragon Monster deal additional damage.";
                case "Ground": return "Ground Monster gain Armor and Magic Resist.";
                case "First Partners": return "Starter Monster gain Attack Speed.";
                case "Playful": return "Playful Monster gain Attack Speed and Critical Chance.";
                case "Plasma Engine": return "Stormlynx's unique (1): basic attacks charge Mana; casting empowers its Attack Speed.";
                case "Legendary Beasts": return "Voltrix, Pyrothane and Aqualith empower one another.";
                default: return MonsterTacticsContentTraitRuntime.IsUsingCatalog ?
                    "Configured trait effects are applied at the listed breakpoints." :
                    "This Monster type has no active bonus in the current roster.";
            }
        }

        private static void EnsureStyles()
        {
            if (tooltipText != null) return;
            tooltipText = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12, wordWrap = true, alignment = TextAnchor.UpperLeft
            };
            tooltipText.normal.textColor = new Color(.89f, .94f, 1f);
            tooltipTitle = new GUIStyle(tooltipText)
            {
                fontSize = 15, fontStyle = FontStyle.Bold
            };
            tooltipTitle.normal.textColor = Gold;
            tooltipHint = new GUIStyle(tooltipText) { fontSize = 11 };
            tooltipHint.normal.textColor = new Color(.70f, .78f, .89f);
            traitLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter
            };
            traitLabel.normal.textColor = Gold;
        }

        /// <summary>Draw outside BeginScrollView to avoid tooltips getting clipped.</summary>
        public static void DrawTooltip(MonsterTacticsTraits.Definition def,
            Dictionary<string, int> counts, Rect sourcePanel, Vector2 mouse,
            bool sourceOnRight, IEnumerable<MonsterUnit> deployedUnits = null,
            bool inspectingEnemy = false)
        {
            if (def == null || Event.current.type != EventType.Repaint) return;
            EnsureStyles();
            int count = 0;
            if (counts != null) counts.TryGetValue(def.Name, out count);
            int tier = inspectingEnemy ? 0 : MonsterTacticsTraits.ActiveTier(def.Name, counts);

            StringBuilder text = new StringBuilder();
            text.AppendLine(Summary(def.Name));
            text.AppendLine();
            if (inspectingEnemy)
                text.AppendLine("Enemy Monster · shown for reference");
            else if (def.Breakpoints.Length == 0)
                text.AppendLine("No active synergy bonus in this set.");
            else
                text.AppendLine(count + " distinct families deployed · " +
                    (tier > 0 ? "ACTIVE" : "Not active"));
            if (def.Breakpoints.Length > 0)
            {
                text.AppendLine();
                for (int i = 0; i < def.Breakpoints.Length; i++)
                    text.AppendLine((!inspectingEnemy && count >= def.Breakpoints[i] ?
                        "[ACTIVE] " : "[    ] ") + def.Breakpoints[i] + ": " +
                        def.Descriptions[i]);
            }
            if (!inspectingEnemy && deployedUnits != null)
            {
                HashSet<string> seen = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
                StringBuilder contributors = new StringBuilder();
                foreach (MonsterUnit unit in deployedUnits)
                {
                    if (unit == null || !unit.gameObject.activeInHierarchy || unit.OccupiedTile == null)
                        continue;
                    string family = MonsterTacticsU5Roster.BaseName(unit.MonsterName);
                    if (!seen.Add(family)) continue;
                    foreach (string name in MonsterTacticsTraits.ForUnit(unit))
                    {
                        if (!string.Equals(name, def.Name, System.StringComparison.OrdinalIgnoreCase)) continue;
                        if (contributors.Length > 0) contributors.Append(", ");
                        contributors.Append(family);
                        break;
                    }
                }
                if (contributors.Length > 0)
                {
                    text.AppendLine();
                    text.AppendLine("Deployed: " + contributors);
                }
            }
            text.AppendLine();
            text.Append(!MonsterTacticsContentTraitRuntime.AppliesOnlyToMatching(def.Name) ?
                "Applies to all allies while this trait is active." :
                def.Origin ? "Applies only to matching origin Monster." :
                "Applies only to matching type Monster.");
            text.Append(" Duplicate copies count once.");

            float width = Mathf.Clamp(Screen.width * .26f, 225f, 320f);
            width = Mathf.Min(width, Screen.width - 20f);
            float contentWidth = width - 24f;
            float bodyHeight = tooltipText.CalcHeight(new GUIContent(text.ToString()), contentWidth);
            float height = 52f + bodyHeight + 12f;
            float availableHeight = Screen.height - 14f;
            if (height > availableHeight) height = availableHeight;
            float rightX = sourcePanel.xMax + 10f;
            float leftX = sourcePanel.xMin - width - 10f;
            float x = sourceOnRight ? leftX : rightX;
            if (x < 7f || x + width > Screen.width - 7f)
                x = sourceOnRight ? rightX : leftX;
            x = Mathf.Clamp(x, 7f, Mathf.Max(7f, Screen.width - width - 7f));
            float y = Mathf.Clamp(mouse.y - 24f, 7f,
                Mathf.Max(7f, Screen.height - height - 7f));

            Rect popup = new Rect(x, y, width, height);
            Color previous = GUI.color;
            GUI.color = TooltipBackground;
            GUI.DrawTexture(popup, Texture2D.whiteTexture);
            GUI.color = new Color(.37f, .50f, .66f, 1f);
            GUI.DrawTexture(new Rect(x, y, width, 1.5f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(x, y + height - 1.5f, width, 1.5f), Texture2D.whiteTexture);
            GUI.color = previous;
            DrawIcon(def.Name, new Rect(x + 9f, y + 8f, 27f, 27f));
            GUI.Label(new Rect(x + 43f, y + 7f, width - 52f, 25f), def.Name, tooltipTitle);
            GUI.Label(new Rect(x + 12f, y + 40f, contentWidth,
                Mathf.Max(0f, height - 45f)), text.ToString(), tooltipText);
        }
    }
}
