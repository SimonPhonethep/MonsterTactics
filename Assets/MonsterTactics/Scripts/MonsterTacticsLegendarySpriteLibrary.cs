using System;
using System.Collections.Generic;
using UnityEngine;

namespace MonsterTactics
{
    /// <summary>
    /// Lightweight runtime sprite generator for 5-cost legendaries.
    /// Keeps the patch self-contained without requiring scene prefab work.
    /// </summary>
    public static class MonsterTacticsLegendarySpriteLibrary
    {
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);

        public static bool HasSprite(string monsterName)
        {
            return string.Equals(monsterName, "Voltrix", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(monsterName, "Pyrothane", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(monsterName, "Aqualith", StringComparison.OrdinalIgnoreCase);
        }

        public static Sprite GetSprite(string monsterName)
        {
            if (string.IsNullOrEmpty(monsterName)) return null;
            if (Cache.TryGetValue(monsterName, out Sprite cached) && cached != null) return cached;

            Sprite sprite = Resources.Load<Sprite>("LegendarySprites/" + monsterName);
            if (sprite != null) Cache[monsterName] = sprite;
            return sprite;
        }

        public static Sprite[] GetAnimation(string monsterName)
        {
            if (!HasSprite(monsterName)) return new Sprite[0];
            Sprite[] frames = Resources.LoadAll<Sprite>("LegendarySprites/" + monsterName.ToLowerInvariant());
            Array.Sort(frames, (a, b) => string.CompareOrdinal(a.name, b.name));
            return frames;
        }

        private static Sprite BuildSprite(string monsterName)
        {
            const int size = 96;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.name = monsterName + " Runtime Sprite";
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            Color32 clear = new Color(0, 0, 0, 0);
            Color32[] px = new Color32[size * size];
            for (int i = 0; i < px.Length; i++) px[i] = clear;

            switch (monsterName.ToLowerInvariant())
            {
                case "voltrix": DrawVoltrix(px, size); break;
                case "pyrothane": DrawPyrothane(px, size); break;
                case "aqualith": DrawAqualith(px, size); break;
                default: DrawFallback(px, size, new Color32(220, 220, 220, 255)); break;
            }

            tex.SetPixels32(px);
            tex.Apply(false, true);
            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(.5f, .22f), 64f, 0, SpriteMeshType.FullRect);
            sprite.name = monsterName + " Runtime Sprite";
            return sprite;
        }

        private static void DrawVoltrix(Color32[] px, int size)
        {
            Color32 black = new Color32(22, 22, 30, 255);
            Color32 yellow = new Color32(240, 210, 76, 255);
            Color32 pale = new Color32(248, 238, 168, 255);
            Color32 white = new Color32(248, 248, 252, 255);
            Color32 blue = new Color32(90, 180, 255, 255);
            // mane cloud
            FillEllipse(px, size, 48, 58, 30, 26, black);
            FillTri(px, size, 19, 58, 8, 72, 22, 75, black);
            FillTri(px, size, 77, 58, 88, 72, 74, 75, black);
            // body/head
            FillEllipse(px, size, 48, 54, 24, 20, yellow);
            FillEllipse(px, size, 48, 62, 18, 18, yellow);
            FillTri(px, size, 33, 67, 24, 83, 36, 79, yellow);
            FillTri(px, size, 63, 67, 72, 83, 60, 79, yellow);
            FillEllipse(px, size, 48, 45, 15, 10, pale);
            FillEllipse(px, size, 40, 54, 4, 4, white);
            FillEllipse(px, size, 56, 54, 4, 4, white);
            FillEllipse(px, size, 40, 55, 2, 2, blue);
            FillEllipse(px, size, 56, 55, 2, 2, blue);
            // saber teeth
            FillRect(px, size, 39, 36, 3, 12, white);
            FillRect(px, size, 54, 36, 3, 12, white);
            // lightning bolt
            FillPoly(px, size, new [] { 50, 32, 62, 32, 54, 20, 64, 20, 46, 2, 50, 16, 40, 16 }, new Color32(255, 244, 150, 255));
            Outline(px, size, black);
        }

        private static void DrawPyrothane(Color32[] px, int size)
        {
            Color32 brown = new Color32(74, 44, 34, 255);
            Color32 red = new Color32(198, 66, 48, 255);
            Color32 cream = new Color32(241, 224, 178, 255);
            Color32 yellow = new Color32(245, 204, 74, 255);
            Color32 white = new Color32(250, 250, 252, 255);
            // mane/body
            FillEllipse(px, size, 48, 56, 30, 26, brown);
            FillEllipse(px, size, 48, 56, 22, 20, red);
            FillEllipse(px, size, 48, 46, 16, 12, cream);
            FillTri(px, size, 34, 69, 28, 82, 39, 79, red);
            FillTri(px, size, 62, 69, 68, 82, 57, 79, red);
            // crown crest
            FillTri(px, size, 33, 70, 27, 88, 41, 79, yellow);
            FillTri(px, size, 48, 72, 42, 94, 54, 94, yellow);
            FillTri(px, size, 63, 70, 69, 88, 55, 79, yellow);
            // face
            FillEllipse(px, size, 40, 53, 4, 4, white);
            FillEllipse(px, size, 56, 53, 4, 4, white);
            FillEllipse(px, size, 40, 54, 2, 2, brown);
            FillEllipse(px, size, 56, 54, 2, 2, brown);
            FillRect(px, size, 44, 37, 8, 4, brown);
            FillRect(px, size, 38, 28, 4, 9, white);
            FillRect(px, size, 54, 28, 4, 9, white);
            Outline(px, size, brown);
        }

        private static void DrawAqualith(Color32[] px, int size)
        {
            Color32 blue = new Color32(86, 206, 236, 255);
            Color32 deep = new Color32(58, 126, 199, 255);
            Color32 purple = new Color32(164, 104, 236, 255);
            Color32 white = new Color32(248, 250, 252, 255);
            // flowing ribbons
            FillPoly(px, size, new [] { 13, 73, 28, 66, 38, 68, 26, 76 }, purple);
            FillPoly(px, size, new [] { 58, 68, 68, 66, 83, 73, 70, 76 }, purple);
            // crest/body
            FillEllipse(px, size, 48, 56, 25, 20, blue);
            FillEllipse(px, size, 48, 44, 15, 12, white);
            FillEllipse(px, size, 48, 63, 20, 16, deep);
            FillTri(px, size, 33, 68, 27, 83, 38, 79, deep);
            FillTri(px, size, 63, 68, 69, 83, 58, 79, deep);
            // diamond head crest
            FillPoly(px, size, new [] { 48, 80, 58, 66, 48, 52, 38, 66 }, purple);
            FillEllipse(px, size, 41, 54, 4, 4, white);
            FillEllipse(px, size, 55, 54, 4, 4, white);
            FillEllipse(px, size, 41, 55, 2, 2, deep);
            FillEllipse(px, size, 55, 55, 2, 2, deep);
            FillRect(px, size, 44, 40, 8, 3, deep);
            Outline(px, size, deep);
        }

        private static void DrawFallback(Color32[] px, int size, Color32 c)
        {
            FillEllipse(px, size, size / 2, size / 2, 24, 24, c);
        }

        private static void FillEllipse(Color32[] px, int size, int cx, int cy, int rx, int ry, Color32 color)
        {
            for (int y = cy - ry; y <= cy + ry; y++)
            for (int x = cx - rx; x <= cx + rx; x++)
            {
                if (x < 0 || y < 0 || x >= size || y >= size) continue;
                float dx = (x - cx) / (float)rx;
                float dy = (y - cy) / (float)ry;
                if (dx * dx + dy * dy <= 1f) px[y * size + x] = color;
            }
        }

        private static void FillRect(Color32[] px, int size, int x, int y, int w, int h, Color32 color)
        {
            for (int yy = y; yy < y + h; yy++)
            for (int xx = x; xx < x + w; xx++)
                if (xx >= 0 && yy >= 0 && xx < size && yy < size) px[yy * size + xx] = color;
        }

        private static void FillTri(Color32[] px, int size, int x1, int y1, int x2, int y2, int x3, int y3, Color32 color)
        {
            FillPoly(px, size, new[] { x1, y1, x2, y2, x3, y3 }, color);
        }

        private static void FillPoly(Color32[] px, int size, int[] points, Color32 color)
        {
            int count = points.Length / 2;
            int minX = size, minY = size, maxX = 0, maxY = 0;
            for (int i = 0; i < count; i++)
            {
                int x = points[i * 2];
                int y = points[i * 2 + 1];
                minX = Mathf.Min(minX, x); minY = Mathf.Min(minY, y);
                maxX = Mathf.Max(maxX, x); maxY = Mathf.Max(maxY, y);
            }
            for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
            {
                if (PointInPolygon(x, y, points))
                    if (x >= 0 && y >= 0 && x < size && y < size) px[y * size + x] = color;
            }
        }

        private static bool PointInPolygon(int x, int y, int[] points)
        {
            bool inside = false;
            int count = points.Length / 2;
            for (int i = 0, j = count - 1; i < count; j = i++)
            {
                int xi = points[i * 2], yi = points[i * 2 + 1];
                int xj = points[j * 2], yj = points[j * 2 + 1];
                bool intersect = ((yi > y) != (yj > y)) &&
                                 (x < (xj - xi) * (y - yi) / (float)Mathf.Max(1, (yj - yi)) + xi);
                if (intersect) inside = !inside;
            }
            return inside;
        }

        private static void Outline(Color32[] px, int size, Color32 outline)
        {
            Color32[] copy = (Color32[])px.Clone();
            for (int y = 1; y < size - 1; y++)
            for (int x = 1; x < size - 1; x++)
            {
                Color32 current = copy[y * size + x];
                if (current.a == 0) continue;
                for (int oy = -1; oy <= 1; oy++)
                for (int ox = -1; ox <= 1; ox++)
                {
                    int idx = (y + oy) * size + (x + ox);
                    if (copy[idx].a == 0) px[idx] = outline;
                }
            }
        }
    }
}
