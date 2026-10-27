using System;
using UnityEngine;
using UnityEngine.UI;

namespace MonsterTactics
{
    /// <summary>
    /// U6.1 - XP bar and capped level progression on U5.2 Canvas HUD.
    /// Deliberately leaves all pointer-down/drag/up input in MonsterTacticsU5ShopManager and
    /// MonsterPlacementController; only appearance and UI hit rectangles move to Canvas.
    /// No EventSystem is required; this is compatible with both Unity input backends.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MonsterTacticsU5CanvasHud : MonoBehaviour
    {
        public static bool HasActiveHud { get; private set; }
        public bool IsReady => manager != null && shopRect != null && canvas != null;

        // Screen pixel Y measured from bottom (not IMGUI top-left).
        // Inspector uses this to stop above the responsive Canvas shop.
        public float ShopTopScreenY
        {
            get
            {
                if (!IsReady) return 0f;
                Vector3[] corners = new Vector3[4];
                shopRect.GetWorldCorners(corners);
                return RectTransformUtility.WorldToScreenPoint(null, corners[1]).y;
            }
        }

        // Pixel coordinate from the top of the screen (IMGUI convention).
        // The trait sidebar uses this to sit BELOW the actual trainer picker,
        // including when the CanvasScaler changes resolution / aspect ratio.
        public float TrainerPickerBottomGuiY
        {
            get
            {
                if (!IsReady || trainerButtons == null || !trainerButtons.activeInHierarchy)
                    return 0f;
                RectTransform picker = trainerButtons.GetComponent<RectTransform>();
                if (picker == null) return 0f;
                Vector3[] corners = new Vector3[4];
                picker.GetWorldCorners(corners);
                float bottomScreenY = RectTransformUtility.WorldToScreenPoint(null, corners[0]).y;
                return Screen.height - bottomScreenY;
            }
        }

        // The inspector uses this to stay above the bottom-right round-economy panel.
        public float IncomePanelTopScreenY
        {
            get
            {
                if (!IsReady || incomePanelRect == null) return 0f;
                Vector3[] corners = new Vector3[4];
                incomePanelRect.GetWorldCorners(corners);
                return RectTransformUtility.WorldToScreenPoint(null, corners[1]).y;
            }
        }

        private MonsterTacticsU5ShopManager manager;
        private TrainerSkinController trainer;
        private Canvas canvas;
        private RectTransform shopRect;
        private RectTransform incomePanelRect;
        private RectTransform rootRect;
        private RectTransform dragGhost;
        private Image dragGhostFrame;
        private Image dragPortrait;
        private Text dragName;
        private Text dragHint;
        private MonsterUnit lastGhostUnit;
        private RectTransform[] cardRects = new RectTransform[5];
        private RectTransform[] actionRects = new RectTransform[7];
        private GameObject normalGroup;
        private GameObject sellGroup;
        private Image[] portraits = new Image[5];
        private Image[] cardBorders = new Image[5];
        private Text[] names = new Text[5];
        private Text[] types = new Text[5];
        private RawImage[,] typeIcons = new RawImage[5, 2];
        private Text[] costs = new Text[5];
        private Text economyTitle;
        private Text shopOddsLevel;
        private Text[] shopOddsTiers = new Text[5];
        private Text streakValue;
        private Text interestValue;
        private Text incomeValue;
        private RectTransform[] interestPips;
        private Text levelTeamText;
        private Text benchText;
        private Text xpText;
        private RectTransform xpFill;
        private Image buyXpButtonImage;
        private Text buyXpButtonText;
        private Text rerollButtonText;
        private Image rerollButtonImage;
        private Text statusText;
        private Text hintText;
        private Text trainerHpLabel;
        private RectTransform trainerHpFill;
        private MonsterTacticsU62RoundManager matchManager;
        private Text sellKicker;
        private Text sellCancelLabel;
        private Image sellShell;
        private Image sellAccent;
        private Text sellName;
        private Text sellRefund;
        private Image sellPortrait;
        private Image[] trainerButtonImages = new Image[5];
        private GameObject trainerButtons;
        private Font font;
        private float refreshAt;
        private bool lastSell;
        private static readonly Color Navy = new Color(.045f, .081f, .125f, .98f);
        private static readonly Color Slate = new Color(.082f, .135f, .192f, 1f);
        private static readonly Color Edge = new Color(.29f, .48f, .63f, 1f);
        private static readonly Color Muted = new Color(.65f, .77f, .86f, 1f);
        private static readonly Color Gold = new Color(1f, .83f, .36f, 1f);

        public void Bind(MonsterTacticsU5ShopManager owner)
        {
            if (IsReady) return;
            manager = owner;
            matchManager = FindAnyObjectByType<MonsterTacticsU62RoundManager>();
            trainer = FindAnyObjectByType<TrainerSkinController>();
            try { font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); }
            catch (Exception) { font = null; }
            if (font == null)
            {
                try { font = Resources.GetBuiltinResource<Font>("Arial.ttf"); }
                catch (Exception) { font = null; }
            }
            if (font == null) font = Font.CreateDynamicFontFromOSFont("Arial", 16);
            Build();
            Canvas.ForceUpdateCanvases();
            HasActiveHud = true;
            Refresh(true);
        }

