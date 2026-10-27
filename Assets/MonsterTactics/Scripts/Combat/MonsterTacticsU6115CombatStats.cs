using System;
using System.Collections.Generic;
using UnityEngine;

namespace MonsterTactics
{
    /// <summary>
    /// U6.11.5: TFT-inspired combat contribution recap. One record per actual Fighter,
    /// so duplicate copies remain distinct. Never changes combat outcomes or damage.
    /// Records survive ResetBattle for last-round review until StartBattle is called.
    /// </summary>
    public sealed partial class MonsterTacticsCombatDirector
    {
        private sealed class U6115Entry
        {
            public Fighter Fighter;
            public string Name;
            public bool Enemy;
            public long Dealt;
            public long Taken;
            public long Healing;
            public long Shielding;
        }

        private readonly Dictionary<Fighter, U6115Entry> u6115ByFighter =
            new Dictionary<Fighter, U6115Entry>();
        private readonly List<U6115Entry> u6115Entries = new List<U6115Entry>();
        private bool u6115HasRound;
        private bool u6115PanelOpen = true;
        private bool u6115ShowEnemies;
        private Fighter U6115ReflectionOwner;
        // 0 Damage dealt, 1 Damage taken, 2 Healing + shielding.
        private int u6115Tab;
        private Vector2 u6115Scroll;
        private GUIStyle u6115Title, u6115Text, u6115Dim, u6115Number, u6115Mini;
        private readonly Color u6115Back = new Color(.035f,.072f,.111f,.96f);
        private readonly Color u6115BarBackground = new Color(.15f,.23f,.30f,.95f);
        private readonly Color[] u6115BarColors = {
            new Color(.94f,.63f,.32f),
            new Color(.60f,.76f,.98f),
            new Color(.50f,.90f,.69f)
        };

        private void U6115BeginRoundStats()
        {
            u6115Entries.Clear();
            u6115ByFighter.Clear();
            u6115HasRound = true;
            u6115ShowEnemies = false;
            u6115Scroll = Vector2.zero;
        }

        private U6115Entry U6115Ensure(Fighter fighter)
        {
            if (fighter == null) return null;
            U6115Entry result;
            if (u6115ByFighter.TryGetValue(fighter, out result)) return result;
            result = new U6115Entry {
                Fighter = fighter,
                Name = string.IsNullOrEmpty(fighter.Name) ? "Monster" : fighter.Name,
                Enemy = fighter.Enemy
            };
            u6115ByFighter.Add(fighter, result);
            u6115Entries.Add(result);
            return result;
        }

        private void U6115TrackDamage(Fighter source, Fighter target, int health, int shield,
            string label)
        {
            if (!u6115HasRound) return;
            int effective = Mathf.Max(0, health) + Mathf.Max(0, shield);
            if (effective <= 0) return;
            U6115Entry recipient = U6115Ensure(target);
            if (recipient != null) recipient.Taken += effective;
            // Burn damage is applied with null attacker to preserve legacy damage
            // logic; its recorded owner is used for statistics only, never procs.
            if (source == null && string.Equals(label, "BURN", StringComparison.Ordinal))
                source = target != null ? target.U6115BurnOwner : null;
            if (source == null && string.Equals(label, "IRON FORTRESS", StringComparison.Ordinal))
                source = U6115ReflectionOwner;
            U6115Entry dealer = U6115Ensure(source);
            if (dealer != null) dealer.Dealt += effective;
        }

        private void U6115TrackHealing(Fighter healer, int actualHealing)
        {
            if (!u6115HasRound || actualHealing <= 0) return;
            U6115Entry entry = U6115Ensure(healer);
            if (entry != null) entry.Healing += actualHealing;
        }

        private void U6115TrackShield(Fighter source, int newlyGrantedShield)
        {
            if (!u6115HasRound || newlyGrantedShield <= 0) return;
            U6115Entry entry = U6115Ensure(source);
            if (entry != null) entry.Shielding += newlyGrantedShield;
        }

