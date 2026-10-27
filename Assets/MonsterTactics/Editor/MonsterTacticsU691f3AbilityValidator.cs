#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace MonsterTactics
{
    /// <summary>Nonmutating tests of authored ability scaling + safety behavior.</summary>
    public static class MonsterTacticsU691f3AbilityValidator
    {
        [MenuItem("MonsterTactics/Tests/U6.9.1f.3 Verify Ability Engine")]
        public static void Verify()
        {
            int checks = 0;
            MonsterTacticsContentCatalog c = MonsterTacticsContentStudioImporter.FindCatalog();
            if (c == null) throw new InvalidOperationException("Import catalog first.");
            var report = MonsterTacticsContentStudioValidator.Check(c);
            if (!report.Passed) throw new InvalidOperationException("Catalog has " +
                report.errors.Count + " validation errors. First: " + report.errors[0]);
            checks++;
            var legacy = MonsterTacticsCombatRules.ForMonster("Emberlet");
            var oldProfile = MonsterTacticsCombatRules.AbilityFor("Emberlet", legacy);
            if (!c.enableLiveAbilities && oldProfile.Name != "Flamethrower")
                throw new InvalidOperationException("Legacy Flamethrower was unexpectedly replaced.");
            checks++;
            var custom = new MonsterTacticsAbilityRecord
            {
                id = "test_thunder", displayName = "Thunder Test", useEffectBlocks = true,
                damageKind = "Magic", hitCount = 1, splashRadius = 1,
                maximumSecondaryTargets = 2, burnDuration = 4f,
                burnMaxHpPerSecond = .01f, woundDuration = 3f,
                primary = new MonsterTacticsAbilityScaling { flat = 10f, abilityPower = 2f },
                splash = new MonsterTacticsAbilityScaling { attack = .5f },
                selfHeal = new MonsterTacticsAbilityScaling { abilityPower = .75f },
                selfShield = new MonsterTacticsAbilityScaling { flat = 20f }
            };
            string error;
            if (!MonsterTacticsContentAbilityRuntime.Valid(custom, out error))
                throw new InvalidOperationException("Valid custom ability rejected: " + error);
            checks++;
            MonsterTacticsCombatRules.Stats fighter = legacy;
            fighter.Attack = 60f;
            fighter.AbilityPower = 130f;
            MonsterTacticsCombatRules.AbilityProfile profile =
                MonsterTacticsContentAbilityRuntime.EvaluateRecord(custom, fighter);
            Near("primary magic scales from EFFECTIVE AP", profile.PrimaryRaw, 270f, ref checks);
            Near("splash physical coefficient", profile.SplashRaw, 30f, ref checks);
            Near("heal from AP", profile.HealRaw, 97.5f, ref checks);
            Near("shield", profile.ShieldRaw, 20f, ref checks);
            Near("burn", profile.BurnMaxHpPerSecond, .01f, ref checks);
            if (profile.HitCount != 1 || profile.MaximumSecondaryTargets != 2 ||
                profile.SplashRadius != 1 || profile.WoundDuration != 3f)
                throw new InvalidOperationException("Profile lost an authored secondary effect.");
            checks++;
            custom.primary.abilityPower = -1f;
            if (MonsterTacticsContentAbilityRuntime.Valid(custom, out error))
                throw new InvalidOperationException("Negative scaling passed validation.");
            checks++;
            custom.primary.abilityPower = 2f;
            custom.hitCount = 0;
            if (MonsterTacticsContentAbilityRuntime.Valid(custom, out error))
                throw new InvalidOperationException("Zero hits passed validation.");
            checks++;
            custom.hitCount = 1;
            custom.useEffectBlocks = false;
            if (!MonsterTacticsContentAbilityRuntime.Valid(custom, out error))
                throw new InvalidOperationException("Legacy fallback failed validation.");
            checks++;
            Debug.Log("[U6.9.1f.3] PASS: " + checks +
                " non-mutating ability math and validation checks. " +
                "Verify effects/VFX in Play Mode separately.");
            EditorUtility.DisplayDialog("MonsterTactics U6.9.1f.3",
                "PASS: " + checks + " tests.\n\nAuthored values scale with effective fighter " +
                "stats; invalid configs are rejected; legacy fallback remains. " +
                "For real casting, follow README's Emberlet test in Play Mode.", "OK");
        }
        private static void Near(string label, float got, float want, ref int checks)
        {
            if (Mathf.Abs(got - want) > .0001f)
                throw new InvalidOperationException(label + ": expected " + want + ", got " + got);
            checks++;
        }
    }
}
#endif
