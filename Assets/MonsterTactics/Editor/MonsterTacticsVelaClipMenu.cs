using System;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MonsterTactics.Editor
{
    /// <summary>
    /// Creates editable, skeleton-specific AnimationClips on Vela's imported rig,
    /// attaches a small Animator controller, and disables the old bone-rotation script.
    /// No prefab/arena re-generation and no TrainerMovement/TrainerSkinController changes.
    /// </summary>
    public static class MonsterTacticsVelaClipMenu
    {
        private const string TrainerName = "MonsterTactics Trainer";
        private const string ProxyName = "Modern Vela 3D - Proxy";
        private const string ModelName = "Vela ORAS Model (Imported)";
        private const string AnimationFolder = "Assets/MonsterTactics/Animations/VelaModern";
        private const string IdlePath = AnimationFolder + "/Vela_Idle_Starter.anim";
        private const string WalkPath = AnimationFolder + "/Vela_Walk_Starter.anim";
        private const string ControllerPath = AnimationFolder + "/Vela_Animator.controller";
        private const string VelaFbxPath = "Assets/MonsterTactics/Art/Trainer/VelaModern/VelaOW.dae";

        private static readonly float[] WalkTimes =
        {
            0f, 0.1f, 0.2f, 0.3f, 0.4f, 0.5f, 0.6f, 0.7f, 0.8f
        };

        // Deliberately keyed poses, not runtime sine-rotations. These are editable
        // demonstration clips — a carefully authored Blender cycle can replace them.
        private static readonly float[] LeftThigh = { 21, 12, 0, -13, -21, -12, 0, 13, 21 };
        private static readonly float[] RightThigh = { -21, -12, 0, 13, 21, 12, 0, -13, -21 };
        private static readonly float[] LeftKnee = { 2, 4, 14, 23, 3, 2, 5, 10, 2 };
        private static readonly float[] RightKnee = { 3, 2, 5, 10, 2, 4, 14, 23, 3 };
        private static readonly float[] LeftArm = { -14, -9, 0, 10, 14, 9, 0, -10, -14 };
        private static readonly float[] RightArm = { 14, 9, 0, -10, -14, -9, 0, 10, 14 };

        [MenuItem("MonsterTactics/Trainers/Install Vela Idle + Walk Clips (U3.6)", priority = 120)]
        private static void Install() { InstallOrUpdate(false, false); }

        [MenuItem("MonsterTactics/Trainers/Test Vela Clips - Walk In Place (U3.6)", priority = 121)]
        private static void TestWalk() { InstallOrUpdate(true, false); }

        [MenuItem("MonsterTactics/Trainers/Stop Vela Clip Walk Test (U3.6)", priority = 122)]
        private static void StopTest() { InstallOrUpdate(false, false); }

        [MenuItem("MonsterTactics/Trainers/Rebuild Starter Clips (U3.6)", priority = 123)]
        private static void RebuildClips()
        {
            if (!EditorUtility.DisplayDialog("Rebuild U3.6 Starter Clips",
                "This will overwrite your edits to Vela_Idle_Starter and Vela_Walk_Starter. " +
                "Any third-party clips should have different filenames. Continue?",
                "Rebuild", "Cancel")) return;
            InstallOrUpdate(false, true);
        }

        private static void InstallOrUpdate(bool forceWalkTest, bool rebuild)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("MonsterTactics U3.6", "Stop Play mode first.", "OK");
                return;
            }

            if (!TryFindVela(out Transform trainer, out Transform model))
            {
                EditorUtility.DisplayDialog("MonsterTactics U3.6",
                    "Can't find the imported Vela model in your open scene. Expected:\n" +
                    TrainerName + " → " + ProxyName + " → " + ModelName +
                    "\n\nOpen your saved arena scene. Do not regenerate the arena.", "OK");
                return;
            }

            Transform lThigh = FindBone(model, "LThigh");
            Transform rThigh = FindBone(model, "RThigh");
            Transform lKnee = FindBone(model, "LLeg");
            Transform rKnee = FindBone(model, "RLeg");
            Transform lShoulder = FindBone(model, "LShoulder");
            Transform rShoulder = FindBone(model, "RShoulder");
            Transform spine = FindBone(model, "Spine2");
            Transform lForearm = FindBone(model, "LForeArm");
            Transform rForearm = FindBone(model, "RForeArm");

            if (lThigh == null || rThigh == null || lKnee == null || rKnee == null ||
                lShoulder == null || rShoulder == null)
            {
                EditorUtility.DisplayDialog("MonsterTactics U3.6",
                    "Missing required Vela ORAS bones. The FBX must include LThigh, RThigh, " +
                    "LLeg, RLeg, LShoulder and RShoulder. See the README.", "OK");
                return;
            }

            EnsureFolder(AnimationFolder);
            AnimationClip idle = AssetDatabase.LoadAssetAtPath<AnimationClip>(IdlePath);
            if (idle == null)
            {
                idle = new AnimationClip { name = "Vela Idle (starter)", frameRate = 30f };
                AssetDatabase.CreateAsset(idle, IdlePath);
                BuildIdleClip(idle, model, lThigh, rThigh, lKnee, rKnee,
                    lShoulder, rShoulder, spine, lForearm, rForearm);
            }
            else if (rebuild)
            {
                idle.ClearCurves();
                BuildIdleClip(idle, model, lThigh, rThigh, lKnee, rKnee,
                    lShoulder, rShoulder, spine, lForearm, rForearm);
            }

            AnimationClip walk = AssetDatabase.LoadAssetAtPath<AnimationClip>(WalkPath);
            if (walk == null)
            {
                walk = new AnimationClip { name = "Vela Walk (starter)", frameRate = 30f };
                AssetDatabase.CreateAsset(walk, WalkPath);
                BuildWalkClip(walk, model, lThigh, rThigh, lKnee, rKnee,
                    lShoulder, rShoulder, spine, lForearm, rForearm);
            }
            else if (rebuild)
            {
                walk.ClearCurves();
                BuildWalkClip(walk, model, lThigh, rThigh, lKnee, rKnee,
                    lShoulder, rShoulder, spine, lForearm, rForearm);
            }

            AnimatorController controller = EnsureController(idle, walk);
            Animator animator = model.GetComponent<Animator>();
            if (animator == null) animator = Undo.AddComponent<Animator>(model.gameObject);
            Undo.RecordObject(animator, "Install Vela Clip Animator");
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            // Generic FBX avatars are optional for transform-curve clips in some
            // configurations, but assign the imported Avatar whenever one exists.
            UnityEngine.Object[] imported = AssetDatabase.LoadAllAssetsAtPath(VelaFbxPath);
            foreach (UnityEngine.Object obj in imported)
            {
                if (obj is Avatar avatar && avatar.isValid)
                {
                    animator.avatar = avatar;
                    break;
                }
            }

            // Avoid a compile-time dependency on the user's older U3.4/U3.5 patch.
            // Only the Vela instance is affected; other trainers are untouched.
            foreach (MonoBehaviour component in model.GetComponents<MonoBehaviour>())
            {
                if (component == null || component.GetType().Name != "VelaModernProceduralWalk" ||
                    !component.enabled) continue;
                Undo.RecordObject(component, "Disable old procedural Vela walk");
                component.enabled = false;
                EditorUtility.SetDirty(component);
            }

            VelaModernClipDriver driver = model.GetComponent<VelaModernClipDriver>();
            if (driver == null) driver = Undo.AddComponent<VelaModernClipDriver>(model.gameObject);
            Undo.RecordObject(driver, "Connect Vela clip driver");
            driver.Configure(animator, trainer, forceWalkTest);
            EditorUtility.SetDirty(animator);
            EditorUtility.SetDirty(driver);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = model.gameObject;

            Debug.Log("MonsterTactics U3.6: Idle/Walk clips installed for ORAS Vela. " +
                "Previous procedural component disabled; existing U3.4.4 turning and all skin keys are unchanged. " +
                (forceWalkTest ? "Walk-in-place TEST enabled." : "Walking follows right-click movement."),
                model.gameObject);
        }

        private static bool TryFindVela(out Transform trainer, out Transform model)
        {
            trainer = null;
            model = null;
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.name != TrainerName) continue;
                trainer = root.transform;
                Transform proxy = trainer.Find(ProxyName);
                model = proxy != null ? proxy.Find(ModelName) : null;
                return model != null;
            }
            return false;
        }

        private static Transform FindBone(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        private static void BuildIdleClip(AnimationClip clip, Transform root, Transform lThigh,
            Transform rThigh, Transform lKnee, Transform rKnee, Transform lShoulder,
            Transform rShoulder, Transform spine, Transform lForearm, Transform rForearm)
        {
            float[] times = { 0f, 0.55f, 1.1f };
            KeyRotations(clip, root, lThigh, times, Repeat(lThigh.localRotation, 3));
            KeyRotations(clip, root, rThigh, times, Repeat(rThigh.localRotation, 3));
            KeyRotations(clip, root, lKnee, times, Repeat(lKnee.localRotation, 3));
            KeyRotations(clip, root, rKnee, times, Repeat(rKnee.localRotation, 3));
            KeyRotations(clip, root, lShoulder, times, Repeat(RelaxedShoulder(lShoulder, 180f), 3));
            KeyRotations(clip, root, rShoulder, times, Repeat(RelaxedShoulder(rShoulder, -180f), 3));
            if (lForearm != null) KeyRotations(clip, root, lForearm, times, Repeat(lForearm.localRotation, 3));
            if (rForearm != null) KeyRotations(clip, root, rForearm, times, Repeat(rForearm.localRotation, 3));
            if (spine != null)
            {
                Quaternion rest = spine.localRotation;
                KeyRotations(clip, root, spine, times, new[]
                {
                    rest, rest * Quaternion.Euler(0.8f, 0f, 0f), rest
                });
            }
            clip.EnsureQuaternionContinuity();
            SetLooping(clip);
        }

        private static void BuildWalkClip(AnimationClip clip, Transform root, Transform lThigh,
            Transform rThigh, Transform lKnee, Transform rKnee, Transform lShoulder,
            Transform rShoulder, Transform spine, Transform lForearm, Transform rForearm)
        {
            float[] times = WalkTimes;
            KeyRotationOffsets(clip, root, lThigh, times, LeftThigh, Vector3.right);
            KeyRotationOffsets(clip, root, rThigh, times, RightThigh, Vector3.right);
            KeyRotationOffsets(clip, root, lKnee, times, LeftKnee, Vector3.right);
            KeyRotationOffsets(clip, root, rKnee, times, RightKnee, Vector3.right);

            Quaternion lRest = RelaxedShoulder(lShoulder, 180f);
            Quaternion rRest = RelaxedShoulder(rShoulder, -180f);
            Quaternion[] leftArm = new Quaternion[times.Length];
            Quaternion[] rightArm = new Quaternion[times.Length];
            for (int i = 0; i < times.Length; ++i)
            {
                leftArm[i] = lRest * Quaternion.AngleAxis(LeftArm[i], Vector3.right);
                rightArm[i] = rRest * Quaternion.AngleAxis(RightArm[i], Vector3.right);
            }
            KeyRotations(clip, root, lShoulder, times, leftArm);
            KeyRotations(clip, root, rShoulder, times, rightArm);

            if (lForearm != null)
            {
                float[] elbow = { 7, 7, 9, 12, 7, 7, 9, 12, 7 };
                KeyRotationOffsets(clip, root, lForearm, times, elbow, Vector3.right);
            }
            if (rForearm != null)
            {
                float[] elbow = { 7, 7, 9, 12, 7, 7, 9, 12, 7 };
                KeyRotationOffsets(clip, root, rForearm, times, elbow, Vector3.right);
            }
            if (spine != null)
            {
                Quaternion baseRotation = spine.localRotation;
                float[] yaw = { -1.5f, 0, 1.5f, 0, -1.5f, 0, 1.5f, 0, -1.5f };
                Quaternion[] rotations = new Quaternion[times.Length];
                for (int i = 0; i < times.Length; ++i)
                    rotations[i] = baseRotation * Quaternion.Euler(1.0f, yaw[i], 0f);
                KeyRotations(clip, root, spine, times, rotations);
            }
            clip.EnsureQuaternionContinuity();
            SetLooping(clip);
        }

        private static void KeyRotationOffsets(AnimationClip clip, Transform root,
            Transform bone, float[] times, float[] angles, Vector3 axis)
        {
            Quaternion original = bone.localRotation;
            Quaternion[] rotations = new Quaternion[times.Length];
            for (int i = 0; i < times.Length; ++i)
                rotations[i] = original * Quaternion.AngleAxis(angles[i], axis);
            KeyRotations(clip, root, bone, times, rotations);
        }

        private static void KeyRotations(AnimationClip clip, Transform root,
            Transform bone, float[] times, Quaternion[] poses)
        {
            if (bone == null || times.Length != poses.Length) return;
            string path = AnimationUtility.CalculateTransformPath(bone, root);
            AnimationCurve[] channels = { new AnimationCurve(), new AnimationCurve(),
                new AnimationCurve(), new AnimationCurve() };
            Quaternion previous = poses[0];
            for (int i = 0; i < times.Length; i++)
            {
                Quaternion q = poses[i];
                if (Quaternion.Dot(previous, q) < 0f)
                    q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
                previous = q;
                channels[0].AddKey(times[i], q.x);
                channels[1].AddKey(times[i], q.y);
                channels[2].AddKey(times[i], q.z);
                channels[3].AddKey(times[i], q.w);
            }
            // Soft animation tangents for the walk cycle instead of hard stepped motion.
            foreach (AnimationCurve curve in channels)
                for (int i = 0; i < curve.length; i++) curve.SmoothTangents(i, 0f);

            string[] names = { "m_LocalRotation.x", "m_LocalRotation.y", "m_LocalRotation.z", "m_LocalRotation.w" };
            for (int c = 0; c < 4; ++c)
                AnimationUtility.SetEditorCurve(clip,
                    EditorCurveBinding.FloatCurve(path, typeof(Transform), names[c]), channels[c]);
        }

        private static Quaternion RelaxedShoulder(Transform bone, float zDegrees)
        {
            Vector3 euler = bone.localEulerAngles;
            return Quaternion.Euler(euler.x, euler.y, zDegrees);
        }

        private static Quaternion[] Repeat(Quaternion q, int n)
        {
            var array = new Quaternion[n];
            for (int i = 0; i < n; ++i) array[i] = q;
            return array;
        }

        private static void SetLooping(AnimationClip clip)
        {
            clip.wrapMode = WrapMode.Loop;
            SerializedObject serialized = new SerializedObject(clip);
            SerializedProperty loop = serialized.FindProperty("m_AnimationClipSettings.m_LoopTime");
            if (loop != null) loop.boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(clip);
        }

        private static AnimatorController EnsureController(AnimationClip idle, AnimationClip walk)
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
                controller.AddParameter("Move", AnimatorControllerParameterType.Float);

                AnimatorStateMachine sm = controller.layers[0].stateMachine;
                AnimatorState idleState = sm.AddState("Idle");
                idleState.motion = idle;
                AnimatorState walkState = sm.AddState("Walk");
                walkState.motion = walk;
                sm.defaultState = idleState;

                AnimatorStateTransition intoWalk = idleState.AddTransition(walkState);
                intoWalk.hasExitTime = false;
                intoWalk.hasFixedDuration = true;
                intoWalk.duration = 0.14f;
                intoWalk.AddCondition(AnimatorConditionMode.Greater, 0.12f, "Move");

                AnimatorStateTransition intoIdle = walkState.AddTransition(idleState);
                intoIdle.hasExitTime = false;
                intoIdle.hasFixedDuration = true;
                intoIdle.duration = 0.16f;
                intoIdle.AddCondition(AnimatorConditionMode.Less, 0.08f, "Move");
            }
            return controller;
        }

        private static void EnsureFolder(string folder)
        {
            string[] parts = folder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; ++i)
            {
                string child = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(child)) AssetDatabase.CreateFolder(current, parts[i]);
                current = child;
            }
        }
    }
}
