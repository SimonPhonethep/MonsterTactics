using System;
using System.Collections.Generic;
using UnityEngine;

namespace MonsterTactics
{
    /// <summary>
    /// U6.9.4: Anvils occupy the NINE Monster reserve bench slots with Monster.
    /// Drag an anvil onto shop/sell area to choose one of four rewards; reward goes to
    /// the separate Item Bench, and ONLY THEN is the anvil bench hex freed.
    /// </summary>
    public enum MonsterTacticsItemCapsuleKind { Parts, Gear }

    [DisallowMultipleComponent]
    public sealed class MonsterTacticsItemCapsules : MonoBehaviour
    {
        [Serializable]
        private sealed class Offer
        {
            public MonsterTacticsItemCapsuleKind Kind;
            public int BenchIndex;
            public MonsterTacticsItemKind[] Choices;
        }

        private const int ChoicesPerAnvil = 4;
        private const int MonsterBenchCapacity = 9;
        private readonly MonsterTacticsItemCapsuleKind?[] benchAnvils = new MonsterTacticsItemCapsuleKind?[MonsterBenchCapacity];
        private readonly Dictionary<MonsterTacticsItemCapsuleKind, Sprite> sprites =
            new Dictionary<MonsterTacticsItemCapsuleKind, Sprite>();
        private Offer activeOffer;
        private MonsterTacticsU5ShopManager shop;
        private MonsterTacticsItemInventory inventory;
        private MonsterTacticsAugmentManager augment;
        private bool minimized;
        private GUIStyle title, subtitle, itemName, itemStats, button, small;

        public int PendingCount => BenchAnvilCount;
        public int BenchAnvilCount
        {
            get
            {
                int total = 0;
                foreach (var anvil in benchAnvils) if (anvil.HasValue) total++;
                return total;
            }
        }
        public bool HasBenchAnvil(int index) => index >= 0 && index < MonsterBenchCapacity &&
            benchAnvils[index].HasValue;
        public bool PlaceAtSlot(int index, MonsterTacticsItemCapsuleKind kind)
        {
            if (index < 0 || index >= MonsterBenchCapacity || HasBenchAnvil(index)) return false;
            benchAnvils[index] = kind;
            return true;
        }
        public Sprite BenchSprite(int index)
        {
            if (!HasBenchAnvil(index)) return null;
            var kind = benchAnvils[index].Value;
            Sprite sprite;
            if (sprites.TryGetValue(kind, out sprite)) return sprite;
            string name = kind == MonsterTacticsItemCapsuleKind.Parts ? "MonsterPartCapsule" : "MonsterGearCapsule";
            Texture2D tex = Resources.Load<Texture2D>("ItemIcons/" + name);
            if (tex == null) return null;
            // Bottom-center sprite pivot makes the device REST ON the hex, not sink into it.
            sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                new Vector2(.5f, .05f), tex.width);
            sprites[kind] = sprite;
            return sprite;
        }
        public string BenchDisplayName(int index) => HasBenchAnvil(index)
            ? DisplayName(benchAnvils[index].Value) : string.Empty;
        public bool IsShowing => activeOffer != null && !minimized;
        public MonsterTacticsItemCapsuleKind? CurrentKind => activeOffer != null
            ? (MonsterTacticsItemCapsuleKind?)activeOffer.Kind : null;
        public IReadOnlyList<MonsterTacticsItemKind> CurrentChoices => activeOffer != null
            ? (IReadOnlyList<MonsterTacticsItemKind>)activeOffer.Choices
            : Array.Empty<MonsterTacticsItemKind>();

        public static string DisplayName(MonsterTacticsItemCapsuleKind kind) =>
            kind == MonsterTacticsItemCapsuleKind.Parts ? "Crafting Part Capsule" : "Gear Capsule";

        private void Awake()
        {
            shop = GetComponent<MonsterTacticsU5ShopManager>();
            inventory = GetComponent<MonsterTacticsItemInventory>();
            augment = GetComponent<MonsterTacticsAugmentManager>();
        }

        /// <summary>Collect into the shared Monster bench, not the Item Bench.</summary>
        public bool GrantCapsule(MonsterTacticsItemCapsuleKind kind, string source = "capsule")
        {
            if (shop == null) shop = GetComponent<MonsterTacticsU5ShopManager>();
            if (shop == null || !shop.TryGrantBenchAnvil(kind)) return false;
            Debug.Log("[MonsterTactics U6.9.4] Added " + DisplayName(kind) +
                " to Monster reserve bench from " + source);
            return true;
        }

