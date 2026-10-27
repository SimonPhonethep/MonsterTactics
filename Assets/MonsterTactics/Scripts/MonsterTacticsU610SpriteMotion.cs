using System;
using UnityEngine;

namespace MonsterTactics
{
    /// <summary>
    /// Visual-only U6.10.3 motion on the SpriteRenderer child.
    /// Does not reposition the unit/tile, change combat attacks, or move the HP bar anchor.
    /// Real PNG-frame loops, when imported, are played by MonsterFrameAnimator.
    /// This adds gentle breathing, tiny attack recoil, and cast emphasis; it also
    /// supplies a minimal living idle when a species has no genuine animated GIF.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(500)]
    public sealed class MonsterTacticsU610SpriteMotion : MonoBehaviour
    {
        [SerializeField, Range(0.0f, 0.06f)] private float breathAmount = 0.017f;
        [SerializeField, Range(0.5f, 3.5f)] private float breathRate = 1.65f;
        [SerializeField, Range(0.0f, 0.18f)] private float attackPulse = 0.07f;
        [SerializeField, Range(0.0f, 0.24f)] private float castPulse = 0.10f;

        private Vector3 restingScale;
        private float recoil;
        private float cast;
        private bool ready;
        private MonsterUnit owner;
        private float visualScale = 1f;

        private static readonly string[] Families =
        {
            "Yaroris", "Quillobor", "Quillorixa", "Astraset", "Rovavian", "Jaspvar",
            "Evodrel", "Sylvodon", "Umbrelisk", "Astramora", "Lumomora", "Vexokel",
            "Kaironet", "Xenobor", "Orbivek", "Vexorius", "Evomon", "Umbralyn",
            "Stormlynx", "Skyweave", "Skyshard", "Spriggit", "Tricklet"
        };

        public static bool IsU610Family(string family)
        {
            foreach (string candidate in Families)
                if (string.Equals(candidate, family, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        public void Configure(MonsterUnit unit, float scale = 1f)
        {
            if (ready) transform.localScale = restingScale;
            owner = unit;
            visualScale = Mathf.Clamp(scale, .65f, 1.2f);
            restingScale = transform.localScale;
            recoil = cast = 0f;
            ready = true;
        }

        /// <summary>For enemy clones: retain the explicit size established by ConfigureEncounterVisual.
        /// Ordinary playable units continue using Configure() unchanged.</summary>
        public void ConfigureEncounter(MonsterUnit unit)
        {
            owner = unit;
            restingScale = transform.localScale;
            visualScale = 1f;
            recoil = cast = 0f;
            ready = true;
        }

        public void TriggerBasicAttack() { recoil = Mathf.Max(recoil, 1f); }
        public void TriggerCast() { cast = Mathf.Max(cast, 1f); }

        private void Awake()
        {
            restingScale = transform.localScale;
            owner = GetComponentInParent<MonsterUnit>();
            ready = true;
        }

        private void OnEnable()
        {
            if (!ready)
            {
                restingScale = transform.localScale;
                ready = true;
            }
            recoil = cast = 0f;
        }

        private void LateUpdate()
        {
            if (!ready || !isActiveAndEnabled) return;
            float dt = Mathf.Min(0.1f, Time.deltaTime);
            recoil = Mathf.Max(0f, recoil - dt * 6.5f);
            cast = Mathf.Max(0f, cast - dt * 4.8f);

            float wave = Mathf.Sin(Time.time * breathRate * 2f + (owner != null ? owner.transform.position.x * 0.47f : 0f));
            float idle = breathAmount * wave;
            float horizontal = 1f + idle * .55f + recoil * attackPulse + cast * castPulse;
            float vertical = 1f - idle * .45f - recoil * attackPulse * .65f + cast * castPulse * .50f;
            transform.localScale = new Vector3(restingScale.x * visualScale * horizontal,
                restingScale.y * visualScale * vertical, restingScale.z);
        }

        private void OnDisable()
        {
            if (ready) transform.localScale = restingScale;
        }
    }
}
