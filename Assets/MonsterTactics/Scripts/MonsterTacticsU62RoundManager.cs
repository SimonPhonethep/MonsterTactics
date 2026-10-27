using UnityEngine;
using UnityEngine.SceneManagement;

namespace MonsterTactics
{
    /// <summary>U6.2 automatic, configurable TFT-like phase and stage scheduler.
    /// Combat and reward calculations remain owned by the existing U4/U5 systems.</summary>
    [DisallowMultipleComponent]
    public sealed class MonsterTacticsU62RoundManager : MonoBehaviour
    {
        public enum RoundKind { PvE, PvPPrototype, Special, Boss }
        [System.Serializable]
        public class StageSchedule
        {
            public RoundKind[] rounds = { RoundKind.PvPPrototype, RoundKind.PvPPrototype,
                RoundKind.PvPPrototype, RoundKind.Special, RoundKind.PvPPrototype,
                RoundKind.PvPPrototype, RoundKind.Boss };
        }

        [SerializeField] private MonsterTacticsCombatDirector combat;
        [SerializeField] private MonsterTacticsU5ShopManager shop;
        [SerializeField, Min(3f)] private float planningSeconds = 30f;
        [Header("U6.3.1b: Shorter PvE planning")]
        [SerializeField, Min(3f)] private float pvePlanningSeconds = 20f;
        [Header("U6.9.1c: TFT-like planning windows")]
        [SerializeField, Min(3f)] private float stageOnePvePlanningSeconds = 10f;
        [SerializeField, Min(3f)] private float augmentPlanningSeconds = 60f;
        [SerializeField, Min(1f)] private float resultsSeconds = 5f;
        [SerializeField] private StageSchedule defaultStage = new StageSchedule();
        [SerializeField] private StageSchedule openingStage = new StageSchedule
        {
            rounds = new [] { RoundKind.PvE, RoundKind.PvE, RoundKind.PvE, RoundKind.Boss }
        };
        [SerializeField] private StageSchedule finalBossStage = new StageSchedule
        {
            rounds = new [] { RoundKind.Boss }
        };
        private int stage = 1;
        private int round = 1;
        private float remaining;
        private bool initialized;
        private bool resultsHandled;
        private bool matchComplete;
        private bool victoryComplete;
        private bool eliminated;
        private bool debugBossPlanningPaused;
        private MonsterTacticsLootController loot;
        private MonsterTacticsAugmentManager augmentManager;
        // U6.9.1b: one shared combat-start deadline on augment rounds.
        private bool augmentRoundDeadline;
        private string augmentDeadlineStage;

        [Header("U6.4 Player Health & Match Results")]
        [SerializeField, Range(1, 200)] private int startingPlayerHP = 100;
        [SerializeField] private bool pveLossesDamagePlayer = true;
        private int currentPlayerHP = 100;
        private int lastRoundDamage;
        private int lastEnemiesSurvived;
        public int PlayerHP => currentPlayerHP;
        public int MaxPlayerHP => Mathf.Max(1, startingPlayerHP);
        public int LastRoundDamage => lastRoundDamage;
        public int LastEnemiesSurvived => lastEnemiesSurvived;
        public bool MatchEnded => matchComplete;
        public bool PlayerEliminated => eliminated;
        public bool PlayerWonMatch => victoryComplete;

        // Stable early-game damage; later stages hurt more. Only surviving
        // enemies contribute extra damage. Bosses count as one enemy.
        public static int DamageOnRoundLoss(int currentStage, int survivors)
        {
            int[] baseByStage = { 0, 2, 3, 4, 5, 6, 8, 10, 12 };
            int safeStage = Mathf.Clamp(currentStage, 1, 8);
            int perSurvivor = safeStage <= 3 ? 1 : safeStage <= 6 ? 2 : 3;
            return baseByStage[safeStage] + Mathf.Max(0, survivors) * perSurvivor;
        }
        private const int FinalStage = 8;
        private const int FinalRound = 1;
        private string message = "";
        private GUIStyle headline;
        private GUIStyle tiny;
        private GUIStyle centered;

        public int Stage => stage;
        public int Round => round;
        public string StageRound => stage + "-" + round;
        public RoundKind CurrentKind => Schedule().rounds[Mathf.Clamp(round - 1, 0, Schedule().rounds.Length - 1)];

