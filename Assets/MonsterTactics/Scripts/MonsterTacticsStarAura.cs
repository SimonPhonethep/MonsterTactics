using UnityEngine;

namespace MonsterTactics
{
    /// <summary>
    /// Persistent silver (2★) or gold (3★) outline for a single-form Monster.
    /// Works both on deployed units and on the separate world-hex bench sprites.
    /// Never creates textures or sprites during the frame loop.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MonsterTacticsStarAura : MonoBehaviour
    {
        private SpriteRenderer target;
        private SpriteRenderer inner;
        private SpriteRenderer outer;
        private int starLevel = 1;

        public void SetStage(int level)
        {
            starLevel = Mathf.Clamp(level, 1, 3);
            EnsureLayers();
            RefreshLayers();
        }

        private void Awake()
        {
            target = GetComponent<SpriteRenderer>();
            EnsureLayers();
            RefreshLayers();
        }

        private void OnEnable() { RefreshLayers(); }

        private void LateUpdate() { RefreshLayers(); }

        private void EnsureLayers()
        {
            if (target == null) target = GetComponent<SpriteRenderer>();
            if (target == null) return;

            // U6.2.7 generated a new sparkle sprite every frame; disable any
            // legacy sparkle object immediately so the magenta square disappears.
            Transform brokenSparkle = transform.Find("Stage Aura Sparkle");
            if (brokenSparkle != null) brokenSparkle.gameObject.SetActive(false);

            if (inner == null) inner = GetOrCreate("Stage Aura Inner");
            if (outer == null) outer = GetOrCreate("Stage Aura Outer");
        }

        private SpriteRenderer GetOrCreate(string childName)
        {
            Transform child = transform.Find(childName);
            GameObject visual = child != null ? child.gameObject : new GameObject(childName);
            visual.transform.SetParent(transform, false);
            SpriteRenderer renderer = visual.GetComponent<SpriteRenderer>();
            if (renderer == null) renderer = visual.AddComponent<SpriteRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }

        private void RefreshLayers()
        {
            if (target == null) target = GetComponent<SpriteRenderer>();
            if (target == null) return;
            if (inner == null || outer == null) EnsureLayers();
            if (inner == null || outer == null) return;

            bool show = starLevel >= 2 && target.enabled && target.sprite != null;
            inner.enabled = show;
            outer.enabled = show;
            if (!show) return;

            bool gold = starLevel == 3;
            Color core = gold ? new Color(1f, .87f, .35f, .68f)
                : new Color(.88f, .96f, 1f, .70f);
            Color bloom = gold ? new Color(1f, .70f, .13f, .38f)
                : new Color(.67f, .84f, 1f, .42f);
            // When dragging a reserve, the main sprite fades; its aura should too.
            core.a *= target.color.a;
            bloom.a *= target.color.a;

            SetLayer(inner, core, 1.15f, -1);
            SetLayer(outer, bloom, 1.32f, -2);
        }

        private void SetLayer(SpriteRenderer layer, Color color, float size, int orderOffset)
        {
            // Mirror the current animation frame, sorting layer and material.
            layer.sprite = target.sprite;
            layer.sharedMaterial = target.sharedMaterial;
            layer.flipX = target.flipX;
            layer.flipY = target.flipY;
            layer.sortingLayerID = target.sortingLayerID;
            layer.sortingOrder = target.sortingOrder + orderOffset;
            layer.color = color;
            layer.transform.localPosition = Vector3.zero;
            layer.transform.localRotation = Quaternion.identity;
            layer.transform.localScale = Vector3.one * size;
        }
    }
}
