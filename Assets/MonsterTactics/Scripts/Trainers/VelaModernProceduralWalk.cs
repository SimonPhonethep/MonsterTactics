using UnityEngine;

namespace MonsterTactics
{
    /// <summary>
    /// ORAS Vela Generic rig walk / idle pose. Only deforms Vela's imported bones and
    /// slightly bobs her *child model*. Never moves or rotates the TrainerMovement root.
    /// Attach to "Vela ORAS Model (Imported)" under Modern Vela 3D - Proxy.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(5000)]
    public sealed class VelaModernProceduralWalk : MonoBehaviour
    {
        [Header("ORAS FBX bones (assigned automatically by U3.5 menu)")]
        [SerializeField] private Transform leftThigh;
        [SerializeField] private Transform rightThigh;
        [SerializeField] private Transform leftShin;
        [SerializeField] private Transform rightShin;
        [SerializeField] private Transform leftArm;
        [SerializeField] private Transform rightArm;
        [SerializeField] private Transform leftShoulder;
        [SerializeField] private Transform rightShoulder;
        [SerializeField] private Transform upperSpine;
        [SerializeField] private Transform head;

        [Header("Movement reference — read only; trainer navigation is untouched")]
        [SerializeField] private Transform trainerRoot;
        [SerializeField, Min(0.1f)] private float referenceWalkSpeed = 2.8f;
        [SerializeField, Min(0.1f)] private float stepsPerSecond = 1.85f;
        [SerializeField, Min(0.1f)] private float speedResponse = 11f;
        [SerializeField, Min(0f)] private float movementThreshold = 0.06f;

        [Header("Relaxed standing shoulders (verified in Unity)")]
        [SerializeField] private float leftShoulderRelaxedZ = 180f;
        [SerializeField] private float rightShoulderRelaxedZ = -180f;

        [Header("Walking — tuned for the chibi ORAS rig")]
        [SerializeField, Range(0f, 60f)] private float legSwingDegrees = 40f;
        [SerializeField, Range(0f, 65f)] private float kneeBendDegrees = 28f;
        [SerializeField, Range(0f, 45f)] private float armSwingDegrees = 27f;
        [SerializeField, Range(0f, 25f)] private float elbowBendDegrees = 7f;
        [SerializeField, Range(0f, 8f)] private float torsoLeanDegrees = 2f;
        [SerializeField, Range(0f, 8f)] private float torsoTwistDegrees = 2.2f;
        [SerializeField, Range(0f, 0.04f)] private float extraBodyBounce = 0.012f;

        [Header("Idle")]
        [SerializeField, Range(0f, 6f)] private float idleBreathingDegrees = 0.9f;
        [SerializeField, Range(0f, 4f)] private float idleBreathingFrequency = 0.34f;

        [Header("Rig-axis corrections (only change if joints bend backwards)")]
        [SerializeField] private bool invertLegSwing;
        [SerializeField] private bool invertKneeBend;
        [SerializeField] private bool invertArmSwing;

        [Header("Diagnostic")]
        [SerializeField] private bool forceWalkTest;
        [SerializeField] private bool showDebugOverlay;
        [SerializeField, HideInInspector] private int configVersion;

        private Transform[] animatedBones;
        private Quaternion[] bindRotations;
        private Quaternion relaxedLeftShoulder;
        private Quaternion relaxedRightShoulder;
        private Vector3 originalModelLocalPosition;
        private Vector3 previousTrainerPosition;
        private float walkBlend;
        private float walkPhase;
        private float measuredSpeed;
        private bool poseReady;
        private bool initialized;
        private bool warnedMissingBones;

        /// <summary>Can run in Edit mode on inactive skins; does NOT rotate any bones.</summary>
        public bool AutoBindBones()
        {
            leftThigh = FindBone("LThigh");
            rightThigh = FindBone("RThigh");
            leftShin = FindBone("LLeg");
            rightShin = FindBone("RLeg");
            leftArm = FindBone("LForeArm") ?? FindBone("LArmB");
            rightArm = FindBone("RForeArm") ?? FindBone("RArmB");
            leftShoulder = FindBone("LShoulder");
            rightShoulder = FindBone("RShoulder");
            upperSpine = FindBone("Spine2");
            head = FindBone("Head");

            TrainerMovement movement = GetComponentInParent<TrainerMovement>(true);
            trainerRoot = movement != null ? movement.transform : null;
            return leftThigh != null && rightThigh != null &&
                   leftShoulder != null && rightShoulder != null && trainerRoot != null;
        }

        private Transform FindBone(string boneName)
        {
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
                if (t.name == boneName) return t;
            return null;
        }

