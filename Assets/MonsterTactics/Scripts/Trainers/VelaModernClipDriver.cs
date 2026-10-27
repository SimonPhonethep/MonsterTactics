using UnityEngine;

namespace MonsterTactics
{
    /// <summary>
    /// Drives Vela's Animator using the EXISTING trainer's world-space movement.
    /// Does not move, scale or rotate any object. Keeps the U3.4.4 facing fix intact.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VelaModernClipDriver : MonoBehaviour
    {
        [Header("Connected by the U3.6 installer")]
        [SerializeField] private Animator mayAnimator;
        [SerializeField] private Transform trainerMovementRoot;

        [Header("Walk transition")]
        [SerializeField, Min(0.05f)] private float referenceSpeed = 2.8f;
        [SerializeField, Min(0f)] private float minMovingSpeed = 0.07f;
        [SerializeField, Range(0f, 0.5f)] private float blendDampingSeconds = 0.12f;

        [Header("Debug only — leave off for actual gameplay")]
        [SerializeField] private bool testWalkInPlace;
        [SerializeField] private bool showDebugText;

        private static readonly int MoveHash = Animator.StringToHash("Move");
        private Vector3 previousPosition;
        private float currentSpeed;
        private bool trackingReady;

        public void Configure(Animator animator, Transform movementRoot, bool test)
        {
            mayAnimator = animator;
            trainerMovementRoot = movementRoot;
            testWalkInPlace = test;
            trackingReady = false;
        }

        private void Awake()
        {
            if (mayAnimator == null) mayAnimator = GetComponent<Animator>();
            if (trainerMovementRoot == null)
            {
                TrainerMovement mover = GetComponentInParent<TrainerMovement>(true);
                if (mover != null) trainerMovementRoot = mover.transform;
            }
        }

        private void OnEnable()
        {
            trackingReady = false; // Switching 2D/3D skins must not trigger false steps.
            currentSpeed = 0f;
        }

        private void LateUpdate()
        {
            if (mayAnimator == null || mayAnimator.runtimeAnimatorController == null)
                return;

            if (trainerMovementRoot == null)
            {
                TrainerMovement mover = GetComponentInParent<TrainerMovement>(true);
                if (mover == null) return;
                trainerMovementRoot = mover.transform;
                trackingReady = false;
            }

            Vector3 position = trainerMovementRoot.position;
            if (!trackingReady)
            {
                previousPosition = position;
                trackingReady = true;
                mayAnimator.SetFloat(MoveHash, testWalkInPlace ? 1f : 0f);
                return;
            }

            Vector3 displacement = position - previousPosition;
            displacement.y = 0f;
            previousPosition = position;
            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0.000001f) return;

            currentSpeed = displacement.magnitude / deltaTime;
            float move = testWalkInPlace ? 1f :
                (currentSpeed > minMovingSpeed ? Mathf.Clamp01(currentSpeed / referenceSpeed) : 0f);

            // Animator owns *only Vela's skeletal transforms*.
            // TrainerMovement and TrainerSkinController own world motion and direction.
            mayAnimator.SetFloat(MoveHash, move, blendDampingSeconds, deltaTime);
        }

        private void OnGUI()
        {
            if (!showDebugText || !Application.isPlaying) return;
            GUI.Box(new Rect(12f, 12f, 280f, 70f), "Vela Animator U3.6");
            GUI.Label(new Rect(20f, 38f, 260f, 24f),
                "Speed: " + currentSpeed.ToString("F2") +
                "   Test: " + (testWalkInPlace ? "ON" : "OFF"));
        }
    }
}
