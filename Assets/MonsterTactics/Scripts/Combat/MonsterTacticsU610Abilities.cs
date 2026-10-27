using UnityEngine;
namespace MonsterTactics
{
    /// <summary>U6.10 real ability profiles, shared by battle and inspector.</summary>
    public static class MonsterTacticsU610Abilities
    {
        private static float Pick(int s, float a, float b, float c) => s >= 3 ? c : s == 2 ? b : a;
        public static bool TryProfile(string family, int star, MonsterTacticsCombatRules.Stats stats,
            out MonsterTacticsCombatRules.AbilityProfile profile)
        {
            float ad = stats.Attack, ap = stats.AbilityPower / 100f;
            profile = default(MonsterTacticsCombatRules.AbilityProfile);
            switch (family)
            {
                // U6.11: first proposed Assassin family. One physical strike,
                // with a position-based bonus applied by the battle director.
                case "Tricklet":
                    // Two true spell hits, each separately mitigated by Magic Resist.
                    // Any retarget for the second strike is chosen at impact time.
                    profile = new MonsterTacticsCombatRules.AbilityProfile("Play Rough",
                        "Two Magic hits. If the first target faints, the second hit seeks another enemy.",
                        "Each hit: 100/155/250 + 45% AP", "Magic",
                        Pick(star, 100f, 155f, 250f) + stats.AbilityPower * .45f,
                        0f, 2);
                    break;
                case "Spriggit":
                    profile = new MonsterTacticsCombatRules.AbilityProfile("Leaf Blade",
                        "Physical slash. Deals 25% bonus damage when cast from behind the target.",
                        "175/265/420% AD; +25% from behind", "Physical",
                        ad * Pick(star, 1.75f, 2.65f, 4.20f));
                    break;
                case "Yaroris":
                    profile = new MonsterTacticsCombatRules.AbilityProfile("Heavy Spark",
                        "Every third basic attack becomes a stronger AP-scaling electric hit.",
                        "Every 3rd basic: " + Pick(star,65,100,155)+" + 65% AP magic", "Magic", 0); break;
                case "Quillobor":
                    profile = new MonsterTacticsCombatRules.AbilityProfile("Avalanche Tackle",
                        "AoE physical hit that slows Attack Speed by 20% for 2s.", "100/160/260 + 5% max HP",
                        "Physical", Pick(star,100,160,260), Pick(star,55,85,125)); break;
                case "Quillorixa":
                    profile = new MonsterTacticsCombatRules.AbilityProfile("Four-Armed Fury", "Four fast physical punches.",
                        "4 x " + Pick(star,55,85,130) + "% AD", "Physical", ad * Pick(star,.55f,.85f,1.30f),0,4); break;
                case "Astraset":
                    profile = new MonsterTacticsCombatRules.AbilityProfile("Thunder Rampage",
                        "Physical strike followed by magic chain lightning.", "AD physical + AP magic splash",
                        "Physical", ad*Pick(star,1.8f,2.8f,4.5f)); break;
                case "Rovavian":
                    profile = new MonsterTacticsCombatRules.AbilityProfile("Magma Cannon",
                        "Magic explosion that Burns enemies for 3s.","150/230/400 + 100% AP",
                        "Magic", Pick(star,150,230,400)+ap*100, Pick(star,65,95,150));
                    profile.BurnDuration=3; profile.BurnMaxHpPerSecond=.015f; break;
                case "Jaspvar":
                    profile = new MonsterTacticsCombatRules.AbilityProfile("Seismic Guard",
                        "Deals nearby physical damage, shields itself and Sunders enemies.", "100/170/300 + 7% max HP",
                        "Physical", Pick(star,100,170,300), Pick(star,60,95,145)); break;
                case "Evodrel":
                    profile = new MonsterTacticsCombatRules.AbilityProfile("Heavy Slam",
                        "Armor-scaling physical slam and brief stun.", "90/150/260 + 60% Armor",
                        "Physical", Pick(star,90,150,260)+stats.Armor*.6f, Pick(star,50,75,110)); break;
                case "Sylvodon":
                    profile = new MonsterTacticsCombatRules.AbilityProfile("Hammer Arm", "Strong AD-scaling physical hit.",
                        "150/225/340% AD", "Physical",ad*Pick(star,1.5f,2.25f,3.4f)); break;
                // U6.11 standalone addition. Primary strike is physical; secondary
                // lightning is resolved as separate Magic Damage after the strike.
                case "Skyweave":
                    profile = new MonsterTacticsCombatRules.AbilityProfile("Mist Ball",
                        "Psychic single-target Magic Damage; shield the living ally with lowest HP% for 3 seconds.",
                        "250/400/850 + 80% AP magic; shields 180/300/650", "Magic",
                        Pick(star,250,400,850) + stats.AbilityPower * .80f);
                    break;
                case "Skyshard":
                    profile = new MonsterTacticsCombatRules.AbilityProfile("Luster Purge",
                        "Gain Attack Speed for 5s. Every third real basic attack during the buff " +
                        "fires a piercing psychic beam through nearby enemies.",
                        "+30/45/70% Attack Speed; beam 140/230/450 + 60% AP magic", "Magic", 0f);
                    break;
                case "Stormlynx":
                    profile = new MonsterTacticsCombatRules.AbilityProfile("Plasma Fists",
                        "Lunge toward your target for Physical Damage, then return. Lightning hits up to 2 OTHER " +
                        "enemies within 2 hexes of that target. If none exist, only the physical strike lands.",
                        "Primary: 240/360/650% AD; each chain: 125/225/450 + 65% AP magic", "Physical",
                        ad * Pick(star, 2.40f, 3.60f, 6.50f));
                    break;
                case "Umbrelisk":
                    profile = new MonsterTacticsCombatRules.AbilityProfile("Spectral Ambush",
                        "Blink behind current target, physical strike and magic followup.",
                        "150/220/350% AD + 40/60/90% AP", "Physical",ad*Pick(star,1.5f,2.2f,3.5f)); break;
                case "Astramora":
                    profile = new MonsterTacticsCombatRules.AbilityProfile("Volcanic Pressure",
                        "Caster-centered magic eruption, Burn and temporary shield.",
                        "160/260/430 + 100% AP", "Magic", Pick(star,160,260,430)+ap*100); break;
                case "Lumomora":
                    profile = new MonsterTacticsCombatRules.AbilityProfile("Savage Bite",
                        "Physical bite that heals for 25% of actual damage dealt.",
                        "145/220/340% AD, 25% damage heal", "Physical", ad*Pick(star,1.45f,2.2f,3.4f)); break;
                case "Vexokel":
                    profile = new MonsterTacticsCombatRules.AbilityProfile("Forest Shelter",
                        "Temporary HP shield for caster, Armor/MR to two closest allies.",
                        "16/20/26% HP shield, +10/15/25 Armor/MR", "Shield", 0); break;
                case "Kaironet":
                    profile = new MonsterTacticsCombatRules.AbilityProfile("Shadow Ball",
                        "Magic explosion applying 30% Wound for 3 seconds.", "130/210/350 + 80% AP",
                        "Magic", Pick(star,130,210,350)+.8f*stats.AbilityPower, Pick(star,55,85,130));
                    profile.WoundDuration=3; break;
                case "Xenobor":
                    profile = new MonsterTacticsCombatRules.AbilityProfile("Shadow Pulse",
                        "Caster-centered magic damage and a 10% Max HP shield.",
                        "90/140/210 + 50% AP", "Magic", Pick(star,90,140,210)+.5f*stats.AbilityPower); break;
                case "Orbivek":
                    profile = new MonsterTacticsCombatRules.AbilityProfile("Wild Charge",
                        "Leap to target for physical damage, AP-scaled lightning splash.",
                        "190/280/430% AD + 50% AP splash", "Physical",ad*Pick(star,1.9f,2.8f,4.3f)); break;
                case "Vexorius":
                    profile = new MonsterTacticsCombatRules.AbilityProfile("Mystical Burst",
                        "Magic area burst applying Hexcraft shred.", "110/170/270 + 70% AP",
                        "Magic", Pick(star,110,170,270)+.70f*stats.AbilityPower,
                        Pick(star,55,85,130)); break;
                case "Evomon":
                    profile = new MonsterTacticsCombatRules.AbilityProfile("Flame Charge",
                        "Sweep two nearby enemies and gain 10% Attack Speed for 4 seconds.",
                        "110/165/260% AD", "Physical", ad*Pick(star,1.1f,1.65f,2.6f),
                        ad*Pick(star,.60f,.90f,1.30f)); profile.MaximumSecondaryTargets=1; break;
                case "Umbralyn":
                    profile = new MonsterTacticsCombatRules.AbilityProfile("Dragon Darts: Barrage",
                        "5s +20% AS and two bonus AD darts on each basic attack.",
                        "2 bonus darts: 30/40/60% AD", "Physical", 0); break;
                default: return false;
            }
            return true;
        }
    }
}