        /// <summary>Called from menu. Existing Inspector tweaks are kept after first migration.</summary>
        public bool ConfigurePolishedWalk(bool testInPlace)
        {
            if (configVersion < 35)
            {
                // Fields from U3.4.x may already be serialized at their old values.
                // Upgrade them once, but never overwrite changes made after U3.5.
                legSwingDegrees = 40f;
                kneeBendDegrees = 28f;
                armSwingDegrees = 27f;
                elbowBendDegrees = 7f;
                torsoLeanDegrees = 2f;
                torsoTwistDegrees = 2.2f;
                extraBodyBounce = 0.012f;
                stepsPerSecond = 1.85f;
                idleBreathingDegrees = 0.9f;
                leftShoulderRelaxedZ = 180f;
                rightShoulderRelaxedZ = -180f;
                configVersion = 35;
            }
            forceWalkTest = testInPlace;
            showDebugOverlay = testInPlace;
            return AutoBindBones();
        }

        private void Awake()
        {
            AutoBindBones();
            CapturePose();
            initialized = true;
            ResetTracking();
        }

        private void OnEnable()
        {
            if (initialized) ResetTracking();
        }

        private void ResetTracking()
        {
            if (trainerRoot != null) previousTrainerPosition = trainerRoot.position;
            measuredSpeed = 0f;
            walkBlend = 0f;
            walkPhase = 0f;
        }

        private void CapturePose()
        {
            animatedBones = new[]
            {
                leftThigh, rightThigh, leftShin, rightShin,
                leftArm, rightArm, leftShoulder, rightShoulder,
                upperSpine, head
            };
            bindRotations = new Quaternion[animatedBones.Length];
            for (int i = 0; i < animatedBones.Length; ++i)
                bindRotations[i] = animatedBones[i] != null
                    ? animatedBones[i].localRotation : Quaternion.identity;

            originalModelLocalPosition = transform.localPosition;
            poseReady = leftThigh != null && rightThigh != null &&
                        leftShoulder != null && rightShoulder != null;
            if (!poseReady && !warnedMissingBones)
            {
                warnedMissingBones = true;
                Debug.LogWarning("MonsterTactics U3.5: Vela thigh/shoulder bones not found. " +
                                 "Use MonsterTactics > Trainers > Apply Vela Animation Polish (U3.5).", this);
            }
            if (poseReady)
            {
                // Absolute angles work even if the user saved a +180/-180 scene pose.
                relaxedLeftShoulder = RelaxedShoulder(leftShoulder, leftShoulderRelaxedZ);
                relaxedRightShoulder = RelaxedShoulder(rightShoulder, rightShoulderRelaxedZ);
            }
        }

        private static Quaternion RelaxedShoulder(Transform bone, float z)
        {
            Vector3 originalEuler = bone.localEulerAngles;
            return Quaternion.Euler(originalEuler.x, originalEuler.y, z);
        }

