using UnityEngine;

namespace MonsterTactics
{
    /// <summary>
    /// Lightweight runtime walk animation for the user-supplied Nova COLLADA rig.
    /// Uses its real skinned shoulder, elbow, hip, knee, and foot bones; it does not
    /// require animation clips, and it never changes the trainer's navigation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MonsterTacticsNovaProceduralWalk : MonoBehaviour
    {
        [Header("Walk cycle")]
        [SerializeField, Range(4f, 16f)] private float cyclesPerSecondRadians = 8.8f;
        [SerializeField, Range(5f, 45f)] private float legSwingDegrees = 24f;
        [SerializeField, Range(5f, 45f)] private float armSwingDegrees = 23f;
        [SerializeField, Range(0f, 45f)] private float kneeBendDegrees = 24f;
        [SerializeField, Range(0f, 22f)] private float elbowBendDegrees = 14f;
        [SerializeField, Range(0f, 5f)] private float torsoSwayDegrees = 2f;
        [SerializeField, Range(1f, 18f)] private float blendSpeed = 9f;

        private sealed class Bone
        {
            public Transform transform;
            public Quaternion rest;
            public Vector3 localHinge;
        }

        private Bone leftArm, rightArm, leftElbow, rightElbow;
        private Bone leftLeg, rightLeg, leftKnee, rightKnee;
        private Bone leftFoot, rightFoot, torso;
        private Transform visualRoot;
        private bool initialized;
        private bool walking;
        private float walkBlend;
        private float phase;

        /// <summary>Bind to the exact skinned rig after the model is grounded.</summary>
        public void Initialize(Transform importedModel, Transform turningRoot)
        {
            if (importedModel == null || turningRoot == null) return;
            visualRoot = turningRoot;
            Transform upperLeft = Find(importedModel, "left_arm_01");
            Transform upperRight = Find(importedModel, "right_arm_01");
            Transform elbowLeft = Find(importedModel, "left_arm_02");
            Transform elbowRight = Find(importedModel, "right_arm_02");
            Transform hip = Find(importedModel, "hips");

            // The imported rig is posed with arms extended. Relax the arms down
            // before capturing the procedural animation's neutral rotations.
            RelaxArm(upperLeft, elbowLeft, hip);
            RelaxArm(upperRight, elbowRight, hip);

            leftArm = Capture(upperLeft);
            rightArm = Capture(upperRight);
            leftElbow = Capture(elbowLeft);
            rightElbow = Capture(elbowRight);
            leftLeg = Capture(Find(importedModel, "left_leg_01"));
            rightLeg = Capture(Find(importedModel, "right_leg_01"));
            leftKnee = Capture(Find(importedModel, "left_leg_02"));
            rightKnee = Capture(Find(importedModel, "right_leg_02"));
            leftFoot = Capture(Find(importedModel, "left_foot"));
            rightFoot = Capture(Find(importedModel, "right_foot"));
            torso = Capture(Find(importedModel, "spine_02"));

            initialized = leftArm != null && rightArm != null &&
                          leftLeg != null && rightLeg != null;
            if (!initialized)
                Debug.LogWarning("MonsterTactics Nova: some walk bones were not found. " +
                                 "Imported model will use the existing walking bob.", this);
        }

        public void SetMovement(bool isMoving)
        {
            walking = isMoving;
        }

        private static Transform Find(Transform parent, string shortName)
        {
            foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
            {
                string name = child.name.ToLowerInvariant();
                if (name == shortName || name.EndsWith("_" + shortName)) return child;
            }
            return null;
        }

        private static void RelaxArm(Transform shoulder, Transform elbow, Transform hips)
        {
            if (shoulder == null || elbow == null || hips == null) return;
            Vector3 armVector = elbow.position - shoulder.position;
            if (armVector.sqrMagnitude < 0.000001f) return;
            Vector3 outside = shoulder.position - hips.position;
            outside.y = 0f;
            if (outside.sqrMagnitude < 0.0001f) outside = Vector3.right;
            Vector3 relaxed = (Vector3.down * 0.9f + outside.normalized * 0.35f).normalized;
            shoulder.rotation = Quaternion.FromToRotation(armVector.normalized, relaxed) * shoulder.rotation;
        }

        private Bone Capture(Transform joint)
        {
            if (joint == null || joint.parent == null) return null;
            Vector3 axis = Quaternion.Inverse(joint.parent.rotation) * visualRoot.right;
            if (axis.sqrMagnitude < 0.000001f) axis = Vector3.right;
            return new Bone
            {
                transform = joint,
                rest = joint.localRotation,
                localHinge = axis.normalized
            };
        }

        private static void Pose(Bone bone, float degrees)
        {
            if (bone == null || bone.transform == null) return;
            bone.transform.localRotation = Quaternion.AngleAxis(degrees, bone.localHinge) * bone.rest;
        }

        private void LateUpdate()
        {
            if (!initialized) return;
            float dt = Time.deltaTime;
            walkBlend = Mathf.MoveTowards(walkBlend, walking ? 1f : 0f, dt * blendSpeed);
            if (walking) phase += dt * cyclesPerSecondRadians;

            float step = Mathf.Sin(phase) * walkBlend;
            float otherStep = -step;
            float leftLift = Mathf.Max(0f, Mathf.Sin(phase)) * walkBlend;
            float rightLift = Mathf.Max(0f, -Mathf.Sin(phase)) * walkBlend;

            // Feet step alternately, while opposite arms swing naturally.
            Pose(leftLeg, step * legSwingDegrees);
            Pose(rightLeg, otherStep * legSwingDegrees);
            Pose(leftArm, -step * armSwingDegrees);
            Pose(rightArm, step * armSwingDegrees);

            // Flex knees as the foot comes forward and soften the elbow pose.
            Pose(leftKnee, leftLift * kneeBendDegrees);
            Pose(rightKnee, rightLift * kneeBendDegrees);
            Pose(leftElbow, -elbowBendDegrees - leftLift * 5f);
            Pose(rightElbow, -elbowBendDegrees - rightLift * 5f);
            Pose(leftFoot, -leftLift * kneeBendDegrees * 0.35f);
            Pose(rightFoot, -rightLift * kneeBendDegrees * 0.35f);

            // Small body movement prevents the upper half from looking rigid.
            Pose(torso, Mathf.Sin(phase * 2f) * walkBlend * torsoSwayDegrees);
        }
    }
}
