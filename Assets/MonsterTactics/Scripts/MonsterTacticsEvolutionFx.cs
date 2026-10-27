using UnityEngine;

namespace MonsterTactics
{
    /// <summary>
    /// Short, prefab-free upgrade burst. Runs on its own GameObject, so a benched
    /// (inactive) Monster can still show its evolution flash over the 3D bench.
    /// No changes to combat stats, the arena, or Monster input.
    /// </summary>
    public sealed class MonsterTacticsEvolutionFx : MonoBehaviour
    {
        private const float Lifetime = .88f;
        private const int SparkCount = 10;
        private static Sprite softGlow;
        private static Sprite ringGlow;
        private static Sprite sparkDiamond;
        private readonly SpriteRenderer[] sparks = new SpriteRenderer[SparkCount];
        private SpriteRenderer glow;
        private SpriteRenderer ring;
        private Camera view;
        private int starLevel;
        private float elapsed;
        private Color hue;
        private GUIStyle textStyle;

        public static void Spawn(Vector3 worldCenter, int stage, Camera camera)
        {
            GameObject container = new GameObject("MonsterTactics Evo " + stage + " Star Burst");
            container.transform.position = worldCenter;
            MonsterTacticsEvolutionFx fx = container.AddComponent<MonsterTacticsEvolutionFx>();
            fx.Initialize(stage, camera);
        }

        private void Initialize(int stage, Camera camera)
        {
            starLevel = Mathf.Clamp(stage, 2, 3);
            view = camera != null ? camera : Camera.main;
            hue = starLevel == 2 ? new Color(.86f, .90f, .96f, 1f) : new Color(1f, .80f, .26f, 1f);
            EnsureSprites();
            glow = MakePart("Soft glow", softGlow, 5);
            ring = MakePart("Expanding ring", ringGlow, 6);
            for (int i = 0; i < SparkCount; i++)
                sparks[i] = MakePart("Spark " + (i + 1), sparkDiamond, 7);
        }

        private SpriteRenderer MakePart(string displayName, Sprite sprite, int sortingOrder)
        {
            GameObject element = new GameObject(displayName);
            element.transform.SetParent(transform, false);
            SpriteRenderer renderer = element.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        private static void EnsureSprites()
        {
            if (softGlow == null) softGlow = MakeSprite("Evolution glow", 64, 0);
            if (ringGlow == null) ringGlow = MakeSprite("Evolution ring", 64, 1);
            if (sparkDiamond == null) sparkDiamond = MakeSprite("Evolution spark", 32, 2);
        }

        private static Sprite MakeSprite(string displayName, int size, int shape)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = displayName;
            texture.hideFlags = HideFlags.DontSave;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = ((x + .5f) / size - .5f) * 2f;
                    float dy = ((y + .5f) / size - .5f) * 2f;
                    float radius = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha;
                    if (shape == 0)
                        alpha = Mathf.Pow(Mathf.Clamp01(1f - radius), 2f) * .85f;
                    else if (shape == 1)
                        alpha = Mathf.Clamp01(1f - Mathf.Abs(radius - .72f) * 16f);
                    else
                        alpha = Mathf.Pow(Mathf.Clamp01(1f - (Mathf.Abs(dx) + Mathf.Abs(dy))), .80f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size),
                new Vector2(.5f, .5f), size, 0, SpriteMeshType.FullRect);
            sprite.name = displayName;
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }

        private static void Tint(SpriteRenderer renderer, Color color, float alpha)
        {
            if (renderer == null) return;
            renderer.color = new Color(color.r, color.g, color.b, Mathf.Clamp01(alpha));
        }

        private void LateUpdate()
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / Lifetime);
            float ease = 1f - (1f - t) * (1f - t);
            if (view == null) view = Camera.main;
            if (view != null) transform.rotation = view.transform.rotation;

            if (glow != null)
            {
                glow.transform.localScale = Vector3.one * (1.0f + 1.1f * ease);
                Tint(glow, Color.white, .56f * (1f - t));
            }
            if (ring != null)
            {
                ring.transform.localScale = Vector3.one * (.62f + 1.9f * ease);
                Tint(ring, hue, (1f - t) * (starLevel == 3 ? .96f : .80f));
            }
            for (int i = 0; i < sparks.Length; i++)
            {
                SpriteRenderer sparkle = sparks[i];
                if (sparkle == null) continue;
                float angle = Mathf.PI * 2f * i / SparkCount + (starLevel == 3 ? .10f : 0f);
                float dist = .12f + ease * (starLevel == 3 ? .96f : .70f);
                sparkle.transform.localPosition = new Vector3(Mathf.Cos(angle) * dist,
                    Mathf.Sin(angle) * dist, -.01f);
                sparkle.transform.localRotation = Quaternion.Euler(0f, 0f, i * 36f + elapsed * 160f);
                sparkle.transform.localScale = Vector3.one * (.10f + .075f * Mathf.Sin(t * Mathf.PI));
                Tint(sparkle, i % 3 == 0 ? Color.white : hue, (1f - t) * 1.1f);
            }
            if (elapsed >= Lifetime) Destroy(gameObject);
        }

        private void OnGUI()
        {
            if (view == null || elapsed >= Lifetime) return;
            Vector3 screen = view.WorldToScreenPoint(transform.position + Vector3.up * .55f);
            if (screen.z <= 0f) return;
            if (textStyle == null)
            {
                textStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 20,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
            }
            textStyle.normal.textColor = starLevel == 3 ? new Color(1f, .82f, .27f)
                : new Color(.92f, .96f, 1f);
            string stars = starLevel == 3 ? "★★★" : "★★";
            float y = Screen.height - screen.y - 22f - (1f - elapsed / Lifetime) * 20f;
            Rect bounds = new Rect(screen.x - 65f, y, 130f, 32f);
            Color previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01((Lifetime - elapsed) * 2.4f));
            GUI.depth = -60;
            GUI.Label(bounds, stars, textStyle);
            GUI.color = previous;
        }
    }

    /// <summary>Subtle pop on a deployed Monster's actual sprite.</summary>
    public sealed class MonsterTacticsEvolutionPulse : MonoBehaviour
    {
        private Vector3 originalScale;
        private float elapsed;
        private float intensity;
        private bool playing;

        public void Play(int stage)
        {
            originalScale = transform.localScale;
            intensity = stage == 3 ? .22f : .15f;
            elapsed = 0f;
            playing = true;
        }

        private void LateUpdate()
        {
            if (!playing) return;
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / .64f);
            float pop = Mathf.Sin(progress * Mathf.PI) * (1f - .30f * progress);
            transform.localScale = originalScale * (1f + intensity * pop);
            if (progress >= 1f)
            {
                transform.localScale = originalScale;
                playing = false;
            }
        }

        private void OnDisable()
        {
            if (playing) transform.localScale = originalScale;
            playing = false;
        }
    }

}