        private void LateUpdate()
        {
            if (!poseReady) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            // Follow root displacement (including both mouse-movement and traversing tiles).
            // Do not move or rotate this root: U3.4.4 already fixed Vela's facing.
            if (trainerRoot == null)
            {
                TrainerMovement movement = GetComponentInParent<TrainerMovement>(true);
                if (movement != null)
                {
                    trainerRoot = movement.transform;
                    previousTrainerPosition = trainerRoot.position;
                }
            }
            if (trainerRoot != null)
            {
                Vector3 delta = trainerRoot.position - previousTrainerPosition;
                previousTrainerPosition = trainerRoot.position;
                delta.y = 0f;
                measuredSpeed = delta.magnitude / dt;
            }
            else measuredSpeed = 0f;

            float speed = forceWalkTest ? referenceWalkSpeed : measuredSpeed;
            float targetBlend = speed >= movementThreshold
                ? Mathf.Clamp01(speed / referenceWalkSpeed) : 0f;
            float smooth = 1f - Mathf.Exp(-speedResponse * dt);
            walkBlend = Mathf.Lerp(walkBlend, targetBlend, smooth);
            if (walkBlend < 0.001f) walkBlend = 0f;

            if (walkBlend > 0.001f)
            {
                float cadence = forceWalkTest ? 0.8f
                    : Mathf.Clamp(speed / referenceWalkSpeed, 0.7f, 1.4f);
                walkPhase += dt * stepsPerSecond * 2f * Mathf.PI * cadence;
                if (walkPhase >= Mathf.PI * 100f) walkPhase %= Mathf.PI * 2f;
            }

            float legDirection = invertLegSwing ? -1f : 1f;
            float leftStep = Mathf.Sin(walkPhase) * legDirection;
            float rightStep = -leftStep;
            float gaitStrength = walkBlend;

            // Thighs alternate clearly. Knees recover on the backward half of
            // their stride so they do not stay locked straight like the old demo.
            PoseAxis(0, Vector3.right, legSwingDegrees * leftStep * gaitStrength);
            PoseAxis(1, Vector3.right, legSwingDegrees * rightStep * gaitStrength);
            float kneeSign = invertKneeBend ? -1f : 1f;
            float leftKnee = Mathf.Pow(Mathf.Max(0f, -leftStep), 1.2f);
            float rightKnee = Mathf.Pow(Mathf.Max(0f, -rightStep), 1.2f);
            PoseAxis(2, Vector3.right, kneeSign * kneeBendDegrees * leftKnee * gaitStrength);
            PoseAxis(3, Vector3.right, kneeSign * kneeBendDegrees * rightKnee * gaitStrength);

            // Arms hang DOWN for idle, then swing opposite their own legs.
            // Quaternions avoid Euler wrapping at exactly +/-180 degrees.
            float armSign = invertArmSwing ? -1f : 1f;
            leftShoulder.localRotation = relaxedLeftShoulder *
                Quaternion.AngleAxis(-armSign * armSwingDegrees * leftStep * gaitStrength, Vector3.right);
            rightShoulder.localRotation = relaxedRightShoulder *
                Quaternion.AngleAxis(-armSign * armSwingDegrees * rightStep * gaitStrength, Vector3.right);
            PoseAxis(4, Vector3.right, elbowBendDegrees * leftKnee * gaitStrength);
            PoseAxis(5, Vector3.right, elbowBendDegrees * rightKnee * gaitStrength);

            float breathe = Mathf.Sin(Time.time * idleBreathingFrequency * 2f * Mathf.PI)
                * idleBreathingDegrees * (1f - gaitStrength);
            if (upperSpine != null)
                upperSpine.localRotation = bindRotations[8] *
                    Quaternion.Euler(breathe + torsoLeanDegrees * gaitStrength,
                        torsoTwistDegrees * leftStep * gaitStrength, 0f);
            if (head != null)
                head.localRotation = bindRotations[9] *
                    Quaternion.AngleAxis(-0.25f * breathe, Vector3.right);

            // The existing TrainerSkinController already adds a tiny bounce to the
            // proxy. This is intentionally a *very small* supplementary step lift.
            // Crucially, it never alters the TrainerMovement transform/coordinates.
            float stepLift = Mathf.Abs(Mathf.Sin(walkPhase)) * extraBodyBounce * gaitStrength;
            transform.localPosition = originalModelLocalPosition + Vector3.up * stepLift;
        }

        private void PoseAxis(int index, Vector3 axis, float degrees)
        {
            if (animatedBones != null && index < animatedBones.Length && animatedBones[index] != null)
                animatedBones[index].localRotation = bindRotations[index] *
                    Quaternion.AngleAxis(degrees, axis);
        }

        private void OnDisable() { RestorePose(); }
        private void OnDestroy() { RestorePose(); }

        private void RestorePose()
        {
            if (!initialized || !poseReady || animatedBones == null) return;
            for (int i = 0; i < animatedBones.Length; ++i)
                if (animatedBones[i] != null)
                    animatedBones[i].localRotation = bindRotations[i];
            transform.localPosition = originalModelLocalPosition;
        }

        private void OnGUI()
        {
            if (!showDebugOverlay || !Application.isPlaying) return;
            float x = Mathf.Max(10f, Screen.width - 360f);
            GUI.Box(new Rect(x - 8f, 8f, 348f, 142f), "ORAS Vela U3.5 — Walk Polish");
            GUI.Label(new Rect(x, 35f, 330f, 20f),
                "Legs: " + (leftThigh != null && rightThigh != null ? "FOUND" : "MISSING") +
                "   Knees: " + (leftShin != null && rightShin != null ? "FOUND" : "MISSING"));
            GUI.Label(new Rect(x, 56f, 330f, 20f),
                "Shoulders: " + (leftShoulder != null && rightShoulder != null ? "FOUND" : "MISSING") +
                "   Root: " + (trainerRoot != null ? "FOUND" : "MISSING"));
            GUI.Label(new Rect(x, 78f, 330f, 20f),
                "Walk blend: " + walkBlend.ToString("F2") +
                "   Speed: " + measuredSpeed.ToString("F2"));
            GUI.Label(new Rect(x, 100f, 330f, 20f),
                "Test: " + (forceWalkTest ? "ON" : "OFF") +
                "   Left/Right thigh: " + (leftStepHint()).ToString("F2"));
        }

        private float leftStepHint() { return Mathf.Sin(walkPhase); }
    }
}
