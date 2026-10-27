using System;
using System.Collections.Generic;
using UnityEngine;

namespace MonsterTactics
{
    public enum MonsterTacticsAugmentTier { Rookie, Ace, Champion }
    public enum MonsterTacticsAugmentCategory { Economy, Shop, Combat, Traits, Items, Monster, Special }
    [Flags]
    public enum MonsterTacticsAugmentSlots { None = 0, First = 1, Second = 2, Final = 4, All = 7 }
    public enum MonsterTacticsAugmentTrigger { OnChosen, PlanningStarted, RoundResolved, UnitEvolved }
    public enum MonsterTacticsAugmentEffectKind
    {
        RuleAddition, Gold, FreeRerolls, ItemReward,
        // Explicitly blocked until U6.9.2–U6.9.4 have proper handlers.
        FutureCombat, FutureMonsterReward, FutureItemChoice, FutureArtifact, FutureTrait,
        // U6.9.2: appended so all existing serialized enum numbers stay unchanged.
        Experience,
        // U6.9.2c custom effects: append only, preserving all serialized IDs.
        AmuletCoin, DaycareCopy, TimedTeamAttackSpeed,
        // U6.9.2d: append-only enum (Unity serializes these integer values).
        CatchRelease, DaycareShift, Eviolite, TeamStatBoost,
        EscapeRope, PPUp, SuperEffectiveHit,
        // U6.9.2e: new player-designed augments, append-only for Unity serialization.
        CriticallyEffective, SheerForce, EvolvingTeam, SafariZoneExplorer,
        NuzlockeDiscipline, TrainersAllowance, DaycareBreedMaster, ChampionsStreak,
        MonsterMartVIP, CatchEmAll, DaycareEvolutionMastery,
        // U6.9.3: append-only, safe for existing serialized augment assets.
        MonsterPartCapsule, MonsterGearCapsule,
        // U6.9.7: 12 new augments; keep enum IDs serialized by Unity stable.
        WeaknessPolicy, MonsterCenterVisit, TrainingGrounds, TypeResearch,
        RareCandyCache, SecondWind, TechnicalMachine,
        LegendaryGift, FinalFormFury, RainbowLeague, EliteFourSponsorship
    }
    public enum MonsterTacticsAugmentRequirementKind { MinimumLevel, MinimumGold, ActiveTrait, OwnedMonster }

    [Serializable]
    public sealed class MonsterTacticsAugmentRequirement
    {
        public MonsterTacticsAugmentRequirementKind kind;
        public int threshold = 1;
        public string key = ""; // Trait/family identifier where appropriate.
    }

    [Serializable]
    public sealed class MonsterTacticsAugmentEffect
    {
        public MonsterTacticsAugmentEffectKind kind;
        public MonsterTacticsAugmentTrigger trigger = MonsterTacticsAugmentTrigger.OnChosen;
        public MonsterTacticsRuleId rule = MonsterTacticsRuleId.RerollCost;
        public int value = 1;
        [Tooltip("Amulet Coin: free next-planning rerolls on a PvP loss.")]
        [Min(0)] public int secondaryValue;
        [Tooltip("Optional third stat/reward: e.g. Sheer Force ++ health bonus.")]
        [Min(0)] public int tertiaryValue;
        [Tooltip("Daycare: number of completed PvP combats needed per free 1-star copy.")]
        [Min(1)] public int roundInterval = 3;
        [Tooltip("Speed Boost: elapsed real seconds between stack increases.")]
        [Min(0.1f)] public float intervalSeconds = 2f;
        [Tooltip("Speed Boost: maximum percent attack speed from this effect.")]
        [Min(0)] public int capPercent = 50;
        public MonsterTacticsItemKind item = MonsterTacticsItemKind.AttackFang;
        [Min(0)] public int delayRounds;
        // 0 = no maximum. RuleAddition is passive and applied once on selection.
        [Min(0)] public int maxTriggers = 1;
        public bool oncePerRound = true;
    }

