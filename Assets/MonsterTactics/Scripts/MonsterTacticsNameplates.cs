using System;
using System.Collections.Generic;
using UnityEngine;

namespace MonsterTactics
{
    /// <summary>
    /// U6.8.4: TFT-style compact combat widgets. By default only HP/mana/items are
    /// shown above units; names appear only when a unit is selected. This keeps the
    /// board much cleaner than a persistent nameplate over every Monster.
    /// </summary>
    public static class MonsterTacticsNameplates
    {
        public struct Entry
        {
            public MonsterUnit Unit;
            public string Label;
            public float Health;
            public float Mana;
            public float Shield;
            public bool Enemy;
            public bool ShowMana;
            public bool Selected;
            public float SunderRemaining;
            public float ShredRemaining;
            public float WoundRemaining;
            public float BurnRemaining;
            public float AttackSpeedSlowRemaining;
            public float ShadowThiefRemaining;
            public float TemporaryDefenseRemaining;
            public float TemporaryShieldRemaining;
            public bool HasDebuffs => SunderRemaining > 0f || ShredRemaining > 0f ||
                WoundRemaining > 0f || BurnRemaining > 0f ||
                AttackSpeedSlowRemaining > 0f || ShadowThiefRemaining > 0f ||
                TemporaryDefenseRemaining > 0f || TemporaryShieldRemaining > 0f;

            public Entry(MonsterUnit unit, string label, float health, float mana,
                float shield, bool enemy, bool showMana, bool selected,
                float sunderRemaining = 0f, float shredRemaining = 0f,
                float woundRemaining = 0f, float burnRemaining = 0f,
                float attackSpeedSlowRemaining = 0f, float shadowThiefRemaining = 0f,
                float temporaryDefenseRemaining = 0f,
                float temporaryShieldRemaining = 0f)
            {
                Unit = unit; Label = label; Health = health; Mana = mana;
                Shield = shield; Enemy = enemy; ShowMana = showMana; Selected = selected;
                SunderRemaining = sunderRemaining; ShredRemaining = shredRemaining;
                WoundRemaining = woundRemaining; BurnRemaining = burnRemaining;
                AttackSpeedSlowRemaining = attackSpeedSlowRemaining;
                ShadowThiefRemaining = shadowThiefRemaining;
                TemporaryDefenseRemaining = temporaryDefenseRemaining;
                TemporaryShieldRemaining = temporaryShieldRemaining;
            }
        }

        private struct Positioned
        {
            public Entry Data;
            public Rect Rect;
        }

        private static GUIStyle labelStyle;
        private const float SpriteGap = 2f;
        private const float MinimumBarWidth = 68f;
        private const float MaximumBarWidth = 112f;
        private const float LabelHeight = 15f;
        private const float BarAreaHeight = 16f;
        private const float ShieldExtraHeight = 4f;
        private const float ItemIconSize = 21f;
        private const float ItemGap = 2f;
        private const float ItemStripHeight = 23f;
        private const float DebuffStripHeight = 15f;
        // Never move the widget vertically to resolve overlaps: it must remain
        // attached to its own sprite's alpha-visible top edge.
        private static readonly float[] HorizontalOffsets = { 0f, -7f, 7f };
        private static readonly Color PanelColor = new Color(.05f, .09f, .14f, .90f);
        private static readonly Color PanelSelected = new Color(.18f, .32f, .42f, .97f);
        private static readonly Color IconBacking = new Color(.035f, .065f, .105f, .91f);

        private static Texture2D sunderIcon;
        private static Texture2D shredIcon;
        private static Texture2D woundIcon;
        private static Texture2D burnIcon;
        private static Texture2D iceSlowIcon;
        private static Texture2D shadowThiefIcon;
        private static Texture2D forestGuardIcon;

        private static void EnsureStyle()
        {
            if (labelStyle != null) return;
            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Clip,
                richText = false
            };
            labelStyle.normal.textColor = Color.white;
        }