        private StageSchedule Schedule() => stage == 1 ? openingStage : stage == 8 ? finalBossStage : defaultStage;

        private bool IsPveEncounter => CurrentKind == RoundKind.PvE || CurrentKind == RoundKind.Boss;
        // A single round clock governs normal planning and augment choosing. Catalog
        // first/later selection seconds remain for the standalone sandbox preview;
        // scheduled matches use this inspector-editable 60-second round deadline.
        private float PlanningTimeForCurrentRound => StagePlanningDuration(
            stage, IsPveEncounter, augmentManager != null && augmentManager.IsActive &&
            MonsterTacticsAugmentManager.SlotForStage(StageRound) != MonsterTacticsAugmentSlots.None,
            planningSeconds, pvePlanningSeconds, stageOnePvePlanningSeconds,
            augmentPlanningSeconds);

        public static float StagePlanningDuration(int currentStage, bool pve, bool augmentRound,
            float pvpSeconds, float pveSeconds, float stageOneSeconds, float augmentSeconds)
        {
            if (augmentRound) return Mathf.Max(3f, augmentSeconds);
            if (currentStage == 1 && pve) return Mathf.Max(3f, stageOneSeconds);
            return Mathf.Max(3f, pve ? pveSeconds : pvpSeconds);
        }
        public float PlanningSecondsRemaining => Mathf.Max(0f, remaining);

        private void Awake()
        {
            // Serialized pre-U6.3.1 scenes may still contain three PvE rounds.
            // Force the approved opening schedule at runtime without scene editing.
            if (openingStage == null) openingStage = new StageSchedule();
            if (defaultStage == null) defaultStage = new StageSchedule();
            if (finalBossStage == null) finalBossStage = new StageSchedule();
            openingStage.rounds = new [] { RoundKind.PvE, RoundKind.PvE, RoundKind.PvE, RoundKind.Boss };
            // Scene settings from older builds cannot accidentally remove solo bosses.
            defaultStage.rounds = new [] { RoundKind.PvPPrototype, RoundKind.PvPPrototype,
                RoundKind.PvPPrototype, RoundKind.Special, RoundKind.PvPPrototype,
                RoundKind.PvPPrototype, RoundKind.Boss };
            finalBossStage.rounds = new [] { RoundKind.Boss };
            // Existing scenes serialized the old 15s PvE default. Migrate that
            // exact legacy value to the new 20s default without touching overrides.
            if (Mathf.Approximately(pvePlanningSeconds, 15f)) pvePlanningSeconds = 20f;
            if (combat == null) combat = FindAnyObjectByType<MonsterTacticsCombatDirector>();
            if (shop == null) shop = FindAnyObjectByType<MonsterTacticsU5ShopManager>();
            augmentManager = FindAnyObjectByType<MonsterTacticsAugmentManager>();
            loot = GetComponent<MonsterTacticsLootController>();
            if (loot == null) loot = gameObject.AddComponent<MonsterTacticsLootController>();
        }

        private void OnEnable()
        {
            remaining = PlanningTimeForCurrentRound;
            initialized = false;
            matchComplete = false;
            victoryComplete = false;
            eliminated = false;
            currentPlayerHP = MaxPlayerHP;
            lastRoundDamage = 0;
            lastEnemiesSurvived = 0;
            resultsHandled = false;
            debugBossPlanningPaused = false;
        }

        private void OnDisable()
        {
            if (combat != null) combat.AutomaticRoundsEnabled = false;
        }