        private void OnDestroy()
        {
            HasActiveHud = false;
            if (canvas != null) Destroy(canvas.gameObject);
        }

        private void LateUpdate()
        {
            if (!IsReady) return;
            UpdateDragGhost();
            bool sell = manager.IsSellPreviewActive;
            if (sell != lastSell)
            {
                lastSell = sell;
                normalGroup.SetActive(!sell);
                sellGroup.SetActive(sell);
                refreshAt = 0f;
            }
            if (Time.unscaledTime >= refreshAt)
            {
                Refresh(false);
                refreshAt = Time.unscaledTime + .14f;
            }
        }

        private void Build()
        {
            var root = new GameObject("MonsterTactics U5.2 | Canvas HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;
            root.GetComponent<GraphicRaycaster>().enabled = false; // Existing drag input remains authoritative.
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = .5f;

            rootRect = root.GetComponent<RectTransform>();
            // Panel is 1500x164 on the 1920x1080 reference resolution.
            shopRect = Box(rootRect, "TFT shop + sell bounds", 0f, 0f, 1500f, 164f, Navy,
                new Vector2(.5f, 0f), new Vector2(.5f, 0f), new Vector2(.5f, 0f), new Vector2(-18f, 18f));
            Border(shopRect, new Color(.30f, .48f, .60f), 2f);
            Box(shopRect, "Gold trim", 0f, 0f, 1500f, 3f, Gold);
            normalGroup = new GameObject("Normal Shop (five cards + economy + actions)", typeof(RectTransform));
            Place(normalGroup.GetComponent<RectTransform>(), shopRect, 0f, 0f, 1500f, 164f);

            BuildEconomy(normalGroup.GetComponent<RectTransform>());
            BuildCards(normalGroup.GetComponent<RectTransform>());
            BuildActions(normalGroup.GetComponent<RectTransform>());
            BuildSell(shopRect);
            BuildTop(rootRect);
            BuildIncomePanel(rootRect);
            BuildShopOddsPanel(shopRect);
            BuildTrainer(rootRect);
            BuildDragGhost(rootRect);
        }

        private void BuildDragGhost(RectTransform root)
        {
            dragGhost = Box(root, "U5.2.1 Dragged Monster cursor preview", 0f, 0f,
                118f, 136f, new Color(.035f, .075f, .115f, .88f),
                new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero);
            dragGhostFrame = dragGhost.GetComponent<Image>();
            Border(dragGhost, new Color(.49f, .78f, .98f, .92f), 2f);
            RectTransform art = Box(dragGhost, "Ghost background", 7f, 6f, 104f, 94f,
                new Color(.09f, .18f, .26f, .86f));
            dragPortrait = MakePortraitImage(art, "Real Monster sprite", 7f, 4f, 90f, 86f);
            dragPortrait.preserveAspect = true;
            dragName = Label(dragGhost, "", 3f, 99f, 112f, 18f, 13,
                Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            dragHint = Label(dragGhost, "", 2f, 119f, 114f, 15f, 10,
                Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            dragGhost.gameObject.SetActive(false);
            dragGhost.SetAsLastSibling();
        }

        private void UpdateDragGhost()
        {
            bool anvilDrag = manager.HasAnvilDragForHud;
            MonsterUnit unit = manager.HasUnitDragForHud ? manager.DraggedUnitForHud : null;
            if (unit == null && !anvilDrag)
            {
                if (dragGhost != null && dragGhost.gameObject.activeSelf) dragGhost.gameObject.SetActive(false);
                lastGhostUnit = null;
                return;
            }
            if (dragGhost == null) return;
            if (!dragGhost.gameObject.activeSelf) dragGhost.gameObject.SetActive(true);
            lastGhostUnit = unit;
            dragName.text = anvilDrag ? manager.DraggedAnvilNameForHud : unit.MonsterName;
            SpriteRenderer sprite = !anvilDrag && unit != null
                ? unit.GetComponentInChildren<SpriteRenderer>(true) : null;
            Sprite artwork = anvilDrag ? manager.DraggedAnvilSpriteForHud :
                (sprite != null ? sprite.sprite : null);
            if (dragPortrait.sprite != artwork) dragPortrait.sprite = artwork;
            dragPortrait.enabled = artwork != null;

            bool validTarget;
            string feedback = manager.GetDragDropFeedbackForHud(out validTarget);
            dragHint.text = feedback;
            dragHint.color = validTarget ? new Color(.60f, 1f, .76f) :
                new Color(1f, .66f, .61f);
            if (dragGhostFrame != null)
                dragGhostFrame.color = validTarget ? new Color(.055f, .22f, .17f, .94f) :
                    new Color(.22f, .08f, .10f, .92f);

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rootRect,
                    manager.DragPointerForHud, null, out Vector2 point))
            {
                // Place just above the pointer, preserving the grab location visually.
                dragGhost.anchoredPosition = point + new Vector2(18f, 75f);
            }
            // Ghost is purely visual and cannot swallow mouse input.
            dragGhost.SetAsLastSibling();
        }

        private void BuildEconomy(RectTransform parent)
        {
            // Keep everything inside the existing 178 x 140 TFT economy column.
            // Uses separate labels so buying XP doesn't resize the shop or move any drag hitboxes.
            RectTransform economy = Box(parent, "Economy + U6.1 XP", 12, 12, 178, 140, Slate);
            Border(economy, Edge, 1f);
            Label(economy, "ECONOMY", 12, 6, 156, 18, 12, Muted, TextAnchor.MiddleLeft, FontStyle.Bold);
            economyTitle = Label(economy, "20 GOLD", 12, 25, 156, 32, 23, Gold, TextAnchor.MiddleLeft, FontStyle.Bold);
            Box(economy, "Separator", 12, 60, 152, 2, Edge);
            levelTeamText = Label(economy, "LV 4 / 10  ·  TEAM 4/4", 12, 64, 158, 18, 13,
                Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
            benchText = Label(economy, "BENCH 6/9", 12, 83, 158, 14, 12, Muted, TextAnchor.MiddleLeft);
            Label(economy, "XP", 12, 99, 35, 12, 11, Muted, TextAnchor.MiddleLeft);
            RectTransform xpTrack = Box(economy, "XP track", 12, 112, 152, 8, new Color(.07f, .13f, .21f));
            xpFill = Box(xpTrack, "XP progress fill", 0, 0, 0, 8, new Color(.37f, .69f, 1f));
            xpFill.anchorMin = new Vector2(0f, 1f);
            xpFill.anchorMax = new Vector2(0f, 1f);
            xpText = Label(economy, "0 / 10 XP", 12, 122, 158, 15, 11, Muted,
                TextAnchor.MiddleRight, FontStyle.Bold);
        }

        private void BuildCards(RectTransform parent)
        {
            for (int i = 0; i < 5; i++)
            {
                float x = 199f + i * 204f;
                RectTransform card = Box(parent, "Shop Card " + (i + 1), x, 12f, 198f, 140f, Slate);
                cardRects[i] = card;
                cardBorders[i] = card.GetComponent<Image>();
                Border(card, new Color(.43f, .56f, .66f), 2f);
                Box(card, "Portrait plate", 6, 6, 186, 91, new Color(.11f, .19f, .26f));
                portraits[i] = MakePortraitImage(card, "Monster portrait", 17, 8, 162, 85);
                portraits[i].preserveAspect = true;
                for (int typeSlot = 0; typeSlot < 2; typeSlot++)
                {
                    RectTransform iconRect = NewRect(card, "Monster type icon", 164f, 9f + typeSlot * 24f, 23f, 23f);
                    typeIcons[i, typeSlot] = iconRect.gameObject.AddComponent<RawImage>();
                    typeIcons[i, typeSlot].raycastTarget = false;
                    typeIcons[i, typeSlot].enabled = false;
                }
                Box(card, "Name plate", 6, 98, 186, 35, new Color(.035f, .07f, .105f, .98f));
                names[i] = Label(card, "—", 11, 99, 129, 19, 16, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
                types[i] = Label(card, "", 11, 118, 148, 13, 11, Muted, TextAnchor.MiddleLeft);
                RectTransform costBadge = Box(card, "Cost badge", 149, 103, 38, 24, new Color(.16f, .20f, .21f));
                costs[i] = Label(costBadge, "1g", 0, 0, 38, 24, 17, Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            }
        }

        private void BuildActions(RectTransform parent)
        {
            RectTransform actions = Box(parent, "Actions", 1229f, 12f, 259f, 140f, Slate);
            Border(actions, Edge, 1.2f);
            Label(actions, "SHOP ACTIONS", 12, 7, 235, 20, 14, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
            RectTransform reroll = ButtonVisual(actions, "REROLL SHOP", 12, 32, 235, 38, "REROLL   ·   2g", new Color(.13f, .27f, .40f));
            RectTransform level = ButtonVisual(actions, "BUY XP", 12, 76, 235, 38, "BUY 4 XP   ·   4g", new Color(.17f, .29f, .33f));
            actionRects[0] = reroll;
            rerollButtonText = reroll.GetComponentInChildren<Text>();
            rerollButtonImage = reroll.GetComponent<Image>();
            actionRects[1] = level;
            buyXpButtonImage = level.GetComponent<Image>();
            buyXpButtonText = level.GetComponentInChildren<Text>();
            Label(actions, "Monster: sell   •   Anvils: open", 10, 117, 239, 15, 10, Muted, TextAnchor.MiddleCenter);
        }

        private void BuildSell(RectTransform parent)
        {
            sellGroup = new GameObject("FULL-WIDTH SELL OVERLAY", typeof(RectTransform));
            Place(sellGroup.GetComponent<RectTransform>(), parent, 0, 0, 1500, 164);
            RectTransform shell = Box(sellGroup.GetComponent<RectTransform>(), "Sell / open background", 1, 1, 1498, 162, new Color(.23f, .040f, .059f, .99f));
            sellShell = shell.GetComponent<Image>();
            Border(shell, new Color(.97f, .40f, .44f), 3f);
            sellAccent = Box(shell, "Top accent", 4, 4, 1490, 5, new Color(.82f, .22f, .29f)).GetComponent<Image>();
            RectTransform art = Box(shell, "Monster preview", 440, 21, 124, 116, new Color(.11f, .035f, .05f));
            sellPortrait = MakePortraitImage(art, "Dragged Monster sprite", 4, 3, 116, 110);
            sellPortrait.preserveAspect = true;
            sellKicker = Label(shell, "RELEASE TO SELL", 598, 17, 490, 24, 19, new Color(1f, .76f, .78f), TextAnchor.MiddleLeft, FontStyle.Bold);
            sellName = Label(shell, "EMBERLET", 598, 45, 600, 51, 37, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
            sellRefund = Label(shell, "+2 GOLD", 598, 100, 360, 32, 26, Gold, TextAnchor.MiddleLeft, FontStyle.Bold);
            sellCancelLabel = Label(shell, "DRAG AWAY TO CANCEL", 1052, 117, 280, 25, 13,
                new Color(1f, .75f, .78f), TextAnchor.MiddleCenter, FontStyle.Bold);
            sellGroup.SetActive(false);
        }

        private void BuildTop(RectTransform root)
        {
            RectTransform top = Box(root, "Player overview", 14, 15, 296, 105, Navy,
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(14, -15));
            Border(top, Edge, 1.5f);
            Label(top, "MONSTERTACTICS", 12, 5, 270, 20, 16, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
            statusText = Label(top, "Gold 20 · Lv 4 · Round 1", 12, 27, 270, 18, 12, Gold, TextAnchor.MiddleLeft);
            hintText = Label(top, "Drag units to move or sell", 12, 48, 270, 15, 10, Muted, TextAnchor.MiddleLeft);
            trainerHpLabel = Label(top, "TRAINER HP   100 / 100", 12, 66, 270, 18, 13,
                new Color(.96f, .72f, .76f), TextAnchor.MiddleLeft, FontStyle.Bold);
            RectTransform healthTrack = Box(top, "Trainer HP Track", 12, 89, 272, 7,
                new Color(.21f, .12f, .16f));
            trainerHpFill = Box(healthTrack, "Current Trainer HP", 0, 0, 272, 7,
                new Color(.94f, .31f, .42f));
        }

        // Reserve the bottom-left for a future item bench. Anchor the separate
        // round-economy panel to the bottom-right, just above the main shop.
        // Right anchoring keeps the panel in place across Game-view aspect ratios.
        private void BuildIncomePanel(RectTransform root)
        {
            RectTransform panel = Box(root, "U6.3.11 Economy + streak (right)", 0f, 0f, 300f, 132f, Navy,
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-18f, 192f));
            incomePanelRect = panel;
            Border(panel, Edge, 1.5f);
            Label(panel, "ROUND ECONOMY", 10, 4, 280, 18, 14, Gold, TextAnchor.MiddleLeft, FontStyle.Bold);
            streakValue = Label(panel, "NO STREAK", 10, 24, 276, 17, 11, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
            // Income-panel labels must not render past the panel border.
            streakValue.horizontalOverflow = HorizontalWrapMode.Wrap;
            streakValue.resizeTextForBestFit = true;
            streakValue.resizeTextMinSize = 9;
            streakValue.resizeTextMaxSize = 11;
            interestValue = Label(panel, "INTEREST +0g", 10, 43, 276, 16, 11, Muted, TextAnchor.MiddleLeft);
            interestValue.horizontalOverflow = HorizontalWrapMode.Wrap;
            interestPips = new RectTransform[5];
            for (int i = 0; i < 5; i++)
                interestPips[i] = Box(panel, "Interest threshold " + (i + 1), 10 + i * 57, 62, 51, 5, Edge);
            incomeValue = Label(panel, "NEXT: 5 base + interest + win/streak", 10, 72, 276, 54,
                11, Color.white, TextAnchor.UpperLeft);
            incomeValue.horizontalOverflow = HorizontalWrapMode.Wrap;
            incomeValue.resizeTextForBestFit = true;
            incomeValue.resizeTextMinSize = 9;
            incomeValue.resizeTextMaxSize = 11;
        }

        // A single-line ribbon fits in the gap immediately above the shop,
        // without obscuring the bench row. No raycast targets are enabled.
        private void BuildShopOddsPanel(RectTransform shop)
        {
            RectTransform panel = Box(shop, "U6.2.3 Shop Odds Ribbon", 0f, 0f, 850f, 23f, Navy,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(370f, 0f));
            Border(panel, Edge, 1f);
            Box(panel, "Gold accent", 0f, 0f, 3f, 23f, Gold);
            Label(panel, "SHOP ODDS", 9f, 2f, 110f, 19f, 11, Gold, TextAnchor.MiddleLeft, FontStyle.Bold);
            shopOddsLevel = Label(panel, "LV 4", 119f, 2f, 80f, 19f, 11, Muted, TextAnchor.MiddleLeft);
            Color[] tiers = {
                new Color(.58f, .68f, .73f), new Color(.39f, .84f, .56f),
                new Color(.35f, .64f, .98f), new Color(.73f, .43f, .97f), Gold
            };
            for (int i = 0; i < 5; i++)
            {
                float x = 205f + i * 128f;
                Box(panel, "Tier " + (i + 1) + " accent", x, 5f, 3f, 13f, tiers[i]);
                shopOddsTiers[i] = Label(panel, (i + 1) + "g  0%", x + 8f, 2f, 115f, 19f,
                    12, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
            }
        }

        private void BuildTrainer(RectTransform root)
        {
            RectTransform panel = Box(root, "Trainer picker (replaces legacy bottom-left picker)", 14, 133, 506, 59, Navy,
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(14, -133));
            Border(panel, Edge, 1f);
            Label(panel, "TRAINER SKINS  ·  1–5", 8, 4, 490, 17, 12, Muted, TextAnchor.MiddleLeft, FontStyle.Bold);
            trainerButtons = panel.gameObject;
            string[] trainerNames = { "Nova", "Classic Vela", "ORAS Vela", "Contest Vela", "Ranger" };
            for (int i = 0; i < 5; i++)
            {
                RectTransform b = ButtonVisual(panel, "Trainer " + i, 8 + i * 98, 23, 94, 29, trainerNames[i], Slate);
                actionRects[i + 2] = b;
                trainerButtonImages[i] = b.GetComponent<Image>();
            }
        }

        public bool ContainsShop(Vector2 screenPoint) => Hit(shopRect, screenPoint);

        public int CardIndexAt(Vector2 screenPoint)
        {
            if (!IsReady || lastSell) return -1;
            for (int i = 0; i < cardRects.Length; i++)
                if (Hit(cardRects[i], screenPoint)) return i;
            return -1;
        }

        // 0 = Reroll, 1 = Buy XP; 2-6 = trainer skins.
        public int ActionIndexAt(Vector2 screenPoint)
        {
            if (!IsReady || lastSell) return -1;
            for (int i = 0; i < actionRects.Length; i++)
            {
                // The Buy XP action is genuinely disabled at the level cap or without enough gold.
                if (i == 1 && !manager.CanBuyXP) continue;
                if (Hit(actionRects[i], screenPoint)) return i;
            }
            return -1;
        }

        public void ActivateAction(int action)
        {
            if (!IsReady) return;
            if (action == 0) manager.Reroll();
            else if (action == 1) manager.BuyXP();
            else if (action >= 2 && action <= 6)
            {
                if (trainer == null) trainer = FindAnyObjectByType<TrainerSkinController>();
                if (trainer != null) trainer.SelectSkin(action - 2);
            }
            Refresh(true);
        }

        private void Refresh(bool first)
        {
            if (!IsReady) return;
            economyTitle.text = manager.Gold + " GOLD";
            if (shopOddsLevel != null)
            {
                int[] odds = manager.ShopOddsForHud;
                shopOddsLevel.text = "LEVEL " + manager.PlayerLevel + " / 10";
                for (int i = 0; i < shopOddsTiers.Length; i++)
                    shopOddsTiers[i].text = (i + 1) + "g  " + odds[i] + "%";
            }
            if (streakValue != null)
            {
                // Concise labels: the original "CURRENT BONUS" string overflowed
                // the fixed-width right-hand economy panel during PvE.
                streakValue.text = manager.IsPveRound
                    ? "PVE: STREAK PAUSED  |  BONUS +0g"
                    : manager.StreakLabel + " " + manager.CurrentStreak +
                      "  |  BONUS +" + manager.CurrentStreakBonus + "g";
                interestValue.text = "INTEREST +" + manager.InterestNextRound + "g" +
                    (manager.InterestNextRound < manager.InterestCap ? "   ·   " + manager.InterestGoldUntilNext + "g to next" : "   ·   MAX");
                for (int i = 0; i < interestPips.Length; i++)
                    interestPips[i].GetComponent<Image>().color = i < manager.InterestNextRound ? Gold : Edge;
                // Round income is deferred to next Planning; PvP win +1 pays now.
                // Interest in Planning is only a preview until Results locks the amount.
                string phase = manager.PhaseForHud;
                if (phase == "Results" && manager.HasPendingPlanningIncome)
                {
                    incomeValue.text = "NEXT PLANNING  +" + manager.PendingPlanningIncome + "g" +
                        (manager.LastVictoryIncome > 0 ? "   ·   PvP win +1 NOW" : "") + "\n" +
                        "Base +" + manager.LastBaseIncome + "  ·  Interest +" + manager.LastInterestIncome +
                        "  ·  Streak +" + manager.LastStreakIncome;
                }
                else
                {
                    int guaranteed = manager.BaseRoundIncome + manager.InterestNextRound;
                    if (manager.IsPveRound)
                    {
                        incomeValue.text = "NEXT PLANNING PVE INCOME\n" +
                            "Base +" + manager.BaseRoundIncome + "  ·  Interest +" + manager.InterestNextRound +
                            "  ·  No PvE win bonus";
                    }
                    else
                    {
                        int winStreakBonus = manager.WinStreak > 0 ? manager.NextStreakBonus : 0;
                        int lossStreakBonus = manager.LossStreak > 0 ? manager.NextStreakBonus : 0;
                        incomeValue.text = "NEXT PLANNING  BASE +" + guaranteed + "g\n" +
                            "PvP win +1 now  ·  Streak +" + Mathf.Max(winStreakBonus, lossStreakBonus) + "g later";
                    }
                }
            }
            levelTeamText.text = "LV " + manager.PlayerLevel + "/" + manager.LevelCap +
                " · TEAM " + manager.DeployedCountForHud + "/" + manager.TeamLimit;
            benchText.text = "BENCH " + manager.BenchCountForHud + "/9";
            xpText.text = manager.IsLevelCapped ? "MAX LEVEL" :
                manager.CurrentXP + " / " + manager.XPToNextLevel + " XP";
            if (xpFill != null)
                xpFill.sizeDelta = new Vector2(152f * manager.XPProgress, 8f);
            // Show how many free SHOP rerolls remain; augment-card rerolls are separate.
            if (rerollButtonText != null)
                rerollButtonText.text = manager.FreeRerolls > 0
                    ? "REROLL   ·   FREE ×" + manager.FreeRerolls
                    : "REROLL   ·   " + manager.CurrentRerollCost + "g";
            if (rerollButtonImage != null)
                rerollButtonImage.color = manager.FreeRerolls > 0
                    ? new Color(.10f, .37f, .32f, 1f)
                    : new Color(.13f, .27f, .40f, 1f);
            if (buyXpButtonText != null)
                buyXpButtonText.text = manager.IsLevelCapped ? "MAX LEVEL" :
                    "BUY " + manager.XPPerPurchase + " XP   ·   " + manager.BuyXPCost + "g";
            if (buyXpButtonImage != null)
                buyXpButtonImage.color = !manager.CanManageShop || manager.IsLevelCapped ?
                    new Color(.12f, .16f, .19f, 1f) :
                    manager.Gold < manager.BuyXPCost ? new Color(.17f, .21f, .23f, 1f) :
                    new Color(.17f, .29f, .33f, 1f);
            statusText.text = manager.Gold + "g · Lv " + manager.PlayerLevel +
                (manager.IsLevelCapped ? " MAX" : " · " + manager.CurrentXP + "/" + manager.XPToNextLevel + " XP") +
                " · Round " + manager.RoundForHud;
            hintText.text = manager.IsCombatRunning &&
                !manager.StatusForHud.StartsWith("Bench full", StringComparison.OrdinalIgnoreCase) ?
                "COMBAT: BOARD LOCKED · SHOP + BENCH OPEN" : manager.StatusForHud;
            if (matchManager == null) matchManager = FindAnyObjectByType<MonsterTacticsU62RoundManager>();
            if (matchManager != null && trainerHpLabel != null)
            {
                int hp = matchManager.PlayerHP;
                int maxHP = matchManager.MaxPlayerHP;
                trainerHpLabel.text = "TRAINER HP   " + hp + " / " + maxHP +
                    (matchManager.LastRoundDamage > 0 && manager.PhaseForHud == "Results" ?
                     "   (-" + matchManager.LastRoundDamage + ")" : "");
                if (trainerHpFill != null)
                    trainerHpFill.sizeDelta = new Vector2(272f * Mathf.Clamp01(hp / (float)maxHP), 7f);
            }
            for (int i = 0; i < 5; i++)
            {
                MonsterTacticsU5Roster.Family family = manager.OfferForHud(i);
                bool available = family != null;
                string name = available ? family.Forms[0] : "SOLD";
                if (first || names[i].text != name)
                {
                    names[i].text = name;
                    types[i].text = available ? family.Type(1).Replace("/", " / ") + " · " + family.Role : "Reroll to refresh";
                    costs[i].text = available ? family.Cost + "g" : "—";
                }
                string[] cardTypes = available ? family.Type(1).Split('/') : new string[0];
                for (int typeSlot = 0; typeSlot < 2; typeSlot++)
                {
                    string type = cardTypes.Length > typeSlot ? cardTypes[typeSlot].Trim() : "";
                    Texture2D texture = type.Length > 0 ? Resources.Load<Texture2D>("TypeIcons/" + type) : null;
                    // U6.9.1f.4a: An icon component may have been destroyed or
                    // left unassigned during a UI rebuild. Recover it instead
                    // of throwing from this HUD refresh loop every 0.14s.
                    RawImage typeIcon = EnsureShopTypeIcon(i, typeSlot);
                    if (typeIcon != null)
                    {
                        typeIcon.texture = texture;
                        typeIcon.enabled = texture != null;
                    }
                }
                Sprite artwork = available ? manager.OfferSpriteForHud(i) : null;
                if (portraits[i].sprite != artwork) portraits[i].sprite = artwork;
                portraits[i].enabled = artwork != null;
                Color rarity = available ? RarityColor(family.Cost) : new Color(.29f, .35f, .40f);
                cardBorders[i].color = available ? new Color(.08f, .13f, .20f) : new Color(.05f, .08f, .11f);
                // The bottom accent bar is the unobstructed rarity indicator.
                Transform accent = cardRects[i].Find("Rarity accent");
                if (accent == null) Box(cardRects[i], "Rarity accent", 0, 135, 198, 5, rarity);
                else accent.GetComponent<Image>().color = rarity;
            }
            if (trainer == null) trainer = FindAnyObjectByType<TrainerSkinController>();
            for (int i = 0; i < trainerButtonImages.Length; i++)
                if (trainerButtonImages[i] != null)
                    trainerButtonImages[i].color = trainer != null && trainer.CurrentSkin == i ? new Color(.25f, .47f, .44f) : Slate;

            if (lastSell)
            {
                bool openingAnvil = manager.IsAnvilOpenPreviewActive;
                MonsterUnit unit = manager.DraggedUnitForHud;
                if (openingAnvil)
                {
                    sellKicker.text = "RELEASE TO OPEN";
                    sellName.text = manager.DraggedAnvilNameForHud.ToUpperInvariant();
                    sellRefund.text = "CHOOSE 1 OF 4 REWARDS";
                    sellPortrait.sprite = manager.DraggedAnvilSpriteForHud;
                    sellPortrait.enabled = sellPortrait.sprite != null;
                    sellShell.color = new Color(.055f, .15f, .25f, .99f);
                    sellAccent.color = new Color(.26f, .69f, .99f);
                    sellKicker.color = new Color(.62f, .85f, 1f);
                    sellCancelLabel.color = new Color(.62f, .85f, 1f);
                }
                else if (unit != null)
                {
                    sellKicker.text = "RELEASE TO SELL";
                    sellName.text = unit.MonsterName.ToUpperInvariant();
                    sellRefund.text = "+" + manager.RefundForHud(unit) + " GOLD";
                    SpriteRenderer source = unit.GetComponentInChildren<SpriteRenderer>(true);
                    sellPortrait.sprite = source != null ? source.sprite : null;
                    sellPortrait.enabled = sellPortrait.sprite != null;
                    sellShell.color = new Color(.23f, .040f, .059f, .99f);
                    sellAccent.color = new Color(.82f, .22f, .29f);
                    sellKicker.color = new Color(1f, .76f, .78f);
                    sellCancelLabel.color = new Color(1f, .75f, .78f);
                }
            }
        }

        // Returns an existing shop type icon or restores it if Unity has
        // destroyed its RawImage/GameObject. Uses the original card positions so
        // both slots remain independent even though legacy children share a name.
        private RawImage EnsureShopTypeIcon(int cardIndex, int typeSlot)
        {
            RawImage icon = typeIcons[cardIndex, typeSlot];
            if (icon != null) return icon;

            RectTransform card = cardRects[cardIndex];
            if (card == null) return null;

            float expectedY = -(9f + typeSlot * 24f);
            for (int childIndex = 0; childIndex < card.childCount; childIndex++)
            {
                Transform child = card.GetChild(childIndex);
                if (child == null || child.name != "Monster type icon") continue;
                RectTransform rect = child as RectTransform;
                if (rect == null || Mathf.Abs(rect.anchoredPosition.y - expectedY) > 1f) continue;

                icon = child.GetComponent<RawImage>();
                if (icon == null) icon = child.gameObject.AddComponent<RawImage>();
                break;
            }

            if (icon == null)
            {
                RectTransform rect = NewRect(card, "Monster type icon",
                    164f, 9f + typeSlot * 24f, 23f, 23f);
                icon = rect.gameObject.AddComponent<RawImage>();
            }

            if (icon != null)
            {
                icon.raycastTarget = false;
                typeIcons[cardIndex, typeSlot] = icon;
                Debug.LogWarning("[U6.9.1f.4a] Restored missing shop type icon on card " +
                    (cardIndex + 1) + ", slot " + (typeSlot + 1) + ".", this);
            }
            return icon;
        }

        private static Color RarityColor(int cost)
        {
            switch (cost)
            {
                case 1: return new Color(.58f, .68f, .73f);
                case 2: return new Color(.39f, .84f, .56f);
                case 3: return new Color(.35f, .64f, .98f);
                case 4: return new Color(.73f, .43f, .97f);
                default: return Gold;
            }
        }

        private static bool Hit(RectTransform rect, Vector2 screenPoint) =>
            rect != null && rect.gameObject.activeInHierarchy &&
            RectTransformUtility.RectangleContainsScreenPoint(rect, screenPoint, null);

        private static RectTransform Box(RectTransform parent, string name, float x, float y, float width, float height, Color fill,
            Vector2? anchorMin = null, Vector2? anchorMax = null, Vector2? pivot = null, Vector2? position = null)
        {
            RectTransform rect = NewRect(parent, name, x, y, width, height, anchorMin, anchorMax, pivot, position);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = fill;
            image.raycastTarget = false;
            return rect;
        }

        private static Image MakePortraitImage(RectTransform parent, string name, float x, float y, float width, float height)
        {
            RectTransform rect = NewRect(parent, name, x, y, width, height);
            Image image = rect.gameObject.AddComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        private Text Label(RectTransform parent, string value, float x, float y, float width, float height,
            int fontSize, Color color, TextAnchor alignment, FontStyle fontStyle = FontStyle.Normal)
        {
            RectTransform rect = NewRect(parent, "Text: " + value, x, y, width, height);
            Text label = rect.gameObject.AddComponent<Text>();
            label.font = font;
            label.text = value;
            label.fontSize = fontSize;
            label.fontStyle = fontStyle;
            label.color = color;
            label.alignment = alignment;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.raycastTarget = false;
            return label;
        }

        private RectTransform ButtonVisual(RectTransform parent, string name, float x, float y, float width, float height, string title, Color color)
        {
            RectTransform rect = Box(parent, name, x, y, width, height, color);
            Border(rect, Edge, 1f);
            Label(rect, title, 5, 2, width - 10, height - 4, 14, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            return rect;
        }

        private static RectTransform NewRect(RectTransform parent, string name, float x, float y, float width, float height,
            Vector2? anchorMin = null, Vector2? anchorMax = null, Vector2? pivot = null, Vector2? position = null)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform));
            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin ?? new Vector2(0f, 1f);
            rect.anchorMax = anchorMax ?? new Vector2(0f, 1f);
            rect.pivot = pivot ?? new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = position ?? new Vector2(x, -y);
            return rect;
        }

        private static void Place(RectTransform rect, RectTransform parent, float x, float y, float width, float height)
        {
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(x, -y);
        }

        private static void Border(RectTransform rect, Color color, float thickness)
        {
            Box(rect, "Border top", 0, 0, rect.sizeDelta.x, thickness, color);
            Box(rect, "Border left", 0, 0, thickness, rect.sizeDelta.y, color);
            Box(rect, "Border right", rect.sizeDelta.x - thickness, 0, thickness, rect.sizeDelta.y, color);
            Box(rect, "Border bottom", 0, rect.sizeDelta.y - thickness, rect.sizeDelta.x, thickness, color);
        }
    }
}