        public static void DrawAll(Camera camera, List<Entry> items)
        {
            // U6.9.1b: when the three augment cards are expanded, hide floating
            // HP/name/mana/item strips. Collapsing instantly restores them.
            if (MonsterTacticsAugmentSelectionUI.SuppressUnitOverlays) return;
            if (camera == null || items == null || items.Count == 0) return;
            EnsureStyle();
            var anchored = new List<Positioned>(items.Count);
            foreach (Entry item in items)
            {
                if (item.Unit == null || !item.Unit.gameObject.activeInHierarchy) continue;
                // Sprite visuals include transparent padding. The U6.8.6 bounds
                // database measures the real non-transparent pixel rectangle.
                SpriteRenderer sprite = item.Unit.NameplateSpriteRenderer;
                Rect visible;
                if (!MonsterTacticsSpriteScreenBounds.TryGetVisibleRect(camera, sprite, out visible))
                {
                    Vector3 fallback = camera.WorldToScreenPoint(item.Unit.NameplateWorldPosition);
                    if (fallback.z <= 0f) continue;
                    visible = new Rect(fallback.x - 28f, Screen.height - fallback.y, 56f, 45f);
                }
                float sx = visible.center.x;
                float sy = visible.y;
                if (sx < -60f || sx > Screen.width + 60f || sy < -80f || sy > Screen.height + 30f)
                    continue;

                MonsterTacticsHeldItems held = item.Unit.GetComponent<MonsterTacticsHeldItems>();
                int count = held != null ? Mathf.Min(3, held.Items.Count) : 0;
                bool showName = item.Selected;
                float rowWidth = count > 0 ? (count * ItemIconSize + (count - 1) * ItemGap) : 0f;
                // TFT-style readable scaling: size relative to the projected
                // visible creature width, with UI-safe min/max boundaries.
                float width = Mathf.Max(Mathf.Clamp(visible.width * 1.18f,
                    MinimumBarWidth, MaximumBarWidth), rowWidth + 6f);
                if (showName)
                {
                    float nameWidth = 12f + item.Label.Length * 6f;
                    width = Mathf.Max(width, Mathf.Clamp(nameWidth, 52f, 108f));
                }

                float totalHeight = BarAreaHeight + (item.Shield > 0f ? ShieldExtraHeight : 0f) +
                    (count > 0 ? ItemStripHeight : 0f) +
                    (item.HasDebuffs ? DebuffStripHeight : 0f) +
                    (showName ? LabelHeight + 2f : 0f);

                anchored.Add(new Positioned
                {
                    Data = item,
                    Rect = new Rect(sx - width * .5f, sy - SpriteGap - totalHeight, width, totalHeight)
                });
            }

            anchored.Sort((a, b) =>
            {
                if (a.Data.Selected != b.Data.Selected) return a.Data.Selected ? -1 : 1;
                int byY = a.Rect.y.CompareTo(b.Rect.y);
                return byY != 0 ? byY : a.Rect.x.CompareTo(b.Rect.x);
            });

            var accepted = new List<Rect>(anchored.Count);
            MonsterTacticsItemKind hoveredItem = default(MonsterTacticsItemKind);
            bool hasHover = false;

            foreach (Positioned p in anchored)
            {
                // Horizontal adjustment only. Vertical relocation would make
                // the widget appear to belong to a neighboring Monster.
                Rect best = p.Rect;
                bool found = false;
                foreach (float dx in HorizontalOffsets)
                {
                    Rect candidate = new Rect(p.Rect.x + dx, p.Rect.y,
                        p.Rect.width, p.Rect.height);
                    if (candidate.x < 2f || candidate.xMax > Screen.width - 2f)
                        continue;
                    if (WouldOverlap(candidate, accepted)) continue;
                    best = candidate;
                    found = true;
                    break;
                }
                if (!found)
                    best.x = Mathf.Clamp(p.Rect.x, 2f,
                        Mathf.Max(2f, Screen.width - p.Rect.width - 2f));
                if (best.yMax < 0f || best.y > Screen.height) continue;
                accepted.Add(best);
                MonsterTacticsItemKind hit;
                if (DrawOne(best, p.Data, out hit))
                {
                    hasHover = true;
                    hoveredItem = hit;
                }
            }

            if (hasHover) MonsterTacticsItemTooltip.Draw(Event.current.mousePosition, hoveredItem);
        }

        private static bool WouldOverlap(Rect candidate, List<Rect> placed)
        {
            foreach (Rect box in placed)
            {
                Rect padded = new Rect(box.x - 4f, box.y - 3f, box.width + 8f, box.height + 6f);
                if (candidate.Overlaps(padded)) return true;
            }
            return false;
        }