    /// <summary>
    /// One authoritative definition of offerable effect kinds; do not use enum
    /// greater-than comparisons because supported kinds can be appended later
    /// without renumbering Unity-serialized legacy values.
    /// </summary>
    public static class MonsterTacticsAugmentEffectSupport
    {
        public static bool IsExecutable(MonsterTacticsAugmentEffect effect)
        {
            if (effect == null) return false;
            switch (effect.kind)
            {
                case MonsterTacticsAugmentEffectKind.RuleAddition:
                    return effect.trigger == MonsterTacticsAugmentTrigger.OnChosen;
                case MonsterTacticsAugmentEffectKind.Gold:
                case MonsterTacticsAugmentEffectKind.FreeRerolls:
                case MonsterTacticsAugmentEffectKind.Experience:
                    return effect.value > 0;
                case MonsterTacticsAugmentEffectKind.ItemReward:
                    return MonsterTacticsItemTypes.IsAvailable(effect.item);
                case MonsterTacticsAugmentEffectKind.MonsterPartCapsule:
                case MonsterTacticsAugmentEffectKind.MonsterGearCapsule:
                    return effect.value > 0;
                case MonsterTacticsAugmentEffectKind.AmuletCoin:
                    return effect.trigger == MonsterTacticsAugmentTrigger.RoundResolved &&
                        effect.value > 0 && effect.secondaryValue >= 0;
                case MonsterTacticsAugmentEffectKind.DaycareCopy:
                    return effect.trigger == MonsterTacticsAugmentTrigger.RoundResolved &&
                        effect.roundInterval > 0;
                case MonsterTacticsAugmentEffectKind.TimedTeamAttackSpeed:
                    return effect.trigger == MonsterTacticsAugmentTrigger.OnChosen &&
                        effect.value > 0 && effect.intervalSeconds > 0f && effect.capPercent > 0;

                case MonsterTacticsAugmentEffectKind.CatchRelease:
                    return effect.trigger == MonsterTacticsAugmentTrigger.OnChosen &&
                        effect.value > 0 && effect.secondaryValue > 0;
                case MonsterTacticsAugmentEffectKind.DaycareShift:
                    return effect.trigger == MonsterTacticsAugmentTrigger.RoundResolved && effect.value > 0;
                case MonsterTacticsAugmentEffectKind.Eviolite:
                    return effect.trigger == MonsterTacticsAugmentTrigger.OnChosen &&
                        effect.value >= 0 && effect.secondaryValue >= 0;
                case MonsterTacticsAugmentEffectKind.TeamStatBoost:
                    return effect.trigger == MonsterTacticsAugmentTrigger.OnChosen &&
                        effect.value > 0 && effect.secondaryValue >= 0;
                case MonsterTacticsAugmentEffectKind.EscapeRope:
                    return effect.trigger == MonsterTacticsAugmentTrigger.OnChosen &&
                        effect.value > 0 && effect.secondaryValue > 0 &&
                        effect.secondaryValue < 100 && effect.intervalSeconds > 0f;
                case MonsterTacticsAugmentEffectKind.PPUp:
                case MonsterTacticsAugmentEffectKind.SuperEffectiveHit:
                    return effect.trigger == MonsterTacticsAugmentTrigger.OnChosen && effect.value > 0;
                case MonsterTacticsAugmentEffectKind.CriticallyEffective:
                    return effect.trigger == MonsterTacticsAugmentTrigger.OnChosen &&
                        effect.value > 0 && effect.secondaryValue > 0 && effect.intervalSeconds > 0f;
                case MonsterTacticsAugmentEffectKind.SheerForce:
                    return effect.trigger == MonsterTacticsAugmentTrigger.OnChosen &&
                        effect.value > 0 && effect.secondaryValue > 0 && effect.tertiaryValue >= 0;
                case MonsterTacticsAugmentEffectKind.EvolvingTeam:
                    return effect.trigger == MonsterTacticsAugmentTrigger.UnitEvolved &&
                        effect.value > 0 && effect.secondaryValue > 0;
                case MonsterTacticsAugmentEffectKind.SafariZoneExplorer:
                    return effect.trigger == MonsterTacticsAugmentTrigger.PlanningStarted &&
                        effect.value > 0 && effect.secondaryValue > 0;
                case MonsterTacticsAugmentEffectKind.NuzlockeDiscipline:
                    return effect.trigger == MonsterTacticsAugmentTrigger.OnChosen &&
                        effect.value > 0 && effect.secondaryValue > 0;
                case MonsterTacticsAugmentEffectKind.TrainersAllowance:
                    return effect.trigger == MonsterTacticsAugmentTrigger.OnChosen && effect.value > 0;
                case MonsterTacticsAugmentEffectKind.DaycareBreedMaster:
                    return effect.trigger == MonsterTacticsAugmentTrigger.RoundResolved &&
                        effect.value > 0 && effect.roundInterval > 0;
                case MonsterTacticsAugmentEffectKind.ChampionsStreak:
                    return effect.trigger == MonsterTacticsAugmentTrigger.RoundResolved && effect.value > 0;
                case MonsterTacticsAugmentEffectKind.MonsterMartVIP:
                    return effect.trigger == MonsterTacticsAugmentTrigger.OnChosen &&
                        effect.value == 1 && effect.secondaryValue > 0;
                case MonsterTacticsAugmentEffectKind.CatchEmAll:
                    return effect.trigger == MonsterTacticsAugmentTrigger.OnChosen &&
                        effect.value > 0 && effect.secondaryValue > 0;
                case MonsterTacticsAugmentEffectKind.DaycareEvolutionMastery:
                    return effect.trigger == MonsterTacticsAugmentTrigger.UnitEvolved &&
                        effect.value > 0 && effect.secondaryValue > 0;
                case MonsterTacticsAugmentEffectKind.WeaknessPolicy:
                case MonsterTacticsAugmentEffectKind.MonsterCenterVisit:
                case MonsterTacticsAugmentEffectKind.TrainingGrounds:
                case MonsterTacticsAugmentEffectKind.TypeResearch:
                case MonsterTacticsAugmentEffectKind.SecondWind:
                case MonsterTacticsAugmentEffectKind.TechnicalMachine:
                case MonsterTacticsAugmentEffectKind.FinalFormFury:
                case MonsterTacticsAugmentEffectKind.RainbowLeague:
                    return effect.trigger == MonsterTacticsAugmentTrigger.OnChosen && effect.value > 0;
                case MonsterTacticsAugmentEffectKind.RareCandyCache:
                    return effect.trigger == MonsterTacticsAugmentTrigger.OnChosen &&
                        effect.value > 0 && effect.secondaryValue > 0 && effect.roundInterval > 0;
                case MonsterTacticsAugmentEffectKind.LegendaryGift:
                    return effect.trigger == MonsterTacticsAugmentTrigger.OnChosen && effect.value == 1;
                case MonsterTacticsAugmentEffectKind.EliteFourSponsorship:
                    return effect.trigger == MonsterTacticsAugmentTrigger.OnChosen &&
                        effect.roundInterval > 0 && effect.value > 0;
                default:
                    return false;
            }
        }
    }

