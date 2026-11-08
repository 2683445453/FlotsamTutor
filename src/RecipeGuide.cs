using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using PajamaLlama.SurvivalGuide;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FlotsamTutor
{
    internal static class RecipeGuide
    {
        private static readonly FieldInfo TooltipItem = AccessTools.Field(typeof(ItemTooltip), "_properties");
        private static readonly FieldInfo CounterItem = AccessTools.Field(typeof(ItemCounterSlot), "_properties");
        private static readonly List<RaycastResult> Hits = new List<RaycastResult>();
        private static Dictionary<Sprite, ItemProperties> icons;
        private static ItemSettings iconSettings;
        private static int rightFrame = -1;
        private static ItemProperties rightItem;
        private static bool discardClick;

        internal static bool OwnsRightMouse()
        {
            if (rightFrame == Time.frameCount)
                return rightItem != null;
            rightFrame = Time.frameCount;
            if (Input.GetMouseButtonDown(1))
            {
                rightItem = null;
                discardClick = false;
                if (EventSystem.current != null && !LoadingScreen.IsLoading && Community.PlayerCommunity != null)
                {
                    var hit = MouseHit();
                    if (hit.gameObject != null)
                    {
                        if (RecipeShortcut.Key == KeyCode.Mouse1 && RecipeShortcut.Pressed)
                            rightItem = FindItem(hit);
                        else if (!RecipeShortcut.Capturing && RecipeShortcut.CurrentModifiers == 0 &&
                            UIManager.State != UIState.Paused &&
                            hit.gameObject.GetComponentInParent<InventoryPanelItemSlot>() is InventoryPanelItemSlot slot &&
                            slot.GetComponentInParent<CommunityInventoryPanel>() != null)
                        {
                            rightItem = slot.ItemProperties;
                            discardClick = true;
                        }
                    }
                }
            }
            else if (!Input.GetMouseButton(1) && !Input.GetMouseButtonUp(1))
                rightItem = null;
            return rightItem != null;
        }

        internal static void HandleMouse()
        {
            if (RecipeShortcut.Key != KeyCode.Mouse1 && RecipeShortcut.Pressed && EventSystem.current != null &&
                Community.PlayerCommunity != null && !LoadingScreen.IsLoading &&
                UIManager.State != UIState.Paused && UIManager.State != UIState.Typing && !IsTextInputFocused())
            {
                var hit = MouseHit();
                if (hit.gameObject != null)
                    TutorPanel.ShowRecipe(FindItem(hit));
            }

            if (!OwnsRightMouse() || !Input.GetMouseButtonUp(1))
                return;
            if (discardClick)
                TutorPanel.ShowDiscard(rightItem);
            else
                TutorPanel.ShowRecipe(rightItem);
        }

        private static bool IsTextInputFocused()
        {
            var selected = EventSystem.current.currentSelectedGameObject;
            if (selected == null)
                return false;
            return (selected.TryGetComponent<InputField>(out var input) && input.isFocused) ||
                (selected.TryGetComponent<TMPro.TMP_InputField>(out var tmpInput) && tmpInput.isFocused);
        }

        private static RaycastResult MouseHit()
        {
            var events = EventSystem.current;
            Hits.Clear();
            events.RaycastAll(new PointerEventData(events) { position = Input.mousePosition }, Hits);
            var hit = Hits.Count > 0 ? Hits[0] : default;
            Hits.Clear();
            return hit;
        }

        private static ItemProperties FindItem(RaycastResult hit)
        {
            var settings = GameManager.Settings?.ItemSettings;
            if (settings == null)
                return null;
            if (iconSettings != settings)
            {
                iconSettings = settings;
                icons = new Dictionary<Sprite, ItemProperties>();
                foreach (var item in settings.ItemProperties)
                {
                    if (item == null)
                        continue;
                    foreach (var sprite in new[] { item.InventorySprite, item.ToggleSprite.Off,
                        item.ToggleSprite.Disbaled, item.ToggleSprite.Selected })
                        if (sprite != null && !icons.ContainsKey(sprite))
                            icons.Add(sprite, item);
                }
            }
            for (var node = hit.gameObject.transform; node != null; node = node.parent)
            {
                var display = node.GetComponent<RecipeItemDisplay>();
                if (display != null && display.ItemProperties != null)
                    return display.ItemProperties;
                var tooltip = node.GetComponent<ItemTooltip>();
                if (tooltip != null && TooltipItem.GetValue(tooltip) is ItemProperties item && item != null)
                    return item;
                var counter = node.GetComponent<ItemCounterSlot>();
                if (counter != null && CounterItem.GetValue(counter) is ItemProperties counted && counted != null)
                    return counted;
                var image = node.GetComponent<Image>();
                if (image != null && image.overrideSprite != null &&
                    icons.TryGetValue(image.overrideSprite, out var pictured))
                    return pictured;
            }
            var camera = hit.module.eventCamera;
            foreach (var image in hit.gameObject.GetComponentsInChildren<Image>())
                if (image.isActiveAndEnabled && image.overrideSprite != null &&
                    icons.TryGetValue(image.overrideSprite, out var pictured) &&
                    RectTransformUtility.RectangleContainsScreenPoint(image.rectTransform, Input.mousePosition, camera))
                    return pictured;
            return null;
        }

        [HarmonyPatch]
        private static class SuppressVanillaRightClick
        {
            private static IEnumerable<MethodBase> TargetMethods()
            {
                foreach (var type in new[] { typeof(RecipeItemDisplay), typeof(InventoryIcon),
                    typeof(InventoryPanelItemSlot), typeof(InventoryContextMenu), typeof(MultiButton), typeof(Linker) })
                {
                    var method = AccessTools.DeclaredMethod(type, "OnPointerClick", new[] { typeof(PointerEventData) });
                    if (method != null)
                        yield return method;
                }
            }

            private static bool Prefix(PointerEventData __0) =>
                __0.button != PointerEventData.InputButton.Right || !OwnsRightMouse();
        }
    }
}
