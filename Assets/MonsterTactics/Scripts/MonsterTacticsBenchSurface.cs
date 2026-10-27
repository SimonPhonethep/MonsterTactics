using UnityEngine;

namespace MonsterTactics
{
    /// <summary>
    /// Marks a U5 reserve bench hex as a walkable surface for TrainerMovement.
    /// Does not add a HexTile component or participate in Monster combat.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MonsterTacticsBenchSurface : MonoBehaviour
    {
    }
}