        /// <summary>Called only after dragging an anvil onto the shop/sell panel.</summary>
        public bool OpenCapsule(int benchIndex)
        {
            if (!HasBenchAnvil(benchIndex)) return false;
            if (augment == null) augment = GetComponent<MonsterTacticsAugmentManager>();
            if (augment != null && augment.IsSelectionOpen) return false;
            MonsterTacticsItemCapsuleKind kind = benchAnvils[benchIndex].Value;
            if (activeOffer != null)
            {
                // Existing roll must be claimed first; no reroll by closing/reopening.
                minimized = false;
                return true;
            }
            var pool = kind == MonsterTacticsItemCapsuleKind.Parts
                ? MonsterTacticsItemTypes.Components : MonsterTacticsItemTypes.Completed;
            var choices = new List<MonsterTacticsItemKind>();
            foreach (var choice in pool)
                if (MonsterTacticsItemTypes.IsAvailable(choice)) choices.Add(choice);
            if (choices.Count < ChoicesPerAnvil) return false;
            int wanted = ChoicesPerAnvil;
            for (int i = 0; i < wanted; i++)
            {
                int j = UnityEngine.Random.Range(i, choices.Count);
                var tmp = choices[i]; choices[i] = choices[j]; choices[j] = tmp;
            }
            var roll = new MonsterTacticsItemKind[wanted];
            for (int i = 0; i < wanted; i++) roll[i] = choices[i];
            activeOffer = new Offer { BenchIndex = benchIndex,
                Kind = kind, Choices = roll };
            minimized = false;
            return true;
        }

        /// <summary>Grant one item to Item Bench, then consume the anvil's Monster bench slot.</summary>
        public bool Choose(int choiceIndex)
        {
            if (activeOffer == null || inventory == null || shop == null) return false;
            var offer = activeOffer;
            if (choiceIndex < 0 || choiceIndex >= offer.Choices.Length) return false;
            MonsterTacticsItemKind chosen = offer.Choices[choiceIndex];
            if (!HasBenchAnvil(offer.BenchIndex) || benchAnvils[offer.BenchIndex].Value != offer.Kind)
                return false;
            if (!shop.GrantItemReward(chosen, "anvil:" + offer.Kind))
                return false; // Preserve anvil and choices if the item reward fails.
            benchAnvils[offer.BenchIndex] = null;
            activeOffer = null;
            minimized = false;
            shop.ReleaseBenchAnvil(offer.BenchIndex);
            Debug.Log("[MonsterTactics U6.9.4] Claimed " + MonsterTacticsItemTypes.Name(chosen) +
                      " from " + DisplayName(offer.Kind));
            return true;
        }

        public void CancelOpen() { activeOffer = null; minimized = false; }
        public void Minimize() { if (activeOffer != null) minimized = true; }
        public void Reopen() { if (activeOffer != null) minimized = false; }

        private static void Fill(Rect rect, Color color)
        {
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = old;
        }