        private void Update()
        {
            if (combat == null) return;
            combat.AutomaticRoundsEnabled = true;
            if (matchComplete) return;
            if (!initialized)
            {
                if (combat.MatchPhase != "Planning") return;
                initialized = true;
                remaining = PlanningTimeForCurrentRound;
            }

            switch (combat.MatchPhase)
            {
                case "Planning":
                    // U6.9.1b: the augment and the combat-start deadline are
                    // the SAME countdown. The cards can be hidden for board inspection,
                    // but the clock never stops. Early picks keep the remaining time.
                    if (augmentDeadlineStage != StageRound)
                    {
                        augmentDeadlineStage = StageRound;
                        augmentRoundDeadline = false;
                    }
                    if (augmentManager == null)
                        augmentManager = FindAnyObjectByType<MonsterTacticsAugmentManager>();
                    if (augmentManager != null && !augmentManager.IsSandboxPreview &&
                        augmentManager.TryBeginForStage(StageRound) &&
                        augmentManager.IsSelectionOpen &&
                        augmentManager.CurrentSlot == MonsterTacticsAugmentManager.SlotForStage(StageRound))
                    {
                        if (!augmentRoundDeadline)
                        {
                            augmentRoundDeadline = true;
                            // Don't restart planning if a depleted catalog only
                            // became elidunebit partway through this round.
                            remaining = Mathf.Min(remaining, Mathf.Max(3f, augmentPlanningSeconds));
                            // Match the visible augment countdown to the SAME round
                            // clock. UI-only previews retain the catalog test timers.
                            augmentManager.SetScheduledSelectionDeadline(remaining);
                        }
                    }
                    if (augmentRoundDeadline && augmentManager != null &&
                        augmentManager.IsSelectionOpen && !augmentManager.IsSandboxPreview &&
                        augmentManager.CurrentSlot == MonsterTacticsAugmentManager.SlotForStage(StageRound))
                    {
                        // The augment manager advances its countdown in Update.
                        // Draw the same number on the stage HUD as the cards.
                        remaining = Mathf.Max(0f, augmentManager.SelectionSecondsRemaining);
                        message = "Choose an augment — combat at 0s";
                        if (remaining <= 0f)
                            augmentManager.AutoChooseVisible();
                    }
                    // Keep the PvE team visible on the actual enemy hexes, including
                    // while the boss tester has the planning clock paused.
                    // Normal PvP rounds continue to spawn opponents at battle start.
                    if (stage == 1 && !MonsterTacticsWildSpriteLibrary.StageOneArtReady)
                    {
                        message = "Import Stage 1 sprites: MonsterTactics > Monster > U6.3.1";
                        return;
                    }
                    if (stage > 1 && CurrentKind == RoundKind.Boss &&
                        !MonsterTacticsBossEncounters.SpriteReady(stage, round))
                    {
                        message = "Import boss sprites: MonsterTactics > Monster > U6.3.2";
                        return;
                    }
                    if (IsPveEncounter)
                        combat.PreparePvePreview(stage, round);
                    else
                        combat.ClearPvePreviewForNonPve();
                    // Countdown never stalls for an empty board. Elidunebit reserves
                    // are deployed at the deadline, never in the middle of planning.
                    if (!combat.HasDeployedTeam)
                        message = "Board empty — reserves auto-deploy at 0s";
                    message = stage == 1 ? MonsterTacticsStageOneEncounters.Title(round) :
                        CurrentKind == RoundKind.Boss ? MonsterTacticsBossEncounters.Title(stage, round) :
                        "Up next: " + MonsterTacticsAiOpponentRoster.TrainerName(stage, round);
                    if (debugBossPlanningPaused)
                    {
                        message = CurrentKind == RoundKind.Boss ?
                            "BOSS TEST: Prepare your team, then click Start Fight Now" :
                            "AI TEST: Build your team, then click Start Fight Now";
                        return;
                    }
                    // During an open scheduled offer, remaining already comes from
                    // the augment clock; after an early pick it advances normally.
                    bool scheduledOfferOpen = augmentRoundDeadline && augmentManager != null &&
                        augmentManager.IsSelectionOpen && !augmentManager.IsSandboxPreview &&
                        augmentManager.CurrentSlot == MonsterTacticsAugmentManager.SlotForStage(StageRound);
                    if (!scheduledOfferOpen)
                        remaining -= augmentRoundDeadline ? Time.unscaledDeltaTime : Time.deltaTime;
                    if (remaining <= 0f)
                    {
                        // Deadline reached: choose from the CURRENT three cards,
                        // never a stale pre-reroll set. Do this before combat starts.
                        if (scheduledOfferOpen)
                            augmentManager.AutoChooseVisible();
                        if (augmentManager != null && augmentManager.IsSelectionOpen &&
                            !augmentManager.IsSandboxPreview &&
                            augmentManager.CurrentSlot == MonsterTacticsAugmentManager.SlotForStage(StageRound))
                        {
                            message = "Resolving augment choice";
                            return;
                        }
                        // TFT-like deployment: fill vacant team slots from reserves
                        // even when the board is partially occupied. Never exceed cap.
                        if (shop != null) shop.AutoDeployElidunebitBenchUnits();
                        if (!combat.HasDeployedTeam)
                        {
                            // No units owned anywhere: resolve the round as a loss
                            // rather than trapping the scheduler at 1 second forever.
                            if (combat.ForfeitEmptyTeamRound())
                            {
                                resultsHandled = false;
                                message = "No Monster available — round forfeited";
                            }
                            else remaining = 1f;
                            break;
                        }
                        combat.StartBattle();
                        if (combat.MatchPhase == "Battle")
                        {
                            resultsHandled = false;
                            message = string.IsNullOrEmpty(combat.CurrentOpponentName) ?
                                "Combat!" : "Versus " + combat.CurrentOpponentName;
                        }
                        else remaining = 1f;
                    }
                    break;
                case "Battle":
                    message = "Auto battle in progress";
                    break;
                case "Results":
                    if (!resultsHandled)
                    {
                        resultsHandled = true;
                        // Spawn preplanned loot ONCE when PvE Results begin; a loss
                        // still gets its preallocated rewards, as in PvE monster loot.
                        if (loot != null && !combat.WasEmptyTeamForfeit)
                            loot.ReleaseForEncounter(stage, round, CurrentKind);
                        remaining = resultsSeconds;
                        message = combat.LastResult;
                        // Damage applies exactly once per Results transition, never
                        // every frame of the 5-second countdown.
                        lastRoundDamage = 0;
                        lastEnemiesSurvived = combat.SurvivingOpponentCount;
                        if (combat.LastResult == "DEFEAT" &&
                            (!IsPveEncounter || pveLossesDamagePlayer))
                        {
                            lastRoundDamage = DamageOnRoundLoss(stage, lastEnemiesSurvived);
                            currentPlayerHP = Mathf.Max(0, currentPlayerHP - lastRoundDamage);
                            // Only ACTUAL loss of player HP triggers Nuzlocke.
                            if (shop != null && lastRoundDamage > 0)
                                shop.EmitGameplayEvent(MonsterTacticsGameplayEventKind.PlayerDamaged,
                                    "round-loss", lastRoundDamage);
                            message += "  ·  -" + lastRoundDamage + " player HP";
                            if (currentPlayerHP == 0)
                            {
                                eliminated = true;
                                matchComplete = true;
                                message = "ELIMINATED AT " + StageRound;
                            }
                        }
                        // The shop watches Results to pay out gold exactly once.
                        // XP is added when the next planning phase starts.
                    }
                    remaining -= Time.deltaTime;
                    if (remaining <= 0f)
                    {
                        bool isLastRound = stage == FinalStage && round == FinalRound;
                        // Winning a timeout on HP score is not the same as
                        // defeating VortexPrime. A true final clear requires 0 foes.
                        bool defeatedFinalBoss = isLastRound && combat.LastResult == "VICTORY" &&
                            combat.SurvivingOpponentCount == 0;
                        if (defeatedFinalBoss)
                        {
                            if (shop != null) shop.GrantXP(2); // +2 XP even for final boss
                            victoryComplete = true;
                            matchComplete = true;
                            message = "CHAMPION! VortexPrime defeated at 8-1";
                            break;
                        }
                        combat.ResetBattle();
                        // Failed 8-1 but survived? Retry VortexPrime instead of claiming
                        // completion or advancing to a nonexistent 8-2.
                        if (!isLastRound) Advance();
                        debugBossPlanningPaused = false;
                        remaining = PlanningTimeForCurrentRound;
                        if (shop != null)
                        {
                            shop.GrantXP(2); // exactly +2 after every round, including bosses
                            shop.RefreshForNewRound();
                        }
                        message = isLastRound ? "VortexPrime rematch — prepare your team" : "Next round!";
                    }
                    break;
            }
        }

