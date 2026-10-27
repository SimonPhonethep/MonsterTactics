using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using MonsterTactics;

namespace MonsterTactics.Editor
{
    /// <summary>
    /// U3.7.2: Calibrates Contest Vela's arm direction from the actual shoulder-to-elbow
    /// bone positions rather than assuming standard ORAS Vela shoulder Euler angles.
    /// Preserves the existing U3.7.1 leg, foot, spine and movement curves.
    /// </summary>
    public sealed class MonsterTacticsContestVelaArmsFixWindow : EditorWindow
    {
        private const string TrainerName = "MonsterTactics Trainer";
        private const string ProxyName = "Contest Vela 3D - Proxy";
        private const string ModelName = "Vela Contest Model (Imported)";
        private const string ModelAsset = "Assets/MonsterTactics/Art/Trainer/VelaFestival/rstr0003_00_fi.dae";
        private const string SourceFolder = "Assets/MonsterTactics/Animations/VelaFestival/Repair_v371";
        private const string OriginalIdle = SourceFolder + "/ContestVela_CalibratedIdle.anim";
        private const string OriginalWalk = SourceFolder + "/ContestVela_CalibratedWalk.anim";
        private const string OriginalController = SourceFolder + "/ContestVela_Calibrated.controller";
        private const string FixFolder = "Assets/MonsterTactics/Animations/VelaFestival/Arms_v372";
        private const string FixedIdle = FixFolder + "/ContestVela_ArmsDown_Idle.anim";
        private const string FixedWalk = FixFolder + "/ContestVela_ArmsDown_Walk.anim";
        private const string FixedController = FixFolder + "/ContestVela_ArmsDown.controller";

        [SerializeField, Range(0f, 35f)] private float armOutwardDegrees = 12f;
        [SerializeField, Range(0f, 8f)] private float armSwingDegrees = 2.5f;
        [SerializeField, Range(0.65f, 1.25f)] private float stepCycleSeconds = 0.90f;

        private struct ArmPose
        {
            public Quaternion left;
            public Quaternion right;
            public Vector3 leftAxis;
            public Vector3 rightAxis;
            public float leftError;
            public float rightError;
        }

