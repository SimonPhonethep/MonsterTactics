using System;
using UnityEngine;

namespace MonsterTactics
{
    /// <summary>
    /// U6.9.1b: compact TFT-inspired augment selection. Hide/Show or Tab lets you
    /// inspect the board without pausing the shared augment/combat countdown.
    /// Does not modify match state; all choices go through AugmentManager.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MonsterTacticsAugmentManager))]
    public sealed class MonsterTacticsAugmentSelectionUI : MonoBehaviour
    {
        private MonsterTacticsAugmentManager manager;
        private static MonsterTacticsAugmentSelectionUI instance;
        private bool collapsed;
        private GUIStyle header, subhead, title, description, caption, button, tiny;
        private GUIStyle activeLabel, tooltipTitle, tooltipBody;
        private static readonly Color Rookie = new Color(.67f, .76f, .85f);
        private static readonly Color Ace = new Color(.96f, .74f, .35f);
        private static readonly Color Champion = new Color(.71f, .50f, .97f);
        private static readonly Color Panel = new Color(.075f, .105f, .165f, .99f);
        private static readonly Color Card = new Color(.125f, .16f, .24f, .99f);
        private static readonly Color White = new Color(.95f, .97f, 1f, 1f);

        // Evaluated dynamically so script execution order can't leave one-frame
        // stale flags. When collapsed, board + benched Monster overlays return.
        public static bool SuppressUnitOverlays =>
            instance != null && instance.manager != null &&
            instance.manager.IsSelectionOpen && !instance.collapsed;

        private void Awake()
        {
            manager = GetComponent<MonsterTacticsAugmentManager>();
            instance = this;
        }
        private void OnEnable() { instance = this; }
        private void OnDisable() { if (instance == this) instance = null; }
        private void Styles()
        {
            if (header != null) return;
            header = Make(23, FontStyle.Bold, TextAnchor.MiddleCenter, White);
            subhead = Make(13, FontStyle.Normal, TextAnchor.MiddleCenter, new Color(.76f,.85f,.95f));
            title = Make(16, FontStyle.Bold, TextAnchor.MiddleCenter, White);
            description = Make(13, FontStyle.Normal, TextAnchor.UpperCenter, new Color(.88f,.92f,.97f));
            description.wordWrap = true;
            caption = Make(12, FontStyle.Bold, TextAnchor.MiddleCenter, White);
            tiny = Make(11, FontStyle.Normal, TextAnchor.MiddleCenter, new Color(.78f,.85f,.92f));
            activeLabel = Make(12, FontStyle.Bold, TextAnchor.MiddleLeft, White);
            tooltipTitle = Make(13, FontStyle.Bold, TextAnchor.MiddleLeft, White);
            tooltipBody = Make(12, FontStyle.Normal, TextAnchor.UpperLeft,
                new Color(.81f, .88f, .95f, 1f));
            tooltipBody.wordWrap = true;
            button = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter, wordWrap = false
            };
        }
        private static GUIStyle Make(int size, FontStyle font, TextAnchor alignment, Color color)
        {
            var s = new GUIStyle(GUI.skin.label) { fontSize = size, fontStyle = font,
                alignment = alignment, wordWrap = false, clipping = TextClipping.Clip };
            s.normal.textColor = color;
            return s;
        }
        private static Color TierColor(MonsterTacticsAugmentTier tier) =>
            tier == MonsterTacticsAugmentTier.Rookie ? Rookie :
            tier == MonsterTacticsAugmentTier.Ace ? Ace : Champion;
        private static void Fill(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }
        private static Color Accent(MonsterTacticsAugmentSnapshot a)
        {
            if (a == null) return Rookie;
            return a.UseCustomAccent ? a.AccentColor : TierColor(a.Tier);
        }

