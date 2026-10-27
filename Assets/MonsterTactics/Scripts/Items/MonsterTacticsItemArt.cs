using System.Collections.Generic;
using UnityEngine;

namespace MonsterTactics
{
    /// <summary>
    /// U6.7.6 — source-independent PNG item art with category frames. Existing enum IDs and
    /// Resources/ItemIcons paths are unchanged to protect saved Unity scenes.
    /// </summary>
    public static class MonsterTacticsItemArt
    {
        private static readonly Dictionary<MonsterTacticsItemKind, Texture2D> iconCache =
            new Dictionary<MonsterTacticsItemKind, Texture2D>();
        private static readonly Dictionary<string, Texture2D> frameCache =
            new Dictionary<string, Texture2D>();

        /// <summary>Reload icons and frames after external PNG files are replaced in the Editor.</summary>
        public static void ClearCache()
        {
            iconCache.Clear();
            frameCache.Clear();
        }

        public static Texture2D Icon(MonsterTacticsItemKind kind)
        {
            if (!MonsterTacticsItemTypes.IsAvailable(kind)) return null;
            Texture2D tex;
            if (iconCache.TryGetValue(kind, out tex)) return tex;
            tex = Resources.Load<Texture2D>("ItemIcons/" + kind);
            iconCache[kind] = tex;
            return tex;
        }

        public static Texture2D Frame(MonsterTacticsItemKind kind)
        {
            if (!MonsterTacticsItemTypes.IsAvailable(kind)) return null;
            string name = MonsterTacticsItemTypes.IsTool(kind) ? "FrameUtility" :
                (MonsterTacticsItemTypes.IsCompleted(kind) ? "FrameCompleted" : "FrameComponent");
            Texture2D tex;
            if (frameCache.TryGetValue(name, out tex)) return tex;
            tex = Resources.Load<Texture2D>("ItemFrames/" + name);
            frameCache[name] = tex;
            return tex;
        }

        /// <summary>Draw a framed item in Unity IMGUI. Reused by bench, drag, and inspector.</summary>
        public static void Draw(Rect rect, MonsterTacticsItemKind kind)
        {
            if (!MonsterTacticsItemTypes.IsAvailable(kind)) return;
            Texture2D frame = Frame(kind);
            if (frame != null) GUI.DrawTexture(rect, frame, ScaleMode.StretchToFill, true);
            else
            {
                Color old = GUI.color;
                GUI.color = MonsterTacticsItemTypes.IsTool(kind) ? new Color(.95f,.51f,.17f) :
                    (MonsterTacticsItemTypes.IsCompleted(kind) ? new Color(.17f,.58f,.97f) :
                     new Color(.44f,.52f,.61f));
                GUI.DrawTexture(rect, Texture2D.whiteTexture);
                GUI.color = old;
            }
            Texture2D icon = Icon(kind);
            if (icon != null)
            {
                float padding = Mathf.Max(2f, Mathf.Min(rect.width, rect.height) * .11f);
                GUI.DrawTexture(new Rect(rect.x + padding, rect.y + padding,
                    rect.width - 2f * padding, rect.height - 2f * padding),
                    icon, ScaleMode.ScaleToFit, true);
            }
        }
    }
}
