using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MonsterTactics
{
    /// <summary>
    /// U4 standalone single-player combat layer. No modifications to the
    /// original U1/U2/U3 game scripts. Start/Reset buttons are runtime IMGUI.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed partial class MonsterTacticsCombatDirector : MonoBehaviour
    {
        private enum Phase { Planning, Battle, Results }

        // Exposed to U4.1 inspection without exposing private Fighter internals.
        public struct CombatSnapshot
        {
            public bool InCombat;
            public bool IsEnemy;
            public int Health;
            public int MaximumHealth;
            public int Mana;
            public int MaximumMana;
            public int Shield;
            public float ArmorReduction;
            public float MagicResistReduction;
            public bool Wounded;
            public float BurnRemaining;
            public float BurnDamagePerSecond;
            // Burn QA: real HP removed by completed one-second ticks (not splash,
            // normal attacks, or damage absorbed by shields).
            public int BurnTicksApplied;
            public int LastBurnHpDamage;
            public int TotalBurnHpDamage;
            // Effective in-combat defenses (not the unchanged roster values).
            public int EffectiveArmor;
            public int EffectiveMagicResist;
            public float SunderRemaining;
            public float ShredRemaining;
            public float WoundRemaining;
            public float AttackSpeedSlowRemaining;
            public float AttackSpeedSlowFraction;
            // U6.9.9b: temporary Shadow Thief offense theft (visible in real time).
            public float ShadowThiefDebuffRemaining;
            public float ShadowThiefDebuffAttackSpeed;
            public float ShadowThiefDebuffDamage;
            public float ShadowThiefBuffRemaining;
            public float ShadowThiefBuffAttackSpeed;
            public float ShadowThiefBuffDamage;
            public bool ShadowThiefSpent;
            // U6.9.9c: trait conversion is derived from bonus defenses ONLY.
            public bool MagmaCoreActive;
            public bool BurnImmune;
            public int MagmaCoreBaseArmor;
            public int MagmaCoreBaseMagicResist;
            public int MagmaCoreConvertedArmor;
            public int MagmaCoreConvertedMagicResist;
            public float MagmaCoreGainedAbilityPower;
            // U6.9.9d: live reusable protection buffs (not Vexokel-specific).
            public float TemporaryDefenseRemaining;
            public float TemporaryShieldRemaining;
            public int TemporaryArmorBonus;
            public int TemporaryMagicResistBonus;
            public int TemporaryShieldAmount;
            public float U610OverdriveRemaining;
            public float U610BarrageRemaining;
            public int U610BarrageDarts;
            public float U610BarrageDartDamage;
            public float U610SwiftyBonusFraction;
            public bool U610GritActivated;
            public bool IronFortressActive;
            public int IronFortressBasicHits;
            public int IronFortressHitsRequired;
            public float IronFortressRetaliationRaw;
            public MonsterTacticsCombatRules.Stats Stats;
        }

        public bool IsBattleActive => phase == Phase.Battle;
        public string MatchPhase => phase.ToString();
        public string LastResult => resultMessage;
        public bool HasDeployedTeam => ReadyToBattle();
        // Snapshot at Results, before cleanup; counts remaining fighters rather
        // than shop copies, defeated corpses, or Stage 1 preview units.
        public int SurvivingOpponentCount => CountAlive(opponents);
        public bool AutomaticRoundsEnabled { get; set; }
        public bool WasEmptyTeamForfeit { get; private set; }
        private Dictionary<string, int> lockedPlayerTraits;
        public Dictionary<string, int> PlayerTraitCounts => lockedPlayerTraits;
        public string CurrentOpponentName { get; private set; } = "";


        // U6.9.9e: keep independent resistance reductions per source.  The
        // strongest *currently active* Shred/Sunder wins. An expired 30% item
        // effect can never inherit the remaining lifetime of a 20% trait.
        // sourceKey is optional for old callers; traits may pass a stable key
        // such as "Hexcraft" when their combat handler is added in U6.10.
        public bool ApplySunder(MonsterUnit target, float fraction = .30f, float duration = 5f,
            string sourceKey = null)
        {
            Fighter fighter = FindCombatFighter(target);
            return AddResistanceEffect(fighter, false, fraction, duration, null,
                sourceKey ?? "API:" + Mathf.RoundToInt(fraction * 10000f));
        }

        public bool ApplyShred(MonsterUnit target, float fraction = .30f, float duration = 5f,
            string sourceKey = null)
        {
            Fighter fighter = FindCombatFighter(target);
            return AddResistanceEffect(fighter, true, fraction, duration, null,
                sourceKey ?? "API:" + Mathf.RoundToInt(fraction * 10000f));
        }

        private sealed class ResistanceEffect
        {
            public Fighter Source;
            public string Key;
            public float Fraction;
            public float Expires;
        }

        private bool AddResistanceEffect(Fighter target, bool shred, float fraction,
            float duration, Fighter source, string sourceKey)
        {
            if (target == null || !target.Alive || duration <= 0f) return false;
            float capped = MonsterTacticsCombatRules.CappedResistanceReduction(fraction);
            if (capped <= 0f) return false;
            List<ResistanceEffect> effects = shred ? target.ShredSources : target.SunderSources;
            // Clear expired sources to prevent long-term buildup in extended fights.
            effects.RemoveAll(e => e.Expires <= elapsed);
            foreach (ResistanceEffect effect in effects)
            {
                if (effect.Source != source || effect.Key != sourceKey) continue;
                effect.Fraction = capped;
                effect.Expires = Mathf.Max(effect.Expires, elapsed + duration);
                return true;
            }
            effects.Add(new ResistanceEffect {
                Source = source, Key = sourceKey, Fraction = capped, Expires = elapsed + duration
            });
            return true;
        }

        // The remaining timer belongs to the WINNING source, not the longest
        // source. It is used by damage mitigation, shields, HUD, and tooltips.
        private float ActiveResistanceReduction(Fighter fighter, bool shred, out float remaining)
        {
            remaining = 0f;
            if (fighter == null) return 0f;
            List<ResistanceEffect> effects = shred ? fighter.ShredSources : fighter.SunderSources;
            float strongest = 0f;
            foreach (ResistanceEffect effect in effects)
            {
                float left = effect.Expires - elapsed;
                if (left <= 0f) continue;
                if (effect.Fraction > strongest ||
                    (Mathf.Approximately(effect.Fraction, strongest) && left > remaining))
                {
                    strongest = effect.Fraction;
                    remaining = left;
                }
            }
            return strongest;
        }

        private float ActiveResistanceFraction(Fighter fighter, bool shred)
        {
            float remaining;
            return ActiveResistanceReduction(fighter, shred, out remaining);
        }

        private float ActiveResistanceRemaining(Fighter fighter, bool shred)
        {
            float remaining;
            ActiveResistanceReduction(fighter, shred, out remaining);
            return remaining;
        }

        /// <summary>
        /// U6.9.9 Ice Slow: lower a living combatant's actual basic-attack speed.
        /// Fraction .20 means -20% AS; strongest slow wins (max 50%),
        /// reapplications refresh duration, effects clear after combat.
        /// Ability scripts, traits, items and augments can all call this hook.
        /// </summary>
        public bool ApplyAttackSpeedSlow(MonsterUnit target, float fraction = .20f, float duration = 2f)
        {
            Fighter fighter = FindCombatFighter(target);
            if (fighter == null || !fighter.Alive || duration <= 0f || fraction <= 0f) return false;
            float newReduction = MonsterTacticsCombatRules.CappedAttackSpeedSlow(fraction);
            if (newReduction <= 0f) return false;
            float oldSpeed = EffectiveAttackSpeed(fighter);
            bool wasSlowed = fighter.AttackSpeedSlowUntil > elapsed;
            if (!wasSlowed) fighter.AttackSpeedSlow = 0f;
            fighter.AttackSpeedSlow = Mathf.Max(fighter.AttackSpeedSlow, newReduction);
            fighter.AttackSpeedSlowUntil = Mathf.Max(fighter.AttackSpeedSlowUntil, elapsed + duration);
            AdjustBasicAttackRecovery(fighter, oldSpeed, EffectiveAttackSpeed(fighter));
            if (!wasSlowed && fighter.Unit != null)
                AddEffect("SLOWED", fighter.Unit.transform.position + Vector3.up * 1.3f,
                    new Color(.48f, .84f, 1f));
            return true;
        }

        // Combat modifiers are evaluated rather than written permanently to Stats.
        // Independent Ice Slow and shadow theft cannot compound into a hard CC lock:
        // strongest Attack Speed reduction wins until its own timer expires.
        private static float CalculateModifiedAttackSpeed(Fighter fighter, float iceSlow,
            float shadowSlow, float shadowBuff)
        {
            return MonsterTacticsCombatRules.AttackSpeedAfterSlow(
                fighter.Stats.AttackSpeed * (1f + shadowBuff), Mathf.Max(iceSlow, shadowSlow));
        }

        private float EffectiveAttackSpeed(Fighter fighter)
        {
            return CalculateModifiedAttackSpeed(fighter,
                fighter.AttackSpeedSlowUntil > elapsed ? fighter.AttackSpeedSlow : 0f,
                fighter.ShadowThiefDebuffUntil > elapsed ? fighter.ShadowThiefDebuffAS : 0f,
                fighter.ShadowThiefBuffUntil > elapsed ? fighter.ShadowThiefBuffAS : 0f);
        }

        private float EffectiveDamageMultiplier(Fighter fighter)
        {
            if (fighter == null) return 1f;
            return MonsterTacticsCombatRules.ShadowThiefDamageMultiplier(
                fighter.Stats.DamageMultiplier,
                fighter.ShadowThiefBuffUntil > elapsed ? fighter.ShadowThiefBuffDamage : 0f,
                fighter.ShadowThiefDebuffUntil > elapsed ? fighter.ShadowThiefDebuffDamage : 0f);
        }

        /// <summary>
        /// U6.9.9b future Umbrelisk (1) origin hook. Invoke ONLY when its damaging
        /// ability successfully hits a living enemy. Consumes the caster's single
        /// theft per combat. Does not spawn, select or upgrade Umbrelisk.
        /// The strongest theft debuff wins on the victim, while its own bonus is
        /// granted only to the triggering caster. Values are fractions.
        /// </summary>
        public bool TryApplyShadowThief(MonsterUnit caster, MonsterUnit victim,
            float attackSpeedFraction = .12f, float damageFraction = .08f,
            float duration = 5f)
        {
            Fighter source = FindCombatFighter(caster);
            Fighter target = FindCombatFighter(victim);
            if (source == null || target == null || !source.Alive || !target.Alive ||
                source == target || source.Enemy == target.Enemy || source.ShadowThiefSpent ||
                attackSpeedFraction <= 0f || duration <= 0f) return false;
            float asAmount = Mathf.Clamp(attackSpeedFraction, 0f, .25f);
            float damageAmount = Mathf.Clamp(damageFraction, 0f, .20f);
            duration = Mathf.Clamp(duration, .1f, 8f);
            float oldSourceAS = EffectiveAttackSpeed(source);
            float oldTargetAS = EffectiveAttackSpeed(target);
            source.ShadowThiefSpent = true;
            source.ShadowThiefBuffAS = Mathf.Max(source.ShadowThiefBuffAS, asAmount);
            source.ShadowThiefBuffDamage = Mathf.Max(source.ShadowThiefBuffDamage, damageAmount);
            source.ShadowThiefBuffUntil = Mathf.Max(source.ShadowThiefBuffUntil, elapsed + duration);
            if (target.ShadowThiefDebuffUntil <= elapsed)
            {
                target.ShadowThiefDebuffAS = 0f;
                target.ShadowThiefDebuffDamage = 0f;
            }
            target.ShadowThiefDebuffAS = Mathf.Max(target.ShadowThiefDebuffAS, asAmount);
            target.ShadowThiefDebuffDamage = Mathf.Max(target.ShadowThiefDebuffDamage, damageAmount);
            target.ShadowThiefDebuffUntil = Mathf.Max(target.ShadowThiefDebuffUntil, elapsed + duration);
            AdjustBasicAttackRecovery(source, oldSourceAS, EffectiveAttackSpeed(source));
            AdjustBasicAttackRecovery(target, oldTargetAS, EffectiveAttackSpeed(target));
            if (caster != null)
                AddEffect("SHADOW THIEF +", caster.transform.position + Vector3.up * 1.4f,
                    new Color(.77f, .48f, 1f));
            if (victim != null)
                AddEffect("SHADOW THIEF -", victim.transform.position + Vector3.up * 1.4f,
                    new Color(.57f, .36f, .81f));
            return true;
        }

        private void TickShadowThief()
        {
            foreach (Fighter fighter in turnList)
            {
                bool buffExpired = fighter.ShadowThiefBuffUntil > 0f && fighter.ShadowThiefBuffUntil <= elapsed;
                bool debuffExpired = fighter.ShadowThiefDebuffUntil > 0f && fighter.ShadowThiefDebuffUntil <= elapsed;
                if (!buffExpired && !debuffExpired) continue;
                // Before expiration, include the old modifiers to rescale the
                // remainder of an ongoing basic attack's recovery timer.
                float oldAS = CalculateModifiedAttackSpeed(fighter,
                    fighter.AttackSpeedSlowUntil > elapsed ? fighter.AttackSpeedSlow : 0f,
                    fighter.ShadowThiefDebuffAS, fighter.ShadowThiefBuffAS);
                if (buffExpired)
                {
                    fighter.ShadowThiefBuffUntil = 0f;
                    fighter.ShadowThiefBuffAS = 0f;
                    fighter.ShadowThiefBuffDamage = 0f;
                }
                if (debuffExpired)
                {
                    fighter.ShadowThiefDebuffUntil = 0f;
                    fighter.ShadowThiefDebuffAS = 0f;
                    fighter.ShadowThiefDebuffDamage = 0f;
                }
                AdjustBasicAttackRecovery(fighter, oldAS, EffectiveAttackSpeed(fighter));
            }
        }

        private void AdjustBasicAttackRecovery(Fighter fighter, float oldSpeed, float newSpeed)
        {
            // Spell windup and walking are not basic attacks. Do not change them.
            if (!fighter.InBasicAttackRecovery || fighter.ActionReady <= elapsed) return;
            fighter.ActionReady = MonsterTacticsCombatRules.RescaledBasicAttackDeadline(
                elapsed, fighter.ActionReady, oldSpeed, newSpeed);
        }

        private void TickAttackSpeedSlows()
        {
            foreach (Fighter fighter in turnList)
            {
                if (fighter.AttackSpeedSlowUntil <= 0f || fighter.AttackSpeedSlowUntil > elapsed) continue;
                float oldSpeed = CalculateModifiedAttackSpeed(fighter,
                    fighter.AttackSpeedSlow,
                    fighter.ShadowThiefDebuffUntil > elapsed ? fighter.ShadowThiefDebuffAS : 0f,
                    fighter.ShadowThiefBuffUntil > elapsed ? fighter.ShadowThiefBuffAS : 0f);
                fighter.AttackSpeedSlow = 0f;
                fighter.AttackSpeedSlowUntil = 0f;
                AdjustBasicAttackRecovery(fighter, oldSpeed, EffectiveAttackSpeed(fighter));
            }
        }

        /// <summary>
        /// U6.9.9d: Give one living fighter a temporary Armor/MR bonus.
        /// Recasts keep the strongest bonus PER STAT and refresh the duration;
        /// they never add buffs together. Defense changes are reflected by
        /// mitigation and the live snapshot immediately, including Magma Core.
        /// </summary>
        public bool ApplyTemporaryDefenseBuff(MonsterUnit target, int armorBonus = 10,
            int magicResistBonus = 10, float duration = 4f)
        {
            Fighter fighter = FindCombatFighter(target);
            if (fighter == null || !fighter.Alive || duration <= 0f) return false;
            if (fighter.TemporaryDefenseUntil > 0f &&
                fighter.TemporaryDefenseUntil <= elapsed)
                RemoveTemporaryDefenseBuff(fighter);
            int a = Mathf.Clamp(armorBonus, 0, 500);
            int mr = Mathf.Clamp(magicResistBonus, 0, 500);
            if (a == 0 && mr == 0) return false;
            int nextA = Mathf.Max(fighter.TemporaryDefenseArmor, a);
            int nextMR = Mathf.Max(fighter.TemporaryDefenseMR, mr);
            fighter.Stats.Armor += nextA - fighter.TemporaryDefenseArmor;
            fighter.Stats.MagicResist += nextMR - fighter.TemporaryDefenseMR;
            fighter.TemporaryDefenseArmor = nextA;
            fighter.TemporaryDefenseMR = nextMR;
            fighter.TemporaryDefenseUntil = Mathf.Max(fighter.TemporaryDefenseUntil, elapsed + duration);
            if (fighter.MagmaCoreEnabled) RecalculateMagmaCore(fighter);
            if (fighter.Unit != null)
                AddEffect("FOREST GUARD", fighter.Unit.transform.position + Vector3.up * 1.55f,
                    new Color(.53f, .94f, .54f));
            return true;
        }

        /// <summary>Refreshable shield that is independent from item/augment shields.</summary>
        public bool ApplyTemporaryShield(MonsterUnit target, float percentMaxHp = .16f,
            float duration = 4f)
        {
            Fighter fighter = FindCombatFighter(target);
            if (fighter == null || !fighter.Alive || percentMaxHp <= 0f || duration <= 0f)
                return false;
            if (fighter.TemporaryShieldUntil > 0f && fighter.TemporaryShieldUntil <= elapsed)
                RemoveTemporaryShield(fighter);
            int desired = Mathf.Clamp(Mathf.RoundToInt(fighter.MaxHP *
                Mathf.Clamp01(percentMaxHp)), 1, fighter.MaxHP);
            int newShield = Mathf.Min(desired, Mathf.Max(0, fighter.MaxHP -
                (fighter.Shield - fighter.TemporaryDefenseShield)));
            int difference = Mathf.Max(0, newShield - fighter.TemporaryDefenseShield);
            fighter.Shield += difference;
            fighter.TemporaryDefenseShield += difference;
            U6115TrackShield(fighter, difference);
            fighter.TemporaryShieldUntil = Mathf.Max(fighter.TemporaryShieldUntil,
                elapsed + duration);
            if (fighter.Unit != null)
                AddEffect("FOREST SHIELD", fighter.Unit.transform.position + Vector3.up * 1.55f,
                    new Color(.42f, .92f, .64f));
            return true;
        }

        /// <summary>
        /// Future Vexokel Forest Shelter or any similar supportive ability:
        /// a self-shield plus defensive buffs on the N nearest LIVING allies.
        /// Excludes caster. Range is unrestricted and uses true hex distance,
        /// not GameObject sibling order or 3D position.
        /// </summary>
        public bool GrantNearestAllyProtection(MonsterUnit caster, float selfShieldPercent,
            int allyArmor, int allyMagicResist, float duration = 4f, int allyCount = 2)
        {
            Fighter source = FindCombatFighter(caster);
            if (source == null || !source.Alive || source.Tile == null || duration <= 0f)
                return false;
            bool applied = ApplyTemporaryShield(caster, selfShieldPercent, duration);
            List<Fighter> team = source.Enemy ? opponents : players;
            var nearby = team.Where(other => other != source && other.Alive && other.Tile != null)
                .OrderBy(other => MonsterTacticsCombatRules.HexDistance(
                    MonsterTacticsCombatRules.Coord(source.Tile),
                    MonsterTacticsCombatRules.Coord(other.Tile)))
                .ThenBy(other => team.IndexOf(other))
                .Take(Mathf.Clamp(allyCount, 0, 8));
            foreach (Fighter ally in nearby)
            {
                bool buffed = ApplyTemporaryDefenseBuff(ally.Unit, allyArmor, allyMagicResist, duration);
                if (buffed && abilityVfx != null)
                    abilityVfx.PlayU610ProtectionLink(caster, ally.Unit);
                applied |= buffed;
            }
            return applied;
        }

        private static void RemoveTemporaryDefenseBuff(Fighter fighter)
        {
            if (fighter.TemporaryDefenseArmor != 0 || fighter.TemporaryDefenseMR != 0)
            {
                fighter.Stats.Armor -= fighter.TemporaryDefenseArmor;
                fighter.Stats.MagicResist -= fighter.TemporaryDefenseMR;
                fighter.TemporaryDefenseArmor = 0;
                fighter.TemporaryDefenseMR = 0;
                if (fighter.MagmaCoreEnabled) RecalculateMagmaCore(fighter);
            }
            fighter.TemporaryDefenseUntil = 0f;
        }

        private static void RemoveTemporaryShield(Fighter fighter)
        {
            fighter.Shield = Mathf.Max(0, fighter.Shield - fighter.TemporaryDefenseShield);
            fighter.TemporaryDefenseShield = 0;
            fighter.TemporaryShieldUntil = 0f;
        }

        private void TickTemporaryProtection()
        {
            foreach (Fighter fighter in turnList)
            {
                if (fighter.TemporaryDefenseUntil > 0f &&
                    elapsed >= fighter.TemporaryDefenseUntil)
                    RemoveTemporaryDefenseBuff(fighter);
                if (fighter.TemporaryShieldUntil > 0f &&
                    elapsed >= fighter.TemporaryShieldUntil)
                    RemoveTemporaryShield(fighter);
            }
        }

        /// <summary>
        /// U6.9.9a future Iron Fortress (1) trait hook. The real trait runtime can
        /// activate this after constructing its fighter, and the same hook can be
        /// called by the Editor tester. Does not automatically create Evodrel units.
        /// Apply once per fighter per combat; never stack the defensive bonuses.
        /// </summary>
        public bool EnableIronFortress(MonsterUnit holder, int armorBonus = 15,
            int magicResistBonus = 15, int incomingBasicsPerCounter = 4,
            float effectiveArmorDamageRatio = .60f)
        {
            Fighter fighter = FindCombatFighter(holder);
            if (fighter == null || !fighter.Alive || fighter.IronFortressEnabled)
                return false;
            fighter.IronFortressEnabled = true;
            fighter.IronFortressHitCounter = 0;
            fighter.IronFortressHitThreshold = Mathf.Max(1, incomingBasicsPerCounter);
            fighter.IronFortressArmorRatio = Mathf.Clamp(effectiveArmorDamageRatio, 0f, 3f);
            fighter.IronFortressArmorBonus = Mathf.Max(0, armorBonus);
            fighter.IronFortressMagicResistBonus = Mathf.Max(0, magicResistBonus);
            fighter.Stats.Armor += fighter.IronFortressArmorBonus;
            fighter.Stats.MagicResist += fighter.IronFortressMagicResistBonus;
            if (fighter.Unit != null)
                AddEffect("IRON FORTRESS", fighter.Unit.transform.position + Vector3.up * 1.55f,
                    new Color(.70f, .88f, 1f));
            return true;
        }

        // Called exactly once for the real primary basic attack. Not called
        // by DealDamage (which also processes spells, burns, item procs etc.).
        // Shields count as receiving an attack; KO'd targets cannot retaliate.
        private void RegisterIronFortressBasicHit(Fighter defender, Fighter attacker)
        {
            if (defender == null || attacker == null || !defender.IronFortressEnabled ||
                !defender.Alive || !attacker.Alive || defender.Enemy == attacker.Enemy)
                return;
            bool triggers = MonsterTacticsCombatRules.IronFortressTriggers(
                defender.IronFortressHitCounter, defender.IronFortressHitThreshold);
            defender.IronFortressHitCounter = MonsterTacticsCombatRules.NextIronFortressCounter(
                defender.IronFortressHitCounter, defender.IronFortressHitThreshold);
            if (!triggers) return;
            // This is the same effective Armor the live inspector displays.
            // Therefore item / trait Armor increases it and Sunder lowers it.
            int currentArmor = MonsterTacticsCombatRules.EffectiveResistance(defender.Stats.Armor,
                ActiveResistanceFraction(defender, false));
            float raw = MonsterTacticsCombatRules.IronFortressRawMagicDamage(
                currentArmor, defender.IronFortressArmorRatio);
            if (defender.Unit != null)
                AddEffect("IRON COUNTER", defender.Unit.transform.position + Vector3.up * 1.45f,
                    new Color(.64f, .78f, 1f));
            if (raw <= 0f) return;
            if (abilityVfx != null && defender.Unit != null && attacker.Unit != null)
                abilityVfx.PlayU610IronFortress(defender.Unit, attacker.Unit);
            // Null attacker deliberately prevents recursive on-hit loops,
            // Omnivamp and unrelated offensive proc effects. This is Magic
            // Damage, so the receiving attacker's MR mitigates it normally.
            if (defender.Unit != null && attacker.Unit != null)
                beams.Add(new BeamEffect
                {
                    Start = defender.Unit.transform.position + Vector3.up * .90f,
                    End = attacker.Unit.transform.position + Vector3.up * .90f,
                    Color = new Color(.70f, .82f, 1f), Born = elapsed
                });
            U6115ReflectionOwner = defender; // Statistics-only attribution.
            DealDamage(attacker, raw, true, null, "IRON FORTRESS");
            U6115ReflectionOwner = null;
        }

        /// <summary>
        /// U6.9.9c: optional Magma Core (1) hook for future Astramora.
        /// Once per combat, convert 40% of bonus Armor + MR to AP (max +80 AP)
        /// while retaining all base defenses. Conversion costs actual bonus
        /// defenses: it is not a free AP grant. Burn is fully blocked.
        /// Does not add Astramora to the roster or enable the trait automatically.
        /// </summary>
        public bool EnableMagmaCore(MonsterUnit holder, float conversionFraction = .40f,
            float maximumBonusAP = 80f)
        {
            Fighter fighter = FindCombatFighter(holder);
            if (phase != Phase.Battle || fighter == null || !fighter.Alive ||
                fighter.MagmaCoreEnabled || holder == null) return false;
            var original = MonsterTacticsU5Roster.CombatStats(holder);
            fighter.MagmaCoreBaseArmor = Mathf.Max(0, original.Armor);
            fighter.MagmaCoreBaseMR = Mathf.Max(0, original.MagicResist);
            fighter.MagmaCoreFraction = Mathf.Clamp01(conversionFraction);
            fighter.MagmaCoreAPCap = Mathf.Max(0f, maximumBonusAP);
            fighter.MagmaCoreEnabled = true;
            // Remove Burn even if immunity was enabled midway through a battle.
            fighter.BurnPercent = 0f;
            fighter.BurnUntil = 0f;
            fighter.NextBurnTick = 0f;
            RecalculateMagmaCore(fighter);
            if (fighter.Unit != null)
                AddEffect("MAGMA CORE", holder.transform.position + Vector3.up * 1.5f,
                    new Color(1f, .48f, .19f));
            return true;
        }

        private static void RecalculateMagmaCore(Fighter fighter)
        {
            if (fighter == null || !fighter.MagmaCoreEnabled) return;
            var stats = fighter.Stats;
            // Unwind our LAST conversion before applying the next one. This
            // avoids endlessly recycling converted AP back into the source.
            stats.Armor += fighter.MagmaCoreConvertedArmor;
            stats.MagicResist += fighter.MagmaCoreConvertedMR;
            stats.AbilityPower -= fighter.MagmaCoreConvertedAP;
            MonsterTacticsCombatRules.ComputeMagmaCoreConversion(
                stats.Armor, stats.MagicResist,
                fighter.MagmaCoreBaseArmor, fighter.MagmaCoreBaseMR,
                fighter.MagmaCoreFraction, fighter.MagmaCoreAPCap,
                out int armorSpent, out int mrSpent, out float apGained);
            stats.Armor -= armorSpent;
            stats.MagicResist -= mrSpent;
            stats.AbilityPower += apGained;
            fighter.MagmaCoreConvertedArmor = armorSpent;
            fighter.MagmaCoreConvertedMR = mrSpent;
            fighter.MagmaCoreConvertedAP = apGained;
            fighter.Stats = stats;
        }

        public bool ApplyBurn(MonsterUnit target, float percentMaxHpPerSecond = .01f, float duration = 5f)
        {
            Fighter fighter = FindCombatFighter(target);
            if (fighter == null || !fighter.Alive || duration <= 0f ||
                fighter.MagmaCoreEnabled) return false;
            if (fighter.BurnUntil <= elapsed) fighter.BurnPercent = 0f;
            fighter.BurnPercent = Mathf.Max(fighter.BurnPercent, Mathf.Max(0f, percentMaxHpPerSecond));
            fighter.U6115BurnOwner = null; // Public API does not specify an attacker.
            fighter.BurnUntil = Mathf.Max(fighter.BurnUntil, elapsed + duration);
            fighter.NextBurnTick = elapsed + 1f;
            return true;
        }

        public bool ApplyWound(MonsterUnit target, float duration = 5f)
        {
            Fighter fighter = FindCombatFighter(target);
            if (fighter == null || !fighter.Alive || duration <= 0f) return false;
            fighter.WoundUntil = Mathf.Max(fighter.WoundUntil, elapsed + duration);
            return true;
        }

#if UNITY_EDITOR
        /// <summary>
        /// U6.6.2 QA only: trigger the exact Sacred Fire ability pipeline (windup,
        /// damage, radius-1 splash, Burn and Wound) without waiting for mana.
        /// The caster must be an active Pyrothane and the target an adjacent enemy.
        /// This is not compiled into player builds.
        /// </summary>
        public bool DebugCastPyrothaneSacredFire(MonsterUnit casterUnit, MonsterUnit targetUnit)
        {
            Fighter caster = FindCombatFighter(casterUnit);
            Fighter victim = FindCombatFighter(targetUnit);
            if (caster == null || victim == null || !caster.Alive || !victim.Alive ||
                caster.Enemy == victim.Enemy ||
                !string.Equals(MonsterTacticsU5Roster.BaseName(caster.Name), "Pyrothane",
                    StringComparison.OrdinalIgnoreCase) ||
                caster.Tile == null || victim.Tile == null ||
                MonsterTacticsCombatRules.HexDistance(MonsterTacticsCombatRules.Coord(caster.Tile),
                    MonsterTacticsCombatRules.Coord(victim.Tile)) > caster.Stats.Range)
                return false;

            CastAbility(caster, victim);
            return true;
        }
#endif

        private Fighter FindCombatFighter(MonsterUnit target)
        {
            if (phase != Phase.Battle || target == null) return null;
            foreach (Fighter fighter in turnList)
                if (fighter.Unit == target) return fighter;
            return null;
        }

        /// <summary>
        /// Apply a newly equipped item to an active player fighter without starting
        /// a new battle, resetting HP, wiping traits, or rerolling existing passives.
        /// Previous items are supplied by the inventory transaction; the unit's
        /// MonsterTacticsHeldItems component already contains its new loadout.
        /// </summary>
        public void NotifyOwnUnitItemEquipped(MonsterUnit unit, MonsterTacticsItemKind[] previouslyEquipped)
        {
            if (phase != Phase.Battle || unit == null) return;
            Fighter fighter = FindCombatFighter(unit);
            if (fighter == null || fighter.Enemy || !fighter.Alive) return;
            MonsterTacticsHeldItems held = unit.GetComponent<MonsterTacticsHeldItems>();
            if (held == null) return;

            MonsterTacticsItemBonus oldBonus = new MonsterTacticsItemBonus();
            bool[] oldPassives = new bool[36];
            if (previouslyEquipped != null)
            {
                foreach (MonsterTacticsItemKind item in previouslyEquipped)
                {
                    oldBonus.Add(MonsterTacticsItemTypes.Bonus(item));
                    int index = (int)item - (int)MonsterTacticsItemKind.Craft00;
                    if (index >= 0 && index < oldPassives.Length) oldPassives[index] = true;
                }
            }
            MonsterTacticsItemBonus newBonus = held.CombinedBonus;
            // A component or finished item equipped DURING battle removes the
            // unitemized bonus immediately; never keep free Sheer Force stats.
            if (held.Items.Count > 0 && fighter.SheerForceAttackFactor > 1f)
            {
                var beforeItem = fighter.Stats;
                beforeItem.Attack /= Mathf.Max(0.01f, fighter.SheerForceAttackFactor);
                beforeItem.AbilityPower -= fighter.SheerForceApBonus;
                fighter.Stats = beforeItem;
                fighter.MaxHP = Mathf.Max(1, fighter.MaxHP - fighter.SheerForceHpBonus);
                fighter.HP = Mathf.Min(fighter.HP, fighter.MaxHP);
                fighter.SheerForceAttackFactor = 1f;
                fighter.SheerForceApBonus = fighter.SheerForceHpBonus = 0;
            }
            var stats = fighter.Stats;
            stats.Attack += newBonus.Attack - oldBonus.Attack;
            stats.AbilityPower += newBonus.AbilityPower - oldBonus.AbilityPower;
            stats.Armor += newBonus.Armor - oldBonus.Armor;
            stats.MagicResist += newBonus.MagicResist - oldBonus.MagicResist;
            stats.AttackSpeed *= (1f + newBonus.AttackSpeed) /
                Mathf.Max(.01f, 1f + oldBonus.AttackSpeed);
            stats.CriticalChance = Mathf.Clamp01(stats.CriticalChance +
                newBonus.CriticalChance - oldBonus.CriticalChance);
            stats.Omnivamp = Mathf.Clamp01(stats.Omnivamp +
                newBonus.Omnivamp - oldBonus.Omnivamp);
            stats.DamageMultiplier *= (1f + newBonus.DamageAmplification) /
                Mathf.Max(.01f, 1f + oldBonus.DamageAmplification);
            stats.IncomingDamageMultiplier *=
                (1f - Mathf.Clamp01(newBonus.DamageReduction)) /
                Mathf.Max(.01f, 1f - Mathf.Clamp01(oldBonus.DamageReduction));
            fighter.Stats = stats;
            RecalculateMagmaCore(fighter);

            int oldMaximum = fighter.MaxHP;
            int hpDelta = newBonus.MaximumHealth - oldBonus.MaximumHealth;
            int newMaximum = Mathf.Max(1, oldMaximum + hpDelta);
            bool hasGiantHeart = held.Items.Contains(MonsterTacticsItemKind.Craft26);
            if (hasGiantHeart && !oldPassives[26])
                newMaximum = Mathf.Max(1, Mathf.RoundToInt(newMaximum * 1.10f));
            fighter.MaxHP = newMaximum;
            fighter.HP = Mathf.Clamp(fighter.HP + Mathf.Max(0, newMaximum - oldMaximum),
                0, fighter.MaxHP);
            fighter.Mana = Mathf.Min(fighter.Stats.ManaCost, fighter.Mana +
                Mathf.Max(0, newBonus.StartingMana - oldBonus.StartingMana));

            foreach (MonsterTacticsItemKind kind in held.Items)
            {
                int id = (int)kind - (int)MonsterTacticsItemKind.Craft00;
                if (id < 0 || id >= fighter.ItemPassive.Length || fighter.ItemPassive[id]) continue;
                fighter.ItemPassive[id] = true;
                // Unique effects activate once; previous stacks/timers remain.
                if (id == 4) fighter.Stats.Omnivamp =
                    Mathf.Clamp01(fighter.Stats.Omnivamp + .12f);
                if (id == 28)
                {
                    int oldShield = fighter.Shield;
                    GrantItemShield(fighter, fighter.MaxHP * .16f, "PROTECTED GROWTH");
                    fighter.TimedItemShield = Mathf.Max(0, fighter.Shield - oldShield);
                    fighter.TimedItemShieldUntil = elapsed + 6f;
                }
                if (id == 32)
                {
                    if (UnityEngine.Random.value < .5f) fighter.Stats.DamageMultiplier *= 1.12f;
                    else fighter.Stats.Omnivamp = Mathf.Clamp01(fighter.Stats.Omnivamp + .12f);
                }
                if (id == 8) fighter.ItemTimers[8] = elapsed + 5f;
                if (id == 22) fighter.ItemTimers[22] = elapsed + 4f;
                if (id == 27) fighter.ItemTimers[27] = elapsed + 7f;
            }
            fighter.Unit.SetTemporaryCombatMaximumHP(fighter.MaxHP);
            AddEffect("ITEM EQUIPPED", unit.transform.position + Vector3.up * 1.28f,
                new Color(.52f, .84f, 1f));
        }

        public bool TryGetSnapshot(MonsterUnit unit, out CombatSnapshot snapshot)
        {
            snapshot = new CombatSnapshot();
            if (unit == null) return false;
            snapshot.Stats = MonsterTacticsU5Roster.CombatStats(unit);
            MonsterTacticsHeldItems heldPreview = unit.GetComponent<MonsterTacticsHeldItems>();
            MonsterTacticsItemBonus previewBonus = heldPreview != null ? heldPreview.CombinedBonus : new MonsterTacticsItemBonus();
            snapshot.Stats = MonsterTacticsItemTypes.ApplyStats(snapshot.Stats, previewBonus);
            snapshot.EffectiveArmor = snapshot.Stats.Armor;
            snapshot.EffectiveMagicResist = snapshot.Stats.MagicResist;
            snapshot.Health = unit.CurrentHP + previewBonus.MaximumHealth;
            snapshot.MaximumHealth = unit.MaximumHP + previewBonus.MaximumHealth;
            if (heldPreview != null && heldPreview.Items.Contains(MonsterTacticsItemKind.Craft26))
            {
                snapshot.Health = Mathf.RoundToInt(snapshot.Health * 1.10f);
                snapshot.MaximumHealth = Mathf.RoundToInt(snapshot.MaximumHealth * 1.10f);
            }
            snapshot.MaximumMana = snapshot.Stats.ManaCost;
            snapshot.Mana = previewBonus.StartingMana;
            snapshot.Shield = 0;
            // U6.6.1: TFT-style planning stats. Deployment updates the effective
            // tooltip immediately; returning a unit to the bench shows base stats.
            // Only our own active, placed units can receive our team's bonuses.
            // This does not modify base stats, HP or the combat lock-in snapshot.
            if (phase == Phase.Planning && unit.OccupiedTile != null &&
                unit.gameObject.activeInHierarchy)
            {
                if (placementController == null)
                    placementController = FindAnyObjectByType<MonsterPlacementController>();
                if (placementController != null && unit.transform.IsChildOf(placementController.transform))
                {
                    Dictionary<string, int> liveCounts = MonsterTacticsTraits.CountDeployed(
                        placementController.GetComponentsInChildren<MonsterUnit>(true));
                    MonsterTacticsTraits.Bonus bonus = MonsterTacticsTraits.ForUnit(unit, liveCounts);
                    snapshot.Stats = MonsterTacticsTraits.ApplyToStats(snapshot.Stats, bonus);
                    // U6.10 planning preview includes the predictable portion of
                    // Swifty/Bruiser. Distance AS and threshold buffs activate in battle.
                    if (MonsterTacticsTraits.ActiveTier("Swifty",liveCounts)>0 &&
                        Array.Exists(MonsterTacticsTraits.ForUnit(unit),n=>n=="Swifty"))
                        snapshot.Stats.AttackSpeed*=1.10f;
                    snapshot.EffectiveArmor = snapshot.Stats.Armor;
                    snapshot.EffectiveMagicResist = snapshot.Stats.MagicResist;
                    int bonusHP = Mathf.Max(1, Mathf.RoundToInt(unit.MaximumHP * bonus.HpMultiplier)) + previewBonus.MaximumHealth;
                    if (heldPreview != null && heldPreview.Items.Contains(MonsterTacticsItemKind.Craft26))
                        bonusHP = Mathf.RoundToInt(bonusHP * 1.10f);
                    int bruiserTier=MonsterTacticsTraits.ActiveTier("Bruiser",liveCounts);
                    if(bruiserTier>0)
                    {
                        bool isBruiser=Array.Exists(MonsterTacticsTraits.ForUnit(unit),n=>n=="Bruiser");
                        bonusHP+=bruiserTier>=2?150:75;
                        if(isBruiser)bonusHP+=Mathf.RoundToInt(snapshot.MaximumHealth*(bruiserTier>=2?.24f:.12f));
                    }
                    snapshot.Health = Mathf.Clamp(
                        Mathf.RoundToInt(unit.CurrentHP * bonus.HpMultiplier) + previewBonus.MaximumHealth, 0, bonusHP);
                    if (heldPreview != null && heldPreview.Items.Contains(MonsterTacticsItemKind.Craft26))
                        snapshot.Health = Mathf.RoundToInt(snapshot.Health * 1.10f);
                    snapshot.MaximumHealth = bonusHP;
                    if(bruiserTier>0) snapshot.Health=bonusHP; // Planning: display full-health deployment.
                }
            }
            foreach (Fighter fighter in turnList)
            {
                if (fighter.Unit != unit) continue;
                snapshot.InCombat = phase != Phase.Planning;
                snapshot.IsEnemy = fighter.Enemy;
                snapshot.Health = fighter.HP;
                snapshot.MaximumHealth = fighter.MaxHP;
                snapshot.Mana = fighter.Mana;
                bool fighting = phase == Phase.Battle;
                snapshot.Shield = fighter.Shield;
                snapshot.TemporaryDefenseRemaining = fighting
                    ? Mathf.Max(0f, fighter.TemporaryDefenseUntil - elapsed) : 0f;
                snapshot.TemporaryArmorBonus = snapshot.TemporaryDefenseRemaining > 0f
                    ? fighter.TemporaryDefenseArmor : 0;
                snapshot.TemporaryMagicResistBonus = snapshot.TemporaryDefenseRemaining > 0f
                    ? fighter.TemporaryDefenseMR : 0;
                snapshot.TemporaryShieldRemaining = fighting
                    ? Mathf.Max(0f, fighter.TemporaryShieldUntil - elapsed) : 0f;
                snapshot.TemporaryShieldAmount = snapshot.TemporaryShieldRemaining > 0f
                    ? fighter.TemporaryDefenseShield : 0;
                // Only active battles have debuffs. During Results/Planning
                // display clean base defenses, even if the old Fighter still exists.
                snapshot.SunderRemaining = fighting ? ActiveResistanceRemaining(fighter, false) : 0f;
                snapshot.ShredRemaining = fighting ? ActiveResistanceRemaining(fighter, true) : 0f;
                snapshot.WoundRemaining = fighting ? Mathf.Max(0f,
                    Mathf.Max(fighter.WoundUntil, fighter.AugmentWoundUntil) - elapsed) : 0f;
                snapshot.ArmorReduction = fighting ? ActiveResistanceFraction(fighter, false) : 0f;
                snapshot.MagicResistReduction = fighting ? ActiveResistanceFraction(fighter, true) : 0f;
                snapshot.Wounded = snapshot.WoundRemaining > 0f;
                snapshot.AttackSpeedSlowRemaining = fighting
                    ? Mathf.Max(0f, fighter.AttackSpeedSlowUntil - elapsed) : 0f;
                snapshot.AttackSpeedSlowFraction = snapshot.AttackSpeedSlowRemaining > 0f
                    ? fighter.AttackSpeedSlow : 0f;
                snapshot.ShadowThiefDebuffRemaining = fighting
                    ? Mathf.Max(0f, fighter.ShadowThiefDebuffUntil - elapsed) : 0f;
                snapshot.ShadowThiefDebuffAttackSpeed = snapshot.ShadowThiefDebuffRemaining > 0f
                    ? fighter.ShadowThiefDebuffAS : 0f;
                snapshot.ShadowThiefDebuffDamage = snapshot.ShadowThiefDebuffRemaining > 0f
                    ? fighter.ShadowThiefDebuffDamage : 0f;
                snapshot.ShadowThiefBuffRemaining = fighting
                    ? Mathf.Max(0f, fighter.ShadowThiefBuffUntil - elapsed) : 0f;
                snapshot.ShadowThiefBuffAttackSpeed = snapshot.ShadowThiefBuffRemaining > 0f
                    ? fighter.ShadowThiefBuffAS : 0f;
                snapshot.ShadowThiefBuffDamage = snapshot.ShadowThiefBuffRemaining > 0f
                    ? fighter.ShadowThiefBuffDamage : 0f;
                snapshot.ShadowThiefSpent = fighting && fighter.ShadowThiefSpent;
                snapshot.MagmaCoreActive = fighting && fighter.MagmaCoreEnabled;
                snapshot.BurnImmune = snapshot.MagmaCoreActive;
                if (snapshot.MagmaCoreActive)
                {
                    snapshot.MagmaCoreBaseArmor = fighter.MagmaCoreBaseArmor;
                    snapshot.MagmaCoreBaseMagicResist = fighter.MagmaCoreBaseMR;
                    snapshot.MagmaCoreConvertedArmor = fighter.MagmaCoreConvertedArmor;
                    snapshot.MagmaCoreConvertedMagicResist = fighter.MagmaCoreConvertedMR;
                    snapshot.MagmaCoreGainedAbilityPower = fighter.MagmaCoreConvertedAP;
                }
                snapshot.U610OverdriveRemaining = fighting ? Mathf.Max(0f,fighter.U610OverdriveUntil-elapsed):0;
                snapshot.U610BarrageRemaining = fighting ? Mathf.Max(0f,fighter.U610BarrageUntil-elapsed):0;
                snapshot.U610BarrageDarts = snapshot.U610BarrageRemaining>0f?2:0;
                snapshot.U610BarrageDartDamage = snapshot.U610BarrageRemaining>0f?
                    fighter.Stats.Attack*fighter.U610BarrageDartFactor:0f;
                snapshot.U610SwiftyBonusFraction = fighter.U610Swifty ?
                    .10f + (fighter.U610SwiftyFactor-1f):0f;
                snapshot.U610GritActivated = fighting && fighter.U610GritUsed;
                snapshot.IronFortressActive = fighting && fighter.IronFortressEnabled;
                snapshot.IronFortressBasicHits = snapshot.IronFortressActive ? fighter.IronFortressHitCounter : 0;
                snapshot.IronFortressHitsRequired = snapshot.IronFortressActive
                    ? fighter.IronFortressHitThreshold : 0;
                snapshot.IronFortressRetaliationRaw = snapshot.IronFortressActive
                    ? MonsterTacticsCombatRules.IronFortressRawMagicDamage(
                        MonsterTacticsCombatRules.EffectiveResistance(fighter.Stats.Armor,
                            ActiveResistanceFraction(fighter, false)),
                        fighter.IronFortressArmorRatio)
                    : 0f;
                snapshot.BurnRemaining = fighting ? Mathf.Max(0f, fighter.BurnUntil - elapsed) : 0f;
                snapshot.BurnDamagePerSecond = snapshot.BurnRemaining > 0f ? fighter.BurnPercent * fighter.MaxHP : 0f;
                snapshot.BurnTicksApplied = fighter.BurnTicksApplied;
                snapshot.LastBurnHpDamage = fighter.LastBurnHpDamage;
                snapshot.TotalBurnHpDamage = fighter.TotalBurnHpDamage;
                snapshot.MaximumMana = fighter.Stats.ManaCost;
                var effectiveStats = fighter.Stats;
                if (fighting)
                {
                    effectiveStats.AttackSpeed = EffectiveAttackSpeed(fighter);
                    effectiveStats.DamageMultiplier = EffectiveDamageMultiplier(fighter);
                }
                snapshot.Stats = effectiveStats;
                // Use the same EffectiveResistance function as DealDamage.
                snapshot.EffectiveArmor = MonsterTacticsCombatRules.EffectiveResistance(
                    snapshot.Stats.Armor, snapshot.ArmorReduction);
                snapshot.EffectiveMagicResist = MonsterTacticsCombatRules.EffectiveResistance(
                    snapshot.Stats.MagicResist, snapshot.MagicResistReduction);
                break;
            }
            return true;
        }

        [Header("Scene references (assigned by U4 installer)")]
        [SerializeField] private Transform arenaRoot;
        [SerializeField] private MonsterPlacementController placementController;
        [SerializeField] private Camera sceneCamera;

        [Header("First combat prototype")]
        [SerializeField, Range(.60f, 1.50f)] private float enemyHealthMultiplier = .94f;
        [Header("U6.9.1c: Overtime (30s normal + 15s overtime)")]
        [SerializeField, Range(10f, 120f)] private float normalCombatSeconds = 30f;
        [SerializeField, Range(0f, 60f)] private float overtimeSeconds = 15f;
        [SerializeField, Range(1f, 4f)] private float overtimeMaxSpeed = 1.8f;
        [SerializeField, Range(1f, 4f)] private float overtimeMaxDamage = 2.0f;
        [SerializeField, Range(0f, 1f)] private float overtimeMaxHealReduction = .5f;
        private float realCombatElapsed;
        private bool overtimeAnnounced;
        public float CombatRealSecondsElapsed => realCombatElapsed;
        public bool IsInOvertime => phase == Phase.Battle && realCombatElapsed >= normalCombatSeconds;
        public float OvertimeProgress => overtimeSeconds <= .0001f ? 1f :
            Mathf.Clamp01((realCombatElapsed - normalCombatSeconds) / overtimeSeconds);
        public float CombatDurationSeconds => normalCombatSeconds + overtimeSeconds;
        public static float OvertimeMultiplier(float progress, float maximum) =>
            Mathf.Lerp(1f, Mathf.Max(1f, maximum), Mathf.Clamp01(progress));
        [SerializeField] private bool showCombatLog = true;
        [SerializeField] private MonsterTacticsAbilityVfx abilityVfx;

        // Runtime bootstrap may attach augment manager after combat Awake.
        private MonsterTacticsAugmentManager augmentSpeedProvider;
        private int augmentRescueShield, augmentRescueThreshold = 25;
        private float augmentRescueDuration = 2.5f;
        private float augmentCritChance, augmentCritWoundReduction, augmentCritWoundDuration;
        // U6.9.7 per-combat only. No combat status carries into the next round.
        private int augmentWeaknessAd, augmentWeaknessAp, augmentSecondWindPercent;
        private float augmentSecondWindSeconds = 3f;
        private int augmentCenterShieldPercent, augmentCenterShieldCount;
        private float augmentCenterShieldSeconds = 8f;
        private sealed class Fighter
        {
            public MonsterUnit Unit;
            public bool Enemy;
            public string Name;
            public MonsterTacticsCombatRules.Stats Stats;
            public int HP;
            public int MaxHP;
            public int Mana;
            public int Shield;
            public int TemporaryDefenseArmor, TemporaryDefenseMR;
            public float TemporaryDefenseUntil;
            public int TemporaryDefenseShield;
            public float TemporaryShieldUntil;
            // Temporary, non-stacking combat debuffs. Times use battle elapsed seconds.
            public readonly List<ResistanceEffect> SunderSources = new List<ResistanceEffect>();
            public readonly List<ResistanceEffect> ShredSources = new List<ResistanceEffect>();
            public float WoundUntil;
            public float AugmentWoundUntil;
            public float AugmentWoundReduction;
            public float BurnPercent;
            public Fighter U6115BurnOwner;
            public float BurnUntil;
            public float AttackSpeedSlow;
            public float AttackSpeedSlowUntil;
            public bool ShadowThiefSpent;
            public float ShadowThiefBuffUntil, ShadowThiefBuffAS, ShadowThiefBuffDamage;
            public float ShadowThiefDebuffUntil, ShadowThiefDebuffAS, ShadowThiefDebuffDamage;
            public bool InBasicAttackRecovery;
            public bool MagmaCoreEnabled;
            public int MagmaCoreBaseArmor, MagmaCoreBaseMR;
            public int MagmaCoreConvertedArmor, MagmaCoreConvertedMR;
            public float MagmaCoreConvertedAP, MagmaCoreFraction, MagmaCoreAPCap;
            public bool IronFortressEnabled;
            public int IronFortressHitCounter;
            public int IronFortressHitThreshold;
            public float IronFortressArmorRatio;
            public int IronFortressArmorBonus;
            public int IronFortressMagicResistBonus;
            public float NextBurnTick;
            public int BurnTicksApplied;
            public int LastBurnHpDamage;
            public int TotalBurnHpDamage;
            // Each standard finished-item passive has one independent, per-fight slot.
            // Duplicate finished items stack their base stats but not the same-named passive.
            public readonly bool[] ItemPassive = new bool[36];
            public readonly int[] ItemStacks = new int[36];
            public readonly float[] ItemTimers = new float[36];
            public readonly bool[] ItemFlags = new bool[36];
            // U6.10 dynamic origins / abilities; all state resets with each Fighter.
            public bool U610Swifty, U610Bruiser, U610Grit, U610Overdrive, U610Hexcraft;
            public int U610GritTier, U610OverdriveTier, U610HexcraftTier;
            public float U610SwiftyFactor = 1f;
            public bool U610GritUsed;
            public float U610OverdriveUntil, U610OverdriveDamageFactor = 1f, U610OverdriveSpeedFactor = 1f;
            public float U610ExtraSpeedUntil, U610ExtraSpeedFactor = 1f;
            public float U610BarrageUntil, U610BarrageDartFactor;
            public int U610HeavySparkCounter;
            // U6.11.3: Stormlynx unique (1) state; only the holder accumulates attack charges.
            public bool U6113PlasmaEngine;
            public int U6113BasicAttacks;
            // U6.11.4 Skyweave/Skyshard: active Eon Bond is checked dynamically on damage/casts.
            public bool U612EonMember;
            public float U612SkyshardBuffUntil;
            public int U612SkyshardAttackCounter;
            public Vector2Int U612LastBasicAttackAim;
            public bool U612HasBasicAttackAim;
            public int ItemAttackCount;
            public float AugmentAttackSpeedFactor = 1f;
            // Track reversible Sheer Force bonuses if an item is equipped mid-fight.
            public float SheerForceAttackFactor = 1f;
            public int SheerForceApBonus, SheerForceHpBonus;
            public bool WeaknessPolicyUsed;
            public bool SecondWindUsed;
            public float SecondWindUntil, SecondWindHealPerSecond, SecondWindHealCarry;
            public int CenterShield;
            public float CenterShieldUntil;
            public bool AugmentRescueUsed;
            public int AugmentRescueShield;
            public float AugmentRescueUntil;
            public int TimedItemShield;
            public float TimedItemShieldUntil;
            public float ItemTempBonusUntil;
            public bool ItemTempBonusActive;
            public HexTile Tile;
            public bool Alive => HP > 0;
            public float ActionReady;
            public float MoveReady;
            public Vector3 SlideFrom;
            public Vector3 SlideTo;
            public float SlideStart;
            public float SlideEnd;
            // U6.11.1 Stormlynx-only target stability + visible dash/lunge animation.
            public float U611BlockedSince;
            public float U611LungeStart, U611LungeEnd;
            public Vector3 U611LungeFrom, U611LungeToward;
            // Locked combat target persists across actions and movement.
            // Reset naturally when a new Fighter is created each battle.
            public Fighter LockedTarget;
            public bool OpeningTargetChosen;
        }

        private sealed class ScheduledHit
        {
            public Fighter Source;
            public Fighter Target;
            public MonsterTacticsCombatRules.AbilityProfile Profile;
            public float ResolveAt;
            public int HitIndex;
        }

        private sealed class TextEffect
        {
            public string Text;
            public Vector3 Position;
            public Color Color;
            public float Start;
        }

        private sealed class BeamEffect
        {
            public Vector3 Start;
            public Vector3 End;
            public Color Color;
            public float Born;
        }

        private Phase phase;
        private readonly List<Fighter> players = new List<Fighter>();
        private readonly List<Fighter> opponents = new List<Fighter>();
        private readonly List<Fighter> turnList = new List<Fighter>();
        private readonly List<MonsterUnit> spawnedEnemies = new List<MonsterUnit>();
        private readonly Dictionary<MonsterUnit, HexTile> originalTiles = new Dictionary<MonsterUnit, HexTile>();
        private readonly Dictionary<Vector2Int, HexTile> board = new Dictionary<Vector2Int, HexTile>();
        private readonly List<TextEffect> effects = new List<TextEffect>();
        private readonly List<BeamEffect> beams = new List<BeamEffect>();
        private readonly List<ScheduledHit> pendingHits = new List<ScheduledHit>();
        private Transform enemyRoot;
        // U6.3.14: PvE units already standing on the enemy hexes during planning.
        // The same objects become combat fighters when the round begins.
        private int previewStage = -1;
        private int previewRound = -1;
        private bool placementWasEnabled;
        private float elapsed;
        private string resultMessage = "";
        private string logLine = "Position your Monster, then start the battle.";
        private GUIStyle headlineStyle;
        private GUIStyle hudStyle;
        private GUIStyle smallStyle;
        private GUIStyle effectStyle;

        private void Awake()
        {
            if (sceneCamera == null) sceneCamera = Camera.main;
            if (arenaRoot == null)
            {
                GameObject arena = GameObject.Find("MonsterTactics Arena");
                if (arena != null) arenaRoot = arena.transform;
            }
            if (placementController == null)
            {
                GameObject team = GameObject.Find("MonsterTactics U3 Monster Team");
                if (team != null) placementController = team.GetComponent<MonsterPlacementController>();
            }
            if (abilityVfx == null) abilityVfx = GetComponent<MonsterTacticsAbilityVfx>();
            if (GetComponent<MonsterTacticsTraitHud>() == null) gameObject.AddComponent<MonsterTacticsTraitHud>();
            CacheBoard();
        }

        private void CacheBoard()
        {
            board.Clear();
            if (arenaRoot == null) return;
            foreach (HexTile tile in arenaRoot.GetComponentsInChildren<HexTile>(true))
                board[MonsterTacticsCombatRules.Coord(tile)] = tile;
        }

        private bool ReadyToBattle()
        {
            if (placementController == null || arenaRoot == null)
            {
                logLine = "Missing U1 arena or U3 Monster team. Install those first.";
                return false;
            }
            if (board.Count != 64) CacheBoard();
            if (board.Count != 64)
            {
                logLine = "Expected 64 arena hexes. Open your U1/U3 scene.";
                return false;
            }
            if (Array.Find(placementController.GetComponentsInChildren<MonsterUnit>(true),
                u => u != null && u.gameObject.activeInHierarchy && u.OccupiedTile != null) == null)
            {
                logLine = "No U3 Monster found in the team.";
                return false;
            }
            return true;
        }

        /// <summary>
        /// Places the upcoming wild team/boss on enemy hexes before the timer ends.
        /// No combat AI runs in planning, and none of these preview units are
        /// parented under the player's placement/bench root.
        /// </summary>
        public bool PreparePvePreview(int stage, int round)
        {
            if (phase != Phase.Planning || placementController == null) return false;
            bool opening = stage == 1 && round >= 1 && round <= 4;
            bool solo = !opening &&
                MonsterTacticsBossEncounters.TryGet(stage, round, out MonsterTacticsBossEncounters.Entry _);
            if (!opening && !solo)
            {
                ClearPlanningPreview();
                return false;
            }
            if (enemyRoot != null && previewStage == stage && previewRound == round &&
                opponents.Count > 0 && opponents.Count == spawnedEnemies.Count)
                return true;

            ClearPlanningPreview();
            if (board.Count != 64) CacheBoard();
            if (board.Count != 64) return false;

            // Unlike combat, the player does not have to deploy first: even an
            // inactive benched Monster can serve as a sprite-bearing prefab.
            MonsterUnit template = null;
            foreach (MonsterUnit candidate in placementController.GetComponentsInChildren<MonsterUnit>(true))
            {
                if (candidate == null) continue;
                if (template == null) template = candidate;
                if (candidate.gameObject.activeInHierarchy && candidate.OccupiedTile != null)
                {
                    template = candidate;
                    break;
                }
            }
            if (template == null) return false;

            bool success = opening ? SpawnStageOneWildTeam(round, template) :
                MonsterTacticsBossEncounters.TryGet(stage, round, out MonsterTacticsBossEncounters.Entry boss) &&
                SpawnSoloBoss(boss, template);
            if (!success)
            {
                ClearPlanningPreview();
                return false;
            }
            previewStage = stage;
            previewRound = round;
            return true;
        }

        public void ClearPvePreviewForNonPve()
        {
            if (phase == Phase.Planning && enemyRoot != null)
                ClearPlanningPreview();
        }

        private void ClearPlanningPreview()
        {
            if (phase != Phase.Planning) return;
            CleanupBattleObjects();
            opponents.Clear();
            turnList.Clear();
            previewStage = -1;
            previewRound = -1;
        }

        /// <summary>No owned team units at the deadline: settle a defeat
        /// rather than indefinitely blocking the automatic round scheduler.</summary>
        public bool ForfeitEmptyTeamRound()
        {
            if (phase != Phase.Planning || ReadyToBattle()) return false;
            placementWasEnabled = placementController != null && placementController.enabled;
            ClearPlanningPreview();
            elapsed = 0f;
            realCombatElapsed = 0f;
            resultMessage = "DEFEAT";
            WasEmptyTeamForfeit = true;
            logLine = "No Monster were available to deploy.";
            U6115BeginRoundStats(); // A forfeit has zero combat contribution.
            phase = Phase.Results;
            return true;
        }

        public void StartBattle()
        {
            if (phase != Phase.Planning || !ReadyToBattle()) return;
            placementWasEnabled = placementController.enabled;
            MonsterTacticsU62RoundManager rounds = FindAnyObjectByType<MonsterTacticsU62RoundManager>();
            bool currentPreview = rounds != null && enemyRoot != null &&
                previewStage == rounds.Stage && previewRound == rounds.Round &&
                opponents.Count > 0 && opponents.Count == spawnedEnemies.Count;
            if (!currentPreview) ClearPlanningPreview();
            effects.Clear();
            beams.Clear();
            pendingHits.Clear();
            if (abilityVfx != null) abilityVfx.ClearEffects();
            elapsed = 0f;
            realCombatElapsed = 0f;
            overtimeAnnounced = false;
            WasEmptyTeamForfeit = false;
            resultMessage = "";
            U6115BeginRoundStats();
            players.Clear();
            if (!currentPreview) opponents.Clear();
            turnList.Clear();
            if (currentPreview)
            {
                // Keep the preview's existing positions, visuals, and stats;
                // only initialize actions when combat actually begins.
                foreach (Fighter previewFighter in opponents)
                {
                    previewFighter.HP = previewFighter.MaxHP;
                    previewFighter.Unit.SetHealth(previewFighter.MaxHP);
                    previewFighter.Mana = 0;
                    previewFighter.Shield = 0;
                    previewFighter.LockedTarget = null;
                    previewFighter.OpeningTargetChosen = false;
                    turnList.Add(previewFighter);
                }
            }
            originalTiles.Clear();

            // Preserve the player's existing deployed positions and HP.
            MonsterUnit[] team = placementController.GetComponentsInChildren<MonsterUnit>(true);
            Array.Sort(team, (a, b) => string.CompareOrdinal(a.MonsterName, b.MonsterName));
            foreach (MonsterUnit monster in team)
            {
                if (monster == null || !monster.gameObject.activeInHierarchy || monster.OccupiedTile == null) continue;
                originalTiles.Add(monster, monster.OccupiedTile);
                monster.SetHealth(monster.MaximumHP);
                Fighter fighter = CreateFighter(monster, false, monster.OccupiedTile, 1f);
                players.Add(fighter);
                turnList.Add(fighter);
            }
            if (players.Count == 0)
            {
                logLine = "Deploy at least one Monster before battling.";
                return;
            }

            // U6.3.1: Stage 1 has fixed wild teams, independent of player team size.
            // Later stages retain the existing simulated-PvP opponent path.
            if (rounds != null && rounds.Stage == 1)
            {
                if (!currentPreview && !SpawnStageOneWildTeam(rounds.Round, players[0].Unit))
                {
                    ResetBattle();
                    return;
                }
                ApplyAllTraitBonuses();
                placementController.enabled = false;
                phase = Phase.Battle;
                logLine = rounds.Round == 4 ? "Boss encounter: Nightfang!" : "Wild Monster appeared!";
                return;
            }

            // U6.3.2: every designated boss encounter is one specially balanced
            // wild Monster, never a clone of the current player team.
            if (rounds != null && MonsterTacticsBossEncounters.TryGet(rounds.Stage, rounds.Round, out MonsterTacticsBossEncounters.Entry boss))
            {
                if (!currentPreview && !SpawnSoloBoss(boss, players[0].Unit))
                {
                    ResetBattle();
                    return;
                }
                ApplyAllTraitBonuses();
                placementController.enabled = false;
                phase = Phase.Battle;
                logLine = "SOLO BOSS: " + boss.Species + "!";
                return;
            }

            // U6.5 regular rounds: a real independent simulated AI trainer squad.
            // Special round 4 still uses an AI opponent until special rounds are
            // implemented in a future update. Stage 1 and solo bosses stay above.
            if (rounds != null && rounds.Stage >= 2 && rounds.Stage <= 7 &&
                rounds.CurrentKind != MonsterTacticsU62RoundManager.RoundKind.Boss)
            {
                var shopManager = FindAnyObjectByType<MonsterTacticsU5ShopManager>();
                var enemyTeam = MonsterTacticsAiOpponentRoster.Build(rounds.Stage, rounds.Round);
                if (!SpawnAiTrainerTeam(enemyTeam, shopManager))
                {
                    logLine = "AI opponent could not spawn; check shop templates and arena hexes.";
                    ResetBattle();
                    return;
                }
                CurrentOpponentName = enemyTeam.Trainer;
                ApplyAllTraitBonuses();
                placementController.enabled = false;
                phase = Phase.Battle;
                logLine = "Vs " + enemyTeam.Trainer + " · " + enemyTeam.Strategy +
                    " · Level " + enemyTeam.Level;
                Debug.Log("MonsterTactics U6.5: " + logLine + " (" + opponents.Count + " units)");
                return;
            }

            // Enemy visuals are CLONES of the current player sprites and frames.
            // They are never saved into the scene: Reset destroys all clones.
            enemyRoot = new GameObject("U4 Enemy Monster (temporary)").transform;
            enemyRoot.SetParent(transform, false);
            int[] columns = { 1, 3, 5, 2, 4, 6, 0, 7 };
            // Unique enemy tiles across all four enemy rows (capacity 32).
            // U4 used alternating rows and repeated coordinates after eight allies.
            for (int i = 0; i < players.Count && i < 32; i++)
            {
                Fighter source = players[i];
                int row = 7 - i / columns.Length;
                int col = columns[i % columns.Length];
                Vector2Int location = new Vector2Int(col, row);
                if (!board.TryGetValue(location, out HexTile tile)) continue;

                GameObject clone = Instantiate(source.Unit.gameObject, enemyRoot);
                clone.name = "Enemy " + source.Name;
                MonsterUnit unit = clone.GetComponent<MonsterUnit>();
                unit.PlaceOnTile(tile);
                unit.SetSelected(false);
                var renderer = clone.GetComponentInChildren<SpriteRenderer>(true);
                if (renderer != null)
                {
                    renderer.color = new Color(1f, .77f, .77f, 1f);
                    // Runtime clones do not inherit the aura's nonserialized star level.
                    // Re-apply the source's upgrade on the enemy visual explicitly.
                    var progress = unit.GetComponent<MonsterTacticsU5Progression>();
                    var family = MonsterTacticsU5Roster.Find(unit.MonsterName);
                    if (progress != null && progress.Stage >= 2 &&
                        family != null && !MonsterTacticsU5Roster.HasDistinctEvolution(family))
                    {
                        MonsterTacticsStarAura aura = renderer.GetComponent<MonsterTacticsStarAura>();
                        if (aura == null) aura = renderer.gameObject.AddComponent<MonsterTacticsStarAura>();
                        aura.SetStage(progress.Stage);
                    }
                }
                spawnedEnemies.Add(unit);
                Fighter fighter = CreateFighter(unit, true, tile, enemyHealthMultiplier);
                opponents.Add(fighter);
                turnList.Add(fighter);
            }
            if (opponents.Count == 0)
            {
                logLine = "No enemy spawn tiles available.";
                ResetBattle();
                return;
            }
            ApplyAllTraitBonuses();
            placementController.enabled = false; // only while fighting/results
            phase = Phase.Battle;
            logLine = "The battle begins!";
            Debug.Log("MonsterTactics U4: " + players.Count + " Monster vs " + opponents.Count + " enemies.");
        }

        private bool SpawnAiTrainerTeam(MonsterTacticsAiOpponentRoster.Team team,
            MonsterTacticsU5ShopManager shopManager)
        {
            if (team == null || shopManager == null || team.Units == null ||
                team.Units.Length == 0) return false;
            enemyRoot = new GameObject("U6.5 AI Trainer - " + team.Trainer).transform;
            enemyRoot.SetParent(transform, false);
            foreach (MonsterTacticsAiOpponentRoster.UnitSpec spec in team.Units)
            {
                if (!board.TryGetValue(spec.Hex, out HexTile tile)) return false;
                MonsterUnit unit = shopManager.CreateAiCombatUnit(
                    spec.Family, spec.Stars, enemyRoot, tile, sceneCamera);
                if (unit == null) return false;
                spawnedEnemies.Add(unit);
                Fighter fighter = CreateFighter(unit, true, tile, 1f);
                opponents.Add(fighter);
                turnList.Add(fighter);
            }
            return opponents.Count == team.Units.Length;
        }

        // U6.3.13: intentional encounter formations, with ranged wild Monster
        // behind melee wild Monster. Rows 4-7 are always on the enemy side.
        // All placements are distinct so no two enemies occupy one hex.
        private static Vector2Int StageOneSpawnTile(int round, string species)
        {
            switch (round)
            {
                case 1:
                    return species == "Winglit" ? new Vector2Int(4, 7) : new Vector2Int(3, 5);
                case 2:
                    switch (species)
                    {
                        case "Winglit": return new Vector2Int(3, 7);
                        case "Skitter": return new Vector2Int(2, 5);
                        default: return new Vector2Int(4, 5); // Crawlet front
                    }
                case 3:
                    switch (species)
                    {
                        case "Winglit": return new Vector2Int(3, 7);
                        case "Skitter": return new Vector2Int(2, 5);
                        case "Crawlet": return new Vector2Int(4, 5);
                        default: return new Vector2Int(3, 6); // Ashhound center
                    }
                case 4: return new Vector2Int(3, 5); // Nightfang solo boss
                default: return new Vector2Int(3, 6);
            }
        }

        private static Vector2Int SoloBossSpawnTile(string species)
        {
            // Grounded, short-range bosses start slightly forward; ranged and
            // airborne bosses hold a centered enemy-backline position.
            if (species == "Huggevodrel" || species == "MonolithKing")
                return new Vector2Int(3, 5);
            return new Vector2Int(3, 6);
        }

        // Visual clearances are relative to the top face of the owner's tile;
        // floating bosses look airborne while grounded bosses keep their feet low.
        private static float SoloBossClearance(string species)
        {
            switch (species)
            {
                case "Skyclaw": return .14f;
                case "Huggevodrel": return .055f;
                case "MonolithKing": return .035f;
                case "Gloomborne": return .15f;
                case "Starshifter": return .13f;
                case "Dreadwing": return .19f;
                case "VortexPrime": return .15f;
                default: return .06f;
            }
        }

        /// <summary>Create Stage 1 PvE units using approved weak-wild stats, not player clones.</summary>
        private bool SpawnStageOneWildTeam(int round, MonsterUnit template)
        {
            MonsterTacticsStageOneEncounters.WildUnit[] wilds = MonsterTacticsStageOneEncounters.ForRound(round);
            enemyRoot = new GameObject("U6.3.1 Wild Monster (temporary)").transform;
            enemyRoot.SetParent(transform, false);
            for (int i = 0; i < wilds.Length; ++i)
            {
                MonsterTacticsStageOneEncounters.WildUnit wild = wilds[i];
                Vector2Int coord = StageOneSpawnTile(round, wild.Name);
                if (!board.TryGetValue(coord, out HexTile tile))
                {
                    Debug.LogError("MonsterTactics: Stage 1 " + round + " missing enemy tile " + coord);
                    return false;
                }
                Sprite[] frames = MonsterTacticsWildSpriteLibrary.GetFrames(wild.Name);
                if (frames.Length == 0)
                {
                    Debug.LogError("MonsterTactics: Missing real sprite for " + wild.Name +
                        ". Run MonsterTactics > Monster > U6.3.1 > Install Wild Animated Sprites.");
                    return false;
                }
                GameObject clone = Instantiate(template.gameObject, enemyRoot);
                clone.name = "Wild " + wild.Name;
                var wildHeld = clone.GetComponent<MonsterTacticsHeldItems>();
                if (wildHeld != null) wildHeld.TakeAll();
                clone.SetActive(true);
                MonsterUnit unit = clone.GetComponent<MonsterUnit>();
                SpriteRenderer visual = clone.GetComponentInChildren<SpriteRenderer>(true);
                if (unit == null || visual == null) { Destroy(clone); return false; }
                MonsterTacticsStarAura aura = visual.GetComponent<MonsterTacticsStarAura>();
                if (aura != null) aura.SetStage(1);
                MonsterTacticsU5Progression progress = clone.GetComponent<MonsterTacticsU5Progression>();
                if (progress != null) progress.Configure(wild.Name, 1, 0);
                unit.Configure(wild.Name, wild.Type, wild.Role, wild.HP, tile, visual, sceneCamera);
                unit.SetSelected(false);
                MonsterFrameAnimator animator = visual.GetComponent<MonsterFrameAnimator>();
                if (animator == null) animator = visual.gameObject.AddComponent<MonsterFrameAnimator>();
                animator.SetAnimation(visual, frames, MonsterTacticsWildSpriteLibrary.GetTimings(wild.Name));
                // Wild creatures are roughly unit-sized; the final Nightfang is
                // a modest solo boss. Scale never inherits the player's sprite.
                bool boss = wild.Name == "Nightfang";
                unit.ConfigureEncounterVisual((boss ? 2.05f : 1.58f) *
                    MonsterTacticsU6105EncounterArtCatalog.ScaleFor(wild.Name, false) *
                    MonsterTacticsU6106ScaleProfile.EncounterMultiplier(wild.Name, false),
                    boss ? .055f : .035f);
                if (MonsterTacticsU6105EncounterArtCatalog.IsUnified(wild.Name))
                {
                    MonsterTacticsU610SpriteMotion motion = visual.GetComponent<MonsterTacticsU610SpriteMotion>();
                    if (motion == null) motion = visual.gameObject.AddComponent<MonsterTacticsU610SpriteMotion>();
                    motion.ConfigureEncounter(unit); // Fallback breathing at the configured encounter scale.
                }
                visual.color = Color.white;
                spawnedEnemies.Add(unit);
                Fighter fighter = CreateFighter(unit, true, tile, 1f);
                fighter.Stats = wild.Stats();
                fighter.ActionReady = .5f + i * .1f;
                opponents.Add(fighter);
                turnList.Add(fighter);
            }
            return opponents.Count == wilds.Length;
        }

        /// <summary>Creates precisely ONE boss. Never adds a reserve/bench unit.</summary>
        private bool SpawnSoloBoss(MonsterTacticsBossEncounters.Entry boss, MonsterUnit template)
        {
            Vector2Int coord = SoloBossSpawnTile(boss.Species);
            if (!board.TryGetValue(coord, out HexTile tile))
            {
                Debug.LogError("MonsterTactics: No solo boss spawn tile at " + coord);
                return false;
            }
            Sprite[] frames = MonsterTacticsWildSpriteLibrary.GetFrames(boss.Species);
            if (frames.Length == 0)
            {
                Debug.LogError("MonsterTactics: Missing sprite for solo boss " + boss.Species +
                    ". Use MonsterTactics > Monster > U6.3.2 > Install Boss Animated Sprites.");
                return false;
            }
            enemyRoot = new GameObject("U6.3.2 Solo Boss (temporary)").transform;
            enemyRoot.SetParent(transform, false);
            GameObject clone = Instantiate(template.gameObject, enemyRoot);
            clone.name = "Boss " + boss.Species;
            var bossHeld = clone.GetComponent<MonsterTacticsHeldItems>();
            if (bossHeld != null) bossHeld.TakeAll();
            clone.SetActive(true);
            MonsterUnit unit = clone.GetComponent<MonsterUnit>();
            SpriteRenderer visual = clone.GetComponentInChildren<SpriteRenderer>(true);
            if (unit == null || visual == null) { Destroy(clone); return false; }
            MonsterTacticsStarAura aura = visual.GetComponent<MonsterTacticsStarAura>();
            if (aura != null) aura.SetStage(1);
            MonsterTacticsU5Progression progress = clone.GetComponent<MonsterTacticsU5Progression>();
            if (progress != null) progress.Configure(boss.Species, 1, 0);
            unit.Configure(boss.Species, boss.Unit.Type, boss.Unit.Role,
                boss.Unit.HP, tile, visual, sceneCamera);
            unit.SetSelected(false);
            MonsterFrameAnimator animator = visual.GetComponent<MonsterFrameAnimator>();
            if (animator == null) animator = visual.gameObject.AddComponent<MonsterFrameAnimator>();
            animator.SetAnimation(visual, frames, MonsterTacticsWildSpriteLibrary.GetTimings(boss.Species));
            // Existing per-boss sizes were already large. A 5% bump makes them
            // more imposing without covering excessive hexes. Importer artwork
            // always has a 96x96 bottom-aligned transparent canvas.
            unit.ConfigureEncounterVisual(1.6f * boss.VisualScale * 1.05f *
                MonsterTacticsU6105EncounterArtCatalog.ScaleFor(boss.Species, true) *
                MonsterTacticsU6106ScaleProfile.EncounterMultiplier(boss.Species, true),
                SoloBossClearance(boss.Species));
            if (MonsterTacticsU6105EncounterArtCatalog.IsUnified(boss.Species))
            {
                MonsterTacticsU610SpriteMotion motion = visual.GetComponent<MonsterTacticsU610SpriteMotion>();
                if (motion == null) motion = visual.gameObject.AddComponent<MonsterTacticsU610SpriteMotion>();
                motion.ConfigureEncounter(unit);
            }
            visual.color = Color.white;
            spawnedEnemies.Add(unit);
            Fighter fighter = CreateFighter(unit, true, tile, 1f);
            fighter.Stats = boss.Unit.Stats();
            fighter.ActionReady = .40f;
            opponents.Add(fighter);
            turnList.Add(fighter);
            return opponents.Count == 1;
        }

        private void ApplyAllTraitBonuses()
        {
            var activeUnits = new List<MonsterUnit>();
            foreach (Fighter fighter in players)
                if (fighter.Unit != null) activeUnits.Add(fighter.Unit);
            lockedPlayerTraits = MonsterTacticsTraits.CountDeployed(activeUnits);
            ApplyTeamTraitBonuses(players);
            // Wild Monster and PvE bosses are intentionally excluded. AI trainer
            // squads get their own synergies, calculated separately from player units.
            var rounds = FindAnyObjectByType<MonsterTacticsU62RoundManager>();
            bool pve = rounds != null && (rounds.Stage == 1 ||
                MonsterTacticsBossEncounters.TryGet(rounds.Stage, rounds.Round, out MonsterTacticsBossEncounters.Entry unused));
            if (!pve) ApplyTeamTraitBonuses(opponents);
            // Final pass AFTER traits/items: authored augments affect the real
            // fighter Stats, HP and starting mana, not just the HUD display.
            ApplyChosenAugmentCombatBonuses();
        }

        private void ApplyChosenAugmentCombatBonuses()
        {
            if (augmentSpeedProvider == null)
                augmentSpeedProvider = FindAnyObjectByType<MonsterTacticsAugmentManager>();
            augmentRescueShield = 0;
            augmentCritChance = augmentCritWoundReduction = augmentCritWoundDuration = 0f;
            augmentWeaknessAd = augmentWeaknessAp = augmentSecondWindPercent = 0;
            augmentCenterShieldPercent = augmentCenterShieldCount = 0;
            if (augmentSpeedProvider == null || !augmentSpeedProvider.IsActive) return;
            augmentSpeedProvider.GetBattleAugmentBonuses(out int hp, out int resist,
                out float ad, out float speed, out int mana,
                out augmentRescueShield, out augmentRescueThreshold,
                out augmentRescueDuration, out float traitDamage);
            augmentSpeedProvider.GetCriticalAugment(out augmentCritChance,
                out augmentCritWoundReduction, out augmentCritWoundDuration);
            augmentSpeedProvider.GetU697ThresholdEffects(out augmentWeaknessAd,
                out augmentWeaknessAp, out augmentSecondWindPercent,
                out augmentSecondWindSeconds, out augmentCenterShieldPercent,
                out augmentCenterShieldSeconds, out augmentCenterShieldCount);
            int activeTypeCount = 0, distinctTypeCount = 0;
            foreach (var definition in MonsterTacticsTraits.All)
            {
                if (definition == null || definition.Origin) continue;
                if (lockedPlayerTraits != null &&
                    lockedPlayerTraits.TryGetValue(definition.Name, out int copies) && copies > 0)
                {
                    distinctTypeCount++;
                    if (MonsterTacticsTraits.ActiveTier(definition.Name, lockedPlayerTraits) > 0)
                        activeTypeCount++;
                }
            }
            bool traitAdvantage = false;
            var shopManager = FindAnyObjectByType<MonsterTacticsU5ShopManager>();
            if (shopManager != null && !shopManager.IsPveRound && traitDamage > 0f)
            {
                int allyCount = CountActivatedTraits(players);
                int enemyCount = CountActivatedTraits(opponents);
                traitAdvantage = allyCount > enemyCount;
            }
            foreach (var fighter in players)
            {
                if (fighter == null || !fighter.Alive) continue;
                // Sheer Force checks actual equipped items, not inventory bag contents.
                augmentSpeedProvider.GetUnitemizedBonuses(fighter.Unit,
                    out fighter.SheerForceAttackFactor, out fighter.SheerForceApBonus,
                    out fighter.SheerForceHpBonus);
                fighter.Stats.Attack *= fighter.SheerForceAttackFactor;
                fighter.Stats.AbilityPower += fighter.SheerForceApBonus;
                fighter.MaxHP += fighter.SheerForceHpBonus;
                fighter.HP = fighter.MaxHP;
                if (fighter.Unit != null) fighter.Unit.SetTemporaryCombatMaximumHP(fighter.MaxHP);
                fighter.Stats.CriticalChance = Mathf.Clamp01(fighter.Stats.CriticalChance + augmentCritChance);
                fighter.Stats.Attack *= 1f + ad;
                fighter.Stats.AttackSpeed *= 1f + speed;
                fighter.Mana = Mathf.Min(fighter.Stats.ManaCost, fighter.Mana + mana);
                var progress = fighter.Unit != null ? fighter.Unit.GetComponent<MonsterTacticsU5Progression>() : null;
                int stars = progress != null ? progress.Stage : 1;
                if (stars == 1 && (hp > 0 || resist > 0))
                {
                    fighter.MaxHP = Mathf.Max(1, fighter.MaxHP + hp);
                    fighter.HP = fighter.MaxHP;
                    fighter.Stats.Armor += resist;
                    fighter.Stats.MagicResist += resist;
                    if (fighter.Unit != null) fighter.Unit.SetTemporaryCombatMaximumHP(fighter.MaxHP);
                }
                if (traitAdvantage) fighter.Stats.DamageMultiplier *= 1f + traitDamage;
                augmentSpeedProvider.ApplyU697CombatStartBonuses(fighter.Unit,
                    stars, activeTypeCount, distinctTypeCount,
                    ref fighter.Stats, ref fighter.MaxHP);
                fighter.HP = fighter.MaxHP;
                if (fighter.Unit != null)
                    fighter.Unit.SetTemporaryCombatMaximumHP(fighter.MaxHP);
            }
            // Select the two lowest maximum-HP teammates at combat start.
            // Everyone is full HP at the start, so max HP is the deterministic tie-breaker.
            if (augmentCenterShieldPercent > 0 && augmentCenterShieldCount > 0)
            {
                var elidunebit = new List<Fighter>();
                foreach (var ally in players)
                    if (ally != null && ally.Alive) elidunebit.Add(ally);
                elidunebit.Sort((a, b) => a.MaxHP.CompareTo(b.MaxHP));
                for (int index = 0; index < Mathf.Min(elidunebit.Count, augmentCenterShieldCount); index++)
                {
                    var ally = elidunebit[index];
                    int gained = Mathf.Max(1, Mathf.RoundToInt(ally.MaxHP * augmentCenterShieldPercent / 100f));
                    ally.Shield += gained;
                    ally.CenterShield = gained;
                    U6115TrackShield(ally, gained);
                    ally.CenterShieldUntil = elapsed + augmentCenterShieldSeconds;
                }
            }
        }

        private static int CountActivatedTraits(List<Fighter> team)
        {
            var units = new List<MonsterUnit>();
            foreach (var fighter in team)
                if (fighter != null && fighter.Unit != null) units.Add(fighter.Unit);
            var counts = MonsterTacticsTraits.CountDeployed(units);
            int activeCount = 0;
            foreach (var trait in MonsterTacticsTraits.All)
                if (trait != null && MonsterTacticsTraits.ActiveTier(trait.Name, counts) > 0)
                    activeCount++;
            return activeCount;
        }

        private void ApplyTeamTraitBonuses(List<Fighter> team)
        {
            var units = new List<MonsterUnit>();
            foreach (Fighter fighter in team)
                if (fighter.Unit != null) units.Add(fighter.Unit);
            Dictionary<string, int> counts = MonsterTacticsTraits.CountDeployed(units);
            foreach (Fighter fighter in team)
            {
                MonsterTacticsTraits.Bonus bonus = MonsterTacticsTraits.ForUnit(fighter.Unit, counts);
                fighter.Stats = MonsterTacticsTraits.ApplyToStats(fighter.Stats, bonus);
                MonsterTacticsHeldItems held = fighter.Unit != null ? fighter.Unit.GetComponent<MonsterTacticsHeldItems>() : null;
                MonsterTacticsItemBonus itemBonus = held != null ? held.CombinedBonus : new MonsterTacticsItemBonus();
                if (held != null)
                    foreach (MonsterTacticsItemKind item in held.Items)
                    {
                        int id = (int)item - (int)MonsterTacticsItemKind.Craft00;
                        if (id >= 0 && id < 36) fighter.ItemPassive[id] = true;
                    }
                fighter.Stats = MonsterTacticsItemTypes.ApplyStats(fighter.Stats, itemBonus);
                fighter.MaxHP = Mathf.Max(1, Mathf.RoundToInt(fighter.MaxHP * bonus.HpMultiplier) + itemBonus.MaximumHealth);
                if (HasItemPassive(fighter, 26)) fighter.MaxHP = Mathf.Max(1, Mathf.RoundToInt(fighter.MaxHP * 1.10f));
                if (HasItemPassive(fighter, 4)) fighter.Stats.Omnivamp = Mathf.Clamp01(fighter.Stats.Omnivamp + .12f);
                fighter.HP = fighter.MaxHP;
                fighter.Mana = Mathf.Min(fighter.Stats.ManaCost, Mathf.Max(fighter.Mana, itemBonus.StartingMana));
                if (fighter.Unit != null)
                {
                    // Keep actual nameplate HP in sync with fighter HP.
                    // Remove these temporary modifications after combat ends.
                    fighter.Unit.SetTemporaryCombatMaximumHP(fighter.MaxHP);
                }
                InitializeItemPassives(fighter);
                U610ApplyTraits(fighter, counts);
            }
        }

        private Fighter CreateFighter(MonsterUnit unit, bool enemy, HexTile tile, float hpMultiplier)
        {
            int maxHP = Mathf.Max(1, Mathf.RoundToInt(unit.MaximumHP * hpMultiplier));
            Fighter actor = new Fighter
            {
                Unit = unit,
                Enemy = enemy,
                Name = unit.MonsterName,
                Tile = tile,
                Stats = MonsterTacticsU5Roster.CombatStats(unit),
                HP = maxHP,
                MaxHP = maxHP,
                Mana = 0,
                Shield = 0,
                ActionReady = UnityEngine.Random.Range(.1f, .35f),
                MoveReady = UnityEngine.Random.Range(.06f, .21f)
            };
            unit.SetHealth(Mathf.Min(maxHP, unit.MaximumHP));
            return actor;
        }

        private void Update()
        {
            if (phase != Phase.Battle) return;
            // Wall-clock round limit stays 30s + 15s regardless of time dilation.
            realCombatElapsed += Mathf.Min(Time.deltaTime, .12f);
            bool inOvertime = IsInOvertime;
            if (inOvertime && !overtimeAnnounced)
            {
                overtimeAnnounced = true;
                logLine = "OVERTIME! Combat speeds up and damage increases.";
                Debug.Log("[U6.9.1c] OVERTIME started.", this);
            }
            float simSpeed = inOvertime ?
                OvertimeMultiplier(OvertimeProgress, overtimeMaxSpeed) : 1f;
            float delta = Mathf.Min(Time.deltaTime, .08f) * simSpeed;
            elapsed += delta;
            // Real-time (not overtime-accelerated) stacking: +5% every 2s up to 50%.
            // Each Fighter starts at factor 1 on each new battle.
            if (augmentSpeedProvider == null)
                augmentSpeedProvider = FindAnyObjectByType<MonsterTacticsAugmentManager>();
            if (augmentSpeedProvider != null && augmentSpeedProvider.IsActive)
            {
                float factor = augmentSpeedProvider.CombatTeamAttackSpeedFactor(realCombatElapsed);
                foreach (Fighter fighter in players)
                {
                    if (!fighter.Alive || Mathf.Approximately(fighter.AugmentAttackSpeedFactor, factor)) continue;
                    fighter.Stats.AttackSpeed *= factor / Mathf.Max(.01f, fighter.AugmentAttackSpeedFactor);
                    fighter.AugmentAttackSpeedFactor = factor;
                }
            }
            UpdateSlides();
            U611UpdateStormlynxLunges();
            TickAugmentRescueShields();
            TickU697CombatEffects(delta);
            TickTemporaryProtection();
            foreach (Fighter fighter in turnList)
                if (fighter.MagmaCoreEnabled) RecalculateMagmaCore(fighter);
            TickAttackSpeedSlows();
            U610TickTraits();
            TickShadowThief();
            TickBurns();
            TickItemPassives();
            ResolvePendingHits();

            for (int i = 0; i < turnList.Count; i++)
            {
                Fighter self = turnList[i];
                if (!self.Alive || elapsed < self.ActionReady || elapsed < self.MoveReady &&
                    self.SlideEnd > elapsed) continue;
                Fighter target = PickTarget(self);
                if (target == null) continue;
                int hexDistance = MonsterTacticsCombatRules.HexDistance(
                    MonsterTacticsCombatRules.Coord(self.Tile), MonsterTacticsCombatRules.Coord(target.Tile));

                if (hexDistance <= self.Stats.Range)
                {
                    if (U611IsStormlynx(self)) self.U611BlockedSince = 0f;
                    if (self.Mana >= self.Stats.ManaCost)
                        CastAbility(self, target);
                    else
                        BasicAttack(self, target);
                }
                else if (elapsed >= self.MoveReady)
                {
                    MoveTowards(self, target);
                }
            }

            if (CountAlive(players) == 0 && CountAlive(opponents) == 0)
                Finish("DRAW", "Both teams fainted.");
            else if (CountAlive(opponents) == 0)
                Finish("VICTORY", "Your team defeated the enemy!");
            else if (CountAlive(players) == 0)
                Finish("DEFEAT", "Your Monster were defeated.");
            else if (realCombatElapsed >= CombatDurationSeconds)
            {
                float allyScore = HealthScore(players);
                float enemyScore = HealthScore(opponents);
                if (allyScore > enemyScore + .01f) Finish("VICTORY", "Time limit: more allied HP remains.");
                else if (enemyScore > allyScore + .01f) Finish("DEFEAT", "Time limit: enemy has more HP.");
                else Finish("DRAW", "Time limit: tied HP.");
            }
            effects.RemoveAll(effect => elapsed - effect.Start > 1.0f);
            beams.RemoveAll(beam => elapsed - beam.Born > .25f);
        }

        private Fighter PickTarget(Fighter self)
        {
            // Do not switch just because another enemy walks closer.
            // Target locks only break when the target faints or is unreachable.
            if (IsValidTarget(self, self.LockedTarget)) return self.LockedTarget;

            self.LockedTarget = null;
            if (!self.OpeningTargetChosen)
            {
                self.OpeningTargetChosen = true;
                // U6.11.2: opt-in nearby lowest-HP% target. No immediate
                // backline bypass. Legacy option keeps old behavior, including
                // Stormlynx's U6.11.1 closest-enemy exception.
                if (U6112EnabledFor(self))
                    self.LockedTarget = U6112PickNearestWeakest(self, null, false);
                else if (IsAssassin(self) && !U611IsStormlynx(self))
                    self.LockedTarget = PickBacklineTarget(self);
            }
            if (!IsValidTarget(self, self.LockedTarget))
            {
                self.LockedTarget = U6112EnabledFor(self) ?
                    U6112PickNearestWeakest(self, null, false) :
                    PickClosestTarget(self, null, null, false);
            }
            return self.LockedTarget;
        }

        private static bool IsAssassin(Fighter fighter)
        {
            return fighter.Unit != null &&
                string.Equals(fighter.Unit.MonsterRole, "Assassin", StringComparison.OrdinalIgnoreCase);
        }

        private bool IsValidTarget(Fighter self, Fighter target)
        {
            return target != null && target.Alive && target.Tile != null &&
                target.Enemy != self.Enemy;
        }

        private HashSet<Vector2Int> OccupiedExcept(Fighter self)
        {
            var occupied = new HashSet<Vector2Int>();
            foreach (Fighter actor in turnList)
                if (actor.Alive && actor != self && actor.Tile != null)
                    occupied.Add(MonsterTacticsCombatRules.Coord(actor.Tile));
            return occupied;
        }

        private Fighter PickClosestTarget(Fighter self, Fighter exclude,
            HashSet<Vector2Int> occupied, bool onlyReachable)
        {
            List<Fighter> enemyTeam = self.Enemy ? players : opponents;
            Fighter best = null;
            int bestDistance = int.MaxValue;
            foreach (Fighter candidate in enemyTeam)
            {
                if (!IsValidTarget(self, candidate) || candidate == exclude) continue;
                int distance = MonsterTacticsCombatRules.HexDistance(
                    MonsterTacticsCombatRules.Coord(self.Tile), MonsterTacticsCombatRules.Coord(candidate.Tile));
                if (onlyReachable && distance > self.Stats.Range &&
                    !MonsterTacticsCombatRules.NextStep(self.Tile, candidate.Tile,
                        self.Stats.Range, board, occupied, out _)) continue;
                if (distance < bestDistance ||
                    (distance == bestDistance && best != null && candidate.HP < best.HP))
                {
                    bestDistance = distance;
                    best = candidate;
                }
            }
            return best;
        }

        private void MoveTowards(Fighter self, Fighter target)
        {
            self.InBasicAttackRecovery = false; // Walking is unaffected by AS Slow.
            HashSet<Vector2Int> occupied = OccupiedExcept(self);
            if (!MonsterTacticsCombatRules.NextStep(self.Tile, target.Tile, self.Stats.Range,
                board, occupied, out HexTile next))
            {
                // The locked target is surrounded or a corridor is blocked.
                // Switch to a DIFFERENT enemy only if one can actually be reached.
                // U6.11.2: all Assassins (when enabled) keep their target
                // through brief crowding, then seek a reachable alternative.
                // U6.11.1 Stormlynx retains this behavior in legacy mode.
                bool holdLock = U611IsStormlynx(self) || U6112EnabledFor(self);
                if (holdLock && self.U611BlockedSince <= 0f)
                    self.U611BlockedSince = Mathf.Max(.0001f, elapsed);
                bool canRetarget = !holdLock || elapsed - self.U611BlockedSince >=
                    (U6112EnabledFor(self) ? U6112BlockedDelay : 1.10f);
                Fighter alternative = canRetarget ?
                    (U6112EnabledFor(self) ? U6112PickNearestWeakest(self, target, true) :
                    PickClosestTarget(self, target, occupied, true)) : null;
                if (alternative != null)
                {
                    self.LockedTarget = alternative;
                    self.U611BlockedSince = 0f;
                    int distance = MonsterTacticsCombatRules.HexDistance(
                        MonsterTacticsCombatRules.Coord(self.Tile), MonsterTacticsCombatRules.Coord(alternative.Tile));
                    if (distance <= self.Stats.Range)
                    {
                        // Now in range: attack on the next action, without moving.
                        self.MoveReady = elapsed;
                        return;
                    }
                    MonsterTacticsCombatRules.NextStep(self.Tile, alternative.Tile,
                        self.Stats.Range, board, occupied, out next);
                }
                else
                {
                    // No complete path to an attack position yet. Approach a free
                    // hex if possible; don't walk through or stack on living units.
                    MonsterTacticsCombatRules.NextApproachStep(self.Tile, target.Tile,
                        board, occupied, out next);
                }
                if (next == null)
                {
                    // Try again soon: other units may have moved/fainted.
                    self.MoveReady = elapsed + .22f;
                    self.ActionReady = Mathf.Max(self.ActionReady, elapsed + .18f);
                    return;
                }
            }

            self.U611BlockedSince = 0f;
            // Only out-of-range Monster reach this method. If an enemy is already
            // within range, the Update loop attacks from the CURRENT hex.
            self.SlideFrom = self.Unit.transform.position;
            self.Tile = next;
            self.Unit.PlaceOnTile(next);
            self.SlideTo = self.Unit.transform.position;
            self.Unit.transform.position = self.SlideFrom;
            self.SlideStart = elapsed;
            self.SlideEnd = elapsed + self.Stats.MoveInterval * .72f;
            self.MoveReady = elapsed + self.Stats.MoveInterval;
            self.ActionReady = Mathf.Max(self.ActionReady, elapsed + self.Stats.MoveInterval * .70f);
        }

        private void UpdateSlides()
        {
            foreach (Fighter fighter in turnList)
            {
                if (!fighter.Alive || fighter.SlideEnd <= fighter.SlideStart || fighter.SlideEnd <= 0f) continue;
                float progress = Mathf.Clamp01((elapsed - fighter.SlideStart) / (fighter.SlideEnd - fighter.SlideStart));
                fighter.Unit.transform.position = Vector3.Lerp(fighter.SlideFrom, fighter.SlideTo, progress) +
                    Vector3.up * (.11f * Mathf.Sin(progress * Mathf.PI));
                if (progress >= 1f) fighter.SlideEnd = 0f;
            }
        }

        private void BasicAttack(Fighter source, Fighter target)
        {
            // Keep the aim hex even when the primary basic attack defeats the target.
            if (MonsterTacticsU5Roster.BaseName(source.Name) == "Skyshard" && target.Tile != null)
            {
                source.U612LastBasicAttackAim = MonsterTacticsCombatRules.Coord(target.Tile);
                source.U612HasBasicAttackAim = true;
            }
            source.ItemAttackCount++;
            float baseAttack = source.Stats.Attack;
            float raw = baseAttack;
            bool crit = UnityEngine.Random.value < source.Stats.CriticalChance;
            if (crit)
            {
                raw *= source.Stats.CriticalMultiplier;
                if (HasItemPassive(source, 35)) raw *= 1.20f;
                if (HasItemPassive(target, 21)) raw = baseAttack + (raw - baseAttack) * .75f;
            }
            DealDamage(target, raw, false, source, crit ? "CRIT" : null);
            RegisterIronFortressBasicHit(target, source);
            U610AfterBasicAttack(source, target);
            // Item-generated strikes never recursively trigger attack-based items.
            if (target.Alive && HasItemPassive(source, 0) && source.ItemAttackCount % 3 == 0)
                DealDamage(target, source.Stats.Attack * .40f, false, source, "TWIN FANGS");
            if (HasItemPassive(source, 2) && source.ItemAttackCount % 3 == 0)
            {
                Fighter extra = FindSecondTargetWithin(source, target, 2);
                if (extra != null) DealDamage(extra, source.Stats.Attack * .55f, false, source, "QUICK CLAW");
            }
            if (target.Alive && HasItemPassive(source, 1) && source.ItemFlags[1])
            {
                DealDamage(target, source.Stats.AbilityPower * .60f, true, source, "SPELLBLADE");
                source.ItemFlags[1] = false;
            }
            if (target.Alive && HasItemPassive(source, 5) && source.ItemFlags[5])
            {
                DealDamage(target, source.Stats.Attack * .50f, false, source, "FOCUSED STRIKE");
                source.Mana = Mathf.Min(source.Stats.ManaCost, source.Mana + 5);
                source.ItemFlags[5] = false;
            }
            OnItemBasicAttack(source, target, crit);
            if (source.Alive)
            {
                int mana = Mathf.RoundToInt(20f * source.Stats.ManaGainMultiplier);
                if (HasItemPassive(source, 18)) mana += 4;
                if (crit && HasItemPassive(source, 35) && elapsed >= source.ItemTimers[35])
                {
                    mana += 3;
                    source.ItemTimers[35] = elapsed + 2f;
                }
                source.Mana = Mathf.Min(source.Stats.ManaCost, source.Mana + mana);
            }
            source.ActionReady = elapsed + 1f / EffectiveAttackSpeed(source);
            source.InBasicAttackRecovery = true;
            if (showCombatLog) logLine = source.Name + " hit " + target.Name + (crit ? " (critical)!" : ".");
        }

        private Fighter FindSecondTargetWithin(Fighter source, Fighter primary, int radius)
        {
            if (source == null || primary == null || primary.Tile == null) return null;
            List<Fighter> enemies = source.Enemy ? players : opponents;
            Fighter best = null;
            int bestDistance = int.MaxValue;
            foreach (Fighter candidate in enemies)
            {
                if (candidate == null || candidate == primary || !candidate.Alive || candidate.Tile == null) continue;
                int distance = MonsterTacticsCombatRules.HexDistance(
                    MonsterTacticsCombatRules.Coord(primary.Tile), MonsterTacticsCombatRules.Coord(candidate.Tile));
                if (distance <= radius && distance < bestDistance) { best = candidate; bestDistance = distance; }
            }
            return best;
        }

        private void CastAbility(Fighter source, Fighter target)
        {
            int u610Stage = source.Unit.GetComponent<MonsterTacticsU5Progression>()?.Stage ??
                MonsterTacticsU5Roster.StageOf(source.Name);
            MonsterTacticsCombatRules.AbilityProfile profile;
            if (!MonsterTacticsU610Abilities.TryProfile(MonsterTacticsU5Roster.BaseName(source.Name),
                u610Stage, source.Stats, out profile))
                profile = MonsterTacticsCombatRules.AbilityFor(MonsterTacticsU5Roster.BaseName(source.Name), source.Stats);
            if (MonsterTacticsU5Roster.BaseName(source.Name)=="Quillobor")
                profile.PrimaryRaw+=source.MaxHP*.05f;
            else if (MonsterTacticsU5Roster.BaseName(source.Name)=="Jaspvar")
                profile.PrimaryRaw+=source.MaxHP*.07f;
            else if(MonsterTacticsU5Roster.BaseName(source.Name)=="Sylvodon" && source.U610GritUsed)
                profile.PrimaryRaw*=1.15f;
            source.Mana = 0;
            OnItemAbilityCast(source);
            string family = MonsterTacticsU5Roster.BaseName(source.Name);
            float windup = family == "Flarbit" ? .45f :
                family == "Emberlet" ? .40f : .31f;
            source.InBasicAttackRecovery = false; // Cast windup is unaffected by AS Slow.
            source.ActionReady = elapsed + (profile.HitCount > 1 ? 1.00f : .82f);

            if (family == "Stormlynx")
                U611StartStormlynxLunge(source, target);

            if (family == "Ripplet")
                DashBesideTarget(source, target);

            if (family == "Dunebit")
            {
                // U6.11.2: no hidden backline retarget during Dragon Rush.
                // In legacy mode preserve its original backline leap.
                if (!U6112EnabledFor(source))
                {
                    Fighter backline = PickBacklineTarget(source);
                    if (backline != null)
                    {
                        target = backline;
                        source.LockedTarget = backline;
                    }
                }
                DashBesideTarget(source, target);
            }

            AddEffect(profile.Name + "!", source.Unit.transform.position + Vector3.up * 1.4f,
                new Color(.95f, .78f, 1f));
            logLine = source.Name + " used " + profile.Name + "!";
            if (abilityVfx != null)
                abilityVfx.PlayCast(source.Unit, target.Unit, profile.Name, windup);

            if (U610AfterCast(source, target, profile, u610Stage)) return;

            // Leaf Blade tests the actual combat hexes at CAST time, not the
            // world-space SpriteRenderer motion. Enemy Monster face the player
            // rows (decreasing Row), friendly Monster face increasing Row.
            // The bonus is snapshotted before a target may move/die.
            if (family == "Spriggit" && source.Tile != null && target.Tile != null &&
                (target.Enemy ? source.Tile.Row > target.Tile.Row :
                    source.Tile.Row < target.Tile.Row))
            {
                profile.PrimaryRaw *= 1.25f;
                AddEffect("BACKSTAB!", target.Unit.transform.position + Vector3.up * 1.1f,
                    new Color(.43f, .95f, .52f));
            }

            // Rock Guard is a protective cast, not an attack. It must not damage the target.
            if (profile.ShieldRaw > 0f)
            {
                int shield = Mathf.Max(1, Mathf.RoundToInt(profile.ShieldRaw *
                    (IsInOvertime ? 1f - overtimeMaxHealReduction * OvertimeProgress : 1f)));
                int previousShield = source.Shield;
                source.Shield = MonsterTacticsCombatRules.RefreshSelfShield(previousShield, shield, source.MaxHP);
                int gainedShield = Mathf.Max(0, source.Shield - previousShield);
                U6115TrackShield(source, gainedShield);
                AddEffect(gainedShield > 0 ? "+" + gainedShield + " SHIELD" : "SHIELD REFRESHED",
                    source.Unit.transform.position + Vector3.up * 1.27f,
                    new Color(.83f, .75f, .51f));
                if (abilityVfx != null) abilityVfx.PlayImpact(source.Unit, profile.Name);
                // Pebblin's Rock Guard is shield-only. Aqualith's Aurora Beam
                // applies its shield AND still schedules its magic hit.
                if (profile.PrimaryRaw <= 0f && profile.SplashRaw <= 0f && profile.HealRaw <= 0f)
                    return;
            }

            for (int i = 0; i < profile.HitCount; i++)
            {
                pendingHits.Add(new ScheduledHit
                {
                    Source = source,
                    Target = target,
                    Profile = profile,
                    ResolveAt = elapsed + windup + (i * .13f),
                    HitIndex = i
                });
            }
        }

        private Fighter PickBacklineTarget(Fighter self)
        {
            List<Fighter> targets = self.Enemy ? players : opponents;
            Fighter best = null;
            int bestDistance = int.MaxValue;
            foreach (Fighter candidate in targets)
            {
                if (!IsValidTarget(self, candidate)) continue;
                // Player attackers seek the highest enemy row; enemy attackers
                // seek the lowest player row. Ties prefer nearby, then low HP.
                int distance = MonsterTacticsCombatRules.HexDistance(
                    MonsterTacticsCombatRules.Coord(self.Tile), MonsterTacticsCombatRules.Coord(candidate.Tile));
                bool farther = best == null || (self.Enemy ?
                    candidate.Tile.Row < best.Tile.Row : candidate.Tile.Row > best.Tile.Row);
                if (farther || (best != null && candidate.Tile.Row == best.Tile.Row &&
                    (distance < bestDistance ||
                        (distance == bestDistance && candidate.HP < best.HP))))
                {
                    best = candidate;
                    bestDistance = distance;
                }
            }
            return best;
        }

        private void DashBesideTarget(Fighter source, Fighter target)
        {
            if (source.Tile == null || target.Tile == null) return;
            HexTile destination = null;
            int closest = int.MaxValue;
            HashSet<Vector2Int> occupied = new HashSet<Vector2Int>();
            foreach (Fighter other in turnList)
                if (other != source && other.Alive && other.Tile != null)
                    occupied.Add(MonsterTacticsCombatRules.Coord(other.Tile));
            foreach (Vector2Int adjacent in MonsterTacticsCombatRules.Neighbors(MonsterTacticsCombatRules.Coord(target.Tile)))
            {
                if (occupied.Contains(adjacent) || !board.TryGetValue(adjacent, out HexTile tile))
                    continue;
                int distance = MonsterTacticsCombatRules.HexDistance(MonsterTacticsCombatRules.Coord(source.Tile), adjacent);
                if (distance < closest) { closest = distance; destination = tile; }
            }
            if (destination == null || destination == source.Tile) return;
            source.SlideFrom = source.Unit.transform.position;
            source.Tile = destination;
            source.Unit.PlaceOnTile(destination);
            source.SlideTo = source.Unit.transform.position;
            source.Unit.transform.position = source.SlideFrom;
            source.SlideStart = elapsed;
            source.SlideEnd = elapsed + .26f;
            source.MoveReady = elapsed + .28f;
        }

        private Fighter U6117PlayRoughRetarget(Fighter source, Fighter original)
        {
            if (source == null) return null;
            List<Fighter> enemyTeam = source.Enemy ? players : opponents;
            HexTile origin = original != null && original.Tile != null ? original.Tile : source.Tile;
            Fighter best = null;
            int bestDistance = int.MaxValue;
            foreach (Fighter candidate in enemyTeam)
            {
                if (!IsValidTarget(source, candidate)) continue;
                int distance = origin != null
                    ? MonsterTacticsCombatRules.HexDistance(
                        MonsterTacticsCombatRules.Coord(origin), MonsterTacticsCombatRules.Coord(candidate.Tile))
                    : 0;
                if (best == null || distance < bestDistance ||
                    (distance == bestDistance && candidate.HP < best.HP))
                {
                    best = candidate;
                    bestDistance = distance;
                }
            }
            return best;
        }

        private void ResolvePendingHits()
        {
            for (int i = pendingHits.Count - 1; i >= 0; i--)
            {
                ScheduledHit hit = pendingHits[i];
                if (elapsed < hit.ResolveAt) continue;
                pendingHits.RemoveAt(i);
                // U6.11.7 Play Rough: the second actual hit never vanishes just
                // because its first target was knocked out. Retarget to a living
                // opponent closest to the original victim (then lowest HP).
                // No extra target is hit if none remain.
                if (hit.HitIndex == 1 && hit.Profile.Name == "Play Rough" &&
                    hit.Source != null && (hit.Target == null || !hit.Target.Alive))
                {
                    Fighter replacement = U6117PlayRoughRetarget(hit.Source, hit.Target);
                    if (replacement != null)
                    {
                        hit.Target = replacement;
                        hit.Source.LockedTarget = replacement;
                        AddEffect("PLAY ROUGH RETARGET", replacement.Unit.transform.position + Vector3.up * 1.24f,
                            new Color(1f, .50f, .86f));
                    }
                }
                if (hit.Target == null || !hit.Target.Alive) continue;
                bool magical = hit.Profile.DamageKind == "Magic";
                if (magical && hit.Source != null && HasItemPassive(hit.Source, 13))
                {
                    AddResistanceEffect(hit.Target, true, .30f, 4f, hit.Source, "VeiledWand");
                }
                float itemModifiedRaw = hit.Profile.PrimaryRaw;
                bool itemSpellCrit = hit.Source != null &&
                    ((magical && HasItemPassive(hit.Source, 14) && UnityEngine.Random.value < .25f) ||
                     (!magical && HasItemPassive(hit.Source, 7) && UnityEngine.Random.value < hit.Source.Stats.CriticalChance));
                if (itemSpellCrit) itemModifiedRaw *= 1.40f;
                DealDamage(hit.Target, itemModifiedRaw, magical, hit.Source,
                    itemSpellCrit ? "SPELL CRIT" : hit.Profile.Name == "Play Rough" ? "PLAY ROUGH" :
                    hit.Profile.HitCount > 1 ? "SHURIKEN" : hit.Profile.Name.ToUpperInvariant());
                // Blight Staff is the offensive Wound/Burn option, applied on magic spell hits.
                // Status refreshes but cannot stack with Ironbark, Pyrothane or other sources.
                if (magical && hit.Source != null && HasItemPassive(hit.Source, 11) &&
                    itemModifiedRaw > 0f && hit.Target.Alive)
                    ApplyItemWoundBurn(hit.Target, 4f, hit.Source);
                if (abilityVfx != null && hit.Target.Unit != null)
                    abilityVfx.PlayImpact(hit.Target.Unit, hit.Profile.Name);
                if (hit.HitIndex == 0 && hit.Source != null)
                    U610AfterSpellHit(hit.Source, hit.Target, hit.Profile);
                if (hit.HitIndex != 0) continue;
                ApplyAbilityDebuffs(hit.Target, hit.Profile, hit.Source);
                if (hit.Profile.SplashRaw > 0f)
                    Splash(hit.Source, hit.Target, hit.Profile.SplashRaw,
                        hit.Profile.MaximumSecondaryTargets, magical, hit.Profile);
                if (hit.Profile.HealRaw > 0f && hit.Source.Alive)
                {
                    Heal(hit.Source, Mathf.RoundToInt(hit.Profile.HealRaw));
                    if (abilityVfx != null) abilityVfx.PlayHealing(hit.Source.Unit);
                }
            }
        }

        private void Splash(Fighter source, Fighter primary, float raw, int maxTargets, bool magical, MonsterTacticsCombatRules.AbilityProfile profile)
        {
            List<Fighter> victims = source.Enemy ? players : opponents;
            int hitCount = 0;
            foreach (Fighter secondary in victims)
            {
                if (hitCount >= maxTargets) break;
                if (secondary == primary || !secondary.Alive) continue;
                if (MonsterTacticsCombatRules.HexDistance(
                    MonsterTacticsCombatRules.Coord(primary.Tile), MonsterTacticsCombatRules.Coord(secondary.Tile)) <= profile.SplashRadius)
                {
                    if (magical && HasItemPassive(source, 13))
                    {
                        AddResistanceEffect(secondary, true, .30f, 4f, source, "VeiledWand");
                    }
                    if (magical && HasItemPassive(source, 11) && raw > 0f)
                        ApplyItemWoundBurn(secondary, 4f, source);
                    bool spellCrit = (magical && HasItemPassive(source, 14) && UnityEngine.Random.value < .25f) ||
                        (!magical && HasItemPassive(source, 7) && UnityEngine.Random.value < source.Stats.CriticalChance);
                    DealDamage(secondary, spellCrit ? raw * 1.40f : raw, magical, source,
                        spellCrit ? "SPELL CRIT" : "SPLASH");
                    ApplyAbilityDebuffs(secondary, profile, source);
                    U610AfterSpellHit(source, secondary, profile);
                    hitCount++;
                }
            }
        }

        private void ApplyAbilityDebuffs(Fighter target, MonsterTacticsCombatRules.AbilityProfile profile, Fighter source = null)
        {
            if (target == null || !target.Alive) return;
            if (!target.MagmaCoreEnabled && profile.BurnDuration > 0f &&
                profile.BurnMaxHpPerSecond > 0f)
            {
                if (target.BurnUntil <= elapsed) target.BurnPercent = 0f;
                if (target.BurnUntil <= elapsed || profile.BurnMaxHpPerSecond >= target.BurnPercent)
                    target.U6115BurnOwner = source;
                target.BurnPercent = Mathf.Max(target.BurnPercent, profile.BurnMaxHpPerSecond);
                target.BurnUntil = elapsed + profile.BurnDuration;
                target.NextBurnTick = elapsed + 1f;
            }
            if (profile.WoundDuration > 0f)
                target.WoundUntil = elapsed + profile.WoundDuration;
        }

        private void TickBurns()
        {
            foreach (Fighter target in turnList)
            {
                if (!target.Alive || target.BurnPercent <= 0f ||
                    target.MagmaCoreEnabled) continue;
                // Catch up on missed tick boundaries if the game has a slow frame.
                // A 5s Burn should deliver up to five 1-second ticks, never extra.
                int safety = 0;
                while (target.Alive && elapsed + .0001f >= target.NextBurnTick &&
                       target.NextBurnTick <= target.BurnUntil + .0001f && safety++ < 8)
                {
                    target.NextBurnTick += 1f;
                    int before = target.HP;
                    // True damage bypasses Armor/MR, but shields, incoming-damage
                    // reduction and insufficient remaining HP can lower real HP loss.
                    DealDamage(target, target.MaxHP * target.BurnPercent,
                        true, null, "BURN", true);
                    int actual = Mathf.Max(0, before - target.HP);
                    target.BurnTicksApplied++;
                    target.LastBurnHpDamage = actual;
                    target.TotalBurnHpDamage += actual;
                }
            }
        }

        private void DealDamage(Fighter target, float raw, bool magical, Fighter attacker, string label, bool ignoresResistance = false)
        {
            if (!target.Alive) return;
            // A target can be both Sundered and Shredded (different debuffs).
            // Same-type applications never stack; both use a 30% maximum.
            int baseDefense = magical ? target.Stats.MagicResist : target.Stats.Armor;
            float reduction = magical
                ? (ActiveResistanceFraction(target, true))
                : (ActiveResistanceFraction(target, false));
            int defense = ignoresResistance ? 0 : MonsterTacticsCombatRules.EffectiveResistance(baseDefense, reduction);
            float modifiedRaw = raw * EffectiveDamageMultiplier(attacker) *
                U610BonusVsShredded(attacker, target) * U612EonDamageFactor(attacker);
            if (attacker != null && HasItemPassive(attacker, 6) && target.HP > target.MaxHP * .60f)
                modifiedRaw *= 1.12f;
            // Overtime progressively increases damage for both teams, without
            // permanently modifying each unit's authored combat stats.
            if (IsInOvertime)
                modifiedRaw *= OvertimeMultiplier(OvertimeProgress, overtimeMaxDamage);
            int damage = Mathf.Max(1, Mathf.RoundToInt(
                MonsterTacticsCombatRules.Mitigate(modifiedRaw, defense) * target.Stats.IncomingDamageMultiplier));
            if (HasItemPassive(target, 19) && target.ItemStacks[19] < 2)
            {
                target.ItemStacks[19]++;
                damage = Mathf.Max(1, Mathf.RoundToInt(damage * .70f));
            }
            int absorbed = Mathf.Min(target.Shield, damage);
            target.Shield -= absorbed;
            // Attribute shield absorption to exactly one shield source at a
            // time. Otherwise expiry of multiple timed shields can remove HP
            // protection belonging to a different active shield.
            int shieldRemainder = absorbed;
            int fromProtection = Mathf.Min(target.TemporaryDefenseShield, shieldRemainder);
            target.TemporaryDefenseShield -= fromProtection;
            shieldRemainder -= fromProtection;
            int fromRescue = Mathf.Min(target.AugmentRescueShield, shieldRemainder);
            target.AugmentRescueShield -= fromRescue;
            shieldRemainder -= fromRescue;
            int fromCenter = Mathf.Min(target.CenterShield, shieldRemainder);
            target.CenterShield -= fromCenter;
            shieldRemainder -= fromCenter;
            int fromItem = Mathf.Min(target.TimedItemShield, shieldRemainder);
            target.TimedItemShield -= fromItem;
            // Only damage that actually removes HP can heal the attacker.
            // This excludes absorbed shield damage and damage beyond remaining HP.
            int healthDamage = Mathf.Min(target.HP, damage - absorbed);
            target.HP -= healthDamage;
            U6115TrackDamage(attacker, target, healthDamage, absorbed, label);
            U610AfterDamageReceived(target);
            if (!target.Enemy && target.HP > 0 && healthDamage > 0)
            {
                if (!target.WeaknessPolicyUsed && augmentWeaknessAd > 0 &&
                    target.HP < target.MaxHP * .50f)
                {
                    target.WeaknessPolicyUsed = true;
                    target.Stats.Attack *= 1f + augmentWeaknessAd / 100f;
                    target.Stats.AbilityPower += augmentWeaknessAp;
                    AddEffect("WEAKNESS POLICY", target.Unit.transform.position + Vector3.up * 1.7f,
                        new Color(1f, .76f, .35f));
                }
                if (!target.SecondWindUsed && augmentSecondWindPercent > 0 &&
                    target.HP < target.MaxHP * .40f)
                {
                    target.SecondWindUsed = true;
                    target.SecondWindUntil = elapsed + augmentSecondWindSeconds;
                    target.SecondWindHealPerSecond = target.MaxHP *
                        (augmentSecondWindPercent / 100f) / Mathf.Max(.1f, augmentSecondWindSeconds);
                    AddEffect("SECOND WIND", target.Unit.transform.position + Vector3.up * 1.7f,
                        new Color(.45f, 1f, .68f));
                }
            }
            // A critical player strike applies the new, independently timed 15%
            // Grievous Wounds. Existing stronger 33% Wounds are never weakened.
            if (attacker != null && !attacker.Enemy && attacker != target &&
                augmentCritWoundReduction > 0f && healthDamage > 0 &&
                !string.IsNullOrEmpty(label) &&
                label.IndexOf("CRIT", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                target.AugmentWoundUntil = Mathf.Max(target.AugmentWoundUntil,
                    elapsed + augmentCritWoundDuration);
                target.AugmentWoundReduction = Mathf.Max(target.AugmentWoundReduction,
                    augmentCritWoundReduction);
            }
            // First threshold crossing once per living allied unit each fight.
            // The triggering hit resolves normally; the shield protects FUTURE hits.
            if (!target.Enemy && !target.AugmentRescueUsed &&
                augmentRescueShield > 0 && target.HP > 0 &&
                target.HP < target.MaxHP * augmentRescueThreshold / 100f)
            {
                target.AugmentRescueUsed = true;
                int granted = Mathf.Min(augmentRescueShield, Mathf.Max(0, target.MaxHP - target.Shield));
                target.Shield += granted;
                target.AugmentRescueShield += granted;
                U6115TrackShield(target, granted);
                target.AugmentRescueUntil = elapsed + augmentRescueDuration;
                if (granted > 0 && target.Unit != null)
                    AddEffect("ESCAPE ROPE +" + granted + " SHIELD",
                        target.Unit.transform.position + Vector3.up * 1.50f,
                        new Color(.48f, .90f, .95f));
            }
            if (attacker != null && attacker.Unit != null)
            {
                beams.Add(new BeamEffect
                {
                    Start = attacker.Unit.transform.position + Vector3.up * .90f,
                    End = target.Unit.transform.position + Vector3.up * .90f,
                    Color = magical ? new Color(.80f, .40f, 1f) : new Color(1f, .79f, .40f),
                    Born = elapsed
                });
            }
            target.Unit.SetHealth(target.HP);
            if (healthDamage > 0) OnItemDamageTaken(target, attacker, healthDamage,
                magical, label != null && label != "BURN" && label != "SPIKED PLATE");
            // Omnivamp works for basic attacks, abilities, multihits and splash
            // because all of them resolve through DealDamage. No overhealing.
            if (attacker != null && attacker.Alive && healthDamage > 0)
            {
                int vampHeal = MonsterTacticsCombatRules.OmnivampHealing(
                    healthDamage, attacker.Stats.Omnivamp);
                if (vampHeal > 0) Heal(attacker, vampHeal);
            }
            if (attacker != null && attacker.Alive && healthDamage > 0 &&
                string.Equals(label, "SAVAGE BITE", StringComparison.OrdinalIgnoreCase))
                Heal(attacker, Mathf.RoundToInt(healthDamage * .25f));
            target.Mana = Mathf.Min(target.Stats.ManaCost, target.Mana + Mathf.RoundToInt(8f * target.Stats.ManaGainMultiplier));
            AddEffect("-" + healthDamage + (absorbed > 0 ? "  (" + absorbed + " shield)" : "") +
                (label == null ? "" : " " + label),
                target.Unit.transform.position + Vector3.up * 1.28f,
                magical ? new Color(.89f, .60f, 1f) : new Color(1f, .86f, .48f));
            if (target.HP == 0)
            {
                AddEffect("FAINTED", target.Unit.transform.position + Vector3.up * 1.55f, Color.red);
                target.Unit.gameObject.SetActive(false);
                logLine = target.Name + (target.Enemy ? " (enemy)" : " (ally)") + " fainted!";
            }
        }

        private void TickU697CombatEffects(float delta)
        {
            foreach (var ally in players)
            {
                if (ally == null) continue;
                if (ally.CenterShield > 0 && elapsed >= ally.CenterShieldUntil)
                {
                    ally.Shield = Mathf.Max(0, ally.Shield - ally.CenterShield);
                    ally.CenterShield = 0;
                }
                if (!ally.Alive || !ally.SecondWindUsed) continue;
                float seconds = Mathf.Min(delta,
                    Mathf.Max(0f, ally.SecondWindUntil - (elapsed - delta)));
                if (seconds <= 0f) continue;
                ally.SecondWindHealCarry += seconds * ally.SecondWindHealPerSecond;
                int healing = Mathf.FloorToInt(ally.SecondWindHealCarry);
                if (healing <= 0) continue;
                ally.SecondWindHealCarry -= healing;
                Heal(ally, healing);
            }
        }

        private void TickAugmentRescueShields()
        {
            foreach (var fighter in players)
            {
                if (fighter.AugmentRescueShield <= 0 || elapsed < fighter.AugmentRescueUntil) continue;
                fighter.Shield = Mathf.Max(0, fighter.Shield - fighter.AugmentRescueShield);
                fighter.AugmentRescueShield = 0;
            }
        }

        private static bool HasItemPassive(Fighter fighter, int id)
        {
            return fighter != null && id >= 0 && id < fighter.ItemPassive.Length && fighter.ItemPassive[id];
        }

        // Called after trait and component stats are locked in for this round.
        private void InitializeItemPassives(Fighter fighter)
        {
            if (HasItemPassive(fighter, 28))
            {
                int oldShield = fighter.Shield;
                GrantItemShield(fighter, fighter.MaxHP * .16f, "PROTECTED GROWTH");
                fighter.TimedItemShield = Mathf.Max(0, fighter.Shield - oldShield);
                fighter.TimedItemShieldUntil = elapsed + 6f;
            }
            if (HasItemPassive(fighter, 32))
            {
                if (UnityEngine.Random.value < .5f)
                {
                    fighter.Stats.DamageMultiplier *= 1.12f;
                    AddEffect("+12% DAMAGE", fighter.Unit.transform.position + Vector3.up * 1.30f, Color.yellow);
                }
                else
                {
                    fighter.Stats.Omnivamp = Mathf.Clamp01(fighter.Stats.Omnivamp + .12f);
                    AddEffect("+12% OMNIVAMP", fighter.Unit.transform.position + Vector3.up * 1.30f, Color.green);
                }
            }
            if (HasItemPassive(fighter, 8)) fighter.ItemTimers[8] = elapsed + 5f;
            if (HasItemPassive(fighter, 22)) fighter.ItemTimers[22] = elapsed + 4f;
            if (HasItemPassive(fighter, 27)) fighter.ItemTimers[27] = elapsed + 7f;
        }

        private void GrantItemShield(Fighter fighter, float amount, string label)
        {
            if (fighter == null || !fighter.Alive) return;
            int shield = Mathf.Max(1, Mathf.RoundToInt(amount *
                (IsInOvertime ? 1f - overtimeMaxHealReduction * OvertimeProgress : 1f)));
            int before = fighter.Shield;
            fighter.Shield = MonsterTacticsCombatRules.RefreshSelfShield(before, shield, fighter.MaxHP);
            int gain = Mathf.Max(0, fighter.Shield - before);
            U6115TrackShield(fighter, gain);
            if (gain > 0) AddEffect("+" + gain + " " + label,
                fighter.Unit.transform.position + Vector3.up * 1.32f, new Color(.45f,.85f,1f));
        }

        private Fighter LowestHealthAlly(Fighter fighter)
        {
            List<Fighter> allies = fighter.Enemy ? opponents : players;
            Fighter best = null;
            float lowest = float.MaxValue;
            foreach (Fighter ally in allies)
            {
                if (ally == null || !ally.Alive) continue;
                float fraction = ally.HP / (float)Mathf.Max(1, ally.MaxHP);
                if (fraction < lowest) { lowest = fraction; best = ally; }
            }
            return best;
        }

        private Fighter NearestEnemy(Fighter fighter, int radius)
        {
            List<Fighter> foes = fighter.Enemy ? players : opponents;
            Fighter best = null;
            int bestDist = int.MaxValue;
            if (fighter.Tile == null) return null;
            foreach (Fighter enemy in foes)
            {
                if (enemy == null || !enemy.Alive || enemy.Tile == null) continue;
                int distance = MonsterTacticsCombatRules.HexDistance(
                    MonsterTacticsCombatRules.Coord(fighter.Tile), MonsterTacticsCombatRules.Coord(enemy.Tile));
                if (distance <= radius && distance < bestDist) { best = enemy; bestDist = distance; }
            }
            return best;
        }

        private void ApplyItemWoundBurn(Fighter target, float duration, Fighter owner = null)
        {
            if (target == null || !target.Alive) return;
            target.WoundUntil = Mathf.Max(target.WoundUntil, elapsed + duration);
            // Burn immunity does NOT confer Wound immunity.
            if (target.MagmaCoreEnabled) return;
            // Existing stronger Burn (e.g. Pyrothane) wins; preserve next tick on refresh.
            if (target.BurnUntil <= elapsed || target.BurnPercent <= 0f)
                target.NextBurnTick = elapsed + 1f;
            if (target.BurnUntil <= elapsed || .005f >= target.BurnPercent)
                target.U6115BurnOwner = owner;
            target.BurnPercent = Mathf.Max(target.BurnUntil > elapsed ? target.BurnPercent : 0f, .005f);
            target.BurnUntil = Mathf.Max(target.BurnUntil, elapsed + duration);
        }

        private void ApplyItemResistanceAuras(Fighter source)
        {
            bool sunderAura = HasItemPassive(source, 25); // Spiked Plate (frontline)
            bool shredAura = HasItemPassive(source, 33);  // Mystic Barrier (frontline)
            if ((!sunderAura && !shredAura) || source.Tile == null) return;
            List<Fighter> enemies = source.Enemy ? players : opponents;
            foreach (Fighter foe in enemies)
            {
                if (foe == null || !foe.Alive || foe.Tile == null) continue;
                int hexes = MonsterTacticsCombatRules.HexDistance(
                    MonsterTacticsCombatRules.Coord(source.Tile), MonsterTacticsCombatRules.Coord(foe.Tile));
                if (hexes > 2) continue;
                // 0.25s grace only. Leaving the aura removes its own debuff rapidly;
                // an attack-based 4s application is never shortened by this aura.
                if (sunderAura)
                {
                    AddResistanceEffect(foe, false, .30f, .25f, source, "SpikedPlateAura");
                }
                if (shredAura)
                {
                    AddResistanceEffect(foe, true, .30f, .25f, source, "MysticBarrierAura");
                }
            }
        }

        private void TickItemPassives()
        {
            foreach (Fighter fighter in turnList)
            {
                if (fighter == null || !fighter.Alive) continue;
                ApplyItemResistanceAuras(fighter);
                if (fighter.TimedItemShieldUntil > 0f && elapsed >= fighter.TimedItemShieldUntil)
                {
                    fighter.Shield = Mathf.Max(0, fighter.Shield - fighter.TimedItemShield);
                    fighter.TimedItemShield = 0;
                    fighter.TimedItemShieldUntil = 0f;
                }
                if (HasItemPassive(fighter, 8) && fighter.ItemStacks[8] < 6 && elapsed >= fighter.ItemTimers[8])
                {
                    fighter.ItemStacks[8]++;
                    fighter.Stats.AbilityPower += 4f;
                    fighter.ItemTimers[8] += 5f;
                }
                if (HasItemPassive(fighter, 9) && fighter.ItemFlags[9] && elapsed >= fighter.ItemTimers[9])
                {
                    fighter.Stats.AttackSpeed /= 1.20f;
                    fighter.ItemFlags[9] = false;
                }
                if (HasItemPassive(fighter, 22) && elapsed >= fighter.ItemTimers[22])
                {
                    fighter.ItemTimers[22] += 4f;
                    Fighter enemy = NearestEnemy(fighter, 2);
                    if (enemy != null)
                    {
                        ApplyItemWoundBurn(enemy, 5f, fighter);
                        AddEffect("WOUND + BURN", enemy.Unit.transform.position + Vector3.up * 1.1f, new Color(1f, .55f, .24f));
                    }
                }
                if (HasItemPassive(fighter, 24) && fighter.ItemFlags[24] && elapsed >= fighter.ItemTimers[24])
                {
                    fighter.Stats.Armor -= 10;
                    fighter.Stats.MagicResist -= 10;
                    fighter.ItemFlags[24] = false;
                }
                if (HasItemPassive(fighter, 27) && elapsed >= fighter.ItemTimers[27])
                {
                    fighter.ItemTimers[27] += 7f;
                    Fighter ally = LowestHealthAlly(fighter);
                    if (ally != null) Heal(ally, Mathf.Min(90, Mathf.Max(1, Mathf.RoundToInt(fighter.MaxHP * .06f))), fighter);
                }
                if (HasItemPassive(fighter, 30) && fighter.ItemFlags[30] && elapsed >= fighter.ItemTimers[30])
                {
                    fighter.ItemFlags[30] = false;
                    fighter.Stats.AttackSpeed /= 1.15f;
                }
            }
        }

        private void OnItemBasicAttack(Fighter source, Fighter target, bool critical)
        {
            if (source == null || !source.Alive) return;
            if (HasItemPassive(source, 3) && target.Alive)
            {
                AddResistanceEffect(target, false, .30f, 4f, source, "IronFang");
            }
            if (HasItemPassive(source, 15) && source.ItemStacks[15] < 10)
            {
                int old = source.ItemStacks[15]++;
                source.Stats.AttackSpeed *= (1f + source.ItemStacks[15] * .025f) / (1f + old * .025f);
            }
            if (HasItemPassive(source, 16) && source.ItemStacks[16] < 7)
            {
                source.ItemStacks[16]++;
                source.Stats.Armor += 2;
                source.Stats.MagicResist += 2;
            }
            if (HasItemPassive(source, 17) && source.ItemAttackCount % 3 == 0)
                Heal(source, Mathf.Min(45, Mathf.Max(1, Mathf.RoundToInt(source.MaxHP * .02f))));
            if (HasItemPassive(source, 20) && source.ItemStacks[20] < 8)
            {
                source.ItemStacks[20]++;
                source.Stats.CriticalChance = Mathf.Clamp01(source.Stats.CriticalChance + .02f);
            }
            if (HasItemPassive(source, 29) && source.ItemAttackCount % 4 == 0)
            {
                Fighter ally = LowestHealthAlly(source);
                if (ally != null) Heal(ally, Mathf.Min(70, Mathf.Max(1, Mathf.RoundToInt(source.MaxHP * .04f))), source);
            }
        }

        private void OnItemAbilityCast(Fighter source)
        {
            if (HasItemPassive(source, 1)) source.ItemFlags[1] = true;
            if (HasItemPassive(source, 5)) source.ItemFlags[5] = true;
            if (HasItemPassive(source, 9))
            {
                if (!source.ItemFlags[9]) { source.ItemFlags[9] = true; source.Stats.AttackSpeed *= 1.20f; }
                source.ItemTimers[9] = elapsed + 4f;
            }
            if (HasItemPassive(source, 10) && elapsed >= source.ItemTimers[10])
            {
                source.ItemTimers[10] = elapsed + 6f;
                GrantItemShield(source, source.MaxHP * .08f, "ARCANE GUARD");
            }
            if (HasItemPassive(source, 12) && elapsed >= source.ItemTimers[12])
            {
                source.Mana = Mathf.Min(source.Stats.ManaCost, source.Mana + 10);
                source.ItemTimers[12] = elapsed + 3f;
            }
            if (HasItemPassive(source, 30))
            {
                // ItemStacks distinguishes 'used once' from an active timed buff.
                if (source.ItemStacks[30] == 0)
                {
                    source.ItemStacks[30] = 1;
                    source.ItemFlags[30] = true;
                    source.Stats.AttackSpeed *= 1.15f;
                    source.ItemTimers[30] = elapsed + 5f;
                    source.Mana = Mathf.Min(source.Stats.ManaCost, source.Mana + 10);
                }
            }
            if (HasItemPassive(source, 31) && elapsed >= source.ItemTimers[31])
            {
                source.ItemTimers[31] = elapsed + 6f;
                Fighter ally = LowestHealthAlly(source);
                if (ally != null) GrantItemShield(ally, source.MaxHP * .10f, "MIND SHIELD");
            }
        }

        private void OnItemDamageTaken(Fighter victim, Fighter attacker, int hpLost,
            bool magical, bool wasCriticalOrSpell)
        {
            if (victim == null || !victim.Alive) return;
            if (HasItemPassive(victim, 4) && !victim.ItemFlags[4] && victim.HP < victim.MaxHP * .40f)
            {
                victim.ItemFlags[4] = true;
                GrantItemShield(victim, victim.MaxHP * .15f, "LIFE DRINKER");
            }
            if (HasItemPassive(victim, 21) && elapsed >= victim.ItemTimers[21])
            {
                victim.ItemTimers[21] = elapsed + 4f;
                List<Fighter> foes = victim.Enemy ? players : opponents;
                foreach (Fighter foe in foes)
                {
                    if (foe == null || !foe.Alive || foe.Tile == null || victim.Tile == null) continue;
                    if (MonsterTacticsCombatRules.HexDistance(MonsterTacticsCombatRules.Coord(victim.Tile),
                        MonsterTacticsCombatRules.Coord(foe.Tile)) <= 1)
                        DealDamage(foe, 25f, true, null, "STONEWALL");
                }
            }
            if (HasItemPassive(victim, 23) && victim.ItemStacks[23] < 10)
            {
                victim.ItemStacks[23]++;
                victim.Stats.Armor += 1;
                victim.Stats.MagicResist += 1;
            }
            if (HasItemPassive(victim, 24) && victim.ItemStacks[24] == 0 && victim.HP < victim.MaxHP * .50f)
            {
                victim.ItemFlags[24] = true;
                victim.ItemStacks[24] = 1;
                victim.ItemTimers[24] = elapsed + 5f;
                victim.Stats.Armor += 10;
                victim.Stats.MagicResist += 10;
                GrantItemShield(victim, victim.MaxHP * .18f, "BALANCED SHIELD");
            }
            if (HasItemPassive(victim, 34) && wasCriticalOrSpell && elapsed >= victim.ItemTimers[34])
            {
                victim.ItemTimers[34] = elapsed + 10f;
                GrantItemShield(victim, victim.MaxHP * .08f, "REFLEX WARD");
            }
        }

        private void Heal(Fighter fighter, int amount, Fighter healer = null)
        {
            if (!fighter.Alive) return;
            float regular = fighter.WoundUntil > elapsed ?
                MonsterTacticsCombatRules.WoundHealReduction : 0f;
            float augmentWound = fighter.AugmentWoundUntil > elapsed ?
                fighter.AugmentWoundReduction : 0f;
            int received = Mathf.Max(0, Mathf.RoundToInt(Mathf.Max(0, amount) *
                (1f - Mathf.Max(regular, augmentWound))));
            if (IsInOvertime)
                received = Mathf.RoundToInt(received *
                    (1f - overtimeMaxHealReduction * OvertimeProgress));
            int gained = Mathf.Min(received, fighter.MaxHP - fighter.HP);
            if (gained <= 0) return;
            fighter.HP += gained;
            U6115TrackHealing(healer ?? fighter, gained);
            fighter.Unit.SetHealth(fighter.HP);
            AddEffect("+" + gained + " HP", fighter.Unit.transform.position + Vector3.up * 1.2f,
                new Color(.43f, 1f, .53f));
        }

        private void AddEffect(string text, Vector3 position, Color color)
        {
            effects.Add(new TextEffect { Text = text, Position = position, Color = color, Start = elapsed });
        }

        private static int CountAlive(List<Fighter> team)
        {
            int count = 0;
            foreach (Fighter fighter in team) if (fighter.Alive) count++;
            return count;
        }

        private static float HealthScore(List<Fighter> team)
        {
            float score = 0f;
            foreach (Fighter fighter in team) score += (float)fighter.HP / fighter.MaxHP;
            return score;
        }

        private static void ClearDebuffs(Fighter fighter)
        {
            // Remove temporary protection before clearing Magma Core so both
            // systems unwind their own stat contributions exactly once.
            RemoveTemporaryDefenseBuff(fighter);
            RemoveTemporaryShield(fighter);
            fighter.SunderSources.Clear();
            fighter.ShredSources.Clear();
            fighter.WoundUntil = 0f;
            fighter.AugmentWoundUntil = 0f;
            fighter.AugmentWoundReduction = 0f;
            fighter.BurnUntil = 0f;
            fighter.BurnPercent = 0f;
            fighter.U6115BurnOwner = null;
            fighter.NextBurnTick = 0f;
            fighter.AttackSpeedSlow = 0f;
            fighter.AttackSpeedSlowUntil = 0f;
            fighter.ShadowThiefSpent = false;
            fighter.ShadowThiefBuffUntil = 0f;
            fighter.ShadowThiefBuffAS = 0f;
            fighter.ShadowThiefBuffDamage = 0f;
            fighter.ShadowThiefDebuffUntil = 0f;
            fighter.ShadowThiefDebuffAS = 0f;
            fighter.ShadowThiefDebuffDamage = 0f;
            fighter.InBasicAttackRecovery = false;
            if (fighter.MagmaCoreEnabled)
            {
                fighter.Stats.Armor += fighter.MagmaCoreConvertedArmor;
                fighter.Stats.MagicResist += fighter.MagmaCoreConvertedMR;
                fighter.Stats.AbilityPower -= fighter.MagmaCoreConvertedAP;
            }
            fighter.MagmaCoreEnabled = false;
            fighter.MagmaCoreConvertedArmor = fighter.MagmaCoreConvertedMR = 0;
            fighter.MagmaCoreConvertedAP = 0f;
            fighter.MagmaCoreBaseArmor = fighter.MagmaCoreBaseMR = 0;
            fighter.MagmaCoreFraction = fighter.MagmaCoreAPCap = 0f;
            // This trait is temporary. Remove exactly the amount we inserted,
            // so the results inspector cannot retain phantom defensive stats.
            if (fighter.IronFortressEnabled)
            {
                fighter.Stats.Armor -= fighter.IronFortressArmorBonus;
                fighter.Stats.MagicResist -= fighter.IronFortressMagicResistBonus;
            }
            fighter.IronFortressEnabled = false;
            fighter.IronFortressHitCounter = 0;
            fighter.IronFortressHitThreshold = 0;
            fighter.IronFortressArmorRatio = 0f;
            fighter.IronFortressArmorBonus = 0;
            fighter.IronFortressMagicResistBonus = 0;
        }

        private void ClearAllDebuffs()
        {
            foreach (Fighter fighter in turnList) ClearDebuffs(fighter);
        }

        private void Finish(string title, string details)
        {
            phase = Phase.Results;
            // TFT-style effects are combat-only: clear immediately at the result,
            // not five seconds later when the next planning phase starts.
            ClearAllDebuffs();
            resultMessage = title;
            logLine = details;
            foreach (Fighter fighter in turnList)
            {
                if (!fighter.Alive || fighter.SlideEnd <= 0f) continue;
                fighter.Unit.transform.position = fighter.SlideTo;
                fighter.SlideEnd = 0f;
            }
            Debug.Log("MonsterTactics U4: " + title + " after " + elapsed.ToString("0.0") + "s — " + details);
        }

        public void ResetBattle()
        {
            ClearAllDebuffs();
            // Restore all U3 positions and HP; destroy only U4-created enemies.
            foreach (KeyValuePair<MonsterUnit, HexTile> pair in originalTiles)
            {
                if (pair.Key == null) continue;
                pair.Key.gameObject.SetActive(true);
                pair.Key.PlaceOnTile(pair.Value);
                pair.Key.ClearTemporaryCombatMaximumHP();
                pair.Key.SetHealth(pair.Key.MaximumHP);
            }
            if (placementController != null) placementController.enabled = placementWasEnabled;
            CleanupBattleObjects();
            previewStage = -1;
            previewRound = -1;
            CurrentOpponentName = "";
            players.Clear();
            opponents.Clear();
            turnList.Clear();
            originalTiles.Clear();
            lockedPlayerTraits = null;
            effects.Clear();
            beams.Clear();
            pendingHits.Clear();
            if (abilityVfx != null) abilityVfx.ClearEffects();
            elapsed = 0f;
            realCombatElapsed = 0f;
            overtimeAnnounced = false;
            WasEmptyTeamForfeit = false;
            phase = Phase.Planning;
            resultMessage = "";
            logLine = "Position your Monster, then start another battle.";
        }

        private void CleanupBattleObjects()
        {
            if (enemyRoot != null)
            {
                Destroy(enemyRoot.gameObject);
                enemyRoot = null;
            }
            spawnedEnemies.Clear();
        }

        private void OnDisable()
        {
            if (phase != Phase.Planning) ResetBattle();
            else ClearPlanningPreview();
        }

        private void OnDestroy()
        {
            if (phase != Phase.Planning) ResetBattle();
            else ClearPlanningPreview();
        }

        private void MakeStyles()
        {
            if (headlineStyle != null) return;
            headlineStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                fontSize = 19
            };
            headlineStyle.normal.textColor = Color.white;
            hudStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 14 };
            hudStyle.normal.textColor = Color.white;
            smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true };
            smallStyle.normal.textColor = new Color(.91f, .95f, 1f);
            effectStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 12,
                alignment = TextAnchor.MiddleCenter
            };
            effectStyle.normal.textColor = Color.white;
        }

        private void OnGUI()
        {
            // Combat effects use effectStyle even when automatic rounds hide the old HUD.
            // Always initialize IMGUI styles before drawing any overlays.
            MakeStyles();
            // U6.2 replaces the manual battle controls, but combat nameplates and
            // ability effects must remain visible throughout the automatic battle.
            if (AutomaticRoundsEnabled)
            {
                if (phase != Phase.Planning || opponents.Count > 0)
                    DrawAutomaticCombatOverlays();
                U6115DrawCombatStats();
                return;
            }
            MakeStyles();
            if (sceneCamera == null) sceneCamera = Camera.main;
            float w = Screen.width;
            float h = Screen.height;
            float left = Mathf.Max(8f, (w - 390f) / 2f);
            Rect banner = new Rect(left, 8f, 390f, 52f);
            GUI.Box(banner, GUIContent.none);
            string status = phase == Phase.Planning ? "PLANNING" :
                phase == Phase.Battle ? "COMBAT" : resultMessage;
            GUI.Label(new Rect(left + 8f, 12f, 375f, 24f), "MONSTERTACTICS U4  ·  " + status, headlineStyle);
            string detail = phase == Phase.Planning ? "Ready when you are" :
                phase == Phase.Battle ? (IsInOvertime ? "OVERTIME +" +
                    Mathf.Max(0f, realCombatElapsed - normalCombatSeconds).ToString("0.0") + "s" :
                    "Combat " + realCombatElapsed.ToString("0.0") + "s") + "  ·  Allies " +
                CountAlive(players) + " / " + players.Count + "  ·  Foes " + CountAlive(opponents) +
                " / " + opponents.Count : logLine;
            GUI.Label(new Rect(left + 9f, 36f, 370f, 17f), detail, smallStyle);

            float buttonX = w - 165f;
            float buttonY = h - 64f;
            if (phase == Phase.Planning)
            {
                if (GUI.Button(new Rect(buttonX, buttonY, 150f, 43f), "START BATTLE"))
                    StartBattle();
            }
            else if (phase == Phase.Results)
            {
                if (GUI.Button(new Rect(buttonX, buttonY, 150f, 43f), "RESET / REPLAY"))
                    ResetBattle();
            }
            U6115DrawCombatStats();
            if (phase != Phase.Planning)
            {
                if (showCombatLog)
                {
                    float logWidth = Mathf.Min(430f, w - 20f);
                    GUI.Box(new Rect(10f, h - 54f, logWidth, 42f), GUIContent.none);
                    GUI.Label(new Rect(20f, h - 49f, logWidth - 20f, 36f), logLine, smallStyle);
                }
                if (sceneCamera != null)
                {
                    var labels = new List<MonsterTacticsNameplates.Entry>();
                    foreach (Fighter fighter in turnList)
                    {
                        if (!fighter.Alive || fighter.Unit == null) continue;
                        labels.Add(new MonsterTacticsNameplates.Entry(
                            fighter.Unit, fighter.Name,
                            fighter.HP / (float)Mathf.Max(1, fighter.MaxHP),
                            fighter.Mana / (float)Mathf.Max(1, fighter.Stats.ManaCost),
                            fighter.Shield / (float)Mathf.Max(1, fighter.MaxHP),
                            fighter.Enemy, true, fighter.Unit.IsSelected,
                            phase == Phase.Battle ? ActiveResistanceRemaining(fighter, false) : 0f,
                            phase == Phase.Battle ? ActiveResistanceRemaining(fighter, true) : 0f,
                            phase == Phase.Battle ? Mathf.Max(0f,
                                Mathf.Max(fighter.WoundUntil, fighter.AugmentWoundUntil) - elapsed) : 0f,
                    phase == Phase.Battle ? Mathf.Max(0f, fighter.BurnUntil - elapsed) : 0f,
                    phase == Phase.Battle ? Mathf.Max(0f, fighter.AttackSpeedSlowUntil - elapsed) : 0f,
                    phase == Phase.Battle ? Mathf.Max(0f, fighter.ShadowThiefDebuffUntil - elapsed) : 0f,
                    phase == Phase.Battle ? Mathf.Max(0f, fighter.TemporaryDefenseUntil - elapsed) : 0f,
                    phase == Phase.Battle ? Mathf.Max(0f, fighter.TemporaryShieldUntil - elapsed) : 0f));
                    }
                    MonsterTacticsNameplates.DrawAll(sceneCamera, labels);
                    foreach (BeamEffect beam in beams) DrawBeam(beam);
                    foreach (TextEffect effect in effects) DrawEffect(effect);
                }
            }
        }

        private void DrawAutomaticCombatOverlays()
        {
            if (sceneCamera == null) sceneCamera = Camera.main;
            if (sceneCamera == null) return;

            var labels = new List<MonsterTacticsNameplates.Entry>();
            foreach (Fighter fighter in turnList)
            {
                if (!fighter.Alive || fighter.Unit == null) continue;
                labels.Add(new MonsterTacticsNameplates.Entry(
                    fighter.Unit, fighter.Name,
                    fighter.HP / (float)Mathf.Max(1, fighter.MaxHP),
                    fighter.Mana / (float)Mathf.Max(1, fighter.Stats.ManaCost),
                    fighter.Shield / (float)Mathf.Max(1, fighter.MaxHP),
                    fighter.Enemy, true, fighter.Unit.IsSelected,
                    phase == Phase.Battle ? ActiveResistanceRemaining(fighter, false) : 0f,
                    phase == Phase.Battle ? ActiveResistanceRemaining(fighter, true) : 0f,
                    phase == Phase.Battle ? Mathf.Max(0f,
                                Mathf.Max(fighter.WoundUntil, fighter.AugmentWoundUntil) - elapsed) : 0f,
                    phase == Phase.Battle ? Mathf.Max(0f, fighter.BurnUntil - elapsed) : 0f,
                    phase == Phase.Battle ? Mathf.Max(0f, fighter.AttackSpeedSlowUntil - elapsed) : 0f,
                    phase == Phase.Battle ? Mathf.Max(0f, fighter.ShadowThiefDebuffUntil - elapsed) : 0f,
                    phase == Phase.Battle ? Mathf.Max(0f, fighter.TemporaryDefenseUntil - elapsed) : 0f,
                    phase == Phase.Battle ? Mathf.Max(0f, fighter.TemporaryShieldUntil - elapsed) : 0f));
            }
            MonsterTacticsNameplates.DrawAll(sceneCamera, labels);
            foreach (BeamEffect beam in beams) DrawBeam(beam);
            foreach (TextEffect effect in effects) DrawEffect(effect);
        }

        private void DrawBeam(BeamEffect beam)
        {
            Vector3 from = sceneCamera.WorldToScreenPoint(beam.Start);
            Vector3 to = sceneCamera.WorldToScreenPoint(beam.End);
            if (from.z <= 0f || to.z <= 0f) return;
            Vector2 a = new Vector2(from.x, Screen.height - from.y);
            Vector2 b = new Vector2(to.x, Screen.height - to.y);
            Vector2 delta = b - a;
            if (delta.sqrMagnitude < 1f) return;
            float age = Mathf.Clamp01((elapsed - beam.Born) / .25f);
            Color old = GUI.color;
            GUI.color = new Color(beam.Color.r, beam.Color.g, beam.Color.b, 1f - age);
            Matrix4x4 matrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, a);
            GUI.DrawTexture(new Rect(a.x, a.y - 2f, delta.magnitude, 4f), Texture2D.whiteTexture);
            GUI.matrix = matrix;
            GUI.color = old;
        }

        private void DrawEffect(TextEffect effect)
        {
            Vector3 projection = sceneCamera.WorldToScreenPoint(effect.Position);
            if (projection.z <= 0f) return;
            float age = Mathf.Clamp01(elapsed - effect.Start);
            Color prev = GUI.color;
            GUI.color = new Color(effect.Color.r, effect.Color.g, effect.Color.b, 1f - age);
            GUI.Label(new Rect(projection.x - 85f, Screen.height - projection.y - 16f - age * 32f,
                170f, 24f), effect.Text, effectStyle);
            GUI.color = prev;
        }

        private static void DrawBar(Rect rect, Color background, Color foreground, float fraction)
        {
            Color previous = GUI.color;
            GUI.color = background;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = foreground;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(fraction), rect.height),
                Texture2D.whiteTexture);
            GUI.color = previous;
        }
    }
}
