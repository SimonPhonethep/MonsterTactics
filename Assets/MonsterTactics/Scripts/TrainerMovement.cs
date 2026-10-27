using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace MonsterTactics
{
    /// <summary>
    /// Right-click movement over the hex board AND the ground. Left-click tile selection is untouched.
    /// Does not require NavMesh, a ground collider, or an Animator Controller.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TrainerMovement : MonoBehaviour
    {
        [Header("Arena references (assigned by MonsterTactics / Add Movable Trainer)")]
        [SerializeField] private Camera targetCamera;
        [SerializeField] private Transform arenaGround;
        [SerializeField] private SpriteRenderer trainerSprite;
        [SerializeField] private GameObject destinationMarker;

        [Header("Four-direction pixel-art animations")]
        [SerializeField] private Sprite[] frontFrames;
        [SerializeField] private Sprite[] backFrames;
        [SerializeField] private Sprite[] leftFrames;
        [SerializeField] private Sprite[] rightFrames;

        [Header("Movement settings")]
        [SerializeField, Min(0.1f)] private float moveSpeed = 2.8f;
        [SerializeField, Min(1f)] private float framesPerSecond = 7f;
        [SerializeField, Min(0.01f)] private float arrivalDistance = 0.055f;

        private const float GroundOffset = 0.032f;
        private const float EdgeMargin = 0.22f;

        [Header("U5.2.3 Trainer foot height")]
        [SerializeField, Range(-0.05f, 0.20f)]
        private float footHeightAdjustment = 0.02f;

        [Header("U5.2.4 - hex seam support")]
        [Tooltip("How far Vela's feet can reach onto nearby hexes when crossing gaps. Increase slightly if narrow seams still cause sinking.")]
        [SerializeField, Range(0.07f, 0.35f)]
        private float footSupportRadius = 0.18f;

        [Tooltip("Briefly preserve the last supported height while crossing a narrow empty seam. Set to zero to disable.")]
        [SerializeField, Range(0f, 0.25f)]
        private float seamGraceSeconds = 0.10f;

        // Reused buffer avoids allocating a new hit array for each foot probe.
        private readonly RaycastHit[] supportHits = new RaycastHit[32];
        private float lastSupportedY;
        private float lastSupportedTime = -1000f;

        private Vector3 destination;
        private bool isMoving;
        private Facing facing = Facing.Front;
        private float animationTime;

        private enum Facing { Front, Back, Left, Right }

        // U6.4.8: Expose navigation intent to animation controllers. Measuring
        // transform displacement in another component's Update is unreliable:
        // script execution order, hex-seam height changes and turn-in-place frames
        // can all look like a one-frame stop while a destination is still active.
        public bool IsNavigating => isMoving;

        public Vector3 NavigationDirection
        {
            get
            {
                if (!isMoving) return Vector3.zero;
                Vector3 direction = destination - transform.position;
                direction.y = 0f;
                return direction.sqrMagnitude > 0.000001f ? direction.normalized : Vector3.zero;
            }
        }

        /// <summary>Editor-only setup action calls this at generation time.</summary>
        public void Configure(
            Camera camera,
            Transform ground,
            SpriteRenderer sprite,
            GameObject marker,
            Sprite[] front,
            Sprite[] back,
            Sprite[] left,
            Sprite[] right)
        {
            targetCamera = camera;
            arenaGround = ground;
            trainerSprite = sprite;
            destinationMarker = marker;
            SetAppearance(front, back, left, right);
        }

        /// <summary>
        /// Update the sprite set without resetting position, destination, facing, or speed.
        /// Called by the character selector; does not change left/right mouse controls.
        /// </summary>
        public void SetAppearance(Sprite[] front, Sprite[] back, Sprite[] left, Sprite[] right)
        {
            frontFrames = front;
            backFrames = back;
            leftFrames = left;
            rightFrames = right;
            ApplySprite();
        }

        private void Awake()
        {
            if (targetCamera == null) targetCamera = Camera.main;
            destination = transform.position;
            if (destinationMarker != null) destinationMarker.SetActive(false);
            ApplySprite();
        }

        private void Update()
        {
            if (WasRightClickPressed(out Vector2 pointerPosition))
                TrySetDestination(pointerPosition);

            AdvanceMovement(Time.deltaTime);
        }

        private void LateUpdate()
        {
            // Keep a true sprite visible in the fixed isometric 3D scene.
            if (trainerSprite != null && targetCamera != null)
                trainerSprite.transform.rotation = targetCamera.transform.rotation;
        }

        private static bool WasRightClickPressed(out Vector2 position)
        {
#if ENABLE_INPUT_SYSTEM
            Mouse mouse = Mouse.current;
            position = mouse != null ? mouse.position.ReadValue() : Vector2.zero;
            return mouse != null && mouse.rightButton.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            position = Input.mousePosition;
            return Input.GetMouseButtonDown(1);
#else
            position = Vector2.zero;
            return false;
#endif
        }

        private void TrySetDestination(Vector2 pointerPosition)
        {
            if (targetCamera == null || arenaGround == null) return;

            Ray ray = targetCamera.ScreenPointToRay(pointerPosition);
            float floorHeight = arenaGround.position.y + GroundOffset + footHeightAdjustment;
            Plane floorPlane = new Plane(Vector3.up, new Vector3(0f, floorHeight, 0f));
            Vector3 point;

            // TFT-style movement: the trainer is allowed on the hex board.
            // Raycast against the raised tiles first, so the click lands where
            // the pointer appears instead of behind the tile on the floor plane.
            if (TryGetWalkableHit(ray, 250f, out RaycastHit hit))
            {
                point = hit.point;
            }
            else
            {
                // The arena ground has no collider. Use its horizontal plane
                // for right-clicks on the empty area around the board.
                if (!floorPlane.Raycast(ray, out float distance)) return;
                point = ray.GetPoint(distance);
            }

            Renderer floorRenderer = arenaGround.GetComponent<Renderer>();
            if (floorRenderer == null) return;
            Bounds limits = floorRenderer.bounds;
            if (point.x < limits.min.x || point.x > limits.max.x ||
                point.z < limits.min.z || point.z > limits.max.z)
                return;

            point.x = Mathf.Clamp(point.x, limits.min.x + EdgeMargin, limits.max.x - EdgeMargin);
            point.z = Mathf.Clamp(point.z, limits.min.z + EdgeMargin, limits.max.z - EdgeMargin);

            // SurfaceHeight keeps the sprite's feet above a hex, but also
            // supports travel across the surrounding empty arena ground.
            destination = new Vector3(point.x, SurfaceHeight(point.x, point.z, false), point.z);
            isMoving = true;
            animationTime = 0f;

            if (destinationMarker != null)
            {
                destinationMarker.transform.position = destination + Vector3.up * 0.015f;
                destinationMarker.SetActive(true);
            }
        }

        private void AdvanceMovement(float dt)
        {
            if (!isMoving)
            {
                // Reserve bench meshes are raised like board tiles; correct the
                // trainer's Y even after navigation has come to a stop.
                float desiredHeight = SurfaceHeight(transform.position.x, transform.position.z);
                float height = Mathf.MoveTowards(transform.position.y, desiredHeight, 5f * dt);
                transform.position = new Vector3(transform.position.x, height, transform.position.z);
                ApplySprite();
                return;
            }

            Vector3 planarDelta = destination - transform.position;
            planarDelta.y = 0f;
            float remaining = planarDelta.magnitude;

            if (remaining <= arrivalDistance)
            {
                isMoving = false;
                if (destinationMarker != null) destinationMarker.SetActive(false);
                ApplySprite();
                return;
            }

            SelectFacing(planarDelta);
            Vector3 next = Vector3.MoveTowards(
                new Vector3(transform.position.x, 0f, transform.position.z),
                new Vector3(destination.x, 0f, destination.z),
                moveSpeed * dt);

            // When crossing the raised hex meshes, place the feet on the tile
            // top instead of letting them disappear inside the geometry.
            float footHeight = SurfaceHeight(next.x, next.z);
            float smoothY = Mathf.MoveTowards(transform.position.y, footHeight, 5f * dt);
            transform.position = new Vector3(next.x, smoothY, next.z);

            animationTime += dt;
            ApplySprite();
        }

        private static bool IsWalkableSurface(Collider collider)
        {
            // The 3D bench has MeshColliders but deliberately no HexTile component.
            // The dedicated marker lets trainer navigation use them without making
            // them combat cells or altering tile-selection behavior.
            return collider != null &&
                (collider.GetComponent<HexTile>() != null ||
                 collider.GetComponent<MonsterTacticsBenchSurface>() != null);
        }

        private static bool TryGetWalkableHit(Ray ray, float maxDistance, out RaycastHit closest)
        {
            closest = default;
            bool found = false;
            float bestDistance = float.PositiveInfinity;
            RaycastHit[] hits = Physics.RaycastAll(ray, maxDistance, ~0, QueryTriggerInteraction.Ignore);
            foreach (RaycastHit hit in hits)
            {
                if (!IsWalkableSurface(hit.collider) || hit.distance >= bestDistance) continue;
                found = true;
                bestDistance = hit.distance;
                closest = hit;
            }
            return found;
        }

        /// <summary>
        /// Determine standing height using a small FOOTPRINT, not only a center ray.
        /// A center ray through the crack between two mesh hexes would otherwise
        /// fall back to floor level even while both feet are supported by tiles.
        /// </summary>
        private float SurfaceHeight(float x, float z, bool useSeamGrace = true)
        {
            float groundY = arenaGround != null ? arenaGround.position.y : transform.position.y;
            float rayOriginY = Mathf.Max(8f, transform.position.y + 5f);
            float r = footSupportRadius;
            float d = r * 0.70710678f;
            float top = float.NegativeInfinity;
            bool supported = false;

            // Center, four cardinal directions, four diagonals. All samples
            // must hit an actual hex surface (board or reserve bench).
            supported |= TrySurfaceSample(x, z, rayOriginY, ref top);
            supported |= TrySurfaceSample(x + r, z, rayOriginY, ref top);
            supported |= TrySurfaceSample(x - r, z, rayOriginY, ref top);
            supported |= TrySurfaceSample(x, z + r, rayOriginY, ref top);
            supported |= TrySurfaceSample(x, z - r, rayOriginY, ref top);
            supported |= TrySurfaceSample(x + d, z + d, rayOriginY, ref top);
            supported |= TrySurfaceSample(x + d, z - d, rayOriginY, ref top);
            supported |= TrySurfaceSample(x - d, z + d, rayOriginY, ref top);
            supported |= TrySurfaceSample(x - d, z - d, rayOriginY, ref top);

            if (supported)
            {
                float supportedHeight = Mathf.Max(groundY, top) + GroundOffset + footHeightAdjustment;
                // Destination preview is not a real step: do not poison the
                // moving character's seam history with a distant mouse click.
                if (useSeamGrace)
                {
                    lastSupportedY = supportedHeight;
                    lastSupportedTime = Time.time;
                }
                return supportedHeight;
            }

            // A very brief seam grace period covers numerical mesh cracks;
            // once off the board, Vela still returns to normal ground height.
            if (useSeamGrace && Time.time - lastSupportedTime <= seamGraceSeconds)
                return lastSupportedY;

            return groundY + GroundOffset + footHeightAdjustment;
        }

        private bool TrySurfaceSample(float x, float z, float startY, ref float top)
        {
            Ray ray = new Ray(new Vector3(x, startY, z), Vector3.down);
            int count = Physics.RaycastNonAlloc(ray, supportHits, startY + 6f, ~0,
                QueryTriggerInteraction.Ignore);
            bool hitTile = false;
            for (int i = 0; i < count; i++)
            {
                if (!IsWalkableSurface(supportHits[i].collider)) continue;
                top = Mathf.Max(top, supportHits[i].point.y);
                hitTile = true;
            }
            return hitTile;
        }

        private void SelectFacing(Vector3 delta)
        {
            if (targetCamera == null) return;

            Vector3 cameraRight = Vector3.ProjectOnPlane(targetCamera.transform.right, Vector3.up).normalized;
            Vector3 cameraUp = Vector3.ProjectOnPlane(targetCamera.transform.up, Vector3.up).normalized;
            float horizontal = Vector3.Dot(delta, cameraRight);
            float vertical = Vector3.Dot(delta, cameraUp);

            if (Mathf.Abs(horizontal) > Mathf.Abs(vertical))
                facing = horizontal > 0 ? Facing.Right : Facing.Left;
            else
                facing = vertical > 0 ? Facing.Back : Facing.Front;
        }

        private void ApplySprite()
        {
            if (trainerSprite == null) return;
            Sprite[] frames;
            switch (facing)
            {
                case Facing.Back: frames = backFrames; break;
                case Facing.Left: frames = leftFrames; break;
                case Facing.Right: frames = rightFrames; break;
                default: frames = frontFrames; break;
            }

            if (frames == null || frames.Length == 0) return;
            // 0 -> 1 -> 0 -> 2: natural alternation of steps and idle pose.
            int phase = isMoving ? Mathf.FloorToInt(animationTime * framesPerSecond) % 4 : 0;
            int index = phase == 3 ? 2 : phase == 2 ? 0 : phase;
            index = Mathf.Clamp(index, 0, frames.Length - 1);
            if (frames[index] != null) trainerSprite.sprite = frames[index];
        }
    }
}