        private void InitStyles()
        {
            if (title != null) return;
            title = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter, fontSize = 27, fontStyle = FontStyle.Bold
            };
            title.normal.textColor = Color.white;
            subtitle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter, fontSize = 13, wordWrap = true
            };
            subtitle.normal.textColor = new Color(.76f, .87f, .98f);
            itemName = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter, fontSize = 14,
                fontStyle = FontStyle.Bold, wordWrap = true
            };
            itemName.normal.textColor = Color.white;
            itemStats = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperCenter, fontSize = 11, wordWrap = true
            };
            itemStats.normal.textColor = new Color(.80f, .88f, .96f);
            button = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter, fontSize = 14, fontStyle = FontStyle.Bold
            };
            small = new GUIStyle(GUI.skin.button) { fontSize = 12 };
        }

        private void OnGUI()
        {
            if (activeOffer == null) return;
            if (!HasBenchAnvil(activeOffer.BenchIndex) ||
                benchAnvils[activeOffer.BenchIndex].Value != activeOffer.Kind)
            {
                CancelOpen();
                return;
            }
            if (augment == null) augment = GetComponent<MonsterTacticsAugmentManager>();
            // An ongoing augment offer owns the screen. Capsules keep their choices.
            if (augment != null && augment.IsSelectionOpen) return;

            int oldDepth = GUI.depth;
            Matrix4x4 oldMatrix = GUI.matrix;
            GUI.depth = -810; // Above the item bench; below augment selection (-900).
            try
            {
                InitStyles();
                if (minimized)
                {
                    Rect reminder = new Rect(Mathf.Max(8f, Screen.width - 298f), 10f, 290f, 43f);
                    Fill(reminder, new Color(.08f, .19f, .28f, .98f));
                    if (GUI.Button(new Rect(reminder.x+5f, reminder.y+5f, reminder.width-10f, 32f),
                        "OPEN ANVIL REWARD", small)) minimized = false;
                    return;
                }

                Fill(new Rect(0f, 0f, Screen.width, Screen.height),
                    new Color(.007f, .013f, .030f, .89f));
                float scale = Mathf.Min(1f, Mathf.Min(Screen.width / 1040f, Screen.height / 610f));
                scale = Mathf.Max(.42f, scale);
                GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
                float w = Screen.width / scale;
                float h = Screen.height / scale;
                float panelW = Mathf.Min(1000f, w - 24f);
                float panelH = 448f;
                Rect frame = new Rect((w-panelW)*.5f, (h-panelH)*.5f, panelW, panelH);
                Fill(frame, new Color(.045f, .091f, .154f, .98f));
                var offer = activeOffer;
                Color accent = offer.Kind == MonsterTacticsItemCapsuleKind.Parts
                    ? new Color(.32f, .67f, 1f) : new Color(1f, .76f, .27f);
                Fill(new Rect(frame.x, frame.y, panelW, 4f), accent);
                GUI.Label(new Rect(frame.x + 20f, frame.y + 13f, panelW - 40f, 44f),
                    DisplayName(offer.Kind).ToUpperInvariant(), title);
                GUI.Label(new Rect(frame.x + 24f, frame.y + 56f, panelW - 48f, 25f),
                    "Choose ONE " + (offer.Kind == MonsterTacticsItemCapsuleKind.Parts ? "component" : "completed item") +
                    " for your Item Bench  •  No rerolls", subtitle);

                const float gap = 12f;
                const float side = 18f;
                float cardW = (panelW - side*2f - gap*(offer.Choices.Length-1)) / offer.Choices.Length;
                float cardY = frame.y + 92f;
                float cardH = 280f;
                for (int i = 0; i < offer.Choices.Length; i++)
                {
                    MonsterTacticsItemKind item = offer.Choices[i];
                    Rect card = new Rect(frame.x + side + i*(cardW+gap), cardY, cardW, cardH);
                    Fill(card, new Color(.09f, .16f, .235f, 1f));
                    Fill(new Rect(card.x, card.y, card.width, 2f), accent);
                    Rect icon = new Rect(card.center.x - 37f, card.y + 12f, 74f, 74f);
                    MonsterTacticsItemArt.Draw(icon, item);
                    GUI.Label(new Rect(card.x+5f, card.y+93f, card.width-10f, 48f),
                        MonsterTacticsItemTypes.Name(item), itemName);
                    string stats = offer.Kind == MonsterTacticsItemCapsuleKind.Parts
                        ? MonsterTacticsItemTypes.BonusSummary(MonsterTacticsItemTypes.Bonus(item))
                        : MonsterTacticsItemTypes.BonusSummary(MonsterTacticsItemTypes.Bonus(item)) +
                          "\n" + MonsterTacticsItemPassiveCatalog.Effect(item);
                    GUI.Label(new Rect(card.x+10f, card.y+152f, card.width-20f, 72f), stats, itemStats);
                    if (GUI.Button(new Rect(card.x+13f, card.y+232f, card.width-26f, 37f),
                        "CHOOSE", button))
                    {
                        Choose(i);
                        break;
                    }
                }
                GUI.Label(new Rect(frame.x+17f, frame.y+383f, panelW-200f, 28f),
                    "Drag an anvil to the shop to open. Chosen reward goes to Item Bench.", subtitle);
                if (GUI.Button(new Rect(frame.xMax - 160f, frame.y+385f, 140f, 32f),
                    "CHOOSE LATER", small)) Minimize();
            }
            finally
            {
                GUI.matrix = oldMatrix;
                GUI.depth = oldDepth;
            }
        }
    }
}
