using System.Collections.Generic;
using UnityEngine;

namespace MonsterTactics
{
    /// <summary>
    /// U6.10.1: decorative, procedural 2.5D ability VFX for the 18 U6.10 families.
    /// Uses Unity primitives, no asset downloads/packages/prefabs; no colliders or gameplay writes.
    /// Owned by MonsterTacticsAbilityVfx, so battle cleanup removes all temporary visuals.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MonsterTacticsU610AbilityVfx : MonoBehaviour
    {
        private sealed class Particle
        {
            public GameObject Object;
            public Vector3 From, To, Size;
            public Quaternion Rotation;
            public float Born, Delay, Duration;
            public bool Moving, Spin;
        }

        private readonly List<Particle> active = new List<Particle>();
        private readonly Dictionary<int, Material> materials = new Dictionary<int, Material>();
        private const int MaximumVisuals = 300;
        private float castVisualScale = 1f;
        private static readonly Color Ice = new Color(.30f, .88f, 1f);
        private static readonly Color IceBright = new Color(.80f, 1f, 1f);
        private static readonly Color Fire = new Color(1f, .30f, .06f);
        private static readonly Color Amber = new Color(1f, .77f, .23f);
        private static readonly Color Lightning = new Color(1f, .93f, .22f);
        private static readonly Color Purple = new Color(.57f, .26f, .95f);
        private static readonly Color Spirit = new Color(.83f, .49f, 1f);
        private static readonly Color Leaf = new Color(.26f, .95f, .47f);
        private static readonly Color Steel = new Color(.63f, .82f, 1f);
        private static readonly Color Earth = new Color(.73f, .57f, .34f);
        private static readonly Color Pink = new Color(1f, .48f, .82f);
        private static readonly Color Scarlet = new Color(1f, .32f, .44f);

        public static bool IsNewAbility(string name)
        {
            switch (name)
            {
                case "Play Rough":
                case "Leaf Blade":
                case "Heavy Spark": case "Avalanche Tackle": case "Four-Armed Fury":
                case "Thunder Rampage": case "Magma Cannon": case "Seismic Guard":
                case "Heavy Slam": case "Hammer Arm": case "Spectral Ambush":
                case "Volcanic Pressure": case "Savage Bite": case "Forest Shelter":
                case "Shadow Ball": case "Shadow Pulse": case "Wild Charge":
                case "Mystical Burst": case "Flame Charge": case "Dragon Darts: Barrage":
                case "Plasma Fists": case "Mist Ball": case "Luster Purge":
                    return true;
                default: return false;
            }
        }

        /// <summary>Call at normal cast time. Timing is cosmetic; damage still uses CombatDirector.</summary>
        public bool TryPlayCast(MonsterUnit caster, MonsterUnit target, string ability, float windup)
        {
            if (!IsNewAbility(ability) || caster == null) return false;
            Vector3 a = At(caster), b = target != null ? At(target) : a;
            int star = caster.GetComponent<MonsterTacticsU5Progression>()?.Stage ?? 1;
            castVisualScale = star >= 3 ? 1.30f : star == 2 ? 1.13f : 1f;
            float travel = Mathf.Max(.19f, windup);
            switch (ability)
            {
                case "Play Rough":
                    Arc(a + Vector3.left * .22f, b + Vector3.right * .12f,
                        Purple, .23f, travel, .01f);
                    Arc(a + Vector3.right * .20f, b + Vector3.left * .12f,
                        Pink, .19f, travel, .10f);
                    Ring(b, Spirit, .45f, .30f, 8, travel * .7f);
                    break;
                case "Leaf Blade":
                    Arc(a + Vector3.left * .18f, b + Vector3.right * .14f,
                        Leaf, .26f, travel, .01f);
                    Arc(a + Vector3.right * .17f, b + Vector3.left * .12f,
                        new Color(.76f, 1f, .46f), .17f, travel, .045f);
                    Ring(b, Leaf, .36f, .27f, 8, travel * .75f);
                    break;
                case "Heavy Spark":
                    Spark(a, b, travel, .0f);
                    break;
                case "Avalanche Tackle":
                    Ring(a, Ice, .54f, .30f, 11);
                    Arc(a, b, IceBright, .17f, travel, .02f);
                    Burst(b, Ice, .49f, .40f, 8, .12f);
                    break;
                case "Four-Armed Fury":
                    for (int i = 0; i < 4; i++)
                        Arc(a + Vector3.right * (i % 2 == 0 ? -.16f : .16f),
                            b + Vector3.up * (i % 2 == 0 ? .08f : -.09f),
                            i % 2 == 0 ? Amber : Scarlet, .16f, .16f, i * .10f);
                    break;
                case "Mist Ball":
                    Ring(a, Pink, .48f, .32f, 10);
                    Orb(a, b, Spirit, .32f, travel, 0f);
                    Burst(b, Pink, .57f, .34f, 11, travel * .78f);
                    break;
                case "Luster Purge":
                    Ring(a, Spirit, .63f, .38f, 12);
                    Burst(a, Pink, .43f, .34f, 8, .03f);
                    break;
                case "Plasma Fists":
                    Ring(a, Lightning, .46f, .28f, 10);
                    Arc(a, b, Lightning, .31f, travel, 0f);
                    Arc(a + Vector3.left * .11f, b + Vector3.right * .10f,
                        Color.white, .12f, travel, .03f);
                    Burst(b, Lightning, .48f, .31f, 9, travel * .72f);
                    break;
                case "Thunder Rampage":
                    Arc(a, b, Lightning, .25f, travel, 0);
                    Arc(a + Vector3.left * .18f, b + Vector3.right * .12f, Color.white, .10f, travel, .045f);
                    Burst(b, Lightning, .54f, .31f, 9, .15f);
                    break;
                case "Magma Cannon":
                    Orb(a, b, Fire, .43f, travel, 0);
                    Orb(a + Vector3.up * .12f, b, Amber, .19f, travel, .04f);
                    Burst(b, Fire, .60f, .43f, 12, travel * .78f);
                    break;
                case "Seismic Guard":
                    GroundRing(a, Earth, .64f, .41f);
                    Burst(a, Amber, .30f, .35f, 6);
                    break;
                case "Heavy Slam":
                    Arc(a + Vector3.up * .25f, b, Steel, .28f, travel, 0);
                    GroundRing(b, Steel, .62f, .38f);
                    Burst(b, Color.white, .43f, .34f, 9, travel * .7f);
                    break;
                case "Hammer Arm":
                    Arc(a + Vector3.up * .26f, b, Earth, .29f, travel, 0);
                    Arc(a + Vector3.right * .16f, b, Amber, .12f, travel, .07f);
                    break;
                case "Spectral Ambush":
                    Burst(a, Purple, .46f, .28f, 12);
                    Arc(a, b, Spirit, .11f, travel, .015f);
                    Arc(a + Vector3.left * .14f, b + Vector3.right * .14f,
                        Purple, .20f, travel, .045f);
                    break;
                case "Volcanic Pressure":
                    GroundRing(a, Fire, .78f, .50f);
                    Burst(a, Amber, .83f, .48f, 14, .07f);
                    Orb(a + Vector3.down * .30f, a + Vector3.up * .55f, Fire, .28f, .40f, .02f);
                    break;
                case "Savage Bite":
                    Arc(a + Vector3.right * .24f, b, Scarlet, .17f, travel, 0);
                    Arc(a + Vector3.left * .24f, b, Amber, .17f, travel, .055f);
                    Ring(b, Scarlet, .28f, .25f, 7, travel * .65f);
                    break;
                case "Forest Shelter":
                    GroundRing(a, Leaf, .83f, .55f);
                    Ring(a, new Color(.72f, 1f, .63f), .52f, .44f, 12, .07f);
                    Burst(a + Vector3.up * .14f, Leaf, .56f, .54f, 12, .07f);
                    break;
                case "Shadow Ball":
                    Orb(a, b, Purple, .39f, travel, 0);
                    Orb(a + Vector3.up * .08f, b, Spirit, .18f, travel, .015f);
                    Burst(b, Purple, .58f, .40f, 10, travel * .85f);
                    break;
                case "Shadow Pulse":
                    Ring(a, Spirit, .69f, .43f, 15);
                    Burst(a, Purple, .79f, .46f, 12, .02f);
                    break;
                case "Wild Charge":
                    Arc(a, b, Lightning, .32f, travel, 0);
                    Arc(a + Vector3.left * .18f, b + Vector3.right * .16f, Steel, .11f, travel, .03f);
                    Burst(b, Lightning, .65f, .39f, 12, travel * .74f);
                    break;
                case "Mystical Burst":
                    Ring(a, Pink, .40f, .34f, 9);
                    Orb(a, b, Spirit, .29f, travel, .025f);
                    Burst(b, Pink, .58f, .44f, 12, travel * .8f);
                    break;
                case "Flame Charge":
                    for (int i = -2; i <= 2; i++)
                        Arc(a + Vector3.right * (i * .035f), b + Vector3.right * (i * .14f),
                            i == 0 ? Amber : Fire, .12f, travel, Mathf.Abs(i) * .022f);
                    Ring(a, Amber, .37f, .30f, 8);
                    break;
                case "Dragon Darts: Barrage":
                    Ring(a, Purple, .55f, .36f, 12);
                    Burst(a, Spirit, .35f, .31f, 6, .07f);
                    break;
            }
            castVisualScale = 1f;
            return true;
        }

        /// <summary>Custom contact patterns. Called for each resolved hit, never deals damage.</summary>
        public bool TryPlayImpact(MonsterUnit victim, string ability)
        {
            if (victim == null || !IsNewAbility(ability)) return false;
            Vector3 b = At(victim);
            switch (ability)
            {
                case "Play Rough":
                    Burst(b, Pink, .45f, .28f, 9);
                    Ring(b, Purple, .29f, .25f, 7);
                    break;
                case "Leaf Blade": Burst(b, Leaf, .44f, .30f, 9); break;
                case "Avalanche Tackle": Burst(b, IceBright, .45f, .32f, 9); break;
                case "Four-Armed Fury": Burst(b, Amber, .27f, .21f, 5); break;
                case "Mist Ball": Burst(b, Pink, .50f, .34f, 10); break;
                case "Luster Purge": Burst(b, Spirit, .48f, .28f, 8); break;
                case "Plasma Fists": Burst(b, Lightning, .54f, .34f, 12); break;
                case "Thunder Rampage": Burst(b, Lightning, .45f, .31f, 11); break;
                case "Magma Cannon": Burst(b, Fire, .63f, .37f, 12); break;
                case "Seismic Guard": GroundRing(b, Earth, .44f, .30f); break;
                case "Heavy Slam": Burst(b, Steel, .54f, .30f, 11); break;
                case "Hammer Arm": Burst(b, Earth, .34f, .28f, 7); break;
                case "Spectral Ambush": Burst(b, Spirit, .49f, .35f, 10); break;
                case "Volcanic Pressure": Burst(b, Fire, .50f, .32f, 10); break;
                case "Savage Bite": Burst(b, Scarlet, .32f, .26f, 7); break;
                case "Shadow Ball": Burst(b, Purple, .59f, .43f, 13); break;
                case "Shadow Pulse": Burst(b, Spirit, .37f, .32f, 7); break;
                case "Wild Charge": Burst(b, Lightning, .50f, .30f, 12); break;
                case "Mystical Burst": Burst(b, Pink, .52f, .35f, 10); break;
                case "Flame Charge": Burst(b, Fire, .36f, .28f, 9); break;
                case "Heavy Spark": Burst(b, Lightning, .62f, .42f, 12); break;
                default: return false; // Forest Shelter and Barrage are cast-only visual states.
            }
            return true;
        }

        // Decorative VFX only. Damage is handled by the combat director.
        public void PlayEonBeam(MonsterUnit source, MonsterUnit victim)
        {
            if (source == null || victim == null) return;
            Vector3 from = At(source), to = At(victim);
            Arc(from, to, Spirit, .26f, .22f, 0f);
            Arc(from + Vector3.left * .08f, to + Vector3.right * .08f,
                Pink, .10f, .22f, .025f);
            Burst(to, Spirit, .50f, .35f, 10, .13f);
        }

        public void PlayStormlynxChain(MonsterUnit source, MonsterUnit victim)
        {
            if (source == null || victim == null) return;
            Vector3 from = At(source), to = At(victim);
            Spark(from, to, .21f, 0f);
            Burst(to, Lightning, .46f, .32f, 9, .12f);
        }

        public void PlayHeavySpark(MonsterUnit caster, MonsterUnit victim)
        {
            if (caster == null || victim == null) return;
            Spark(At(caster), At(victim), .22f, 0f);
            TryPlayImpact(victim, "Heavy Spark");
        }

        public void PlayDragonDart(MonsterUnit caster, MonsterUnit victim, int dartIndex)
        {
            if (caster == null || victim == null) return;
            Vector3 from = At(caster) + Vector3.right * (dartIndex == 0 ? -.11f : .11f);
            Vector3 to = At(victim) + Vector3.up * (dartIndex == 0 ? .06f : -.06f);
            Arc(from, to, dartIndex == 0 ? Spirit : Purple, .18f, .24f, dartIndex * .035f);
            Orb(from, to, new Color(.86f, .79f, 1f), .09f, .24f, dartIndex * .035f);
            Burst(to, Purple, .23f, .25f, 5, .18f + dartIndex * .035f);
        }

        public void PlayTraitPulse(MonsterUnit subject, string trait)
        {
            if (subject == null) return;
            Vector3 center = At(subject);
            Color color = trait == "Grit" ? Scarlet : trait == "Overdrive" ? Lightning : Leaf;
            Ring(center, color, .46f, .37f, 10);
            Burst(center, color, .35f, .36f, 7);
        }

        public void PlayIronFortress(MonsterUnit defender, MonsterUnit attacker)
        {
            if (defender == null || attacker == null) return;
            Vector3 from = At(defender), to = At(attacker);
            Ring(from, Steel, .49f, .30f, 9);
            Arc(from, to, Steel, .20f, .23f, 0);
            Burst(to, Steel, .36f, .30f, 8, .13f);
        }

        public void PlayProtectionLink(MonsterUnit caster, MonsterUnit ally)
        {
            if (caster == null || ally == null) return;
            Vector3 a = At(caster), b = At(ally);
            Arc(a, b, Leaf, .11f, .35f, 0f);
            Ring(b, Leaf, .38f, .42f, 9, .15f);
        }

        public void PlayShadowThief(MonsterUnit caster, MonsterUnit victim)
        {
            if (caster == null || victim == null) return;
            Vector3 from = At(victim), to = At(caster);
            Arc(from, to, Spirit, .17f, .33f, 0f);
            Burst(from, Purple, .33f, .35f, 8);
            Ring(to, Spirit, .41f, .37f, 10, .19f);
        }

        private void Spark(Vector3 a, Vector3 b, float duration, float delay)
        {
            Arc(a, b, Lightning, .33f, duration, delay);
            Arc(a + Vector3.left * .13f, b + Vector3.right * .10f, Color.white, .15f, duration, delay + .02f);
            Burst(b, Lightning, .49f, .33f, 10, delay + duration * .7f);
        }

        private static Vector3 At(MonsterUnit unit) => unit.transform.position + Vector3.up * .78f;

        private void Ring(Vector3 center, Color tint, float radius, float duration, int points, float delay = 0f)
        {
            for (int i = 0; i < points; ++i)
            {
                float angle = i * 2f * Mathf.PI / points;
                Vector3 dir = new Vector3(Mathf.Cos(angle), .15f, Mathf.Sin(angle));
                Add(center + dir * radius * .28f, center + dir * radius, tint,
                    new Vector3(.10f, .09f, .10f), duration, delay + i * .007f);
            }
        }

        private void GroundRing(Vector3 center, Color tint, float radius, float duration)
        {
            center += Vector3.down * .52f;
            Ring(center, tint, radius, duration, 14);
        }

        private void Burst(Vector3 center, Color tint, float radius, float duration, int points, float delay = 0f)
        {
            for (int i = 0; i < points; ++i)
            {
                float angle = i * 2f * Mathf.PI / points;
                Vector3 dir = new Vector3(Mathf.Cos(angle), .20f + .22f * (i % 3), Mathf.Sin(angle));
                Add(center, center + dir * radius, tint, new Vector3(.09f, .10f, .13f),
                    duration, delay + i * .009f);
            }
        }

        private void Arc(Vector3 from, Vector3 to, Color tint, float thickness, float duration, float delay)
        {
            Vector3 diff = to - from;
            Quaternion rotation = diff.sqrMagnitude > .0001f ? Quaternion.LookRotation(diff) : Quaternion.identity;
            Add(from, to, tint, new Vector3(thickness, thickness, thickness * 2.3f), duration, delay,
                PrimitiveType.Cube, rotation);
        }

        private void Orb(Vector3 from, Vector3 to, Color tint, float diameter, float duration, float delay)
        {
            Add(from, to, tint, Vector3.one * diameter, duration, delay);
        }

        private void Add(Vector3 from, Vector3 to, Color color, Vector3 size, float duration,
            float delay = 0f, PrimitiveType primitive = PrimitiveType.Sphere, Quaternion rotation = default(Quaternion))
        {
            if (active.Count >= MaximumVisuals) return;
            Material material = GetMaterial(color);
            if (material == null) return;
            GameObject go = GameObject.CreatePrimitive(primitive);
            go.name = "U6.10.1 VFX";
            go.transform.SetParent(transform, true);
            go.transform.position = from;
            size *= castVisualScale;
            go.transform.localScale = size;
            go.transform.rotation = rotation == default(Quaternion) ? Quaternion.identity : rotation;
            Collider collider = go.GetComponent<Collider>();
            if (collider != null) { collider.enabled = false; Destroy(collider); }
            MeshRenderer renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.sharedMaterial = material;
            active.Add(new Particle
            {
                Object = go, From = from, To = to, Size = size, Rotation = go.transform.rotation,
                Born = Time.time, Delay = delay, Duration = Mathf.Max(.06f, duration),
                Moving = (from - to).sqrMagnitude > .000001f
            });
        }

        private Material GetMaterial(Color color)
        {
            Color32 c = color;
            int key = (c.r << 24) | (c.g << 16) | (c.b << 8) | c.a;
            Material material;
            if (materials.TryGetValue(key, out material) && material != null) return material;
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) return null;
            material = new Material(shader);
            material.color = color;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            materials[key] = material;
            return material;
        }

        private void Update()
        {
            float now = Time.time;
            for (int i = active.Count - 1; i >= 0; --i)
            {
                Particle fx = active[i];
                if (fx.Object == null) { active.RemoveAt(i); continue; }
                float age = now - fx.Born - fx.Delay;
                if (age < 0f) { fx.Object.SetActive(false); continue; }
                if (!fx.Object.activeSelf) fx.Object.SetActive(true);
                float t = Mathf.Clamp01(age / fx.Duration);
                float ease = t * t * (3f - 2f * t);
                fx.Object.transform.position = fx.Moving ? Vector3.Lerp(fx.From, fx.To, ease) : fx.From;
                fx.Object.transform.localScale = fx.Size * Mathf.Max(.01f, 1f - .70f * t);
                if (t >= 1f) { Destroy(fx.Object); active.RemoveAt(i); }
            }
        }

        public void ClearEffects()
        {
            foreach (Particle fx in active)
                if (fx.Object != null) Destroy(fx.Object);
            active.Clear();
        }

        private void OnDestroy()
        {
            ClearEffects();
            foreach (Material material in materials.Values)
                if (material != null) Destroy(material);
            materials.Clear();
        }
    }
}
