using UnityEngine;

namespace MonsterTactics
{
    /// <summary>
    /// The visual/deployment representation of one Monster in U3.
    /// U4 can hook combat HP and animations into this component later.
    /// Does not control TrainerMovement or TileSelection.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MonsterUnit : MonoBehaviour
    {
        [SerializeField] private string monsterName = "Monster";
        [SerializeField] private string monsterType = "Normal";
        [SerializeField] private string monsterRole = "Fighter";
        [SerializeField, Min(1)] private int maximumHP = 100;
        [SerializeField] private int currentHP = 100;
        [SerializeField] private HexTile occupiedTile;
        [SerializeField] private SpriteRenderer spriteVisual;
        [SerializeField] private Camera arenaCamera;

        private Vector3 spriteRestPosition;
        private float extraSpriteYOffset;
        private float idlePhase;
        private bool selected;
        private bool dragging;
        // PvE visual customization: display-only. Combat tile, collider, HP and
        // movement continue using the unit root (never an enlarged hitbox).
        private bool hasEncounterVisual;
        private int temporaryCombatMaxHP;

        /// <summary>
        /// World-space anchor for HP/name/item badges. U6.8.3 lowers the badge stack by
        /// anchoring near the actual top of the visible sprite instead of using the old
        /// fixed +1.5 world-unit offset that floated labels too high.
        /// </summary>
        public Vector3 NameplateWorldPosition
        {
            get
            {
                float fallbackY = transform.position.y + 0.85f;
                if (spriteVisual == null || spriteVisual.sprite == null)
                    return new Vector3(transform.position.x, fallbackY, transform.position.z);

                float spriteTop = spriteVisual.bounds.max.y + (hasEncounterVisual ? 0.04f : 0.02f);
                float minimumTop = transform.position.y + (hasEncounterVisual ? 0.90f : 0.72f);
                return new Vector3(transform.position.x, Mathf.Max(minimumTop, spriteTop), transform.position.z);
            }
        }

        /// <summary>
        /// Align 96x96 bottom-anchored wild/boss art with its tile. Unlike changing
        /// the child Transform once, this updates the rest pose so LateUpdate's idle
        /// bob does not undo the position fix. Only call for temporary PvE clones.
        /// </summary>
        public void ConfigureEncounterVisual(float uniformScale, float feetClearance)
        {
            if (spriteVisual == null) return;
            // Large flying bosses (Dreadwing) need more headroom than the old 3.25 cap.
            // This remains a sprite-child-only scale; the combat collider is unchanged.
            uniformScale = Mathf.Clamp(uniformScale, 1f, 4.25f);
            feetClearance = Mathf.Clamp(feetClearance, 0.015f, 0.30f);
            spriteVisual.transform.localScale = Vector3.one * uniformScale;
            // Imported wild sprites use a centered pivot on a 96px canvas and are
            // bottom-aligned in that canvas (93/96). At 96 pixels/unit, the sprite
            // bottom is ~0.5 world units below its center per 1x visual scale.
            spriteRestPosition = new Vector3(0f, uniformScale * .5f + feetClearance, 0f);
            spriteVisual.transform.localPosition = spriteRestPosition;
            hasEncounterVisual = true;
        }


        // U6.8.5: precise nameplate placement follows the actual visual sprite.
        public SpriteRenderer NameplateSpriteRenderer => spriteVisual;
        public string MonsterName => monsterName;
        public string MonsterType => monsterType;
        public string MonsterRole => monsterRole;
        public int MaximumHP => temporaryCombatMaxHP > 0 ? temporaryCombatMaxHP : maximumHP;
        public void SetTemporaryCombatMaximumHP(int value)
        {
            temporaryCombatMaxHP = Mathf.Max(1, value);
            currentHP = temporaryCombatMaxHP;
        }
        public void ClearTemporaryCombatMaximumHP()
        {
            temporaryCombatMaxHP = 0;
            currentHP = maximumHP;
        }
        public int CurrentHP => currentHP;
        public float HealthFraction => maximumHP > 0 ? (float)currentHP / maximumHP : 0f;
        public HexTile OccupiedTile => occupiedTile;
        public bool IsSelected => selected;

        public void Configure(
            string displayName, string type, string role, int hp,
            HexTile startingTile, SpriteRenderer spriteRenderer, Camera camera)
        {
            monsterName = displayName;
            monsterType = type;
            monsterRole = role;
            maximumHP = Mathf.Max(1, hp);
            currentHP = maximumHP;
            spriteVisual = spriteRenderer;
            arenaCamera = camera;
            if (spriteVisual != null)
            {
                extraSpriteYOffset = RecommendedSpriteYOffset(monsterName);
                spriteRestPosition = spriteVisual.transform.localPosition + Vector3.up * extraSpriteYOffset;
            }
            idlePhase = transform.GetSiblingIndex() * 0.85f;
            PlaceOnTile(startingTile);
        }

        public void PlaceOnTile(HexTile tile)
        {
            if (tile == null) return;
            occupiedTile = tile;
            var tileCollider = tile.GetComponent<Collider>();
            float top = tileCollider != null ? tileCollider.bounds.max.y : tile.transform.position.y + 0.18f;
            transform.position = new Vector3(tile.transform.position.x, top + 0.028f, tile.transform.position.z);
            dragging = false;
        }

        /// <summary>Clear board occupancy before deactivating a benched or sold Monster.</summary>
        public void SetBenched()
        {
            occupiedTile = null;
            dragging = false;
            SetSelected(false);
            gameObject.SetActive(false);
        }

        public void MovePreviewTo(Vector3 worldPosition)
        {
            dragging = true;
            transform.position = worldPosition;
        }

        public void SetSelected(bool state)
        {
            selected = state;
            if (spriteVisual != null)
                spriteVisual.color = state ? new Color(1f, 0.97f, 0.82f, 1f) : Color.white;
        }

        public void SetHealth(int hp)
        {
            currentHP = Mathf.Clamp(hp, 0, MaximumHP);
        }

        private void Awake()
        {
            if (arenaCamera == null) arenaCamera = Camera.main;
            if (spriteVisual != null)
            {
                extraSpriteYOffset = RecommendedSpriteYOffset(monsterName);
                spriteRestPosition = spriteVisual.transform.localPosition + Vector3.up * extraSpriteYOffset;
            }
        }


        private static float RecommendedSpriteYOffset(string displayName)
        {
            string key = (displayName ?? string.Empty).Trim().ToLowerInvariant();
            return key switch
            {
                "mosslet" => -0.08f,
                "ripplet" => -0.03f,
                "mistrip" => -0.025f,
                "dunebit" => -0.02f,
                _ => 0f
            };
        }

        private void LateUpdate()
        {
            if (spriteVisual == null) return;
            if (arenaCamera == null) arenaCamera = Camera.main;
            if (arenaCamera != null) spriteVisual.transform.rotation = arenaCamera.transform.rotation;

            float bob = dragging ? 0.05f : 0.010f * Mathf.Sin(Time.time * 2.4f + idlePhase);
            spriteVisual.transform.localPosition = spriteRestPosition + Vector3.up * bob;
        }
    }
}
