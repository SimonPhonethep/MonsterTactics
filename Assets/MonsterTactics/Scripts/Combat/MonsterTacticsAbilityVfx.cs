using System.Collections.Generic;
using UnityEngine;

namespace MonsterTactics
{
    /// <summary>
    /// Lightweight, self-contained world-space ability visuals. No external art,
    /// packages, shaders, or changes to the trainer/board are required.
    /// Effects have no active colliders and clean themselves up after playback.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MonsterTacticsAbilityVfx : MonoBehaviour
    {
        private sealed class Effect
        {
            public GameObject Object;
            public Vector3 From;
            public Vector3 To;
            public float Born;
            public float StartDelay;
            public float Duration;
            public float Size;
            public bool Projectile;
            public bool Spin;
        }

        [SerializeField, Range(.75f, 1.6f)] private float effectSizeMultiplier = 1.16f;

        // Lazily installed procedural VFX for the U6.10 Monster; no prefab changes needed.
        private MonsterTacticsU610AbilityVfx monsterVfx;
        private MonsterTacticsU610AbilityVfx U610Effects
        {
            get
            {
                if (monsterVfx == null) monsterVfx = GetComponent<MonsterTacticsU610AbilityVfx>();
                if (monsterVfx == null) monsterVfx = gameObject.AddComponent<MonsterTacticsU610AbilityVfx>();
                return monsterVfx;
            }
        }

        public void PlayU612EonBeam(MonsterUnit source, MonsterUnit target)
        {
            if (source != null && target != null) U610Effects.PlayEonBeam(source, target);
        }

        public void PlayU611StormlynxChain(MonsterUnit mainTarget, MonsterUnit secondaryTarget)
        {
            if (mainTarget != null && secondaryTarget != null)
                U610Effects.PlayStormlynxChain(mainTarget, secondaryTarget);
        }

        public void PlayU610HeavySpark(MonsterUnit source, MonsterUnit victim)
        {
            if (source != null && victim != null) U610Effects.PlayHeavySpark(source, victim);
        }

        public void PlayU610DragonDart(MonsterUnit source, MonsterUnit victim, int index)
        {
            if (source != null && victim != null) U610Effects.PlayDragonDart(source, victim, index);
        }

        public void PlayU610TraitPulse(MonsterUnit source, string trait)
        {
            if (source != null) U610Effects.PlayTraitPulse(source, trait);
        }

        public void PlayU610IronFortress(MonsterUnit defender, MonsterUnit attacker)
        {
            if (defender != null && attacker != null) U610Effects.PlayIronFortress(defender, attacker);
        }

        public void PlayU610ProtectionLink(MonsterUnit caster, MonsterUnit ally)
        {
            if (caster != null && ally != null) U610Effects.PlayProtectionLink(caster, ally);
        }

        public void PlayU610ShadowThief(MonsterUnit thief, MonsterUnit victim)
        {
            if (thief != null && victim != null) U610Effects.PlayShadowThief(thief, victim);
        }


        private readonly List<Effect> effects = new List<Effect>();
        private readonly Dictionary<int, Material> materials = new Dictionary<int, Material>();
        private static readonly Color Water = new Color(.16f, .82f, 1f);
        private static readonly Color Fire = new Color(1f, .35f, .08f);
        private static readonly Color Seed = new Color(.35f, .95f, .36f);
        private static readonly Color Stone = new Color(.80f, .66f, .43f);
        private static readonly Color Dragon = new Color(.70f, .39f, 1f);
        private static readonly Color Psychic = new Color(.96f, .40f, .90f);
        private static readonly Color Electric = new Color(1f, .88f, .22f);
        private static readonly Color Ice = new Color(.40f, .88f, 1f);

        public void PlayCast(MonsterUnit source, MonsterUnit target, string ability, float delay)
        {
            if (source == null || target == null) return;
            // U6.10.3: caster recoil animation is only cosmetic.
            MonsterTacticsU610SpriteMotion motion = source.GetComponentInChildren<MonsterTacticsU610SpriteMotion>();
            if (motion != null) motion.TriggerCast();
            Vector3 from = source.transform.position + Vector3.up * .78f;
            Vector3 to = target.transform.position + Vector3.up * .78f;
            Color color = Tint(ability);
            // Custom authored visuals use the same world-space effect system.
            MonsterTacticsContentAbilityRuntime.Visual authored;
            bool custom = MonsterTacticsContentAbilityRuntime.TryGetVisual(ability, out authored);
            if (custom)
            {
                color = PresetTint(authored.Preset);
                Spawn(from, from + Vector3.up * .14f, color, .27f, .23f, 0f, false, false);
                // A clearly visible generic projectile (even with no prefab asset).
                Spawn(from, to, color, .24f * authored.Scale,
                    Mathf.Max(.15f, delay), 0f, true, false);
                if (authored.Prefab != null)
                    SpawnPrefab(authored.Prefab, from, authored.Scale, authored.Lifetime);
                return;
            }
            // Bespoke procedural casts take precedence over the old generic orb,
            // but never override an authored Content Studio prefab above.
            if (MonsterTacticsU610AbilityVfx.IsNewAbility(ability) &&
                U610Effects.TryPlayCast(source, target, ability, delay)) return;

            // Casting flash at the Monster's location.
            Spawn(from, from + Vector3.up * .14f, color, .27f, .23f, 0f, false, false);

            if (ability == "Rock Guard")
            {
                for (int i = 0; i < 8; i++)
                {
                    float theta = 2f * Mathf.PI * i / 8f;
                    Vector3 point = from + new Vector3(Mathf.Cos(theta) * .35f, -.23f,
                        Mathf.Sin(theta) * .35f);
                    Spawn(point, point + Vector3.up * .27f, Stone, .14f, .42f,
                        i * .025f, true, false);
                }
            }
            else if (ability == "Psybeam")
            {
                for (int i = -2; i <= 2; i++)
                    Spawn(from + Vector3.up * (i * .045f), to + Vector3.up * (i * .045f),
                        Psychic, .13f, Mathf.Max(.16f, delay), 0f, true, false);
            }
            else if (ability == "Dragon Rush")
            {
                Spawn(from, to, Dragon, .34f, Mathf.Max(.15f, delay), 0f, true, false);
                Spawn(from + Vector3.right * .12f, to + Vector3.right * .12f,
                    new Color(.88f, .80f, 1f), .12f, Mathf.Max(.15f, delay), 0f, true, false);
            }
            else if (ability == "Dragon Claw")
            {
                for (int i = -1; i <= 1; i++)
                    Spawn(from, to + Vector3.right * (i * .12f), Dragon,
                        .12f, Mathf.Max(.15f, delay), i * .035f, true, false);
            }
            else if (ability == "Leaf Arrow")
            {
                Spawn(from, to, Seed, .19f, Mathf.Max(.16f, delay), 0f, true, false);
                Spawn(from + Vector3.left * .06f, to + Vector3.left * .06f,
                    new Color(.76f, 1f, .44f), .10f, Mathf.Max(.16f, delay), 0f, true, false);
            }
            else if (ability == "Water Shuriken")
            {
                for (int i = 0; i < 3; i++)
                    Spawn(from, to, Water, .25f, Mathf.Max(.12f, delay), i * .13f,
                        true, true);
            }
            else if (ability == "Flamethrower")
            {
                // Fan of embers, reaching the target just before damage resolves.
                for (int i = -2; i <= 2; i++)
                {
                    Vector3 end = to + Vector3.right * (i * .11f);
                    Spawn(from, end, i == 0 ? new Color(1f, .84f, .17f) : Fire,
                        i == 0 ? .25f : .15f, Mathf.Max(.18f, delay), 0f, true, false);
                }
            }
            else if (ability == "Thunderbolt")
            {
                for (int i = -2; i <= 2; i++)
                    Spawn(from + Vector3.right * (i * .06f),
                        to + Vector3.right * (i * .12f), i == 0 ? Color.white : Electric,
                        i == 0 ? .18f : .09f, Mathf.Max(.15f, delay), i >= 0 ? i * .018f : -i * .018f,
                        true, false);
            }
            else if (ability == "Sacred Fire")
            {
                for (int i = -2; i <= 2; i++)
                    Spawn(from, to + Vector3.right * (i * .10f),
                        i == 0 ? new Color(1f, .88f, .35f) : Fire,
                        i == 0 ? .30f : .14f, Mathf.Max(.15f, delay), 0f, true, false);
            }
            else if (ability == "Aurora Beam")
            {
                // Ice-blue beam to the target, plus a visible protective halo.
                Spawn(from, to, Ice, .26f, Mathf.Max(.15f, delay), 0f, true, false);
                for (int i = 0; i < 8; i++)
                {
                    float angle = 2f * Mathf.PI * i / 8f;
                    Vector3 ring = from + new Vector3(Mathf.Cos(angle) * .33f,
                        -.20f, Mathf.Sin(angle) * .33f);
                    Spawn(ring, ring + Vector3.up * .28f, Ice, .12f, .40f,
                        i * .018f, true, false);
                }
            }
            else if (ability == "Aqua Jet")
            {
                Spawn(from, to, Water, .27f, Mathf.Max(.14f, delay), 0f, true, false);
                Spawn(from + Vector3.left * .07f, to + Vector3.left * .07f,
                    new Color(.68f, .96f, 1f), .13f, Mathf.Max(.14f, delay), 0f, true, false);
            }
            else
            {
                Spawn(from, to, color, ability == "Pyro Ball" ? .36f : .22f,
                    Mathf.Max(.15f, delay), 0f, true, false);
            }
        }

        public void PlayImpact(MonsterUnit target, string ability)
        {
            if (target == null) return;
            Vector3 hit = target.transform.position + Vector3.up * .75f;
            Color color = Tint(ability);
            MonsterTacticsContentAbilityRuntime.Visual authored;
            if (MonsterTacticsContentAbilityRuntime.TryGetVisual(ability, out authored))
            {
                color = PresetTint(authored.Preset);
                if (authored.Prefab != null)
                    SpawnPrefab(authored.Prefab, hit, authored.Scale, authored.Lifetime);
            }
            if (!MonsterTacticsContentAbilityRuntime.TryGetVisual(ability, out authored) &&
                MonsterTacticsU610AbilityVfx.IsNewAbility(ability) &&
                U610Effects.TryPlayImpact(target, ability)) return;
            for (int i = 0; i < 9; i++)
            {
                float angle = i * Mathf.PI * 2f / 9f;
                Vector3 direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle) * .50f,
                    Mathf.Sin(angle));
                Spawn(hit, hit + direction * .38f, color, .085f, .33f, 0f, true, false);
            }
            Spawn(hit, hit, Color.white, .27f, .16f, 0f, false, false);
        }

        public void PlayHealing(MonsterUnit source)
        {
            if (source == null) return;
            Vector3 center = source.transform.position + Vector3.up * .55f;
            for (int i = 0; i < 7; i++)
            {
                float angle = i * Mathf.PI * 2f / 7f;
                Vector3 from = center + new Vector3(Mathf.Cos(angle) * .20f, -.25f,
                    Mathf.Sin(angle) * .20f);
                Spawn(from, from + Vector3.up * .58f, Seed, .10f, .55f,
                    i * .03f, true, false);
            }
        }

        private readonly List<GameObject> authoredPrefabs = new List<GameObject>();

        private void SpawnPrefab(GameObject prefab, Vector3 location, float scale, float lifetime)
        {
            // Prefab is user-authored. We never alter the underlying asset.
            if (prefab == null) return;
            GameObject spawned = Instantiate(prefab, location, Quaternion.identity, transform);
            spawned.name = "U6.9.1f.3 Authored VFX - " + prefab.name;
            spawned.transform.localScale *= Mathf.Max(.1f, scale);
            foreach (Collider collider in spawned.GetComponentsInChildren<Collider>(true))
                collider.enabled = false; // visual-only: cannot intercept clicks / combat
            authoredPrefabs.Add(spawned);
            Destroy(spawned, Mathf.Max(.1f, lifetime));
        }
        private static Color PresetTint(MonsterTacticsAbilityVfxPreset preset)
        {
            switch (preset)
            {
                case MonsterTacticsAbilityVfxPreset.Fire: return Fire;
                case MonsterTacticsAbilityVfxPreset.Grass: return Seed;
                case MonsterTacticsAbilityVfxPreset.Electric: return Electric;
                case MonsterTacticsAbilityVfxPreset.Rock: return Stone;
                case MonsterTacticsAbilityVfxPreset.Dragon: return Dragon;
                case MonsterTacticsAbilityVfxPreset.Psychic: return Psychic;
                case MonsterTacticsAbilityVfxPreset.Ice: return Ice;
                case MonsterTacticsAbilityVfxPreset.Dark: return new Color(.48f, .25f, .78f);
                case MonsterTacticsAbilityVfxPreset.Healing: return new Color(.36f, 1f, .56f);
                default: return Water;
            }
        }

        private static Color Tint(string ability)
        {
            switch (ability)
            {
                case "Flamethrower":
                case "Pyro Ball": return Fire;
                case "Leech Seed":
                case "Leaf Arrow": return Seed;
                case "Rock Guard": return Stone;
                case "Dragon Rush":
                case "Dragon Claw": return Dragon;
                case "Psybeam": return Psychic;
                case "Thunderbolt": return Electric;
                case "Sacred Fire": return Fire;
                case "Aurora Beam": return Ice;
                default: return Water;
            }
        }

        private GameObject CreateVisual(string title, Color color)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "U4.1 VFX - " + title;
            go.transform.SetParent(transform, true);
            Collider hitbox = go.GetComponent<Collider>();
            if (hitbox != null) { hitbox.enabled = false; Destroy(hitbox); }
            MeshRenderer renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.sharedMaterial = GetMaterial(color);
            return go;
        }

        private Material GetMaterial(Color color)
        {
            Color32 keyColor = color;
            int key = (keyColor.r << 24) | (keyColor.g << 16) | (keyColor.b << 8) | keyColor.a;
            if (materials.TryGetValue(key, out Material cached) && cached != null) return cached;
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Standard");
            Material material = new Material(shader);
            material.color = color;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            materials[key] = material;
            return material;
        }

        private void Spawn(Vector3 from, Vector3 to, Color color, float size,
            float duration, float startDelay, bool moving, bool spin)
        {
            GameObject go = CreateVisual(spin ? "Shuriken" : "Spark", color);
            go.transform.position = from;
            go.transform.localScale = Vector3.one * (size * effectSizeMultiplier);
            effects.Add(new Effect
            {
                Object = go,
                From = from,
                To = to,
                Size = size * effectSizeMultiplier,
                Born = Time.time,
                Duration = Mathf.Max(.05f, duration),
                StartDelay = startDelay,
                Projectile = moving,
                Spin = spin
            });
        }

        private void Update()
        {
            float now = Time.time;
            for (int i = effects.Count - 1; i >= 0; i--)
            {
                Effect fx = effects[i];
                if (fx.Object == null) { effects.RemoveAt(i); continue; }
                float age = now - fx.Born - fx.StartDelay;
                if (age < 0f) { fx.Object.SetActive(false); continue; }
                if (!fx.Object.activeSelf) fx.Object.SetActive(true);
                float t = Mathf.Clamp01(age / fx.Duration);
                fx.Object.transform.position = fx.Projectile ?
                    Vector3.Lerp(fx.From, fx.To, t) : fx.From;
                float size = fx.Size * (fx.Projectile ? Mathf.Lerp(1f, .55f, t) : 1f - t);
                fx.Object.transform.localScale = Vector3.one * Mathf.Max(.001f, size);
                if (fx.Spin) fx.Object.transform.Rotate(Vector3.forward, 900f * Time.deltaTime);
                if (t >= 1f)
                {
                    Destroy(fx.Object);
                    effects.RemoveAt(i);
                }
            }
        }

        public void ClearEffects()
        {
            if (monsterVfx != null) monsterVfx.ClearEffects();
            foreach (GameObject prefab in authoredPrefabs)
                if (prefab != null) Destroy(prefab);
            authoredPrefabs.Clear();
            foreach (Effect effect in effects)
                if (effect.Object != null) Destroy(effect.Object);
            effects.Clear();
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
