using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using PajamaLlama.SurvivalGuide;

namespace FlotsamTutor
{
    public sealed partial class TutorPanel : ICancelable
    {
        private const int RecipeWidth = 588;
        private const int RecipeHeight = 490;
        private const int ContentWidth = RecipeWidth - 44;
        private readonly Stack<(ItemProperties Item, float Scroll)> recipeHistory =
            new Stack<(ItemProperties, float)>();
        private RectTransform recipeWindow;
        private RectTransform recipeContent;
        private ScrollRect recipeScroll;
        private CanvasGroup recipeFade;
        private Text recipeTitle;
        private Image recipeIcon;
        private Button recipeBack;
        private Text recipeStatus;
        private ItemProperties shownItem;

        internal static void ShowRecipe(ItemProperties item)
        {
            if (instance == null || item == null)
                return;
            instance.ShowRecipePage(item);
        }

        private void CreateRecipeWindow()
        {
            var canvas = (RectTransform)transform;
            recipeWindow = AddImage(canvas, new Rect(24, 128, RecipeWidth, RecipeHeight),
                new Color32(244, 248, 248, 255)).rectTransform;
            recipeWindow.name = "Flotsam Tutor Recipe";
            UseSlicedArt(recipeWindow.GetComponent<Image>(), frameArt);
            recipeFade = recipeWindow.gameObject.AddComponent<CanvasGroup>();

            var header = AddImage(recipeWindow, new Rect(4, 4, RecipeWidth - 8, 48), TitleColor);
            UseSlicedArt(header, controlArt);
            BindGrip(header, recipeWindow, false);
            recipeBack = AddButton(recipeWindow, "‹", new Rect(12, 11, 34, 34), ButtonColor, PreviousRecipe);
            recipeIcon = AddImage(recipeWindow, new Rect(56, 10, 36, 36), Color.white);
            recipeIcon.preserveAspect = true;
            recipeIcon.raycastTarget = false;
            recipeTitle = Label(recipeWindow, "", new Rect(102, 5, RecipeWidth - 156, 46), 24, Ink);
            recipeTitle.resizeTextForBestFit = true;
            recipeTitle.resizeTextMinSize = 16;
            recipeTitle.resizeTextMaxSize = 24;
            AddButton(recipeWindow, "×", new Rect(RecipeWidth - 46, 11, 34, 34),
                new Color32(247, 172, 130, 255), CloseRecipe);

            var view = AddImage(recipeWindow, new Rect(14, 62, RecipeWidth - 28, RecipeHeight - 104), Color.clear);
            view.gameObject.AddComponent<RectMask2D>();
            recipeScroll = view.gameObject.AddComponent<ScrollRect>();
            recipeScroll.viewport = view.rectTransform;
            recipeScroll.horizontal = false;
            recipeScroll.movementType = ScrollRect.MovementType.Clamped;
            recipeScroll.scrollSensitivity = 30;
            recipeContent = new GameObject("Recipes", typeof(RectTransform)).GetComponent<RectTransform>();
            Position(recipeContent, view.rectTransform, new Rect(8, 0, ContentWidth, 0));
            recipeScroll.content = recipeContent;
            recipeStatus = Label(recipeWindow, "", new Rect(22, RecipeHeight - 37, ContentWidth, 28),
                16, Ink);
            recipeWindow.gameObject.SetActive(false);
        }

        private void ShowRecipePage(ItemProperties item)
        {
            if (Community.PlayerCommunity == null || LoadingScreen.IsLoading)
                return;
            if (recipeWindow == null)
                CreateRecipeWindow();
            if (!recipeWindow.gameObject.activeSelf)
            {
                recipeWindow.gameObject.SetActive(true);
                recipeWindow.SetAsLastSibling();
                recipeFade.alpha = 0;
                FlotsamInputManager.PushCancelable(this);
                var canvas = (RectTransform)transform;
                PanelGrip.SetBounds(recipeWindow, canvas, recipeWindow.anchoredPosition, recipeWindow.localScale.x);
            }
            if (shownItem == item)
                return;
            if (shownItem != null)
                recipeHistory.Push((shownItem, recipeScroll.verticalNormalizedPosition));
            RenderRecipe(item, 1);
        }