        // UI-safe handling for ordinary Sprite-mode PNGs and sliced atlas sprites.
        // Tight/rotated packed sprites can have no usable textureRect; fall back.
        private static bool DrawSprite(Sprite sprite, Rect rect, float opacity = 1f)
        {
            if (sprite == null || sprite.texture == null) return false;
            try
            {
                Rect t = sprite.textureRect;
                Texture2D texture = sprite.texture;
                if (t.width <= 0f || t.height <= 0f || texture.width <= 0 || texture.height <= 0)
                    return false;
                float aspect = t.width / t.height;
                Rect fit = rect;
                if (aspect > rect.width / Mathf.Max(1f, rect.height))
                {
                    fit.height = rect.width / aspect;
                    fit.y += (rect.height - fit.height) * .5f;
                }
                else
                {
                    fit.width = rect.height * aspect;
                    fit.x += (rect.width - fit.width) * .5f;
                }
                Color previous = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(opacity));
                GUI.DrawTextureWithTexCoords(fit, texture, new Rect(
                    t.x / texture.width, t.y / texture.height,
                    t.width / texture.width, t.height / texture.height));
                GUI.color = previous;
                return true;
            }
            catch (UnityException)
            {
                // Unity warns on an unsupported packed sprite: show the fallback.
                return false;
            }
        }

        private static string CategoryGlyph(MonsterTacticsAugmentCategory category)
        {
            switch (category)
            {
                case MonsterTacticsAugmentCategory.Economy: return "$";
                case MonsterTacticsAugmentCategory.Shop: return "S";
                case MonsterTacticsAugmentCategory.Combat: return "X";
                case MonsterTacticsAugmentCategory.Traits: return "T";
                case MonsterTacticsAugmentCategory.Items: return "I";
                case MonsterTacticsAugmentCategory.Monster: return "P";
                default: return "*";
            }
        }

        private void DrawIconPlate(Rect rect, MonsterTacticsAugmentSnapshot aug, Color tier,
            bool includeBadge)
        {
            Color accent = Accent(aug);
            // Small luminous frame; no asset dependency or blocking missing icons.
            Fill(new Rect(rect.x - 4f, rect.y - 4f, rect.width + 8f, rect.height + 8f),
                new Color(tier.r, tier.g, tier.b, .25f));
            Fill(rect, new Color(.045f, .072f, .13f, .98f));
            Fill(new Rect(rect.x, rect.y, rect.width, 2f), accent);
            Fill(new Rect(rect.x, rect.yMax - 2f, rect.width, 2f), tier);
            if (!DrawSprite(aug.Icon,
                new Rect(rect.x + 8f, rect.y + 8f, rect.width - 16f, rect.height - 16f)))
            {
                var fallback = new GUIStyle(title)
                {
                    fontSize = Mathf.RoundToInt(Mathf.Clamp(rect.height * .5f, 15f, 40f))
                };
                GUI.Label(rect, CategoryGlyph(aug.Category), fallback);
            }
            if (includeBadge)
            {
                var b = new Rect(rect.xMax - 12f, rect.yMax - 17f, 28f, 28f);
                Fill(new Rect(b.x - 2f, b.y - 2f, b.width + 4f, b.height + 4f), tier);
                Fill(b, Panel);
                if (!DrawSprite(aug.CategoryBadge, new Rect(b.x + 3f, b.y + 3f, 22f, 22f)))
                    GUI.Label(b, CategoryGlyph(aug.Category), caption);
            }
        }

        private static string EffectDescription(MonsterTacticsAugmentSnapshot a, MonsterTacticsAugmentSlots slot)
        {
            string custom = a.DescriptionFor(slot);
            if (!string.IsNullOrWhiteSpace(custom) &&
                !custom.StartsWith("Sample:", StringComparison.OrdinalIgnoreCase)) return custom;
            // The generated U6.9.0 samples still have placeholder descriptions.
            // Use their actual effect data until each one gets custom copy in Editor.
            if (a.Effects.Length == 0 || a.Effects[0] == null) return "Special augment.";
            var e = a.Effects[0];
            var variant = a.Variant(slot);
            int value = e.value + (variant != null ? variant.effectValueBonus : 0);
            string prefix = e.trigger == MonsterTacticsAugmentTrigger.PlanningStarted ? "Each planning round: " :
                e.trigger == MonsterTacticsAugmentTrigger.RoundResolved ? "After each round: " : "";
            switch (e.kind)
            {
                case MonsterTacticsAugmentEffectKind.Gold:
                    return prefix + "Gain " + value + " gold.";
                case MonsterTacticsAugmentEffectKind.FreeRerolls:
                    return prefix + "Gain " + value + " free shop reroll" + (value == 1 ? "." : "s.");
                case MonsterTacticsAugmentEffectKind.Experience:
                    return prefix + "Gain " + value + " XP.";
                case MonsterTacticsAugmentEffectKind.ItemReward:
                    return prefix + "Gain " + MonsterTacticsItemTypes.Name(e.item) + ".";
                case MonsterTacticsAugmentEffectKind.MonsterPartCapsule:
                    return prefix + "Gain " + value + " Crafting Part Capsule(s). Each offers 4 components; choose one.";
                case MonsterTacticsAugmentEffectKind.MonsterGearCapsule:
                    return prefix + "Gain " + value + " Gear Capsule(s). Each offers 4 completed items; choose one.";
                case MonsterTacticsAugmentEffectKind.RuleAddition:
                    string rule = e.rule.ToString();
                    if (e.rule == MonsterTacticsRuleId.RerollCost)
                        return "Shop rerolls cost " + Mathf.Abs(value) + (value < 0 ? " less" : " more") + " gold.";
                    if (e.rule == MonsterTacticsRuleId.TeamSizeBonus)
                        return "Gain +" + value + " team capacity.";
                    return "Change " + rule + " by " + (value > 0 ? "+" : "") + value + ".";
                default: return "Special augment (unavailable).";
            }
        }


        private void OnGUI()
        {
            if (manager == null) manager = GetComponent<MonsterTacticsAugmentManager>();
            if (manager == null || !manager.IsActive) return;
            Styles();
            int oldDepth = GUI.depth;
            Matrix4x4 oldMatrix = GUI.matrix;
            GUI.depth = -900;
            try
            {
                if (!manager.IsSelectionOpen)
                {
                    collapsed = false;
                    DrawActiveSummary();
                    return;
                }
                // Key events are handled by OnGUI and work with the new Input System.
                if (Event.current.type == EventType.KeyDown &&
                    Event.current.keyCode == KeyCode.Tab)
                {
                    collapsed = !collapsed;
                    Event.current.Use();
                }
                // The clock is managed by AugmentManager.Update(), never by drawing
                // the selection. Hide/Show cannot pause or reset the deadline.
                if (collapsed)
                {
                    DrawCollapsed();
                    return;
                }

                // Full dim obscures other HUD; world labels are suppressed by
                // SuppressUnitOverlays while this panel is visible.
                Fill(new Rect(0f, 0f, Screen.width, Screen.height),
                    new Color(.008f,.015f,.035f,.91f));
                float scale = Mathf.Min(Screen.width / 1100f, Screen.height / 680f);
                scale = Mathf.Max(.50f, scale);
                GUI.matrix = Matrix4x4.Scale(new Vector3(scale,scale,1f));
                DrawSelection(Screen.width / scale, Screen.height / scale);
            }
            finally
            {
                GUI.matrix = oldMatrix;
                GUI.depth = oldDepth;
            }
        }

        private void DrawCollapsed()
        {
            float w = Mathf.Clamp(Screen.width * .45f, 300f, 530f);
            float x = (Screen.width - w) * .5f;
            Rect r = new Rect(x, 8f, w, 47f);
            Fill(r, Panel);
            Fill(new Rect(r.x, r.y, r.width, 3f), TierColor(manager.CurrentTier));
            float seconds = Mathf.Max(0f, manager.SelectionSecondsRemaining);
            GUI.Label(new Rect(r.x + 8f,r.y+7f,w-124f,30f),
                (manager.IsMixedTierOffer ? "MIXED TIERS" : manager.CurrentTier.ToString().ToUpperInvariant()) + "  •  " +
                Mathf.CeilToInt(seconds) + "s  •  " + SlotLabel(manager.CurrentSlot), caption);
            if (GUI.Button(new Rect(r.xMax-116f,r.y+9f,105f,29f),"SHOW (TAB)",button))
                collapsed = false;
        }

        private void DrawActiveSummary()
        {
            var chosen = manager.Selected;
            if (chosen == null || chosen.Count == 0) return;
            float w = Mathf.Min(270f, Screen.width - 20f);
            float x = Screen.width - w - 10f;
            Rect p = new Rect(x, 112f, w, 30f + chosen.Count * 43f);
            Fill(new Rect(p.x + 2f, p.y + 3f, p.width, p.height),
                new Color(0f, 0f, 0f, .26f));
            Fill(p, Panel);
            GUI.Label(new Rect(p.x + 9f, p.y + 3f, w - 18f, 23f), "ACTIVE AUGMENTS", caption);
            int hovered = -1;
            for (int i = 0; i < chosen.Count; i++)
            {
                var aug = chosen[i];
                float y = p.y + 29f + i * 43f;
                Rect r = new Rect(p.x + 6f, y, w - 12f, 39f);
                bool over = r.Contains(Event.current.mousePosition);
                Fill(r, over ? new Color(.20f, .26f, .38f, .98f) : Card);
                Fill(new Rect(r.x, r.y, 3f, r.height), TierColor(aug.Tier));
                DrawIconPlate(new Rect(r.x + 9f, r.y + 6f, 26f, 26f),
                    aug, TierColor(aug.Tier), false);
                GUI.Label(new Rect(r.x + 49f, r.y + 3f, r.width - 56f, 20f), aug.Name, activeLabel);
                GUI.Label(new Rect(r.x + 49f, r.y + 20f, r.width - 56f, 16f),
                    aug.Tier + "  •  " + aug.Category, tiny);
                if (over) hovered = i;
            }
            // Hover details are helpful on the smaller active-augment HUD.
            if (hovered >= 0)
            {
                var aug = chosen[hovered];
                float tw = 255f;
                float tx = p.x - tw - 8f;
                if (tx < 4f) tx = Mathf.Max(4f, p.x - 4f);
                var popup = new Rect(tx, p.y + 30f + hovered * 43f, tw, 107f);
                Fill(popup, Panel);
                Fill(new Rect(popup.x, popup.y, popup.width, 3f), TierColor(aug.Tier));
                GUI.Label(new Rect(popup.x + 10f, popup.y + 8f, tw - 20f, 19f),
                    aug.Name, tooltipTitle);
                GUI.Label(new Rect(popup.x + 10f, popup.y + 31f, tw - 20f, 68f),
                    aug.Description, tooltipBody);
            }
        }

        private void DrawSelection(float w,float h)
        {
            var offered = manager.Offered;
            if (offered.Count == 0) return;
            float pw = Mathf.Min(890f,w-28f);
            float ph = Mathf.Min(530f,h-24f);
            float x=(w-pw)*.5f, y=(h-ph)*.5f;
            Color tier=TierColor(manager.CurrentTier);
            Fill(new Rect(x+4f,y+5f,pw,ph),new Color(0f,0f,0f,.30f));
            Fill(new Rect(x,y,pw,ph),Panel);
            Fill(new Rect(x,y,pw,4f),tier);
            GUI.Label(new Rect(x+26f,y+16f,pw-52f,31f),
                manager.IsSandboxPreview ? "AUGMENT PREVIEW" : "CHOOSE YOUR AUGMENT",header);
            GUI.Label(new Rect(x+18f,y+49f,pw-36f,22f),
                (manager.IsMixedTierOffer ? "MIXED TIERS" : manager.CurrentTier.ToString().ToUpperInvariant()) + "  •  " +
                SlotLabel(manager.CurrentSlot) + "  •  " +
                Mathf.CeilToInt(Mathf.Max(0f,manager.SelectionSecondsRemaining)) + "s LEFT",subhead);
            // U6.9.1d: Use the active selection's total time, not the catalog's
            // 43/58 second standalone-preview defaults. Scheduled rounds
            // synchronize this total to the shared planning window (60s).
            float barWidth=pw-54f;
            Fill(new Rect(x+27f,y+78f,barWidth,6f),new Color(.22f,.28f,.38f));
            Fill(new Rect(x+27f,y+78f,barWidth*manager.SelectionProgress01,6f),tier);
            if(GUI.Button(new Rect(x+pw-122f,y+12f,109f,28f),"HIDE (TAB)",button))
            {
                collapsed=true;
                return;
            }
            // U6.9.8: one glance reveals the full reroll budget for this offering.
            // Charges remain per-card; this display does NOT pool or refill them.
            DrawRerollBudget(new Rect(x+27f,y+91f,pw-54f,25f), offered.Count);
            // Snapshot because choosing immediately clears Offered.
            var visible = new MonsterTacticsAugmentSnapshot[offered.Count];
            for (int i = 0; i < offered.Count; i++) visible[i] = offered[i];
            float gap=10f, cardW=(pw-38f-gap*2f)/3f;
            float cardY=y+123f, cardH=ph-179f;
            float groupW = visible.Length * cardW + (visible.Length-1)*gap;
            float startX = x + (pw-groupW)*.5f;
            for(int i=0;i<visible.Length;i++)
            {
                Rect r=new Rect(startX+i*(cardW+gap),cardY,cardW,cardH);
                DrawCard(r,visible[i],i,TierColor(visible[i].Tier));
                if(!manager.IsSelectionOpen) return;
            }
            GUI.Label(new Rect(x+16f,y+ph-45f,pw-32f,21f),
                manager.IsSandboxPreview?
                "UI TEST • no rewards or selection slots used • Hide to inspect":
                (visible.Length < 3 ? "Limited elidunebit pool • " : "") +
                "Choose before combat • Timeout picks a visible card • Hide to inspect",tiny);
            if(manager.IsSandboxPreview &&
                GUI.Button(new Rect(x+pw-146f,y+ph-27f,130f,25f),"CLOSE TEST",button))
                manager.CloseSandboxPreview();
        }

        // The manager reports remaining charges for each displayed card. New stages
        // start a fresh set based on Catalog.rerollsPerCard (default 1 each).
        private void DrawRerollBudget(Rect r, int visibleCardCount)
        {
            int perCard = Mathf.Max(0, manager.RerollsPerCard);
            int total = Mathf.Max(0, visibleCardCount * perCard);
            int remaining = Mathf.Clamp(manager.RemainingOfferRerolls, 0, total);
            Fill(r, new Color(.12f,.16f,.24f,.92f));
            Fill(new Rect(r.x,r.y,3f,r.height), TierColor(manager.CurrentTier));
            GUI.Label(new Rect(r.x+9f,r.y+2f,235f,r.height-4f),
                "AUGMENT REROLLS  " + remaining + " / " + total, caption);
            // Small pips: filled means available, dim means spent. One bank
            // summary plus a separate charge indicator on every card below.
            int pips = Mathf.Min(total, 15);
            const float pipW = 14f, gap = 4f;
            float start = r.xMax-10f-(pips*(pipW+gap)-gap);
            for (int i=0;i<pips;i++)
                Fill(new Rect(start+i*(pipW+gap),r.y+7f,pipW,11f),
                    i < remaining ? new Color(.80f,.90f,1f,.95f) :
                    new Color(.33f,.39f,.49f,.60f));
        }

        private static string SlotLabel(MonsterTacticsAugmentSlots slot) =>
            slot == MonsterTacticsAugmentSlots.First ? "2-1" :
            slot == MonsterTacticsAugmentSlots.Second ? "3-2" : "4-2";

        private void DrawCard(Rect r,MonsterTacticsAugmentSnapshot aug,int index,Color tier)
        {
            Color accent = Accent(aug);
            Fill(new Rect(r.x + 3f, r.y + 4f, r.width, r.height),
                new Color(0f, 0f, 0f, .25f));
            if (aug.ShowGlow)
                Fill(new Rect(r.x - 2f, r.y - 2f, r.width + 4f, r.height + 4f),
                    new Color(tier.r, tier.g, tier.b, .16f));
            Fill(r, Card);
            Fill(new Rect(r.x, r.y, r.width, 4f), tier);
            Fill(new Rect(r.x + 1f, r.y + 4f, 2f, r.height - 4f),
                new Color(tier.r, tier.g, tier.b, .42f));
            Fill(new Rect(r.xMax - 3f, r.y + 4f, 2f, r.height - 4f),
                new Color(tier.r, tier.g, tier.b, .42f));
            Fill(new Rect(r.x + 7f, r.y + 7f, r.width - 14f, 155f),
                new Color(accent.r, accent.g, accent.b, .095f));
            if (aug.CardArtwork != null)
            {
                var art = new Rect(r.x + 8f, r.y + 9f, r.width - 16f, 147f);
                DrawSprite(aug.CardArtwork, art, aug.ArtworkOpacity);
            }
            int cardRemaining = manager.CardRerollsRemaining(index);
            int cardBudget = Mathf.Max(0, manager.RerollsPerCard);
            Rect rerollPill = new Rect(r.xMax-91f,r.y+11f,80f,21f);
            Fill(rerollPill, new Color(.055f,.075f,.12f,.93f));
            GUI.Label(rerollPill, "REROLL " + cardRemaining + "/" + cardBudget, tiny);
            float cx = r.center.x;
            const float iconSize = 87f;
            DrawIconPlate(new Rect(cx - iconSize*.5f, r.y + 20f, iconSize, iconSize),
                aug, tier, true);
            GUI.Label(new Rect(r.x + 9f, r.y + 119f, r.width - 18f, 26f),
                aug.Name, title);
            GUI.Label(new Rect(r.x + 9f, r.y + 146f, r.width - 18f, 20f),
                (aug.Tier + "  •  " + aug.Category).ToUpperInvariant(), tiny);
            Fill(new Rect(r.x + 21f, r.y + 174f, r.width - 42f, 1f),
                new Color(accent.r, accent.g, accent.b, .36f));
            float footer = r.yMax - 79f;
            var desc = new GUIStyle(description) { alignment = TextAnchor.UpperCenter };
            float descH = Mathf.Max(22f, footer - (r.y + 183f) - 8f);
            GUI.Label(new Rect(r.x + 14f, r.y + 183f, r.width - 28f, descH),
                EffectDescription(aug, manager.CurrentSlot), desc);
            if (GUI.Button(new Rect(r.x + 10f, footer, r.width - 20f, 34f),
                manager.IsSandboxPreview ? "TEST CHOOSE" : "CHOOSE", button))
                manager.ChooseVisible(index);
            if (!manager.IsSelectionOpen) return;
            int charges = manager.CardRerollsRemaining(index);
            bool oldEnabled = GUI.enabled;
            GUI.enabled = charges > 0;
            if (GUI.Button(new Rect(r.x + 10f, footer + 40f, r.width - 20f, 31f),
                charges > 0 ? "REROLL  •  " + charges + " LEFT" : "NO REROLLS LEFT", button))
                manager.TryRerollCard(index);
            GUI.enabled = oldEnabled;
        }
    }
}
