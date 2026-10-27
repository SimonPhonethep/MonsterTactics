using System.Collections.Generic;
using UnityEngine;

namespace MonsterTactics
{
    [CreateAssetMenu(fileName = "MonsterTacticsAugmentCatalog", menuName = "MonsterTactics/Augments/Catalog")]
    public sealed class MonsterTacticsAugmentCatalog : ScriptableObject
    {
        public bool masterEnabled = false; // OFF until the U6.9.1 selection UI is built.
        [Header("Custom Augment Roster")]
        [Tooltip("Prevents the legacy sample/draft creation buttons from repopulating a curated custom roster.")]
        public bool suppressLegacySampleCreation = false;
        public List<MonsterTacticsAugmentDefinition> definitions = new List<MonsterTacticsAugmentDefinition>();
        public int seed = 42;
        // New local matches normally receive a fresh runtime seed. Turn OFF to
        // reproduce identical offers from this inspector seed when debugging.
        public bool useFreshMatchSeed = true;
        // One independent reroll per card matches modern TFT (three per round).
        [Min(0)] public int rerollsPerCard = 1;
        // Legacy shared counter retained for compatibility with older catalog assets.
        [HideInInspector] public int freeOfferRerollsPerSelection = 1;
        [Min(1f)] public float firstSelectionSeconds = 43f;
        [Min(1f)] public float laterSelectionSeconds = 58f;
        public bool autoPickWhenTimeExpires = true;
        [Header("TFT-style offer rules (new matches only)")]
        public bool useMatchTierPatterns = true;
        public bool limitSameCategoryToTwo = true;
        public bool enableTraitFamilyTailoring = true;
        // Entries and probabilities are editable in the Balance Editor. The host
        // will send the match's chosen pattern to all players in future multiplayer.
        public List<MonsterTacticsAugmentTierPattern> tierPatterns = new List<MonsterTacticsAugmentTierPattern>
        {
            new MonsterTacticsAugmentTierPattern { first=MonsterTacticsAugmentTier.Rookie, second=MonsterTacticsAugmentTier.Ace, final=MonsterTacticsAugmentTier.Champion, weight=25 },
            new MonsterTacticsAugmentTierPattern { first=MonsterTacticsAugmentTier.Ace, second=MonsterTacticsAugmentTier.Ace, final=MonsterTacticsAugmentTier.Ace, weight=20 },
            new MonsterTacticsAugmentTierPattern { first=MonsterTacticsAugmentTier.Rookie, second=MonsterTacticsAugmentTier.Rookie, final=MonsterTacticsAugmentTier.Ace, weight=20 },
            new MonsterTacticsAugmentTierPattern { first=MonsterTacticsAugmentTier.Champion, second=MonsterTacticsAugmentTier.Rookie, final=MonsterTacticsAugmentTier.Ace, weight=10 },
            new MonsterTacticsAugmentTierPattern { first=MonsterTacticsAugmentTier.Rookie, second=MonsterTacticsAugmentTier.Ace, final=MonsterTacticsAugmentTier.Ace, weight=25 }
        };
    }
}
