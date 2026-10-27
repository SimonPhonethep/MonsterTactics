using System;
using System.Collections.Generic;
using UnityEngine;

namespace MonsterTactics
{
    /// <summary>U6.11.4: Skyweave + Skyshard unique duo, supports player and enemy teams.</summary>
    public sealed partial class MonsterTacticsCombatDirector
    {
        private bool U612HasLivingEonPartner(Fighter source, out Fighter partner)
        {
            partner = null;
            var tuning = MonsterTacticsU612EonProfile.Current;
            if (source == null || !source.Alive || !source.U612EonMember ||
                tuning == null || !tuning.Enabled) return false;
            string own = MonsterTacticsU5Roster.BaseName(source.Name);
            if (own != "Skyweave" && own != "Skyshard") return false;
            string matching = own == "Skyweave" ? "Skyshard" : "Skyweave";
            foreach (Fighter ally in (source.Enemy ? opponents : players))
            {
                if (ally != null && ally != source && ally.Alive && ally.U612EonMember &&
                    MonsterTacticsU5Roster.BaseName(ally.Name) == matching)
                {
                    partner = ally;
                    return true;
                }
            }
            return false;
        }
        // Computed at every damage event. Immediately stops when either sibling faints.
        private float U612EonDamageFactor(Fighter attacker)
        {
            Fighter partner;
            return U612HasLivingEonPartner(attacker, out partner) ?
                1f + Mathf.Max(0f, MonsterTacticsU612EonProfile.Current.BondDamageAmplification) : 1f;
        }
        private void U612OnEonCast(Fighter caster)
        {
            Fighter partner;
            if (!U612HasLivingEonPartner(caster, out partner)) return;
            int gain = Mathf.Max(0, MonsterTacticsU612EonProfile.Current.PartnerManaPerCast);
            // Direct grant; does NOT call a cast or trigger another Eon Bond grant.
            partner.Mana = Mathf.Min(partner.Stats.ManaCost, partner.Mana + gain);
            if (partner.Unit != null)
                AddEffect("EON +" + gain + " MANA",
                    partner.Unit.transform.position + Vector3.up*1.55f,
                    new Color(.78f,.59f,1f));
        }
        private void U612StartSkyshardLusterPurge(Fighter caster, int star)
        {
            var tuning = MonsterTacticsU612EonProfile.Current;
            float boost = star>=3 ? .70f : star==2 ? .45f : .30f;
            float seconds = 5f;
            if (tuning != null)
            {
                boost = star>=3 ? tuning.SkyshardAttackSpeed3 :
                    star==2 ? tuning.SkyshardAttackSpeed2 : tuning.SkyshardAttackSpeed1;
                seconds = tuning.SkyshardBuffSeconds;
            }
            U610StartExtraSpeed(caster, 1f+Mathf.Max(0f,boost), seconds);
            caster.U612SkyshardBuffUntil = elapsed + seconds;
            caster.U612SkyshardAttackCounter = 0;
        }
        private void U612OnSkyshardBasicAttack(Fighter attacker, Fighter target)
        {
            if (attacker == null || !attacker.Alive || attacker.Tile == null ||
                !attacker.U612HasBasicAttackAim || elapsed >= attacker.U612SkyshardBuffUntil) return;
            var tuning = MonsterTacticsU612EonProfile.Current;
            int threshold = tuning != null ? Mathf.Max(1,tuning.SkyshardAttacksPerBeam):3;
            if (++attacker.U612SkyshardAttackCounter % threshold != 0) return;
            var enemies = attacker.Enemy ? players : opponents;
            int stage=attacker.Unit.GetComponent<MonsterTacticsU5Progression>()?.Stage??
                MonsterTacticsU5Roster.StageOf(attacker.Name);
            float raw = (stage>=3?450f:stage==2?230f:140f) +
                attacker.Stats.AbilityPower * .60f;
            int maxDistance=tuning!=null? Mathf.Max(1,tuning.BeamMaximumRangeHexes):5;
            Vector2Int from=MonsterTacticsCombatRules.Coord(attacker.Tile);
            Vector2Int toward=attacker.U612LastBasicAttackAim;
            // Snapshot targets; multiple damaged targets may faint before the next entry.
            var victims=new List<Fighter>();
            foreach (Fighter enemy in enemies)
                if (enemy!=null && enemy.Alive && enemy.Tile!=null &&
                    MonsterTacticsU612EonRules.IsOnPiercingRay(from,toward,
                        MonsterTacticsCombatRules.Coord(enemy.Tile),maxDistance)) victims.Add(enemy);
            // Main attack may have killed the target; still use its last tile as aim.
            foreach (Fighter victim in victims)
            {
                if (!victim.Alive) continue;
                DealDamage(victim,raw,true,attacker,"LUSTER BEAM");
                if (abilityVfx!=null)
                    abilityVfx.PlayU612EonBeam(attacker.Unit,victim.Unit);
            }
        }
        private void U612ShieldLowestHpAlly(Fighter caster,int star)
        {
            var team=caster.Enemy?opponents:players;
            Fighter lowest=null;
            float fraction=float.MaxValue;
            foreach(Fighter ally in team)
            {
                if(ally==null||!ally.Alive)continue;
                float ratio=ally.HP/(float)Mathf.Max(1,ally.MaxHP);
                if(lowest==null || ratio<fraction) { lowest=ally; fraction=ratio; }
            }
            if (lowest==null) return;
            int raw=star>=3?650:star==2?300:180;
            var tuning=MonsterTacticsU612EonProfile.Current;
            float seconds=tuning!=null?tuning.SkyweaveShieldSeconds:3f;
            // Reuse existing reversible temporary-shield pool; don't add infinite shield
            // across frequent Skyweave casts. Stronger shield refreshes the smaller one.
            if (lowest.TemporaryShieldUntil>0f && lowest.TemporaryShieldUntil<=elapsed)
                RemoveTemporaryShield(lowest);
            int shield=Mathf.Min(raw,Mathf.Max(0,lowest.MaxHP-
                (lowest.Shield-lowest.TemporaryDefenseShield)));
            int diff=Mathf.Max(0,shield-lowest.TemporaryDefenseShield);
            lowest.Shield+=diff;
            lowest.TemporaryDefenseShield+=diff;
            U6115TrackShield(caster,diff);
            lowest.TemporaryShieldUntil=Mathf.Max(lowest.TemporaryShieldUntil,
                elapsed+Mathf.Max(.5f,seconds));
            if(lowest.Unit!=null)
                AddEffect("MIST SHIELD +"+diff,
                    lowest.Unit.transform.position+Vector3.up*1.55f,
                    new Color(.94f,.66f,1f));
        }
    }
}
