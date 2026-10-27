using System;
using System.Collections.Generic;
using UnityEngine;

namespace MonsterTactics
{
    /// <summary>
    /// U6.9.0 augment runtime. Add this to the existing shop manager object via the
    /// editor install menu. U6.9.1 opens offers at scheduled planning rounds and displays the choice UI.
    /// Matches use snapshots of ScriptableObjects; effects only use supported APIs.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MonsterTacticsAugmentManager : MonoBehaviour
    {
        [SerializeField] private MonsterTacticsAugmentCatalog catalog;
        [SerializeField] private MonsterTacticsU5ShopManager shop;
        [SerializeField] private MonsterTacticsU62RoundManager roundManager;

        private readonly List<MonsterTacticsAugmentSnapshot> library = new List<MonsterTacticsAugmentSnapshot>();
        private readonly List<MonsterTacticsAugmentSnapshot> selected = new List<MonsterTacticsAugmentSnapshot>();
        private readonly Dictionary<int, int> effectTriggerCounts = new Dictionary<int, int>();
        private readonly Dictionary<int, int> chosenAtRound = new Dictionary<int, int>();
        private readonly Dictionary<int, MonsterTacticsAugmentSlots> chosenSlotByIndex =
            new Dictionary<int, MonsterTacticsAugmentSlots>();
        private readonly Dictionary<int, string> lastTriggerRound = new Dictionary<int, string>();
        private readonly Dictionary<MonsterTacticsAugmentSlots, string> resolvedChoices =
            new Dictionary<MonsterTacticsAugmentSlots, string>();
        private readonly Dictionary<MonsterTacticsAugmentSlots, int> rerollCounts =
            new Dictionary<MonsterTacticsAugmentSlots, int>();
        private readonly HashSet<string> justOffered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly List<MonsterTacticsAugmentSnapshot> offered = new List<MonsterTacticsAugmentSnapshot>();
        private bool active, subscribed, processingEvent;
        // U6.9.1a: UI-only test runs never grant effects or consume match slots.
        private bool sandboxPreview;
        private int sandboxPreviewSerial, sandboxRerolls;
        // Log an unavailable pool once per slot; retry periodically during
        // planning in case buying/leveling enables a valid augment later.
        private readonly HashSet<MonsterTacticsAugmentSlots> attemptedSlots = new HashSet<MonsterTacticsAugmentSlots>();
        private readonly Dictionary<MonsterTacticsAugmentSlots, float> offerRetryAt =
            new Dictionary<MonsterTacticsAugmentSlots, float>();
        private int rngSeed, roundResolvedCounter, rerollsPerCard;
        private readonly MonsterTacticsAugmentSelectionClock choiceClock = new MonsterTacticsAugmentSelectionClock();
        // U6.9.2c per-match state only. No ScriptableObject mutations in Play Mode.
        private int pendingAmuletRerolls;
        // U6.9.2d: per-match effect state. Never stored on catalog assets.
        private string lastCatchReleasePlanning = "";
        private bool daycareShiftElidunebit;
        private int pendingTwoCostGrants;
        // U6.9.2e: all counters are match-local (never mutate asset definitions).
        private int pendingNuzlockeGold, pendingNuzlockeRerolls;
        private readonly Queue<MonsterTacticsGameplayEvent> deferredGameplayEvents =
            new Queue<MonsterTacticsGameplayEvent>();
        private bool drainingGameplayEvents;
        private int pendingFiveCostOneStars, pendingFiveCostTwoStars;
        private int daycareBreedPvpProgress;
        private bool catchEmAllAwarded;
        // U6.9.7 match-local progression. Never mutates ScriptableObject assets.
        private readonly Dictionary<int, int> rareCandyLevelsLeft = new Dictionary<int, int>();
        private readonly Dictionary<int, int> eliteFourPvpWins = new Dictionary<int, int>();
        private int pendingEliteFourGearDrops;
        private System.Random rewardRandom;
        private int daycareSlot = 4; // Center of the nine existing reserve hexes.
        private int daycarePvpProgress;
        private string daycareTrackedFamily = "";
        private readonly Queue<string> pendingDaycareCopies = new Queue<string>();
        public int DaycareSlot => daycareSlot;
        public int DaycareProgress => daycarePvpProgress;
        public int DaycarePendingCopies => pendingDaycareCopies.Count;
        public bool HasDaycare => active && SelectedHasEffect(MonsterTacticsAugmentEffectKind.DaycareCopy);
        public int DaycareRoundTarget
        {
            get
            {
                foreach (var def in selected)
                    foreach (var effect in def.Effects)
                        if (effect != null && effect.kind == MonsterTacticsAugmentEffectKind.DaycareCopy)
                            return Mathf.Max(1, effect.roundInterval);
                return 3;
            }
        }
        public int PendingAmuletRerolls => pendingAmuletRerolls;

        private float firstSelectionSeconds, laterSelectionSeconds;
        private bool useMatchTierPatterns, limitSameCategoryToTwo, enableTraitFamilyTailoring;
        private MonsterTacticsAugmentTier[] matchTiers = new[]
            { MonsterTacticsAugmentTier.Rookie, MonsterTacticsAugmentTier.Ace, MonsterTacticsAugmentTier.Champion };
        private readonly HashSet<string> seenThisSelection = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public event Action<MonsterTacticsAugmentSlots, string, bool> SelectionResolved;
        private MonsterTacticsAugmentSlots currentSlot;
        private MonsterTacticsAugmentTier currentTier;
        private string lastNotice = "Framework installed. No automatic selections until U6.9.1.";

        public bool IsActive => active;
        public bool IsSandboxPreview => sandboxPreview && IsSelectionOpen;
        public bool IsSelectionOpen => active && currentSlot != MonsterTacticsAugmentSlots.None && offered.Count > 0;
        public bool HasResolved(MonsterTacticsAugmentSlots slot) => resolvedChoices.ContainsKey(slot);
        public string LastNotice => lastNotice;
        public IReadOnlyList<MonsterTacticsAugmentSnapshot> Selected => selected;
        public IReadOnlyList<MonsterTacticsAugmentSnapshot> Offered => offered;
        public int RerollsPerCard => rerollsPerCard;
        public int RemainingOfferRerolls => choiceClock.TotalRerollsRemaining;
        public int CardRerollsRemaining(int index) => choiceClock.RemainingForCard(index);
        public float SelectionSecondsRemaining => choiceClock.SecondsRemaining;
        public float SelectionSecondsTotal => choiceClock.TotalSeconds;
        public float SelectionProgress01 => choiceClock.Progress01;
        public bool SelectionTimerRunning => choiceClock.IsOpen;
        // Live round deadlines must never pause waiting for an augment pick.
        public bool AutoPickOnTimeout => true;
        public int MatchSeed => rngSeed;
        public bool IsMixedTierOffer
        {
            get
            {
                if (offered.Count < 2) return false;
                for (int i = 1; i < offered.Count; i++)
                    if (offered[i].Tier != offered[0].Tier) return true;
                return false;
            }
        }
        public MonsterTacticsAugmentSlots CurrentSlot => currentSlot;
        public MonsterTacticsAugmentTier CurrentTier => currentTier;
        public MonsterTacticsAugmentCatalog Catalog => catalog;
        public int DefinitionCount => library.Count;

        private void Awake()
        {
            if (catalog == null) catalog = Resources.Load<MonsterTacticsAugmentCatalog>("Augments/MonsterTacticsAugmentCatalog");
            if (shop == null) shop = GetComponent<MonsterTacticsU5ShopManager>();
            if (shop == null) shop = FindAnyObjectByType<MonsterTacticsU5ShopManager>();
            if (roundManager == null) roundManager = FindAnyObjectByType<MonsterTacticsU62RoundManager>();
            StartMatch();
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void Start()
        {
            Subscribe(); // Works if the shop is initialized in a later component Awake.
            // Existing scenes do not need to be reauthored after importing U6.9.1.
            if (GetComponent<MonsterTacticsAugmentSelectionUI>() == null)
                gameObject.AddComponent<MonsterTacticsAugmentSelectionUI>();
            if (GetComponent<MonsterTacticsDaycareBenchUI>() == null)
                gameObject.AddComponent<MonsterTacticsDaycareBenchUI>();
        }

        private void OnDisable()
        {
            Unsubscribe();
            if (shop != null)
                for (int i = 0; i < selected.Count; i++)
                    for (int j = 0; j < selected[i].Effects.Length; j++)
                        shop.Rules.RemoveSource("augment:" + i + ":" + selected[i].Id + ":" + j);
            choiceClock.Clear(); selectionTimerEnabled = false;
            sandboxPreview = false;
            active = false;
        }

        private void Subscribe()
        {
            if (subscribed || shop == null) return;
            shop.GameplayEvents.Raised += OnGameEvent;
            subscribed = true;
        }
        private void Unsubscribe()
        {
            if (subscribed && shop != null) shop.GameplayEvents.Raised -= OnGameEvent;
            subscribed = false;
        }

        private void StartMatch()
        {
            active = catalog != null && catalog.masterEnabled && shop != null;
            // The catalog seed remains a reproducible base when fresh randomness is off.
            // A fresh seed is local-match only; future multiplayer should instead
            // receive a single authoritative seed from the host.
            int configuredSeed = catalog != null ? catalog.seed : 42;
            rngSeed = catalog != null && catalog.useFreshMatchSeed ?
                unchecked(configuredSeed ^ Guid.NewGuid().GetHashCode()) : configuredSeed;
            rerollsPerCard = catalog != null ? Mathf.Max(0, catalog.rerollsPerCard) : 1;
            firstSelectionSeconds = catalog != null ? Mathf.Max(1f, catalog.firstSelectionSeconds) : 43f;
            laterSelectionSeconds = catalog != null ? Mathf.Max(1f, catalog.laterSelectionSeconds) : 58f;
            // Legacy toggle is retained in the catalog for backward compatibility,
            // but live rounds always auto-select to prevent an infinite pause.
            if (catalog != null && !catalog.autoPickWhenTimeExpires)
                Debug.LogWarning("[U6.9.1e] Auto Pick On Timeout was disabled in the catalog. " +
                    "Live augment rounds now auto-pick to guarantee combat starts.", this);
            useMatchTierPatterns = catalog != null && catalog.useMatchTierPatterns;
            limitSameCategoryToTwo = catalog != null && catalog.limitSameCategoryToTwo;
            enableTraitFamilyTailoring = catalog != null && catalog.enableTraitFamilyTailoring;
            // Snapshot the shared, weighted tier sequence ONCE per new match.
            matchTiers = useMatchTierPatterns ?
                MonsterTacticsAugmentTierPatterns.Roll(catalog.tierPatterns, rngSeed) :
                new[] { MonsterTacticsAugmentTier.Rookie, MonsterTacticsAugmentTier.Ace, MonsterTacticsAugmentTier.Champion };
            roundResolvedCounter = 0;
            library.Clear(); selected.Clear(); offered.Clear(); justOffered.Clear();
            resolvedChoices.Clear(); rerollCounts.Clear(); effectTriggerCounts.Clear(); attemptedSlots.Clear(); offerRetryAt.Clear();
            chosenAtRound.Clear(); chosenSlotByIndex.Clear(); lastTriggerRound.Clear();
            currentSlot = MonsterTacticsAugmentSlots.None;
            choiceClock.Clear(); seenThisSelection.Clear(); selectionTimerEnabled = false;
            sandboxPreview = false; sandboxPreviewSerial = 0; sandboxRerolls = 0;
            daycareSlot = 4; daycarePvpProgress = 0; daycareTrackedFamily = "";
            pendingDaycareCopies.Clear(); pendingAmuletRerolls = 0;
            lastCatchReleasePlanning = "";
            daycareShiftElidunebit = false;
            pendingTwoCostGrants = 0;
            pendingNuzlockeGold = pendingNuzlockeRerolls = 0;
            deferredGameplayEvents.Clear(); drainingGameplayEvents = false;
            pendingFiveCostOneStars = pendingFiveCostTwoStars = 0;
            daycareBreedPvpProgress = 0;
            catchEmAllAwarded = false;
            rareCandyLevelsLeft.Clear(); eliteFourPvpWins.Clear();
            pendingEliteFourGearDrops = 0;
            rewardRandom = new System.Random(unchecked(rngSeed ^ 0x54C4D));
            if (catalog == null) { lastNotice = "No augment catalog. Use MonsterTactics > Augments > Install Framework."; return; }
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var d in catalog.definitions)
            {
                if (d == null || string.IsNullOrWhiteSpace(d.id) || !seen.Add(d.id.Trim())) continue;
                library.Add(new MonsterTacticsAugmentSnapshot(d));
            }
            lastNotice = active ? "Augments active: selections at 2-1, 3-2 and 4-2." :
                "Augment master switch OFF; existing gameplay unchanged.";
        }

        /// <summary>
        /// U6.9.2a.2: Recover a scene manager that awoke with missing/stale catalog
        /// or Shop Manager references. Intended only at match initialization.
        /// Never resets a selection or a match that has already progressed.
        /// </summary>
        public bool TryRecoverStartup(MonsterTacticsAugmentCatalog expectedCatalog,
            MonsterTacticsU5ShopManager currentShop, MonsterTacticsU62RoundManager currentRound)
        {
            if (active) return true;
            if (expectedCatalog == null || !expectedCatalog.masterEnabled || currentShop == null)
                return false;
            if (selected.Count != 0 || offered.Count != 0 || resolvedChoices.Count != 0)
                return false;

            // Unhook previous shop before changing the reference.
            Unsubscribe();
            catalog = expectedCatalog;
            shop = currentShop;
            roundManager = currentRound;
            StartMatch();
            if (isActiveAndEnabled) Subscribe();
            if (isActiveAndEnabled && GetComponent<MonsterTacticsAugmentSelectionUI>() == null)
                gameObject.AddComponent<MonsterTacticsAugmentSelectionUI>();
            return active;
        }

        public static MonsterTacticsAugmentSlots SlotForStage(string stageRound)
        {
            if (stageRound == "2-1") return MonsterTacticsAugmentSlots.First;
            if (stageRound == "3-2") return MonsterTacticsAugmentSlots.Second;
            if (stageRound == "4-2") return MonsterTacticsAugmentSlots.Final;
            return MonsterTacticsAugmentSlots.None;
        }

        public MonsterTacticsAugmentOfferContext BuildContext()
        {
            var ctx = new MonsterTacticsAugmentOfferContext();
            if (shop == null) return ctx;
            ctx.PlayerLevel = shop.PlayerLevel;
            ctx.Gold = shop.Gold;
            ctx.Selected.AddRange(selected);
            // Scope only to the local player's placement hierarchy, not PvE enemies.
            var placement = FindAnyObjectByType<MonsterPlacementController>();
            if (placement != null)
            {
                var all = placement.GetComponentsInChildren<MonsterUnit>(true);
                foreach (var unit in all)
                {
                    if (unit == null) continue;
                    ctx.OwnedFamilies.Add(MonsterTacticsU5Roster.BaseName(unit.MonsterName));
                }
                var activeUnits = new List<MonsterUnit>();
                foreach (var unit in all)
                    if (unit != null && shop.IsDeployed(unit) && unit.gameObject.activeInHierarchy)
                        activeUnits.Add(unit);
                foreach (var pair in MonsterTacticsTraits.CountDeployed(activeUnits)) ctx.ActiveTraits[pair.Key] = pair.Value;
            }
            return ctx;
        }

        /// <summary>Match-wide scheduled tier. A chosen tier-shifting augment can
        /// affect the NEXT selection only; card tiers remain uniform per offering.</summary>
        public MonsterTacticsAugmentTier TierForSlot(MonsterTacticsAugmentSlots slot)
        {
            int index = slot == MonsterTacticsAugmentSlots.First ? 0 :
                slot == MonsterTacticsAugmentSlots.Second ? 1 :
                slot == MonsterTacticsAugmentSlots.Final ? 2 : -1;
            if (index < 0) return MonsterTacticsAugmentTier.Ace;
            int baseTier = (int)matchTiers[index];
            if (index > 0)
            {
                // Only the previous selection can shift this tier.
                MonsterTacticsAugmentSlots previous = index == 1 ?
                    MonsterTacticsAugmentSlots.First : MonsterTacticsAugmentSlots.Second;
                for (int i = 0; i < selected.Count; i++)
                    if (chosenSlotByIndex.TryGetValue(i, out var selectedSlot) && selectedSlot == previous)
                        baseTier += selected[i].NextSelectionTierAdjustment;
            }
            return (MonsterTacticsAugmentTier)Mathf.Clamp(baseTier, 0, 2);
        }

        /// <summary>Entry point for the U6.9.1 round scheduler. Can be called each
        /// planning frame; a slot opens at most once. On insufficient elidunebit offers
        /// the round continues normally, with a single warning instead of freezing.</summary>
        public bool TryBeginForStage(string stageRound)
        {
            if (!active) return false;
            if (IsSelectionOpen) return true;
            var slot = SlotForStage(stageRound);
            if (slot == MonsterTacticsAugmentSlots.None || resolvedChoices.ContainsKey(slot) ||
                shop == null || !shop.IsShopReady) return false;
            if (offerRetryAt.TryGetValue(slot, out float retryAt) && Time.unscaledTime < retryAt)
                return false;
            bool opened = OpenScheduledOffer(slot);
            if (!opened)
            {
                offerRetryAt[slot] = Time.unscaledTime + 2f;
                if (attemptedSlots.Add(slot))
                    Debug.LogWarning("[U6.9.1e] No elidunebit augment yet for " + stageRound +
                        ": " + lastNotice + " Retrying during planning; combat will not be blocked.", this);
            }
            else offerRetryAt.Remove(slot);
            return opened;
        }

        public bool OpenScheduledOffer(MonsterTacticsAugmentSlots slot) => OpenOffer(slot, TierForSlot(slot));

        // Preserve the original preview=true API for TESTS THAT GRANT REAL REWARDS.
        // It bypasses the stage requirement but consumes one normal augment slot.
        public bool OpenOffer(MonsterTacticsAugmentSlots slot, MonsterTacticsAugmentTier tier,
            bool reroll = false, bool preview = false)
        {
            return OpenOfferInternal(slot, tier, reroll, preview, false);
        }

        /// <summary>Repeatable UI test. Does not grant effects, write selected
        /// augments, resolve a slot, or change the real match's tier pattern.</summary>
        public bool OpenSandboxPreview(MonsterTacticsAugmentSlots slot, MonsterTacticsAugmentTier tier)
        {
            return OpenOfferInternal(slot, tier, false, true, true);
        }

        private bool OpenOfferInternal(MonsterTacticsAugmentSlots slot, MonsterTacticsAugmentTier tier,
            bool reroll, bool ignoreStage, bool sandbox)
        {
            if (reroll) return slot == currentSlot && tier == currentTier && TryRerollCard(0);
            if (!active || shop == null || !shop.IsShopReady || slot == MonsterTacticsAugmentSlots.None)
            { lastNotice = "Augment manager or shop is not ready."; return false; }
            if (!sandbox && resolvedChoices.ContainsKey(slot))
            { lastNotice = "Already chosen an augment for " + slot + "."; return false; }
            if (roundManager == null) roundManager = FindAnyObjectByType<MonsterTacticsU62RoundManager>();
            if (!ignoreStage && (roundManager == null || SlotForStage(roundManager.StageRound) != slot))
            { lastNotice = "Cannot open the selection outside its configured round."; return false; }
            if (offered.Count > 0) { lastNotice = "Choose or reroll the current offer first."; return false; }

            // In a sandbox, existing match picks must not reduce the available
            // card pool. Other eligibility checks still use the current team.
            var context = BuildContext();
            if (sandbox) context.Selected.Clear();
            int previewSeed = sandbox ? unchecked((sandboxPreviewSerial + 1) * 16777619) : 0;
            var rng = new System.Random(unchecked(rngSeed ^ ((int)slot * 1103515245) ^
                ((int)tier * 7919) ^ 16777619 ^ previewSeed));
            var candidates = MonsterTacticsAugmentOffers.Roll(library, slot, tier, context, 3, rng,
                null, limitSameCategoryToTwo ? 2 : 0, null, enableTraitFamilyTailoring);
            // Prefer 3 cards of the requested tier, then 3 cards of one other
            // elidunebit tier; finally make up to 3 distinct cards across tiers.
            // Never invent rewards, enable disabled augments or bypass eligibility.
            if (candidates.Count < 3)
            {
                candidates = MonsterTacticsAugmentOffers.RollWithFallback(
                    library, slot, tier, context, 3, rng,
                    limitSameCategoryToTwo ? 2 : 0, enableTraitFamilyTailoring);
                if (candidates.Count == 0)
                {
                    lastNotice = "No enabled, supported and elidunebit augments for " + slot +
                        ". Verify the catalog. Cannot generate a safe reward.";
                    return false;
                }
                Debug.LogWarning("[U6.9.1e] Only " + candidates.Count +
                    " elidunebit card(s) were available for " + tier + " at " + slot +
                    ". Using safe fallback; check tier availability in Balance Editor.", this);
            }
            MonsterTacticsAugmentTier actualTier = candidates[0].Tier;
            currentSlot = slot; currentTier = actualTier; sandboxPreview = sandbox;
            if (sandbox) { sandboxPreviewSerial++; sandboxRerolls = 0; }
            else rerollCounts[slot] = 0;
            offered.Clear(); offered.AddRange(candidates);
            seenThisSelection.Clear(); justOffered.Clear();
            foreach (var d in candidates) { seenThisSelection.Add(d.Id); justOffered.Add(d.Id); }
            // U6.9.1b: The clock starts with the offer, not the UI repaint.
            // Hiding the cards can never pause or restart the deadline.
            choiceClock.Begin(offered.Count, rerollsPerCard, slot == MonsterTacticsAugmentSlots.First ?
                firstSelectionSeconds : laterSelectionSeconds);
            selectionTimerEnabled = true;
            lastNotice = sandbox ?
                "UI-only preview: rerolls and picks are not saved or awarded." :
                "Opened " + tier + " offering for " + slot + " (timer started)";
            return true;
        }

        /// <summary>Close a sandbox preview without making any match changes.</summary>
        public bool CloseSandboxPreview()
        {
            if (!sandboxPreview || !IsSelectionOpen) return false;
            CloseSandboxOffer("Closed augment UI preview. No effects granted.");
            return true;
        }

        private void CloseSandboxOffer(string notice)
        {
            offered.Clear(); justOffered.Clear(); seenThisSelection.Clear();
            choiceClock.Clear(); selectionTimerEnabled = false;
            currentSlot = MonsterTacticsAugmentSlots.None; sandboxPreview = false;
            lastNotice = notice;
            // Do NOT invoke SelectionResolved: this wasn't a real selection.
        }

        private bool selectionTimerEnabled;

        /// <summary>
        /// Call from U6.9.1 only when all 3 cards are visible and the planning
        /// countdown has been paused. Prevents unseen auto-picks in the framework patch.
        /// </summary>
        public void BeginSelectionTimer()
        {
            selectionTimerEnabled = active && offered.Count > 0 && choiceClock.IsOpen;
        }

        /// <summary>Only the round scheduler calls this for actual scheduled
        /// selections; sandbox previews use their independent configured clock.</summary>
        public void SetScheduledSelectionDeadline(float seconds)
        {
            if (!IsSelectionOpen || sandboxPreview) return;
            choiceClock.SetSecondsRemaining(Mathf.Max(1f, seconds));
            selectionTimerEnabled = true;
        }

        private void Update()
        {
            UpdateCatchEmAll();
            if (!selectionTimerEnabled || !active) return;
            if (!choiceClock.Advance(Time.unscaledDeltaTime)) return;
            // Mandatory for live matches and for repeatable sandbox previews.
            // No extra wait is added to the shared planning countdown.
            AutoChooseVisible();
        }

        /// <summary>Reroll only one card; other two never move. No replacement = no charge.</summary>
        public bool TryRerollCard(int cardIndex)
        {
            if (!active || offered.Count == 0 || cardIndex < 0 || cardIndex >= offered.Count ||
                !choiceClock.CanReroll(cardIndex) ||
                (!sandboxPreview && resolvedChoices.ContainsKey(currentSlot))) return false;
            // No repeated IDs within this selection, including previously rerolled cards.
            var excluded = new HashSet<string>(seenThisSelection, StringComparer.OrdinalIgnoreCase);
            foreach (var visible in offered) excluded.Add(visible.Id);
            int attempts = sandboxPreview ? sandboxRerolls :
                (rerollCounts.TryGetValue(currentSlot, out int prior) ? prior : 0);
            var rng = new System.Random(unchecked(rngSeed ^ ((int)currentSlot * 1103515245) ^
                ((int)currentTier * 7919) ^ ((cardIndex + 1) * 486187739) ^
                ((attempts + 1) * 16777619) ^
                (sandboxPreview ? (sandboxPreviewSerial * 65899) : 0)));
            var others = new List<MonsterTacticsAugmentSnapshot>();
            for (int i = 0; i < offered.Count; i++) if (i != cardIndex) others.Add(offered[i]);
            var context = BuildContext();
            if (sandboxPreview) context.Selected.Clear();
            var candidates = MonsterTacticsAugmentOffers.Roll(library, currentSlot,
                offered[cardIndex].Tier, context, 1, rng, excluded,
                limitSameCategoryToTwo ? 2 : 0, others, enableTraitFamilyTailoring);
            // If this card's tier has no replacement, allow an elidunebit replacement
            // from another tier, and display its true tier on the card.
            if (candidates.Count == 0)
                candidates = MonsterTacticsAugmentOffers.RollWithFallback(library, currentSlot,
                    offered[cardIndex].Tier, context, 1, rng,
                    limitSameCategoryToTwo ? 2 : 0, enableTraitFamilyTailoring, excluded, others);
            if (candidates.Count == 0)
            { lastNotice = "No new elidunebit augment for this card; reroll not consumed."; return false; }
            if (!choiceClock.SpendReroll(cardIndex)) return false;
            offered[cardIndex] = candidates[0];
            seenThisSelection.Add(candidates[0].Id);
            if (sandboxPreview) sandboxRerolls = attempts + 1;
            else rerollCounts[currentSlot] = attempts + 1;
            lastNotice = "Rerolled card " + (cardIndex + 1) + " to " + candidates[0].Name;
            return true;
        }

        /// <summary>On timeout choose one of the three CURRENTLY DISPLAYED options.</summary>
        public bool AutoChooseVisible()
        {
            if (!active || offered.Count == 0 || currentSlot == MonsterTacticsAugmentSlots.None ||
                (!sandboxPreview && resolvedChoices.ContainsKey(currentSlot))) return false;
            int attempts = sandboxPreview ? sandboxRerolls :
                (rerollCounts.TryGetValue(currentSlot, out int used) ? used : 0);
            int seed = unchecked(rngSeed ^ ((int)currentSlot * 334214467) ^
                ((int)currentTier * 953723) ^ (attempts * 198491317) ^
                (sandboxPreview ? (sandboxPreviewSerial * 65899) : 0));
            int index = MonsterTacticsAugmentSelectionClock.ChooseVisibleIndex(offered.Count, seed);
            if (index < 0) return false;
            return ChooseVisible(index, true);
        }

        public bool ChooseVisible(int cardIndex, bool automatic = false)
        {
            if (cardIndex < 0 || cardIndex >= offered.Count) return false;
            string id = offered[cardIndex].Id;
            return TryChooseInternal(id, automatic);
        }

        // Selection only succeeds for something already on the active offer.
        public bool TryChoose(string id) => TryChooseInternal(id, false);

        private bool TryChooseInternal(string id, bool automatic)
        {
            if (!active || currentSlot == MonsterTacticsAugmentSlots.None ||
                (!sandboxPreview && resolvedChoices.ContainsKey(currentSlot))) return false;
            MonsterTacticsAugmentSnapshot picked = null;
            foreach (var d in offered)
                if (string.Equals(d.Id, id, StringComparison.OrdinalIgnoreCase)) { picked = d; break; }
            // Eligibility was already checked when the offering was generated.
            // Do NOT re-check current Gold/level on click: purchases during a selection
            // must not invalidate a card that the player was already shown.
            if (picked == null)
                return false;
            if (sandboxPreview)
            {
                CloseSandboxOffer((automatic ? "Preview auto-picked " : "Previewed ") +
                    picked.Name + " (" + currentSlot + "). No rewards or slot consumed.");
                return true;
            }
            int selectionIndex = selected.Count;
            selected.Add(picked);
            chosenAtRound[selectionIndex] = roundResolvedCounter;
            chosenSlotByIndex[selectionIndex] = currentSlot;
            resolvedChoices[currentSlot] = picked.Id;
            var variant = picked.Variant(currentSlot);
            for (int j = 0; j < picked.Effects.Length; j++)
            {
                var effect = picked.Effects[j];
                if (effect == null || effect.trigger != MonsterTacticsAugmentTrigger.OnChosen) continue;
                ApplyEffect(selectionIndex, j, picked, effect, variant, true);
            }
            var completedSlot = currentSlot;
            lastNotice = (automatic ? "Auto-picked " : "Chosen ") + picked.Name + " (" + completedSlot + ")";
            offered.Clear(); justOffered.Clear(); seenThisSelection.Clear();
            choiceClock.Finish();
            selectionTimerEnabled = false;
            currentSlot = MonsterTacticsAugmentSlots.None;
            SelectionResolved?.Invoke(completedSlot, picked.Id, automatic);
            return true;
        }

        private void OnGameEvent(MonsterTacticsGameplayEvent evt)
        {
            if (!active) return;
            if (processingEvent)
            {
                // Granting Monster/XP inside another augment may itself evolve
                // a unit, buy a unit, or level up. Replay only those events after
                // the current handler, instead of losing earned rewards.
                if (evt.Kind == MonsterTacticsGameplayEventKind.PlayerLeveledUp ||
                    evt.Kind == MonsterTacticsGameplayEventKind.UnitEvolved ||
                    evt.Kind == MonsterTacticsGameplayEventKind.UnitPurchased ||
                    evt.Kind == MonsterTacticsGameplayEventKind.PlayerDamaged)
                    if (deferredGameplayEvents.Count < 128) deferredGameplayEvents.Enqueue(evt);
                return;
            }
            if (evt.Kind == MonsterTacticsGameplayEventKind.RoundResolved) roundResolvedCounter++;
            if (selected.Count == 0) return;
            processingEvent = true;
            try
            {
                // Amulet Coin credit becomes usable in NEXT planning/shop, never in Results.
                if (evt.Kind == MonsterTacticsGameplayEventKind.PlanningStarted &&
                    pendingAmuletRerolls > 0 && shop != null && shop.IsShopReady)
                {
                    int credit = pendingAmuletRerolls;
                    pendingAmuletRerolls = 0;
                    shop.GrantFreeRerolls(credit, "amulet-coin-next-shop");
                }
                // Defer full-bench Daycare rewards until space exists.
                if (evt.Kind == MonsterTacticsGameplayEventKind.PlanningStarted)
                {
                    TryFulfillDaycareCopies();
                    TryFulfillTwoCostGrants();
                    TryFulfillLegendaryRewards();
                    TryDropEliteFourGear();
                    if (pendingNuzlockeGold > 0)
                    {
                        int gold = pendingNuzlockeGold;
                        pendingNuzlockeGold = 0;
                        shop.GrantGold(gold, "nuzlocke-following-turn");
                    }
                    if (pendingNuzlockeRerolls > 0)
                    {
                        int rolls = pendingNuzlockeRerolls;
                        pendingNuzlockeRerolls = 0;
                        shop.GrantFreeRerolls(rolls, "nuzlocke-following-turn");
                    }
                }
                // Lock the bench state at the START of combat, not at round end.
                if (evt.Kind == MonsterTacticsGameplayEventKind.CombatStarted)
                    daycareShiftElidunebit = shop != null && shop.EmptyBenchSlots >= 2;
                // Only bona fide successful 2-star sales qualify, once per planning round.
                if (evt.Kind == MonsterTacticsGameplayEventKind.UnitSold &&
                    evt.Source == "shop-2star" && shop != null && shop.IsPlanning &&
                    lastCatchReleasePlanning != evt.StageRound)
                {
                    foreach (var d in selected)
                        foreach (var effect in d.Effects)
                            if (effect != null && effect.kind == MonsterTacticsAugmentEffectKind.CatchRelease &&
                                lastCatchReleasePlanning != evt.StageRound)
                            {
                                lastCatchReleasePlanning = evt.StageRound;
                                shop.GrantGold(Mathf.Max(0, effect.value), "catch-release");
                                shop.GrantFreeRerolls(Mathf.Max(0, effect.secondaryValue), "catch-release");
                                break;
                            }
                }
                for (int i = 0; i < selected.Count; i++)
                {
                    var d = selected[i];
                    MonsterTacticsAugmentSlots chosenSlot;
                    if (!chosenSlotByIndex.TryGetValue(i, out chosenSlot))
                        chosenSlot = MonsterTacticsAugmentSlots.None;
                    var variant = d.Variant(chosenSlot);
                    for (int j = 0; j < d.Effects.Length; j++)
                    {
                        var effect = d.Effects[j];
                        if (effect == null) continue;
                        // Per-event effects are kept outside ApplyEffect so they
                        // can inspect round kind, evolution star, or transaction cost.
                        if (effect.kind == MonsterTacticsAugmentEffectKind.EvolvingTeam &&
                            evt.Kind == MonsterTacticsGameplayEventKind.UnitEvolved)
                        {
                            int award = evt.Amount == 2 ? effect.value :
                                evt.Amount == 3 ? effect.secondaryValue : 0;
                            if (award > 0) shop.GrantGold(award, "evolving-team");
                            continue;
                        }
                        if (effect.kind == MonsterTacticsAugmentEffectKind.SafariZoneExplorer &&
                            evt.Kind == MonsterTacticsGameplayEventKind.PlanningStarted)
                        {
                            if (shop.IsPveRound)
                            {
                                shop.GrantGold(effect.value, "safari-zone");
                                shop.GrantFreeRerolls(effect.secondaryValue, "safari-zone");
                            }
                            continue;
                        }
                        if (effect.kind == MonsterTacticsAugmentEffectKind.NuzlockeDiscipline &&
                            evt.Kind == MonsterTacticsGameplayEventKind.PlayerDamaged)
                        {
                            if (evt.Amount > 0)
                            {
                                pendingNuzlockeGold += effect.value;
                                pendingNuzlockeRerolls += effect.secondaryValue;
                            }
                            continue;
                        }
                        if (effect.kind == MonsterTacticsAugmentEffectKind.TrainersAllowance &&
                            evt.Kind == MonsterTacticsGameplayEventKind.PlayerLeveledUp)
                        {
                            shop.GrantFreeRerolls(effect.value, "trainer-allowance");
                            continue;
                        }
                        if (effect.kind == MonsterTacticsAugmentEffectKind.DaycareBreedMaster &&
                            evt.Kind == MonsterTacticsGameplayEventKind.RoundResolved)
                        {
                            if (!shop.IsPveRound)
                            {
                                daycareBreedPvpProgress++;
                                if (daycareBreedPvpProgress >= Mathf.Max(1, effect.roundInterval))
                                {
                                    daycareBreedPvpProgress = 0;
                                    shop.GrantGold(effect.value, "daycare-breed-master");
                                    pendingTwoCostGrants += Mathf.Max(1, effect.secondaryValue);
                                    TryFulfillTwoCostGrants();
                                }
                            }
                            continue;
                        }
                        if (effect.kind == MonsterTacticsAugmentEffectKind.ChampionsStreak &&
                            evt.Kind == MonsterTacticsGameplayEventKind.RoundResolved)
                        {
                            if (evt.Amount > 0) shop.GrantXP(effect.value);
                            continue;
                        }
                        if (effect.kind == MonsterTacticsAugmentEffectKind.MonsterMartVIP &&
                            evt.Kind == MonsterTacticsGameplayEventKind.UnitPurchased)
                        {
                            var purchased = MonsterTacticsU5Roster.Find(evt.UnitName);
                            if (purchased != null && (purchased.Cost == 4 || purchased.Cost == 5))
                                shop.GrantFreeRerolls(Mathf.Max(1, evt.Amount) * effect.secondaryValue,
                                    "poke-mart-vip");
                            continue;
                        }
                        if (effect.kind == MonsterTacticsAugmentEffectKind.RareCandyCache &&
                            evt.Kind == MonsterTacticsGameplayEventKind.PlayerLeveledUp)
                        {
                            int key = i * 1024 + j;
                            if (rareCandyLevelsLeft.TryGetValue(key, out int left) && left > 0)
                            {
                                rareCandyLevelsLeft[key] = left - 1;
                                shop.GrantGold(Mathf.Max(0, effect.secondaryValue), "rare-candy-cache");
                            }
                            continue;
                        }
                        if (effect.kind == MonsterTacticsAugmentEffectKind.EliteFourSponsorship &&
                            evt.Kind == MonsterTacticsGameplayEventKind.RoundResolved)
                        {
                            // Count victories only against trainers, not wild PvE.
                            if (!shop.IsPveRound && evt.Amount > 0)
                            {
                                int key = i * 1024 + j;
                                eliteFourPvpWins.TryGetValue(key, out int wins);
                                wins++;
                                if (wins >= Mathf.Max(1, effect.roundInterval))
                                {
                                    wins = 0;
                                    pendingEliteFourGearDrops += Mathf.Max(1, effect.value);
                                    TryDropEliteFourGear();
                                }
                                eliteFourPvpWins[key] = wins;
                            }
                            continue;
                        }
                        if (effect.kind == MonsterTacticsAugmentEffectKind.DaycareEvolutionMastery &&
                            evt.Kind == MonsterTacticsGameplayEventKind.UnitEvolved)
                        {
                            if (evt.Amount == 3)
                            {
                                shop.GrantGold(effect.value, "daycare-evolution-mastery");
                                pendingFiveCostOneStars += Mathf.Max(1, effect.secondaryValue);
                                TryFulfillLegendaryRewards();
                            }
                            continue;
                        }
                        if (effect.kind == MonsterTacticsAugmentEffectKind.AmuletCoin &&
                            evt.Kind == MonsterTacticsGameplayEventKind.RoundResolved &&
                            !shop.IsPveRound)
                        {
                            if (evt.Amount > 0) shop.GrantGold(Mathf.Max(0, effect.value), "amulet-coin-win");
                            else if (evt.Amount < 0)
                                pendingAmuletRerolls = Mathf.Min(999,
                                    pendingAmuletRerolls + Mathf.Max(0, effect.secondaryValue));
                            continue;
                        }
                        if (effect.kind == MonsterTacticsAugmentEffectKind.DaycareCopy &&
                            evt.Kind == MonsterTacticsGameplayEventKind.RoundResolved &&
                            !shop.IsPveRound)
                        {
                            TickDaycare(Mathf.Max(1, effect.roundInterval));
                            continue;
                        }
                        if (effect.kind == MonsterTacticsAugmentEffectKind.DaycareShift)
                        {
                            if (evt.Kind == MonsterTacticsGameplayEventKind.RoundResolved && daycareShiftElidunebit)
                                shop.GrantGold(Mathf.Max(0, effect.value), "daycare-shift");
                            continue;
                        }
                        if (effect.kind == MonsterTacticsAugmentEffectKind.CatchRelease ||
                            effect.kind == MonsterTacticsAugmentEffectKind.Eviolite ||
                            effect.kind == MonsterTacticsAugmentEffectKind.TeamStatBoost ||
                            effect.kind == MonsterTacticsAugmentEffectKind.EscapeRope ||
                            effect.kind == MonsterTacticsAugmentEffectKind.PPUp ||
                            effect.kind == MonsterTacticsAugmentEffectKind.SuperEffectiveHit ||
                            effect.kind == MonsterTacticsAugmentEffectKind.TimedTeamAttackSpeed ||
                            effect.kind == MonsterTacticsAugmentEffectKind.CriticallyEffective ||
                            effect.kind == MonsterTacticsAugmentEffectKind.SheerForce ||
                            effect.kind == MonsterTacticsAugmentEffectKind.CatchEmAll ||
                            effect.kind == MonsterTacticsAugmentEffectKind.EvolvingTeam ||
                            effect.kind == MonsterTacticsAugmentEffectKind.SafariZoneExplorer ||
                            effect.kind == MonsterTacticsAugmentEffectKind.NuzlockeDiscipline ||
                            effect.kind == MonsterTacticsAugmentEffectKind.TrainersAllowance ||
                            effect.kind == MonsterTacticsAugmentEffectKind.DaycareBreedMaster ||
                            effect.kind == MonsterTacticsAugmentEffectKind.ChampionsStreak ||
                            effect.kind == MonsterTacticsAugmentEffectKind.MonsterMartVIP ||
                            effect.kind == MonsterTacticsAugmentEffectKind.DaycareEvolutionMastery ||
                            effect.kind == MonsterTacticsAugmentEffectKind.WeaknessPolicy ||
                            effect.kind == MonsterTacticsAugmentEffectKind.MonsterCenterVisit ||
                            effect.kind == MonsterTacticsAugmentEffectKind.TrainingGrounds ||
                            effect.kind == MonsterTacticsAugmentEffectKind.TypeResearch ||
                            effect.kind == MonsterTacticsAugmentEffectKind.RareCandyCache ||
                            effect.kind == MonsterTacticsAugmentEffectKind.SecondWind ||
                            effect.kind == MonsterTacticsAugmentEffectKind.TechnicalMachine ||
                            effect.kind == MonsterTacticsAugmentEffectKind.LegendaryGift ||
                            effect.kind == MonsterTacticsAugmentEffectKind.FinalFormFury ||
                            effect.kind == MonsterTacticsAugmentEffectKind.RainbowLeague ||
                            effect.kind == MonsterTacticsAugmentEffectKind.EliteFourSponsorship)
                            continue; // Dedicated sale/round/combat handlers.
                        bool delayedSelection = effect.trigger == MonsterTacticsAugmentTrigger.OnChosen &&
                            effect.delayRounds > 0 && evt.Kind == MonsterTacticsGameplayEventKind.RoundResolved;
                        if (!delayedSelection && !EventMatches(effect.trigger, evt.Kind)) continue;
                        ApplyEffect(i, j, d, effect, variant, false, evt.StageRound);
                    }
                }
                if (evt.Kind == MonsterTacticsGameplayEventKind.RoundResolved)
                    daycareShiftElidunebit = false;
            }
            finally
            {
                processingEvent = false;
                if (!drainingGameplayEvents && deferredGameplayEvents.Count > 0)
                {
                    drainingGameplayEvents = true;
                    try
                    {
                        int safety = 0;
                        while (deferredGameplayEvents.Count > 0 && safety++ < 128)
                            OnGameEvent(deferredGameplayEvents.Dequeue());
                        if (deferredGameplayEvents.Count > 0)
                        {
                            Debug.LogError("[U6.9.2e] Deferred gameplay events exceeded safety limit.");
                            deferredGameplayEvents.Clear();
                        }
                    }
                    finally { drainingGameplayEvents = false; }
                }
            }
        }
        private static bool EventMatches(MonsterTacticsAugmentTrigger trigger, MonsterTacticsGameplayEventKind kind)
        {
            return (trigger == MonsterTacticsAugmentTrigger.PlanningStarted && kind == MonsterTacticsGameplayEventKind.PlanningStarted) ||
                   (trigger == MonsterTacticsAugmentTrigger.RoundResolved && kind == MonsterTacticsGameplayEventKind.RoundResolved) ||
                   (trigger == MonsterTacticsAugmentTrigger.UnitEvolved && kind == MonsterTacticsGameplayEventKind.UnitEvolved);
        }

        private void ApplyEffect(int selectionIndex, int effectIndex, MonsterTacticsAugmentSnapshot def,
            MonsterTacticsAugmentEffect effect, MonsterTacticsAugmentStageVariant variant,
            bool onChosen, string stageRound = "")
        {
            int key = selectionIndex * 1024 + effectIndex;
            effectTriggerCounts.TryGetValue(key, out int used);
            if (effect.maxTriggers > 0 && used >= effect.maxTriggers) return;
            if (effect.delayRounds > 0 &&
                (!chosenAtRound.TryGetValue(selectionIndex, out int at) ||
                 roundResolvedCounter - at < effect.delayRounds)) return;
            if (!onChosen && effect.oncePerRound && !string.IsNullOrEmpty(stageRound) &&
                lastTriggerRound.TryGetValue(key, out var last) && last == stageRound) return;
            int value = effect.value + (variant != null ? variant.effectValueBonus : 0);
            string source = "augment:" + selectionIndex + ":" + def.Id + ":" + effectIndex;
            bool success = false;
            switch (effect.kind)
            {
                case MonsterTacticsAugmentEffectKind.RuleAddition:
                    if (used == 0) { shop.Rules.SetAddition(source, effect.rule, value); success = true; }
                    break;
                case MonsterTacticsAugmentEffectKind.Gold:
                    if (value > 0 && shop.IsShopReady) { shop.GrantGold(value, source); success = true; }
                    break;
                case MonsterTacticsAugmentEffectKind.FreeRerolls:
                    if (value > 0 && shop.IsShopReady) { shop.GrantFreeRerolls(value, source); success = true; }
                    break;
                case MonsterTacticsAugmentEffectKind.ItemReward:
                    if (MonsterTacticsItemTypes.IsAvailable(effect.item))
                        success = shop.GrantItemReward(effect.item, source);
                    break;
                case MonsterTacticsAugmentEffectKind.MonsterPartCapsule:
                case MonsterTacticsAugmentEffectKind.MonsterGearCapsule:
                    // Augment rewards MUST land on the arena just like PvE loot.
                    // They cannot bypass the shared Monster bench or disappear
                    // when the nine reserve slots are full.
                    var loot = FindAnyObjectByType<MonsterTacticsLootController>();
                    if (loot == null && roundManager != null)
                        loot = roundManager.GetComponent<MonsterTacticsLootController>();
                    if (loot != null && value > 0)
                    {
                        var kind = effect.kind == MonsterTacticsAugmentEffectKind.MonsterPartCapsule
                            ? MonsterTacticsItemCapsuleKind.Parts : MonsterTacticsItemCapsuleKind.Gear;
                        int count = Mathf.Clamp(value, 1, 10);
                        success = loot.SpawnAugmentCapsules(kind, count, source) == count;
                    }
                    else if (loot == null)
                    {
                        Debug.LogError("[MonsterTactics U6.9.6] Missing arena loot controller. " +
                            "Supply capsules cannot be granted directly to the bench.");
                    }
                    break;
                case MonsterTacticsAugmentEffectKind.Eviolite:
                    // Passive combat stats are read by the director; queue the free
                    // 2-cost unit without losing it if the bench is currently full.
                    pendingTwoCostGrants++;
                    TryFulfillTwoCostGrants();
                    success = true;
                    break;
                case MonsterTacticsAugmentEffectKind.WeaknessPolicy:
                case MonsterTacticsAugmentEffectKind.MonsterCenterVisit:
                case MonsterTacticsAugmentEffectKind.TrainingGrounds:
                case MonsterTacticsAugmentEffectKind.TypeResearch:
                case MonsterTacticsAugmentEffectKind.SecondWind:
                case MonsterTacticsAugmentEffectKind.TechnicalMachine:
                case MonsterTacticsAugmentEffectKind.FinalFormFury:
                case MonsterTacticsAugmentEffectKind.RainbowLeague:
                case MonsterTacticsAugmentEffectKind.EliteFourSponsorship:
                case MonsterTacticsAugmentEffectKind.CatchRelease:
                case MonsterTacticsAugmentEffectKind.CriticallyEffective:
                case MonsterTacticsAugmentEffectKind.SheerForce:
                case MonsterTacticsAugmentEffectKind.NuzlockeDiscipline:
                case MonsterTacticsAugmentEffectKind.TrainersAllowance:
                case MonsterTacticsAugmentEffectKind.TeamStatBoost:
                case MonsterTacticsAugmentEffectKind.EscapeRope:
                case MonsterTacticsAugmentEffectKind.PPUp:
                case MonsterTacticsAugmentEffectKind.SuperEffectiveHit:
                    // Applied by sale event or during combat. Count selection once.
                    success = true;
                    break;
                case MonsterTacticsAugmentEffectKind.RareCandyCache:
                    if (value > 0 && shop.IsShopReady)
                    {
                        // The initial XP is NOT counted toward the next N level-ups.
                        shop.GrantXP(value);
                        rareCandyLevelsLeft[key] = Mathf.Max(1, effect.roundInterval);
                        success = true;
                    }
                    break;
                case MonsterTacticsAugmentEffectKind.LegendaryGift:
                    // Queue rather than losing a gift when the Monster bench is full.
                    pendingFiveCostOneStars += Mathf.Max(1, value);
                    TryFulfillLegendaryRewards();
                    success = true;
                    break;
                case MonsterTacticsAugmentEffectKind.CatchEmAll:
                    if (value > 0 && shop.IsShopReady)
                    {
                        shop.GrantGold(value, source);
                        success = true;
                        // If the current board already qualifies, reward next Update.
                    }
                    break;
                case MonsterTacticsAugmentEffectKind.MonsterMartVIP:
                    // Set to exactly 1g relative to the standard base rule; no
                    // mutation of the shared default or other selected bonuses.
                    if (used == 0)
                    {
                        shop.Rules.SetAddition(source, MonsterTacticsRuleId.RerollCost,
                            effect.value - MonsterTacticsRuleBook.Default(MonsterTacticsRuleId.RerollCost));
                        success = true;
                    }
                    break;
                case MonsterTacticsAugmentEffectKind.Experience:
                    // Uses the same level-up, XP, team-size and event pipeline as
                    // the normal BUY XP button. No duplicate level calculations.
                    if (value > 0 && shop.IsShopReady && !shop.IsLevelCapped)
                    {
                        shop.GrantXP(value);
                        success = true;
                    }
                    break;
            }
            if (!success) return;
            effectTriggerCounts[key] = used + 1;
            if (!onChosen && effect.oncePerRound) lastTriggerRound[key] = stageRound;
        }

        private void TryDropEliteFourGear()
        {
            if (pendingEliteFourGearDrops <= 0) return;
            var loot = FindAnyObjectByType<MonsterTacticsLootController>();
            if (loot == null) return; // Retry next planning instead of losing a drop.
            int count = Mathf.Min(10, pendingEliteFourGearDrops);
            int spawned = loot.SpawnAugmentCapsules(MonsterTacticsItemCapsuleKind.Gear,
                count, "elite-four-sponsorship");
            pendingEliteFourGearDrops = Mathf.Max(0, pendingEliteFourGearDrops - spawned);
        }

        private void TryFulfillLegendaryRewards()
        {
            if (shop == null || !shop.IsShopReady) return;
            if (rewardRandom == null) rewardRandom = new System.Random(rngSeed);
            while (pendingFiveCostTwoStars > 0 && shop.TryGrantLegendaryFiveCost(2, rewardRandom.Next()))
                pendingFiveCostTwoStars--;
            while (pendingFiveCostOneStars > 0 && shop.TryGrantLegendaryFiveCost(1, rewardRandom.Next()))
                pendingFiveCostOneStars--;
        }

        /// <summary>Rookie Critically Effective: bonus critical chance and
        /// separate 15% Wound for 3 seconds; strongest heal reduction wins.</summary>
        public void GetCriticalAugment(out float critChance, out float woundReduction,
            out float woundSeconds)
        {
            critChance = woundReduction = woundSeconds = 0f;
            if (!active) return;
            foreach (var d in selected)
                foreach (var e in d.Effects)
                    if (e != null && e.kind == MonsterTacticsAugmentEffectKind.CriticallyEffective)
                    {
                        critChance += Mathf.Max(0, e.value) / 100f;
                        woundReduction = Mathf.Max(woundReduction, Mathf.Max(0, e.secondaryValue) / 100f);
                        woundSeconds = Mathf.Max(woundSeconds, Mathf.Max(0.1f, e.intervalSeconds));
                    }
        }

        /// <summary>Find unitemized team bonuses, without touching permanent
        /// stats. Item components AND finished items disable all Sheer Force ranks.</summary>
        public void GetUnitemizedBonuses(MonsterUnit unit,
            out float attackFactor, out int flatAp, out int bonusHp)
        {
            attackFactor = 1f; flatAp = bonusHp = 0;
            if (!active || unit == null) return;
            var held = unit.GetComponent<MonsterTacticsHeldItems>();
            if (held != null && held.Items != null && held.Items.Count > 0) return;
            foreach (var d in selected)
                foreach (var e in d.Effects)
                    if (e != null && e.kind == MonsterTacticsAugmentEffectKind.SheerForce)
                    {
                        attackFactor *= 1f + Mathf.Max(0, e.value) / 100f;
                        flatAp += Mathf.Max(0, e.secondaryValue);
                        bonusHp += Mathf.Max(0, e.tertiaryValue);
                    }
        }

        public void ApplyUnitemizedBonuses(MonsterUnit unit,
            ref MonsterTacticsCombatRules.Stats stats, ref int maximumHp)
        {
            GetUnitemizedBonuses(unit, out float factor, out int ap, out int hp);
            stats.Attack *= factor;
            stats.AbilityPower += ap;
            maximumHp += hp;
        }

        private void UpdateCatchEmAll()
        {
            if (!active || catchEmAllAwarded || shop == null ||
                !shop.IsShopReady || !shop.IsPlanning) return;
            foreach (var d in selected)
                foreach (var e in d.Effects)
                    if (e != null && e.kind == MonsterTacticsAugmentEffectKind.CatchEmAll &&
                        shop.ActiveOriginTraitCount() >= Mathf.Max(1, e.secondaryValue))
                    {
                        catchEmAllAwarded = true; // Trigger only once per match.
                        pendingFiveCostTwoStars++;
                        TryFulfillLegendaryRewards();
                        return;
                    }
        }

        private void TryFulfillTwoCostGrants()
        {
            if (shop == null || !shop.IsShopReady) return;
            while (pendingTwoCostGrants > 0 && shop.TryGrantRandomCostCopy(2))
                pendingTwoCostGrants--;
        }

        /// <summary>Returns live match snapshot values for battle-only effects.</summary>
        public void GetBattleAugmentBonuses(out int oneStarHp, out int oneStarResist,
            out float teamAd, out float teamAs, out int startingMana,
            out int rescueShield, out int rescueThreshold, out float rescueSeconds,
            out float traitDamage)
        {
            oneStarHp = oneStarResist = startingMana = rescueShield = 0;
            teamAd = teamAs = traitDamage = 0f;
            rescueThreshold = 25;
            rescueSeconds = 2.5f;
            if (!active) return;
            foreach (var def in selected)
                foreach (var effect in def.Effects)
                {
                    if (effect == null) continue;
                    switch (effect.kind)
                    {
                        case MonsterTacticsAugmentEffectKind.Eviolite:
                            oneStarHp += Mathf.Max(0, effect.value);
                            oneStarResist += Mathf.Max(0, effect.secondaryValue);
                            break;
                        case MonsterTacticsAugmentEffectKind.TeamStatBoost:
                            teamAs += Mathf.Max(0, effect.value) / 100f;
                            teamAd += Mathf.Max(0, effect.secondaryValue) / 100f;
                            break;
                        case MonsterTacticsAugmentEffectKind.PPUp:
                            startingMana += Mathf.Max(0, effect.value);
                            break;
                        case MonsterTacticsAugmentEffectKind.EscapeRope:
                            rescueShield += Mathf.Max(0, effect.value);
                            rescueThreshold = Mathf.Clamp(effect.secondaryValue, 1, 99);
                            rescueSeconds = Mathf.Max(.1f, effect.intervalSeconds);
                            break;
                        case MonsterTacticsAugmentEffectKind.SuperEffectiveHit:
                            traitDamage += Mathf.Max(0, effect.value) / 100f;
                            break;
                    }
                }
        }

        /// <summary>U6.9.7 all modifiers are sampled fresh at combat start;
        /// the director applies them only to allied fighters and never to base stats.</summary>
        public void ApplyU697CombatStartBonuses(MonsterUnit unit, int star,
            int activeTypes, int distinctTypes,
            ref MonsterTacticsCombatRules.Stats stats, ref int maximumHp)
        {
            if (!active || unit == null) return;
            var held = unit.GetComponent<MonsterTacticsHeldItems>();
            bool completed = false;
            if (held != null && held.Items != null)
                foreach (var item in held.Items)
                    if (MonsterTacticsItemTypes.IsCompleted(item)) { completed = true; break; }
            foreach (var def in selected)
                foreach (var e in def.Effects)
                {
                    if (e == null) continue;
                    switch (e.kind)
                    {
                        case MonsterTacticsAugmentEffectKind.TrainingGrounds:
                            if (star == 1)
                            { stats.AttackSpeed *= 1f + e.value / 100f; maximumHp += e.secondaryValue; }
                            break;
                        case MonsterTacticsAugmentEffectKind.TypeResearch:
                            int amount = Mathf.Min(Mathf.Max(0, e.capPercent),
                                Mathf.Max(0, activeTypes) * Mathf.Max(0, e.value));
                            stats.Armor += amount; stats.MagicResist += amount;
                            break;
                        case MonsterTacticsAugmentEffectKind.TechnicalMachine:
                            if (completed)
                            { stats.AttackSpeed *= 1f + e.value / 100f; stats.AbilityPower += e.secondaryValue; }
                            break;
                        case MonsterTacticsAugmentEffectKind.FinalFormFury:
                            if (star >= 3)
                            { stats.DamageMultiplier *= 1f + e.secondaryValue / 100f;
                              maximumHp += e.tertiaryValue; }
                            else if (star == 2)
                            { stats.DamageMultiplier *= 1f + e.value / 100f;
                              maximumHp += e.capPercent; }
                            break;
                        case MonsterTacticsAugmentEffectKind.RainbowLeague:
                            if (distinctTypes >= Mathf.Max(1, e.roundInterval))
                            { stats.DamageMultiplier *= 1f + e.value / 100f;
                              maximumHp = Mathf.RoundToInt(maximumHp *
                                  (1f + e.secondaryValue / 100f)); }
                            break;
                    }
                }
        }

        public void GetU697ThresholdEffects(out int weaknessAd, out int weaknessAp,
            out int secondWindPercent, out float secondWindSeconds,
            out int centerShieldPercent, out float centerShieldSeconds,
            out int centerShieldCount)
        {
            weaknessAd = weaknessAp = secondWindPercent = centerShieldPercent = 0;
            secondWindSeconds = centerShieldSeconds = 3f;
            centerShieldCount = 0;
            if (!active) return;
            foreach (var def in selected)
                foreach (var e in def.Effects)
                {
                    if (e == null) continue;
                    switch (e.kind)
                    {
                        case MonsterTacticsAugmentEffectKind.WeaknessPolicy:
                            weaknessAd += Mathf.Max(0, e.value);
                            weaknessAp += Mathf.Max(0, e.secondaryValue);
                            break;
                        case MonsterTacticsAugmentEffectKind.SecondWind:
                            secondWindPercent += Mathf.Max(0, e.value);
                            secondWindSeconds = Mathf.Max(.1f, e.intervalSeconds);
                            break;
                        case MonsterTacticsAugmentEffectKind.MonsterCenterVisit:
                            centerShieldPercent += Mathf.Max(0, e.value);
                            centerShieldSeconds = Mathf.Max(.1f, e.intervalSeconds);
                            centerShieldCount = Mathf.Max(centerShieldCount, e.secondaryValue);
                            break;
                    }
                }
        }

        private bool SelectedHasEffect(MonsterTacticsAugmentEffectKind kind)
        {
            foreach (var def in selected)
                foreach (var effect in def.Effects)
                    if (effect != null && effect.kind == kind) return true;
            return false;
        }

        // Called by the actual combat director; only the player's fighters receive it.
        // Stack at 2s, 4s, ... capped at the configured percentage (default 50%).
        public float CombatTeamAttackSpeedFactor(float realSeconds)
        {
            if (!active) return 1f;
            float additional = 0f;
            foreach (var def in selected)
                foreach (var effect in def.Effects)
                {
                    if (effect == null || effect.kind != MonsterTacticsAugmentEffectKind.TimedTeamAttackSpeed) continue;
                    int ticks = Mathf.FloorToInt(Mathf.Max(0f, realSeconds) /
                        Mathf.Max(.1f, effect.intervalSeconds));
                    additional += Mathf.Min(Mathf.Max(0, effect.capPercent),
                        ticks * Mathf.Max(0, effect.value)) / 100f;
                }
            return 1f + additional;
        }

        public bool SetDaycareSlot(int index)
        {
            if (!HasDaycare || shop == null || !shop.IsPlanning || index < 0 || index >= 9)
                return false;
            if (daycareSlot == index) return true;
            daycareSlot = index;
            daycarePvpProgress = 0;
            daycareTrackedFamily = "";
            return true;
        }

        private void TickDaycare(int interval)
        {
            if (shop == null) return;
            string family = shop.BenchFamilyAtSlot(daycareSlot);
            if (string.IsNullOrEmpty(family))
            { daycareTrackedFamily = ""; daycarePvpProgress = 0; return; }
            if (!string.Equals(family, daycareTrackedFamily, StringComparison.OrdinalIgnoreCase))
            { daycareTrackedFamily = family; daycarePvpProgress = 0; }
            daycarePvpProgress++;
            if (daycarePvpProgress < interval) return;
            daycarePvpProgress = 0;
            pendingDaycareCopies.Enqueue(daycareTrackedFamily);
            TryFulfillDaycareCopies();
        }

        public void TryFulfillDaycareCopies()
        {
            if (!active || shop == null || !shop.IsShopReady) return;
            // Rewards never vanish when the nine-slot bench is full.
            while (pendingDaycareCopies.Count > 0)
            {
                if (!shop.TryGrantDaycareCopy(pendingDaycareCopies.Peek())) break;
                pendingDaycareCopies.Dequeue();
            }
        }

        // Diagnostics are available from the component context menu.
        [ContextMenu("U6.9.0 | Show Framework Status")]
        private void ShowFrameworkStatus() => Debug.Log("[U6.9.0] " + lastNotice +
            " | snapshots=" + library.Count + " selected=" + selected.Count, this);
    }
}
