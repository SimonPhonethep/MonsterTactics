using System;
using System.Collections.Generic;
using UnityEngine;

namespace MonsterTactics
{
    /// <summary>
    /// Stores reversible scene-specific camera/sprite sizes. No Update or runtime
    /// behavior, so this component cannot interfere with animation or combat.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MonsterTacticsU43PresentationSettings : MonoBehaviour
    {
        [Serializable]
        public sealed class SpriteSize
        {
            public Transform Visual;
            public Vector3 OriginalLocalScale;
        }

        [SerializeField] private Camera targetCamera;
        [SerializeField] private float initialCameraSize;
        [SerializeField] private List<SpriteSize> sprites = new List<SpriteSize>();

        public Camera TargetCamera => targetCamera;
        public float InitialCameraSize => initialCameraSize;
        public List<SpriteSize> Sprites => sprites;

        public void Initialize(Camera camera)
        {
            if (targetCamera != null) return;
            targetCamera = camera;
            initialCameraSize = camera != null ? camera.orthographicSize : 7.25f;
        }

        public void RecordVisual(Transform visual)
        {
            if (visual == null) return;
            foreach (SpriteSize item in sprites)
                if (item.Visual == visual) return;
            sprites.Add(new SpriteSize { Visual = visual, OriginalLocalScale = visual.localScale });
        }
    }
}
