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
        private readonly Stack<(ItemProperties Item, float Scroll, bool Uses)> recipeHistory =
            new Stack<(ItemProperties, float, bool)>();
        private RectTransform recipeWindow;
        private RectTransform recipeContent;
        private ScrollRect recipeScroll;
        private CanvasGroup recipeFade;
        private Text recipeTitle;
        private Image recipeIcon;
        private Button recipeBack;
        private Text recipeStatus;
        private ItemProperties shownItem;
        private Button acquisitionTab;
        private Button usesTab;
        private bool showingUses;

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

            acquisitionTab = AddButton(recipeWindow, "获取", new Rect(22, 59, 100, 28), TitleColor,
                () => SwitchRecipeTab(false));
            usesTab = AddButton(recipeWindow, "用途", new Rect(127, 59, 100, 28), ButtonColor,
                () => SwitchRecipeTab(true));
            var view = AddImage(recipeWindow, new Rect(14, 95, RecipeWidth - 28, RecipeHeight - 137), Color.clear);
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
                recipeHistory.Push((shownItem, recipeScroll.verticalNormalizedPosition, showingUses));
            showingUses = false;
            RenderRecipe(item, 1);
        }

        private void PreviousRecipe()
        {
            if (recipeHistory.Count > 0)
            {
                var previous = recipeHistory.Pop();
                showingUses = previous.Uses;
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

        private void SwitchRecipeTab(bool uses)
        {
            if (shownItem == null || showingUses == uses)
                return;
            showingUses = uses;
            RenderRecipe(shownItem, 1);
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

            acquisitionTab.targetGraphic.color = showingUses ? ButtonColor : TitleColor;
            usesTab.targetGraphic.color = showingUses ? TitleColor : ButtonColor;
            if (showingUses)
            {
                recipeStatus.text = "";
                FinishRecipeLayout(RenderUses(item), scroll);
                return;
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

                y += AddProductionCard(entry.Station, entry.Recipe, y);
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
            FinishRecipeLayout(y, scroll);
        }

        private float AddProductionCard(BuildableProperties station, ProductionRecipeProperties recipe, float y)
        {
            const int column = 172;
            const int center = ContentWidth - column * 2;
            var stationButton = AddButton(recipeContent, station.Name,
                new Rect(column + 6, y, center - 12, 29), ButtonColor,
                () => FocusRecipeStation(station, recipe));
            var stationName = stationButton.GetComponentInChildren<Text>();
            var stationSprite = station.GetIcon();
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
            Label(recipeContent, $"{recipe.ProductionTime:0.##} 秒",
                new Rect(ContentWidth / 2 - 17, y + 65, 90, 30), 17, Ink);

            float input = AddRecipeItems("材料", recipe.RequiredItems, 0, column, y);
            float output = AddRecipeItems("产物", recipe.ProducedItems, ContentWidth - column, column, y);
            return Mathf.Max(103, Mathf.Max(input, output));
        }

        private void AddUseHeading(string caption, ref float y)
        {
            if (y > 0)
                AddRecipeDivider(ref y);
            Label(recipeContent, caption, new Rect(0, y, ContentWidth, 27), 20, Ink);
            y += 32;
        }

        private float RenderUses(ItemProperties item)
        {
            float y = 0;
            var recipes = Acquisition.Recipes(item, ingredient: true)
                .GroupBy(entry => (entry.Station.Name, entry.Recipe)).Select(group => group.First()).ToList();
            if (recipes.Count > 0)
            {
                AddUseHeading("制作材料", ref y);
                for (int i = 0; i < recipes.Count; i++)
                {
                    if (i > 0)
                        AddRecipeDivider(ref y);
                    y += AddProductionCard(recipes[i].Station, recipes[i].Recipe, y);
                }
                recipeStatus.text = "基础用时 / 批 · 受工作站和工人加成影响";
            }

            var construction = ItemUses.Construction(item).ToList();
            if (construction.Count > 0)
            {
                AddUseHeading("建造材料", ref y);
                for (int i = 0; i < construction.Count; i++)
                {
                    if (i > 0)
                        AddRecipeDivider(ref y);
                    var entry = construction[i];
                    const int column = 172;
                    float outputX = ContentWidth - column;
                    AddRecipeArrow(y + 48);
                    AddCardIcon(recipeContent, entry.Placeable.GetIcon(), new Rect(outputX + 65, y + 7, 42, 42));
                    FitLabel(Label(recipeContent, entry.Placeable.GetName(), new Rect(outputX, y + 52, column, 36),
                        18, Ink, TextAnchor.MiddleCenter));
                    y += Mathf.Max(92, AddRecipeItems("材料", entry.Materials, 0, column, y));
                }
            }

            var upgrades = ItemUses.Upgrades(item, FinishedBuildings()).ToList();
            if (upgrades.Count > 0)
            {
                AddUseHeading("升级材料", ref y);
                bool first = true;
                bool unavailableHeading = false;
                foreach (var upgrade in upgrades.OrderByDescending(entry => entry.Available))
                {
                    if (!first)
                        AddRecipeDivider(ref y);
                    first = false;
                    if (!upgrade.Available && !unavailableHeading)
                    {
                        Label(recipeContent, "暂不可升级", new Rect(0, y, ContentWidth, 24), 16, Ink);
                        y += 26;
                        unavailableHeading = true;
                    }
                    y += AddUpgradeCard(item, upgrade.Building, upgrade.Module, upgrade.Amount, upgrade.Available, y);
                }
            }

            var salvage = ItemUses.Salvage(item).GroupBy(entry => (entry.Location, entry.Cost,
                    Output: string.Join(",", entry.Output.Select(output => $"{output.ItemProperties.GetInstanceID()}:{output.Amount}")
                        .OrderBy(output => output))))
                .Select(group => group.First()).ToList();
            if (salvage.Count > 0)
            {
                AddUseHeading("拆解工具", ref y);
                for (int i = 0; i < salvage.Count; i++)
                {
                    if (i > 0)
                        AddRecipeDivider(ref y);
                    var entry = salvage[i];
                    var location = Label(recipeContent, entry.Location, new Rect(0, y + 26, 172, 55),
                        17, Ink, TextAnchor.MiddleCenter);
                    FitLabel(location);
                    int singleCost = Mathf.CeilToInt(entry.Cost);
                    string cost = $"拆 1 处用 {singleCost} 个";
                    if (entry.Cost != singleCost)
                    {
                        int groupCost = Mathf.CeilToInt(entry.Cost * 10);
                        cost += $"\n同类拆 10 处共用 {groupCost} 个";
                    }
                    FitLabel(Label(recipeContent, cost, new Rect(172, y + 4, 200, 39),
                        17, Ink, TextAnchor.MiddleCenter));
                    AddRecipeArrow(y + 53);
                    y += Mathf.Max(92, AddRecipeItems("产物", entry.Output, ContentWidth - 172, 172, y));
                }
            }

            foreach (var group in ItemUses.Other(item).GroupBy(use => use.Kind))
            {
                AddUseHeading(group.Key, ref y);
                foreach (var use in group.GroupBy(entry => (entry.Detail, Building: entry.Building?.Name))
                    .Select(entries => entries.First()))
                {
                    int detailsWidth = use.Building != null ? ContentWidth - 195 : ContentWidth;
                    var detail = Label(recipeContent, use.Detail, new Rect(4, y, detailsWidth - 8, 30), 17, Ink);
                    float height = Mathf.Max(32, detail.preferredHeight);
                    detail.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
                    if (use.Building != null)
                    {
                        var button = AddButton(recipeContent, use.Building.Name,
                            new Rect(detailsWidth + 4, y, ContentWidth - detailsWidth - 4, 29), ButtonColor,
                            () => FocusRecipeStation(use.Building, null));
                        FitLabel(button.GetComponentInChildren<Text>());
                    }
                    y += height + 3;
                }
            }
            if (y == 0)
            {
                Label(recipeContent, "暂无已知用途", new Rect(0, 0, ContentWidth, 30), 18, Ink);
                y = 30;
            }
            return y;
        }

        private float AddUpgradeCard(ItemProperties item, BuildableProperties building, ModuleProperties module, int amount, bool available, float y)
        {
            var button = AddButton(recipeContent, "", new Rect(59, y + 18, 52, 52), available ? ButtonColor : new Color32(178, 183, 183, 255),
                () => FocusRecipeStation(building, null, upgrade: module == null, module: module));
            button.interactable = available;
            var icon = AddCardIcon((RectTransform)button.transform, building.GetIcon(), new Rect(5, 5, 42, 42));
            if (!available && icon != null)
                icon.color = new Color32(140, 140, 140, 180);
            FitLabel(Label(recipeContent, building.Name, new Rect(0, y + 70, 172, 25), 17, Ink, TextAnchor.MiddleCenter));
            AddCardIcon(recipeContent, item.InventorySprite, new Rect(176, y + 9, 24, 24));
            FitLabel(Label(recipeContent, item.LocalizedName, new Rect(205, y + 7, 112, 28), 17, Ink));
            FitLabel(Label(recipeContent, $"×{amount}", new Rect(319, y + 7, 49, 28),
                17, Ink, TextAnchor.MiddleRight));
            AddRecipeArrow(y + 49);

            const int effectWidth = 172;
            float effectX = ContentWidth - effectWidth;
            string upgradeName = module != null ? module.GetName() : building.Upgrade.Name;
            FitLabel(Label(recipeContent, upgradeName, new Rect(effectX, y + 5, effectWidth, 28),
                17, Ink, TextAnchor.MiddleCenter));
            string description = module != null ? module.GetDescription() : building.Upgrade.Description;
            description = System.Text.RegularExpressions.Regex.Replace(description ?? "", "<[^>]+>", "").Trim();
            var effect = Label(recipeContent, string.IsNullOrEmpty(description) ? "暂无效果说明" : description,
                new Rect(effectX + 3, y + 34, effectWidth - 6, 60), 16, Ink, TextAnchor.UpperLeft);
            float height = Mathf.Max(60, effect.preferredHeight + 2);
            effect.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            return Mathf.Max(104, 40 + height);
        }

        private Image AddCardIcon(RectTransform parent, Sprite sprite, Rect bounds)
        {
            if (sprite == null)
                return null;
            var image = AddImage(parent, bounds, Color.white);
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        private static void FitLabel(Text label)
        {
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 13;
            label.resizeTextMaxSize = label.fontSize;
        }

        private void FinishRecipeLayout(float y, float scroll)
        {
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

        private static IEnumerable<Buildable> FinishedBuildings()
        {
            var town = Community.PlayerCommunity;
            if (town == null)
                return Enumerable.Empty<Buildable>();
            var camera = CameraController.Instance;
            var focus = camera != null ? camera.ReturnFocusPoint() : Vector3.zero;
            return town.Buildables.Where(building => building != null && building.Properties != null &&
                    building.BuildPhase == BuildPhase.Finished && building.gameObject.activeInHierarchy)
                .OrderBy(building => (building.transform.position - focus).sqrMagnitude);
        }

        private void FocusRecipeStation(BuildableProperties station, ProductionRecipeProperties recipe, bool upgrade = false, ModuleProperties module = null)
        {
            var camera = CameraController.Instance;
            var town = Community.PlayerCommunity;
            if (camera == null || town == null)
                return;
            if ((upgrade && !station.Upgrade.IsUnlocked()) || (module != null && !module.IsUnlocked()))
            {
                recipeStatus.text = "该升级尚未解锁";
                return;
            }
            var target = FinishedBuildings().FirstOrDefault(buildable =>
                (module == null || (buildable.TryReturnBuildableExtendable<ModuleManager>(out var manager) &&
                    !manager.IsActiveModule(module))) &&
                (buildable.Properties == station || (recipe != null && buildable.Properties.Name == station.Name)) &&
                (recipe == null || (buildable.TryReturnBuildableExtendable<Producer>(out var producer) &&
                    producer.TryReturnRecipe(recipe, out _))));
            if (target == null)
            {
                recipeStatus.text = upgrade || module != null ? "城镇里没有可升级的此类建筑" : "城镇里没有已建成的此类工作站";
                return;
            }
            Producer selectedProducer = null;
            if (recipe != null && target.TryReturnBuildableExtendable<Producer>(out selectedProducer) &&
                selectedProducer.TryReturnRecipe(recipe, out var selectedRecipe))
            {
                if (!selectedRecipe.IsUnlocked())
                {
                    recipeStatus.text = "该配方尚未解锁";
                    return;
                }
                if (selectedProducer.SelectedRecipeIndex != selectedRecipe.Index)
                    selectedProducer.SetSelectedRecipe(selectedRecipe.Index);
            }
            CloseRecipe();
            camera.Lock(target.gameObject, Mathf.Min(camera.CurrentZoomLevel, 0.5f));
            var link = target.GetComponentsInChildren<SelectionLink>()
                .FirstOrDefault(candidate => candidate.isActiveAndEnabled && candidate.Type == ObjectType.Buildable &&
                    candidate.ObjectToSelect == target.gameObject && candidate.GetComponent<QuickConnecting>() == null);
            if (link != null && Selector.ReturnSelectedObjectComponent<Buildable>(ObjectType.Buildable) != target)
                Selector.Select(link);
            else
            {
                target.OnSelected(playSelectionSound: true);
                GameManager.HighlightManager?.HighlightObject(target.OutlineRenderer);
            }
            if (selectedProducer != null)
                ShowStationRecipePanel(target, selectedProducer);
        }

        private static void ShowStationRecipePanel(Buildable target, Producer producer)
        {
            var panel = FindObjectsByType<BuildablePanel>(FindObjectsSortMode.None)
                .FirstOrDefault(candidate => candidate.Buildable == target);
            if (panel == null)
                return;

            panel.Initialize(target, refresh: true);
            var recipePanel = producer.ProductionProperties.Type == Producer.Type.Farm
                ? (MonoBehaviour)panel.GetComponentInChildren<FarmPanel>(true)
                : panel.GetComponentInChildren<ProductionPanel>(true);
            var tabs = panel.GetComponentInChildren<PanelTabContainer>(true);
            if (recipePanel == null || tabs == null || !tabs.isActiveAndEnabled ||
                !(HarmonyLib.AccessTools.Field(typeof(PanelTabContainer), "_activeTabs").GetValue(tabs) is System.Collections.IEnumerable pages))
                return;

            foreach (var page in pages)
            {
                var pageType = page.GetType();
                if (HarmonyLib.AccessTools.Field(pageType, "Page").GetValue(page) is Transform content &&
                    recipePanel.transform.IsChildOf(content) &&
                    HarmonyLib.AccessTools.Field(pageType, "Tab").GetValue(page) is Toggle toggle)
                {
                    toggle.isOn = true;
                    break;
                }
            }
        }
    }
}
