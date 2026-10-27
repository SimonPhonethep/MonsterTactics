#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace MonsterTactics
{
    /// <summary>Non-mutating regression checks for the augment timer bar.</summary>
    public static class MonsterTacticsU691dValidator
    {
        [MenuItem("MonsterTactics/Tests/Archive/Augments/U6.9.1d Verify Augment Timer Bar")]
        private static void Verify()
        {
            var clock = new MonsterTacticsAugmentSelectionClock();
            int checks = 0;
            clock.Begin(3, 1, 43f);
            Check("Preview initial time", clock.TotalSeconds, 43f, ref checks);
            Check("Preview full progress", clock.Progress01, 1f, ref checks);
            clock.SetSecondsRemaining(60f);
            Check("Scheduled total time", clock.TotalSeconds, 60f, ref checks);
            Check("Scheduled remaining time", clock.SecondsRemaining, 60f, ref checks);
            Check("Scheduled full progress", clock.Progress01, 1f, ref checks);
            clock.Advance(15f);
            Check("45s remaining", clock.SecondsRemaining, 45f, ref checks);
            Check("75 percent progress", clock.Progress01, 0.75f, ref checks);
            clock.Advance(30f);
            Check("15s remaining", clock.SecondsRemaining, 15f, ref checks);
            Check("25 percent progress", clock.Progress01, 0.25f, ref checks);
            clock.Advance(15f);
            Check("Expired", clock.Progress01, 0f, ref checks);
            if (clock.TotalRerollsRemaining != 3) throw new InvalidOperationException(
                "Resynchronizing the clock unexpectedly changed card rerolls.");
            checks++;
            clock.Clear();
            Check("Clear resets total", clock.TotalSeconds, 0f, ref checks);

            Debug.Log("[U6.9.1d] PASS: " + checks +
                " augment clock tests. Visual timing still requires Play Mode.");
            EditorUtility.DisplayDialog("MonsterTactics U6.9.1d",
                "PASS: " + checks +
                " clock checks. In Play Mode verify the blue bar starts full at 60s and is half-full at 30s.",
                "OK");
        }

        private static void Check(string name, float got, float want, ref int checks)
        {
            if (Mathf.Abs(got - want) > 0.0001f)
                throw new InvalidOperationException(name + ": expected " + want +
                    " but got " + got);
            checks++;
        }
    }
}
#endif