        private static bool DrawOne(Rect rect, Entry item, out MonsterTacticsItemKind hovered)
        {
            hovered = default(MonsterTacticsItemKind);
            bool hoveredAny = false;
            Color before = GUI.color;
            MonsterTacticsHeldItems equipped = item.Unit != null ? item.Unit.GetComponent<MonsterTacticsHeldItems>() : null;
            int equippedCount = equipped != null ? Mathf.Min(3, equipped.Items.Count) : 0;
            bool showName = item.Selected;

            float currentY = rect.y;
            if (showName)
            {
                Rect nameRect = new Rect(rect.x, currentY, rect.width, LabelHeight);
                GUI.color = item.Selected ? PanelSelected : PanelColor;
                GUI.DrawTexture(nameRect, Texture2D.whiteTexture);
                GUI.color = item.Enemy ? new Color(1f, .82f, .76f) : Color.white;
                GUI.Label(nameRect, item.Label, labelStyle);
                currentY += LabelHeight + 2f;
            }

            Rect barBg = new Rect(rect.x + 1f, currentY, rect.width - 2f,
                BarAreaHeight + (item.Shield > 0f ? ShieldExtraHeight : 0f));
            GUI.color = item.Selected ? PanelSelected : PanelColor;
            GUI.DrawTexture(barBg, Texture2D.whiteTexture);

            float x = rect.x + 4f;
            float barWidth = rect.width - 8f;
            float hpY = currentY + 2f;
            Bar(new Rect(x, hpY, barWidth, 8f), item.Health,
                item.Enemy ? new Color(.96f, .40f, .38f) : new Color(.36f, .88f, .52f));

            float manaY = hpY + 9f;
            if (item.ShowMana)
                Bar(new Rect(x, manaY, barWidth, 4f), item.Mana, new Color(.39f, .65f, 1f));
            if (item.Shield > 0f)
                Bar(new Rect(x, manaY + 5f, barWidth, 2f), item.Shield, new Color(.96f, .83f, .45f));

            currentY += barBg.height + 1f;
            if (equippedCount > 0)
            {
                float rowWidth = equippedCount * ItemIconSize + (equippedCount - 1) * ItemGap;
                float left = rect.center.x - rowWidth * .5f;
                for (int i = 0; i < equippedCount; i++)
                {
                    Rect icon = new Rect(left + i * (ItemIconSize + ItemGap), currentY, ItemIconSize, ItemIconSize);
                    GUI.color = IconBacking;
                    GUI.DrawTexture(new Rect(icon.x - 1f, icon.y - 1f, icon.width + 2f, icon.height + 2f), Texture2D.whiteTexture);
                    GUI.color = Color.white;
                    MonsterTacticsItemArt.Draw(icon, equipped.Items[i]);
                    if (icon.Contains(Event.current.mousePosition))
                    {
                        hovered = equipped.Items[i];
                        hoveredAny = true;
                    }
                }
                currentY += ItemStripHeight;
            }

            if (item.HasDebuffs)
            {
                EnsureDebuffIcons();
                int count = (item.SunderRemaining > 0f ? 1 : 0) + (item.ShredRemaining > 0f ? 1 : 0) +
                    (item.WoundRemaining > 0f ? 1 : 0) + (item.BurnRemaining > 0f ? 1 : 0) +
                    (item.AttackSpeedSlowRemaining > 0f ? 1 : 0) +
                    (item.ShadowThiefRemaining > 0f ? 1 : 0) +
                    (item.TemporaryDefenseRemaining > 0f || item.TemporaryShieldRemaining > 0f ? 1 : 0);
                const float size = 13f;
                const float spacing = 2f;
                float iconsWidth = count * size + (count - 1) * spacing;
                float atX = rect.x + (rect.width - iconsWidth) * .5f;
                float atY = currentY;
                GUI.color = Color.white;
                if (item.SunderRemaining > 0f)
                {
                    GUI.DrawTexture(new Rect(atX, atY, size, size), sunderIcon);
                    atX += size + spacing;
                }
                if (item.ShredRemaining > 0f)
                {
                    GUI.DrawTexture(new Rect(atX, atY, size, size), shredIcon);
                    atX += size + spacing;
                }
                if (item.WoundRemaining > 0f)
                {
                    GUI.DrawTexture(new Rect(atX, atY, size, size), woundIcon);
                    atX += size + spacing;
                }
                if (item.BurnRemaining > 0f)
                    GUI.DrawTexture(new Rect(atX, atY, size, size), burnIcon);
                if (item.BurnRemaining > 0f) atX += size + spacing;
                if (item.AttackSpeedSlowRemaining > 0f)
                {
                    GUI.DrawTexture(new Rect(atX, atY, size, size), iceSlowIcon);
                    atX += size + spacing;
                }
                if (item.ShadowThiefRemaining > 0f)
                {
                    GUI.DrawTexture(new Rect(atX, atY, size, size), shadowThiefIcon);
                    atX += size + spacing;
                }
                if (item.TemporaryDefenseRemaining > 0f || item.TemporaryShieldRemaining > 0f)
                    GUI.DrawTexture(new Rect(atX, atY, size, size), forestGuardIcon);
            }
            GUI.color = before;
            return hoveredAny;
        }