        private void PreviousRecipe()
        {
            if (recipeHistory.Count > 0)
            {
                var previous = recipeHistory.Pop();
                RenderRecipe(previous.Item, previous.Scroll);
            }
        }

        private void CloseRecipe()
        {
            if (recipeWindow == null || !recipeWindow.gameObject.activeSelf)
                return;
            recipeWindow.gameObject.SetActive(false);
            FlotsamInputManager.RemoveCancelable(this);
            recipeHistory.Clear();
            shownItem = null;
        }

        bool ICancelable.TryCancel()
        {
            if (discardItem != null)
                CloseDiscard();
            else
                CloseRecipe();
            return true;
        }

        private void UpdateRecipeWindow()
        {
            if (recipeWindow == null || !recipeWindow.gameObject.activeSelf)
                return;
            if (Community.PlayerCommunity == null || LoadingScreen.IsLoading ||
                UIManager.State == UIState.Paused || UIManager.State == UIState.Map)
                CloseRecipe();
            else
                recipeFade.alpha = Mathf.MoveTowards(recipeFade.alpha, 1, Time.unscaledDeltaTime * 10);
        }

        private void RenderRecipe(ItemProperties item, float scroll)
        {
            shownItem = item;
            recipeTitle.text = item.LocalizedName;
            recipeIcon.sprite = item.InventorySprite;
            recipeIcon.enabled = item.InventorySprite != null;
            recipeBack.interactable = recipeHistory.Count > 0;
            foreach (Transform child in recipeContent)
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }

            var recipes = Acquisition.Recipes(item).GroupBy(entry => (Name: entry.Station.Name, entry.Recipe))
                .Select(group => group.First()).ToList();
            float y = 0;
            for (int i = 0; i < recipes.Count; i++)
            {
                if (i > 0)
                    AddRecipeDivider(ref y);
                var entry = recipes[i];
                if (recipes.Count > 1)
                {
                    Label(recipeContent, $"配方 {i + 1}", new Rect(0, y, ContentWidth, 24), 16, Ink);
                    y += 24;
                }

                const int column = 172;
                const int center = ContentWidth - column * 2;
                var stationButton = AddButton(recipeContent, entry.Station.Name,
                    new Rect(column + 6, y, center - 12, 29), ButtonColor,
                    () => FocusRecipeStation(entry.Station, entry.Recipe));
                var stationName = stationButton.GetComponentInChildren<Text>();
                var stationSprite = entry.Station.GetIcon();
                if (stationSprite != null)
                {
                    var stationRect = (RectTransform)stationButton.transform;
                    var stationIcon = AddImage(stationRect, new Rect(7, 3, 24, 23), Color.white);
                    stationIcon.sprite = stationSprite;
                    stationIcon.preserveAspect = true;
                    stationIcon.raycastTarget = false;
                    Position(stationName.rectTransform, stationRect, new Rect(36, 0, center - 54, 29));
                    stationName.alignment = TextAnchor.MiddleLeft;
                }
                stationName.resizeTextForBestFit = true;
                stationName.resizeTextMinSize = 13;
                stationName.resizeTextMaxSize = 18;
                AddRecipeArrow(y + 48);

                var time = AddImage(recipeContent, new Rect(ContentWidth / 2 - 43, y + 70, 20, 20), Color.white);
                time.sprite = SurvivalGuideManager.Properties?.TimeSprite;
                time.enabled = time.sprite != null;
                time.preserveAspect = true;
                time.raycastTarget = false;
                Label(recipeContent, $"{entry.Recipe.ProductionTime:0.##} 秒",
                    new Rect(ContentWidth / 2 - 17, y + 65, 90, 30), 17, Ink);

                float input = AddRecipeItems("材料", entry.Recipe.RequiredItems, 0, column, y);
                float output = AddRecipeItems("产物", entry.Recipe.ProducedItems, ContentWidth - column, column, y);
                y += Mathf.Max(103, Mathf.Max(input, output));
            }

            var sources = Acquisition.Sources(item).ToList();
            if (sources.Count > 0 || recipes.Count == 0)
            {
                if (recipes.Count > 0)
                    AddRecipeDivider(ref y);
                Label(recipeContent, "获取地点", new Rect(0, y, ContentWidth, 27), 20, Ink);
                y += 30;
                foreach (var place in sources.DefaultIfEmpty("暂无来源"))
                {
                    var label = Label(recipeContent, place, new Rect(6, y, ContentWidth - 12, 25), 18, Ink);
                    float height = Mathf.Max(25, label.preferredHeight);
                    label.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
                    y += height + 3;
                }
            }

