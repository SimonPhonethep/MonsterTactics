using System;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using MonsterTactics;

namespace MonsterTactics.Editor
{
    /// <summary>
    /// U3.7.1: Contest Vela-specific rig calibration. Adds its OWN animation assets and
    /// never modifies the regular ORAS Vela, TrainerMovement, or TrainerSkinController.
    /// All rest rotations come from the *Contest* .dae, not the normal Vela FBX.
    /// </summary>
    public sealed class MonsterTacticsContestVelaRepairWindow : EditorWindow
    {
        const string ModelAsset = "Assets/MonsterTactics/Art/Trainer/VelaFestival/rstr0003_00_fi.dae";
        const string AnimationFolder = "Assets/MonsterTactics/Animations/VelaFestival/Repair_v371";
        const string IdlePath = AnimationFolder + "/ContestVela_CalibratedIdle.anim";
        const string WalkPath = AnimationFolder + "/ContestVela_CalibratedWalk.anim";
        const string ControllerPath = AnimationFolder + "/ContestVela_Calibrated.controller";
        const string LegacyControllerPath = "Assets/MonsterTactics/Animations/VelaFestival/ContestVela_Animator.controller";
        const string ProxyName = "Contest Vela 3D - Proxy";
        const string ModelName = "Vela Contest Model (Imported)";
        static readonly string[] JointNames = {
            "LShoulder", "RShoulder", "LThigh", "RThigh", "LLeg", "RLeg", "LFoot", "RFoot", "Spine2"
        };

        enum BendAxis { X, Z, Y }

        // Contest .dae source shoulder rotations are different from the regular ORAS FBX.
        // These small *relative* adjustments replace the incorrect +180/-180 absolute pose.
        float leftShoulderDown = 30f;
        float rightShoulderDown = -30f;
        float hipSwing = 7f;
        float kneeFlex = 5f;
        float armSwing = 2.5f;
        float ankleCompensation = 0.7f;
        float cycleLength = 0.90f;
        BendAxis legBendAxis = BendAxis.X;

        [MenuItem("MonsterTactics/Trainers/Contest Vela/Repair Contest Vela Rig & Walk (U3.7.1)", priority = 192)]
        public static void OpenWindow()
        {
            var window = GetWindow<MonsterTacticsContestVelaRepairWindow>("Contest Vela Repair");
            window.minSize = new Vector2(430f, 385f);
            window.Show();
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("Contest Vela | Rig & Walk Repair", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "U3.7 reused regular Vela's shoulder angles and leg axis. Contest Vela's .dae " +
                "has different bone orientations. This tool starts from Contest Vela's OWN rest pose " +
                "and creates separate, reversible clips. Stop Play mode first.", MessageType.Info);

            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Neutral shoulder pose (relative to Contest model)", EditorStyles.boldLabel);
                leftShoulderDown = EditorGUILayout.Slider("Left shoulder Z offset", leftShoulderDown, -75f, 75f);
                rightShoulderDown = EditorGUILayout.Slider("Right shoulder Z offset", rightShoulderDown, -75f, 75f);
                EditorGUILayout.HelpBox("If the arms point upward, try offsets closer to 0. The pose " +
                    "preview is safe to undo (Ctrl+Z).", MessageType.None);

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Gentle gait", EditorStyles.boldLabel);
                legBendAxis = (BendAxis)EditorGUILayout.EnumPopup("Leg bend axis", legBendAxis);
                hipSwing = EditorGUILayout.Slider("Thigh swing (degrees)", hipSwing, 0f, 16f);
                kneeFlex = EditorGUILayout.Slider("Knee bend (degrees)", kneeFlex, 0f, 14f);
                armSwing = EditorGUILayout.Slider("Arm swing (degrees)", armSwing, 0f, 9f);
                ankleCompensation = EditorGUILayout.Slider("Foot counter-rotation", ankleCompensation, 0f, 1f);
                cycleLength = EditorGUILayout.Slider("Step cycle (seconds)", cycleLength, 0.6f, 1.4f);

                EditorGUILayout.Space(6);
                if (GUILayout.Button("1. Preview neutral Contest pose (no animation)", GUILayout.Height(28)))
                    PreviewRestPose(false);
                if (GUILayout.Button("Preview adjusted shoulder pose", GUILayout.Height(25)))
                    PreviewRestPose(true);
                if (GUILayout.Button("2. Generate & apply corrected Idle + Walk", GUILayout.Height(31)))
                    ApplyNewAnimations(false);
                if (GUILayout.Button("Test corrected walk in place (then Play, key 4)", GUILayout.Height(25)))
                    ApplyNewAnimations(true);
                if (GUILayout.Button("Return to normal movement", GUILayout.Height(25)))
                    ApplyNewAnimations(false);
                EditorGUILayout.Space(5);
                if (GUILayout.Button("Optional: match Contest Vela's height and floor to regular Vela"))
                    MatchVisualHeight();
                if (GUILayout.Button("Rollback: restore original U3.7 Animator"))
                    RestoreLegacyController();
            }
            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox("Only Contest Vela (key 4) is touched. Other four trainer skins, " +
                "Monster, board, camera and right-click navigation are unaffected. The previous " +
                "U3.7 clips are kept for rollback.", MessageType.None);
        }

