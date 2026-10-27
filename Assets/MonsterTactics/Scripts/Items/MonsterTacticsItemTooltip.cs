using UnityEngine;

namespace MonsterTactics
{
    /// <summary>U6.8 shared inventory/recipe/equipped item tooltip. No gameplay state.</summary>
    public static class MonsterTacticsItemTooltip
    {
        private static GUIStyle title, details, tag;
        private static readonly Color Back = new Color(.046f, .078f, .125f, .985f);
        private static readonly Color Edge = new Color(.32f, .52f, .70f, 1f);

        private static void EnsureStyles()
        {
            if (title != null) return;
            title = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, wordWrap = true };
            title.normal.textColor = Color.white;
            details = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true };
            details.normal.textColor = new Color(.84f, .91f, .97f);
            tag = new GUIStyle(GUI.skin.label) { fontSize = 10, fontStyle = FontStyle.Bold };
            tag.normal.textColor = new Color(.48f, .77f, 1f);
        }

        private static void Fill(Rect r, Color c)
        {
            Color prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = prev;
        }

        public static void Draw(Vector2 pointer, MonsterTacticsItemKind kind, string context = null)
        {
            if (!MonsterTacticsItemTypes.IsAvailable(kind) || Event.current.type != EventType.Repaint) return;
            EnsureStyles();
            bool completed = MonsterTacticsItemTypes.IsCompleted(kind);
            bool tool = MonsterTacticsItemTypes.IsTool(kind);
            string category = tool ? "CONSUMABLE" : completed ? "COMPLETED ITEM" : "COMPONENT";
            string stats = tool ? "Single use" : MonsterTacticsItemTypes.BonusSummary(MonsterTacticsItemTypes.Bonus(kind));
            string effect = completed ? MonsterTacticsItemPassiveCatalog.Effect(kind) :
                tool ? MonsterTacticsItemTypes.Description(kind) : "Combine with another component to craft an item.";
            if (string.IsNullOrEmpty(effect)) effect = "No passive effect.";
            const float width = 308f;
            float statsHeight = Mathf.Max(26f,
                details.CalcHeight(new GUIContent(stats), width - 22f));
            float textHeight = details.CalcHeight(new GUIContent(effect), width - 22f);
            float effectTop = 69f + statsHeight + 8f;
            float height = Mathf.Max(133f, effectTop + textHeight + 13f +
                (context != null ? 21f : 0f));
            float x = pointer.x + 18f;
            if (x + width > Screen.width - 5f) x = pointer.x - width - 18f;
            float y = Mathf.Clamp(pointer.y + 14f, 5f, Mathf.Max(5f, Screen.height - height - 5f));
            x = Mathf.Clamp(x, 5f, Mathf.Max(5f, Screen.width - width - 5f));
            Rect r = new Rect(x, y, width, height);
            Fill(r, Back);
            Fill(new Rect(x, y, width, 2f), tool ? new Color(1f,.53f,.15f) : completed ?
                new Color(.18f,.60f,1f) : Edge);
            MonsterTacticsItemArt.Draw(new Rect(x + 10f, y + 12f, 43f, 43f), kind);
            GUI.Label(new Rect(x + 61f, y + 9f, width - 70f, 24f), MonsterTacticsItemTypes.Name(kind), title);
            GUI.Label(new Rect(x + 61f, y + 34f, width - 70f, 16f), category, tag);
            Fill(new Rect(x + 10f, y + 62f, width - 20f, 1f), Edge * .55f);
            GUI.Label(new Rect(x + 10f, y + 69f, width - 20f, statsHeight), stats, details);
            GUI.Label(new Rect(x + 10f, y + effectTop, width - 20f,
                textHeight + 5f), effect, details);
            if (context != null)
                GUI.Label(new Rect(x + 10f, y + height - 20f, width - 20f, 17f), context, tag);
        }
    }
}
