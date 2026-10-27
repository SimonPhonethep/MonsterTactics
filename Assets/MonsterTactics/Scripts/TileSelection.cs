using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace MonsterTactics
{
    public sealed class TileSelection : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        private HexTile selectedTile;

        private void Awake()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }
        }

        private void Update()
        {
            Vector2 pointerPosition;
            bool clicked;

#if ENABLE_INPUT_SYSTEM
            Mouse mouse = Mouse.current;
            clicked = mouse != null && mouse.leftButton.wasPressedThisFrame;
            pointerPosition = mouse != null ? mouse.position.ReadValue() : Vector2.zero;
#elif ENABLE_LEGACY_INPUT_MANAGER
            clicked = Input.GetMouseButtonDown(0);
            pointerPosition = Input.mousePosition;
#else
            clicked = false;
            pointerPosition = Vector2.zero;
#endif

            if (!clicked || targetCamera == null)
            {
                return;
            }

            Ray ray = targetCamera.ScreenPointToRay(pointerPosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, 250f))
            {
                return;
            }

            HexTile clickedTile = hit.collider.GetComponent<HexTile>();
            if (clickedTile == null || clickedTile == selectedTile)
            {
                return;
            }

            if (selectedTile != null)
            {
                selectedTile.SetSelected(false);
            }

            selectedTile = clickedTile;
            selectedTile.SetSelected(true);
            Debug.Log($"MonsterTactics: selected tile ({selectedTile.Column}, {selectedTile.Row}) " +
                      (selectedTile.IsPlayerSide ? "[player]" : "[enemy]"));
        }
    }
}
