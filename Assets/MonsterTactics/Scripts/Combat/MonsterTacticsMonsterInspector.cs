using UnityEngine;
using System.Collections.Generic;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace MonsterTactics
{
    /// <summary>
    /// U4.3's larger, scrollable unit inspector. Reads the U4.2 combat snapshot
    /// and U4.2 ability formulas without modifying either system.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MonsterTacticsMonsterInspector : MonoBehaviour
    {
        [SerializeField] private MonsterTacticsCombatDirector combat;
        [SerializeField] private Camera boardCamera;
        [SerializeField] private bool showPanel = true;

        private MonsterUnit selected;
        private MonsterTacticsU5ShopManager shopManager;
        private MonsterTacticsU5CanvasHud canvasHud;
        private MonsterPlacementController placement;

        /// <summary>Bench Monster are inactive in the scene, so UI selection routes here.</summary>
        public void Inspect(MonsterUnit unit)
        {
            if (unit == null) return;
            if (selected != unit) scroll = Vector2.zero;
            selected = unit;
            showPanel = true;
        }
        private GUIStyle titleStyle;
        private GUIStyle sectionStyle;
        private GUIStyle bodyStyle;
        private GUIStyle captionStyle;
        private GUIStyle valueStyle;
        private GUIStyle formulaStyle;
        private GUIStyle traitNameStyle;
        private GUIStyle traitSubStyle;
        private const float TraitRowHeight = 44f;
        private Vector2 scroll;
        private float scrollHeight = 1800f;

        private static readonly Color Panel = new Color(.10f, .16f, .24f, .97f);
        private static readonly Color Surface = new Color(.15f, .23f, .33f, 1f);
        private static readonly Color Soft = new Color(.72f, .82f, .92f, 1f);
        private static readonly Color Accent = new Color(.98f, .80f, .42f, 1f);

        private void Awake()
        {
            if (combat == null) combat = GetComponent<MonsterTacticsCombatDirector>();
            if (boardCamera == null) boardCamera = Camera.main;
        }

        private void Update()
        {
            if (boardCamera == null) boardCamera = Camera.main;
            ReadPointer(out Vector2 position, out bool click, out bool toggle);
            if (toggle) showPanel = !showPanel;
            if (!click || boardCamera == null) return;
            if (shopManager == null) shopManager = FindAnyObjectByType<MonsterTacticsU5ShopManager>();
            if (shopManager != null && (shopManager.IsOverBenchArea(position) || shopManager.IsOverShopArea(position)))
                return; // Shop UI clicks must not deselect inspected bench Monster.

            Vector2 guiPointer = new Vector2(position.x, Screen.height - position.y);
            if (showPanel && selected != null && GetPanelRect().Contains(guiPointer)) return;

            RaycastHit[] hits = Physics.RaycastAll(boardCamera.ScreenPointToRay(position),
                250f, ~0, QueryTriggerInteraction.Ignore);
            MonsterUnit closest = null;
            float distance = float.MaxValue;
            foreach (RaycastHit hit in hits)
            {
                MonsterUnit unit = hit.collider.GetComponentInParent<MonsterUnit>();
                if (unit == null || hit.distance >= distance) continue;
                closest = unit;
                distance = hit.distance;
            }
            if (closest != null)
            {
                if (selected != closest) scroll = Vector2.zero;
                selected = closest;
                showPanel = true;
            }
            else selected = null;
        }

        private static void ReadPointer(out Vector2 pointer, out bool click, out bool toggle)
        {
#if ENABLE_INPUT_SYSTEM
            Mouse mouse = Mouse.current;
            Keyboard keyboard = Keyboard.current;
            pointer = mouse != null ? mouse.position.ReadValue() : Vector2.zero;
            click = mouse != null && mouse.leftButton.wasPressedThisFrame;
            toggle = keyboard != null && keyboard.iKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            pointer = Input.mousePosition;
            click = Input.GetMouseButtonDown(0);
            toggle = Input.GetKeyDown(KeyCode.I);
#else
            pointer = Vector2.zero;
            click = false;
            toggle = false;
#endif
        }

        private Rect GetPanelRect()
        {
            float width = Mathf.Min(420f, Mathf.Max(285f, Screen.width * .28f));
            float top = Screen.height < 640f ? 56f : 72f;
            float bottom = Screen.height - 24f;

            // The shop belongs to uGUI. Its real screen bounds depend on the
            // CanvasScaler and aspect ratio, so do not hardcode a shop height.
            if (MonsterTacticsU5CanvasHud.HasActiveHud)
            {
                if (canvasHud == null) canvasHud = FindAnyObjectByType<MonsterTacticsU5CanvasHud>();
                if (canvasHud != null && canvasHud.IsReady)
                {
                    float shopTopGuiY = Screen.height - canvasHud.ShopTopScreenY;
                    bottom = Mathf.Min(bottom, shopTopGuiY - 12f);
                    // The economy panel now sits on the bottom-right, directly
                    // underneath the inspector. Don't cover its income readout.
                    if (canvasHud.IncomePanelTopScreenY > 0f)
                    {
                        float incomeTopGuiY = Screen.height - canvasHud.IncomePanelTopScreenY;
                        bottom = Mathf.Min(bottom, incomeTopGuiY - 12f);
                    }
                }
            }
            float height = Mathf.Clamp(bottom - top, 115f, 870f);
            return new Rect(Mathf.Max(6f, Screen.width - width - 16f), top, width, height);
        }

        private void EnsureStyles()
        {
            if (titleStyle != null) return;
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 25, fontStyle = FontStyle.Bold };
            titleStyle.normal.textColor = Color.white;
            sectionStyle = new GUIStyle(GUI.skin.label) { fontSize = 19, fontStyle = FontStyle.Bold };
            sectionStyle.normal.textColor = Accent;
            bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, wordWrap = true };
            bodyStyle.normal.textColor = new Color(.91f, .96f, 1f);
            captionStyle = new GUIStyle(bodyStyle) { fontSize = 15 };
            captionStyle.normal.textColor = Soft;
            valueStyle = new GUIStyle(bodyStyle) { fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleRight, wordWrap = true };
            formulaStyle = new GUIStyle(bodyStyle) { fontSize = 17, fontStyle = FontStyle.Bold,
                wordWrap = true };
            formulaStyle.normal.textColor = Accent;
            traitNameStyle = new GUIStyle(bodyStyle)
            {
                fontSize = 15, fontStyle = FontStyle.Bold, clipping = TextClipping.Clip
            };
            traitSubStyle = new GUIStyle(captionStyle)
            {
                fontSize = 12, clipping = TextClipping.Clip
            };
        }

        private static void Fill(Rect box, Color color)
        {
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(box, Texture2D.whiteTexture);
            GUI.color = old;
        }

        private static void Bar(Rect box, float fraction, Color color)
        {
            Fill(box, new Color(.035f, .07f, .10f, 1f));
            float w = (box.width - 2f) * Mathf.Clamp01(fraction);
            Fill(new Rect(box.x + 1f, box.y + 1f, w, Mathf.Max(1f, box.height - 2f)), color);
        }

        private void Heading(string title, ref float y, float width)
        {
            Fill(new Rect(0f, y, width, 32f), Surface);
            GUI.Label(new Rect(9f, y + 1f, width - 18f, 29f), title, sectionStyle);
            y += 39f;
        }

        // Auto-size every line: otherwise long labels such as Burn duration and
        // Healing Received can clip on small Game views.
        private void Stat(string name, string value, ref float y, float width)
        {
            float labelWidth = width * .64f - 4f;
            float valueWidth = width * .32f;
            float height = Mathf.Max(29f,
                Mathf.Max(bodyStyle.CalcHeight(new GUIContent(name), labelWidth),
                    valueStyle.CalcHeight(new GUIContent(value), valueWidth)) + 4f);
            GUI.Label(new Rect(6f, y, labelWidth, height), name, bodyStyle);
            GUI.Label(new Rect(width * .66f, y, valueWidth, height), value, valueStyle);
            y += height + 4f;
        }

        private void WrappedText(string message, GUIStyle style, ref float y, float width,
            float spacing = 7f)
        {
            float available = Mathf.Max(100f, width - 12f);
            float height = Mathf.Max(style.lineHeight + 6f,
                style.CalcHeight(new GUIContent(message), available) + 6f);
            GUI.Label(new Rect(6f, y, available, height), message, style);
            y += height + spacing;
        }

        private void OnGUI()
        {
            if (selected == null || !showPanel) return;
            if (combat == null) combat = GetComponent<MonsterTacticsCombatDirector>();
            if (combat == null || !combat.TryGetSnapshot(selected, out MonsterTacticsCombatDirector.CombatSnapshot info))
                return;
            EnsureStyles();

            int oldDepth = GUI.depth;
            GUI.depth = -105;
            Rect bounds = GetPanelRect();
            Fill(bounds, Panel);
            GUI.Box(bounds, GUIContent.none);
            float x = bounds.x + 16f;
            float y = bounds.y + 12f;
            float w = bounds.width - 32f;
            MonsterTacticsU5Roster.Family family = MonsterTacticsU5Roster.Find(selected.MonsterName);
            GUI.Label(new Rect(x, y, w - 42f, 36f),
                selected.MonsterName + (info.IsEnemy ? " ENEMY" : "") +
                (family != null ? "  ·  " + family.Cost + "g" : ""), titleStyle);
            if (GUI.Button(new Rect(bounds.xMax - 43f, y + 1f, 28f, 27f), "X"))
            {
                selected = null;
                GUI.depth = oldDepth;
                return;
            }
            y += 40f;
            GUI.Label(new Rect(x, y, w, 28f),
                selected.MonsterType + "   /   " + selected.MonsterRole + "   /   " +
                (selected.GetComponent<MonsterTacticsU5Progression>()?.Stage ?? 1) + "★", captionStyle);
            y += 39f;

            Rect viewport = new Rect(x, y, w, Mathf.Max(60f, bounds.yMax - y - 12f));
            float inner = Mathf.Max(170f, w - 20f);
            scroll = GUI.BeginScrollView(viewport, scroll,
                new Rect(0f, 0f, inner, scrollHeight), false, true);
            float cy = 0f;
            MonsterTacticsU5Progression progression = selected.GetComponent<MonsterTacticsU5Progression>();
            int stage = progression != null ? progression.Stage : MonsterTacticsU5Roster.StageOf(selected.MonsterName);
            int baseCost = family != null ? family.Cost : 0;
            int invested = progression != null ? progression.InvestedGold : baseCost;
            // Trait list is always shown for shop Monster, even if not activated.
            // The popup is positioned later, OUTSIDE the clipped scroll view.
            string[] inspectedTraits = family != null
                ? MonsterTacticsTraits.ForUnit(selected) : new string[0];
            if (placement == null) placement = FindAnyObjectByType<MonsterPlacementController>();
            MonsterUnit[] teammates = placement != null
                ? placement.GetComponentsInChildren<MonsterUnit>(true) : new MonsterUnit[0];
            Dictionary<string, int> traitCounts = combat != null && combat.MatchPhase != "Planning" &&
                combat.PlayerTraitCounts != null ? combat.PlayerTraitCounts :
                MonsterTacticsTraits.CountDeployed(teammates);
            float traitStart = -1f;
            if (inspectedTraits.Length > 0)
            {
                Heading("TYPES & ORIGINS", ref cy, inner);
                traitStart = cy;
                for (int traitIndex = 0; traitIndex < inspectedTraits.Length; traitIndex++)
                {
                    string name = inspectedTraits[traitIndex];
                    MonsterTacticsTraits.Definition definition = MonsterTacticsTraitPresentation.Find(name);
                    int deployedCount = 0;
                    traitCounts.TryGetValue(name, out deployedCount);
                    int activeTier = info.IsEnemy ? 0 : MonsterTacticsTraits.ActiveTier(name, traitCounts);
                    float rowY = cy;
                    if (activeTier > 0) Fill(new Rect(2f, rowY, inner - 4f, TraitRowHeight - 2f),
                        new Color(.14f, .30f, .23f, .55f));
                    MonsterTacticsTraitPresentation.DrawIcon(name,
                        new Rect(7f, rowY + 7f, 28f, 28f));
                    GUI.Label(new Rect(44f, rowY + 1f, inner - 52f, 21f), name,
                        traitNameStyle);
                    string currentStatus;
                    if (info.IsEnemy) currentStatus = "Enemy · hover for trait details";
                    else if (definition == null || definition.Breakpoints.Length == 0)
                        currentStatus = "Type · no active bonus yet";
                    else currentStatus = deployedCount + " deployed · " +
                        (activeTier > 0 ? "Active" : "Inactive") +
                        " · " + MonsterTacticsTraitPresentation.BreakpointText(definition);
                    GUI.Label(new Rect(44f, rowY + 22f, inner - 50f, 19f), currentStatus,
                        traitSubStyle);
                    cy += TraitRowHeight;
                }
                cy += 9f;
            }
            Heading("SHOP VALUE & EVOLUTION", ref cy, inner);
            Stat("Shop cost", baseCost + " gold", ref cy, inner);
            Stat("Sell value", invested + " gold", ref cy, inner);
            Stat("Evolution stage", stage + "★", ref cy, inner);
            cy += 9f;
            // Item bonuses are applied in combat and planning. Crafting uses
            // the same held-items data shown here.
            Heading("EQUIPPED ITEMS", ref cy, inner);
            MonsterTacticsHeldItems equippedItems = selected.GetComponent<MonsterTacticsHeldItems>();
            if (equippedItems == null || equippedItems.Items.Count == 0)
                WrappedText("0/3 items equipped", captionStyle, ref cy, inner);
            else
            {
                for (int itemIndex = 0; itemIndex < equippedItems.Items.Count; itemIndex++)
                {
                    MonsterTacticsItemKind item = equippedItems.Items[itemIndex];
                    Texture2D itemIcon=MonsterTacticsItemArt.Icon(item);
                    if(itemIcon != null)
                    {
                        MonsterTacticsItemArt.Draw(new Rect(4f,cy+1f,38f,38f), item);
                        float itemTextY = cy;
                        // Indent text next to the item image, not over it.
                        float itemTextWidth = Mathf.Max(80f,inner-48f);
                        string itemText = MonsterTacticsItemTypes.Name(item) + " — " + MonsterTacticsItemTypes.Description(item);
                        float measured = captionStyle.CalcHeight(new GUIContent(itemText),itemTextWidth);
                        GUI.Label(new Rect(46f,itemTextY,itemTextWidth,Mathf.Max(42f,measured)),itemText,captionStyle);
                        cy += Mathf.Max(44f,measured) + 7f;
                    }
                    else WrappedText(MonsterTacticsItemTypes.Name(item) + " — " +
                        MonsterTacticsItemTypes.Description(item), captionStyle, ref cy, inner, 5f);
                }
                WrappedText(equippedItems.Items.Count + "/3 item slots filled", captionStyle, ref cy, inner);
            }
            cy += 6f;
            Heading("HEALTH", ref cy, inner);
            Bar(new Rect(4f, cy, inner - 10f, 17f),
                info.Health / (float)Mathf.Max(1, info.MaximumHealth),
                new Color(.34f, .82f, .48f));
            cy += 21f;
            GUI.Label(new Rect(4f, cy, inner - 10f, 29f),
                info.Health + " / " + info.MaximumHealth + (info.Health == 0 ? "  - FAINTED" : ""), bodyStyle);
            cy += 35f;
            if (info.Shield > 0)
            {
                Stat("Shield", info.Shield.ToString(), ref cy, inner);
            }
            Heading("MANA", ref cy, inner);
            Bar(new Rect(4f, cy, inner - 10f, 16f),
                info.Mana / (float)Mathf.Max(1, info.MaximumMana),
                new Color(.32f, .62f, .98f));
            cy += 20f;
            GUI.Label(new Rect(4f, cy, inner - 10f, 28f),
                info.Mana + " / " + info.MaximumMana, bodyStyle);
            cy += 29f;
            GUI.Label(new Rect(4f, cy, inner - 10f, 24f),
                "+20 mana / basic attack     +8 / hit taken", captionStyle);
            cy += 33f;

            Heading("COMBAT STATS", ref cy, inner);
            MonsterTacticsCombatRules.Stats st = info.Stats;
            Stat("Attack Damage (AD)", st.Attack.ToString("0"), ref cy, inner);
            Stat("Ability Power (AP)", st.AbilityPower.ToString("0") + "%", ref cy, inner);
            Stat("Attack Speed", st.AttackSpeed.ToString("0.00") + "/sec", ref cy, inner);
            // U6.9.9: This is the effective (slowed) runtime attack rate.
            // The timer and debuff magnitude update every OnGUI repaint.
            if (info.AttackSpeedSlowRemaining > 0f)
                Stat("Ice Slow -" + (info.AttackSpeedSlowFraction * 100f).ToString("0") + "% AS",
                    info.AttackSpeedSlowRemaining.ToString("0.0") + "s remaining", ref cy, inner);
            if (info.U610SwiftyBonusFraction > 0f)
                Stat("Swifty total AS bonus", "+" +
                    (100f*info.U610SwiftyBonusFraction).ToString("0.#")+"%", ref cy, inner);
            if (info.U610OverdriveRemaining > 0f)
                Stat("Overdrive active",info.U610OverdriveRemaining.ToString("0.0")+"s",ref cy,inner);
            if (info.U610BarrageRemaining > 0f)
            {
                Stat("Dragon Darts Barrage",info.U610BarrageRemaining.ToString("0.0")+"s",ref cy,inner);
                Stat("Bonus darts per attack",info.U610BarrageDarts.ToString(),ref cy,inner);
                Stat("Damage per dart (raw)",info.U610BarrageDartDamage.ToString("0.#"),ref cy,inner);
            }
            if (info.U610GritActivated) Stat("Grit", "Activated this combat",ref cy,inner);
            if (info.ShadowThiefBuffRemaining > 0f)
                Stat("Shadow Thief +AS/+Damage",
                    "+" + (info.ShadowThiefBuffAttackSpeed * 100f).ToString("0") + "% AS, +" +
                    (info.ShadowThiefBuffDamage * 100f).ToString("0") + "% dealt  ·  " +
                    info.ShadowThiefBuffRemaining.ToString("0.0") + "s", ref cy, inner);
            if (info.ShadowThiefDebuffRemaining > 0f)
                Stat("Shadow Thief -AS/-Damage",
                    "-" + (info.ShadowThiefDebuffAttackSpeed * 100f).ToString("0") + "% AS, -" +
                    (info.ShadowThiefDebuffDamage * 100f).ToString("0") + "% dealt  ·  " +
                    info.ShadowThiefDebuffRemaining.ToString("0.0") + "s", ref cy, inner);
            // Display effective in-battle resistances, not unchanged base values.
            Stat("Armor",
                info.ArmorReduction > 0f
                    ? info.EffectiveArmor + "/" + st.Armor
                    : info.EffectiveArmor.ToString(), ref cy, inner);
            Stat("Magic Resist",
                info.MagicResistReduction > 0f
                    ? info.EffectiveMagicResist + "/" + st.MagicResist
                    : info.EffectiveMagicResist.ToString(), ref cy, inner);
            if (info.TemporaryDefenseRemaining > 0f)
                Stat("Forest Guard  +Armor / +MR", "+" + info.TemporaryArmorBonus +
                    " / +" + info.TemporaryMagicResistBonus + "  ·  " +
                    info.TemporaryDefenseRemaining.ToString("0.0") + "s", ref cy, inner);
            if (info.TemporaryShieldRemaining > 0f)
                Stat("Forest Shield remaining", info.TemporaryShieldAmount + "  ·  " +
                    info.TemporaryShieldRemaining.ToString("0.0") + "s", ref cy, inner);
            if (info.MagmaCoreActive)
            {
                Stat("Magma Core", "Burn IMMUNE", ref cy, inner);
                Stat("Armor converted to AP", "-" + info.MagmaCoreConvertedArmor,
                    ref cy, inner);
                Stat("MR converted to AP", "-" + info.MagmaCoreConvertedMagicResist,
                    ref cy, inner);
                Stat("AP from Magma Core", "+" +
                    info.MagmaCoreGainedAbilityPower.ToString("0"), ref cy, inner);
            }
            if (info.IronFortressActive)
            {
                Stat("Iron Fortress", info.IronFortressBasicHits + "/" +
                    info.IronFortressHitsRequired + " attacks", ref cy, inner);
                Stat("Iron Counter (raw Magic)",
                    info.IronFortressRetaliationRaw.ToString("0.#") + " dmg", ref cy, inner);
            }
            Stat("Attack Range", st.Range + " hex", ref cy, inner);
            Stat("Omnivamp", (st.Omnivamp * 100f).ToString("0.#") + "%", ref cy, inner);
            // The same multipliers used by DealDamage(): present 1.0x as 0% bonus.
            // Live combat snapshots include item, trait and augment bonuses.
            float damageAmpPercent = (st.DamageMultiplier - 1f) * 100f;
            Stat("Damage Amp", (damageAmpPercent >= 0f ? "+" : "") +
                damageAmpPercent.ToString("0.#") + "%", ref cy, inner);
            float damageReductionPercent = (1f - st.IncomingDamageMultiplier) * 100f;
            Stat("Damage Reduction", (damageReductionPercent >= 0f ? "+" : "") +
                damageReductionPercent.ToString("0.#") + "%", ref cy, inner);
            if (MonsterTacticsU5Roster.BaseName(selected.MonsterName) == "Dunebit")
            {
                GUI.Label(new Rect(6f, cy, inner - 12f, 45f),
                    "Passive: +10% Omnivamp. Heal for 10% of actual HP damage dealt.", captionStyle);
                cy += 49f;
            }
            // Healing received is always visible, so Wound has a changing value too.
            Stat("Healing Received", info.Wounded
                ? (100f * (1f - MonsterTacticsCombatRules.WoundHealReduction)).ToString("0") + "%"
                : "100%", ref cy, inner);
            if (info.SunderRemaining > 0f)
                Stat("Sunder -" + (info.ArmorReduction * 100f).ToString("0") + "% (Armor)",
                    info.SunderRemaining.ToString("0.0") + "s", ref cy, inner);
            if (info.ShredRemaining > 0f)
                Stat("Shred -" + (info.MagicResistReduction * 100f).ToString("0") + "% (MR)",
                    info.ShredRemaining.ToString("0.0") + "s", ref cy, inner);
            if (info.WoundRemaining > 0f)
                Stat("Wound -33% (Healing)", info.WoundRemaining.ToString("0.0") + "s",
                    ref cy, inner);
            if (info.BurnRemaining > 0f)
            {
                Stat("Burn expected", info.BurnDamagePerSecond.ToString("0.#") + " /s  · " +
                    info.BurnRemaining.ToString("0.0") + "s", ref cy, inner);
                Stat("Burn ticks (actual)", info.BurnTicksApplied + "  ·  last " +
                    info.LastBurnHpDamage + " HP", ref cy, inner);
            }
            Stat("Critical Chance", (st.CriticalChance * 100f).ToString("0") + "%", ref cy, inner);
            Stat("Critical Damage", (st.CriticalMultiplier * 100f).ToString("0") + "%", ref cy, inner);
            cy += 8f;

            int displayStage = selected.GetComponent<MonsterTacticsU5Progression>()?.Stage ??
                MonsterTacticsU5Roster.StageOf(selected.MonsterName);
            MonsterTacticsCombatRules.AbilityProfile ability;
            if (!MonsterTacticsU610Abilities.TryProfile(MonsterTacticsU5Roster.BaseName(selected.MonsterName),
                displayStage, st, out ability))
                ability=MonsterTacticsCombatRules.AbilityFor(MonsterTacticsU5Roster.BaseName(selected.MonsterName),st);
            Heading("ABILITY  -  " + ability.Name.ToUpperInvariant(), ref cy, inner);
            WrappedText(ability.Description, bodyStyle, ref cy, inner, 10f);
            WrappedText("Scaling: " + ability.Formula, formulaStyle, ref cy, inner, 12f);
            Stat("Mana to cast", MonsterTacticsU5Roster.BaseName(selected.MonsterName)=="Yaroris" ?
                "Passive (every 3rd attack)" : st.ManaCost.ToString(), ref cy, inner);
            // Every offensive ability uses its current attack range to begin
            // casting. Dashes and splash radii are separately described below.
            Stat("Cast range", st.Range + " hex", ref cy, inner);
            string damage = ability.PrimaryRaw.ToString("0.#") + " " + ability.DamageKind;
            if (ability.HitCount > 1) damage += " x " + ability.HitCount;
            Stat("Raw damage", damage, ref cy, inner);
            if (ability.SplashRaw > 0f) Stat("Splash damage", ability.SplashRaw.ToString("0.#"), ref cy, inner);
            if (ability.HealRaw > 0f) Stat("Healing", ability.HealRaw.ToString("0.#"), ref cy, inner);
            if (ability.ShieldRaw > 0f) Stat("Shield gained", ability.ShieldRaw.ToString("0.#"), ref cy, inner);
            if (ability.SplashRaw > 0f) Stat("Splash radius", ability.SplashRadius + " hex", ref cy, inner);
            if (ability.SplashRaw > 0f) Stat("Secondary targets", ability.MaximumSecondaryTargets == int.MaxValue
                ? "All in radius" : ability.MaximumSecondaryTargets.ToString(), ref cy, inner);
            if (ability.HitCount > 1) Stat("Ability hits", ability.HitCount.ToString(), ref cy, inner);
            if (ability.BurnDuration > 0f)
            {
                Stat("Burn duration", ability.BurnDuration.ToString("0.#") + "s", ref cy, inner);
                Stat("Burn (max HP / sec)", (ability.BurnMaxHpPerSecond * 100f).ToString("0.#") + "%", ref cy, inner);
            }
            if (ability.WoundDuration > 0f) Stat("Wound duration", ability.WoundDuration.ToString("0.#") + "s", ref cy, inner);
            cy += 16f;
            WrappedText(
                "Ability damage shown before Armor / MR. AP starts at 100%. " +
                "Omnivamp heals from actual HP damage dealt (not shield damage). " +
                "Sunder / Shred: up to 30% Armor / MR reduction each; no stacking. " +
                "Wound: -33% healing received; no stacking. " +
                "Items, traits and augments can apply these effects later.",
                captionStyle, ref cy, inner, 10f);
            if (selected.OccupiedTile != null)
                GUI.Label(new Rect(6f, cy, inner - 12f, 30f),
                    "Hex: " + selected.OccupiedTile.Column + ", " + selected.OccupiedTile.Row, captionStyle);
            scrollHeight = Mathf.Max(100f, cy + 48f);
            GUI.EndScrollView();

            // In IMGUI the scroll view clips its contents; calculate the hovered
            // row in screen space and draw its tooltip after EndScrollView().
            if (traitStart >= 0f && viewport.Contains(Event.current.mousePosition))
            {
                Vector2 mouse = Event.current.mousePosition;
                float localY = mouse.y - viewport.y + scroll.y;
                int hovered = Mathf.FloorToInt((localY - traitStart) / TraitRowHeight);
                float localX = mouse.x - viewport.x;
                if (hovered >= 0 && hovered < inspectedTraits.Length && localX >= 0f &&
                    localX < viewport.width - 10f &&
                    localY >= traitStart &&
                    localY < traitStart + inspectedTraits.Length * TraitRowHeight)
                {
                    MonsterTacticsTraitPresentation.DrawTooltip(
                        MonsterTacticsTraitPresentation.Find(inspectedTraits[hovered]),
                        traitCounts, bounds, mouse, true, teammates, info.IsEnemy);
                }
            }
            GUI.depth = oldDepth;
        }
    }
}
