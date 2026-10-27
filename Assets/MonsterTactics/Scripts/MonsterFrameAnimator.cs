using UnityEngine;

namespace MonsterTactics
{
    /// <summary>
    /// Plays imported PNG frames on the existing U3 SpriteRenderer.
    /// Does not modify MonsterUnit, unit positions, HP, the trainer, or the camera.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MonsterFrameAnimator : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer targetRenderer;
        [SerializeField] private Sprite[] frames = new Sprite[0];
        [SerializeField] private float[] durations = new float[0];
        [SerializeField, Range(0.25f, 3f)] private float playbackSpeed = 1f;

        private int currentFrame;
        private float elapsed;

        public void SetAnimation(SpriteRenderer renderer, Sprite[] newFrames, float[] frameDurations)
        {
            targetRenderer = renderer;
            frames = newFrames ?? new Sprite[0];
            durations = frameDurations ?? new float[0];
            currentFrame = 0;
            elapsed = 0f;
            if (targetRenderer != null && frames.Length > 0)
                targetRenderer.sprite = frames[0];
        }

        private void Awake()
        {
            if (targetRenderer == null) targetRenderer = GetComponent<SpriteRenderer>();
        }

        private void OnEnable()
        {
            currentFrame = 0;
            elapsed = 0f;
            if (targetRenderer == null) targetRenderer = GetComponent<SpriteRenderer>();
            if (targetRenderer != null && frames != null && frames.Length > 0)
                targetRenderer.sprite = frames[0];
        }

        private void Update()
        {
            if (targetRenderer == null || frames == null || frames.Length <= 1) return;

            elapsed += Time.deltaTime * playbackSpeed;
            // Cap frame catch-up if the Editor pauses or the frame rate briefly drops.
            int advanced = 0;
            while (advanced < frames.Length && elapsed >= GetDuration(currentFrame))
            {
                elapsed -= GetDuration(currentFrame);
                currentFrame = (currentFrame + 1) % frames.Length;
                targetRenderer.sprite = frames[currentFrame];
                advanced++;
            }
        }

        private float GetDuration(int index)
        {
            float value = durations != null && index < durations.Length ? durations[index] : 0.12f;
            return Mathf.Clamp(value, 0.055f, 1f);
        }
    }
}