    [Serializable]
    public sealed class MonsterTacticsAugmentStageVariant
    {
        public MonsterTacticsAugmentSlots slot = MonsterTacticsAugmentSlots.Second;
        public string descriptionOverride = "";
        public int effectValueBonus;
        // Can scale a reward up/down without duplicating the augment definition.
    }

    [CreateAssetMenu(fileName = "Augment_New", menuName = "MonsterTactics/Augments/Individual Augment")]
    public sealed class MonsterTacticsAugmentDefinition : ScriptableObject
    {
        [Header("Stable Identity / Display")]
        public string id = "new_augment";
        public string displayName = "New Augment";
        [TextArea(2, 6)] public string description = "Describe the benefit here.";
        [Tooltip("Main logo on selection cards and the active augment HUD. PNGs must import as Sprite (2D and UI).")]
        public Sprite icon;
        [Header("Visuals (optional — missing artwork uses a built-in fallback)")]
        [Tooltip("Optional faded art behind the logo on the selection card.")]
        public Sprite cardArtwork;
        [Tooltip("Optional small category emblem. If empty, the category initial is used.")]
        public Sprite categoryBadge;
        public bool useCustomAccent;
        public Color accentColor = new Color(0.40f, 0.83f, 0.94f, 1f);
        [Range(0f, 0.65f)] public float artworkOpacity = 0.24f;
        public bool showGlow = true;
        public MonsterTacticsAugmentTier tier = MonsterTacticsAugmentTier.Rookie;
        public MonsterTacticsAugmentCategory category = MonsterTacticsAugmentCategory.Economy;

        [Header("Offering / Balance")]
        public bool enabledInPool = false;
        [Range(0, 1000)] public int offerWeight = 100;
        public MonsterTacticsAugmentSlots allowedSelections = MonsterTacticsAugmentSlots.All;
        [Min(1)] public int minPlayerLevel = 1;
        [Min(0)] public int minGold;
        public bool allowRepeatInMatch = false;
        public string exclusiveGroup = "";
        public List<string> conflictIds = new List<string>();
        public List<string> requiredAugmentIds = new List<string>();
        public List<MonsterTacticsAugmentRequirement> requirements = new List<MonsterTacticsAugmentRequirement>();
        [Header("Optional tailoring (not hard requirements)")]
        public string preferredTraitKey = "";
        public string preferredFamilyKey = "";
        [Min(0)] public int preferredBonusWeight = 0;
        // 0 = no change. +1 makes next offering stronger, -1 weaker.
        [Range(-2, 2)] public int nextSelectionTierAdjustment = 0;

        [Header("Effects / Stage variants")]
        public List<MonsterTacticsAugmentEffect> effects = new List<MonsterTacticsAugmentEffect>();
        public List<MonsterTacticsAugmentStageVariant> stageVariants = new List<MonsterTacticsAugmentStageVariant>();

        public static bool HasSlot(MonsterTacticsAugmentSlots mask, MonsterTacticsAugmentSlots slot) =>
            slot != MonsterTacticsAugmentSlots.None && (mask & slot) == slot;
    }

}
