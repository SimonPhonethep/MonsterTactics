using System;
using System.Collections.Generic;
using UnityEngine;

namespace MonsterTactics
{
    /// <summary>
    /// U6.9.1f.1: Opt-in live Monster roster (shop, costs, base stats, evolutions).
    /// Traits, ability execution, and VFX still use their original handlers.
    /// </summary>
    [CreateAssetMenu(menuName = "MonsterTactics/Content/Catalog", fileName = "MonsterTacticsContentCatalog")]
    public sealed class MonsterTacticsContentCatalog : ScriptableObject
    {
        public const int CurrentSchema = 1;
        [SerializeField, HideInInspector] private int schemaVersion = CurrentSchema;
        [Tooltip("Checked = LEGACY ROSTER (safe default). Unchecked = CONTENT CATALOG for NEW matches. Never change during Play Mode.")]
        public bool readOnlyLegacyRuntime = true;
        [Tooltip("Opt-in live trait bonuses and breakpoints. OFF keeps U6.6 legacy traits. New Play Mode session required.")]
        public bool enableLiveTraits = false;
        [Tooltip("Opt-in: execute authored ability effects in NEW Play Mode matches. Legacy abilities remain unchanged unless their Use Effect Blocks flag is checked.")]
        public bool enableLiveAbilities = false;
        public List<MonsterTacticsMonsterRecord> monster = new List<MonsterTacticsMonsterRecord>();
        public List<MonsterTacticsTraitRecord> traits = new List<MonsterTacticsTraitRecord>();
        public List<MonsterTacticsAbilityRecord> abilities = new List<MonsterTacticsAbilityRecord>();
        public int SchemaVersion { get { return schemaVersion; } }
    }

    [Serializable]
    public sealed class MonsterTacticsMonsterFormRecord
    {
        public string name;
        [Tooltip("Elemental typings separated with /; e.g. Fire / Flying")]
        public string types;
        public int bestiaryNumber;
        [Tooltip("Optional future artwork override; legacy sprite loading remains active.")]
        public Sprite sprite;
    }

    [Serializable]
    public sealed class MonsterTacticsMonsterStatRecord
    {
        public float attack;
        public float attackSpeed;
        public float abilityPower = 100f;
        public float omnivamp;
        public float criticalChance = .10f;
        public float criticalMultiplier = 1.5f;
        public int armor;
        public int magicResist;
        public int attackRange = 1;
        public int manaCost = 75;
        public float moveInterval = .55f;
    }

    [Serializable]
    public sealed class MonsterTacticsMonsterRecord
    {
        public string id;
        public bool enabled = true;
        public string displayName;
        public int shopCost = 1;
        public string combatRole = "Fighter";
        public int baseHp = 500;
        public List<MonsterTacticsMonsterFormRecord> forms = new List<MonsterTacticsMonsterFormRecord>();
        [Tooltip("Origin traits only. Elemental types are defined on each evolution form.")]
        public List<string> origins = new List<string>();
        public string abilityId;
        public MonsterTacticsMonsterStatRecord stats = new MonsterTacticsMonsterStatRecord();
        [TextArea(1, 3)] public string designerNotes;
    }

    public enum MonsterTacticsContentEffectStat
    {
        AttackMultiplier, AttackSpeedMultiplier, AbilityPowerFlat,
        MaxHpMultiplier, DamageMultiplier, IncomingDamageMultiplier,
        ManaGainMultiplier, CritChanceFlat, ArmorFlat, MagicResistFlat
    }

    [Serializable]
    public sealed class MonsterTacticsContentEffectRecord
    {
        public MonsterTacticsContentEffectStat stat;
        [Tooltip("Multipliers stored as DELTAS: 0.12 = +12%; -0.20 = -20%. Flat bonuses use raw values.")]
        public float value;
        [Tooltip("Not executable in U6.9.1f.0; registered effect handlers arrive in later patches.")]
        public string vfxPresetId;
    }

    [Serializable]
    public sealed class MonsterTacticsTraitTierRecord
    {
        public int uniqueFamiliesRequired = 2;
        public string description;
        public List<MonsterTacticsContentEffectRecord> effects = new List<MonsterTacticsContentEffectRecord>();
    }

    [Serializable]
    public sealed class MonsterTacticsTraitRecord
    {
        public string id;
        public bool enabled = true;
        public string displayName;
        public bool isOrigin;
        [Tooltip("Bonuses apply to matching Monster only; deployed distinct families count once.")]
        public bool matchingUnitsOnly = true;
        public List<MonsterTacticsTraitTierRecord> tiers = new List<MonsterTacticsTraitTierRecord>();
        [TextArea(1, 3)] public string designerNotes;
    }

    public enum MonsterTacticsAbilityVfxPreset
    {
        Water, Fire, Grass, Electric, Rock, Dragon, Psychic, Ice, Dark, Healing
    }

    [Serializable]
    public sealed class MonsterTacticsAbilityScaling
    {
        [Tooltip("Flat raw amount before mitigation; e.g., 30.")]
        public float flat;
        [Tooltip("Attack coefficient: 1.75 = 175% of actual effective Attack.")]
        public float attack;
        [Tooltip("Ability Power coefficient: 2 = 200% of actual AP (AP=100 at baseline).")]
        public float abilityPower;
        public float Evaluate(float attackValue, float apValue)
        {
            return Mathf.Max(0f, flat + attack * attackValue + abilityPower * apValue);
        }
    }

    [Serializable]
    public sealed class MonsterTacticsAbilityRecord
    {
        public string id;
        public bool enabled = true;
        public string displayName;
        [TextArea(2, 4)] public string description;
        public string formula;
        public string damageKind = "Magic";
        [Tooltip("Display snapshot of the legacy ability evaluated using base stats. NOT a scaling formula.")]
        public float legacyBasePrimaryDamage;
        public float legacyBaseSplashDamage;
        public float legacyBaseHealing;
        public float legacyBaseShield;
        public int hitCount = 1;
        public int splashRadius;
        public int maximumSecondaryTargets;
        public float burnDuration;
        public float burnMaxHpPerSecond;
        public float woundDuration;
        [Tooltip("Existing combat handlers remain authoritative until the ability engine migration.")]
        public string legacyHandlerMonster;
        [Header("U6.9.1f.3 • Live Ability Engine")]
        [Tooltip("OFF = existing hardcoded ability; ON = actual gameplay uses the effect blocks below, only when Live Abilities is enabled.")]
        public bool useEffectBlocks = false;
        [Tooltip("Direct-hit formula. Each coefficient is a decimal: 2 = +200% ATK or AP.")]
        public MonsterTacticsAbilityScaling primary = new MonsterTacticsAbilityScaling();
        public MonsterTacticsAbilityScaling splash = new MonsterTacticsAbilityScaling();
        public MonsterTacticsAbilityScaling selfHeal = new MonsterTacticsAbilityScaling();
        public MonsterTacticsAbilityScaling selfShield = new MonsterTacticsAbilityScaling();
        [Tooltip("Built-in projectile/impact color; used for any custom ability name.")]
        public MonsterTacticsAbilityVfxPreset vfxPreset = MonsterTacticsAbilityVfxPreset.Water;
        [Tooltip("Optional authored Unity prefab spawned at caster at cast time and target on impact.")]
        public GameObject vfxPrefab;
        [Range(.2f, 4f)] public float vfxScale = 1f;
        [Range(.1f, 10f)] public float vfxLifetime = 1.2f;
        [TextArea(1, 3)] public string designerNotes;
    }
}