        private void U6115InitializeStyles()
        {
            if (u6115Title != null) return;
            u6115Title = new GUIStyle(GUI.skin.label) {
                fontSize = 13, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip
            };
            u6115Title.normal.textColor = Color.white;
            u6115Text = new GUIStyle(GUI.skin.label) {
                fontSize = 11, fontStyle = FontStyle.Bold, clipping = TextClipping.Clip
            };
            u6115Text.normal.textColor = Color.white;
            u6115Dim = new GUIStyle(GUI.skin.label) {
                fontSize = 10, clipping = TextClipping.Clip
            };
            u6115Dim.normal.textColor = new Color(.77f,.85f,.92f);
            u6115Number = new GUIStyle(u6115Text) { alignment = TextAnchor.MiddleRight };
            u6115Mini = new GUIStyle(u6115Dim) { fontSize = 10 };
        }

        private static string U6115Format(long value)
        {
            if (value >= 1000000L) return (value / 1000000f).ToString("0.0") + "m";
            if (value >= 10000L) return (value / 1000f).ToString("0.0") + "k";
            return value.ToString("N0");
        }

        private long U6115Value(U6115Entry entry)
        {
            return u6115Tab == 0 ? entry.Dealt :
                u6115Tab == 1 ? entry.Taken : entry.Healing + entry.Shielding;
        }