        private void Advance()
        {
            round++;
            if (round > Mathf.Max(1, Schedule().rounds.Length))
            {
                stage++;
                round = 1;
            }
        }

        /// <summary>
        /// Play-mode diagnostics only. Jump directly to a PvE solo boss without
        /// stepping the normal stage scheduler, XP grants or income history.
        /// </summary>
        public bool DebugJumpToBoss(int targetStage)
        {
            if (!Application.isPlaying || targetStage < 2 || targetStage > 8 || combat == null)
                return false;
            if (combat.MatchPhase != "Planning") combat.ResetBattle();
            stage = targetStage;
            round = targetStage == 8 ? 1 : 7;
            // Boss tester deliberately preserves HP: choosing another test
            // stage does not secretly heal the player or award round income.
            matchComplete = false;
            victoryComplete = false;
            eliminated = false;
            resultsHandled = false;
            initialized = true;
            debugBossPlanningPaused = true;
            remaining = PlanningTimeForCurrentRound;
            message = MonsterTacticsBossEncounters.Title(stage, round) + " • Test mode";
            combat.AutomaticRoundsEnabled = true;
            if (shop != null) shop.RefreshForNewRound();
            return true;
        }

        public bool DebugStartBossNow()
        {
            if (!Application.isPlaying || combat == null || combat.MatchPhase != "Planning" ||
                CurrentKind != RoundKind.Boss || stage == 1 || !combat.HasDeployedTeam ||
                !MonsterTacticsBossEncounters.SpriteReady(stage, round)) return false;
            combat.StartBattle();
            if (combat.MatchPhase != "Battle") return false;
            debugBossPlanningPaused = false;
            resultsHandled = false;
            message = MonsterTacticsBossEncounters.Title(stage, round);
            return true;
        }