            recipeStatus.text = recipes.Count > 0 ? "基础用时 / 批 · 受工作站和工人加成影响" : "";
            recipeContent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, y + 4);
            recipeScroll.StopMovement();
            Canvas.ForceUpdateCanvases();
            recipeScroll.verticalNormalizedPosition = scroll;
        }

        private void AddRecipeArrow(float y)
        {
            float x = ContentWidth / 2f;
            var tip = new Vector2(x + 24, y);
            AddArrowStroke(new Vector2(x - 24, y), tip);
            AddArrowStroke(tip, new Vector2(x + 13, y - 9));
            AddArrowStroke(tip, new Vector2(x + 13, y + 9));
        }

        private void AddArrowStroke(Vector2 from, Vector2 to)
        {
            var delta = to - from;
            var stroke = AddImage(recipeContent, new Rect(0, 0, delta.magnitude + 2, 3), Ink);
            stroke.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            var center = (from + to) / 2;
            stroke.rectTransform.anchoredPosition = new Vector2(center.x, -center.y);
            stroke.rectTransform.localRotation = Quaternion.Euler(0, 0, -Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            stroke.raycastTarget = false;
        }

        private void AddRecipeDivider(ref float y)
        {
            AddImage(recipeContent, new Rect(0, y + 5, ContentWidth, 1), new Color32(163, 192, 188, 255))
                .raycastTarget = false;
            y += 15;
        }

        private float AddRecipeItems(string caption, IReadOnlyList<CountedItemProperty> entries, float x, int width, float y)
        {
            Label(recipeContent, caption, new Rect(x, y, width, 25), 17, Ink, TextAnchor.MiddleCenter);
            int index = 0;
            foreach (var entry in entries)
            {
                if (entry?.ItemProperties == null || entry.Amount <= 0)
                    continue;
                var item = entry.ItemProperties;
                var tile = AddButton(recipeContent, "", new Rect(x + 11 + index % 3 * 52, y + 30 + index / 3 * 48, 46, 42),
                    item.ItemType.Color, () => ShowRecipePage(item));
                var tileRect = (RectTransform)tile.transform;
                var icon = AddImage(tileRect, new Rect(4, 3, 35, 33), Color.white);
                icon.sprite = item.InventorySprite;
                icon.enabled = icon.sprite != null;
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                Label(tileRect, entry.Amount.ToString(), new Rect(19, 19, 23, 20), 16, Color.white,
                    TextAnchor.MiddleRight).gameObject.AddComponent<Outline>().effectDistance = new Vector2(1, -1);
                var tooltip = tile.gameObject.AddComponent<ItemTooltip>();
                tooltip.Initialize(item);
                index++;
            }
            if (index == 0)
                Label(recipeContent, "—", new Rect(x, y + 30, width, 42), 17, Ink, TextAnchor.MiddleCenter);
            return 30 + Mathf.Max(1, (index + 2) / 3) * 48;
        }

        private void FocusRecipeStation(BuildableProperties station, ProductionRecipeProperties recipe)
        {
            var camera = CameraController.Instance;
            var town = Community.PlayerCommunity;
            if (camera == null || town == null)
                return;
            var focus = camera.ReturnFocusPoint();
            var target = town.Buildables.Where(buildable => buildable != null &&
                    buildable.BuildPhase == BuildPhase.Finished && buildable.gameObject.activeInHierarchy &&
                    (buildable.Properties == station ||
                        (buildable.Properties.Name == station.Name && buildable.TryGetComponent<Producer>(out var producer) &&
                            producer.ProductionProperties.Recipes.Contains(recipe))))
                .OrderBy(buildable => (buildable.transform.position - focus).sqrMagnitude)
                .FirstOrDefault();
            if (target == null)
            {
                recipeStatus.text = "城镇里没有已建成的此类工作站";
                return;
            }
            CloseRecipe();
            camera.Lock(target.gameObject, Mathf.Min(camera.CurrentZoomLevel, 0.5f));
            target.OnSelected(playSelectionSound: true);
        }
    }
}
