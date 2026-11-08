using System;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace FlotsamTutor
{
    public sealed partial class TutorPanel
    {
        private GameObject discardOverlay;
        private ItemProperties discardItem;
        private Community discardTown;
        private UIManager discardUI;
        private Slider discardSlider;
        private InputField discardAmount;
        private bool discardPausedGame;

        internal static void ShowDiscard(ItemProperties item)
        {
            if (instance == null || instance.discardItem != null || item == null ||
                item.IsQuestItem || item.Id < 0)
                return;
            var town = Community.PlayerCommunity;
            if (town == null)
                return;
            int available = town.Inventory.ReturnCount(item);
            if (available > 0)
                instance.OpenDiscard(town, item, available);
        }

        private void OpenDiscard(Community town, ItemProperties item, int available)
        {
            CloseRecipe();
            var shade = new GameObject("Discard confirmation", typeof(RectTransform), typeof(Image));
            var overlay = (RectTransform)shade.transform;
            overlay.SetParent(transform, false);
            overlay.anchorMin = Vector2.zero;
            overlay.anchorMax = Vector2.one;
            overlay.offsetMin = overlay.offsetMax = Vector2.zero;
            shade.GetComponent<Image>().color = new Color(0.05f, 0.12f, 0.16f, 0.38f);

            var box = AddImage(overlay, new Rect(0, 0, 440, 220), PanelColor);
            UseSlicedArt(box, frameArt);
            var rect = box.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            var title = AddImage(rect, new Rect(5, 5, 430, 38), TitleColor);
            UseSlicedArt(title, controlArt);
            Label(rect, "丢弃物品", new Rect(16, 5, 408, 38), 22, Ink, TextAnchor.MiddleCenter);

            var icon = AddImage(rect, new Rect(21, 58, 38, 38), Color.white);
            icon.sprite = item.InventorySprite;
            icon.enabled = icon.sprite != null;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            Label(rect, item.LocalizedName, new Rect(69, 58, 220, 38), 20, Ink);
            Label(rect, $"可丢弃 {available}", new Rect(289, 58, 129, 38), 17, Ink, TextAnchor.MiddleRight);

            var slider = DefaultControls.CreateSlider(new DefaultControls.Resources()).GetComponent<Slider>();
            var track = slider.transform.Find("Background").GetComponent<Image>();
            UseSlicedArt(track, controlArt);
            track.color = new Color32(61, 91, 98, 255);
            track.rectTransform.anchorMin = new Vector2(0, 0.28f);
            track.rectTransform.anchorMax = new Vector2(1, 0.72f);

            var fill = slider.fillRect.GetComponent<Image>();
            UseSlicedArt(fill, controlArt);
            fill.color = new Color32(77, 184, 157, 255);
            fill.rectTransform.parent.GetComponent<RectTransform>().anchorMin = new Vector2(0, 0.32f);
            fill.rectTransform.parent.GetComponent<RectTransform>().anchorMax = new Vector2(1, 0.68f);

            var handle = slider.handleRect.GetComponent<Image>();
            UseSlicedArt(handle, controlArt);
            handle.color = new Color32(237, 169, 101, 255);
            handle.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 22);
            var colors = slider.colors;
            colors.highlightedColor = new Color32(255, 246, 215, 255);
            colors.pressedColor = new Color32(211, 181, 148, 255);
            colors.disabledColor = new Color32(198, 198, 198, 255);
            slider.colors = colors;
            Position((RectTransform)slider.transform, rect, new Rect(21, 113, 302, 30));
            slider.wholeNumbers = true;
            slider.minValue = 1;
            slider.maxValue = available;
            slider.SetValueWithoutNotify(1);
            slider.interactable = available > 1;
            discardSlider = slider;

            var inputBox = AddImage(rect, new Rect(335, 108, 83, 39), FieldColor);
            UseSlicedArt(inputBox, controlArt);
            discardAmount = inputBox.gameObject.AddComponent<InputField>();
            discardAmount.targetGraphic = inputBox;
            discardAmount.textComponent = Label(inputBox.rectTransform, "", new Rect(6, 0, 71, 39),
                19, Ink, TextAnchor.MiddleCenter);
            discardAmount.contentType = InputField.ContentType.IntegerNumber;
            discardAmount.characterLimit = 10;
            discardAmount.text = "1";
            slider.onValueChanged.AddListener(value => discardAmount.SetTextWithoutNotify(((int)value).ToString()));
            discardAmount.onValueChanged.AddListener(value =>
            {
                if (long.TryParse(value, out var quantity))
                    slider.SetValueWithoutNotify(ClampDiscardAmount(quantity));
            });
            discardAmount.onEndEdit.AddListener(_ => discardAmount.SetTextWithoutNotify(ReadDiscardAmount().ToString()));

            AddButton(rect, "取消", new Rect(21, 166, 190, 37), ButtonColor, CloseDiscard);
            AddButton(rect, "确认丢弃", new Rect(228, 166, 190, 37),
                new Color32(247, 172, 130, 255), ConfirmDiscard);

            discardOverlay = shade;
            discardItem = item;
            discardTown = town;
            discardUI = GameManager.UIManager;
            discardPausedGame = GameSpeedManager.GameSpeed != GameSpeed.Paused;
            if (discardPausedGame)
                GameSpeedManager.Pause();
            FlotsamInputManager.PushCancelable(this);
        }

        private int ClampDiscardAmount(long quantity) =>
            (int)Math.Max(1, Math.Min(quantity, (long)discardSlider.maxValue));

        private int ReadDiscardAmount() =>
            long.TryParse(discardAmount.text, out var quantity) ? ClampDiscardAmount(quantity) : (int)discardSlider.value;

        private void ConfirmDiscard()
        {
            if (discardItem == null)
                return;
            try
            {
                if (discardTown != Community.PlayerCommunity)
                    return;
                var inventory = discardTown.Inventory;
                int amount = Math.Min(ReadDiscardAmount(), inventory.ReturnCount(discardItem));
                if (amount == 0)
                    return;
                bool reserved = inventory.TryReserveItems(discardItem, amount, out var items);
                try
                {
                    if (!reserved)
                        return;
                    UIEvent.Dispatch(UIEvent.Type.EjectItem);
                    foreach (var item in items)
                        item.TryTakeFromInventory(out _);
                }
                finally
                {
                    if (items != null)
                        foreach (var item in items)
                            if (item.IsReserved)
                                item.CancelReservation();
                }
            }
            finally
            {
                CloseDiscard();
            }
        }

        private void CloseDiscard()
        {
            if (discardItem == null)
                return;
            discardItem = null;
            FlotsamInputManager.RemoveCancelable(this);
            if (discardOverlay != null)
            {
                discardOverlay.SetActive(false);
                Destroy(discardOverlay);
            }
            if (discardPausedGame && GameSpeedManager.GameSpeed == GameSpeed.Paused)
                GameSpeedManager.Unpause();
            discardPausedGame = false;
            discardTown = null;
            discardUI = null;
        }

        private void UpdateDiscardWindow()
        {
            if (discardItem != null && (LoadingScreen.IsLoading || discardTown != Community.PlayerCommunity ||
                discardUI != GameManager.UIManager))
                CloseDiscard();
        }

        [HarmonyPatch(typeof(UIManager), nameof(UIManager.PauseGame))]
        private static class CloseDiscardBeforeGameMenu
        {
            private static void Prefix() => instance?.CloseDiscard();
        }
    }
}
