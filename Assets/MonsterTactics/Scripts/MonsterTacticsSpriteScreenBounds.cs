using System;
using System.Collections.Generic;
using UnityEngine;

namespace MonsterTactics
{
    /// <summary>
    /// Projects the actual visible pixels, not the full transparent 96x96 canvas,
    /// onto the Unity screen. The metadata is baked once by an Editor-only importer;
    /// textures do NOT need Read/Write enabled at runtime or in WebGL.
    /// </summary>
    public static class MonsterTacticsSpriteScreenBounds
    {
        private static Dictionary<Sprite, Rect> boundsBySprite;

        // Called after the Editor rebuilds the database, including projects
        // that disable domain reload between Play Mode sessions.
        public static void InvalidateCache() { boundsBySprite = null; }

        private static void EnsureData()
        {
            if (boundsBySprite != null) return;
            boundsBySprite = new Dictionary<Sprite, Rect>();
            MonsterTacticsSpriteVisualBoundsDatabase asset =
                Resources.Load<MonsterTacticsSpriteVisualBoundsDatabase>("MonsterTacticsSpriteVisualBounds");
            if (asset == null) return;
            foreach (var entry in asset.Entries)
                if (entry.Sprite != null)
                    boundsBySprite[entry.Sprite] = entry.VisibleRect;
        }

        public static bool TryGetVisibleRect(Camera camera, SpriteRenderer renderer, out Rect screenRect)
        {
            screenRect = default(Rect);
            if (camera == null || renderer == null || !renderer.enabled || renderer.sprite == null)
                return false;

            EnsureData();
            Sprite sprite = renderer.sprite;
            Rect normalized;
            if (!boundsBySprite.TryGetValue(sprite, out normalized))
            {
                // Fallback for custom art not yet included in the database.
                // Use the center/lower part of its imported canvas; rebuild the
                // database from the Art menu to obtain exact transparent bounds.
                normalized = new Rect(.12f, .22f, .76f, .76f);
            }

            float ppu = sprite.pixelsPerUnit;
            if (ppu <= .0001f) return false;
            Vector2 pivot = sprite.pivot;
            Rect spriteRect = sprite.rect;
            float lx = (normalized.xMin * spriteRect.width - pivot.x) / ppu;
            float rx = (normalized.xMax * spriteRect.width - pivot.x) / ppu;
            float by = (normalized.yMin * spriteRect.height - pivot.y) / ppu;
            float ty = (normalized.yMax * spriteRect.height - pivot.y) / ppu;

            Transform t = renderer.transform;
            Vector3 a = camera.WorldToScreenPoint(t.TransformPoint(new Vector3(lx, by, 0f)));
            Vector3 b = camera.WorldToScreenPoint(t.TransformPoint(new Vector3(lx, ty, 0f)));
            Vector3 c = camera.WorldToScreenPoint(t.TransformPoint(new Vector3(rx, by, 0f)));
            Vector3 d = camera.WorldToScreenPoint(t.TransformPoint(new Vector3(rx, ty, 0f)));
            if (a.z <= 0f || b.z <= 0f || c.z <= 0f || d.z <= 0f) return false;
            float minX = Mathf.Min(Mathf.Min(a.x, b.x), Mathf.Min(c.x, d.x));
            float maxX = Mathf.Max(Mathf.Max(a.x, b.x), Mathf.Max(c.x, d.x));
            float minY = Mathf.Min(Mathf.Min(a.y, b.y), Mathf.Min(c.y, d.y));
            float maxY = Mathf.Max(Mathf.Max(a.y, b.y), Mathf.Max(c.y, d.y));
            screenRect = new Rect(minX, Screen.height - maxY,
                Mathf.Max(1f, maxX - minX), Mathf.Max(1f, maxY - minY));
            return true;
        }
    }
}
