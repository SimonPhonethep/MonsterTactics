using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace MonsterTactics
{
    /// <summary>
    /// U5.2.1: click to inspect, hold to pick up a deployed unit.
    /// Canvas drag ghost follows cursor, original remains in its hex until drop.
    /// Board is locked while combat runs. Reserve/shop handled by U5 manager.
    /// Right mouse button is never consumed: the trainer's U2 movement still works.
    /// U1 TileSelection is not modified and continues to handle empty tile clicks.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MonsterPlacementController : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private MonsterUnit[] monster = new MonsterUnit[0];
        [SerializeField, Min(1f)] private float dragThresholdPixels = 9f;

        private MonsterTacticsU5ShopManager shopManager;
        private MonsterUnit selected;
        private MonsterUnit pressed;
        private Vector2 pressPosition;
        private bool dragging;
        private SpriteRenderer fadedRenderer;
        private Color fadedOriginalColor;
        private string statusMessage = "Click a Monster to inspect; drag it between green hexes.";
        private float statusUntil;
        private GUIStyle labelStyle;
        private GUIStyle nameStyle;
        private GUIStyle inspectorStyle;
        [SerializeField] private bool showLegacyTopBar = false;
        [SerializeField] private bool showLegacyCompactInspector = false;

        public void Configure(Camera camera, MonsterUnit[] placedMonster)
        {
            targetCamera = camera;
            monster = placedMonster;
        }

        private void Awake()
        {
            if (targetCamera == null) targetCamera = Camera.main;
            shopManager = FindAnyObjectByType<MonsterTacticsU5ShopManager>();
            if (monster == null || monster.Length == 0)
                monster = GetComponentsInChildren<MonsterUnit>();
        }

        private void Update()
        {
            if (targetCamera == null) targetCamera = Camera.main;
            if (targetCamera == null) return;

            ReadPointer(out Vector2 pointer, out bool leftDown, out bool leftHeld, out bool leftUp);
            if (shopManager == null) shopManager = FindAnyObjectByType<MonsterTacticsU5ShopManager>();
            if (shopManager != null && !shopManager.CanManageBoard)
            {
                EndDragVisual();
                pressed = null;
                dragging = false;
                shopManager.ReportBoardDrag(null, false, pointer);
                return;
            }
            if (shopManager != null && (shopManager.IsOverBenchArea(pointer) || shopManager.IsOverShopArea(pointer)))
            {
                // A world drag must still be allowed to FINISH over the bench/shop.
                // Only clicks starting inside the UI are ignored by board selection.
                if (leftDown || (!dragging && pressed == null)) return;
            }
            if (leftDown)
            {
                pressPosition = pointer;
                pressed = FindMonsterAt(pointer);
                SelectMonster(pressed);
            }

            if (pressed != null && leftHeld)
            {
                if (!dragging && Vector2.Distance(pointer, pressPosition) >= dragThresholdPixels)
                {
                    dragging = true;
                    StartDragVisual(pressed);
                }
                // Keep the authoritative board unit at its original tile until release.
                // U5.2 Canvas shows the cursor-follow sprite; no projection off-board.
                if (shopManager != null)
                    shopManager.ReportBoardDrag(pressed, dragging, pointer);
            }

            if (leftUp && pressed != null)
            {
                // Restore the sprite before selling (Unity may destroy the unit).
                EndDragVisual();
                if (dragging) FinishDrag(pointer);
                pressed = null;
                dragging = false;
                if (shopManager != null) shopManager.ReportBoardDrag(null, false, pointer);
            }
        }

        private void SelectMonster(MonsterUnit unit)
        {
            if (selected == unit) return;
            if (selected != null) selected.SetSelected(false);
            selected = unit;
            if (selected != null) selected.SetSelected(true);
            if (shopManager != null) shopManager.SelectFromBoard(selected);
        }

        private MonsterUnit FindMonsterAt(Vector2 pointer)
        {
            Ray ray = targetCamera.ScreenPointToRay(pointer);
            RaycastHit[] hits = Physics.RaycastAll(ray, 300f, ~0, QueryTriggerInteraction.Ignore);
            MonsterUnit nearest = null;
            float distance = float.PositiveInfinity;
            foreach (RaycastHit hit in hits)
            {
                MonsterUnit unit = hit.collider.GetComponentInParent<MonsterUnit>();
                // PvE preview Monster live under CombatDirector, not our team.
                // They may be inspected, but must never be dragged or sold.
                if (unit == null || !unit.transform.IsChildOf(transform) || hit.distance >= distance)
                    continue;
                nearest = unit;
                distance = hit.distance;
            }
            return nearest;
        }

        private HexTile FindTileAt(Vector2 pointer)
        {
            Ray ray = targetCamera.ScreenPointToRay(pointer);
            RaycastHit[] hits = Physics.RaycastAll(ray, 300f, ~0, QueryTriggerInteraction.Ignore);
            HexTile nearest = null;
            float distance = float.PositiveInfinity;
            foreach (RaycastHit hit in hits)
            {
                HexTile tile = hit.collider.GetComponent<HexTile>();
                if (tile == null || hit.distance >= distance) continue;
                nearest = tile;
                distance = hit.distance;
            }
            return nearest;
        }

        private void StartDragVisual(MonsterUnit unit)
        {
            EndDragVisual();
            if (unit == null) return;
            fadedRenderer = unit.GetComponentInChildren<SpriteRenderer>(true);
            if (fadedRenderer == null) return;
            fadedOriginalColor = fadedRenderer.color;
            Color faint = fadedOriginalColor;
            faint.a = .30f;
            fadedRenderer.color = faint;
        }

        private void EndDragVisual()
        {
            if (fadedRenderer != null) fadedRenderer.color = fadedOriginalColor;
            fadedRenderer = null;
        }

        private void OnDisable()
        {
            EndDragVisual();
            pressed = null;
            dragging = false;
            if (shopManager != null) shopManager.ReportBoardDrag(null, false, Vector2.zero);
        }

        private void FinishDrag(Vector2 pointer)
        {
            HexTile destination = FindTileAt(pointer);
            HexTile original = pressed.OccupiedTile;
            if (original == null) return;
            // Selling wins over world physics/colliders behind the bottom HUD.
            if (shopManager != null && shopManager.IsOverSellArea(pointer))
            {
                if (shopManager.TrySell(pressed)) selected = null;
                else pressed.PlaceOnTile(original);
                return;
            }
            if (shopManager != null && shopManager.IsOverBenchArea(pointer))
            {
                int benchSlot = shopManager.GetBenchSlotAtScreen(pointer);
                if (shopManager.TryBenchAtSlot(pressed, benchSlot))
                    selected = null;
                else
                    pressed.PlaceOnTile(original);
                return;
            }

            if (destination == null || !destination.IsPlayerSide)
            {
                pressed.PlaceOnTile(original);
                Report("Place your Monster on a GREEN (friendly) tile.");
                return;
            }

            MonsterUnit occupant = null;
            foreach (MonsterUnit other in GetComponentsInChildren<MonsterUnit>(true))
            {
                if (other != null && other.gameObject.activeSelf && other != pressed && other.OccupiedTile == destination)
                {
                    occupant = other;
                    break;
                }
            }

            if (occupant != null) occupant.PlaceOnTile(original);
            pressed.PlaceOnTile(destination);
            Report(occupant == null
                ? pressed.MonsterName + " moved to (" + destination.Column + ", " + destination.Row + ")."
                : pressed.MonsterName + " swapped with " + occupant.MonsterName + ".");
        }

        private void Report(string message)
        {
            statusMessage = message;
            statusUntil = Time.unscaledTime + 4f;
            Debug.Log("MonsterTactics U3: " + message);
        }

        private static void ReadPointer(
            out Vector2 pointer, out bool down, out bool held, out bool up)
        {
#if ENABLE_INPUT_SYSTEM
            Mouse mouse = Mouse.current;
            pointer = mouse != null ? mouse.position.ReadValue() : Vector2.zero;
            down = mouse != null && mouse.leftButton.wasPressedThisFrame;
            held = mouse != null && mouse.leftButton.isPressed;
            up = mouse != null && mouse.leftButton.wasReleasedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            pointer = Input.mousePosition;
            down = Input.GetMouseButtonDown(0);
            held = Input.GetMouseButton(0);
            up = Input.GetMouseButtonUp(0);
#else
            pointer = Vector2.zero;
            down = false;
            held = false;
            up = false;
#endif
        }

        private void InitializeStyles()
        {
            if (labelStyle != null) return;
            labelStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                fontStyle = FontStyle.Bold
            };
            labelStyle.normal.textColor = Color.white;
            nameStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold
            };
            nameStyle.normal.textColor = new Color(0.90f, 0.98f, 1f);
            inspectorStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                wordWrap = true
            };
            inspectorStyle.normal.textColor = Color.white;
        }

        private void OnGUI()
        {
            if (targetCamera == null) return;
            InitializeStyles();

            // U5.2.2: only planning owns placement nameplates. During combat/results,
            // U4 renders the live fighter HP/mana labels, otherwise every unit draws twice.
            if (shopManager == null || shopManager.IsPlanning)
            {
                var labels = new List<MonsterTacticsNameplates.Entry>();
                foreach (MonsterUnit unit in GetComponentsInChildren<MonsterUnit>(true))
                {
                    if (unit == null || !unit.gameObject.activeInHierarchy || unit.OccupiedTile == null) continue;
                    labels.Add(new MonsterTacticsNameplates.Entry(unit, unit.MonsterName,
                        unit.HealthFraction, 0f, 0f, false, false, unit.IsSelected));
                }
                MonsterTacticsNameplates.DrawAll(targetCamera, labels);
            }

            if (showLegacyTopBar)
            {
                GUI.Box(new Rect(10f, 10f, 326f, 45f), GUIContent.none);
                GUI.Label(new Rect(19f, 12f, 310f, 21f), "MONSTERTACTICS · U3  —  POKÉMON PLACEMENT", nameStyle);
                GUI.Label(new Rect(19f, 33f, 312f, 17f), "Left-click inspect · Drag green tiles · Right-click trainer", inspectorStyle);
            }

            if (showLegacyCompactInspector && selected != null)
            {
                float x = Mathf.Max(8f, Screen.width - 246f);
                GUI.Box(new Rect(x, 12f, 235f, 133f), GUIContent.none);
                GUI.Label(new Rect(x + 12f, 17f, 210f, 20f), selected.MonsterName, nameStyle);
                GUI.Label(new Rect(x + 12f, 42f, 210f, 80f),
                    selected.MonsterType + "  ·  " + selected.MonsterRole + "\n" +
                    "HP  " + selected.CurrentHP + " / " + selected.MaximumHP + "\n" +
                    "Tile  (" + selected.OccupiedTile.Column + ", " + selected.OccupiedTile.Row + ")\n" +
                    "Drag to move or swap", inspectorStyle);
            }

            if (Time.unscaledTime < statusUntil)
            {
                float w = Mathf.Min(480f, Screen.width - 20f);
                GUI.Box(new Rect((Screen.width - w) * 0.5f, 10f, w, 27f), statusMessage);
            }
        }

        private static void DrawSolid(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }
    }
}
