using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace MonsterTactics
{
    /// <summary>
    /// U6.1.1: XP progression plus evolved-form sprites and quick star-up VFX.
    /// Keeps cursor drag, hex reserve bench, evolutions and U4 combat intact.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MonsterTacticsU5ShopManager : MonoBehaviour
    {
        [Serializable]
        public struct FormArt
        {
            public string Form;
            public Sprite[] Frames;
            public float[] Seconds;
        }

        [SerializeField] private MonsterPlacementController placement;
        [SerializeField] private MonsterTacticsCombatDirector combat;
        [SerializeField] private Transform arenaRoot;
        [SerializeField] private Camera arenaCamera;
        [SerializeField, Range(1, 10)] private int teamLimit = 4;
        [SerializeField] private FormArt[] evolutionArt = new FormArt[0];

        private const int BenchCapacity = 9;
        private const int ShopSlots = 5;
        // U6.8.7: Economy/shop defaults now live in MonsterTacticsRuleBook.
        private const int MaxPlayerLevel = 10;
        // Index = current level; value = XP required to reach the next level.
        // These are a prototype TFT-inspired progression curve, not tied to a live TFT set.
        private static readonly int[] XpRequiredByLevel = { 0, 2, 2, 6, 10, 20, 36, 48, 80, 84, 0 };
        private readonly MonsterTacticsU5Roster.Family[] shop = new MonsterTacticsU5Roster.Family[ShopSlots];
        private readonly Dictionary<string, MonsterUnit> templates = new Dictionary<string, MonsterUnit>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<Vector2Int, HexTile> tiles = new Dictionary<Vector2Int, HexTile>();
        private readonly HashSet<MonsterUnit> removed = new HashSet<MonsterUnit>();
        private readonly MonsterUnit[] benchSlots = new MonsterUnit[BenchCapacity];
        private MonsterTacticsMonsterInspector inspector;
        private Transform templateContainer;
        private bool initialized;
        private int gold;
        private int freeRerolls;
        public MonsterTacticsRuleBook Rules { get; } = new MonsterTacticsRuleBook();
        public MonsterTacticsGameplayEvents GameplayEvents { get; } = new MonsterTacticsGameplayEvents();
        public int FreeRerolls => freeRerolls;
        public int CurrentRerollCost => freeRerolls > 0 ? 0 : Rules.Get(MonsterTacticsRuleId.RerollCost);
        public int InterestCap => Rules.Get(MonsterTacticsRuleId.InterestCap);
        public int BaseRoundIncome => Rules.Get(MonsterTacticsRuleId.BaseRoundIncome);

        public void EmitGameplayEvent(MonsterTacticsGameplayEventKind kind, string source = "",
            int amount = 0, string unitName = "")
        {
            GameplayEvents.Publish(new MonsterTacticsGameplayEvent(kind, source, amount, unitName,
                roundManager != null ? roundManager.StageRound : ""));
        }

        public bool TrySpendGold(int amount, string source = "external")
        {
            if (!initialized || amount < 0 || gold < amount) return false;
            gold -= amount;
            if (amount != 0) EmitGameplayEvent(MonsterTacticsGameplayEventKind.GoldChanged, source, -amount);
            return true;
        }

        public void GrantGold(int amount, string source = "external")
        {
            if (!initialized || amount <= 0) return;
            gold = Mathf.Min(999999, gold + amount);
            EmitGameplayEvent(MonsterTacticsGameplayEventKind.GoldChanged, source, amount);
        }

        public void GrantFreeRerolls(int amount, string source = "external")
        {
            if (!initialized || amount <= 0) return;
            freeRerolls = Mathf.Min(999, freeRerolls + amount);
            EmitGameplayEvent(MonsterTacticsGameplayEventKind.FreeRerollsGranted, source, amount);
        }

        public bool GrantItemReward(MonsterTacticsItemKind kind, string source = "external")
        {
            if (!initialized || !MonsterTacticsItemTypes.IsAvailable(kind)) return false;
            var bag = GetComponent<MonsterTacticsItemInventory>();
            if (bag == null) return false;
            bag.Grant(kind);
            EmitGameplayEvent(MonsterTacticsGameplayEventKind.ItemGranted, source, 1,
                MonsterTacticsItemTypes.Name(kind));
            return true;
        }
        private int battleNumber = 1;
        private int playerLevel = 4;
        private int currentXp;
        private string lastPhase = "Planning";
        private string status = "Drag to position. Release over SELL to sell.";
        private MonsterUnit selected;

        private float styleScale = -1f;
        private GUIStyle head;
        private GUIStyle small;
        private GUIStyle centered;
        private GUIStyle tiny;
        private GUIStyle cardTitle;
        private GUIStyle cardSub;
        private GUIStyle cardCost;
        private GUIStyle slotHint;
        private GUIStyle stageBadge;
        private GUIStyle benchNameStyle;
        private GUIStyle worldBenchItemNameStyle;
        private GUIStyle statusStyle;
        private GUIStyle actionStyle;
        private Texture2D emptyHexTexture;
        private Texture2D fullHexTexture;
        private Texture2D hoverHexTexture;
        private Texture2D selectedHexTexture;
        [Header("U5.1.9 world hex bench")]
        [SerializeField] private bool useWorldBench = true;
        [SerializeField, Range(0.65f, 2.3f)] private float worldBenchForwardOffset = 1.10f;
        [Header("U5.2.2 subtle bench gap")]
        [SerializeField, Range(0f, 0.45f)] private float worldBenchExtraGap = 0.18f;
        [SerializeField, Range(.85f, 1.65f)] private float worldBenchSpacing = 1.16f;
        [Header("U6.9.5 anvil sprite placement (visual only)")]
        [SerializeField, Range(.05f, .8f)] private float worldBenchAnvilBaseHeight = .26f;
        [SerializeField, Range(.5f, 1.7f)] private float worldBenchAnvilScale = 1.02f;
        private readonly Transform[] worldTileTransforms = new Transform[BenchCapacity];
        private readonly Collider[] worldTileColliders = new Collider[BenchCapacity];
        private readonly MeshRenderer[] worldTileRenderers = new MeshRenderer[BenchCapacity];
        private readonly SpriteRenderer[] worldSpriteRenderers = new SpriteRenderer[BenchCapacity];
        private Transform worldBenchRoot;
        private MaterialPropertyBlock tileColors;
        private bool worldBenchReady;
        private string lastResult = "";
        private int winStreak;
        private int lossStreak;
        private MonsterTacticsU62RoundManager roundManager;
        // Boss encounters are PvE too. PvE pauses PvP streaks instead of ending them.
        public bool IsPveRound
        {
            get
            {
                if (roundManager == null) roundManager = FindAnyObjectByType<MonsterTacticsU62RoundManager>();
                if (roundManager == null) return false;
                var kind = roundManager.CurrentKind;
                return kind == MonsterTacticsU62RoundManager.RoundKind.PvE ||
                       kind == MonsterTacticsU62RoundManager.RoundKind.Boss;
            }
        }
        private int lastBaseIncome;
        private int lastInterestIncome;
        private int lastVictoryIncome;
        private int lastStreakIncome;
        private int lastTotalIncome;
        private bool hasIncomeReport;
        // Round income is earned after combat but credited at the NEXT Planning
        // phase. A PvP victory alone pays +1g immediately in Results.
        private int pendingPlanningIncome;
        private bool pendingPlanningPayout;
        public int PendingPlanningIncome => pendingPlanningPayout ? pendingPlanningIncome : 0;
        public bool HasPendingPlanningIncome => pendingPlanningPayout;
        public int InterestNextRound => Rules.InterestForGold(gold);
        public int InterestGoldUntilNext => Rules.GoldToNextInterest(gold);
        public int CurrentStreak => Mathf.Max(winStreak, lossStreak);
        public string StreakLabel => IsPveRound ? "PVE: STREAK PAUSED" :
            winStreak > 0 ? "WIN STREAK" : lossStreak > 0 ? "LOSS STREAK" : "NO STREAK";
        public int NextStreakBonus => IsPveRound ? 0 : StreakBonus(CurrentStreak + 1);
        public int CurrentStreakBonus => IsPveRound ? 0 : StreakBonus(CurrentStreak);
        public bool HasIncomeReport => hasIncomeReport;
        public int LastBaseIncome => lastBaseIncome;
        public int LastInterestIncome => lastInterestIncome;
        public int LastVictoryIncome => lastVictoryIncome;
        public int LastStreakIncome => lastStreakIncome;
        public int LastTotalIncome => lastTotalIncome;
        public int WinStreak => winStreak;
        public int LossStreak => lossStreak;
        private static int StreakBonus(int streak) => streak >= 5 ? 3 : streak >= 4 ? 2 : streak >= 2 ? 1 : 0;

        private MonsterUnit draggedBenchUnit;
        private int draggedAnvilSlot = -1; // Monster reserve slot; never an item-inventory slot.
        private MonsterTacticsItemCapsules cachedAnvils;
        private bool HasAnvilAt(int slot)
        {
            if (cachedAnvils == null) cachedAnvils = GetComponent<MonsterTacticsItemCapsules>();
            return cachedAnvils != null && cachedAnvils.HasBenchAnvil(slot);
        }
        public int FreeMonsterReserveSlots => EmptyBenchSlots;
        // Anvils take the same nine reserve-hex slots as Monster, not Item Bench slots.
        public bool TryGrantBenchAnvil(MonsterTacticsItemCapsuleKind kind)
        {
            if (!initialized) return false;
            SyncBenchSlots();
            int index = FreeBenchSlot();
            if (index < 0) { status = "Clear your Monster Bench first!"; return false; }
            if (cachedAnvils == null) cachedAnvils = GetComponent<MonsterTacticsItemCapsules>();
            if (cachedAnvils == null || !cachedAnvils.PlaceAtSlot(index, kind)) return false;
            status = MonsterTacticsItemCapsules.DisplayName(kind) + " added to Monster Bench (slot " + (index + 1) + ")";
            RefreshWorldBench();
            return true;
        }
        public void ReleaseBenchAnvil(int slot)
        {
            // The capsule manager has already cleared its occupancy.
            RefreshWorldBench();
        }
        public void ShowAnvilNotice(string text) { status = text; }
        private int draggedShopIndex = -1;
        private Vector2 dragStartScreen;
        private Vector2 dragPointerScreen;
        private bool guiDragging;
        private MonsterTacticsU5CanvasHud canvasHud;
        private int pressedHudAction = -1;
        private MonsterUnit boardDraggedUnit;
        private Vector2 boardDragPointer;
        private bool boardDragActive;
        private const float GuiDragThreshold = 8f;
        private static readonly Color UiPanel = new Color(.05f, .09f, .14f, .94f);
        private static readonly Color UiPanelSoft = new Color(.08f, .13f, .19f, .96f);
        private static readonly Color UiLine = new Color(.25f, .49f, .78f, .95f);
        private static readonly Color UiGold = new Color(1f, .84f, .31f, 1f);
        private float HudScale => Mathf.Clamp(Screen.height / 1080f, .76f, 1.40f);

        public int Gold => gold;

        // U6.7.2: loot gold is separate from income/streak calculations and
        // does not cause rerolls or modify shop pool reservations.
        public void GrantLootGold(int amount)
        {
            if (!initialized || amount <= 0) return;
            GrantGold(amount, "loot");
            status = "Loot collected: +" + amount + " gold.";
        }
        public int TeamLimit => Mathf.Clamp(teamLimit + Rules.Get(MonsterTacticsRuleId.TeamSizeBonus), 1, 12);
        public int PlayerLevel => playerLevel;
        public int[] ShopOddsForHud => Rules.ShopOdds(MonsterTacticsU5Roster.TierWeights(playerLevel));
        public int LevelCap => MaxPlayerLevel;
        public int CurrentXP => currentXp;
        public int XPToNextLevel => playerLevel >= MaxPlayerLevel ? 0 : XpRequiredByLevel[Mathf.Clamp(playerLevel, 1, MaxPlayerLevel)];
        public int XPRemaining => Mathf.Max(0, XPToNextLevel - currentXp);
        public float XPProgress => playerLevel >= MaxPlayerLevel ? 1f :
            Mathf.Clamp01(currentXp / (float)Mathf.Max(1, XPToNextLevel));
        public bool IsLevelCapped => playerLevel >= MaxPlayerLevel;
        public int BuyXPCost => Rules.Get(MonsterTacticsRuleId.XPBuyCost);
        public int XPPerPurchase => Rules.Get(MonsterTacticsRuleId.XPPerPurchase);
        public bool CanBuyXP => CanManageShop && !IsLevelCapped && gold >= BuyXPCost;
        public bool IsPlanning => combat == null || combat.MatchPhase == "Planning";
        public bool IsCombatRunning => combat != null && combat.MatchPhase == "Battle";
        public bool IsResults => combat != null && combat.MatchPhase == "Results";
        // Shopping, rerolling, XP purchases and reserve management remain available in
        // Results just as in Battle. Deployed units stay locked until Planning.
        // Modal augment selection blocks purchases and board movement, while
        // the augment effect engine can still grant gold/items through IsShopReady.
        public bool AugmentSelectionOpen
        {
            get
            {
                var manager = GetComponent<MonsterTacticsAugmentManager>();
                return manager != null && manager.IsSelectionOpen;
            }
        }
        public bool AnvilSelectionOpen
        {
            get
            {
                var anvils = GetComponent<MonsterTacticsItemCapsules>();
                return anvils != null && anvils.IsShowing;
            }
        }
        public bool CanManageShop => initialized && !AugmentSelectionOpen && !AnvilSelectionOpen &&
            (roundManager == null || !roundManager.MatchEnded) &&
            (IsPlanning || IsCombatRunning || IsResults);
        public bool CanManageBench => CanManageShop;
        public bool CanManageBoard => IsPlanning && !AugmentSelectionOpen && !AnvilSelectionOpen &&
            (roundManager == null || !roundManager.MatchEnded);
        public string PhaseForHud => combat == null ? "Planning" : combat.MatchPhase;
        public bool IsShopReady => initialized;
        // Read-only model access for the new Canvas. All economy and shop rules remain here.
        public int DeployedCountForHud => initialized ? DeployedCount() : 0;
        public int BenchCountForHud => initialized ? Bench().Count +
            (GetComponent<MonsterTacticsItemCapsules>() != null ? GetComponent<MonsterTacticsItemCapsules>().BenchAnvilCount : 0) : 0;
        public int RoundForHud => combat != null && combat.AutomaticRoundsEnabled ? Mathf.Max(1, battleNumber) : battleNumber;
        public string StatusForHud => status;
        public MonsterUnit DraggedUnitForHud => ActiveDraggedUnit;
        public bool HasAnvilDragForHud => guiDragging && draggedAnvilSlot >= 0 &&
            HasAnvilAt(draggedAnvilSlot) && CanManageBench;
        public Sprite DraggedAnvilSpriteForHud => HasAnvilDragForHud && cachedAnvils != null
            ? cachedAnvils.BenchSprite(draggedAnvilSlot) : null;
        public string DraggedAnvilNameForHud => HasAnvilDragForHud && cachedAnvils != null
            ? cachedAnvils.BenchDisplayName(draggedAnvilSlot) : string.Empty;
        public Vector2 DragPointerForHud => HasAnvilDragForHud ? dragPointerScreen : ActiveDragPointer;
        public bool HasUnitDragForHud => ActiveDraggedUnit != null && CanManageBench;
        public bool CanDropOnBoardForHud => CanManageBoard;
        public bool IsAnvilOpenPreviewActive => CanManageShop && HasAnvilDragForHud &&
            IsOverShopArea(dragPointerScreen);
        public bool IsSellPreviewActive => ShowSellOverlay || IsAnvilOpenPreviewActive;
        public MonsterTacticsU5Roster.Family OfferForHud(int i) => i >= 0 && i < ShopSlots ? shop[i] : null;
        public Sprite OfferSpriteForHud(int i) => i >= 0 && i < ShopSlots ? GetFamilySprite(shop[i]) : null;
        public int RefundForHud(MonsterUnit unit) => SellRefund(unit);

        /// <summary>Board placement sends an active drag here, enabling the shared SELL HUD.
        /// This is visual state only; the placement controller remains responsible for the drop.
        /// </summary>
        public void ReportBoardDrag(MonsterUnit unit, bool active, Vector2 screenPointer)
        {
            boardDraggedUnit = active ? unit : null;
            boardDragActive = active && unit != null;
            boardDragPointer = screenPointer;
        }

        private MonsterUnit ActiveDraggedUnit =>
            guiDragging && draggedBenchUnit != null ? draggedBenchUnit :
            boardDragActive ? boardDraggedUnit : null;

        private Vector2 ActiveDragPointer =>
            guiDragging && draggedBenchUnit != null ? dragPointerScreen : boardDragPointer;

        private bool ShowSellOverlay => CanManageShop && ActiveDraggedUnit != null &&
            CanSellUnit(ActiveDraggedUnit) && IsOverShopArea(ActiveDragPointer);

        // During Battle/Results, only real bench residents can be sold.
        // Knocked-out deployed units keep their board tile and must not appear as
        // sellable bench copies (U6.2.10 regression safeguard).
        private bool CanSellUnit(MonsterUnit unit) => CanManageShop && unit != null &&
            (IsPlanning || ((IsCombatRunning || IsResults) &&
                unit.OccupiedTile == null && SlotOf(unit) >= 0));

        private static int SellRefund(MonsterUnit unit)
        {
            if (unit == null) return 0;
            MonsterTacticsU5Progression progress = unit.GetComponent<MonsterTacticsU5Progression>();
            return progress != null ? Mathf.Max(0, progress.InvestedGold) :
                Mathf.Max(1, MonsterTacticsU5Roster.Find(unit.MonsterName)?.Cost ?? 1);
        }

        public void Configure(MonsterPlacementController board, MonsterTacticsCombatDirector director,
            Transform arena, Camera camera, FormArt[] art)
        {
            placement = board;
            combat = director;
            arenaRoot = arena;
            arenaCamera = camera;
            evolutionArt = art ?? new FormArt[0];
        }

        /// <summary>
        /// Editor-safe art linking. Updates only artwork, leaving the existing scene,
        /// trainer, combat references, gold, and XP settings untouched.
        /// </summary>
        public void SetEvolutionArt(FormArt[] art)
        {
            evolutionArt = art ?? new FormArt[0];
        }

        public int LinkedEvolutionArtCount
        {
            get
            {
                int valid = 0;
                foreach (FormArt asset in evolutionArt)
                    if (!string.IsNullOrEmpty(asset.Form) && asset.Frames != null &&
                        asset.Frames.Length > 0 && asset.Frames[0] != null) valid++;
                return valid;
            }
        }

        private void Awake()
        {
            // This milestone intentionally uses one world-space hex bench, never an extra HUD reserve row.
            useWorldBench = true;
            if (arenaCamera == null) arenaCamera = Camera.main;
            if (placement == null) placement = FindAnyObjectByType<MonsterPlacementController>();
            if (combat == null) combat = FindAnyObjectByType<MonsterTacticsCombatDirector>();
            if (inspector == null) inspector = FindAnyObjectByType<MonsterTacticsMonsterInspector>();
            if (arenaRoot == null)
            {
                GameObject found = GameObject.Find("MonsterTactics Arena");
                if (found != null) arenaRoot = found.transform;
            }
        }

        private void Start() { InitializeIfReady(); }

        private void InitializeIfReady()
        {
            if (initialized || placement == null || arenaRoot == null) return;
            MonsterUnit[] current = placement.GetComponentsInChildren<MonsterUnit>(true);
            if (current.Length == 0) return;
            tiles.Clear();
            foreach (HexTile tile in arenaRoot.GetComponentsInChildren<HexTile>(true))
                tiles[new Vector2Int(tile.Column, tile.Row)] = tile;
            if (tiles.Count != 64) { status = "U5 requires the existing 64-tile arena."; return; }

            GameObject templateRoot = new GameObject("U5 runtime sprite templates (hidden)");
            templateRoot.transform.SetParent(transform, false);
            templateContainer = templateRoot.transform;
            foreach (MonsterUnit unit in current)
            {
                MonsterTacticsU5Roster.Family family = MonsterTacticsU5Roster.Find(unit.MonsterName);
                if (family == null || templates.ContainsKey(family.Forms[0])) continue;
                GameObject example = Instantiate(unit.gameObject, templateContainer);
                example.name = family.Forms[0] + " - Runtime Template";
                example.SetActive(false);
                templates.Add(family.Forms[0], example.GetComponent<MonsterUnit>());
            }
            EnsureRuntimeTemplates(current);

            // U6.3.1: Scene Monster were demonstration units, NOT free starting inventory.
            // Use a single free 1g Pebblin; all other 9 scene units only supply art/templates.
            // Removing demo units keeps the finite pool and 9-slot bench correct.
            MonsterUnit starter = current.FirstOrDefault(u => u != null &&
                string.Equals(MonsterTacticsU5Roster.BaseName(u.MonsterName), "Pebblin", StringComparison.OrdinalIgnoreCase)) ??
                current.FirstOrDefault(u => u != null && MonsterTacticsU5Roster.Find(u.MonsterName)?.Cost == 1);
            if (starter == null) { status = "Stage 1 needs at least one 1-cost starter in the scene."; return; }
            HexTile starterTile = starter.OccupiedTile;
            if (starterTile == null)
            {
                tiles.TryGetValue(new Vector2Int(3, 1), out starterTile);
                if (starterTile == null) starterTile = tiles.Values.FirstOrDefault(t => t.Row <= 3);
            }
            foreach (MonsterUnit unit in current)
            {
                if (unit == null) continue;
                if (unit != starter)
                {
                    removed.Add(unit);
                    unit.SetBenched();
                    Destroy(unit.gameObject); // runtime-only: does not change the scene file
                    continue;
                }
                MonsterTacticsU5Roster.Family family = MonsterTacticsU5Roster.Find(unit.MonsterName);
                MonsterTacticsU5Progression progress = unit.GetComponent<MonsterTacticsU5Progression>();
                if (progress == null) progress = unit.gameObject.AddComponent<MonsterTacticsU5Progression>();
                progress.Configure(family.Forms[0], 1, family.Cost);
                // The scene demo prefab can retain outdated serialized HP after
                // roster balance changes. Sync the free starter to current roster
                // data before the first PvE fight, just like shop-purchased units.
                SpriteRenderer starterRenderer = unit.GetComponentInChildren<SpriteRenderer>(true);
                unit.Configure(family.Forms[0], family.Type(1), family.Role, family.BaseHP,
                    starterTile, starterRenderer, arenaCamera);
                // U6.10.4: starter was placed in the original scene, not bought
                // from a runtime template. Apply the same catalog art to it too.
                ApplyUnitVisuals(unit, family, 1, starterRenderer);
                unit.gameObject.SetActive(true);
                unit.SetHealth(unit.MaximumHP);
            }
            // Starting with 10g gives enough flexibility to buy 1g bench units early.
            gold = 10;
            freeRerolls = 0;
            Rules.Clear();
            winStreak = 0;
            lossStreak = 0;
            hasIncomeReport = false;
            pendingPlanningIncome = 0;
            pendingPlanningPayout = false;
            playerLevel = 1;
            teamLimit = 1; // first round is one unit; later rounds auto-unlock board slots
            currentXp = 0; // one-time migration from U5's instant-level system
            RollShop();
            initialized = true;
            SyncBenchSlots();
            CreateWorldBench();
            status = "Drag units to move. In combat, buy and sell reserves only.";
            canvasHud = GetComponent<MonsterTacticsU5CanvasHud>();
            if (canvasHud == null) canvasHud = gameObject.AddComponent<MonsterTacticsU5CanvasHud>();
            canvasHud.Bind(this);
            // U6.7.1 item inventory is attached to the existing shop object so its
            // components persist across rounds without a separate scene setup step.
            MonsterTacticsItemInventory itemInventory = GetComponent<MonsterTacticsItemInventory>();
            if (itemInventory == null) itemInventory = gameObject.AddComponent<MonsterTacticsItemInventory>();
            itemInventory.Bind(this);
            // U6.9.3: item/component reward capsules use this same inventory.
            MonsterTacticsItemCapsules capsules = GetComponent<MonsterTacticsItemCapsules>();
            if (capsules == null) capsules = gameObject.AddComponent<MonsterTacticsItemCapsules>();
        }

        private void EnsureRuntimeTemplates(MonsterUnit[] currentUnits)
        {
            MonsterUnit fallback = currentUnits != null ? currentUnits.FirstOrDefault(u => u != null) : null;
            if (fallback == null) return;
            foreach (MonsterTacticsU5Roster.Family family in MonsterTacticsU5Roster.ActiveFamilies)
            {
                if (templates.ContainsKey(family.Forms[0])) continue;
                GameObject example = Instantiate(fallback.gameObject, templateContainer);
                example.name = family.Forms[0] + " - Runtime Template";
                example.SetActive(false);
                MonsterUnit runtime = example.GetComponent<MonsterUnit>();
                SpriteRenderer renderer = example.GetComponentInChildren<SpriteRenderer>(true);
                if (runtime != null)
                {
                    runtime.Configure(family.Forms[0], family.Type(1), family.Role, family.BaseHP,
                        null, renderer, arenaCamera);
                    ApplyUnitVisuals(runtime, family, 1, renderer);
                    runtime.SetBenched();
                    MonsterTacticsU5Progression progress = example.GetComponent<MonsterTacticsU5Progression>();
                    if (progress == null) progress = example.AddComponent<MonsterTacticsU5Progression>();
                    progress.Configure(family.Forms[0], 1, family.Cost);
                    templates[family.Forms[0]] = runtime;
                }
                else Destroy(example);
            }
        }

        private static bool UsesSingleFormVisual(MonsterTacticsU5Roster.Family family) =>
            family != null && !MonsterTacticsU5Roster.HasDistinctEvolution(family);

        private void ApplyUnitVisuals(MonsterUnit unit, MonsterTacticsU5Roster.Family family, int stage, SpriteRenderer renderer)
        {
            if (family == null || renderer == null) return;

            // U6.9.1f.1: Per-form Content Studio sprite overrides beat legacy art.
            Sprite customArt = MonsterTacticsContentRosterRuntime.FormSprite(family.Form(stage));
            if (customArt != null)
            {
                MonsterFrameAnimator overrideAnimator = renderer.GetComponent<MonsterFrameAnimator>();
                if (overrideAnimator == null)
                    overrideAnimator = renderer.gameObject.AddComponent<MonsterFrameAnimator>();
                // U6.10.3: use genuine imported form-specific frames when the artwork
                // matches the Content Catalog source. A Studio sprite override still wins.
                MonsterTacticsU610AnimatedArtCatalog.FormAnimation animation;
                if (MonsterTacticsU610AnimatedArtCatalog.TryGet(family.Form(stage), customArt, out animation))
                    overrideAnimator.SetAnimation(renderer, animation.Frames, animation.Durations);
                else
                    overrideAnimator.SetAnimation(renderer, new[] { customArt }, null);
            }
            else if (MonsterTacticsLegendarySpriteLibrary.HasSprite(family.Forms[0]))
            {
                Sprite[] frames = MonsterTacticsLegendarySpriteLibrary.GetAnimation(family.Forms[0]);
                if (frames.Length == 0) frames = new[] { MonsterTacticsLegendarySpriteLibrary.GetSprite(family.Forms[0]) };
                MonsterFrameAnimator animator = renderer.GetComponent<MonsterFrameAnimator>();
                if (animator == null) animator = renderer.gameObject.AddComponent<MonsterFrameAnimator>();
                if (frames.Length > 0 && frames[0] != null) animator.SetAnimation(renderer, frames, null);
            }

            // U6.10.4: both the original and new playable families use the same
            // frame animator and fallback motion after the opt-in art installer.
            // New units are 0.90x by default; original units keep their baseline.
            bool newFamily = MonsterTacticsU610SpriteMotion.IsU610Family(family.Forms[0]);
            MonsterTacticsU6104ArtSettings visualSettings = MonsterTacticsU6104ArtSettings.Current;
            bool unifyOriginal = visualSettings != null && visualSettings.UnifiedLegacyArt &&
                MonsterTacticsContentRosterRuntime.FormSprite(family.Form(stage)) != null;
            if (newFamily || unifyOriginal || MonsterTacticsU6106ScaleProfile.IsActive)
            {
                MonsterTacticsU610SpriteMotion motion = renderer.GetComponent<MonsterTacticsU610SpriteMotion>();
                if (motion == null) motion = renderer.gameObject.AddComponent<MonsterTacticsU610SpriteMotion>();
                float scale = MonsterTacticsU6106ScaleProfile.PlayableScale(
                    family.Forms[0], newFamily, visualSettings);
                motion.Configure(unit, scale);
            }

            MonsterTacticsStarAura aura = renderer.GetComponent<MonsterTacticsStarAura>();
            if (UsesSingleFormVisual(family))
            {
                if (aura == null) aura = renderer.gameObject.AddComponent<MonsterTacticsStarAura>();
                aura.SetStage(stage);
            }
            else if (aura != null)
            {
                aura.SetStage(1);
            }
        }

        private void Update()
        {
            if (!initialized) InitializeIfReady();
            if (!initialized) return;
            if (combat != null)
            {
                string phase = combat.MatchPhase;
                if (lastPhase != "Results" && phase == "Results")
                {
                    // Snapshot income ONCE at Results. Only a PvP victory bonus
                    // is available immediately; other income waits until the next
                    // Planning phase, matching the TFT round-payout cadence.
                    int interest = InterestNextRound;
                    bool won = combat.LastResult == "VICTORY";
                    bool lost = combat.LastResult == "DEFEAT";
                    bool isPve = IsPveRound;
                    if (!isPve)
                    {
                        if (won) { winStreak++; lossStreak = 0; }
                        else if (lost) { lossStreak++; winStreak = 0; }
                        else { winStreak = 0; lossStreak = 0; }
                    }
                    int immediatePvpWin = !isPve && won ? Rules.Get(MonsterTacticsRuleId.PvpWinGold) : 0;
                    int streakBonus = isPve ? 0 : StreakBonus(won ? winStreak : lost ? lossStreak : 0);
                    pendingPlanningIncome = BaseRoundIncome + interest + streakBonus;
                    pendingPlanningPayout = true;
                    lastBaseIncome = BaseRoundIncome;
                    lastInterestIncome = interest;
                    lastVictoryIncome = immediatePvpWin;
                    lastStreakIncome = streakBonus;
                    lastTotalIncome = pendingPlanningIncome + immediatePvpWin;
                    hasIncomeReport = true;
                    // No Results-phase base/interest/streak payout and no PvE win gold.
                    if (immediatePvpWin > 0) GrantGold(immediatePvpWin, "pvp-win");
                    EmitGameplayEvent(MonsterTacticsGameplayEventKind.RoundResolved, "results",
                        won ? 1 : lost ? -1 : 0);
                    lastResult = combat.LastResult;
                    battleNumber++;
                    string streakText = isPve ? "PvP streak paused" :
                        won ? winStreak + " wins" : lost ? lossStreak + " losses" : "no streak";
                    status = lastResult + (immediatePvpWin > 0 ? "  +1g PvP win now." : ".") +
                        "  +" + pendingPlanningIncome + "g at next planning (" + lastBaseIncome + " base, " +
                        interest + " interest, " + streakBonus + " streak; " + streakText + ").";
                }
                // Reconcile at phase transition, and CREDIT round income only now.
                if (phase == "Planning" && lastPhase != "Planning")
                {
                    if (pendingPlanningPayout)
                    {
                        GrantGold(pendingPlanningIncome, "round-income");
                        status = "Planning income received: +" + pendingPlanningIncome + "g (" +
                            lastBaseIncome + " base, " + lastInterestIncome + " interest, " +
                            lastStreakIncome + " streak).";
                        pendingPlanningIncome = 0;
                        pendingPlanningPayout = false;
                    }
                    EmitGameplayEvent(MonsterTacticsGameplayEventKind.PlanningStarted, "phase");
                    foreach (MonsterTacticsU5Roster.Family family in MonsterTacticsU5Roster.ActiveFamilies)
                    {
                        TryCombine(family, 1);
                        TryCombine(family, 2);
                    }
                }
                if (phase != lastPhase)
                {
                    EmitGameplayEvent(MonsterTacticsGameplayEventKind.PhaseChanged, phase);
                    if (phase == "Battle")
                        EmitGameplayEvent(MonsterTacticsGameplayEventKind.CombatStarted, "phase");
                }
                lastPhase = phase;
            }

            if (!CanManageShop)
            {
                ResetGuiDrag();
                pressedHudAction = -1;
                boardDragActive = false;
                boardDraggedUnit = null;
                return;
            }

            if (WasPressed(KeyCode.Delete) && selected != null && CanSellUnit(selected)) TrySell(selected);
            SyncBenchSlots();
            var daycare = GetComponent<MonsterTacticsAugmentManager>();
            if (daycare != null && daycare.HasDaycare) daycare.TryFulfillDaycareCopies();
            RefreshWorldBench();
            HandleGuiDragAndClicks();
        }

        private void HandleGuiDragAndClicks()
        {
            ReadPointer(out Vector2 pointer, out bool leftDown, out bool leftHeld, out bool leftUp);
            dragPointerScreen = pointer;
            Vector2 guiPoint = ScreenToGui(pointer);

            if (leftDown)
            {
                int benchIndex = GetBenchSlotAtScreen(pointer);
                if (benchIndex >= 0 && HasAnvilAt(benchIndex))
                {
                    // U6.9.5: press starts a drag; a click alone must NOT open the anvil.
                    draggedAnvilSlot = benchIndex;
                    dragStartScreen = pointer;
                    guiDragging = false;
                    return;
                }
                if (benchIndex >= 0 && benchSlots[benchIndex] != null)
                {
                    draggedBenchUnit = benchSlots[benchIndex];
                    dragStartScreen = pointer;
                    guiDragging = false;
                    return;
                }

                if (canvasHud != null && canvasHud.IsReady)
                {
                    int action = canvasHud.ActionIndexAt(pointer);
                    if (action >= 0)
                    {
                        pressedHudAction = action;
                        return;
                    }
                }
                int shopIndex = ShopCardIndexAt(guiPoint);
                if (shopIndex >= 0 && shop[shopIndex] != null)
                {
                    draggedShopIndex = shopIndex;
                    dragStartScreen = pointer;
                    guiDragging = false;
                }
            }

            if ((draggedBenchUnit != null || draggedShopIndex >= 0 || draggedAnvilSlot >= 0) && leftHeld)
            {
                if (!guiDragging && Vector2.Distance(pointer, dragStartScreen) >= GuiDragThreshold)
                    guiDragging = true;
            }

            if (!leftUp) return;

            if (pressedHudAction >= 0)
            {
                if (canvasHud != null && canvasHud.IsReady &&
                    canvasHud.ActionIndexAt(pointer) == pressedHudAction)
                    canvasHud.ActivateAction(pressedHudAction);
                pressedHudAction = -1;
                return;
            }

            if (draggedAnvilSlot >= 0)
            {
                int fromSlot = draggedAnvilSlot;
                if (!guiDragging)
                    status = "Drag the anvil into the shop / SELL area to open four choices.";
                else if (IsOverSellArea(pointer))
                {
                    // This opens a choice panel, NEVER sells the anvil or gives gold.
                    if (cachedAnvils != null && cachedAnvils.OpenCapsule(fromSlot))
                        status = "Choose one of four rewards for your Item Bench.";
                    else
                        status = "Unable to open anvil right now. It remains on your Monster Bench.";
                }
                else status = "Anvil returned to Monster Bench. Drag to shop to open.";
                ResetGuiDrag();
                return;
            }

            if (draggedBenchUnit != null)
            {
                MonsterUnit unit = draggedBenchUnit;
                if (!guiDragging)
                {
                    // Click only opens the inspection panel. Never queues a deployment.
                    selected = unit;
                    if (inspector == null) inspector = FindAnyObjectByType<MonsterTacticsMonsterInspector>();
                    if (inspector != null) inspector.Inspect(unit);
                    status = unit.MonsterName + (IsCombatRunning ?
                        " selected. Combat: drag on the bench or to SELL." :
                        " selected. Drag to move or sell.");
                }
                else if (IsOverSellArea(pointer))
                    TrySell(unit);
                else
                {
                    int destinationSlot = GetBenchSlotAtScreen(pointer);
                    if (destinationSlot >= 0) TryMoveBenchToSlot(unit, destinationSlot);
                    else if (CanManageBoard)
                    {
                        HexTile tile = FindTileAtScreen(pointer);
                        if (tile != null) TryDeploy(unit, tile);
                        else status = "Invalid drop — Monster stayed on the bench.";
                    }
                    else status = "Board locked outside planning — reserve stayed on the bench.";
                }
                ResetGuiDrag();
                return;
            }

            if (draggedShopIndex >= 0)
            {
                // Shop purchasing remains a deliberate click; dragging shop cards
                // cannot buy accidentally while rearranging Monster.
                if (!guiDragging && ShopCardIndexAt(guiPoint) == draggedShopIndex)
                    Buy(draggedShopIndex);
                ResetGuiDrag();
            }
        }

        private void ResetGuiDrag()
        {
            draggedBenchUnit = null;
            draggedAnvilSlot = -1;
            draggedShopIndex = -1;
            guiDragging = false;
        }

        private static bool WasPressed(KeyCode key)
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard k = Keyboard.current;
            if (k == null) return false;
            if (key == KeyCode.B) return k.bKey.wasPressedThisFrame;
            if (key == KeyCode.Delete) return k.deleteKey.wasPressedThisFrame;
            return false;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(key);
#else
            return false;
#endif
        }

        private static void MousePosition(out Vector2 pointer)
        {
#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            pointer = mouse != null ? mouse.position.ReadValue() : Vector2.zero;
#elif ENABLE_LEGACY_INPUT_MANAGER
            pointer = Input.mousePosition;
#else
            pointer = Vector2.zero;
#endif
        }

        private static bool MouseLeftDown(out Vector2 pointer)
        {
#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            pointer = mouse != null ? mouse.position.ReadValue() : Vector2.zero;
            return mouse != null && mouse.leftButton.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            pointer = Input.mousePosition;
            return Input.GetMouseButtonDown(0);
#else
            pointer = Vector2.zero;
            return false;
#endif
        }

        private static void ReadPointer(out Vector2 pointer, out bool down, out bool held, out bool up)
        {
#if ENABLE_INPUT_SYSTEM
            Mouse mouse = Mouse.current;
            pointer = mouse != null ? mouse.position.ReadValue() : Vector2.zero;
            down = mouse != null && mouse.leftButton.wasPressedThisFrame;
            held = mouse != null && mouse.leftButton.isPressed;
            up = mouse != null && mouse.leftButton.wasReleasedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            pointer = Input.mousePosition;
            down = Input.GetMouseButtonDown(0);
            held = Input.GetMouseButton(0);
            up = Input.GetMouseButtonUp(0);
#else
            pointer = Vector2.zero;
            down = false;
            held = false;
            up = false;
#endif
        }

        private IEnumerable<MonsterUnit> LiveUnits()
        {
            if (placement == null) yield break;
            foreach (MonsterUnit unit in placement.GetComponentsInChildren<MonsterUnit>(true))
                if (unit != null && !removed.Contains(unit)) yield return unit;
        }

        // A KO'd Monster is temporarily inactive but STILL owns a board hex.
        // Treating activeSelf == false as "benched" caused phantom reserve copies
        // during combat; only OccupiedTile == null means truly on the bench.
        public bool IsDeployed(MonsterUnit unit) =>
            unit != null && !removed.Contains(unit) && unit.OccupiedTile != null;
        /// <summary>Keep each bench position stable, including empty spaces.</summary>
        private void SyncBenchSlots()
        {
            List<MonsterUnit> all = LiveUnits().ToList();
            HashSet<MonsterUnit> seen = new HashSet<MonsterUnit>();
            for (int i = 0; i < BenchCapacity; i++)
            {
                MonsterUnit unit = benchSlots[i];
                if (unit == null || !all.Contains(unit) || IsDeployed(unit) || !seen.Add(unit))
                    benchSlots[i] = null;
            }
            foreach (MonsterUnit unit in all)
            {
                if (unit == null || IsDeployed(unit) || seen.Contains(unit)) continue;
                int empty = FreeBenchSlot();
                if (empty < 0) break; // transient third copy can still combine when bench is full
                benchSlots[empty] = unit;
                seen.Add(unit);
            }
        }

        private List<MonsterUnit> Bench()
        {
            SyncBenchSlots();
            return benchSlots.Where(unit => unit != null).ToList();
        }

        /// <summary>Which family occupies a numbered reserve hex (0-8).</summary>
        public int EmptyBenchSlots
        {
            get
            {
                if (!initialized) return 0;
                SyncBenchSlots();
                int empty = 0;
                for (int i = 0; i < BenchCapacity; i++)
                    if (benchSlots[i] == null && !HasAnvilAt(i)) empty++;
                return empty;
            }
        }

        // Eviolite's one-time reward uses the SAME copy/merge path as Daycare.
        // Never creates a disabled family or a hidden temporary shop-only copy.
        public bool TryGrantRandomCostCopy(int cost)
        {
            if (!initialized || cost < 1 || cost > 5 || EmptyBenchSlots <= 0) return false;
            var candidates = new List<MonsterTacticsU5Roster.Family>();
            foreach (var family in MonsterTacticsU5Roster.ActiveFamilies)
                if (family != null && family.Cost == cost &&
                    !IsFamilyCompleted(family) && templates.ContainsKey(family.Forms[0]))
                    candidates.Add(family);
            if (candidates.Count == 0) return false;
            var chosen = candidates[UnityEngine.Random.Range(0, candidates.Count)];
            return TryGrantDaycareCopy(chosen.Forms[0]);
        }

        /// <summary>Free 5-cost Legendary of a requested star stage, using existing
        /// bench/evolution creation, with no shop purchase or pool reservation.
        /// Returns false on a full bench so the augment may retry next planning.
        /// The caller supplies a deterministic index, not Unity global RNG.</summary>
        public bool TryGrantLegendaryFiveCost(int stage, int randomIndex)
        {
            if (!initialized || stage < 1 || stage > 3 || EmptyBenchSlots <= 0) return false;
            var candidates = new List<MonsterTacticsU5Roster.Family>();
            foreach (var family in MonsterTacticsU5Roster.ActiveFamilies)
                if (family != null && family.Cost == 5 &&
                    !IsFamilyCompleted(family) && templates.ContainsKey(family.Forms[0]))
                    candidates.Add(family);
            if (candidates.Count == 0) return false;
            var chosen = candidates[(randomIndex & int.MaxValue) % candidates.Count];
            MonsterUnit copy = CreateCopy(chosen, stage, 0);
            if (copy == null) return false;
            SyncBenchSlots();
            if (IsPlanning) TryCombine(chosen, stage);
            status = "Augment reward: free " + stage + "-star " + chosen.Forms[0] + "!";
            return true;
        }

        /// <summary>
        /// U6.11.8: Eligibility for a physical Monster reward ball. This only checks
        /// roster/template ownership eligibility; the ball may spawn while the bench
        /// is full and must remain uncollected until the player frees a slot.
        /// </summary>
        private MonsterTacticsU5Roster.Family LootFamily(string familyName)
        {
            if (!initialized || string.IsNullOrWhiteSpace(familyName)) return null;
            MonsterTacticsU5Roster.Family family = MonsterTacticsU5Roster.ActiveFamilies.FirstOrDefault(
                f => f != null && f.Forms != null && f.Forms.Length == 3 &&
                    string.Equals(f.Forms[0], familyName, StringComparison.OrdinalIgnoreCase));
            return family != null &&
                templates.TryGetValue(family.Forms[0], out MonsterUnit template) && template != null
                    ? family : null;
        }

        public bool CanOfferLootMonster(string familyName)
        {
            MonsterTacticsU5Roster.Family family = LootFamily(familyName);
            return family != null && !IsFamilyCompleted(family);
        }

        /// <summary>
        /// A real one-star Monster from a Creature Orb; does not spend gold, reroll,
        /// or reserve copies from a shop offer. Returns false without consuming the
        /// physical ball if there is no bench space or the match is not ready.
        /// </summary>
        // Previously spawned balls remain claimable if the family reached 3★
        // after the ball rolled. Only NEW ball rolls exclude completed families.
        public bool CanReceiveLootMonster(string familyName) =>
            CanManageShop && EmptyBenchSlots > 0 && LootFamily(familyName) != null;

        public bool TryGrantLootMonster(string familyName)
        {
            if (!CanReceiveLootMonster(familyName)) return false;
            MonsterTacticsU5Roster.Family family = MonsterTacticsU5Roster.ActiveFamilies.FirstOrDefault(
                f => f != null && string.Equals(f.Forms[0], familyName, StringComparison.OrdinalIgnoreCase));
            if (family == null) return false;
            MonsterUnit copy = CreateCopy(family, 1, 0);
            if (copy == null) return false;
            SyncBenchSlots();
            // In combat, only bench residents may merge, matching existing rules.
            TryCombine(family, 1);
            status = "Creature Orb reward: +1 " + family.Forms[0] + " (" + family.Cost + "g)!";
            return true;
        }

        /// <summary>Number of distinct ACTIVATED origins, not elemental types or roles.
        /// Uses the same count/breakpoint rules as the active trait HUD.</summary>
        public int ActiveOriginTraitCount()
        {
            if (!initialized) return 0;
            var counts = MonsterTacticsTraits.CountDeployed(LiveUnits());
            int total = 0;
            foreach (var def in MonsterTacticsTraits.All)
                if (def != null && def.Origin && MonsterTacticsTraits.ActiveTier(def.Name, counts) > 0)
                    total++;
            return total;
        }

        public string BenchFamilyAtSlot(int index)
        {
            if (!initialized || index < 0 || index >= BenchCapacity) return "";
            SyncBenchSlots();
            MonsterUnit unit = benchSlots[index];
            if (unit == null) return "";
            var progress = unit.GetComponent<MonsterTacticsU5Progression>();
            return progress != null ? progress.FamilyName : MonsterTacticsU5Roster.BaseName(unit.MonsterName);
        }

        /// <summary>Daycare creates a free one-star copy through the same evolution
        /// pipeline as shop purchases, but without charging gold or consuming shop odds.
        /// Returns false if full/missing so the manager retains the pending reward.</summary>
        public bool TryGrantDaycareCopy(string familyName)
        {
            if (!initialized || string.IsNullOrEmpty(familyName)) return false;
            var family = MonsterTacticsU5Roster.Find(familyName);
            if (family == null || !templates.ContainsKey(family.Forms[0])) return false;
            SyncBenchSlots();
            if (FreeBenchSlot() < 0) return false;
            MonsterUnit copy = CreateCopy(family, 1, 0);
            if (copy == null) return false;
            SyncBenchSlots();
            if (IsPlanning) TryCombine(family, 1);
            status = "Daycare produced a free 1-star " + family.Forms[0] + "!";
            return true;
        }

#if UNITY_EDITOR
        /// <summary>
        /// U6.11 debug-only grant: creates real owned Monster via the normal shop
        /// spawn/evolution/art path, but charges no gold and changes no shop pool.
        /// Editor-only; not included in player builds. Grant during Planning.
        /// </summary>
        public bool TryGrantTestMonster(string formName, int stars, int copies,
            bool deployImmediately, out string result)
        {
            result = "";
            if (!Application.isPlaying || !initialized)
            {
                result = "Enter Play Mode and wait for the shop to initialize.";
                return false;
            }
            if (!CanManageBoard)
            {
                result = "Grant units during Planning, with no augment/anvil choice open.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(formName) || stars < 1 || stars > 3 ||
                copies < 1 || copies > 9)
            {
                result = "Choose an enabled Monster, 1–3 stars, and 1–9 copies.";
                return false;
            }

            // ActiveFamilies excludes disabled Content Studio entries. Do not
            // fall back to legacy families that are disabled in the live roster.
            MonsterTacticsU5Roster.Family family = MonsterTacticsU5Roster.ActiveFamilies.FirstOrDefault(
                candidate => candidate != null && candidate.Forms.Any(form =>
                    string.Equals(form, formName.Trim(), StringComparison.OrdinalIgnoreCase)));
            if (family == null)
            {
                result = "That Monster is not enabled in the current roster.";
                return false;
            }
            if (!templates.TryGetValue(family.Forms[0], out MonsterUnit template) || template == null)
            {
                result = "No runtime template for " + family.Forms[0] + ". Check scene setup.";
                return false;
            }
            if (deployImmediately && copies != 1)
            {
                result = "Grant + Deploy works for one copy at a time.";
                return false;
            }

            // Preflight the entire grant before spawning anything: no silent
            // lost copies and no partially completed multi-grant operations.
            SyncBenchSlots();
            int free = EmptyBenchSlots;
            if (free < copies)
            {
                result = "Need " + copies + " empty bench slots; only " + free + " available. " +
                    "Deploy or sell a unit first.";
                return false;
            }
            HexTile deploymentTile = null;
            if (deployImmediately)
            {
                if (DeployedCount() >= TeamLimit)
                {
                    result = "Your board is at the team limit; grant to bench instead.";
                    return false;
                }
                deploymentTile = tiles.Values
                    .Where(tile => tile != null && tile.IsPlayerSide &&
                        !LiveUnits().Any(other => IsDeployed(other) && other.OccupiedTile == tile))
                    .OrderByDescending(tile => tile.Row)
                    .ThenBy(tile => Mathf.Abs(tile.Column - 3.5f))
                    .ThenBy(tile => tile.Column)
                    .FirstOrDefault();
                if (deploymentTile == null)
                {
                    result = "No free player-side hex. Grant to bench instead.";
                    return false;
                }
            }

            MonsterUnit first = null;
            for (int i = 0; i < copies; i++)
            {
                MonsterUnit unit = CreateCopy(family, stars, 0);
                if (unit == null)
                {
                    // Template validity is checked above; this is defensive.
                    result = "Unable to instantiate " + family.Forms[0] + ".";
                    return false;
                }
                if (first == null) first = unit;
            }
            SyncBenchSlots();
            if (stars < 3) TryCombine(family, stars);
            bool deployed = false;
            if (deployImmediately && first != null && !removed.Contains(first))
                deployed = TryDeploy(first, deploymentTile);

            result = "Granted " + copies + " free " + stars + "★ " + family.Form(stars) +
                (copies == 1 ? "" : " copies") + (deployed ? " and deployed it." :
                ". Find it on the 9-slot bench; drag onto a green hex to test combat.");
            status = "TEST: " + result;
            Debug.Log("[MonsterTactics Unit Grant Tester] " + result, this);
            return true;
        }
#endif

        private int SlotOf(MonsterUnit unit) => Array.IndexOf(benchSlots, unit);
        private int FreeBenchSlot()
        {
            for (int i = 0; i < BenchCapacity; i++)
                if (benchSlots[i] == null && !HasAnvilAt(i)) return i;
            return -1;
        }
        private int DeployedCount() => LiveUnits().Count(IsDeployed);

        public void SelectFromBoard(MonsterUnit unit)
        {
            if (!initialized || !IsPlanning) return;
            if (unit != null && !LiveUnits().Contains(unit)) return;
            selected = unit;
        }

        // U6.2.4: Prototype finite shop pool. Offers reserve copies while visible.
        // The local pool is scoped to this player until multiplayer opponents exist.
        // Star-ups retain 3/9 underlying copies; those copies return on sale.
        private static int CopiesPerFamily(int cost)
        {
            switch (cost)
            {
                case 1: return 30;
                case 2: return 25;
                case 3: return 18;
                case 4: return 10;
                case 5: return 9;
                default: return 0;
            }
        }

        private bool IsFamilyCompleted(MonsterTacticsU5Roster.Family family)
        {
            return LiveUnits().Any(unit =>
            {
                MonsterTacticsU5Progression p = unit.GetComponent<MonsterTacticsU5Progression>();
                return p != null && p.Stage >= 3 &&
                    string.Equals(p.FamilyName, family.Forms[0], StringComparison.OrdinalIgnoreCase);
            });
        }

        private int AvailableCopies(MonsterTacticsU5Roster.Family family)
        {
            int committed = 0;
            foreach (MonsterUnit unit in LiveUnits())
            {
                MonsterTacticsU5Progression p = unit.GetComponent<MonsterTacticsU5Progression>();
                if (p == null || !string.Equals(p.FamilyName, family.Forms[0], StringComparison.OrdinalIgnoreCase))
                    continue;
                committed += p.Stage == 1 ? 1 : p.Stage == 2 ? 3 : 9;
            }
            for (int i = 0; i < ShopSlots; i++)
                if (shop[i] == family) committed++;
            return Mathf.Max(0, CopiesPerFamily(family.Cost) - committed);
        }

        private MonsterTacticsU5Roster.Family RollPooledOffer()
        {
            // Choose the cost tier first. Within that tier, weight each Monster
            // by the number of copies still in the pool (TFT-like bag sampling).
            int[] odds = ShopOddsForHud;
            int[] availablePerTier = new int[5];
            foreach (MonsterTacticsU5Roster.Family family in MonsterTacticsU5Roster.ActiveFamilies)
                if (!IsFamilyCompleted(family) && family.Cost >= 1 && family.Cost <= 5)
                    availablePerTier[family.Cost - 1] += AvailableCopies(family);

            int totalWeight = 0;
            for (int i = 0; i < 5; i++)
                if (availablePerTier[i] > 0) totalWeight += odds[i];
            if (totalWeight == 0) return null;

            int tierRoll = UnityEngine.Random.Range(0, totalWeight);
            int tier = 0;
            for (int i = 0; i < 5; i++)
            {
                if (availablePerTier[i] == 0) continue;
                tierRoll -= odds[i];
                if (tierRoll < 0) { tier = i + 1; break; }
            }
            if (tier == 0) return null;

            int copyRoll = UnityEngine.Random.Range(0, availablePerTier[tier - 1]);
            foreach (MonsterTacticsU5Roster.Family family in MonsterTacticsU5Roster.ActiveFamilies)
            {
                if (family.Cost != tier || IsFamilyCompleted(family)) continue;
                copyRoll -= AvailableCopies(family);
                if (copyRoll < 0) return family;
            }
            return null;
        }

        private void RollShop()
        {
            // All unsold offers go back into the pool before a reroll.
            for (int i = 0; i < ShopSlots; i++) shop[i] = null;
            for (int i = 0; i < ShopSlots; i++) shop[i] = RollPooledOffer();
        }

        /// <summary>Each completed round opens a fresh TFT-style shop for free.</summary>
        public void RefreshForNewRound()
        {
            if (!initialized) return;
            RollShop();
            status = "New round: shop refreshed automatically.";
        }

        public void Reroll()
        {
            int cost = CurrentRerollCost;
            if (!CanManageShop || gold < cost)
            {
                status = "Need " + cost + " gold and an active match phase to reroll.";
                return;
            }
            if (freeRerolls > 0) freeRerolls--;
            else if (!TrySpendGold(cost, "shop-reroll")) return;
            RollShop();
            EmitGameplayEvent(MonsterTacticsGameplayEventKind.ShopRerolled, "shop", cost);
            status = cost == 0 ? "Shop rerolled for free." : "Shop rerolled for " + cost + " gold.";
        }

        /// <summary>One purchase gives 4 XP for 4 gold; it does NOT immediately increase level.</summary>
        public void BuyXP()
        {
            if (!CanManageShop)
            {
                status = "XP purchases are available during planning, combat and results.";
                return;
            }
            if (IsLevelCapped)
            {
                status = "Maximum level reached (Level 10).";
                return;
            }
            int cost = BuyXPCost;
            if (gold < cost)
            {
                status = "Need " + cost + " gold to purchase " + XPPerPurchase + " XP.";
                return;
            }
            if (!TrySpendGold(cost, "buy-xp")) return;
            AddXP(XPPerPurchase);
            EmitGameplayEvent(MonsterTacticsGameplayEventKind.XPGranted, "buy-xp", XPPerPurchase);
        }

        // Preserve the older Canvas/IMGUI action method in case another U5 script references it.
        public void LevelUpTeam() => BuyXP();

        /// <summary>Can be used later by U6.2 to grant round XP without charging gold.</summary>
        public void GrantXP(int xp)
        {
            if (!initialized || xp <= 0 || IsLevelCapped) return;
            AddXP(xp);
            EmitGameplayEvent(MonsterTacticsGameplayEventKind.XPGranted, "grant-xp", xp);
        }

        private void AddXP(int amount)
        {
            if (amount <= 0 || IsLevelCapped) return;
            currentXp += amount;
            int previousLevel = playerLevel;
            while (playerLevel < MaxPlayerLevel && currentXp >= XpRequiredByLevel[playerLevel])
            {
                currentXp -= XpRequiredByLevel[playerLevel];
                playerLevel++;
            }
            // At the cap, there is no more progress or spending.
            if (playerLevel >= MaxPlayerLevel) currentXp = 0;
            teamLimit = Mathf.Clamp(playerLevel, 1, MaxPlayerLevel);
            // One event per actual level reached, including multiple in one XP grant.
            for (int advanced = previousLevel + 1; advanced <= playerLevel; advanced++)
                EmitGameplayEvent(MonsterTacticsGameplayEventKind.PlayerLeveledUp, "level-up", advanced);
            status = playerLevel > previousLevel
                ? "LEVEL UP! Level " + playerLevel + " · team cap " + TeamLimit +
                  (IsLevelCapped ? " · MAX LEVEL" : " · " + currentXp + "/" + XPToNextLevel + " XP")
                : "+" + amount + " XP · Level " + playerLevel + " · " + currentXp + "/" + XPToNextLevel + " XP";
        }

#if UNITY_EDITOR
        /// <summary>
        /// Editor-only play-mode test resources. Does not touch win/loss streaks,
        /// shared pool counts, or normal XP progression.
        /// </summary>
        public void DebugPrepareBossTest(int targetLevel, int targetGold)
        {
            if (!Application.isPlaying || !initialized) return;
            playerLevel = Mathf.Clamp(targetLevel, 1, MaxPlayerLevel);
            currentXp = 0;
            teamLimit = playerLevel;
            gold = Mathf.Max(0, targetGold);
            RollShop();
            status = "BOSS TEST: Level " + playerLevel + " / " + gold + " gold. Buy and deploy your team.";
        }
#endif

        // Full-bench TFT rule: a shop purchase is allowed if it immediately upgrades
        // a Monster already owned, without consuming another permanent bench slot.
        // During Battle / Results we only combine real bench residents so deployed
        // (including KO'd) Monster remain protected until Planning.
        private int CountMergeableOneStarCopies(MonsterTacticsU5Roster.Family family)
        {
            if (family == null) return 0;
            return LiveUnits().Count(unit =>
            {
                MonsterTacticsU5Progression progress = unit.GetComponent<MonsterTacticsU5Progression>();
                return progress != null && progress.FamilyName == family.Forms[0] &&
                    progress.Stage == 1 && (IsPlanning || unit.OccupiedTile == null);
            });
        }

        private void Buy(int index)
        {
            if (!CanManageShop || index < 0 || index >= ShopSlots) return;
            MonsterTacticsU5Roster.Family family = shop[index];
            if (family == null || !templates.TryGetValue(family.Forms[0], out MonsterUnit template) || template == null)
            { status = "Missing species template. Reopen the U4.2 scene with all 10 Monster."; return; }
            if (gold < family.Cost) { status = "Not enough gold for " + family.Forms[0] + "."; return; }

            bool benchFull = EmptyBenchSlots <= 0;
            int copiesToBuy = 1;
            int secondSlot = -1;
            if (benchFull)
            {
                int alreadyOwned = CountMergeableOneStarCopies(family);
                if (alreadyOwned >= 2)
                {
                    // Buying the third copy merges immediately, even with a full bench.
                    copiesToBuy = 1;
                }
                else if (alreadyOwned == 1)
                {
                    // One owned + two offers -> buy the pair as one atomic upgrade.
                    // The player initiates this by clicking one of the two offers.
                    for (int i = 0; i < ShopSlots; i++)
                        if (i != index && shop[i] == family) { secondSlot = i; break; }
                    if (secondSlot < 0)
                    { status = "Bench full (9/9). Need another " + family.Forms[0] + " in this shop to upgrade."; return; }
                    copiesToBuy = 2;
                }
                else
                {
                    status = "Bench full (9/9). Sell or deploy a Monster first.";
                    return;
                }
            }

            int totalCost = family.Cost * copiesToBuy;
            if (gold < totalCost)
            {
                status = "Need " + totalCost + " gold to upgrade " + family.Forms[0] + " with a full bench.";
                return;
            }

            // Both offers are already reserved in the finite shop pool; buy exactly
            // those two slots and pay only once. Never auto-spend gold otherwise.
            CreateCopy(family, 1, family.Cost);
            if (secondSlot >= 0) CreateCopy(family, 1, family.Cost);
            TrySpendGold(totalCost, "unit-purchase");
            EmitGameplayEvent(MonsterTacticsGameplayEventKind.UnitPurchased, "shop", copiesToBuy,
                family.Forms[0]);
            shop[index] = null;
            if (secondSlot >= 0) shop[secondSlot] = null;
            status = copiesToBuy == 2
                ? "Bought two " + family.Forms[0] + " for " + totalCost + " gold."
                : "Bought " + family.Forms[0] + " for " + totalCost + " gold.";
            TryCombine(family, 1);
        }

        private MonsterUnit CreateCopy(MonsterTacticsU5Roster.Family family, int stage, int invested)
        {
            MonsterUnit source = templates[family.Forms[0]];
            GameObject copy = Instantiate(source.gameObject, placement.transform);
            copy.name = family.Forms[0];
            MonsterUnit unit = copy.GetComponent<MonsterUnit>();
            SpriteRenderer renderer = copy.GetComponentInChildren<SpriteRenderer>(true);
            unit.Configure(family.Forms[0], family.Type(1), family.Role, family.BaseHP,
                null, renderer, arenaCamera);
            MonsterTacticsU5Progression progress = copy.GetComponent<MonsterTacticsU5Progression>();
            if (progress == null) progress = copy.AddComponent<MonsterTacticsU5Progression>();
            progress.Configure(family.Forms[0], 1, invested);
            ApplyUnitVisuals(unit, family, 1, renderer);
            unit.SetBenched();
            if (stage > 1) ApplyStage(unit, family, stage, invested, false);
            return unit;
        }

        /// <summary>
        /// U6.5 AI opponent creation. Uses existing registered sprite templates,
        /// animations, evolution art and aura WITHOUT touching player inventory,
        /// gold, shop reservations or finite pool counts.
        /// </summary>
        public MonsterUnit CreateAiCombatUnit(MonsterTacticsU5Roster.Family family, int stars,
            Transform enemyParent, HexTile tile, Camera camera)
        {
            if (!initialized || family == null || enemyParent == null || tile == null ||
                !templates.TryGetValue(family.Forms[0], out MonsterUnit template) || template == null)
                return null;
            stars = Mathf.Clamp(stars, 1, 3);
            GameObject clone = Instantiate(template.gameObject, enemyParent);
            clone.SetActive(false);
            clone.name = "AI " + family.Form(stars) + " (" + stars + " star)";
            MonsterUnit unit = clone.GetComponent<MonsterUnit>();
            SpriteRenderer renderer = clone.GetComponentInChildren<SpriteRenderer>(true);
            if (unit == null || renderer == null)
            {
                Destroy(clone);
                return null;
            }
            MonsterTacticsU5Progression progression = clone.GetComponent<MonsterTacticsU5Progression>();
            if (progression == null) progression = clone.AddComponent<MonsterTacticsU5Progression>();
            progression.Configure(family.Forms[0], stars, 0);
            unit.Configure(family.Form(stars), family.Type(stars), family.Role,
                MonsterTacticsU5Roster.MaxHP(family, stars), tile, renderer, camera);
            if (stars > 1)
            {
                FormArt? art = FindEvolutionArt(family.Form(stars));
                if (art.HasValue && art.Value.Frames != null && art.Value.Frames.Length > 0)
                {
                    MonsterFrameAnimator animator = renderer.GetComponent<MonsterFrameAnimator>();
                    if (animator == null) animator = renderer.gameObject.AddComponent<MonsterFrameAnimator>();
                    animator.SetAnimation(renderer, art.Value.Frames, art.Value.Seconds);
                }
            }
            ApplyUnitVisuals(unit, family, stars, renderer);
            clone.SetActive(true);
            unit.SetSelected(false);
            return unit;
        }

        public void GrantTwoTestCopies()
        {
            if (!initialized || !IsPlanning || selected == null)
            { status = "Select a Monster during planning first."; return; }
            MonsterTacticsU5Progression progress = selected.GetComponent<MonsterTacticsU5Progression>();
            if (progress == null || progress.Stage >= 3)
            { status = "Select a 1★ or 2★ Monster to test combining."; return; }
            MonsterTacticsU5Roster.Family family = MonsterTacticsU5Roster.Find(progress.FamilyName);
            if (family == null || Bench().Count > BenchCapacity - 2)
            { status = "Need two free bench slots for the evolution test."; return; }
            int stage = progress.Stage;
            for (int i = 0; i < 2; i++) CreateCopy(family, stage, 0);
            TryCombine(family, stage);
            status = "Evolution test copies added for " + family.Form(stage) + ".";
        }

        private void TryCombine(MonsterTacticsU5Roster.Family family, int stage)
        {
            if (stage >= 3) return;
            List<MonsterUnit> matching = LiveUnits()
                .Where(u =>
                {
                    MonsterTacticsU5Progression p = u.GetComponent<MonsterTacticsU5Progression>();
                    return p != null && p.FamilyName == family.Forms[0] && p.Stage == stage &&
                        (IsPlanning || u.OccupiedTile == null);
                })
                .OrderByDescending(IsDeployed).ToList();
            while (matching.Count >= 3)
            {
                MonsterUnit keeper = matching[0];
                int invested = 0;
                for (int j = 0; j < 3; j++)
                {
                    MonsterUnit target = matching[j];
                    invested += target.GetComponent<MonsterTacticsU5Progression>().InvestedGold;
                    if (j == 0) continue;
                    // Merge equipped items deterministically into the surviving
                    // Monster. Overflow returns to the item bench (never deleted).
                    MonsterTacticsHeldItems oldItems = target.GetComponent<MonsterTacticsHeldItems>();
                    if (oldItems != null)
                    {
                        MonsterTacticsHeldItems keeperItems = keeper.GetComponent<MonsterTacticsHeldItems>();
                        if (keeperItems == null) keeperItems = keeper.gameObject.AddComponent<MonsterTacticsHeldItems>();
                        MonsterTacticsItemInventory itemBench = GetComponent<MonsterTacticsItemInventory>();
                        foreach (MonsterTacticsItemKind item in oldItems.TakeAll())
                        {
                            if (!keeperItems.TryAdd(item) && itemBench != null)
                                itemBench.Grant(item);
                        }
                    }
                    removed.Add(target);
                    if (selected == target) selected = keeper;
                    target.SetBenched();
                    Destroy(target.gameObject);
                }
                matching.RemoveRange(0, 3);
                ApplyStage(keeper, family, stage + 1, invested);
                EmitGameplayEvent(MonsterTacticsGameplayEventKind.UnitEvolved, "evolution", stage + 1,
                    family.Forms[0]);
                status = UsesSingleFormVisual(family)
                    ? family.Forms[0] + " upgraded to " + (stage + 1) + "★!"
                    : family.Forms[stage] + " evolved! 3 matching " + stage + "-star copies combined.";
                TryCombine(family, stage + 1);
            }
        }

        private void ApplyStage(MonsterUnit unit, MonsterTacticsU5Roster.Family family, int stage,
            int invested, bool playUpgradeFx = true)
        {
            bool deployed = IsDeployed(unit);
            HexTile tile = deployed ? unit.OccupiedTile : null;
            SpriteRenderer renderer = unit.GetComponentInChildren<SpriteRenderer>(true);
            unit.gameObject.name = family.Form(stage) + " (" + stage + " star)";
            unit.Configure(family.Form(stage), family.Type(stage), family.Role,
                MonsterTacticsU5Roster.MaxHP(family, stage), tile, renderer, arenaCamera);
            MonsterTacticsU5Progression progress = unit.GetComponent<MonsterTacticsU5Progression>();
            if (progress == null) progress = unit.gameObject.AddComponent<MonsterTacticsU5Progression>();
            progress.Configure(family.Forms[0], stage, invested);

            // Do not just replace SpriteRenderer.sprite: an existing frame animator
            // would overwrite it on the following frame. Switch its full clip.
            FormArt? art = FindEvolutionArt(family.Form(stage));
            if (art.HasValue && renderer != null)
            {
                FormArt asset = art.Value;
                MonsterFrameAnimator animator = renderer.GetComponent<MonsterFrameAnimator>();
                if (animator == null) animator = renderer.gameObject.AddComponent<MonsterFrameAnimator>();
                animator.SetAnimation(renderer, asset.Frames, asset.Seconds);
            }
            // Missing art: intentionally keep the previous sprite AND animation.
            // The upgraded name, stage, ability, and combat stats still apply.
            ApplyUnitVisuals(unit, family, stage, renderer);
            if (!deployed) unit.SetBenched();

            if (playUpgradeFx && stage >= 2)
            {
                if (deployed && renderer != null)
                {
                    MonsterTacticsEvolutionPulse pulse = renderer.GetComponent<MonsterTacticsEvolutionPulse>();
                    if (pulse == null) pulse = renderer.gameObject.AddComponent<MonsterTacticsEvolutionPulse>();
                    pulse.Play(stage);
                }
                Vector3 center = unit.transform.position + Vector3.up * .45f;
                if (!deployed)
                {
                    SyncBenchSlots();
                    int slot = SlotOf(unit);
                    if (worldBenchReady && slot >= 0 && worldTileTransforms[slot] != null)
                        center = worldTileTransforms[slot].position + Vector3.up * .62f;
                    else if (arenaCamera != null)
                        center = arenaCamera.ViewportToWorldPoint(new Vector3(.5f, .30f, 8f));
                }
                MonsterTacticsEvolutionFx.Spawn(center, stage, arenaCamera);
                status = UsesSingleFormVisual(family)
                    ? family.Forms[0] + " upgraded to " + stage + "★!"
                    : family.Form(stage) + " evolved to " + stage + "★!";
            }
        }

        private FormArt? FindEvolutionArt(string form)
        {
            foreach (FormArt art in evolutionArt)
                if (string.Equals(art.Form, form, StringComparison.OrdinalIgnoreCase) &&
                    art.Frames != null && art.Frames.Length > 0 && art.Frames[0] != null) return art;
            return null;
        }

        public bool TryBench(MonsterUnit unit)
        {
            SyncBenchSlots();
            return TryBenchAtSlot(unit, FreeBenchSlot());
        }

        /// <summary>Board to target bench slot. If occupied, swap its Monster onto the original hex.</summary>
        public bool TryBenchAtSlot(MonsterUnit unit, int slotIndex)
        {
            if (!IsPlanning || !IsDeployed(unit) || slotIndex < 0 || slotIndex >= BenchCapacity)
                return false;
            SyncBenchSlots();
            if (HasAnvilAt(slotIndex)) { status = "Reserve occupied by an anvil."; return false; }
            MonsterUnit occupant = benchSlots[slotIndex];
            HexTile original = unit.OccupiedTile;
            unit.SetBenched();
            benchSlots[slotIndex] = unit;
            if (occupant != null && occupant != unit)
            {
                occupant.gameObject.SetActive(true);
                occupant.PlaceOnTile(original);
                status = unit.MonsterName + " swapped with " + occupant.MonsterName + ".";
            }
            else status = unit.MonsterName + " moved to bench slot " + (slotIndex + 1) + ".";
            selected = unit;
            return true;
        }

        public bool TryMoveBenchToSlot(MonsterUnit unit, int targetIndex)
        {
            if (!CanManageBench || unit == null || targetIndex < 0 || targetIndex >= BenchCapacity) return false;
            SyncBenchSlots();
            int from = SlotOf(unit);
            if (from < 0) return false;
            if (from == targetIndex) return true;
            if (HasAnvilAt(targetIndex)) { status = "Reserve occupied by an anvil."; return false; }
            MonsterUnit other = benchSlots[targetIndex];
            benchSlots[targetIndex] = unit;
            benchSlots[from] = other;
            status = other == null
                ? unit.MonsterName + " moved to bench slot " + (targetIndex + 1) + "."
                : unit.MonsterName + " swapped with " + other.MonsterName + " on bench.";
            selected = unit;
            return true;
        }

        /// <summary>U6.9.1c: At the end of planning, automatically fill open
        /// player-side hexes from the 9-slot reserve bench, up to TeamLimit.
        /// Never moves existing deployed units, consumes shop stock, or exceeds cap.
        /// Deterministic bench order and center/frontline priority for open tiles.
        /// </summary>
        public int AutoDeployElidunebitBenchUnits()
        {
            if (!IsPlanning || !initialized || arenaRoot == null) return 0;
            SyncBenchSlots();
            int openCapacity = Mathf.Max(0, TeamLimit - DeployedCount());
            if (openCapacity == 0) return 0;
            var openTiles = arenaRoot.GetComponentsInChildren<HexTile>(true)
                .Where(tile => tile != null && tile.IsPlayerSide &&
                    !LiveUnits().Any(unit => IsDeployed(unit) && unit.OccupiedTile == tile))
                .OrderByDescending(tile => tile.Row)
                .ThenBy(tile => Mathf.Abs(tile.Column - 3.5f))
                .ThenBy(tile => tile.Column)
                .ToList();
            int deployed = 0;
            // TryDeploy already enforces team cap, tile ownership and source slot.
            foreach (MonsterUnit unit in benchSlots.ToArray())
            {
                if (deployed >= openCapacity || deployed >= openTiles.Count) break;
                if (unit == null || IsDeployed(unit)) continue;
                if (TryDeploy(unit, openTiles[deployed])) deployed++;
            }
            if (deployed > 0)
            {
                status = "Automatically deployed " + deployed + " reserve Monster.";
                Debug.Log("[U6.9.1c] Auto-deployed " + deployed +
                    " reserve Monster (team cap " + TeamLimit + ").", this);
            }
            return deployed;
        }

        public bool TryDeploy(MonsterUnit unit, HexTile tile)
        {
            if (!IsPlanning || unit == null || tile == null || !tile.IsPlayerSide) return false;
            SyncBenchSlots();
            int sourceSlot = SlotOf(unit);
            if (sourceSlot < 0) return false;
            MonsterUnit occupant = LiveUnits().FirstOrDefault(u => IsDeployed(u) && u.OccupiedTile == tile);
            if (occupant == null && DeployedCount() >= TeamLimit)
            {
                status = "Team full. Drag onto an occupied green hex to swap.";
                return false;
            }
            if (occupant != null) occupant.SetBenched();
            benchSlots[sourceSlot] = occupant; // a swap keeps both units in their logical slots
            unit.gameObject.SetActive(true);
            unit.PlaceOnTile(tile);
            selected = unit;
            status = occupant == null ? "Deployed " + unit.MonsterName + "."
                : "Swapped " + unit.MonsterName + " with " + occupant.MonsterName + ".";
            return true;
        }

        /// <summary>Item drops target actual owned Monster only, never enemy clones.</summary>
        public bool CanEquipItemOn(MonsterUnit unit)
        {
            // TFT: Items can be equipped on either deployed or benched owned
            // Monster while planning, battling, or viewing round results.
            // Opponent clones are excluded by the LiveUnits ownership check.
            return CanManageShop && unit != null && LiveUnits().Contains(unit);
        }

        /// <summary>TFT consumables cannot target deployed fighters mid-battle.</summary>
        public bool CanUseItemToolOn(MonsterUnit unit)
        {
            return CanEquipItemOn(unit) &&
                (!IsCombatRunning || (unit.OccupiedTile == null && SlotOf(unit) >= 0));
        }

        /// <summary>Apply the new item's stats/passives to a live battle fighter.</summary>
        public void NotifyCombatItemEquipped(MonsterUnit unit, MonsterTacticsItemKind[] before)
        {
            if (unit != null) EmitGameplayEvent(MonsterTacticsGameplayEventKind.ItemEquipped,
                "item-bench", 1, unit.MonsterName);
            if (IsCombatRunning && combat != null && unit != null && IsDeployed(unit))
                combat.NotifyOwnUnitItemEquipped(unit, before);
        }

        /// <summary>Resolve a drag end over a bench hex or any occupied player hex.</summary>
        public MonsterUnit ItemDropTargetAt(Vector2 pointer)
        {
            if (IsOverShopArea(pointer)) return null;
            SyncBenchSlots();
            int reserveSlot = GetBenchSlotAtScreen(pointer);
            if (reserveSlot >= 0) return benchSlots[reserveSlot];

            HexTile tile = FindTileAtScreen(pointer);
            if (tile != null)
            {
                MonsterUnit unit = LiveUnits().FirstOrDefault(candidate =>
                    candidate != null && IsDeployed(candidate) && candidate.OccupiedTile == tile);
                if (unit != null) return unit;
            }
            // Some animated sprites have no collider. Allow a close screen-space
            // drop on their body instead of requiring pinpoint tile selection.
            if (arenaCamera == null) return null;
            MonsterUnit closest = null;
            float best = 60f * 60f;
            foreach (MonsterUnit unit in LiveUnits())
            {
                if (unit == null || !IsDeployed(unit)) continue;
                Vector3 screen = arenaCamera.WorldToScreenPoint(
                    unit.transform.position + Vector3.up * .50f);
                if (screen.z < 0f) continue;
                float dx = pointer.x - screen.x;
                float dy = pointer.y - screen.y;
                float distance = dx * dx + dy * dy;
                if (distance >= best) continue;
                best = distance;
                closest = unit;
            }
            return closest;
        }

        public bool TrySell(MonsterUnit unit)
        {
            if (!CanManageShop || unit == null || !LiveUnits().Contains(unit) || !CanSellUnit(unit)) return false;
            int refund = SellRefund(unit);
            GrantGold(refund, "unit-sale");
            var progressAtSale = unit.GetComponent<MonsterTacticsU5Progression>();
            bool soldTwoStar = progressAtSale != null && progressAtSale.Stage == 2;
            EmitGameplayEvent(MonsterTacticsGameplayEventKind.UnitSold,
                soldTwoStar ? "shop-2star" : "shop", refund, unit.MonsterName);
            status = "Sold " + unit.MonsterName + " for " + refund + " gold.";
            if (selected == unit) selected = null;
            int slot = SlotOf(unit);
            if (slot >= 0) benchSlots[slot] = null;
            // Returning items happens before the unit is destroyed, regardless of
            // whether it was deployed or occupying a physical bench slot.
            MonsterTacticsHeldItems equipped = unit.GetComponent<MonsterTacticsHeldItems>();
            MonsterTacticsItemInventory inventory = GetComponent<MonsterTacticsItemInventory>();
            if (equipped != null && inventory != null)
                inventory.AddReturned(equipped.TakeAll());
            removed.Add(unit);
            unit.SetBenched();
            Destroy(unit.gameObject);
            return true;
        }

        private void Styles()
        {
            float scale = HudScale;
            if (head != null && Mathf.Abs(styleScale - scale) < .015f) return;
            styleScale = scale;
            head = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(18f * scale), fontStyle = FontStyle.Bold,
                clipping = TextClipping.Clip };
            head.normal.textColor = new Color(.92f, .97f, 1f);
            small = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(15f * scale), clipping = TextClipping.Clip };
            small.normal.textColor = new Color(.74f, .83f, .94f);
            centered = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(15f * scale),
                fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Clip };
            centered.normal.textColor = Color.white;
            tiny = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(12f * scale), alignment = TextAnchor.MiddleRight };
            tiny.normal.textColor = new Color(.78f, .89f, 1f);
            cardTitle = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(17f * scale),
                fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip };
            cardTitle.normal.textColor = Color.white;
            cardSub = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(12f * scale),
                alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip };
            cardSub.normal.textColor = new Color(.67f, .78f, .89f);
            cardCost = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(16f * scale),
                fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            cardCost.normal.textColor = new Color(1f, .86f, .36f);
            slotHint = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(12f * scale),
                alignment = TextAnchor.MiddleCenter };
            slotHint.normal.textColor = new Color(.64f, .76f, .86f);
            stageBadge = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(13f * scale),
                fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            stageBadge.normal.textColor = new Color(1f, .94f, .65f);
            benchNameStyle = new GUIStyle(cardTitle) { alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.RoundToInt(14f * scale) };
            statusStyle = new GUIStyle(small) { fontSize = Mathf.RoundToInt(12f * scale),
                clipping = TextClipping.Clip };
            actionStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = Mathf.RoundToInt(16f * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            // Retain fallback mini-hex textures for old custom camera layouts only.
            if (emptyHexTexture == null)
            {
                emptyHexTexture = BuildHexTexture(new Color(.25f, .43f, .40f), new Color(.40f, .55f, .58f));
                fullHexTexture = BuildHexTexture(new Color(.36f, .70f, .50f), new Color(.56f, .85f, .71f));
                selectedHexTexture = BuildHexTexture(new Color(.45f, .70f, .46f), new Color(1f, .81f, .29f));
                hoverHexTexture = BuildHexTexture(new Color(.45f, .78f, .60f), new Color(.80f, .99f, .82f));
            }
        }

        private void OnDestroy()
        {
            if (worldBenchRoot != null) Destroy(worldBenchRoot.gameObject);
            if (emptyHexTexture != null) Destroy(emptyHexTexture);
            if (fullHexTexture != null) Destroy(fullHexTexture);
            if (selectedHexTexture != null) Destroy(selectedHexTexture);
            if (hoverHexTexture != null) Destroy(hoverHexTexture);
        }

        /// <summary>A real pointy-top hex silhouette with bevel and shaded sidewalls.
        /// Pixel-generated once; no rectangular fake-hex rendering.</summary>
        private static Texture2D BuildHexTexture(Color surface, Color border)
        {
            const int w = 144;
            const int h = 108;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.name = "MonsterTactics U5 hex pad";
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            Color32[] pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float u = x / (float)(w - 1);
                    float v = y / (float)(h - 1);
                    bool shadow = InHex(u, (v - .018f) / .85f, 1f);
                    bool side = InHex(u, (v - .025f) / .86f, .97f);
                    bool outer = InHex(u, (v - .105f) / .86f, .96f);
                    bool inside = InHex(u, (v - .105f) / .86f, .90f);
                    Color color = Color.clear;
                    if (shadow) color = new Color(.025f, .045f, .065f, .93f);
                    if (side) color = new Color(.07f, .18f, .16f, 1f);
                    if (outer) color = border;
                    if (inside)
                    {
                        float lighting = .86f + .14f * Mathf.Clamp01(v);
                        color = new Color(surface.r * lighting, surface.g * lighting, surface.b * lighting, 1f);
                    }
                    pixels[y * w + x] = color;
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            return tex;
        }

        private static bool InHex(float u, float v, float scale)
        {
            if (v < 0f || v > 1f) return false;
            u = (u - .5f) / scale + .5f;
            v = (v - .5f) / scale + .5f;
            if (v < 0f || v > 1f) return false;
            float d = Mathf.Abs(v - .5f);
            float half = d <= .25f ? .50f : Mathf.Max(0f, .5f - (d - .25f) * 2f);
            return Mathf.Abs(u - .5f) <= half;
        }

        // All geometry here is expressed in actual game-view pixels. Input uses
        // those same coordinates, so scaling the HUD does not break drag targets.
        public Rect GetBenchPanelRect()
        {
            // Only used as a screen-space fallback if the world bench is not visible.
            float scale = HudScale;
            float width = Mathf.Min(Screen.width - 20f * scale, 1100f * scale);
            return new Rect((Screen.width - width) * .5f,
                Screen.height - 275f * scale, width, 82f * scale);
        }

        /// <summary>One bottom bar. All drag hit-tests and drawing use this identical Rect.</summary>
        private Rect GetShopPanelRect()
        {
            float scale = HudScale;
            // The existing trainer selector occupies the lower-left corner;
            // the U4 Start Battle button occupies the far lower-right.
            // Reserve both areas instead of allowing HUD overlap.
            float left = Mathf.Max(604f * scale, Screen.width * .295f);
            float right = Mathf.Max(169f * scale, Screen.width * .078f);
            float minimum = 575f * scale;
            if (Screen.width - left - right < minimum)
            {
                // Smaller screens: preserve shop usability and keep a visible edge.
                left = Mathf.Min(left, Screen.width * .27f);
                right = Mathf.Min(right, 20f * scale);
            }
            float width = Mathf.Max(350f, Screen.width - left - right);
            float height = 182f * scale;
            return new Rect(left, Screen.height - height - 26f * scale, width, height);
        }

        private Rect GetShopEconomyRect()
        {
            float scale = HudScale;
            Rect panel = GetShopPanelRect();
            float width = Mathf.Min(177f * scale, panel.width * .20f);
            return new Rect(panel.x + 8f * scale, panel.y + 9f * scale,
                width, panel.height - 18f * scale);
        }

        private Rect GetShopActionRect()
        {
            float scale = HudScale;
            Rect panel = GetShopPanelRect();
            float width = Mathf.Min(178f * scale, panel.width * .23f);
            return new Rect(panel.xMax - width - 8f * scale, panel.y + 9f * scale,
                width, panel.height - 18f * scale);
        }

        private Rect GetShopCardsRect()
        {
            float s = HudScale;
            Rect panel = GetShopPanelRect();
            Rect economy = GetShopEconomyRect();
            Rect actions = GetShopActionRect();
            float x = economy.xMax + 7f * s;
            return new Rect(x, panel.y + 12f * s,
                Mathf.Max(120f, actions.x - x - 7f * s), panel.height - 24f * s);
        }

        private Rect GetBenchSlotRect(int index)
        {
            Rect panel = GetBenchPanelRect();
            float cell = (panel.width - 28f) / BenchCapacity;
            return new Rect(panel.x + 12f + cell * index, panel.y + 12f, cell - 3f, panel.height - 22f);
        }

        private Rect GetShopCardRect(int index)
        {
            Rect cards = GetShopCardsRect();
            float s = HudScale;
            float cell = cards.width / ShopSlots;
            float gap = 2.5f * s;
            return new Rect(cards.x + index * cell + gap,
                cards.y + 2f * s, cell - gap * 2f, cards.height - 4f * s);
        }

        private int BenchSlotIndexAt(Vector2 guiPoint)
        {
            for (int i = 0; i < BenchCapacity; i++)
            {
                Rect slot = GetBenchSlotRect(i);
                if (slot.Contains(guiPoint)) return i;
            }
            return -1;
        }

        public int GetBenchSlotAtScreen(Vector2 pointerScreen)
        {
            if (IsOverShopArea(pointerScreen)) return -1;
            return useWorldBench ? GetWorldSlotAtScreen(pointerScreen) : BenchSlotIndexAt(ScreenToGui(pointerScreen));
        }

        private int ShopCardIndexAt(Vector2 guiPoint)
        {
            if (canvasHud != null && canvasHud.IsReady)
                return canvasHud.CardIndexAt(new Vector2(guiPoint.x, Screen.height - guiPoint.y));
            for (int i = 0; i < ShopSlots; i++)
                if (GetShopCardRect(i).Contains(guiPoint)) return i;
            return -1;
        }

        private static Vector2 ScreenToGui(Vector2 pointer) => new Vector2(pointer.x, Screen.height - pointer.y);

        public bool IsOverShopArea(Vector2 pointerScreen) =>
            canvasHud != null && canvasHud.IsReady ? canvasHud.ContainsShop(pointerScreen) :
            GetShopPanelRect().Contains(ScreenToGui(pointerScreen));

        public bool IsOverBenchArea(Vector2 pointerScreen)
        {
            // HUD must win over 3D mesh hits behind the shop.
            if (IsOverShopArea(pointerScreen)) return false;
            if (useWorldBench) return GetWorldSlotAtScreen(pointerScreen) >= 0;
            return GetBenchSlotAtScreen(pointerScreen) >= 0 ||
                GetBenchPanelRect().Contains(ScreenToGui(pointerScreen));
        }

        /// <summary>A dragged Monster may be sold anywhere over the bottom shop bar.
        /// A normal click never calls TrySell, so shop purchase remains unaffected.
        /// </summary>
        public bool IsOverSellArea(Vector2 pointerScreen) =>
            CanManageShop && IsOverShopArea(pointerScreen);

        /// <summary>Visual-only feedback for the Canvas drag ghost.
        /// Mirrors the current U5 board/bench/sell restrictions without changing drops.</summary>
        public string GetDragDropFeedbackForHud(out bool valid)
        {
            valid = false;
            if (HasAnvilDragForHud)
            {
                valid = IsOverShopArea(dragPointerScreen);
                return valid ? "OPEN 4 CHOICES" : "DRAG TO SHOP TO OPEN";
            }
            MonsterUnit unit = ActiveDraggedUnit;
            if (unit == null) return string.Empty;
            Vector2 pointer = ActiveDragPointer;
            if (IsOverShopArea(pointer))
            {
                valid = CanSellUnit(unit);
                return valid ? "SELL +" + SellRefund(unit) + "g" : "CANNOT SELL";
            }
            int reserve = GetBenchSlotAtScreen(pointer);
            if (reserve >= 0)
            {
                valid = CanManageBench;
                if (!valid) return "BENCH LOCKED";
                if (HasAnvilAt(reserve)) { valid = false; return "ANVIL OCCUPIES SLOT"; }
                MonsterUnit other = benchSlots[reserve];
                if (other == unit) return "RETURN TO BENCH";
                return other == null ? "MOVE TO BENCH" : "SWAP BENCH";
            }
            if (!CanManageBoard) return "BOARD LOCKED";
            HexTile destination = FindTileAtScreen(pointer);
            if (destination == null || !destination.IsPlayerSide)
                return "INVALID DROP";
            bool fromBench = unit.OccupiedTile == null;
            MonsterUnit occupant = LiveUnits().FirstOrDefault(other =>
                other != null && other != unit && IsDeployed(other) && other.OccupiedTile == destination);
            if (fromBench && occupant == null && DeployedCount() >= TeamLimit)
                return "TEAM FULL";
            valid = true;
            return occupant == null ? "PLACE ON HEX" : "SWAP POKÉMON";
        }

        private HexTile FindTileAtScreen(Vector2 pointer)
        {
            if (arenaCamera == null) return null;
            RaycastHit[] hits = Physics.RaycastAll(arenaCamera.ScreenPointToRay(pointer), 300f,
                ~0, QueryTriggerInteraction.Ignore);
            HexTile closest = null;
            float distance = float.MaxValue;
            foreach (RaycastHit hit in hits)
            {
                HexTile tile = hit.collider.GetComponent<HexTile>();
                if (tile == null || hit.distance >= distance) continue;
                closest = tile;
                distance = hit.distance;
            }
            return closest;
        }

        /// <summary>Build nine copies of the very same hex prism mesh used by the arena.
        /// No HexTile components are attached, so U4's 64 combat tiles are unchanged.</summary>
        private void CreateWorldBench()
        {
            if (!useWorldBench || worldBenchReady || arenaRoot == null) return;
            HexTile[] arenaTiles = arenaRoot.GetComponentsInChildren<HexTile>(true);
            HexTile sample = arenaTiles.FirstOrDefault(t => t != null && t.Row == 0);
            if (sample == null) return;
            MeshFilter sampleFilter = sample.GetComponent<MeshFilter>();
            MeshRenderer sampleRenderer = sample.GetComponent<MeshRenderer>();
            if (sampleFilter == null || sampleFilter.sharedMesh == null || sampleRenderer == null) return;

            // Use existing row positions to orient the reserve lane even if the scene
            // rotates or moves the entire arena later.
            List<HexTile> rowZero = arenaTiles.Where(t => t.Row == 0).OrderBy(t => t.Column).ToList();
            List<HexTile> rowOne = arenaTiles.Where(t => t.Row == 1).ToList();
            if (rowZero.Count < 2 || rowOne.Count == 0) return;
            Vector3 center0 = Vector3.zero;
            Vector3 center1 = Vector3.zero;
            foreach (HexTile t in rowZero) center0 += t.transform.position;
            foreach (HexTile t in rowOne) center1 += t.transform.position;
            center0 /= rowZero.Count;
            center1 /= rowOne.Count;
            Vector3 across = rowZero[1].transform.position - rowZero[0].transform.position;
            Vector3 forward = center0 - center1;
            across.y = 0f;
            forward.y = 0f;
            if (across.sqrMagnitude < .01f || forward.sqrMagnitude < .01f) return;
            across.Normalize();
            forward.Normalize();

            worldBenchRoot = new GameObject("MonsterTactics U5.2 Hex Bench (9 Reserves)").transform;
            worldBenchRoot.SetParent(arenaRoot, true);
            tileColors = new MaterialPropertyBlock();
            for (int i = 0; i < BenchCapacity; i++)
            {
                GameObject tile = new GameObject("Reserve Hex " + (i + 1));
                tile.transform.SetParent(worldBenchRoot, true);
                // Extra spacing is along the arena's own outward direction, not camera/world Z.
                tile.transform.position = center0 + forward * (worldBenchForwardOffset + worldBenchExtraGap) +
                    across * ((i - (BenchCapacity - 1) * .5f) * worldBenchSpacing);
                // Use the arena tile's orientation and scale for an exact visual match.
                tile.transform.rotation = sample.transform.rotation;
                tile.transform.localScale = sample.transform.lossyScale;
                MeshFilter filter = tile.AddComponent<MeshFilter>();
                filter.sharedMesh = sampleFilter.sharedMesh;
                MeshRenderer renderer = tile.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = sampleRenderer.sharedMaterial;
                MeshCollider collider = tile.AddComponent<MeshCollider>();
                collider.sharedMesh = sampleFilter.sharedMesh;
                // Trainer may stand on the reserve mesh even though it is not
                // a combat HexTile. U5.2.3 movement reads this marker.
                tile.AddComponent<MonsterTacticsBenchSurface>();
                worldTileTransforms[i] = tile.transform;
                worldTileRenderers[i] = renderer;
                worldTileColliders[i] = collider;

                GameObject visual = new GameObject("Reserve Monster Art");
                visual.transform.SetParent(tile.transform, true);
                SpriteRenderer sprite = visual.AddComponent<SpriteRenderer>();
                sprite.enabled = false;
                worldSpriteRenderers[i] = sprite;
            }
            worldBenchReady = true;
            Debug.Log("MonsterTactics U5.1.9: 9 reserve hexes; 64 combat hexes unchanged.");
            RefreshWorldBench();
        }

        private bool hoveredUnitForDrag(int slot, int hovering)
        {
            // Hover highlights only while actively dragging a reserve Monster.
            return CanManageBench && (guiDragging && draggedBenchUnit != null || boardDragActive) && hovering == slot;
        }

        private void RefreshWorldBench()
        {
            if (!worldBenchReady) return;
            int hovering = GetWorldSlotAtScreen(ActiveDragPointer);
            for (int i = 0; i < BenchCapacity; i++)
            {
                MonsterUnit unit = benchSlots[i];
                MeshRenderer tile = worldTileRenderers[i];
                SpriteRenderer sprite = worldSpriteRenderers[i];
                if (tile == null || sprite == null) continue;
                var augment = GetComponent<MonsterTacticsAugmentManager>();
                bool daycareHex = augment != null && augment.HasDaycare && augment.DaycareSlot == i;
                bool anvilHere = HasAnvilAt(i);
                Color color = hoveredUnitForDrag(i, hovering) ? new Color(.48f, .76f, .88f) :
                    anvilHere ? new Color(.44f, .63f, .85f) :
                    daycareHex ? new Color(.98f, .72f, .18f) :
                    unit != null && unit == selected ? new Color(.90f, .70f, .28f) :
                    unit != null ? new Color(.31f, .54f, .56f) : new Color(.19f, .33f, .39f);
                tile.GetPropertyBlock(tileColors);
                tileColors.SetColor("_BaseColor", color);
                tileColors.SetColor("_Color", color);
                tile.SetPropertyBlock(tileColors);
                if (unit == null)
                {
                    if (anvilHere && cachedAnvils != null)
                    {
                        Sprite capsuleSprite = cachedAnvils.BenchSprite(i);
                        sprite.enabled = capsuleSprite != null;
                        sprite.sprite = capsuleSprite;
                        sprite.color = (guiDragging && draggedAnvilSlot == i)
                            ? new Color(1f, 1f, 1f, .28f) : Color.white;
                        sprite.sortingOrder = 320;
                        // Sprite uses a feet/base pivot (see BenchSprite), not its center.
                        // Keep the lowest opaque pixel above the reserve hex top.
                        sprite.transform.position = worldTileTransforms[i].position +
                            Vector3.up * (worldBenchAnvilBaseHeight +
                                .018f * Mathf.Sin(Time.time * 2.5f + i));
                        sprite.transform.localScale = Vector3.one * worldBenchAnvilScale;
                        if (arenaCamera != null) sprite.transform.rotation = arenaCamera.transform.rotation;
                    }
                    else sprite.enabled = false;
                    MonsterTacticsStarAura emptyAura = sprite.GetComponent<MonsterTacticsStarAura>();
                    if (emptyAura != null) emptyAura.SetStage(1);
                    continue;
                }
                SpriteRenderer source = unit.GetComponentInChildren<SpriteRenderer>(true);
                if (source == null || source.sprite == null)
                {
                    sprite.enabled = false;
                    MonsterTacticsStarAura missingAura = sprite.GetComponent<MonsterTacticsStarAura>();
                    if (missingAura != null) missingAura.SetStage(1);
                    continue;
                }
                sprite.enabled = true;
                sprite.sprite = source.sprite;
                sprite.sharedMaterial = source.sharedMaterial;
                if (guiDragging && draggedBenchUnit == unit) sprite.color = new Color(1f, 1f, 1f, .30f);
                else
                {
                    MonsterTacticsU5Progression progress = unit.GetComponent<MonsterTacticsU5Progression>();
                    MonsterTacticsU5Roster.Family family = progress != null ? MonsterTacticsU5Roster.Find(progress.FamilyName) : MonsterTacticsU5Roster.Find(unit.MonsterName);
                    if (progress != null && family != null && UsesSingleFormVisual(family) && progress.Stage >= 2)
                        sprite.color = progress.Stage >= 3 ? new Color(1f, .92f, .62f, 1f) : new Color(.94f, .97f, 1f, 1f);
                    else sprite.color = Color.white;
                }
                sprite.sortingLayerID = source.sortingLayerID;
                sprite.sortingOrder = source.sortingOrder;
                float idleBob = .018f * Mathf.Sin(Time.time * 2.5f + i * .85f);
                sprite.transform.position = worldTileTransforms[i].position +
                    Vector3.up * (0.22f + source.transform.localPosition.y + idleBob);
                sprite.transform.localScale = source.transform.lossyScale;
                if (arenaCamera != null) sprite.transform.rotation = arenaCamera.transform.rotation;

                // The world bench displays a separate, inactive-unit sprite proxy.
                // Transfer the upgraded star aura onto that proxy as well.
                MonsterTacticsU5Progression benchProgress = unit.GetComponent<MonsterTacticsU5Progression>();
                MonsterTacticsU5Roster.Family benchFamily = benchProgress != null
                    ? MonsterTacticsU5Roster.Find(benchProgress.FamilyName)
                    : MonsterTacticsU5Roster.Find(unit.MonsterName);
                bool showStarAura = benchProgress != null && benchProgress.Stage >= 2 &&
                    benchFamily != null && UsesSingleFormVisual(benchFamily);
                MonsterTacticsStarAura benchAura = sprite.GetComponent<MonsterTacticsStarAura>();
                if (showStarAura)
                {
                    if (benchAura == null) benchAura = sprite.gameObject.AddComponent<MonsterTacticsStarAura>();
                    benchAura.SetStage(benchProgress.Stage);
                }
                else if (benchAura != null) benchAura.SetStage(1);
            }
        }

        private int GetWorldSlotAtScreen(Vector2 pointerScreen)
        {
            if (!worldBenchReady || arenaCamera == null) return -1;
            RaycastHit[] hits = Physics.RaycastAll(arenaCamera.ScreenPointToRay(pointerScreen),
                300f, ~0, QueryTriggerInteraction.Ignore);
            float distance = float.MaxValue;
            int slot = -1;
            foreach (RaycastHit hit in hits)
            {
                for (int i = 0; i < BenchCapacity; i++)
                {
                    if (hit.collider == worldTileColliders[i] && hit.distance < distance)
                    {
                        distance = hit.distance;
                        slot = i;
                    }
                }
            }
            return slot;
        }

        private bool CanDisplayWorldBench()
        {
            if (!worldBenchReady || arenaCamera == null) return false;
            Vector3 projected = arenaCamera.WorldToViewportPoint(worldTileTransforms[4].position);
            float shopEdge = (Screen.height - GetShopPanelRect().y) / Mathf.Max(1f, Screen.height);
            return projected.z > 0f && projected.y > shopEdge + .035f && projected.y < .94f;
        }

        private static void DrawSolid(Rect rect, Color color)
        {
            Color prev = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = prev;
        }

        private static void DrawOutline(Rect rect, Color color, float thickness = 1f)
        {
            DrawSolid(new Rect(rect.x, rect.y, rect.width, thickness), color);
            DrawSolid(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
            DrawSolid(new Rect(rect.x, rect.y, thickness, rect.height), color);
            DrawSolid(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
        }

        private static void DrawSprite(Rect bounds, Sprite sprite)
        {
            if (sprite == null || sprite.texture == null) return;
            Rect tr = sprite.textureRect;
            Texture2D texture = sprite.texture;
            Rect uv = new Rect(tr.x / texture.width, tr.y / texture.height,
                tr.width / texture.width, tr.height / texture.height);
            float aspect = tr.width / Mathf.Max(1f, tr.height);
            float w = Mathf.Min(bounds.width, bounds.height * aspect);
            float h = w / Mathf.Max(.01f, aspect);
            GUI.DrawTextureWithTexCoords(new Rect(bounds.center.x - w / 2f,
                bounds.center.y - h / 2f, w, h), texture, uv, true);
        }

        private Sprite GetUnitSprite(MonsterUnit unit)
        {
            if (unit == null) return null;
            SpriteRenderer renderer = unit.GetComponentInChildren<SpriteRenderer>(true);
            return renderer != null ? renderer.sprite : null;
        }

        private Sprite GetFamilySprite(MonsterTacticsU5Roster.Family family)
        {
            if (family == null) return null;
            return templates.TryGetValue(family.Forms[0], out MonsterUnit unit) ? GetUnitSprite(unit) : null;
        }

        private void DrawBenchSlot(Rect rect, MonsterUnit unit, int index, bool hovered)
        {
            bool active = unit != null;
            Color fill = hovered ? new Color(.12f, .24f, .36f, .98f) : active ? new Color(.09f, .16f, .24f, .97f) : new Color(.07f, .11f, .16f, .92f);
            Color edge = unit != null && unit == selected ? UiGold : hovered ? new Color(.40f, .72f, .95f) : new Color(.17f, .28f, .40f);
            DrawSolid(rect, fill);
            DrawOutline(rect, edge, 1.5f);
            Rect slotNo = new Rect(rect.x + 2f, rect.y + 1f, 14f, 10f);
            GUI.Label(slotNo, (index + 1).ToString(), tiny);
            if (!active)
            {
                GUI.Label(new Rect(rect.x + 12f, rect.y + 14f, rect.width - 24f, 12f), "EMPTY", cardSub);
                return;
            }
            Rect monster = new Rect(rect.x + 3f, rect.y + 4f, 28f, rect.height - 8f);
            DrawSprite(monster, GetUnitSprite(unit));
            string name = unit.MonsterName;
            GUIStyle fit = new GUIStyle(benchNameStyle);
            fit.fontSize = name.Length > 10 ? 8 : name.Length > 8 ? 9 : 10;
            fit.alignment = TextAnchor.MiddleLeft;
            GUI.Label(new Rect(rect.x + 30f, rect.y + 5f, rect.width - 58f, 12f), name, fit);
            MonsterTacticsU5Progression progress = unit.GetComponent<MonsterTacticsU5Progression>();
            int stars = progress != null ? progress.Stage : 1;
            GUI.Label(new Rect(rect.x + 30f, rect.y + 18f, rect.width - 60f, 10f), stars + "★  Bench", cardSub);
            MonsterTacticsHeldItems equipped = unit.GetComponent<MonsterTacticsHeldItems>();
            if (equipped != null && equipped.Items.Count > 0)
            {
                int shown = Mathf.Min(3,equipped.Items.Count);
                for (int k = 0; k < shown; k++)
                {
                    Rect mini = new Rect(rect.x + 31f + k * 21f,
                        rect.yMax - 22f,20f,20f);
                    MonsterTacticsItemArt.Draw(mini,equipped.Items[k]);
                    if (mini.Contains(Event.current.mousePosition))
                        MonsterTacticsItemTooltip.Draw(Event.current.mousePosition,equipped.Items[k]);
                }
            }
        }

        private static Color RarityColor(int cost)
        {
            switch (cost)
            {
                case 1: return new Color(.55f, .66f, .73f);
                case 2: return new Color(.35f, .75f, .51f);
                case 3: return new Color(.34f, .60f, .94f);
                case 4: return new Color(.72f, .48f, .94f);
                default: return new Color(.98f, .76f, .30f);
            }
        }

        private void DrawShopCard(Rect rect, MonsterTacticsU5Roster.Family family)
        {
            float s = HudScale;
            bool sold = family == null;
            bool canBuy = !sold && CanManageShop && gold >= family.Cost;
            Color rarity = sold ? new Color(.20f, .27f, .34f) : RarityColor(family.Cost);

            DrawSolid(rect, sold ? new Color(.055f, .075f, .095f, .91f) :
                new Color(.09f, .13f, .19f, .99f));
            DrawOutline(rect, canBuy ? rarity : rarity * .63f, 1.5f * s);
            DrawSolid(new Rect(rect.x + 2f*s, rect.y + 2f*s, rect.width - 4f*s, 4f*s), rarity);
            if (sold)
            {
                GUI.Label(rect, "SOLD", centered);
                return;
            }

            // Portrait-first composition: the sprite is the hero, not the text.
            Rect art = new Rect(rect.x + 6f*s, rect.y + 9f*s,
                rect.width - 12f*s, rect.height - 58f*s);
            DrawSolid(art, new Color(.055f, .080f, .117f, 1f));
            DrawSolid(new Rect(art.x, art.y, 5f*s, art.height), rarity * .65f);
            Sprite portrait = GetFamilySprite(family);
            if (portrait != null)
            {
                // Larger sprite, centered and scaled without distorting aspect ratio.
                float side = Mathf.Min(art.height - 2f*s, art.width - 12f*s);
                DrawSprite(new Rect(art.center.x - side*.5f, art.y + 1f*s,
                    side, side), portrait);
            }

            Rect details = new Rect(rect.x + 4f*s, rect.yMax - 47f*s,
                rect.width - 8f*s, 43f*s);
            DrawSolid(details, new Color(.035f, .060f, .085f, .99f));
            bool compact = rect.width < 155f*s;
            GUIStyle nameStyle = new GUIStyle(cardTitle);
            // Long names are adjusted before rendering rather than allowed to overflow.
            if (family.Forms[0].Length > 9)
                nameStyle.fontSize = Mathf.Max(11, nameStyle.fontSize - 2);
            GUI.Label(new Rect(details.x + 7f*s, details.y + 1f*s,
                details.width - (compact ? 14f : 45f)*s, 21f*s), family.Forms[0], nameStyle);
            string traitText = family.Type(1).Replace(" / ", "/") +
                (compact ? "" : "  ·  " + family.Role);
            GUIStyle traitStyle = new GUIStyle(cardSub);
            if (traitText.Length > 17)
                traitStyle.fontSize = Mathf.Max(9, traitStyle.fontSize - 2);
            GUI.Label(new Rect(details.x + 7f*s, details.y + 23f*s,
                details.width - (compact ? 14f : 48f)*s, 16f*s), traitText, traitStyle);

            Rect price = compact ?
                new Rect(rect.xMax - 38f*s, rect.y + 11f*s, 31f*s, 27f*s) :
                new Rect(details.xMax - 39f*s, details.y + 6f*s, 33f*s, 30f*s);
            DrawSolid(price, new Color(.11f, .17f, .19f, 1f));
            DrawOutline(price, rarity, .9f*s);
            GUI.Label(price, family.Cost + "g", cardCost);
        }

        private void DrawDraggedGhost()
        {
            if (!guiDragging || IsSellPreviewActive ||
                (draggedBenchUnit == null && !HasAnvilDragForHud)) return;
            float scale = HudScale;
            Rect ghost = new Rect(dragPointerScreen.x - 48f * scale,
                Screen.height - dragPointerScreen.y - 25f * scale, 116f * scale, 58f * scale);
            DrawSolid(ghost, new Color(.08f, .14f, .21f, .94f));
            DrawOutline(ghost, UiGold, 2f * scale);
            DrawSprite(new Rect(ghost.x + 5f * scale, ghost.y + 4f * scale,
                45f * scale, 50f * scale), HasAnvilDragForHud
                ? DraggedAnvilSpriteForHud : GetUnitSprite(draggedBenchUnit));
            GUI.Label(new Rect(ghost.x + 52f * scale, ghost.y + 10f * scale,
                60f * scale, 32f * scale), HasAnvilDragForHud ? "ANVIL" : draggedBenchUnit.MonsterName, cardSub);
        }

        private void DrawShopBackground(Rect panel)
        {
            float s = HudScale;
            DrawSolid(panel, new Color(.028f, .045f, .066f, .985f));
            DrawOutline(panel, new Color(.29f, .43f, .56f, .95f), 2f*s);
            DrawSolid(new Rect(panel.x + 4f*s, panel.y + 4f*s,
                panel.width - 8f*s, 3f*s), new Color(.28f, .44f, .58f, .86f));
            DrawSolid(new Rect(panel.x + 7f*s, panel.yMax - 8f*s,
                panel.width - 14f*s, 2f*s), new Color(.15f, .26f, .38f, .8f));
        }

        private void DrawEconomy(Rect panel)
        {
            float s = HudScale;
            DrawSolid(panel, new Color(.052f, .088f, .130f, .99f));
            DrawOutline(panel, new Color(.20f, .34f, .47f), s);
            GUI.Label(new Rect(panel.x + 11f*s, panel.y + 8f*s,
                panel.width - 22f*s, 21f*s), "YOUR TEAM", head);
            GUIStyle goldStyle = new GUIStyle(cardCost)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = Mathf.RoundToInt(24f*s)
            };
            goldStyle.normal.textColor = new Color(1f, .85f, .38f);
            GUI.Label(new Rect(panel.x + 11f*s, panel.y + 35f*s,
                panel.width - 22f*s, 37f*s), gold + " GOLD", goldStyle);
            GUI.Label(new Rect(panel.x + 11f*s, panel.y + 84f*s,
                panel.width - 22f*s, 20f*s), "LEVEL " + playerLevel + "/" + MaxPlayerLevel, small);
            GUI.Label(new Rect(panel.x + 11f*s, panel.y + 107f*s,
                panel.width - 22f*s, 20f*s), "BOARD  " + DeployedCount() + "/" + TeamLimit, small);
            GUI.Label(new Rect(panel.x + 11f*s, panel.y + 130f*s,
                panel.width - 22f*s, 20f*s),
                IsLevelCapped ? "XP  MAX LEVEL" : "XP  " + currentXp + "/" + XPToNextLevel, small);
        }

        private void DrawActions(Rect panel)
        {
            float s = HudScale;
            DrawSolid(panel, new Color(.052f, .088f, .13f, .99f));
            DrawOutline(panel, new Color(.20f, .34f, .47f), s);
            GUI.Label(new Rect(panel.x + 9f*s, panel.y + 7f*s,
                panel.width - 18f*s, 23f*s), "SHOP ACTIONS", head);
            bool prevEnabled = GUI.enabled;
            GUI.enabled = prevEnabled && CanManageShop && gold >= CurrentRerollCost;
            if (GUI.Button(new Rect(panel.x + 9f*s, panel.y + 40f*s,
                panel.width - 18f*s, 45f*s), FreeRerolls > 0
                    ? "REROLL  ·  FREE ×" + FreeRerolls
                    : "REROLL  ·  " + CurrentRerollCost + "g", actionStyle)) Reroll();
            GUI.enabled = prevEnabled && CanBuyXP;
            if (GUI.Button(new Rect(panel.x + 9f*s, panel.y + 93f*s,
                panel.width - 18f*s, 45f*s), IsLevelCapped ? "MAX LEVEL" : "BUY " + XPPerPurchase + " XP  ·  " + BuyXPCost + "g", actionStyle)) BuyXP();
            GUI.enabled = prevEnabled;
            GUI.Label(new Rect(panel.x + 9f*s, panel.y + 142f*s,
                panel.width - 18f*s, 14f*s), "Monster: sell | Anvil: open", statusStyle);
        }

        private void DrawAnvilOpenOverlay(Rect panel)
        {
            if (!HasAnvilDragForHud) return;
            float s = HudScale;
            DrawSolid(panel, new Color(.055f, .14f, .24f, .99f));
            DrawOutline(panel, new Color(.34f, .77f, 1f), 3f*s);
            DrawSprite(new Rect(panel.center.x - 290f*s, panel.y+19f*s, 118f*s, 118f*s),
                DraggedAnvilSpriteForHud);
            GUI.Label(new Rect(panel.center.x - 135f*s, panel.y+23f*s, 520f*s, 36f*s),
                "RELEASE TO OPEN", head);
            GUI.Label(new Rect(panel.center.x - 135f*s, panel.y+60f*s, 520f*s, 34f*s),
                DraggedAnvilNameForHud.ToUpperInvariant(), cardTitle);
            GUI.Label(new Rect(panel.center.x - 135f*s, panel.y+102f*s, 520f*s, 29f*s),
                "CHOOSE 1 OF 4 REWARDS  ·  NO SELL", cardSub);
        }

        private void DrawSellOverlay(Rect panel, MonsterUnit unit)
        {
            if (unit == null) return;
            float s = HudScale;
            DrawSolid(panel, new Color(.22f, .060f, .076f, .985f));
            DrawOutline(panel, new Color(.96f, .37f, .40f), 3f*s);
            DrawSolid(new Rect(panel.x + 5f*s, panel.y + 5f*s,
                panel.width - 10f*s, 5f*s), new Color(.90f, .28f, .33f));

            float middle = panel.center.x;
            // Keep the icon and ALL three text rows inside the same safe content area.
            float iconSize = 116f*s;
            float iconX = middle - 296f*s;
            DrawSolid(new Rect(iconX - 7f*s, panel.y + 28f*s,
                iconSize + 14f*s, iconSize + 7f*s),
                new Color(.12f, .040f, .052f, .96f));
            DrawSprite(new Rect(iconX, panel.y + 31f*s, iconSize, iconSize),
                GetUnitSprite(unit));

            float textX = middle - 138f*s;
            GUIStyle kicker = new GUIStyle(small)
            {
                fontSize = Mathf.RoundToInt(16f*s),
                alignment = TextAnchor.MiddleLeft
            };
            kicker.normal.textColor = new Color(.96f, .68f, .72f);
            GUI.Label(new Rect(textX, panel.y + 26f*s, 380f*s, 21f*s),
                "RELEASE TO SELL", kicker);

            GUIStyle name = new GUIStyle(head)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = Mathf.RoundToInt((unit.MonsterName.Length > 12 ? 29f : 34f)*s),
                wordWrap = false,
                clipping = TextClipping.Clip
            };
            name.normal.textColor = Color.white;
            GUI.Label(new Rect(textX, panel.y + 51f*s,
                Mathf.Min(590f*s, panel.xMax - textX - 15f*s), 44f*s),
                unit.MonsterName.ToUpperInvariant(), name);

            GUIStyle amount = new GUIStyle(cardCost)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = Mathf.RoundToInt(25f*s)
            };
            amount.normal.textColor = new Color(1f, .85f, .42f);
            GUI.Label(new Rect(textX, panel.y + 100f*s, 360f*s, 34f*s),
                "+" + SellRefund(unit) + " GOLD", amount);

            GUI.Label(new Rect(panel.x + 18f*s, panel.yMax - 25f*s,
                panel.width - 36f*s, 18f*s),
                "DROP TO CONFIRM  ·  MOVE BACK TO CANCEL", centered);
        }

        /// <summary>
        /// U6.8.6: Bench and board share the same alpha-visible sprite anchoring.
        /// The reserve Monster is represented by a SpriteRenderer proxy, so use
        /// that proxy to place a compact HP/item widget close to its actual art.
        /// </summary>
        private void DrawWorldBenchEquippedItems()
        {
            // U6.9.1b: suppress world name/health/item badges while the augment
            // cards are expanded; hiding cards restores board inspection.
            if (MonsterTacticsAugmentSelectionUI.SuppressUnitOverlays) return;
            if (!useWorldBench || !worldBenchReady || arenaCamera == null) return;
            Styles();
            int oldDepth = GUI.depth;
            GUI.depth = -165;
            MonsterTacticsItemKind hovered = default(MonsterTacticsItemKind);
            bool showTooltip = false;
            if (worldBenchItemNameStyle == null)
            {
                worldBenchItemNameStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 11,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    clipping = TextClipping.Clip
                };
                worldBenchItemNameStyle.normal.textColor = Color.white;
            }
            const float iconGap = 2f;
            const float iconSize = 21f;
            const float barHeight = 15f;
            const float selectedNameHeight = 16f;
            const float gapFromSprite = 2f;
            for (int i = 0; i < BenchCapacity; i++)
            {
                MonsterUnit unit = benchSlots[i];
                SpriteRenderer proxy = worldSpriteRenderers[i];
                if (unit == null || proxy == null || !proxy.enabled || proxy.sprite == null)
                    continue;

                Rect visible;
                if (!MonsterTacticsSpriteScreenBounds.TryGetVisibleRect(arenaCamera, proxy, out visible))
                    continue;
                float x = visible.center.x;
                float spriteTop = visible.y;
                bool showName = unit == selected;
                MonsterTacticsHeldItems held = unit.GetComponent<MonsterTacticsHeldItems>();
                int count = held != null ? Mathf.Min(3, held.Items.Count) : 0;
                float iconsWidth = count * iconSize + Mathf.Max(0, count - 1) * iconGap;
                float width = Mathf.Max(Mathf.Clamp(visible.width * 1.18f, 68f, 112f),
                    iconsWidth + 6f);
                float rowHeight = count > 0 ? 23f : 0f;
                float nameHeight = showName ? selectedNameHeight : 0f;
                float top = spriteTop - gapFromSprite - barHeight - rowHeight - nameHeight;
                if (x < -width || x > Screen.width + width || top > Screen.height ||
                    spriteTop < -10f) continue;
                x = Mathf.Clamp(x, width * .5f + 2f,
                    Mathf.Max(width * .5f + 2f, Screen.width - width * .5f - 2f));

                if (showName)
                {
                    Rect nameRect = new Rect(x - width * .5f, top, width, selectedNameHeight);
                    DrawSolid(nameRect, new Color(.13f, .25f, .33f, .94f));
                    GUI.Label(nameRect, unit.MonsterName, worldBenchItemNameStyle);
                }
                float barY = top + nameHeight;
                Rect backing = new Rect(x - width * .5f, barY, width, barHeight);
                DrawSolid(backing, new Color(.025f, .052f, .078f, .88f));
                float inset = 4f;
                DrawSolid(new Rect(backing.x + inset, barY + 3f,
                    width - inset * 2f, 8f), new Color(.11f, .17f, .20f));
                DrawSolid(new Rect(backing.x + inset, barY + 3f,
                    (width - inset * 2f) * Mathf.Clamp01(unit.HealthFraction), 8f),
                    new Color(.39f, .85f, .52f));
                if (count <= 0) continue;

                float left = x - iconsWidth * .5f;
                float itemY = barY + barHeight + 1f;
                for (int k = 0; k < count; k++)
                {
                    Rect badge = new Rect(left + k * (iconSize + iconGap),
                        itemY, iconSize, iconSize);
                    DrawSolid(new Rect(badge.x - 1f, badge.y - 1f,
                        badge.width + 2f, badge.height + 2f),
                        new Color(.035f, .065f, .10f, .88f));
                    MonsterTacticsItemArt.Draw(badge, held.Items[k]);
                    if (badge.Contains(Event.current.mousePosition))
                    {
                        hovered = held.Items[k];
                        showTooltip = true;
                    }
                }
            }
            if (showTooltip) MonsterTacticsItemTooltip.Draw(Event.current.mousePosition, hovered);
            GUI.depth = oldDepth;
        }

        private void OnGUI()
        {
            if (!initialized) return;
            DrawWorldBenchEquippedItems();
            if (canvasHud != null && canvasHud.IsReady) return; // U5.2 renders using uGUI only.
            Styles();
            SyncBenchSlots();
            GUI.depth = -90;
            float s = HudScale;

            // U4 owns the top-center phase label. Only show compact player stats here.
            Rect stats = new Rect(18f*s, 11f*s, 240f*s, 62f*s);
            DrawSolid(stats, UiPanel);
            DrawOutline(stats, new Color(.27f, .41f, .55f), s);
            GUI.Label(new Rect(stats.x + 9f*s, stats.y + 6f*s,
                stats.width - 18f*s, 24f*s), "MONSTERTACTICS", head);
            GUI.Label(new Rect(stats.x + 9f*s, stats.y + 29f*s,
                stats.width - 18f*s, 20f*s),
                gold + "g  ·  LV " + playerLevel + (IsLevelCapped ? " MAX" : " " + currentXp + "/" + XPToNextLevel + " XP") +
                "  ·  " + DeployedCount() + "/" + TeamLimit +
                "  ·  ROUND " + battleNumber, small);

            Rect panel = GetShopPanelRect();
            DrawShopBackground(panel);
            if (IsAnvilOpenPreviewActive)
                DrawAnvilOpenOverlay(panel);
            else if (ShowSellOverlay)
                DrawSellOverlay(panel, ActiveDraggedUnit);
            else
            {
                DrawEconomy(GetShopEconomyRect());
                for (int i = 0; i < ShopSlots; i++)
                    DrawShopCard(GetShopCardRect(i), shop[i]);
                DrawActions(GetShopActionRect());
                DrawDraggedGhost();
            }
        }
    }
}