        [MenuItem("MonsterTactics/Trainers/Contest Vela/Fix Arms From Bone Directions (U3.7.2)", priority = 193)]
        public static void ShowWindow()
        {
            var win = GetWindow<MonsterTacticsContestVelaArmsFixWindow>("Contest Vela Arms");
            win.minSize = new Vector2(475, 330);
            win.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Contest Vela - Automatic Arm Alignment", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "The previous repair used fixed shoulder offsets that may leave the arms sideways. " +
                "This tool measures the actual shoulder-to-elbow direction on Contest Vela's OWN " +
                "unanimated model, then aligns both arms downward. It copies the U3.7.1 walking " +
                "clips and changes ONLY the shoulder curves.", MessageType.Info);
            armOutwardDegrees = EditorGUILayout.Slider("Arms away from body", armOutwardDegrees, 0, 35);
            armSwingDegrees = EditorGUILayout.Slider("Walk arm swing", armSwingDegrees, 0, 8);
            stepCycleSeconds = EditorGUILayout.Slider("Walking cycle duration", stepCycleSeconds, .65f, 1.25f);
            EditorGUILayout.HelpBox(
                "Recommended first test: 12 degrees outward, 2.5 degrees arm swing. " +
                "If the arms intersect the dress, increase 'Arms away from body' slightly.", MessageType.None);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button("1. Preview natural arm pose", GUILayout.Height(31))) Preview();
                if (GUILayout.Button("2. Create & apply arm-fixed Idle + Walk", GUILayout.Height(34))) Apply(false);
                if (GUILayout.Button("Test arm-fixed walk in place", GUILayout.Height(27))) Apply(true);
                if (GUILayout.Button("Return to movement-driven walking", GUILayout.Height(27))) Apply(false);
                EditorGUILayout.Space(5);
                if (GUILayout.Button("Restore U3.7.1 animation (rollback)", GUILayout.Height(26))) Restore();
            }
            EditorGUILayout.HelpBox(
                "Keys 1-5, Contest Vela's imported model, regular Vela, Ranger, Monster, trainer " +
                "movement and the camera are left unchanged. Requires U3.7 and U3.7.1 installed.",
                MessageType.None);
        }

        private static Transform FindBone(Transform root, string name)
        {
            if (root == null) return null;
            Transform[] list = root.GetComponentsInChildren<Transform>(true);
            foreach (Transform t in list)
                if (t.name == name) return t;
            return null;
        }

        private static bool TryGetScene(out Transform trainer, out Transform model)
        {
            trainer = null;
            model = null;
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
                if (root.name == TrainerName) { trainer = root.transform; break; }
            Transform proxy = trainer != null ? trainer.Find(ProxyName) : null;
            model = proxy != null ? proxy.Find(ModelName) : null;
            if (trainer != null && model != null) return true;
            EditorUtility.DisplayDialog("Contest Vela not found",
                "Open your MonsterTactics arena and install U3.7 Contest Vela first. " +
                "Expected MonsterTactics Trainer / Contest Vela 3D - Proxy / " + ModelName, "OK");
            return false;
        }

        /// <summary>
        /// Evaluate the rest pose on a temporary instance, so any active Animator or
        /// user changes in the scene cannot contaminate calibration.
        /// </summary>
        private bool TryCalibrate(Transform sceneModel, out ArmPose pose)
        {
            pose = default(ArmPose);
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelAsset);
            if (asset == null)
            {
                EditorUtility.DisplayDialog("Contest Vela model missing", "Missing " + ModelAsset, "OK");
                return false;
            }
            GameObject temp = null;
            try
            {
                temp = PrefabUtility.InstantiatePrefab(asset) as GameObject;
                if (temp == null) throw new InvalidOperationException("Cannot instantiate Contest Vela model.");
                temp.name = "Contest Vela - temporary arm calibration";
                temp.transform.SetParent(sceneModel.parent, false);
                temp.transform.localPosition = sceneModel.localPosition;
                temp.transform.localRotation = sceneModel.localRotation;
                temp.transform.localScale = sceneModel.localScale;

                Transform left = FindBone(temp.transform, "LShoulder");
                Transform right = FindBone(temp.transform, "RShoulder");
                Transform leftElbow = FindBone(temp.transform, "LForeArm");
                Transform rightElbow = FindBone(temp.transform, "RForeArm");
                if (leftElbow == null) leftElbow = FindBone(temp.transform, "LArmA");
                if (rightElbow == null) rightElbow = FindBone(temp.transform, "RArmA");
                if (left == null || right == null || leftElbow == null || rightElbow == null)
                    throw new InvalidOperationException("Shoulder or arm bones missing. Need LShoulder, RShoulder, and LForeArm/RForeArm.");

                Vector3 center = (left.position + right.position) * .5f;
                Vector3 sideVector = right.position - left.position;
                Vector3 lateral = Vector3.ProjectOnPlane(sideVector, Vector3.up);
                if (lateral.sqrMagnitude < 0.000001f)
                    lateral = Vector3.ProjectOnPlane(temp.transform.right, Vector3.up);
                if (lateral.sqrMagnitude < 0.000001f) lateral = Vector3.right;
                lateral.Normalize();

                pose.left = AlignArm(left, leftElbow, center, lateral, armOutwardDegrees, out pose.leftAxis, out pose.leftError);
                pose.right = AlignArm(right, rightElbow, center, lateral, armOutwardDegrees, out pose.rightAxis, out pose.rightError);
                if (pose.leftError > 6f || pose.rightError > 6f)
                    throw new InvalidOperationException("Automatic arm alignment residual is too high. " +
                        "Left " + pose.leftError.ToString("F1") + "°, right " + pose.rightError.ToString("F1") + "°.");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError("MonsterTactics Contest Vela arm calibration: " + ex.Message);
                EditorUtility.DisplayDialog("Arm alignment failed", ex.Message +
                    "\nNo animation clips or trainer scripts were changed.", "OK");
                return false;
            }
            finally
            {
                if (temp != null) DestroyImmediate(temp);
            }
        }

        private static Quaternion AlignArm(Transform shoulder, Transform elbow, Vector3 center,
            Vector3 lateral, float outwardDegrees, out Vector3 axisLocal, out float error)
        {
            Vector3 rawDirection = elbow.position - shoulder.position;
            if (rawDirection.sqrMagnitude < .000001f)
                throw new InvalidOperationException("Arm bone length is zero at " + shoulder.name);
            rawDirection.Normalize();

            // Use actual shoulder placement to determine its outward direction.
            Vector3 outward = Vector3.ProjectOnPlane(shoulder.position - center, Vector3.up);
            if (outward.sqrMagnitude < .000001f)
                outward = shoulder.name.StartsWith("L") ? -lateral : lateral;
            outward.Normalize();
            float r = outwardDegrees * Mathf.Deg2Rad;
            Vector3 desired = (Vector3.down * Mathf.Cos(r) + outward * Mathf.Sin(r)).normalized;

            Quaternion worldDelta = Quaternion.FromToRotation(rawDirection, desired);
            Quaternion desiredWorld = worldDelta * shoulder.rotation;
            Quaternion localTarget = Quaternion.Inverse(shoulder.parent.rotation) * desiredWorld;

            // After solving, derive the local twist axis matching the character's
            // shoulder-to-shoulder direction. This swings the arms front/back.
            axisLocal = (Quaternion.Inverse(desiredWorld) * lateral).normalized;
            error = Vector3.Angle(worldDelta * rawDirection, desired);
            return localTarget;
        }

        private void Preview()
        {
            if (!TryGetScene(out Transform trainer, out Transform model)) return;
            if (!TryCalibrate(model, out ArmPose pose)) return;
            Animator animator = model.GetComponent<Animator>();
            VelaModernClipDriver driver = model.GetComponent<VelaModernClipDriver>();
            if (animator != null) { Undo.RecordObject(animator, "Preview Contest Vela arm pose"); animator.enabled = false; }
            if (driver != null) { Undo.RecordObject(driver, "Preview Contest Vela arm pose"); driver.enabled = false; }
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelAsset);
            Transform source = asset.transform;
            foreach (string name in new[] { "Spine2", "LShoulder", "RShoulder", "LArmA", "RArmA", "LForeArm", "RForeArm" })
            {
                Transform sceneBone = FindBone(model, name);
                Transform sourceBone = FindBone(source, name);
                if (sceneBone == null || sourceBone == null) continue;
                Undo.RecordObject(sceneBone, "Preview Contest Vela arm pose");
                sceneBone.localRotation = name == "LShoulder" ? pose.left :
                    name == "RShoulder" ? pose.right : sourceBone.localRotation;
            }
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Selection.activeGameObject = model.gameObject;
            Debug.Log("Contest Vela U3.7.2: natural arm pose preview. To resume animation, click 'Create & apply'. " +
                "Ctrl+Z undoes this preview.", model.gameObject);
        }

        private void Apply(bool forceWalk)
        {
            if (!TryGetScene(out Transform trainer, out Transform model)) return;
            if (!TryCalibrate(model, out ArmPose pose)) return;
            AnimationClip sourceIdle = AssetDatabase.LoadAssetAtPath<AnimationClip>(OriginalIdle);
            AnimationClip sourceWalk = AssetDatabase.LoadAssetAtPath<AnimationClip>(OriginalWalk);
            if (sourceIdle == null || sourceWalk == null)
            {
                EditorUtility.DisplayDialog("Install U3.7.1 first",
                    "The U3.7.1 calibrated Idle/Walk clips were not found. Run " +
                    "MonsterTactics > Trainers > Contest Vela > Repair Contest Vela Rig & Walk (U3.7.1), " +
                    "then click Generate & apply corrected Idle + Walk before using this fix.", "OK");
                return;
            }

            EnsureFolder(FixFolder);
            AnimationClip idle = MakeCopy(sourceIdle, FixedIdle, "Contest Vela - Arm aligned Idle");
            AnimationClip walk = MakeCopy(sourceWalk, FixedWalk, "Contest Vela - Arm aligned Walk");
            ReplaceShoulderCurve(idle, model, "LShoulder", pose.left, pose.leftAxis, 0f, stepCycleSeconds, 0);
            ReplaceShoulderCurve(idle, model, "RShoulder", pose.right, pose.rightAxis, 0f, stepCycleSeconds, 0);
            ReplaceShoulderCurve(walk, model, "LShoulder", pose.left, pose.leftAxis, armSwingDegrees, stepCycleSeconds, -1);
            ReplaceShoulderCurve(walk, model, "RShoulder", pose.right, pose.rightAxis, armSwingDegrees, stepCycleSeconds, 1);
            idle.EnsureQuaternionContinuity();
            walk.EnsureQuaternionContinuity();
            SetLoop(idle); SetLoop(walk);
            EditorUtility.SetDirty(idle); EditorUtility.SetDirty(walk);

            AnimatorController controller = MakeController(idle, walk);
            Animator animator = model.GetComponent<Animator>();
            if (animator == null) animator = Undo.AddComponent<Animator>(model.gameObject);
            Undo.RecordObject(animator, "Apply Contest Vela arm-aligned animations");
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.enabled = true;
            VelaModernClipDriver driver = model.GetComponent<VelaModernClipDriver>();
            if (driver == null) driver = Undo.AddComponent<VelaModernClipDriver>(model.gameObject);
            Undo.RecordObject(driver, "Apply Contest Vela arm-aligned animations");
            driver.Configure(animator, trainer, forceWalk);
            driver.enabled = true;
            foreach (MonoBehaviour component in model.GetComponents<MonoBehaviour>())
                if (component != null && component.GetType().Name == "VelaModernProceduralWalk" && component.enabled)
                { Undo.RecordObject(component, "Disable older procedural Vela animation"); component.enabled = false; }

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("MonsterTactics U3.7.2: Contest Vela arms aligned downward (" + armOutwardDegrees +
                "° out), other existing U3.7.1 curves preserved. " +
                (forceWalk ? "Walk in place enabled." : "Normal movement enabled."), model.gameObject);
        }

        private static AnimationClip MakeCopy(AnimationClip original, string path, string name)
        {
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null)
            {
                clip = Instantiate(original);
                clip.name = name;
                AssetDatabase.CreateAsset(clip, path);
            }
            else
            {
                EditorUtility.CopySerialized(original, clip);
                clip.name = name;
            }
            return clip;
        }

        private static void ReplaceShoulderCurve(AnimationClip clip, Transform model, string boneName,
            Quaternion rest, Vector3 localAxis, float amplitude, float seconds, int phaseSign)
        {
            Transform shoulder = FindBone(model, boneName);
            if (shoulder == null) throw new InvalidOperationException("Missing " + boneName + " in scene model.");
            string path = AnimationUtility.CalculateTransformPath(shoulder, model);
            AnimationCurve[] curves = { new AnimationCurve(), new AnimationCurve(),
                new AnimationCurve(), new AnimationCurve() };
            Quaternion previous = rest;
            for (int i = 0; i < 9; i++)
            {
                float time = seconds * i / 8f;
                float wave = Mathf.Sin(i * Mathf.PI * 2f / 8f);
                Quaternion q = rest * Quaternion.AngleAxis(amplitude * wave * phaseSign, localAxis);
                if (Quaternion.Dot(previous, q) < 0f)
                    q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
                previous = q;
                curves[0].AddKey(time, q.x);
                curves[1].AddKey(time, q.y);
                curves[2].AddKey(time, q.z);
                curves[3].AddKey(time, q.w);
            }
            string[] props = { "m_LocalRotation.x", "m_LocalRotation.y", "m_LocalRotation.z", "m_LocalRotation.w" };
            for (int n = 0; n < props.Length; n++)
            {
                for (int i = 0; i < curves[n].length; i++) curves[n].SmoothTangents(i, 0f);
                AnimationUtility.SetEditorCurve(clip,
                    EditorCurveBinding.FloatCurve(path, typeof(Transform), props[n]), curves[n]);
            }
        }

        private static AnimatorController MakeController(AnimationClip idle, AnimationClip walk)
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(FixedController);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(FixedController);
                controller.AddParameter("Move", AnimatorControllerParameterType.Float);
                AnimatorStateMachine sm = controller.layers[0].stateMachine;
                AnimatorState idleState = sm.AddState("Idle");
                AnimatorState walkState = sm.AddState("Walk");
                sm.defaultState = idleState;
                AnimatorStateTransition toWalk = idleState.AddTransition(walkState);
                toWalk.hasExitTime = false;
                toWalk.duration = .12f;
                toWalk.AddCondition(AnimatorConditionMode.Greater, .12f, "Move");
                AnimatorStateTransition toIdle = walkState.AddTransition(idleState);
                toIdle.hasExitTime = false;
                toIdle.duration = .15f;
                toIdle.AddCondition(AnimatorConditionMode.Less, .08f, "Move");
            }
            foreach (ChildAnimatorState state in controller.layers[0].stateMachine.states)
            {
                if (state.state.name == "Idle") state.state.motion = idle;
                if (state.state.name == "Walk") state.state.motion = walk;
            }
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private void Restore()
        {
            if (!TryGetScene(out Transform trainer, out Transform model)) return;
            AnimatorController source = AssetDatabase.LoadAssetAtPath<AnimatorController>(OriginalController);
            if (source == null)
            {
                EditorUtility.DisplayDialog("U3.7.1 controller missing", "Cannot find " + OriginalController, "OK");
                return;
            }
            Animator animator = model.GetComponent<Animator>();
            if (animator == null) return;
            Undo.RecordObject(animator, "Restore Contest Vela U3.7.1 walk");
            animator.runtimeAnimatorController = source;
            animator.enabled = true;
            VelaModernClipDriver driver = model.GetComponent<VelaModernClipDriver>();
            if (driver != null)
            {
                Undo.RecordObject(driver, "Restore Contest Vela U3.7.1 walk");
                driver.Configure(animator, trainer, false);
                driver.enabled = true;
            }
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("Contest Vela U3.7.1 animation restored. U3.7.2 generated assets remain available.", model.gameObject);
        }

        private static void SetLoop(AnimationClip clip)
        {
            clip.wrapMode = WrapMode.Loop;
            SerializedObject serialized = new SerializedObject(clip);
            SerializedProperty prop = serialized.FindProperty("m_AnimationClipSettings.m_LoopTime");
            if (prop != null) prop.boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureFolder(string folder)
        {
            string[] pieces = folder.Split('/');
            string current = pieces[0];
            for (int i = 1; i < pieces.Length; i++)
            {
                string next = current + "/" + pieces[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, pieces[i]);
                current = next;
            }
        }
    }
}