        /// <summary>U6.5 Editor test controls: pick a regular simulated-PvP round.</summary>
        public bool DebugJumpToAiRound(int targetStage, int targetRound)
        {
            if (!Application.isPlaying || matchComplete || combat == null ||
                !MonsterTacticsAiOpponentRoster.IsRegularOpponentRound(targetStage, targetRound))
                return false;
            if (combat.MatchPhase != "Planning") combat.ResetBattle();
            combat.ClearPvePreviewForNonPve();
            stage = targetStage;
            round = targetRound;
            resultsHandled = false;
            initialized = true;
            debugBossPlanningPaused = true;
            remaining = PlanningTimeForCurrentRound;
            message = "AI TEST: Prepare team vs " + MonsterTacticsAiOpponentRoster.TrainerName(stage, round);
            if (shop != null) shop.RefreshForNewRound();
            return true;
        }

        public bool DebugStartAiNow()
        {
            if (!Application.isPlaying || combat == null || matchComplete ||
                combat.MatchPhase != "Planning" || !combat.HasDeployedTeam ||
                !MonsterTacticsAiOpponentRoster.IsRegularOpponentRound(stage, round))
                return false;
            combat.StartBattle();
            if (combat.MatchPhase != "Battle") return false;
            debugBossPlanningPaused = false;
            resultsHandled = false;
            message = "Versus " + combat.CurrentOpponentName;
            return true;
        }

        private void MakeStyles()
        {
            if (headline != null) return;
            headline = new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter };
            headline.normal.textColor = Color.white;
            tiny = new GUIStyle(GUI.skin.label) { fontSize = 11, alignment = TextAnchor.MiddleCenter };
            tiny.normal.textColor = Color.white;
            centered = new GUIStyle(GUI.skin.label) { fontSize = 12, alignment = TextAnchor.MiddleCenter };
            centered.normal.textColor = Color.white;
        }