        private void U6115DrawCombatStats()
        {
            if (!u6115HasRound) return;
            U6115InitializeStyles();
            // Collect zero-output units too, including defeated ones. Fighters stay
            // in these lists through results; records then persist through reset.
            foreach (Fighter fighter in players) U6115Ensure(fighter);
            foreach (Fighter fighter in opponents) U6115Ensure(fighter);

            float screenW = Screen.width;
            float screenH = Screen.height;
            if (screenW < 310f || screenH < 230f) return;
            // U6.11.5b: TFT-inspired right-side damage recap. The compact panel
            // docks at the extreme right, above the shop, away from the centre.
            // It can be collapsed at any time using the right-edge header.
            float width = Mathf.Min(242f, screenW - 16f);
            float left = screenW - width - 10f;
            float top = Mathf.Min(95f, Mathf.Max(10f, screenH * .13f));
            float titleHeight = 30f;
            float bottomReserve = Mathf.Min(130f, screenH * .20f);
            float available = Mathf.Max(125f, screenH - top - bottomReserve - titleHeight - 5f);
            float height = Mathf.Min(425f, available);
            Rect toggle = new Rect(left, top, width, titleHeight);
            Color wasColor = GUI.color;
            GUI.color = u6115Back;
            GUI.Box(toggle, GUIContent.none);
            GUI.color = wasColor;
            if (GUI.Button(new Rect(toggle.x + 2, toggle.y + 2,
                toggle.width - 4, toggle.height - 4),
                u6115PanelOpen ? "COMBAT STATS  ▼" : "COMBAT STATS  ▶"))
                u6115PanelOpen = !u6115PanelOpen;
            if (!u6115PanelOpen) return;
            Rect bounds = new Rect(left, top + titleHeight + 3f, width, height);
            GUI.color = u6115Back;
            GUI.Box(bounds, GUIContent.none);
            GUI.color = wasColor;
            float x = bounds.x + 10f;
            float y = bounds.y + 6f;
            float contentWidth = bounds.width - 20f;
            string phaseLabel = phase == Phase.Battle ? "LIVE ROUND" :
                phase == Phase.Results ? "ROUND RESULTS" : "PREVIOUS ROUND";
            GUI.Label(new Rect(x, y, contentWidth, 20f), phaseLabel, u6115Title);
            y += 25f;
            float tabWidth = (contentWidth - 8f) / 3f;
            string[] names = { "DEALT", "TAKEN", "HEAL/SHLD" };
            for (int tab = 0; tab < names.Length; tab++)
            {
                float tx = x + tab * (tabWidth + 4f);
                if (GUI.Button(new Rect(tx, y, tabWidth, 26f),
                    (u6115Tab == tab ? "* " : "") + names[tab]))
                {
                    u6115Tab = tab;
                    u6115Scroll = Vector2.zero;
                }
            }
            y += 33f;
            float sideWidth = (contentWidth - 5f) * .5f;
            if (GUI.Button(new Rect(x, y, sideWidth, 25f),
                (!u6115ShowEnemies ? "* " : "") + "MY TEAM"))
            {
                u6115ShowEnemies = false;
                u6115Scroll = Vector2.zero;
            }
            if (GUI.Button(new Rect(x + sideWidth + 5f, y, sideWidth, 25f),
                (u6115ShowEnemies ? "* " : "") + "ENEMIES"))
            {
                u6115ShowEnemies = true;
                u6115Scroll = Vector2.zero;
            }
            y += 30f;
            string help = u6115Tab == 0 ? "Actual HP + shield damage (no overkill)" :
                u6115Tab == 1 ? "Actual HP damage + shield absorption" :
                "Effective healing + newly granted shielding";
            GUI.Label(new Rect(x, y, contentWidth, 17f), help, u6115Mini);
            y += 22f;
            List<U6115Entry> displayed = new List<U6115Entry>();
            foreach (U6115Entry entry in u6115Entries)
                if (entry.Enemy == u6115ShowEnemies) displayed.Add(entry);
            displayed.Sort((a, b) => {
                int c = U6115Value(b).CompareTo(U6115Value(a));
                return c != 0 ? c : string.CompareOrdinal(a.Name,b.Name);
            });
            long biggest = 1;
            foreach (U6115Entry entry in displayed)
                biggest = Math.Max(biggest, U6115Value(entry));
            float viewH = Mathf.Max(0f, bounds.yMax - y - 6f);
            Rect view = new Rect(x, y, contentWidth, viewH);
            Rect interior = new Rect(0f, 0f, Mathf.Max(1f, contentWidth - 15f),
                Mathf.Max(viewH, displayed.Count * 48f + 2f));
            u6115Scroll = GUI.BeginScrollView(view, u6115Scroll, interior, false, true);
            if (displayed.Count == 0)
                GUI.Label(new Rect(5f, 6f, interior.width - 10f, 24f),
                    "No Monster recorded for this team.", u6115Dim);
            for (int i = 0; i < displayed.Count; i++)
            {
                U6115Entry entry = displayed[i];
                long value = U6115Value(entry);
                float ry = 2f + 48f * i;
                if (i % 2 == 0)
                {
                    GUI.color = new Color(.14f,.23f,.30f,.60f);
                    GUI.DrawTexture(new Rect(0f,ry,interior.width,45f),Texture2D.whiteTexture);
                    GUI.color = wasColor;
                }
                // Show the currently animated sprite, even for fainted units.
                if (entry.Fighter != null && entry.Fighter.Unit != null)
                {
                    SpriteRenderer renderer = entry.Fighter.Unit.GetComponentInChildren<SpriteRenderer>(true);
                    Sprite sprite = renderer != null ? renderer.sprite : null;
                    if (sprite != null && sprite.texture != null)
                    {
                        Rect spriteRect = sprite.textureRect;
                        Rect uv = new Rect(spriteRect.x / sprite.texture.width,
                            spriteRect.y / sprite.texture.height,
                            spriteRect.width / sprite.texture.width,
                            spriteRect.height / sprite.texture.height);
                        GUI.DrawTextureWithTexCoords(new Rect(4f,ry+3f,39f,39f), sprite.texture, uv, true);
                    }
                }
                GUI.Label(new Rect(48f, ry + 2f, Mathf.Max(32f, interior.width - 143f), 18f), entry.Name, u6115Text);
                GUI.Label(new Rect(interior.width - 102f, ry + 2f, 95f, 18f),
                    U6115Format(value), u6115Number);
                float barW = Mathf.Max(10f, interior.width - 59f);
                Rect bar = new Rect(48f, ry + 25f, barW, 9f);
                GUI.color = u6115BarBackground;
                GUI.DrawTexture(bar, Texture2D.whiteTexture);
                GUI.color = u6115BarColors[u6115Tab];
                GUI.DrawTexture(new Rect(bar.x,bar.y,bar.width *
                    Mathf.Clamp01(value / (float)biggest),bar.height),Texture2D.whiteTexture);
                GUI.color = wasColor;
                if (u6115Tab == 2)
                    GUI.Label(new Rect(49f,ry+34f,barW,13f),
                        "Heal " + U6115Format(entry.Healing) +
                        "  |  Shield " + U6115Format(entry.Shielding), u6115Mini);
            }
            GUI.EndScrollView();
            GUI.color = wasColor;
        }
    }
}
