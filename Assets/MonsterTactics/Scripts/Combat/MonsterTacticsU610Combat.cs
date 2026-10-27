using System;
using System.Collections.Generic;
using UnityEngine;

namespace MonsterTactics
{
    /// <summary>U6.10 encounter logic. Reuses the single Combat Director damage/heal/status pipeline.</summary>
    public sealed partial class MonsterTacticsCombatDirector
    {
        private static bool HasOrigin(Fighter f, string name)
        {
            if (f == null || f.Unit == null) return false;
            foreach (var t in MonsterTacticsTraits.ForUnit(f.Unit))
                if (string.Equals(t, name, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
        private static int Tier(Dictionary<string,int> counts,string name)
        { return MonsterTacticsTraits.ActiveTier(name, counts); }
        private void U610ApplyTraits(Fighter f, Dictionary<string,int> counts)
        {
            // No effect on existing Monster without newly authored origins.
            int bruiser = Tier(counts,"Bruiser");
            if (bruiser>0)
            {
                int bonus = bruiser>=2 ? 150 : 75;
                int bruiserHp = HasOrigin(f,"Bruiser") ? Mathf.RoundToInt(f.MaxHP*(bruiser>=2?.24f:.12f)) : 0;
                f.MaxHP += bonus + bruiserHp; f.HP=f.MaxHP;
                f.Unit.SetTemporaryCombatMaximumHP(f.MaxHP);
            }
            f.U610Swifty = HasOrigin(f,"Swifty") && Tier(counts,"Swifty")>0;
            if (f.U610Swifty) f.Stats.AttackSpeed *= 1.10f;
            f.U610Grit = HasOrigin(f,"Grit") && Tier(counts,"Grit")>0;
            f.U610GritTier=Tier(counts,"Grit");
            f.U610Overdrive=HasOrigin(f,"Overdrive") && Tier(counts,"Overdrive")>0;
            f.U610OverdriveTier=Tier(counts,"Overdrive");
            f.U610Hexcraft=HasOrigin(f,"Hexcraft") && Tier(counts,"Hexcraft")>0;
            f.U610HexcraftTier=Tier(counts,"Hexcraft");
            // Unique 1-unit origin: only the actual deployed Stormlynx can receive it.
            f.U6113PlasmaEngine = MonsterTacticsU5Roster.BaseName(f.Name) == "Stormlynx" &&
                HasOrigin(f, "Plasma Engine") && Tier(counts, "Plasma Engine") > 0;
            f.U6113BasicAttacks = 0;
            string eonName = MonsterTacticsU5Roster.BaseName(f.Name);
            f.U612EonMember = (eonName == "Skyweave" || eonName == "Skyshard") &&
                HasOrigin(f, "Eon Bond") && Tier(counts, "Eon Bond") > 0;
            f.U612SkyshardBuffUntil = 0f;
            f.U612SkyshardAttackCounter = 0;
            // Trait lock-in occurs before phase flips to Battle, so initialize
            // directly here instead of using the in-battle public tester APIs.
            if (HasOrigin(f,"Iron Fortress") && Tier(counts,"Iron Fortress")>0)
            {
                f.IronFortressEnabled=true; f.IronFortressHitCounter=0;
                f.IronFortressHitThreshold=4; f.IronFortressArmorRatio=.60f;
                f.IronFortressArmorBonus=15; f.IronFortressMagicResistBonus=15;
                f.Stats.Armor+=15; f.Stats.MagicResist+=15;
            }
            if (HasOrigin(f,"Magma Core") && Tier(counts,"Magma Core")>0)
            {
                var baseline=MonsterTacticsU5Roster.CombatStats(f.Unit);
                f.MagmaCoreEnabled=true;
                f.MagmaCoreBaseArmor=baseline.Armor; f.MagmaCoreBaseMR=baseline.MagicResist;
                f.MagmaCoreFraction=.40f; f.MagmaCoreAPCap=80f;
                f.BurnUntil=f.BurnPercent=f.NextBurnTick=0f;
                RecalculateMagmaCore(f);
            }
        }
        private void U610AfterDamageReceived(Fighter f)
        {
            if (!f.Alive || !f.U610Grit || f.U610GritUsed || f.HP>=f.MaxHP*.5f) return;
            f.U610GritUsed=true;
            int bonusHp=f.U610GritTier>=2?250:125;
            f.MaxHP+=bonusHp; f.HP+=bonusHp;
            f.Stats.Attack *= f.U610GritTier>=2?1.20f:1.12f;
            f.Stats.Omnivamp=Mathf.Clamp01(f.Stats.Omnivamp+(f.U610GritTier>=2?.15f:.10f));
            f.Unit.SetTemporaryCombatMaximumHP(f.MaxHP);
            f.Unit.SetHealth(f.HP);
            AddEffect("GRIT!",f.Unit.transform.position+Vector3.up*1.6f,new Color(1f,.7f,.4f));
            if (abilityVfx != null) abilityVfx.PlayU610TraitPulse(f.Unit, "Grit");
        }
        private float U610BonusVsShredded(Fighter attacker,Fighter target)
        {
            if (attacker==null || target==null || !attacker.U610Hexcraft || attacker.U610HexcraftTier<2)
                return 1f;
            return ActiveResistanceFraction(target,true)>0f ? 1.10f : 1f;
        }
        private void U610StartExtraSpeed(Fighter f,float factor,float duration)
        {
            if (f.U610ExtraSpeedUntil<=elapsed)
            { f.U610ExtraSpeedFactor=1f; }
            float oldSpeed=EffectiveAttackSpeed(f);
            float newFactor=Mathf.Max(f.U610ExtraSpeedFactor, factor);
            f.Stats.AttackSpeed*=newFactor/Mathf.Max(.001f,f.U610ExtraSpeedFactor);
            f.U610ExtraSpeedFactor=newFactor;
            f.U610ExtraSpeedUntil=Mathf.Max(f.U610ExtraSpeedUntil,elapsed+duration);
            AdjustBasicAttackRecovery(f,oldSpeed,EffectiveAttackSpeed(f));
        }
        private void U610StartOverdrive(Fighter f)
        {
            if (!f.U610Overdrive) return;
            float newDamage=f.U610OverdriveTier>=2?1.20f:1.12f;
            float newSpeed=f.U610OverdriveTier>=2?1.15f:1f;
            if (f.U610OverdriveUntil<=elapsed)
            {f.U610OverdriveDamageFactor=1f;f.U610OverdriveSpeedFactor=1f;}
            float oldSpeed=EffectiveAttackSpeed(f);
            f.Stats.DamageMultiplier*=newDamage/Mathf.Max(.001f,f.U610OverdriveDamageFactor);
            f.Stats.AttackSpeed*=newSpeed/Mathf.Max(.001f,f.U610OverdriveSpeedFactor);
            f.U610OverdriveDamageFactor=newDamage;
            f.U610OverdriveSpeedFactor=newSpeed;
            f.U610OverdriveUntil=elapsed+8f;
            if (abilityVfx != null) abilityVfx.PlayU610TraitPulse(f.Unit, "Overdrive");
            AdjustBasicAttackRecovery(f,oldSpeed,EffectiveAttackSpeed(f));
        }
        private void U610TickTraits()
        {
            foreach (Fighter f in turnList)
            {
                if (!f.Alive) continue;
                if (f.U610Swifty)
                {
                    Fighter target = f.LockedTarget != null && f.LockedTarget.Alive ? f.LockedTarget : PickTarget(f);
                    int distance=target!=null && target.Tile!=null && f.Tile!=null ?
                        MonsterTacticsCombatRules.HexDistance(MonsterTacticsCombatRules.Coord(f.Tile),MonsterTacticsCombatRules.Coord(target.Tile)):0;
                    float next=1f+.03f*Mathf.Clamp(distance,0,5);
                    if (!Mathf.Approximately(next,f.U610SwiftyFactor))
                    {
                        float old=EffectiveAttackSpeed(f);
                        f.Stats.AttackSpeed*=next/Mathf.Max(.001f,f.U610SwiftyFactor);
                        f.U610SwiftyFactor=next;
                        AdjustBasicAttackRecovery(f,old,EffectiveAttackSpeed(f));
                    }
                }
                if (f.U610OverdriveUntil>0 && elapsed>=f.U610OverdriveUntil)
                {
                    float old=EffectiveAttackSpeed(f);
                    f.Stats.DamageMultiplier/=Mathf.Max(.001f,f.U610OverdriveDamageFactor);
                    f.Stats.AttackSpeed/=Mathf.Max(.001f,f.U610OverdriveSpeedFactor);
                    f.U610OverdriveDamageFactor=f.U610OverdriveSpeedFactor=1f;
                    f.U610OverdriveUntil=0f;
                    AdjustBasicAttackRecovery(f,old,EffectiveAttackSpeed(f));
                }
                if (f.U610ExtraSpeedUntil>0f && elapsed>=f.U610ExtraSpeedUntil)
                {
                    float old=EffectiveAttackSpeed(f);
                    f.Stats.AttackSpeed/=Mathf.Max(.001f,f.U610ExtraSpeedFactor);
                    f.U610ExtraSpeedUntil=0f;f.U610ExtraSpeedFactor=1f;
                    AdjustBasicAttackRecovery(f,old,EffectiveAttackSpeed(f));
                }
                if (f.U610BarrageUntil>0 && elapsed>=f.U610BarrageUntil)
                { f.U610BarrageUntil=0; f.U610BarrageDartFactor=0; }
            }
        }
        private void U610AfterBasicAttack(Fighter attacker,Fighter target)
        {
            if(attacker==null || !attacker.Alive) return;
            // U6.10.3: play a small hit/recoil pose once per REAL basic attack.
            MonsterTacticsU610SpriteMotion motion = attacker.Unit != null ?
                attacker.Unit.GetComponentInChildren<MonsterTacticsU610SpriteMotion>() : null;
            if (motion != null) motion.TriggerBasicAttack();
            string family=MonsterTacticsU5Roster.BaseName(attacker.Name);
            if (family == "Stormlynx" && attacker.U6113PlasmaEngine)
            {
                var plasma = MonsterTacticsU6113PlasmaEngineProfile.LoadProfile();
                if (plasma != null && plasma.Enabled)
                {
                    attacker.U6113BasicAttacks++;
                    if (plasma.AttacksPerManaProc > 0 &&
                        attacker.U6113BasicAttacks % plasma.AttacksPerManaProc == 0)
                    {
                        // This is in addition to normal on-hit Mana gained by BasicAttack.
                        // A cap prevents overcharging and never spills into another cast.
                        attacker.Mana = Mathf.Min(attacker.Stats.ManaCost,
                            attacker.Mana + Mathf.Max(0, plasma.BonusManaPerProc));
                    }
                }
            }
            // An on-hit passive, not an extra basic attack (no item/on-hit recursion).
            if (family == "Skyshard") U612OnSkyshardBasicAttack(attacker, target);
            if (family=="Yaroris" && ++attacker.U610HeavySparkCounter%3==0 && target.Alive)
            {
                int star=attacker.Unit.GetComponent<MonsterTacticsU5Progression>()?.Stage??1;
                float baseDamage=star>=3?155:star==2?100:65;
                DealDamage(target,baseDamage+attacker.Stats.AbilityPower*.65f,true,attacker,"HEAVY SPARK");
                if(abilityVfx!=null)abilityVfx.PlayU610HeavySpark(attacker.Unit,target.Unit);
            }
            if(family=="Umbralyn" && attacker.U610BarrageUntil>elapsed)
            {
                for(int i=0;i<2;i++)
                {
                    Fighter other=FindSecondTargetWithin(attacker,target,4);
                    if(other==null || !other.Alive) other=target.Alive?target:null;
                    if(other==null)break;
                    DealDamage(other,attacker.Stats.Attack*attacker.U610BarrageDartFactor,false,attacker,"DRAGON DART");
                    beams.Add(new BeamEffect { Start = attacker.Unit.transform.position + Vector3.up*.9f,
                        End = other.Unit.transform.position + Vector3.up*.9f,
                        Color = new Color(.76f,.42f,1f), Born = elapsed });
                    if (abilityVfx!=null) abilityVfx.PlayU610DragonDart(attacker.Unit,other.Unit,i);
                }
            }
        }
        // Return true if the ability is fully handled and does not schedule a normal hit.
        private bool U610AfterCast(Fighter source,Fighter target,MonsterTacticsCombatRules.AbilityProfile ability,int star)
        {
            string family=MonsterTacticsU5Roster.BaseName(source.Name);
            U610StartOverdrive(source);
            if (family == "Skyshard" || family == "Skyweave") U612OnEonCast(source);
            if (family == "Skyshard")
            {
                U612StartSkyshardLusterPurge(source, star);
                return true; // No immediate spell hit; beams occur on basic attacks.
            }
            if (family == "Skyweave") U612ShieldLowestHpAlly(source, star);
            if (family == "Stormlynx" && source.U6113PlasmaEngine)
            {
                var plasma = MonsterTacticsU6113PlasmaEngineProfile.LoadProfile();
                if (plasma != null && plasma.Enabled && plasma.AttackSpeedBonus > 0f)
                {
                    // Existing non-stacking timed speed buff handles refresh & clean expiry.
                    // Never modifies chain radius, damage, target count, or targeting AI.
                    U610StartExtraSpeed(source,
                        1f + Mathf.Clamp01(plasma.AttackSpeedBonus),
                        Mathf.Max(.1f, plasma.AttackSpeedDuration));
                }
            }
            if (family=="Vexokel")
            {
                GrantNearestAllyProtection(source.Unit,star>=3?.26f:star==2?.20f:.16f,
                    star>=3?25:star==2?15:10,star>=3?25:star==2?15:10,4f,2);
                return true;
            }
            if(family=="Umbralyn")
            {
                source.U610BarrageUntil=elapsed+5f;
                source.U610BarrageDartFactor=star>=3?.60f:star==2?.40f:.30f;
                U610StartExtraSpeed(source,1.20f,5f);
                return true;
            }
            if (family=="Xenobor" || family=="Astramora")
            {
                if (family=="Xenobor") ApplyTemporaryShield(source.Unit,.10f,3f);
                else ApplyTemporaryShield(source.Unit,.14f,4f);
                var victims=source.Enemy?players:opponents;
                foreach (Fighter victim in victims)
                {
                    if (!victim.Alive || victim.Tile==null || source.Tile==null ||
                        MonsterTacticsCombatRules.HexDistance(MonsterTacticsCombatRules.Coord(source.Tile),MonsterTacticsCombatRules.Coord(victim.Tile))>1) continue;
                    DealDamage(victim,ability.PrimaryRaw,true,source,ability.Name.ToUpperInvariant());
                    if (family=="Astramora") ApplyAbilityBurn(victim,3f,.015f,source);
                    U610AfterSpellHit(source,victim,ability);
                    if (abilityVfx!=null)abilityVfx.PlayImpact(victim.Unit,ability.Name);
                }
                return true;
            }
            if(family=="Evomon") U610StartExtraSpeed(source,1.10f,4f);
            if(family=="Jaspvar") ApplyTemporaryShield(source.Unit,.18f,4f);
            if(family=="Orbivek") DashBesideTarget(source,target);
            // Stormlynx has a visible forward-and-return Plasma Fists lunge,
            // which runs even when already adjacent to its locked target.
            if(family=="Umbrelisk") U610DashBehindTarget(source,target);
            return false;
        }
        // Unlike the ordinary dash (closest adjacent tile), Umbrelisk chooses
        // the opposite side of the current target when an empty hex exists.
        private void U610DashBehindTarget(Fighter source,Fighter target)
        {
            if(source==null || target==null || source.Tile==null || target.Tile==null) return;
            var taken=new HashSet<Vector2Int>();
            foreach(Fighter other in turnList)
                if(other!=source && other.Alive && other.Tile!=null)
                    taken.Add(MonsterTacticsCombatRules.Coord(other.Tile));
            Vector2Int start=MonsterTacticsCombatRules.Coord(source.Tile);
            HexTile best=null;
            int mostDistance=-1;
            foreach(Vector2Int pos in MonsterTacticsCombatRules.Neighbors(MonsterTacticsCombatRules.Coord(target.Tile)))
            {
                HexTile candidate;
                if(taken.Contains(pos)||!board.TryGetValue(pos,out candidate))continue;
                int distance=MonsterTacticsCombatRules.HexDistance(start,pos);
                if(distance>mostDistance){ mostDistance=distance; best=candidate; }
            }
            if(best==null){DashBesideTarget(source,target);return;}
            source.SlideFrom=source.Unit.transform.position;
            source.Tile=best;
            source.Unit.PlaceOnTile(best);
            source.SlideTo=source.Unit.transform.position;
            source.Unit.transform.position=source.SlideFrom;
            source.SlideStart=elapsed;
            source.SlideEnd=elapsed+.24f;
            source.MoveReady=elapsed+.26f;
        }
        private void ApplyAbilityBurn(Fighter victim,float seconds,float percent, Fighter owner = null)
        {
            if(victim==null || !victim.Alive || victim.MagmaCoreEnabled)return;
            if (victim.BurnUntil <= elapsed || percent >= victim.BurnPercent)
                victim.U6115BurnOwner=owner;
            victim.BurnPercent=Mathf.Max(victim.BurnPercent,percent);
            victim.BurnUntil=Mathf.Max(victim.BurnUntil,elapsed+seconds);
            victim.NextBurnTick=elapsed+1f;
        }
        private void U610AfterSpellHit(Fighter source,Fighter victim,MonsterTacticsCombatRules.AbilityProfile ability)
        {
            if(source==null || victim==null)return;
            // The primary physical strike has already resolved. The chain uses
            // the original impact hex, even if the main victim just fainted.
            if (MonsterTacticsU5Roster.BaseName(source.Name) == "Stormlynx")
            {
                U611ResolveStormlynxLightning(source, victim);
                return;
            }
            if(source.U610Hexcraft && victim.Alive)
                AddResistanceEffect(victim,true,.20f,4f,source,"Hexcraft");
            string family=MonsterTacticsU5Roster.BaseName(source.Name);
            if(family=="Quillobor" && victim.Alive) ApplyAttackSpeedSlow(victim.Unit,.20f,2f);
            else if(family=="Jaspvar" && victim.Alive) AddResistanceEffect(victim,false,.15f,3f,source,"SeismicGuard");
            else if(family=="Evodrel" && victim.Alive) victim.ActionReady=Mathf.Max(victim.ActionReady,elapsed+1f);
            else if(family=="Umbrelisk" && victim.Alive)
            {
                if (!source.ShadowThiefSpent &&
                    TryApplyShadowThief(source.Unit,victim.Unit,.12f,.08f,5f) && abilityVfx!=null)
                    abilityVfx.PlayU610ShadowThief(source.Unit,victim.Unit);
                int star=source.Unit.GetComponent<MonsterTacticsU5Progression>()?.Stage??1;
                DealDamage(victim,source.Stats.AbilityPower*(star>=3?.90f:star==2?.60f:.40f),true,source,"SPECTRAL MAGIC");
            }
            else if(family=="Astraset" || family=="Orbivek")
            {
                int star=source.Unit.GetComponent<MonsterTacticsU5Progression>()?.Stage??1;
                float splash=family=="Astraset" ?
                    (star>=3?260f:star==2?160f:100f)+source.Stats.AbilityPower*.8f:
                    (star>=3?90f:star==2?60f:35f)+source.Stats.AbilityPower*.5f;
                foreach(Fighter other in (source.Enemy?players:opponents))
                    if(other!=victim && other.Alive && other.Tile!=null && victim.Tile!=null &&
                        MonsterTacticsCombatRules.HexDistance(MonsterTacticsCombatRules.Coord(victim.Tile),MonsterTacticsCombatRules.Coord(other.Tile))<=1)
                        DealDamage(other,splash,true,source,"LIGHTNING");
            }
        }
    }
}
