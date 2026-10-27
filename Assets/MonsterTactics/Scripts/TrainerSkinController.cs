using UnityEngine;
using System.Collections.Generic;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace MonsterTactics
{
    /// <summary>
    /// Hybrid visual skins for the existing ONE movable trainer.
    /// Keeps TrainerMovement, its right-click navigation and all tile selection intact.
    /// The 2D skins work as before; 3D Vela skins use their own models and animators.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TrainerSkinController : MonoBehaviour
    {
        [Header("Existing U2 movement and visual")]
        [SerializeField] private TrainerMovement movement;
        [SerializeField] private SpriteRenderer pixelVisual;

        [Header("2D fallback for slot 1 and Classic Vela sprite frames")]
        [SerializeField] private Sprite[] starterFront;
        [SerializeField] private Sprite[] starterBack;
        [SerializeField] private Sprite[] starterLeft;
        [SerializeField] private Sprite[] starterRight;
        [SerializeField] private Sprite[] mayFront;
        [SerializeField] private Sprite[] mayBack;
        [SerializeField] private Sprite[] mayLeft;
        [SerializeField] private Sprite[] mayRight;

        [Header("Ranger 2D sprite frames (U2.1)")]
        [SerializeField] private Sprite[] adamanFront;
        [SerializeField] private Sprite[] adamanBack;
        [SerializeField] private Sprite[] adamanLeft;
        [SerializeField] private Sprite[] adamanRight;

        [Header("Regular ORAS Vela 3D visual (U3.3)")]
        [SerializeField] private GameObject modernVelaVisual;

        [Header("User controls")]
        [SerializeField, Range(0, 4)] private int currentSkin;
        [SerializeField] private bool showPicker = true;

        [Header("U3.7 Contest Vela — additional 3D skin")]
        [SerializeField] private GameObject contestVelaVisual;

        private Vector3 lastPosition;
        private Vector3 modelBaseLocalPosition;
        private Vector3 contestModelBaseLocalPosition;
        private GameObject ionoVisual;
        private Vector3 ionoBaseLocalPosition;
        private bool attemptedNovaLoad;
        private readonly Dictionary<string, Material> ionoMaterials = new Dictionary<string, Material>();
        private bool moving;

        [Header("Nova tuning (U6.4.2)")]
        [SerializeField, Range(0.6f, 2.5f)] private float ionoTargetHeight = 1.45f;
        [SerializeField, Range(-180f, 180f)] private float ionoFacingYaw = 180f; // Legacy COLLADA only.
        [Tooltip("Mixamo FBX faces forward in its local +Z direction. 0 means walk forward, not backwards.")]
        [SerializeField, Range(-180f, 180f)] private float ionoMixamoForwardCorrection = 0f;
        [SerializeField, Range(0.05f, 0.4f)] private float ionoWalkStopGraceSeconds = 0.18f;
        [SerializeField, Range(-0.5f, 0.5f)] private float ionoGroundOffset = 0.025f;
        [SerializeField, Range(0f, 0.08f)] private float ionoBounceHeight = 0.005f;
        private Animator ionoAnimator;
        private bool ionoHasSpeedParameter;
        private bool ionoHasWalkingParameter;
        private bool ionoMixamoRig;
        private bool ionoWasMoving;
        private bool ionoHasTurnTarget;
        private Quaternion ionoTurnTarget;
        private float ionoLastMoveTime = -100f;
        // The trainer transform owns navigation. Animation can move Mixamo's hips
        // internally; each frame we anchor them back to the trainer pivot.
        private Transform ionoMixamoModel;
        private Transform ionoMixamoHips;
        private Transform ionoMixamoLeftFoot;
        private Transform ionoMixamoRightFoot;
        [Header("Nova Mixamo grounding (U6.4.6)")]
        [SerializeField, Range(0f, 0.2f)] private float ionoAnkleAboveHex = 0.065f;
        [SerializeField, Range(90f, 1080f)] private float ionoTurnSpeedDegrees = 520f;

        [Header("Modern Vela - gentle walk bounce (U3.6.2)")]
        [SerializeField, Range(0f, 0.04f)] private float modernVelaBounceHeight = 0.010f;
        [SerializeField, Range(1f, 16f)] private float modernVelaBounceFrequency = 8f;

        public int CurrentSkin => currentSkin;

        public void Configure(
            TrainerMovement trainerMovement, SpriteRenderer sprite,
            Sprite[] originalFront, Sprite[] originalBack, Sprite[] originalLeft, Sprite[] originalRight,
            Sprite[] classicFront, Sprite[] classicBack, Sprite[] classicLeft, Sprite[] classicRight,
            Sprite[] diamondFront, Sprite[] diamondBack, Sprite[] diamondLeft, Sprite[] diamondRight,
            GameObject modernModel, int initialSkin)
        {
            movement = trainerMovement;
            pixelVisual = sprite;
            starterFront = originalFront;
            starterBack = originalBack;
            starterLeft = originalLeft;
            starterRight = originalRight;
            mayFront = classicFront;
            mayBack = classicBack;
            mayLeft = classicLeft;
            mayRight = classicRight;
            adamanFront = diamondFront;
            adamanBack = diamondBack;
            adamanLeft = diamondLeft;
            adamanRight = diamondRight;
            modernVelaVisual = modernModel;
            currentSkin = Mathf.Clamp(initialSkin, 0, 4);
            if (modernVelaVisual != null)
                modelBaseLocalPosition = modernVelaVisual.transform.localPosition;
            lastPosition = transform.position;
            SelectSkin(currentSkin);
        }

        private void Start()
        {
            if (movement == null) movement = GetComponent<TrainerMovement>();
            if (modernVelaVisual != null)
                modelBaseLocalPosition = modernVelaVisual.transform.localPosition;
            if (contestVelaVisual != null)
                contestModelBaseLocalPosition = contestVelaVisual.transform.localPosition;
            lastPosition = transform.position;
            SelectSkin(currentSkin);
        }

        // Called by the U3.7 editor installer. Keeps existing Configure(...) API intact.
        // Existing Ranger index 3 becomes index 4 when Contest Vela is installed.
        public void ConfigureContestVela(GameObject model)
        {
            if (contestVelaVisual == null && currentSkin == 3)
                currentSkin = 4;
            contestVelaVisual = model;
            if (contestVelaVisual != null)
                contestModelBaseLocalPosition = contestVelaVisual.transform.localPosition;
            SelectSkin(currentSkin);
        }

        public void SelectSkin(int index)
        {
            if (index < 0 || index > 4) return;
            // Compatibility until Contest Vela has been installed: key 4 was Ranger.
            if (index == 3 && contestVelaVisual == null)
                index = 4;
            currentSkin = index;
            if (movement == null) movement = GetComponent<TrainerMovement>();
            // Nova replaces the former pixel Starter appearance at slot 1.
            // Load only on demand; no scene rebuild or prefab installer is required.
            if (index == 0) EnsureNovaLoaded();

            if (index == 0 && movement != null)
                movement.SetAppearance(starterFront, starterBack, starterLeft, starterRight);
            else if (index == 1 && movement != null)
                movement.SetAppearance(mayFront, mayBack, mayLeft, mayRight);
            else if (index == 4 && movement != null)
                movement.SetAppearance(adamanFront, adamanBack, adamanLeft, adamanRight);

            if (pixelVisual != null) pixelVisual.enabled = index != 2 && index != 3 && !(index == 0 && ionoVisual != null);
            if (modernVelaVisual != null) modernVelaVisual.SetActive(index == 2);
            if (contestVelaVisual != null) contestVelaVisual.SetActive(index == 3);
            if (ionoVisual != null) ionoVisual.SetActive(index == 0);
        }

        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard k = Keyboard.current;
            if (k != null)
            {
                if (k.digit1Key.wasPressedThisFrame) SelectSkin(0);
                if (k.digit2Key.wasPressedThisFrame) SelectSkin(1);
                if (k.digit3Key.wasPressedThisFrame) SelectSkin(2);
                if (k.digit4Key.wasPressedThisFrame) SelectSkin(3);
                if (k.digit5Key.wasPressedThisFrame) SelectSkin(4);
                if (k.f1Key.wasPressedThisFrame) showPicker = !showPicker;
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Alpha1)) SelectSkin(0);
            if (Input.GetKeyDown(KeyCode.Alpha2)) SelectSkin(1);
            if (Input.GetKeyDown(KeyCode.Alpha3)) SelectSkin(2);
            if (Input.GetKeyDown(KeyCode.Alpha4)) SelectSkin(3);
            if (Input.GetKeyDown(KeyCode.Alpha5)) SelectSkin(4);
            if (Input.GetKeyDown(KeyCode.F1)) showPicker = !showPicker;
#endif

            // 3D model follows the exact same trainer transform as 2D skins.
            // Walking changes only appearance; TrainerMovement owns all navigation.
            Vector3 flatTravel = transform.position - lastPosition;
            flatTravel.y = 0f;
            moving = flatTravel.sqrMagnitude > 0.000005f;
            if (currentSkin == 0 && ionoVisual != null)
            {
                bool ionoTurning = false;
                bool isNavigating = movement != null && movement.IsNavigating;
                if (ionoMixamoRig)
                {
                    // Follow the actual destination, not how many units were moved
                    // since the preceding frame. This fixes pausing on turns,
                    // backwards travel and changes in TrainerMovement Update order.
                    Vector3 heading = isNavigating ? movement.NavigationDirection : flatTravel;
                    ionoTurning = TurnNovaTowardTravel(heading);
                    if (isNavigating || moving || ionoTurning)
                        ionoLastMoveTime = Time.time;
                }
                else
                    Update3DVisual(ionoVisual, ionoBaseLocalPosition, flatTravel);

                if (ionoAnimator != null && ionoAnimator.isActiveAndEnabled)
                {
                    if (ionoMixamoRig)
                    {
                        // Keep the imported Mixamo clip progressing throughout the
                        // complete navigation, including turning in place. Never
                        // restart it just because a frame has zero displacement.
                        bool playWalk = isNavigating || moving || ionoTurning ||
                            (Time.time - ionoLastMoveTime < ionoWalkStopGraceSeconds);
                        if (playWalk != ionoWasMoving)
                        {
                            // Start a new walk cycle only when leaving a true idle,
                            // not each time a direction or hex changes.
                            if (playWalk) ionoAnimator.Play("Walk", 0, 0f);
                            else ionoAnimator.Play("Walk", 0, 0f); // consistent standing pose
                        }
                        ionoAnimator.speed = playWalk ? 1f : 0f;
                        ionoWasMoving = playWalk;
                    }
                    else
                    {
                        if (ionoHasSpeedParameter)
                            ionoAnimator.SetFloat("Speed", moving ? 1f : 0f, 0.12f, Time.deltaTime);
                        if (ionoHasWalkingParameter)
                            ionoAnimator.SetBool("Walking", moving);
                    }
                }
            }
            else if (currentSkin == 2)
                Update3DVisual(modernVelaVisual, modelBaseLocalPosition, flatTravel);
            else if (currentSkin == 3)
                Update3DVisual(contestVelaVisual, contestModelBaseLocalPosition, flatTravel);
            lastPosition = transform.position;
        }

        // The trainer movement component may update before or after this controller.
        // Correct animated-root drift only AFTER both movement and the Animator have
        // updated, so rotating Nova never swings an offset model around a distant pivot.
        private void LateUpdate()
        {
            if (currentSkin == 0 && ionoMixamoRig && ionoVisual != null &&
                ionoVisual.activeInHierarchy && ionoMixamoModel != null)
            {
                KeepNovaFeetOnBoard();
            }
        }

        private bool TurnNovaTowardTravel(Vector3 flatTravel)
        {
            // Mixamo's rig faces +Z. The old 180-degree COLLADA correction made
            // the rig visually walk backwards as TrainerMovement moved it forward.
            if (flatTravel.sqrMagnitude > 0.000005f)
            {
                ionoTurnTarget = Quaternion.LookRotation(flatTravel.normalized, Vector3.up) *
                    Quaternion.Euler(0f, ionoMixamoForwardCorrection, 0f);
                ionoHasTurnTarget = true;
            }

            bool turning = false;
            if (ionoHasTurnTarget)
            {
                Quaternion current = ionoVisual.transform.rotation;
                turning = Quaternion.Angle(current, ionoTurnTarget) > 2f;
                if (turning)
                    ionoVisual.transform.rotation = Quaternion.RotateTowards(
                        current, ionoTurnTarget, ionoTurnSpeedDegrees * Time.deltaTime);
            }
            // Root position is owned by TrainerMovement. No extra procedural bob.
            ionoVisual.transform.localPosition = ionoBaseLocalPosition;
            return turning;
        }

        private void KeepNovaFeetOnBoard()
        {
            // The animation called "Walk W Briefcase" contains displacement in the
            // hips, even with Animator.applyRootMotion disabled. Undo that drift by
            // anchoring the hips' horizontal position to TrainerMovement's pivot.
            if (ionoMixamoHips != null)
            {
                Vector3 hips = ionoMixamoHips.position;
                Vector3 anchor = transform.position;
                ionoMixamoModel.position += new Vector3(anchor.x - hips.x, 0f, anchor.z - hips.z);
            }

            // Pin the sole height to the current hex surface. We use the lower of
            // the two ankle bones, not Renderer.bounds (hair/cape can extend below
            // the soles and cause visible hovering or bouncing).
            if (ionoMixamoLeftFoot != null || ionoMixamoRightFoot != null)
            {
                float ankleY = float.PositiveInfinity;
                if (ionoMixamoLeftFoot != null)
                    ankleY = Mathf.Min(ankleY, ionoMixamoLeftFoot.position.y);
                if (ionoMixamoRightFoot != null)
                    ankleY = Mathf.Min(ankleY, ionoMixamoRightFoot.position.y);
                float expected = transform.position.y + ionoGroundOffset + ionoAnkleAboveHex;
                ionoMixamoModel.position += Vector3.up * (expected - ankleY);
            }
        }

        // U6.4.1: The user-supplied Scarlet/Violet Nova COLLADA model replaces
        // the first trainer skin. Unity imports .dae models into Resources.
        private void EnsureNovaLoaded()
        {
            if (ionoVisual != null || attemptedNovaLoad) return;
            attemptedNovaLoad = true;
            // Prefer the correctly oriented Mixamo-rigged Monster Masters model.
            // The old sideways COLLADA Nova remains only as a compatibility fallback.
            if (LoadMixamoNova()) return;
            GameObject source = Resources.Load<GameObject>("Trainers/Nova/Nova");
            if (source == null)
            {
                Debug.LogWarning("MonsterTactics Nova: Could not load the .dae model in Resources/Trainers/Nova. " +
                                 "The old sprite is temporarily used to keep trainer movement working.", this);
                return;
            }

            ionoVisual = new GameObject("Nova (trainer slot 1)");
            ionoVisual.transform.SetParent(transform, false);
            // Instantiate the original model as a child: the outer transform handles
            // movement, turning and walk bob, without touching other trainer skins.
            GameObject model = Instantiate(source, ionoVisual.transform, false);
            model.name = "Nova | imported Scarlet Violet model";
            model.transform.localPosition = Vector3.zero;
            // Blender's Z-up COLLADA may be pre-converted by Unity on import.
            // Align using the ACTUAL foot/head bones, avoiding the fixed -90-degree
            // rotation that could leave Nova sideways or face-down in some imports.
            model.transform.localRotation = Quaternion.identity;
            AlignNovaUpright(model);
            model.transform.localRotation = Quaternion.Euler(0f, ionoFacingYaw, 0f) * model.transform.localRotation;
            // No native Nova motion was present in the uploaded COLLADA archive.
            // A real retargeted Animator Controller can be installed later under:
            //   Assets/MonsterTactics/Resources/Trainers/Nova/NovaLocomotion.controller
            // Until then, DO NOT alter shoulder/knee bones (the earlier procedural
            // animation twisted the imported rig and caused visible distortion).
            RuntimeAnimatorController controller = Resources.Load<RuntimeAnimatorController>(
                "Trainers/Nova/NovaLocomotion");
            ionoAnimator = model.GetComponentInChildren<Animator>(true);
            if (controller != null)
            {
                if (ionoAnimator == null) ionoAnimator = model.AddComponent<Animator>();
                ionoAnimator.runtimeAnimatorController = controller;
                ionoAnimator.applyRootMotion = false;
                ionoAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                ionoAnimator.enabled = true;
                foreach (AnimatorControllerParameter parameter in ionoAnimator.parameters)
                {
                    if (parameter.name == "Speed" && parameter.type == AnimatorControllerParameterType.Float)
                        ionoHasSpeedParameter = true;
                    if (parameter.name == "Walking" && parameter.type == AnimatorControllerParameterType.Bool)
                        ionoHasWalkingParameter = true;
                }
            }
            else if (ionoAnimator != null)
            {
                // Avoid accidental importer animation / bind-pose resets with no clip.
                ionoAnimator.enabled = false;
                ionoAnimator = null;
            }
            ApplyNovaMaterials(model);

            // Scale and ground the imported model more conservatively than U6.4.1.
            // The first pass normalized Nova too tall and left the model partially
            // sunk into the arena. Here we aim for a smaller trainer-sized silhouette.
            if (TryNovaBounds(model, out Bounds b))
            {
                float factor = Mathf.Clamp(ionoTargetHeight / Mathf.Max(0.001f, b.size.y), 0.0001f, 500f);
                model.transform.localScale *= factor;
                if (TryNovaBounds(model, out b))
                {
                    Vector3 worldDelta = new Vector3(
                        transform.position.x - b.center.x,
                        transform.position.y - b.min.y + ionoGroundOffset,
                        transform.position.z - b.center.z);
                    model.transform.position += worldDelta;
                }
            }
            ionoVisual.transform.localRotation = Quaternion.identity;
            ionoBaseLocalPosition = ionoVisual.transform.localPosition;
            // Keep the imported bind-pose untouched. U6.4.3's procedural walk is
            // intentionally NOT added to the model anymore.
            // Remove imported colliders: the existing trainer movement controller
            // already owns input and tile navigation.
            foreach (Collider collider in model.GetComponentsInChildren<Collider>(true))
                Destroy(collider);
            ionoVisual.SetActive(currentSkin == 0);
        }

        // U6.4.5 — Load Nova's Mixamo-rigged full body, provided by the user.
        // All animation and mesh data are contained in NovaRigged.dae. The Editor
        // creates NovaWalk.controller automatically from its embedded walk clip.
        private bool LoadMixamoNova()
        {
            GameObject source = Resources.Load<GameObject>("Trainers/NovaMixamo/NovaRigged");
            if (source == null) return false;

            ionoVisual = new GameObject("Nova (Mixamo trainer slot 1)");
            ionoVisual.transform.SetParent(transform, false);
            GameObject model = Instantiate(source, ionoVisual.transform, false);
            model.name = "Nova | Mixamo rig + walk";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity; // FBX is already Y-up.
            ApplyMixamoNovaMaterials(model);

            // Size relative to the character's visible mesh, not FBX import scale.
            if (TryNovaBounds(model, out Bounds bounds))
            {
                // Slightly taller than the prior 1.45-unit version.
                float factor = Mathf.Clamp(1.62f / Mathf.Max(0.001f, bounds.size.y), 0.0001f, 500f);
                model.transform.localScale *= factor;
                // Do NOT center the skinned mesh by renderer bounds. That shifts
                // the mesh away from its turning pivot, causing a visible orbit/
                // teleport whenever Nova changes direction. Mixamo's hip and ankle
                // bones are used for precise grounding in LateUpdate instead.
            }

            // FBX's forward axis is +Z, unlike the previous COLLADA model.
            ionoVisual.transform.localRotation = Quaternion.Euler(0f, ionoMixamoForwardCorrection, 0f);
            ionoBaseLocalPosition = ionoVisual.transform.localPosition;
            ionoTurnTarget = ionoVisual.transform.rotation;
            ionoHasTurnTarget = true;
            ionoLastMoveTime = -100f;
            ionoWasMoving = false;
            ionoMixamoModel = model.transform;
            ionoMixamoHips = FindNovaRigBone(model.transform, "Hips");
            ionoMixamoLeftFoot = FindNovaRigBone(model.transform, "LeftFoot");
            ionoMixamoRightFoot = FindNovaRigBone(model.transform, "RightFoot");
            if (ionoMixamoHips == null || (ionoMixamoLeftFoot == null && ionoMixamoRightFoot == null))
                Debug.LogWarning("MonsterTactics Nova: Could not locate all Mixamo bones for walk grounding.", this);
            foreach (Collider collider in model.GetComponentsInChildren<Collider>(true))
                Destroy(collider);

            ionoAnimator = model.GetComponentInChildren<Animator>(true);
            if (ionoAnimator == null) ionoAnimator = model.AddComponent<Animator>();
            RuntimeAnimatorController locomotion = Resources.Load<RuntimeAnimatorController>(
                "Trainers/NovaMixamo/NovaWalk");
            if (locomotion != null)
            {
                ionoAnimator.runtimeAnimatorController = locomotion;
                ionoAnimator.applyRootMotion = false;
                ionoAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                ionoAnimator.enabled = true;
                ionoAnimator.Play("Walk", 0, 0f);
                ionoAnimator.speed = 0f; // standing until TrainerMovement starts moving
                ionoMixamoRig = true;
            }
            else
            {
                ionoAnimator.enabled = false;
                Debug.LogWarning("MonsterTactics Nova: Walk controller not imported yet. " +
                    "Select Assets > Reimport All, or reimport NovaRigged.dae.", this);
            }
            ionoVisual.SetActive(currentSkin == 0);
            return true;
        }

        private void ApplyMixamoNovaMaterials(GameObject model)
        {
            // The uploaded Mixamo FBX has seven meshes but no embedded textures.
            // Their numbering matches the OBJ material mapping supplied by the user.
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                string name = renderer.gameObject.name.ToLowerInvariant();
                string textureName;
                if (name.Contains("mesh_2")) textureName = "Coat";
                else if (name.Contains("mesh_3")) textureName = "Magnemite_Eye";
                else if (name.Contains("mesh_5") || name.Contains("mesh_6")) textureName = "Face";
                else textureName = "Body";
                Material material = GetMixamoMaterial(textureName);
                if (material == null) continue;
                Material[] slots = renderer.sharedMaterials;
                if (slots == null || slots.Length == 0)
                    renderer.sharedMaterial = material;
                else
                {
                    for (int i = 0; i < slots.Length; i++) slots[i] = material;
                    renderer.sharedMaterials = slots;
                }
                SkinnedMeshRenderer skinned = renderer as SkinnedMeshRenderer;
                if (skinned != null) skinned.updateWhenOffscreen = true;
            }
        }

        private Material GetMixamoMaterial(string textureName)
        {
            string key = "Mixamo:" + textureName;
            if (ionoMaterials.TryGetValue(key, out Material existing)) return existing;
            Texture2D texture = Resources.Load<Texture2D>("Trainers/NovaMixamo/" + textureName);
            if (texture == null) return null;
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Transparent Cutout");
            if (shader == null) shader = Shader.Find("Unlit/Texture");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) return null;
            Material material = new Material(shader) { name = "Nova Mixamo " + textureName };
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
            if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);
            ionoMaterials[key] = material;
            return material;
        }

        private static void AlignNovaUpright(GameObject model)
        {
            Transform head = FindNovaRigBone(model.transform, "head");
            Transform feet = FindNovaRigBone(model.transform, "foot_base");
            if (feet == null) feet = FindNovaRigBone(model.transform, "hips");
            if (head != null && feet != null)
            {
                Vector3 up = head.position - feet.position;
                if (up.sqrMagnitude > 0.000001f)
                {
                    model.transform.rotation =
                        Quaternion.FromToRotation(up.normalized, Vector3.up) * model.transform.rotation;
                    return;
                }
            }
            // Fallback for imports without the expected rig bones.
            model.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
        }

        private static Transform FindNovaRigBone(Transform root, string shortName)
        {
            string requested = shortName.ToLowerInvariant();
            foreach (Transform bone in root.GetComponentsInChildren<Transform>(true))
            {
                string name = bone.name.ToLowerInvariant();
                if (name == requested || name.EndsWith("_" + requested) ||
                    name.EndsWith(":" + requested)) return bone;
            }
            return null;
        }

        private static bool TryNovaBounds(GameObject model, out Bounds bounds)
        {
            bounds = new Bounds();
            bool found = false;
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                if (!found) { bounds = renderer.bounds; found = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
            return found && bounds.size.y > 0.0001f;
        }

        private void ApplyNovaMaterials(GameObject model)
        {
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                Material[] slots = renderer.sharedMaterials;
                if (slots == null || slots.Length == 0) continue;
                string part = renderer.gameObject.name.ToLowerInvariant();
                for (int i = 0; i < slots.Length; i++)
                {
                    string materialName = slots[i] != null ? slots[i].name.ToLowerInvariant() : part;
                    string textureName = NovaTextureFor(materialName + " " + part);
                    Material mat = GetNovaMaterial(textureName);
                    if (mat != null) slots[i] = mat;
                }
                renderer.sharedMaterials = slots;
                SkinnedMeshRenderer skinned = renderer as SkinnedMeshRenderer;
                if (skinned != null) skinned.updateWhenOffscreen = true;
            }
        }

        private static string NovaTextureFor(string material)
        {
            if (material.Contains("eye_lens") || material.Contains("lens")) return "cm_eye_lens00_alb";
            if (material.Contains("eyelash") || material.Contains("eyebrow")) return "tr0240_00_eyelash_00_alb";
            if (material.Contains("obj00")) return "tr0240_00_obj00_00_alb";
            if (material.Contains("obj01")) return "tr0240_00_obj01_00_alb";
            if (material.Contains("obj02")) return "tr0240_00_obj02_00_alb";
            if (material.Contains("hairstyle") || material.Contains("hair")) return "tr0240_00_hairstyle_00_alb";
            if (material.Contains("body_skin") || material.Contains("face_skin") || material.Contains("face")) return "tr0240_00_face_00_alb";
            if (material.Contains("eye")) return "tr0240_00_eye_00_alb";
            return "tr0240_00_body_00_alb";
        }

        private Material GetNovaMaterial(string textureName)
        {
            if (ionoMaterials.TryGetValue(textureName, out Material cached)) return cached;
            Texture2D texture = Resources.Load<Texture2D>("Trainers/Nova/" + textureName);
            if (texture == null) return null;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Unlit/Texture");
            if (shader == null) return null;
            Material material = new Material(shader) { name = "Nova " + textureName };
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
            if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);
            // Nova's hair and lenses have significant transparent pixels.
            bool cutout = textureName.Contains("hairstyle") || textureName.Contains("lens");
            if (material.HasProperty("_AlphaClip")) material.SetFloat("_AlphaClip", cutout ? 1f : 0f);
            if (material.HasProperty("_Cutoff")) material.SetFloat("_Cutoff", cutout ? 0.35f : 0.5f);
            if (cutout) material.EnableKeyword("_ALPHATEST_ON");
            ionoMaterials[textureName] = material;
            return material;
        }

        private void OnDestroy()
        {
            foreach (Material material in ionoMaterials.Values)
                if (material != null) Destroy(material);
            ionoMaterials.Clear();
        }

        private void Update3DVisual(GameObject visual, Vector3 restPosition, Vector3 flatTravel)
        {
            if (visual == null) return;
            if (moving)
            {
                // Same corrected U3.4.4 direction for both 3D Vela skins.
                Quaternion target = Quaternion.LookRotation(flatTravel.normalized, Vector3.up);
                visual.transform.rotation = Quaternion.Slerp(
                    visual.transform.rotation, target, Mathf.Clamp01(Time.deltaTime * 14f));
            }
            float height = visual == ionoVisual ? (ionoMixamoRig ? 0f : ionoBounceHeight) : modernVelaBounceHeight;
            float bounce = moving
                ? Mathf.Abs(Mathf.Sin(Time.time * modernVelaBounceFrequency)) * height
                : 0f;
            visual.transform.localPosition = restPosition + Vector3.up * bounce;
        }

        private void OnGUI()
        {
            if (!showPicker || MonsterTacticsU5CanvasHud.HasActiveHud) return; // U5.2 Canvas owns the trainer picker.
            float width = Mathf.Min(Screen.width - 24f, 650f);
            if (width < 180f) return;
            float x = 12f;
            float y = Mathf.Max(12f, Screen.height - 90f);
            GUI.Box(new Rect(x, y, width, 78f), "MONSTERTACTICS TRAINERS     1 / 2 / 3 / 4 / 5    ·    F1 Hide");
            string[] names = { "1 Nova", "2 Classic Vela", "3 ORAS Vela", "4 Contest Vela", "5 Ranger" };
            float buttonWidth = (width - 18f) / names.Length;
            for (int i = 0; i < names.Length; i++)
            {
                Color previous = GUI.color;
                GUI.color = currentSkin == i ? new Color(0.56f, 0.95f, 0.74f) : Color.white;
                if (GUI.Button(new Rect(x + 6f + i * buttonWidth, y + 30f, buttonWidth - 2f, 32f), names[i]))
                    SelectSkin(i);
                GUI.color = previous;
            }
        }
    }
}