        static bool TryModel(out Transform trainer, out Transform model, out Transform source)
        {
            trainer = null; model = null; source = null;
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.name == "MonsterTactics Trainer") { trainer = root.transform; break; }
            }
            Transform proxy = trainer != null ? trainer.Find(ProxyName) : null;
            model = proxy != null ? proxy.Find(ModelName) : null;
            GameObject imported = AssetDatabase.LoadAssetAtPath<GameObject>(ModelAsset);
            source = imported != null ? imported.transform : null;
            if (trainer != null && model != null && source != null) return true;
            EditorUtility.DisplayDialog("Contest Vela not found", "Open the arena scene and install U3.7 " +
                "first. Expected MonsterTactics Trainer > Contest Vela 3D - Proxy > " + ModelName +
                ". The original .dae must exist at " + ModelAsset, "OK");
            return false;
        }

        static Transform FindBone(Transform root, string name)
        {
            if (root == null) return null;
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == name) return child;
            return null;
        }

        static Vector3 Axis(BendAxis which)
        {
            switch (which)
            {
                case BendAxis.Z: return Vector3.forward;
                case BendAxis.Y: return Vector3.up;
                default: return Vector3.right;
            }
        }

        Quaternion NeutralRotation(Transform importedBone, string name)
        {
            Quaternion rest = importedBone.localRotation;
            if (name == "LShoulder") rest *= Quaternion.AngleAxis(leftShoulderDown, Vector3.forward);
            if (name == "RShoulder") rest *= Quaternion.AngleAxis(rightShoulderDown, Vector3.forward);
            return rest;
        }

        void PreviewRestPose(bool useOffsets)
        {
            if (!TryModel(out Transform trainer, out Transform model, out Transform imported)) return;
            Animator animator = model.GetComponent<Animator>();
            if (animator != null)
            {
                Undo.RecordObject(animator, "Preview Contest pose");
                animator.enabled = false;
                EditorUtility.SetDirty(animator);
            }
            var driver = model.GetComponent<VelaModernClipDriver>();
            if (driver != null)
            {
                Undo.RecordObject(driver, "Preview Contest pose");
                driver.enabled = false;
                EditorUtility.SetDirty(driver);
            }
            foreach (string boneName in JointNames)
            {
                Transform sourceBone = FindBone(imported, boneName);
                Transform sceneBone = FindBone(model, boneName);
                if (sourceBone == null || sceneBone == null) continue;
                Undo.RecordObject(sceneBone, "Preview Contest " + boneName);
                sceneBone.localRotation = useOffsets ? NeutralRotation(sourceBone, boneName) : sourceBone.localRotation;
                EditorUtility.SetDirty(sceneBone);
            }
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Selection.activeGameObject = model.gameObject;
            Debug.Log(useOffsets ? "Contest Vela: adjusted neutral pose preview. " +
                "Use Apply to animate it, or Ctrl+Z to undo." :
                "Contest Vela: original DAE rest pose preview. Use Apply to resume animation.");
        }

        void ApplyNewAnimations(bool forceWalk)
        {
            if (!TryModel(out Transform trainer, out Transform model, out Transform source)) return;
            foreach (string boneName in JointNames)
            {
                if (FindBone(model, boneName) != null && FindBone(source, boneName) != null) continue;
                EditorUtility.DisplayDialog("Contest Vela bone missing", "Could not find the bone '" +
                    boneName + "' in both Contest Vela models. Nothing was changed.", "OK");
                return;
            }
            EnsureFolder(AnimationFolder);
            AnimationClip idle = LoadOrCreateClip(IdlePath, "Contest Vela | Calibrated Idle");
            AnimationClip walk = LoadOrCreateClip(WalkPath, "Contest Vela | Calibrated Walk");
            idle.ClearCurves();
            walk.ClearCurves();
            GenerateIdle(idle, model, source);
            GenerateWalk(walk, model, source);
            SetLoop(idle); SetLoop(walk);
            idle.EnsureQuaternionContinuity(); walk.EnsureQuaternionContinuity();
            EditorUtility.SetDirty(idle); EditorUtility.SetDirty(walk);
            AnimatorController controller = EnsureController(idle, walk);

            Animator animator = model.GetComponent<Animator>();
            if (animator == null) animator = Undo.AddComponent<Animator>(model.gameObject);
            Undo.RecordObject(animator, "Repair Contest Vela animator");
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.enabled = true;

            // Reuse the U3.6 speed/idle driver; it never moves or rotates the model.
            VelaModernClipDriver driver = model.GetComponent<VelaModernClipDriver>();
            if (driver == null) driver = Undo.AddComponent<VelaModernClipDriver>(model.gameObject);
            Undo.RecordObject(driver, "Connect calibrated Contest Vela walking");
            driver.Configure(animator, trainer, forceWalk);
            driver.enabled = true;

            // Do not let the previous procedural walker fight the Animator.
            foreach (MonoBehaviour script in model.GetComponents<MonoBehaviour>())
            {
                if (script == null || script.GetType().Name != "VelaModernProceduralWalk" || !script.enabled) continue;
                Undo.RecordObject(script, "Disable old Contest procedural walking");
                script.enabled = false;
                EditorUtility.SetDirty(script);
            }
            EditorUtility.SetDirty(driver); EditorUtility.SetDirty(animator);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("MonsterTactics U3.7.1: calibrated Contest Vela animation installed. " +
                (forceWalk ? "Walk-in-place test enabled." : "Movement-driven walking enabled.") +
                " Keys 1-5 unchanged.", model.gameObject);
        }

        static AnimationClip LoadOrCreateClip(string path, string title)
        {
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip != null) return clip;
            clip = new AnimationClip { name = title, frameRate = 30f };
            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }

        void GenerateIdle(AnimationClip clip, Transform model, Transform source)
        {
            float[] times = { 0f, .5f, 1f };
            foreach (string name in JointNames)
            {
                Transform bone = FindBone(model, name);
                Quaternion q = NeutralRotation(FindBone(source, name), name);
                Quaternion[] poses = { q, q, q };
                if (name == "Spine2") poses[1] = q * Quaternion.AngleAxis(.35f, Vector3.right);
                SetRotations(clip, model, bone, times, poses);
            }
        }

        void GenerateWalk(AnimationClip clip, Transform model, Transform source)
        {
            const int samples = 9;
            float[] time = new float[samples];
            Quaternion[][] output = new Quaternion[JointNames.Length][];
            for (int b = 0; b < output.Length; b++) output[b] = new Quaternion[samples];
            for (int i = 0; i < samples; i++)
            {
                float phase = (float)i / (samples - 1) * Mathf.PI * 2f;
                float wave = Mathf.Sin(phase);
                float leftHip = hipSwing * wave;
                float rightHip = -leftHip;
                float leftKnee = kneeFlex * Mathf.Max(0f, -wave);
                float rightKnee = kneeFlex * Mathf.Max(0f, wave);
                float lFoot = -(leftHip + leftKnee) * ankleCompensation;
                float rFoot = -(rightHip + rightKnee) * ankleCompensation;
                float leftArm = -armSwing * wave;
                float rightArm = armSwing * wave;
                float[] angles = { leftArm, rightArm, leftHip, rightHip, leftKnee,
                    rightKnee, lFoot, rFoot, .4f * wave };
                for (int j = 0; j < JointNames.Length; j++)
                {
                    string name = JointNames[j];
                    Quaternion rest = NeutralRotation(FindBone(source, name), name);
                    Vector3 axis = (j < 2) ? Vector3.right : (j == 8 ? Vector3.forward : Axis(legBendAxis));
                    output[j][i] = rest * Quaternion.AngleAxis(angles[j], axis);
                }
                time[i] = cycleLength * i / (samples - 1f);
            }
            for (int j = 0; j < JointNames.Length; j++)
                SetRotations(clip, model, FindBone(model, JointNames[j]), time, output[j]);
        }

        static void SetRotations(AnimationClip clip, Transform root, Transform bone, float[] times, Quaternion[] poses)
        {
            string path = AnimationUtility.CalculateTransformPath(bone, root);
            AnimationCurve[] curves = { new AnimationCurve(), new AnimationCurve(),
                new AnimationCurve(), new AnimationCurve() };
            Quaternion previous = poses[0];
            for (int i = 0; i < times.Length; i++)
            {
                Quaternion q = poses[i];
                if (Quaternion.Dot(previous, q) < 0f)
                    q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
                previous = q;
                curves[0].AddKey(times[i], q.x);
                curves[1].AddKey(times[i], q.y);
                curves[2].AddKey(times[i], q.z);
                curves[3].AddKey(times[i], q.w);
            }
            foreach (AnimationCurve curve in curves)
                for (int i = 0; i < curve.length; i++) curve.SmoothTangents(i, 0f);
            string[] names = { "m_LocalRotation.x", "m_LocalRotation.y",
                "m_LocalRotation.z", "m_LocalRotation.w" };
            for (int c = 0; c < 4; c++)
                AnimationUtility.SetEditorCurve(clip,
                    EditorCurveBinding.FloatCurve(path, typeof(Transform), names[c]), curves[c]);
        }

        static void SetLoop(AnimationClip clip)
        {
            clip.wrapMode = WrapMode.Loop;
            SerializedObject serialized = new SerializedObject(clip);
            SerializedProperty prop = serialized.FindProperty("m_AnimationClipSettings.m_LoopTime");
            if (prop != null) prop.boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static AnimatorController EnsureController(AnimationClip idle, AnimationClip walk)
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
                controller.AddParameter("Move", AnimatorControllerParameterType.Float);
                AnimatorStateMachine machine = controller.layers[0].stateMachine;
                AnimatorState idleState = machine.AddState("Idle");
                AnimatorState walkState = machine.AddState("Walk");
                machine.defaultState = idleState;
                AnimatorStateTransition toWalk = idleState.AddTransition(walkState);
                toWalk.hasExitTime = false;
                toWalk.hasFixedDuration = true;
                toWalk.duration = .12f;
                toWalk.AddCondition(AnimatorConditionMode.Greater, .12f, "Move");
                AnimatorStateTransition toIdle = walkState.AddTransition(idleState);
                toIdle.hasExitTime = false;
                toIdle.hasFixedDuration = true;
                toIdle.duration = .15f;
                toIdle.AddCondition(AnimatorConditionMode.Less, .08f, "Move");
            }
            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            foreach (ChildAnimatorState entry in stateMachine.states)
            {
                if (entry.state.name == "Idle") entry.state.motion = idle;
                if (entry.state.name == "Walk") entry.state.motion = walk;
            }
            EditorUtility.SetDirty(controller);
            return controller;
        }

        void RestoreLegacyController()
        {
            if (!TryModel(out Transform trainer, out Transform model, out Transform imported)) return;
            RuntimeAnimatorController controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(LegacyControllerPath);
            if (controller == null)
            {
                EditorUtility.DisplayDialog("Original animation not found",
                    "Couldn't find U3.7's old ContestVela_Animator.controller. The corrected " +
                    "animation is unchanged.", "OK");
                return;
            }
            Animator animator = model.GetComponent<Animator>();
            if (animator == null) return;
            Undo.RecordObject(animator, "Restore Contest Vela U3.7 clips");
            animator.runtimeAnimatorController = controller;
            animator.enabled = true;
            var driver = model.GetComponent<VelaModernClipDriver>();
            if (driver != null)
            {
                Undo.RecordObject(driver, "Restore Contest Vela U3.7 clips");
                driver.Configure(animator, trainer, false);
                driver.enabled = true;
                EditorUtility.SetDirty(driver);
            }
            EditorUtility.SetDirty(animator);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("Contest Vela original U3.7 Animator restored. Regular Vela is unchanged.");
        }

        static void MatchVisualHeight()
        {
            if (!TryModel(out Transform trainer, out Transform model, out Transform imported)) return;
            Transform contestProxy = trainer.Find(ProxyName);
            Transform regularProxy = trainer.Find("Modern Vela 3D - Proxy");
            Transform regularModel = regularProxy != null ? regularProxy.Find("Vela ORAS Model (Imported)") : null;
            bool oldContest = contestProxy.gameObject.activeSelf;
            bool oldRegular = regularProxy != null && regularProxy.gameObject.activeSelf;
            try
            {
                contestProxy.gameObject.SetActive(true);
                if (regularProxy != null) regularProxy.gameObject.SetActive(true);
                float target = 1.55f;
                if (regularModel != null && TryBounds(regularModel, out Bounds regular))
                    target = regular.size.y;
                if (!TryBounds(model, out Bounds original))
                    throw new InvalidOperationException("Contest Vela renderers have no measurable bounds.");
                Undo.RecordObject(model, "Match Contest Vela scale & floor");
                float scaleFactor = Mathf.Clamp(target / Mathf.Max(.0001f, original.size.y), 0.02f, 50f);
                model.localScale *= scaleFactor;
                if (TryBounds(model, out Bounds corrected))
                {
                    Vector3 shift = contestProxy.position - corrected.center;
                    shift.y = contestProxy.position.y - corrected.min.y;
                    model.position += shift;
                }
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                Debug.Log("Contest Vela matched regular Vela height and placed feet near floor. " +
                    "You can Ctrl+Z to undo this appearance-only adjustment.");
            }
            catch (Exception e)
            {
                Debug.LogError("Contest Vela height adjustment failed: " + e.Message);
            }
            finally
            {
                contestProxy.gameObject.SetActive(oldContest);
                if (regularProxy != null) regularProxy.gameObject.SetActive(oldRegular);
            }
        }

        static bool TryBounds(Transform root, out Bounds bounds)
        {
            bounds = new Bounds();
            bool valid = false;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                if (!valid) { bounds = renderer.bounds; valid = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
            return valid && bounds.size.y > .00001f;
        }

        static void EnsureFolder(string folder)
        {
            string[] parts = folder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
