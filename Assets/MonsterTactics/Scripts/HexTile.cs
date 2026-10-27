using UnityEngine;

namespace MonsterTactics
{
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class HexTile : MonoBehaviour
    {
        [SerializeField] private int column;
        [SerializeField] private int row;
        [SerializeField] private bool playerSide;
        [SerializeField] private Color originalColor = Color.white;

        private MeshRenderer tileRenderer;
        private MaterialPropertyBlock propertyBlock;

        public int Column => column;
        public int Row => row;
        public bool IsPlayerSide => playerSide;

        // Called by the editor generator, not needed during normal play.
        public void Configure(int newColumn, int newRow, bool newPlayerSide, Color color)
        {
            column = newColumn;
            row = newRow;
            playerSide = newPlayerSide;
            originalColor = color;
        }

        public void SetSelected(bool selected)
        {
            if (tileRenderer == null)
            {
                tileRenderer = GetComponent<MeshRenderer>();
            }

            if (propertyBlock == null)
            {
                propertyBlock = new MaterialPropertyBlock();
            }

            Color displayColor = selected
                ? new Color(1f, 0.82f, 0.32f, 1f)
                : originalColor;

            tileRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_BaseColor", displayColor); // URP Lit
            propertyBlock.SetColor("_Color", displayColor);     // Standard fallback
            tileRenderer.SetPropertyBlock(propertyBlock);
        }
    }
}