        private static void EnsureDebuffIcons()
        {
            if (sunderIcon == null) sunderIcon = MakeDebuffIcon(0);
            if (shredIcon == null) shredIcon = MakeDebuffIcon(1);
            if (woundIcon == null) woundIcon = MakeDebuffIcon(2);
            if (burnIcon == null) burnIcon = MakeDebuffIcon(3);
            if (iceSlowIcon == null) iceSlowIcon = MakeDebuffIcon(4);
            if (shadowThiefIcon == null) shadowThiefIcon = MakeDebuffIcon(5);
            if (forestGuardIcon == null) forestGuardIcon = MakeDebuffIcon(6);
        }

        private static Texture2D MakeDebuffIcon(int kind)
        {
            const int size = 18;
            Texture2D image = new Texture2D(size, size, TextureFormat.RGBA32, false);
            image.name = kind == 0 ? "MonsterTactics Sunder" : kind == 1 ? "MonsterTactics Shred"
                : kind == 2 ? "MonsterTactics Wound" : kind == 3 ? "MonsterTactics Burn"
                : kind == 4 ? "MonsterTactics Ice Slow" : kind == 5 ?
                    "MonsterTactics Shadow Thief" : "MonsterTactics Forest Guard";
            image.filterMode = FilterMode.Point;
            image.wrapMode = TextureWrapMode.Clamp;
            Color32 clear = new Color32(0, 0, 0, 0);
            Color32 border = new Color32(15, 20, 33, 255);
            Color32 fill = kind == 0 ? new Color32(235, 111, 62, 255)
                : kind == 1 ? new Color32(174, 110, 244, 255)
                : kind == 2 ? new Color32(76, 215, 168, 255)
                : kind == 3 ? new Color32(255, 114, 49, 255)
                : kind == 4 ? new Color32(104, 214, 255, 255)
                : kind == 5 ? new Color32(165, 92, 235, 255)
                : new Color32(104, 231, 126, 255);
            Color32 mark = new Color32(17, 28, 42, 255);
            for (int y = 0; y < size; ++y)
                for (int x = 0; x < size; ++x)
                    image.SetPixel(x, y, clear);
            for (int y = 1; y < size - 1; ++y)
                for (int x = 1; x < size - 1; ++x)
                    if ((x >= 3 && x <= 14) || (y >= 3 && y <= 14))
                        SetPixelTop(image, x, y, border);

            if (kind == 6)
            {
                // Green pixel shield + leaf mark: temporary ally protection.
                for (int y = 3; y <= 13; y++)
                    for (int x = 4; x <= 13; x++)
                        if (y < 8 || Mathf.Abs(x - 8.5f) <= (14 - y) * .82f)
                            SetPixelTop(image, x, y, fill);
                for (int i = 0; i < 5; i++)
                {
                    SetPixelTop(image, 6 + i, 8 - (i / 2), mark);
                    SetPixelTop(image, 8, 7 + i, mark);
                }
                SetPixelTop(image, 8, 6, new Color32(220, 255, 223, 255));
            }
            else if (kind == 5)
            {
                // Purple crescent and bright glowing eye: compact Shadow Thief icon.
                for (int y = 3; y <= 14; ++y)
                    for (int x = 3; x <= 14; ++x)
                    {
                        float dx = x - 8f, dy = y - 8f;
                        float cutDx = x - 11f;
                        if (dx * dx + dy * dy <= 37f && cutDx * cutDx + dy * dy > 23f)
                            SetPixelTop(image, x, y, fill);
                    }
                for (int x = 5; x <= 11; ++x) SetPixelTop(image, x, 12, fill);
                SetPixelTop(image, 6, 6, new Color32(239, 213, 255, 255));
                SetPixelTop(image, 7, 6, new Color32(239, 213, 255, 255));
            }
            else if (kind == 4)
            {
                // 18px frosty snowflake in the same pixel-art style and size
                // as the existing Sunder, Shred, Wound and Burn status icons.
                for (int n = 3; n <= 14; n++)
                {
                    SetPixelTop(image, 8, n, fill);
                    SetPixelTop(image, 9, n, fill);
                    SetPixelTop(image, n, 8, fill);
                    SetPixelTop(image, n, 9, fill);
                }
                for (int n = 4; n <= 13; n++)
                {
                    SetPixelTop(image, n, n, fill);
                    SetPixelTop(image, n, 17 - n, fill);
                }
                // Branches suggest six crystalline arms; dark core adds contrast.
                foreach (int n in new int[] { 5, 12 })
                {
                    SetPixelTop(image, 6, n, fill);
                    SetPixelTop(image, 11, n, fill);
                    SetPixelTop(image, n, 6, fill);
                    SetPixelTop(image, n, 11, fill);
                }
                for (int y = 7; y <= 10; y++)
                    for (int x = 7; x <= 10; x++) SetPixelTop(image, x, y, mark);
                SetPixelTop(image, 8, 8, new Color32(205, 248, 255, 255));
                SetPixelTop(image, 9, 9, new Color32(205, 248, 255, 255));
            }
            else if (kind == 3)
            {
                for (int y = 3; y < 16; y++)
                    for (int x = 3; x < 15; x++)
                    {
                        int width = y < 8 ? 2 + y / 3 : (16 - y) / 2 + 1;
                        if (Mathf.Abs(x - 8) <= width)
                            SetPixelTop(image, x, y, fill);
                    }
                for (int y = 10; y < 15; y++)
                    for (int x = 7; x <= 9; x++)
                        SetPixelTop(image, x, y, mark);
            }
            else if (kind == 2)
            {
                for (int y = 4; y <= 13; ++y)
                    for (int x = 3; x <= 14; ++x)
                    {
                        bool heart = y <= 7
                            ? ((x >= 4 && x <= 7) || (x >= 10 && x <= 13) || (y >= 6 && x >= 3 && x <= 14))
                            : Mathf.Abs(x - 8.5f) <= (14f - y) * .94f;
                        if (heart) SetPixelTop(image, x, y, fill);
                    }
                for (int step = 0; step < 12; ++step)
                {
                    int x = 3 + step;
                    int y = 14 - step;
                    SetPixelTop(image, x, y, mark);
                    SetPixelTop(image, x, y + 1, mark);
                }
            }
            else
            {
                for (int y = 3; y <= 14; ++y)
                    for (int x = 4; x <= 13; ++x)
                    {
                        bool shield = y < 9 || Mathf.Abs(x - 8.5f) <= (15 - y) * .9f;
                        if (shield) SetPixelTop(image, x, y, fill);
                    }
                for (int y = 5; y <= 10; ++y)
                    for (int x = 8; x <= 9; ++x) SetPixelTop(image, x, y, mark);
                for (int x = 6; x <= 11; ++x) SetPixelTop(image, x, 10, mark);
                for (int x = 7; x <= 10; ++x) SetPixelTop(image, x, 11, mark);
                for (int x = 8; x <= 9; ++x) SetPixelTop(image, x, 12, mark);
            }
            image.Apply(false, true);
            return image;
        }

        private static void SetPixelTop(Texture2D tex, int x, int y, Color color)
        {
            if (x >= 0 && x < tex.width && y >= 0 && y < tex.height)
                tex.SetPixel(x, tex.height - 1 - y, color);
        }

        private static void Bar(Rect rect, float fraction, Color fill)
        {
            Color old = GUI.color;
            GUI.color = new Color(.08f, .11f, .17f, 1f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = fill;
            GUI.DrawTexture(new Rect(rect.x, rect.y,
                rect.width * Mathf.Clamp01(fraction), rect.height), Texture2D.whiteTexture);
            GUI.color = old;
        }
    }
}
