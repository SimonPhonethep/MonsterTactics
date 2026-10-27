using System;
using System.Collections.Generic;
using UnityEngine;

namespace MonsterTactics
{
    /// <summary>
    /// U6.8 — TFT-style inventory: hold, then RELEASE to craft, recipe panel,
    /// hover tooltips, safe reorder, and completion glow. Early release cancels.
    /// Uses IMGUI pixels, separate from the world hex bench.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MonsterTacticsItemInventory : MonoBehaviour
    {
        private const int ItemsPerPage = 10;
        private static readonly Color craftBlue = new Color(.19f, .65f, 1f, 1f);
        [SerializeField] private List<MonsterTacticsItemKind> inventory = new List<MonsterTacticsItemKind>();
        private MonsterTacticsU5ShopManager shop;
        private int page;
        private int draggingIndex = -1;
        private bool draggedFar;
        private Vector2 dragStart;
        private readonly MonsterTacticsCraftHoldGate craftHold = new MonsterTacticsCraftHoldGate();
        private float lastDragPageFlip;
        private int recipeSource = -1;
        private int craftFlashIndex = -1;
        private float craftFlashUntil;
        private string message = "Grant components via MonsterTactics > Tests > U6.7.1 Item Tester";
        private float messageUntil;
        private GUIStyle heading, detail, cell, small, notice, smallLabel;
        private readonly Color background = new Color(.055f,.090f,.142f,.98f);
        private static readonly Color trim = new Color(.37f,.58f,.77f,.95f);
        private const float Width = 178f;
        private const float Height = 435f;

        public int Count => inventory.Count;
        public IReadOnlyList<MonsterTacticsItemKind> Items => inventory;

        public void Bind(MonsterTacticsU5ShopManager owner) { shop = owner; }

        public void Grant(MonsterTacticsItemKind kind)
        {
            if (!MonsterTacticsItemTypes.IsAvailable(kind)) { SetMessage("Special items are paused for now."); return; }
            inventory.Add(kind);
            page = Mathf.Max(0,(inventory.Count - 1) / ItemsPerPage);
            SetMessage("Added " + MonsterTacticsItemTypes.Name(kind));
        }

        public void GrantAllComponents()
        {
            foreach (MonsterTacticsItemKind kind in MonsterTacticsItemTypes.Components) inventory.Add(kind);
            page = Mathf.Max(0,(inventory.Count - 1) / ItemsPerPage);
            SetMessage("8 components added (test inventory)");
        }

        public void AddReturned(IEnumerable<MonsterTacticsItemKind> returned)
        {
            if (returned == null) return;
            foreach (MonsterTacticsItemKind kind in returned)
                if (MonsterTacticsItemTypes.IsAvailable(kind)) inventory.Add(kind);
            // No drop-on-floor if the display is full; inventory pages are unlimited.
        }

        public bool TryEquip(int itemIndex, MonsterUnit unit)
        {
            if (itemIndex < 0 || itemIndex >= inventory.Count || shop == null ||
                !shop.CanEquipItemOn(unit)) return false;
            MonsterTacticsHeldItems held = unit.GetComponent<MonsterTacticsHeldItems>();
            if (held == null) held = unit.gameObject.AddComponent<MonsterTacticsHeldItems>();
            MonsterTacticsItemKind kind = inventory[itemIndex];
            if (!MonsterTacticsItemTypes.IsAvailable(kind)) { SetMessage("Special items are paused for now."); return false; }
            if (MonsterTacticsItemTypes.IsTool(kind))
            {
                // Unlike equipping normal items, consumable removal/reforging
                // is forbidden on an actively fighting deployed Monster.
                if (!shop.CanUseItemToolOn(unit))
                {
                    SetMessage("During combat, use Remover/Reforger on BENCHED Monster only.");
                    return false;
                }
                if (held.Items.Count == 0) { SetMessage("No items on " + unit.MonsterName); return false; }
                // Both tools are single-use. Reforger changes each item within its
                // own category, then returns everything to the item bench.
                MonsterTacticsItemKind[] removed = held.TakeAll();
                for (int i = 0; i < removed.Length; i++)
                    inventory.Add(kind == MonsterTacticsItemKind.Reforger
                        ? MonsterTacticsItemTypes.Reforge(removed[i]) : removed[i]);
                inventory.RemoveAt(itemIndex);
                SetMessage(MonsterTacticsItemTypes.Name(kind) + " used on " + unit.MonsterName);
            }
            else
            {
                // Capture the old item state before TryAdd (including auto-crafting
                // on the unit). The combat director applies the exact item delta.
                MonsterTacticsItemKind[] equippedBefore = new MonsterTacticsItemKind[held.Items.Count];
                for (int i = 0; i < equippedBefore.Length; i++)
                    equippedBefore[i] = held.Items[i];
                if (!held.TryAdd(kind))
                {
                    SetMessage(unit.MonsterName + " has no free item slot.");
                    return false;
                }
                shop.NotifyCombatItemEquipped(unit, equippedBefore);
                inventory.RemoveAt(itemIndex);
                SetMessage(MonsterTacticsItemTypes.Name(kind) + " equipped / crafted!");
            }
            page = Mathf.Clamp(page, 0, Mathf.Max(0, (inventory.Count - 1) / ItemsPerPage));
            CancelDrag();
            return true;
        }

        /// <summary>
        /// Atomically combines two inventory slots. Called only when the hold timer
        /// finishes (or by explicit tests/game logic); never on an early mouse release.
        /// </summary>
        public bool TryCombineBenchItems(int sourceIndex, int targetIndex, out MonsterTacticsItemKind result)
        {
            result = default(MonsterTacticsItemKind);
            if (sourceIndex < 0 || targetIndex < 0 || sourceIndex >= inventory.Count ||
                targetIndex >= inventory.Count || sourceIndex == targetIndex)
                return false;

            MonsterTacticsItemKind source = inventory[sourceIndex];
            MonsterTacticsItemKind target = inventory[targetIndex];
            if (!MonsterTacticsItemTypes.TryRecipe(source, target, out result)) return false;

            // Preserve destination position, adjusting if we removed an earlier slot.
            inventory.RemoveAt(sourceIndex);
            int resultIndex = targetIndex - (sourceIndex < targetIndex ? 1 : 0);
            inventory[resultIndex] = result;
            page = resultIndex / ItemsPerPage;
            craftFlashIndex = resultIndex;
            craftFlashUntil = Time.unscaledTime + .65f;
            recipeSource = -1;
            CancelDrag();
            SetMessage("Crafted " + MonsterTacticsItemTypes.Name(result) + " in item bench!");
            return true;
        }

        /// <summary>Reorder non-recipe items on the bench; never silently craft.</summary>
        public bool TryReorderBenchItems(int source, int target)
        {
            if (source < 0 || source >= inventory.Count || target < 0 ||
                target > inventory.Count || source == target) return false;
            MonsterTacticsItemKind result;
            if (target < inventory.Count && MonsterTacticsItemTypes.TryRecipe(
                inventory[source], inventory[target], out result)) return false;
            if (target == inventory.Count)
            {
                MonsterTacticsItemKind moving = inventory[source];
                inventory.RemoveAt(source);
                inventory.Add(moving);
                page = (inventory.Count - 1) / ItemsPerPage;
            }
            else
            {
                MonsterTacticsItemKind temp = inventory[target];
                inventory[target] = inventory[source];
                inventory[source] = temp;
            }
            SetMessage("Item order updated");
            CancelDrag();
            return true;
        }

        /// <summary>Alternative TFT recipe panel action. Requires both components in bench.</summary>
        public bool TryCraftFromRecipe(int sourceIndex, MonsterTacticsItemKind partner,
            out MonsterTacticsItemKind result)
        {
            result = default(MonsterTacticsItemKind);
            if (sourceIndex < 0 || sourceIndex >= inventory.Count ||
                !MonsterTacticsItemTypes.IsComponent(inventory[sourceIndex]) ||
                !MonsterTacticsItemTypes.IsComponent(partner)) return false;
            for (int i = 0; i < inventory.Count; i++)
                if (i != sourceIndex && inventory[i] == partner)
                    return TryCombineBenchItems(sourceIndex, i, out result);
            return false;
        }

        private int RawCellIndexAt(Rect panel, Vector2 pointer)
        {
            for (int i = 0; i < ItemsPerPage; i++)
                if (CellAt(panel, i).Contains(pointer)) return page * ItemsPerPage + i;
            return -1;
        }

        private int ItemIndexAt(Rect panel, Vector2 pointer)
        {
            int begin = page * ItemsPerPage;
            for (int i = 0; i < ItemsPerPage && begin + i < inventory.Count; i++)
                if (CellAt(panel, i).Contains(pointer)) return begin + i;
            return -1;
        }

        public void GrantTools()
        {
            Grant(MonsterTacticsItemKind.ItemRemover);
            Grant(MonsterTacticsItemKind.Reforger);
        }

        public void ClearTestInventory()
        {
            inventory.Clear();
            CancelDrag();
            page = 0;
            SetMessage("Inventory cleared");
        }

        private void SetMessage(string value)
        {
            message = value;
            messageUntil = Time.unscaledTime + 3.5f;
        }

        private Rect PanelRect()
        {
            // Left of the shop; world reserve hexes stay in the center.
            float maxHeight = Mathf.Max(240f, Screen.height - 225f);
            float h = Mathf.Min(Height, maxHeight);
            return new Rect(12f, Screen.height - h - 14f, Width, h);
        }

        private void PrepareStyles()
        {
            if (heading != null) return;
            heading = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft };
            heading.normal.textColor = new Color(1f,.82f,.35f);
            detail = new GUIStyle(GUI.skin.label) { fontSize = 11, wordWrap = true };
            detail.normal.textColor = new Color(.80f,.89f,.95f);
            cell = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter };
            cell.normal.textColor = Color.white;
            small = new GUIStyle(GUI.skin.button) { fontSize = 11 };
            notice = new GUIStyle(detail) { fontSize = 10, wordWrap = true };
            smallLabel = new GUIStyle(cell) { fontSize = 10, fontStyle = FontStyle.Bold };
            smallLabel.normal.textColor = new Color(.72f,.88f,1f);
        }

        private static void Fill(Rect r, Color c)
        {
            Color old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        private Rect CellAt(Rect r, int local)
        {
            float x = r.x + 12f + (local % 2) * 80f;
            float y = r.y + 45f + (local / 2) * 58f;
            return new Rect(x, y, 73f, 51f);
        }


        private void CancelDrag()
        {
            draggingIndex = -1;
            draggedFar = false;
            craftHold.Cancel();
        }

        // Compatible with Unity Player Settings: Input System Package (New),
        // Input Manager (Old), or Both. Never call the legacy API when disabled.
        private static bool LeftMouseHeld()
        {
#if ENABLE_INPUT_SYSTEM
            UnityEngine.InputSystem.Mouse mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null) return mouse.leftButton.isPressed;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return UnityEngine.Input.GetMouseButton(0);
#else
            return false;
#endif
        }

        // Handled before drawing, so a MouseUp event always cancels an unfinished
        // hold BEFORE the per-frame timer can attempt a combine.
        private void ProcessPointerEvent(Rect panel, Event ev)
        {
            // Layout/Used events do not always carry the cursor location.
            // Never advance or reset progress from those synthetic GUI events.
            EventType originalType = ev.type;
            if (originalType == EventType.MouseDown && ev.button == 1)
            {
                int clicked = ItemIndexAt(panel, ev.mousePosition);
                if (clicked >= 0 && MonsterTacticsItemTypes.IsComponent(inventory[clicked]))
                {
                    recipeSource = recipeSource == clicked ? -1 : clicked;
                    CancelDrag();
                    ev.Use();
                    return;
                }
                if (recipeSource >= 0) recipeSource = -1;
            }
            if (originalType == EventType.MouseDown && ev.button == 0)
            {
                int clickedIndex = ItemIndexAt(panel, ev.mousePosition);
                if (clickedIndex >= 0)
                {
                    draggingIndex = clickedIndex;
                    dragStart = ev.mousePosition;
                    draggedFar = false;
                    craftHold.Cancel();
                    lastDragPageFlip = Time.unscaledTime;
                    recipeSource = -1;
                    ev.Use();
                }
            }

            if (draggingIndex >= 0 && originalType == EventType.MouseDrag && ev.button == 0)
            {
                if (Vector2.Distance(ev.mousePosition, dragStart) > 7f)
                    draggedFar = true;
                ev.Use();
            }

            if (draggingIndex >= 0 && originalType == EventType.MouseUp && ev.button == 0)
            {
                if (draggedFar)
                {
                    int benchTarget = ItemIndexAt(panel, ev.mousePosition);
                    if (benchTarget >= 0 && benchTarget != draggingIndex)
                    {
                        MonsterTacticsItemKind recipe;
                        if (draggingIndex < inventory.Count &&
                            MonsterTacticsItemTypes.TryRecipe(inventory[draggingIndex],
                                inventory[benchTarget], out recipe))
                        {
                            // TFT-style hold + RELEASE: never craft until the
                            // timer is full and the mouse is released on target.
                            if (craftHold.Active && craftHold.SourceIndex == draggingIndex &&
                                craftHold.TargetIndex == benchTarget &&
                                craftHold.IsComplete(Time.unscaledTime))
                            {
                                if (!TryCombineBenchItems(draggingIndex, benchTarget, out recipe))
                                    SetMessage("Could not craft these components.");
                            }
                            else SetMessage("Canceled: hold until READY, then release.");
                        }
                        else TryReorderBenchItems(draggingIndex, benchTarget);
                    }
                    else if (panel.Contains(ev.mousePosition))
                    {
                        int emptyTarget = RawCellIndexAt(panel, ev.mousePosition);
                        if (emptyTarget >= inventory.Count)
                            TryReorderBenchItems(draggingIndex, inventory.Count);
                    }
                    else
                    {
                        // Equipping onto Monster is still immediate on drop.
                        Vector2 screenPoint = new Vector2(ev.mousePosition.x,
                            Screen.height - ev.mousePosition.y);
                        MonsterUnit target = shop != null ? shop.ItemDropTargetAt(screenPoint) : null;
                        if (target != null)
                        {
                            if (!TryEquip(draggingIndex, target))
                                SetMessage("Can't equip now, or the Monster's item slots are full.");
                        }
                        else SetMessage("Drop on a Monster or hold above a component to craft.");
                    }
                }
                CancelDrag();
                ev.Use();
                return;
            }

            if (originalType != EventType.Repaint && originalType != EventType.MouseDrag &&
                originalType != EventType.MouseMove)
                return;

            // Extra guard for releasing the mouse outside the Unity game view:
            // do not allow a stale hover to complete after the button is up.
            if (originalType == EventType.Repaint && draggingIndex >= 0 &&
                !LeftMouseHeld())
            {
                CancelDrag();
                return;
            }

            if (draggingIndex < 0 || draggingIndex >= inventory.Count || !draggedFar)
            {
                craftHold.Cancel();
                return;
            }

            int hoveredIndex = ItemIndexAt(panel, ev.mousePosition);
            MonsterTacticsItemKind crafted;
            bool valid = hoveredIndex >= 0 && hoveredIndex < inventory.Count &&
                hoveredIndex != draggingIndex &&
                MonsterTacticsItemTypes.TryRecipe(inventory[draggingIndex],
                    inventory[hoveredIndex], out crafted);
            if (!valid)
            {
                // Moving off the target, changing pages or hovering an invalid
                // recipe resets the timer to zero.
                craftHold.Cancel();
                return;
            }

            craftHold.UpdateHover(draggingIndex, hoveredIndex, Time.unscaledTime);
            // At 100% we show READY; the transaction happens on MouseUp only.
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused) CancelDrag();
        }

        private void OnDisable()
        {
            CancelDrag();
        }

        private void DrawRecipePanel(Rect bench, Event ev)
        {
            const float width = 254f;
            float x = bench.xMax + 10f;
            if (x + width > Screen.width - 8f) x = Mathf.Max(8f, bench.x - width - 10f);
            float height = 68f + MonsterTacticsItemTypes.Components.Length * 42f;
            Rect panel = new Rect(x, Mathf.Clamp(bench.y, 8f,
                Mathf.Max(8f, Screen.height - height - 8f)), width, height);
            Fill(panel, new Color(.055f, .09f, .15f, .985f));
            Fill(new Rect(panel.x, panel.y, panel.width, 2f), craftBlue);
            GUI.Label(new Rect(panel.x + 10f, panel.y + 6f, panel.width - 20f, 22f),
                "RECIPES - " + MonsterTacticsItemTypes.Name(inventory[recipeSource]), heading);
            GUI.Label(new Rect(panel.x + 9f, panel.y + 31f, panel.width - 18f, 20f),
                "Choose a recipe to craft instantly", detail);
            for (int i = 0; i < MonsterTacticsItemTypes.Components.Length; i++)
            {
                MonsterTacticsItemKind ingredient = MonsterTacticsItemTypes.Components[i];
                MonsterTacticsItemKind result;
                if (!MonsterTacticsItemTypes.TryRecipe(inventory[recipeSource], ingredient,
                    out result)) continue;
                Rect row = new Rect(panel.x + 8f, panel.y + 59f + i * 42f,
                    panel.width - 16f, 39f);
                Fill(row, new Color(.10f,.16f,.24f,.98f));
                MonsterTacticsItemArt.Draw(new Rect(row.x + 4f,row.y + 3f,33f,33f), result);
                GUI.Label(new Rect(row.x + 44f,row.y + 2f,row.width - 119f,35f),
                    MonsterTacticsItemTypes.Name(result), notice);
                bool available = false;
                for (int k = 0; k < inventory.Count; k++)
                    if (k != recipeSource && inventory[k] == ingredient) { available = true; break; }
                bool oldEnabled = GUI.enabled;
                GUI.enabled = available;
                if (GUI.Button(new Rect(row.xMax-71f,row.y+8f,67f,24f),
                    "CRAFT",small))
                {
                    MonsterTacticsItemKind crafted;
                    if (TryCraftFromRecipe(recipeSource,ingredient,out crafted))
                        SetMessage("Crafted " + MonsterTacticsItemTypes.Name(crafted));
                    else SetMessage("Second component is not available.");
                    GUI.enabled = oldEnabled;
                    return;
                }
                GUI.enabled = oldEnabled;
                if (row.Contains(ev.mousePosition))
                    MonsterTacticsItemTooltip.Draw(ev.mousePosition, result,
                        available ? "Craft from components in your bench" :
                        "Missing: " + MonsterTacticsItemTypes.Name(ingredient));
            }
        }

        private void OnGUI()
        {
            if (shop == null) shop = GetComponent<MonsterTacticsU5ShopManager>();
            if (shop == null || !shop.IsShopReady) return;
            PrepareStyles();
            int oldDepth = GUI.depth;
            GUI.depth = -210;
            Rect r = PanelRect();
            Event ev = Event.current;
            ProcessPointerEvent(r, ev);
            Fill(r, background);
            Fill(new Rect(r.x,r.y,r.width,2f),trim);
            GUI.Label(new Rect(r.x+9f,r.y+6f,r.width-18f,20f),"ITEM BENCH", heading);
            GUI.Label(new Rect(r.x+9f,r.y+25f,r.width-18f,17f),
                inventory.Count + " items  |  3 equipped slots", detail);
            MonsterTacticsItemKind? hoveredItem = null;
            string hoveredContext = null;
            int baseIndex = page * ItemsPerPage;
            int visible = Mathf.Min(ItemsPerPage, Mathf.Max(0, inventory.Count - baseIndex));
            for (int i = 0; i < ItemsPerPage; i++)
            {
                Rect slot = CellAt(r, i);
                Fill(slot, new Color(.11f,.16f,.22f,1f));
                if (i >= visible) continue;
                int index = baseIndex + i;
                MonsterTacticsItemKind kind = inventory[index];
                bool validDropPreview = craftHold.Active && craftHold.TargetIndex == index &&
                    draggingIndex >= 0 && draggingIndex < inventory.Count;
                MonsterTacticsItemKind previewKind = default(MonsterTacticsItemKind);
                if (validDropPreview)
                    MonsterTacticsItemTypes.TryRecipe(inventory[draggingIndex], kind, out previewKind);

                MonsterTacticsItemArt.Draw(new Rect(slot.x+18f, slot.y+1f, 37f, 37f), kind);
                if (craftFlashIndex == index && Time.unscaledTime < craftFlashUntil)
                {
                    float pulse = Mathf.Clamp01((craftFlashUntil - Time.unscaledTime) / .65f);
                    Color glow = new Color(.4f,.94f,1f, .3f + pulse * .7f);
                    Fill(new Rect(slot.x,slot.y,slot.width,3f),glow);
                    Fill(new Rect(slot.x,slot.yMax-3f,slot.width,3f),glow);
                    Fill(new Rect(slot.x,slot.y,3f,slot.height),glow);
                    Fill(new Rect(slot.xMax-3f,slot.y,3f,slot.height),glow);
                    float sparkle = 1f - pulse;
                    for (int particle = 0; particle < 4; particle++)
                    {
                        float px = slot.center.x + (particle < 2 ? -1f : 1f) *
                            (12f + sparkle * 15f);
                        float py = slot.center.y + (particle % 2 == 0 ? -1f : 1f) *
                            (7f + sparkle * 14f);
                        Fill(new Rect(px,py,3f,3f),new Color(.60f,.94f,1f,pulse));
                    }
                    GUI.Label(new Rect(slot.x+1f,slot.yMax-17f,slot.width-2f,16f),
                        "CRAFTED!", smallLabel);
                }
                if (validDropPreview)
                {
                    // Bar grows while the mouse is held, BEFORE crafting happens.
                    float percent = craftHold.Progress(Time.unscaledTime);
                    Fill(new Rect(slot.x+3f, slot.y+2f, slot.width-6f, 18f),
                        new Color(.035f, .11f, .21f, .94f));
                    GUI.Label(new Rect(slot.x+3f, slot.y+2f, slot.width-6f, 16f),
                        percent >= 1f ? "RELEASE" :
                            "HOLD " + Mathf.RoundToInt(percent * 100f) + "%", smallLabel);
                    Rect progress = new Rect(slot.x+5f, slot.yMax-8f, slot.width-10f, 5f);
                    Fill(progress, new Color(.04f,.09f,.15f));
                    Fill(new Rect(progress.x, progress.y,
                        progress.width * percent, progress.height), craftBlue);
                }
                if (validDropPreview)
                {
                    // A bright cyan frame + recipe preview while holding one
                    // component over another, before actually releasing it.
                    Fill(new Rect(slot.x,slot.y,slot.width,2f), craftBlue);
                    Fill(new Rect(slot.x,slot.yMax-2f,slot.width,2f), craftBlue);
                    Fill(new Rect(slot.x,slot.y,2f,slot.height), craftBlue);
                    Fill(new Rect(slot.xMax-2f,slot.y,2f,slot.height), craftBlue);
                }
                if (!validDropPreview)
                {
                    var shortStyle = new GUIStyle(cell) { fontSize = 9 };
                    GUI.Label(new Rect(slot.x+2f,slot.y+36f,slot.width-4f,14f),
                        MonsterTacticsItemTypes.ShortName(kind), shortStyle);
                }
                if (slot.Contains(Event.current.mousePosition))
                {
                    hoveredItem = validDropPreview ? previewKind : kind;
                    hoveredContext = validDropPreview ? "Release when READY to craft" :
                        MonsterTacticsItemTypes.IsComponent(kind) ? "Right-click for recipes" : null;
                }
            }
            // Secondary, wide progress bar in the free area below the item slots.
            if (craftHold.Active && draggingIndex >= 0 &&
                craftHold.SourceIndex == draggingIndex &&
                craftHold.TargetIndex < inventory.Count)
            {
                float percent = craftHold.Progress(Time.unscaledTime);
                MonsterTacticsItemKind result;
                if (MonsterTacticsItemTypes.TryRecipe(inventory[draggingIndex],
                    inventory[craftHold.TargetIndex], out result))
                {
                    GUI.Label(new Rect(r.x+9f, r.yMax-102f, r.width-18f, 17f),
                        percent >= 1f ? "READY: RELEASE TO CRAFT " + MonsterTacticsItemTypes.Name(result) :
                        "HOLD TO PREVIEW: " + MonsterTacticsItemTypes.Name(result), notice);
                    Rect bar = new Rect(r.x+9f, r.yMax-81f, r.width-18f, 6f);
                    Fill(bar, new Color(.05f,.10f,.16f,1f));
                    Fill(new Rect(bar.x, bar.y, bar.width*percent, bar.height), craftBlue);
                }
            }
            int lastPage = Mathf.Max(0,(inventory.Count-1)/ItemsPerPage);
            Rect previous = new Rect(r.x+8f,r.yMax-70f,32f,20f);
            Rect next = new Rect(r.xMax-40f,r.yMax-70f,32f,20f);
            if (GUI.Button(previous,"<",small)) page = Mathf.Max(0,page-1);
            if (GUI.Button(next,">",small)) page = Mathf.Min(lastPage,page+1);
            // Hold a dragged item over a page arrow to combine across pages.
            if (draggingIndex >= 0 && draggedFar &&
                Time.unscaledTime - lastDragPageFlip >= 0.58f)
            {
                if (previous.Contains(Event.current.mousePosition) && page > 0)
                {
                    page--;
                    lastDragPageFlip = Time.unscaledTime;
                }
                else if (next.Contains(Event.current.mousePosition) && page < lastPage)
                {
                    page++;
                    lastDragPageFlip = Time.unscaledTime;
                }
            }
            GUI.Label(new Rect(r.x+43f,r.yMax-70f,r.width-86f,20f),
                "Page " + (page+1) + "/" + (lastPage+1), detail);
            GUI.Label(new Rect(r.x+8f,r.yMax-20f,r.width-16f,17f),
                Time.unscaledTime < messageUntil ? message : "Drag, then HOLD on a component to craft",notice);

            if (recipeSource >= 0 && recipeSource < inventory.Count &&
                MonsterTacticsItemTypes.IsComponent(inventory[recipeSource]) && !draggedFar)
                DrawRecipePanel(r, ev);
            if (hoveredItem.HasValue && recipeSource < 0 &&
                (draggingIndex < 0 || craftHold.Active))
                MonsterTacticsItemTooltip.Draw(ev.mousePosition, hoveredItem.Value, hoveredContext);

            if (draggingIndex >= 0 && draggedFar && draggingIndex < inventory.Count)
            {
                Rect ghost = new Rect(ev.mousePosition.x - 33f,ev.mousePosition.y - 22f,67f,44f);
                Fill(ghost,new Color(.12f,.16f,.21f,.9f));
                MonsterTacticsItemArt.Draw(new Rect(ghost.center.x-21f,ghost.center.y-21f,42f,42f),
                    inventory[draggingIndex]);
                // Just like TFT, preview crafting when dragging a component
                // over a Monster already holding another component.
                if (!r.Contains(ev.mousePosition) && ev.type == EventType.Repaint && shop != null)
                {
                    Vector2 pointer = new Vector2(ev.mousePosition.x,
                        Screen.height - ev.mousePosition.y);
                    MonsterUnit unit = shop.ItemDropTargetAt(pointer);
                    if (unit != null && shop.CanEquipItemOn(unit))
                    {
                        MonsterTacticsItemKind preview = inventory[draggingIndex];
                        MonsterTacticsHeldItems held = unit.GetComponent<MonsterTacticsHeldItems>();
                        if (MonsterTacticsItemTypes.IsComponent(preview) && held != null)
                        {
                            for (int k = 0; k < held.Items.Count; k++)
                            {
                                MonsterTacticsItemKind result;
                                if (MonsterTacticsItemTypes.TryRecipe(preview,held.Items[k],out result))
                                {
                                    preview = result;
                                    break;
                                }
                            }
                        }
                        string dropHint = MonsterTacticsItemTypes.IsTool(inventory[draggingIndex]) &&
                            !shop.CanUseItemToolOn(unit)
                            ? "During combat: Remover/Reforger only works on benched Monster"
                            : "Release to equip on " + unit.MonsterName;
                        MonsterTacticsItemTooltip.Draw(ev.mousePosition, preview, dropHint);
                    }
                }
                // Keep dragged item visible above everything else.
                GUI.depth = -220;
            }
            GUI.depth = oldDepth;
        }
    }

    /// <summary>
    /// Independent, deterministic hold timer. A change in source or target starts
    /// a fresh timer; Cancel() discards progress with no inventory mutation.
    /// </summary>
    public sealed class MonsterTacticsCraftHoldGate
    {
        public const float RequiredSeconds = 0.85f;
        public int SourceIndex { get; private set; } = -1;
        public int TargetIndex { get; private set; } = -1;
        private float startedAt;
        public bool Active { get { return SourceIndex >= 0 && TargetIndex >= 0; } }

        public void UpdateHover(int source, int target, float now)
        {
            if (source < 0 || target < 0 || source == target)
            {
                Cancel();
                return;
            }
            if (SourceIndex != source || TargetIndex != target)
            {
                SourceIndex = source;
                TargetIndex = target;
                startedAt = now;
            }
        }

        public float Progress(float now)
        {
            return Active ? Mathf.Clamp01((now - startedAt) / RequiredSeconds) : 0f;
        }

        public bool IsComplete(float now)
        {
            return Active && now - startedAt >= RequiredSeconds;
        }

        public void Cancel()
        {
            SourceIndex = -1;
            TargetIndex = -1;
            startedAt = 0f;
        }
    }
}
