using System;
namespace MonsterTactics
{
    /// <summary>U6.7.3c balanced 36 standard item effects. Same-named passive unique per holder.</summary>
    public static class MonsterTacticsItemPassiveCatalog
    {
        private static readonly string[] Passives =
        {
            "Every 3rd basic attack deals 40% AD bonus physical damage.",
            "After casting, the next attack deals 60% AP bonus magic damage.",
            "Every 3rd basic attack fires a bolt at another enemy within 2 hexes for 55% AD physical damage.",
            "Basic attacks apply Sunder: -30% Armor for 4s (refreshes, does not stack).",
            "Gain 12% Omnivamp. Once per combat below 40% HP, gain a 15% max-HP shield.",
            "After casting, the next attack deals +50% AD physical damage and restores 5 Mana.",
            "Deal 12% more damage to enemies above 60% HP (attacks and spells).",
            "Physical abilities can critically strike for 140% damage.",
            "Gain +4 AP every 5 seconds in combat, up to +24.",
            "After casting, gain +20% Attack Speed for 4 seconds (refresh, not stack).",
            "Casting gives an 8% max-HP shield, with a 6s cooldown.",
            "Damaging magic abilities apply Wound (-33% healing) and Burn (0.5% max HP true damage per second) for 4s; refresh, never stack.",
            "After casting, restore 10 Mana, with a 3s cooldown.",
            "Magic ability hits apply Shred: -30% Magic Resist for 4s (refreshes).",
            "Magic abilities have a 25% chance to critically strike for 140% damage.",
            "Each basic attack grants +2.5% Attack Speed, up to +25%.",
            "Basic attacks grant +2 Armor and +2 MR, up to +14 each.",
            "Every 3rd basic attack heals 2% maximum HP, capped at 45 HP.",
            "Basic attacks generate 4 additional Mana.",
            "The first two incoming damage hits deal 30% less damage (after mitigation, before shields).",
            "Each basic attack grants +2 percentage points Crit Chance, up to +16 points.",
            "Reduce bonus damage from enemy critical attacks by 25%. When hit, deal 25 magic damage to adjacent enemies, 4s cooldown.",
            "Every 4s, Wound a nearby enemy within 2 hexes for 5s (-33% healing) and Burn for 0.5% max HP true damage each second.",
            "Each time HP damage is taken, gain +1 Armor and +1 MR, up to +10 each.",
            "Once below 50% HP, gain an 18% max-HP shield and +10 Armor/MR for 5s.",
            "Frontline aura: Sunder enemies within 2 hexes (-30% Armor). Does not stack with other Sunder sources.",
            "At combat start gain +10% maximum HP.",
            "Every 7s, heal lowest-health ally for 6% of holder max HP (max 90 HP).",
            "At combat start gain a shield for 16% maximum HP that expires after 6s.",
            "Every 4th basic attack heals lowest-health ally for 4% holder max HP (max 70 HP).",
            "After first cast, restore 10 Mana and gain 15% Attack Speed for 5s; once per combat.",
            "Casting shields the lowest-health ally for 10% holder max HP, 6s cooldown.",
            "At combat start randomly gain either +12% damage or +12% Omnivamp for the battle.",
            "Frontline aura: Shred enemies within 2 hexes (-30% Magic Resist). Does not stack with other Shred sources.",
            "Taking a critical attack or damaging spell grants an 8% max-HP shield (10s cooldown).",
            "Critical basic attacks deal 20% bonus damage; restore 3 Mana on critical hits (2s cooldown)."
        };
        public static string Effect(MonsterTacticsItemKind item)
        {
            int id = (int)item - (int)MonsterTacticsItemKind.Craft00;
            return id >= 0 && id < Passives.Length ? Passives[id] : "";
        }
        public static bool HasImplementedPassive(MonsterTacticsItemKind item)
        {
            int id = (int)item - (int)MonsterTacticsItemKind.Craft00;
            return id >= 0 && id < Passives.Length && !string.IsNullOrEmpty(Passives[id]);
        }
        public static int StandardPassiveCount => Passives.Length;
    }
}
