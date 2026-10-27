using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace MonsterTactics
{
    /// <summary>
    /// Three appearances for the SINGLE local trainer. No extra playable objects,
    /// cameras, input handlers, or changes to the tile-selection system.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TrainerCharacterSwitcher : MonoBehaviour
    {
        [Serializable]
        public sealed class TrainerAppearance
        {
            public string displayName;
            public Sprite[] front;
            public Sprite[] back;
            public Sprite[] left;
            public Sprite[] right;
        }

        [SerializeField] private TrainerMovement movement;
        [SerializeField] private TrainerAppearance[] appearances;
        [SerializeField] private int selectedIndex;
        [SerializeField] private bool showPicker = true;

        /// <summary>The active visual appearance, indexed from zero.</summary>
        public int SelectedIndex => selectedIndex;

        /// <summary>Called from the Unity editor installation menu.</summary>
        public void Configure(TrainerMovement target, TrainerAppearance[] options, int defaultIndex)
        {
            movement = target;
            appearances = options;
            selectedIndex = 0;
            SelectTrainer(defaultIndex);
        }

        /// <summary>Safe to call while the trainer is walking.</summary>
        public void SelectTrainer(int index)
        {
            if (appearances == null || index < 0 || index >= appearances.Length)
                return;

            if (movement == null)
                movement = GetComponent<TrainerMovement>();

            selectedIndex = index;
            TrainerAppearance entry = appearances[index];
            if (movement != null && entry != null)
                movement.SetAppearance(entry.front, entry.back, entry.left, entry.right);
        }

        private void Start()
        {
            SelectTrainer(selectedIndex);
        }

        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.digit1Key.wasPressedThisFrame) SelectTrainer(0);
            if (keyboard.digit2Key.wasPressedThisFrame) SelectTrainer(1);
            if (keyboard.digit3Key.wasPressedThisFrame) SelectTrainer(2);
            if (keyboard.f1Key.wasPressedThisFrame) showPicker = !showPicker;
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Alpha1)) SelectTrainer(0);
            if (Input.GetKeyDown(KeyCode.Alpha2)) SelectTrainer(1);
            if (Input.GetKeyDown(KeyCode.Alpha3)) SelectTrainer(2);
            if (Input.GetKeyDown(KeyCode.F1)) showPicker = !showPicker;
#endif
        }

        // Temporary prototyping UI. This can be replaced with Unity UI Toolkit later.
        private void OnGUI()
        {
            if (!showPicker || appearances == null || appearances.Length == 0)
                return;

            const float margin = 14f;
            const float width = 332f;
            const float height = 71f;
            float y = Mathf.Max(margin, Screen.height - height - margin);

            GUI.Box(new Rect(margin, y, width, height), "TRAINER  ·  1 / 2 / 3 to switch  ·  F1 to hide");
            float buttonWidth = (width - 24f) / 3f;
            for (int i = 0; i < appearances.Length && i < 3; i++)
            {
                bool active = i == selectedIndex;
                Color previous = GUI.color;
                GUI.color = active ? new Color(0.65f, 1f, 0.78f) : Color.white;
                string name = appearances[i] != null ? appearances[i].displayName : "Trainer";
                if (GUI.Button(new Rect(margin + 8f + i * buttonWidth, y + 30f,
                    buttonWidth - 4f, 29f), (i + 1) + "  " + name))
                    SelectTrainer(i);
                GUI.color = previous;
            }
        }
    }
}
