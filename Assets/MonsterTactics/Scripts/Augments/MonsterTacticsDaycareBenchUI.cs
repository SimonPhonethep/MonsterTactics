using UnityEngine;

namespace MonsterTactics
{
    /// <summary>U6.9.2c selectable Daycare bench hex UI; does not replace
    /// existing world-hex bench or drag/drop placement.</summary>
    [DisallowMultipleComponent]
    public sealed class MonsterTacticsDaycareBenchUI : MonoBehaviour
    {
        private MonsterTacticsAugmentManager augment;
        private MonsterTacticsU5ShopManager shop;
        private bool expanded;
        private void Awake()
        {
            augment = GetComponent<MonsterTacticsAugmentManager>();
            shop = GetComponent<MonsterTacticsU5ShopManager>();
        }
        private void OnGUI()
        {
            if (augment == null || shop == null || !augment.HasDaycare ||
                !shop.IsShopReady) return;
            float width = Mathf.Min(325f, Screen.width * .33f);
            float x = Screen.width - width - 14f;
            float y = Mathf.Clamp(Screen.height * .32f, 110f, Screen.height - 175f);
            GUILayout.BeginArea(new Rect(x, y, width, expanded ? 148f : 44f),
                GUI.skin.box);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(expanded ? "Hide Daycare" : "Daycare Hex", GUILayout.Width(117f)))
                expanded = !expanded;
            GUILayout.Label((augment.DaycareProgress) + "/" + augment.DaycareRoundTarget + " | pending " +
                augment.DaycarePendingCopies);
            GUILayout.EndHorizontal();
            if (expanded)
            {
                GUILayout.Label("Choose a reserve hex. Place a Monster on it.");
                GUILayout.BeginHorizontal();
                for (int i = 0; i < 9; i++)
                {
                    bool chosen = augment.DaycareSlot == i;
                    string label = chosen ? "[" + (i + 1) + "]" : (i + 1).ToString();
                    GUI.enabled = shop.IsPlanning && !shop.AugmentSelectionOpen;
                    if (GUILayout.Button(label, GUILayout.Height(27f))) augment.SetDaycareSlot(i);
                }
                GUI.enabled = true;
                GUILayout.EndHorizontal();
                string family = shop.BenchFamilyAtSlot(augment.DaycareSlot);
                GUILayout.Label(string.IsNullOrEmpty(family) ? "Selected hex is empty" :
                    "Training: " + family);
            }
            GUILayout.EndArea();
        }
    }
}
