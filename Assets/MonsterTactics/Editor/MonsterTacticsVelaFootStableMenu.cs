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
    /// U3.6.1: Generates NON-DESTRUCTIVE alternate Vela Idle / Walk clips.
    /// The original U3.6 clips and controller remain untouched.
    ///
    /// ORAS Vela's thigh -> shin -> foot bones extend along their local X axes.
    /// Rotating around that axis twists the leg. For front/back strides, use local Y
    /// and compensate the ankle so shoes do not roll with hip/knee rotations.
    /// This is a basic gait, not full planted-foot IK.
    /// </summary>
    public static class MonsterTacticsVelaFootStableMenu
    {
        private const string TrainerName = "MonsterTactics Trainer";
        private const string ProxyName = "Modern Vela 3D - Proxy";
        private const string ModelName = "Vela ORAS Model (Imported)";

        private const string Folder = "Assets/MonsterTactics/Animations/VelaModern/U361_FootStable";
        private const string IdlePath = Folder + "/Vela_Idle_FootStable.anim";
        private const string WalkPath = Folder + "/Vela_Walk_FootStable.anim";
        private const string ControllerPath = Folder + "/Vela_FootStable.controller";
        private const string OriginalController = "Assets/MonsterTactics/Animations/VelaModern/Vela_Animator.controller";
        private const string FbxPath = "Assets/MonsterTactics/Art/Trainer/VelaModern/VelaOW.dae";

        // Nine poses over 0.8s. First/last keyframes match, avoiding loop snapping.
        private static readonly float[] Times = { 0f, .1f, .2f, .3f, .4f, .5f, .6f, .7f, .8f };
        private static readonly float[] LeftHip = { -16, -12, 0, 12, 16, 12, 0, -12, -16 };
        private static readonly float[] RightHip = { 16, 12, 0, -12, -16, -12, 0, 12, 16 };
        private static readonly float[] LeftKnee = { 2, 5, 16, 18, 7, 2, 1, 1, 2 };
        private static readonly float[] RightKnee = { 7, 2, 1, 1, 2, 5, 16, 18, 7 };
        private static readonly float[] LeftArm = { 9, 7, 0, -7, -9, -7, 0, 7, 9 };
        private static readonly float[] RightArm = { -9, -7, 0, 7, 9, 7, 0, -7, -9 };

        [MenuItem("MonsterTactics/Trainers/Use Foot-Stable Vela Walk (U3.6.1)", priority = 131)]
        private static void Install() { Setup(false, false); }

        [MenuItem("MonsterTactics/Trainers/Test Foot-Stable Vela Walk in Place (U3.6.1)", priority = 132)]
        private static void TestWalk() { Setup(true, false); }

        [MenuItem("MonsterTactics/Trainers/Stop Foot-Stable Walk Test (U3.6.1)", priority = 133)]
        private static void StopTest() { Setup(false, false); }

        [MenuItem("MonsterTactics/Trainers/Restore Original U3.6 Vela Clips", priority = 134)]
        private static void RestoreOriginal()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { WarnStopPlay(); return; }
            if (!TryFindModel(out Transform trainer, out Transform model)) return;
            RuntimeAnimatorController original = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(OriginalController);
            if (original == null)
            {
                EditorUtility.DisplayDialog("Original U3.6 controller not found",
                    "Expected " + OriginalController + ". The U3.6.1 clips have not been changed.", "OK");
                return;
            }
            Animator animator = model.GetComponent<Animator>();
            if (animator == null) return;
            Undo.RecordObject(animator, "Restore original Vela Animator");
            animator.runtimeAnimatorController = original;
            VelaModernClipDriver driver = model.GetComponent<VelaModernClipDriver>();
            if (driver != null)
            {
                Undo.RecordObject(driver, "Restore Vela clip driver");
                driver.Configure(animator, trainer, false);
                EditorUtility.SetDirty(driver);
            }
            EditorUtility.SetDirty(animator);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("MonsterTactics: Restored the original U3.6 Vela Animator. The alternate foot-stable clips were kept.", model);
        }

        [MenuItem("MonsterTactics/Trainers/Rebuild Foot-Stable Vela Clips (U3.6.1)", priority = 135)]
        private static void Rebuild()
        {
            if (!EditorUtility.DisplayDialog("Rebuild Foot-Stable Clips?",
                "This overwrites any edits to the U3.6.1 foot-stable Idle/Walk clips. Original U3.6 clips will NOT be changed.",
                "Rebuild", "Cancel")) return;
            Setup(false, true);
        }

        private static void Setup(bool test, bool rebuild)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { WarnStopPlay(); return; }
            if (!TryFindModel(out Transform trainer, out Transform model)) return;

            // Use the UNMODIFIED FBX bone poses, not the pose currently stored
            // in the scene (which might have manually adjusted shoulders).
            GameObject importedFbx = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            Transform source = importedFbx != null ? importedFbx.transform : null;
            if (source == null)
            {
                EditorUtility.DisplayDialog("Vela ORAS FBX missing",
                    "Cannot load " + FbxPath + ". Import the U3.3 ORAS model first.", "OK");
                return;
            }
            string[] names = { "LThigh", "RThigh", "LLeg", "RLeg", "LFoot", "RFoot", "LShoulder", "RShoulder" };
            foreach (string name in names)
            {
                if (FindBone(source, name) != null && FindBone(model, name) != null) continue;
                EditorUtility.DisplayDialog("Vela bone missing",
                    "Cannot locate " + name + " in both the imported FBX and your active Vela model. No changes were made.", "OK");
                return;
            }

            EnsureFolder(Folder);
            AnimationClip idle = AssetDatabase.LoadAssetAtPath<AnimationClip>(IdlePath);
            if (idle == null)
            {
                idle = new AnimationClip { name = "Vela Idle (foot-stable)", frameRate = 30f };
                AssetDatabase.CreateAsset(idle, IdlePath);
                BuildIdle(idle, model, source);
            }
            else if (rebuild)
            {
                idle.ClearCurves();
                BuildIdle(idle, model, source);
            }
            AnimationClip walk = AssetDatabase.LoadAssetAtPath<AnimationClip>(WalkPath);
            if (walk == null)
            {
                walk = new AnimationClip { name = "Vela Walk (foot-stable)", frameRate = 30f };
                AssetDatabase.CreateAsset(walk, WalkPath);
                BuildWalk(walk, model, source);
            }
            else if (rebuild)
            {
                walk.ClearCurves();
                BuildWalk(walk, model, source);
            }

            AnimatorController controller = EnsureController(idle, walk);
            Animator animator = model.GetComponent<Animator>();
            if (animator == null) animator = Undo.AddComponent<Animator>(model.gameObject);
            Undo.RecordObject(animator, "Use foot-stable Vela clips");
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            foreach (UnityEngine.Object obj in AssetDatabase.LoadAllAssetsAtPath(FbxPath))
                if (obj is Avatar avatar && avatar.isValid)
                {
                    animator.avatar = avatar;
                    break;
                }

            // Only one animation system may control this skeleton.
            foreach (MonoBehaviour component in model.GetComponents<MonoBehaviour>())
                if (component != null && component.GetType().Name == "VelaModernProceduralWalk" && component.enabled)
                {
                    Undo.RecordObject(component, "Disable old procedural Vela walk");
                    component.enabled = false;
                    EditorUtility.SetDirty(component);
                }

            VelaModernClipDriver driver = model.GetComponent<VelaModernClipDriver>();
            if (driver == null) driver = Undo.AddComponent<VelaModernClipDriver>(model.gameObject);
            Undo.RecordObject(driver, "Connect foot-stable Vela Animator");
            driver.Configure(animator, trainer, test);
            driver.enabled = true;

            EditorUtility.SetDirty(animator);
            EditorUtility.SetDirty(driver);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Selection.activeGameObject = model.gameObject;
            Debug.Log("MonsterTactics U3.6.1: foot-stable Vela walk installed. " +
                (test ? "Walk-in-place test ON." : "Walk responds to right-click trainer movement.") +
                " Original U3.6 clips remain available for rollback.", model.gameObject);
        }

        private static void BuildIdle(AnimationClip clip, Transform sceneRoot, Transform fbxRoot)
        {
            float[] times = { 0f, 0.6f, 1.2f };
            foreach (string name in new[] { "LThigh", "RThigh", "LLeg", "RLeg", "LFoot", "RFoot" })
            {
                Quaternion q = FindBone(fbxRoot, name).localRotation;
                KeyQuaternions(clip, sceneRoot, FindBone(sceneRoot, name), times, Repeat(q, times.Length));
            }
            KeyQuaternions(clip, sceneRoot, FindBone(sceneRoot, "LShoulder"), times,
                Repeat(RelaxedShoulder(FindBone(fbxRoot, "LShoulder"), 180f), times.Length));
            KeyQuaternions(clip, sceneRoot, FindBone(sceneRoot, "RShoulder"), times,
                Repeat(RelaxedShoulder(FindBone(fbxRoot, "RShoulder"), -180f), times.Length));
            Transform sceneSpine = FindBone(sceneRoot, "Spine2");
            Transform fbxSpine = FindBone(fbxRoot, "Spine2");
            if (sceneSpine != null && fbxSpine != null)
            {
                Quaternion rest = fbxSpine.localRotation;
                KeyQuaternions(clip, sceneRoot, sceneSpine, times,
                    new[] { rest, rest * Quaternion.AngleAxis(0.6f, Vector3.up), rest });
            }
            clip.EnsureQuaternionContinuity();
            SetLooping(clip);
        }

        private static void BuildWalk(AnimationClip clip, Transform sceneRoot, Transform fbxRoot)
        {
            // Correct bending axis: local Y. Local X is along the shin/femur
            // and twisting around it visually rotates shoes.
            PoseOffset(clip, sceneRoot, fbxRoot, "LThigh", LeftHip);
            PoseOffset(clip, sceneRoot, fbxRoot, "RThigh", RightHip);
            PoseOffset(clip, sceneRoot, fbxRoot, "LLeg", LeftKnee);
            PoseOffset(clip, sceneRoot, fbxRoot, "RLeg", RightKnee);

            // Counter-rotate each ankle by the sum of hip + knee bend so the
            // shoes keep approximately the same orientation throughout a step.
            // True foot planting still requires IK / authored model-specific animation.
            float[] leftFoot = new float[Times.Length];
            float[] rightFoot = new float[Times.Length];
            for (int i = 0; i < Times.Length; i++)
            {
                leftFoot[i] = -(LeftHip[i] + LeftKnee[i]);
                rightFoot[i] = -(RightHip[i] + RightKnee[i]);
            }
            PoseOffset(clip, sceneRoot, fbxRoot, "LFoot", leftFoot);
            PoseOffset(clip, sceneRoot, fbxRoot, "RFoot", rightFoot);

            PoseShoulder(clip, sceneRoot, fbxRoot, "LShoulder", 180f, LeftArm);
            PoseShoulder(clip, sceneRoot, fbxRoot, "RShoulder", -180f, RightArm);

            Transform spine = FindBone(sceneRoot, "Spine2");
            Transform fbxSpine = FindBone(fbxRoot, "Spine2");
            if (spine != null && fbxSpine != null)
            {
                Quaternion rest = fbxSpine.localRotation;
                float[] yaw = { -0.8f, 0, 0.8f, 0, -0.8f, 0, 0.8f, 0, -0.8f };
                Quaternion[] poses = new Quaternion[Times.Length];
                for (int i = 0; i < Times.Length; i++)
                    poses[i] = rest * Quaternion.AngleAxis(yaw[i], Vector3.up);
                KeyQuaternions(clip, sceneRoot, spine, Times, poses);
            }
            clip.EnsureQuaternionContinuity();
            SetLooping(clip);
        }

        private static void PoseOffset(AnimationClip clip, Transform sceneRoot,
            Transform fbxRoot, string boneName, float[] degrees)
        {
            Transform bone = FindBone(sceneRoot, boneName);
            Quaternion rest = FindBone(fbxRoot, boneName).localRotation;
            Quaternion[] poses = new Quaternion[degrees.Length];
            for (int i = 0; i < degrees.Length; i++)
                poses[i] = rest * Quaternion.AngleAxis(degrees[i], Vector3.up);
            KeyQuaternions(clip, sceneRoot, bone, Times, poses);
        }

        private static void PoseShoulder(AnimationClip clip, Transform sceneRoot, Transform fbxRoot,
            string boneName, float relaxedZ, float[] degrees)
        {
            Quaternion rest = RelaxedShoulder(FindBone(fbxRoot, boneName), relaxedZ);
            Quaternion[] poses = new Quaternion[degrees.Length];
            for (int i = 0; i < degrees.Length; i++)
                poses[i] = rest * Quaternion.AngleAxis(degrees[i], Vector3.up);
            KeyQuaternions(clip, sceneRoot, FindBone(sceneRoot, boneName), Times, poses);
        }

        private static Quaternion RelaxedShoulder(Transform bone, float z)
        {
            Vector3 euler = bone.localEulerAngles;
            return Quaternion.Euler(euler.x, euler.y, z);
        }

        private static void KeyQuaternions(AnimationClip clip, Transform root, Transform bone,
            float[] times, Quaternion[] poses)
        {
            string path = AnimationUtility.CalculateTransformPath(bone, root);
            AnimationCurve[] curves = { new AnimationCurve(), new AnimationCurve(),
                new AnimationCurve(), new AnimationCurve() };
            Quaternion previous = poses[0];
            for (int i = 0; i < times.Length; ++i)
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
            string[] channels = { "m_LocalRotation.x", "m_LocalRotation.y",
                "m_LocalRotation.z", "m_LocalRotation.w" };
            for (int c = 0; c < 4; ++c)
                AnimationUtility.SetEditorCurve(clip,
                    EditorCurveBinding.FloatCurve(path, typeof(Transform), channels[c]), curves[c]);
        }

        private static Quaternion[] Repeat(Quaternion q, int count)
        {
            Quaternion[] poses = new Quaternion[count];
            for (int i = 0; i < count; ++i) poses[i] = q;
            return poses;
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
            if (controller != null) return controller;
            controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("Move", AnimatorControllerParameterType.Float);
            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            AnimatorState idleState = stateMachine.AddState("Idle");
            idleState.motion = idle;
            AnimatorState walkState = stateMachine.AddState("Walk");
            walkState.motion = walk;
            stateMachine.defaultState = idleState;
            AnimatorStateTransition toWalk = idleState.AddTransition(walkState);
            toWalk.hasExitTime = false;
            toWalk.hasFixedDuration = true;
            toWalk.duration = 0.13f;
            toWalk.AddCondition(AnimatorConditionMode.Greater, 0.12f, "Move");
            AnimatorStateTransition toIdle = walkState.AddTransition(idleState);
            toIdle.hasExitTime = false;
            toIdle.hasFixedDuration = true;
            toIdle.duration = 0.15f;
            toIdle.AddCondition(AnimatorConditionMode.Less, 0.08f, "Move");
            return controller;
        }

        private static Transform FindBone(Transform root, string name)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == name) return child;
            return null;
        }

        private static bool TryFindModel(out Transform trainer, out Transform model)
        {
            trainer = null;
            model = null;
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.name != TrainerName) continue;
                trainer = root.transform;
                Transform proxy = trainer.Find(ProxyName);
                if (proxy != null) model = proxy.Find(ModelName);
                break;
            }
            if (model != null) return true;
            EditorUtility.DisplayDialog("ORAS Vela model not found",
                "Open your existing MonsterTactics arena scene first. Expected: " +
                TrainerName + " → " + ProxyName + " → " + ModelName +
                ". No scene objects were changed.", "OK");
            return false;
        }

        private static void EnsureFolder(string folder)
        {
            string[] parts = folder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; ++i)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static void WarnStopPlay()
        {
            EditorUtility.DisplayDialog("MonsterTactics U3.6.1", "Stop Play mode before updating Vela's animation.", "OK");
        }
    }
}
