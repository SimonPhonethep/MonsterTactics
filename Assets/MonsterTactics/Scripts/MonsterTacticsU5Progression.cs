using UnityEngine;

namespace MonsterTactics
{
    /// <summary>Per-copy evolution progress and accumulated shop cost (also cloned by combat enemies).</summary>
    [DisallowMultipleComponent]
    public sealed class MonsterTacticsU5Progression : MonoBehaviour
    {
        [SerializeField] private string familyName;
        [SerializeField, Range(1, 3)] private int stage = 1;
        [SerializeField, Min(0)] private int investedGold = 1;
        public string FamilyName => familyName;
        public int Stage => stage;
        public int InvestedGold => investedGold;
        public void Configure(string family, int nextStage, int invested)
        {
            familyName = family;
            stage = Mathf.Clamp(nextStage, 1, 3);
            investedGold = Mathf.Max(0, invested);
        }
    }
}
