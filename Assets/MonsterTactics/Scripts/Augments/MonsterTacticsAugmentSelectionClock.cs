using System;

namespace MonsterTactics
{
    /// <summary>
    /// U6.9.0b: selection session state, independent of UI, shop, and round manager.
    /// The hosting game code is responsible for starting the clock and applying a choice.
    /// Timeout picks use the CURRENTLY VISIBLE choices, not the original set.
    /// </summary>
    public sealed class MonsterTacticsAugmentSelectionClock
    {
        private int[] rerolls = new int[0];
        public bool IsOpen { get; private set; }
        public float SecondsRemaining { get; private set; }
        // The UI progress bar must use the same total as the live selection.
        // A scheduled augment round overrides the standalone preview duration.
        public float TotalSeconds { get; private set; }
        public float Progress01 => TotalSeconds <= 0f ? 0f :
            Math.Min(1f, Math.Max(0f, SecondsRemaining / TotalSeconds));
        public int CardCount => rerolls.Length;
        public int TotalRerollsRemaining
        {
            get
            {
                int total = 0;
                foreach (int v in rerolls) total += v;
                return total;
            }
        }

        public void Begin(int cardCount, int rerollsPerCard, float seconds)
        {
            if (cardCount <= 0) throw new ArgumentOutOfRangeException(nameof(cardCount));
            if (seconds <= 0f) throw new ArgumentOutOfRangeException(nameof(seconds));
            rerolls = new int[cardCount];
            for (int i = 0; i < cardCount; ++i)
                rerolls[i] = Math.Max(0, rerollsPerCard);
            SecondsRemaining = seconds;
            TotalSeconds = seconds;
            IsOpen = true;
        }

        /// <summary>Align an already opened choice to the round's shared deadline.
        /// Per-card reroll charges and current offers are preserved.</summary>
        public void SetSecondsRemaining(float seconds)
        {
            if (!IsOpen) return;
            SecondsRemaining = Math.Max(0f, seconds);
            TotalSeconds = Math.Max(0f, seconds);
        }

        public int RemainingForCard(int index) =>
            index >= 0 && index < rerolls.Length ? rerolls[index] : 0;

        public bool CanReroll(int index) =>
            IsOpen && SecondsRemaining > 0f && RemainingForCard(index) > 0;

        // Call this only AFTER the replacement offer is available.
        public bool SpendReroll(int index)
        {
            if (!CanReroll(index)) return false;
            --rerolls[index];
            return true;
        }

        // Returns true every time expired/unresolved is sampled; resolving ends it.
        public bool Advance(float elapsedSeconds)
        {
            if (!IsOpen) return false;
            if (elapsedSeconds > 0f)
                SecondsRemaining = Math.Max(0f, SecondsRemaining - elapsedSeconds);
            return SecondsRemaining <= 0f;
        }

        public bool Finish()
        {
            if (!IsOpen) return false;
            IsOpen = false;
            return true;
        }

        public void Clear()
        {
            IsOpen = false;
            SecondsRemaining = 0f;
            TotalSeconds = 0f;
            rerolls = new int[0];
        }

        public static int ChooseVisibleIndex(int visibleCount, int seed)
        {
            if (visibleCount <= 0) return -1;
            return new System.Random(seed).Next(visibleCount);
        }
    }
}