        private void OnGUI()
        {
            // The augment overlay owns the presentation while expanded. The
            // round clock keeps running independently of this drawing guard.
            // One augment clock strip replaces the normal round timeline in both
            // expanded and collapsed modes; prevents duplicate overlapping timers.
            if (augmentManager != null && augmentManager.IsSelectionOpen) return;
            if (combat == null || !initialized) return;
            MakeStyles();
            int count = Mathf.Max(1, Schedule().rounds.Length);
            float width = Mathf.Min(450f, Screen.width - 20f);
            float x = (Screen.width - width) * .5f;
            GUI.Box(new Rect(x, 8f, width, 91f), GUIContent.none);
            string phase = combat.MatchPhase;
            bool choosingAugment = augmentManager != null && augmentManager.IsSelectionOpen;
            string timer = choosingAugment ? Mathf.CeilToInt(Mathf.Max(0f,
                augmentManager.SelectionSecondsRemaining)) + "s" :
                phase == "Battle" ? "AUTO" : Mathf.CeilToInt(Mathf.Max(0, remaining)) + "s";
            GUI.Label(new Rect(x + 5f, 11f, width - 10f, 24f),
                "STAGE " + StageRound + "  •  " + (matchComplete ? "COMPLETE" :
                choosingAugment ? "AUGMENT" : phase.ToUpperInvariant()) + "  •  " +
                (matchComplete ? "END" : timer), headline);
            float cell = (width - 20f) / count;
            for (int i = 0; i < count; ++i)
            {
                RoundKind kind = Schedule().rounds[i];
                Rect r = new Rect(x + 10 + i * cell + 1, 39, cell - 2, 29);
                Color old = GUI.color;
                GUI.color = i + 1 == round ? new Color(.28f, .78f, .43f) :
                    i + 1 < round ? new Color(.29f, .47f, .35f) : new Color(.20f, .29f, .36f);
                GUI.DrawTexture(r, Texture2D.whiteTexture);
                GUI.color = old;
                string symbol = kind == RoundKind.Boss ? "B" : kind == RoundKind.Special ? "★" :
                    kind == RoundKind.PvE ? "E" : "⚔";
                GUI.Label(r, symbol + " " + stage + "-" + (i + 1), tiny);
            }
            GUI.Label(new Rect(x + 4, 72, width - 8, 17), message, centered);
            if (matchComplete) DrawEndScreen();
        }

        private void DrawEndScreen()
        {
            float sw = Screen.width, sh = Screen.height;
            Color tint = GUI.color;
            GUI.color = new Color(0.015f, 0.035f, 0.06f, 0.90f);
            GUI.DrawTexture(new Rect(0, 0, sw, sh), Texture2D.whiteTexture);
            GUI.color = tint;
            float cardW = Mathf.Min(490f, sw - 32f);
            float cardH = Mathf.Min(314f, sh - 30f);
            float cardX = (sw - cardW) * .5f;
            float cardY = (sh - cardH) * .5f;
            GUI.Box(new Rect(cardX, cardY, cardW, cardH), GUIContent.none);
            string heading = victoryComplete ? "MONSTERTACTICS CHAMPION!" : "YOU WERE ELIMINATED";
            GUI.Label(new Rect(cardX + 12, cardY + 30, cardW - 24, 44), heading, headline);
            string subtitle = victoryComplete ? "VortexPrime defeated — match complete" :
                "Your trainer ran out of HP";
            GUI.Label(new Rect(cardX + 20, cardY + 88, cardW - 40, 28), subtitle, centered);
            GUI.Label(new Rect(cardX + 20, cardY + 135, cardW - 40, 24),
                "Reached Stage " + StageRound + "   •   HP " + PlayerHP + " / " + MaxPlayerHP, centered);
            if (GUI.Button(new Rect(cardX + (cardW - 210f) / 2f, cardY + cardH - 76f, 210f, 48f),
                "RESTART MATCH")) RestartMatch();
        }

#if UNITY_EDITOR
        // Local Play Mode diagnostics; excluded from production player builds.
        public void DebugSetPlayerHP(int value)
        {
            if (!Application.isPlaying || matchComplete) return;
            currentPlayerHP = Mathf.Clamp(value, 1, MaxPlayerHP);
            lastRoundDamage = 0;
        }

        public void DebugEndMatch(bool won)
        {
            if (!Application.isPlaying) return;
            matchComplete = true;
            victoryComplete = won;
            eliminated = !won;
            if (!won) currentPlayerHP = 0;
            message = won ? "CHAMPION (test)" : "ELIMINATED (test)";
        }
#endif

        public void RestartMatch()
        {
            // Scene reload resets rounds, health, money, bench, pool and all
            // runtime-instantiated enemies. Add the gameplay scene to Build Settings.
            Scene scene = SceneManager.GetActiveScene();
            if (scene.buildIndex >= 0) SceneManager.LoadScene(scene.buildIndex);
            else SceneManager.LoadScene(scene.name);
        }
    }
}
