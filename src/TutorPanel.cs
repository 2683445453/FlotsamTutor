using System;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace FlotsamTutor
{
    public sealed partial class TutorPanel : MonoBehaviour
    {
        private const string PatchId = "local.flotsam.tutor";
        internal const string MapSetting = "FlotsamTutor.Minimap";
        internal const string SailSetting = "FlotsamTutor.QuickSailing";
        private static readonly Color PanelColor = new Color32(239, 247, 243, 255);
        private static readonly Color TitleColor = new Color32(151, 213, 200, 255);
        private static readonly Color FieldColor = new Color32(253, 250, 231, 255);
        private static readonly Color ButtonColor = new Color32(182, 223, 214, 255);
        private static readonly Color Ink = new Color32(38, 62, 70, 255);
        private static TutorPanel instance;
        private Font font;

        internal static int Install()
        {
            if (instance != null)
                return 0;
            var harmony = new Harmony(PatchId);
            GameObject host = null;
            try
            {
                RecipeShortcut.Load();
                harmony.PatchAll(typeof(TutorPanel).Assembly);
                host = new GameObject("Flotsam Tutor", typeof(RectTransform), typeof(Canvas),
                    typeof(CanvasScaler), typeof(GraphicRaycaster));
                DontDestroyOnLoad(host);
                var panel = host.AddComponent<TutorPanel>();
                var canvas = host.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 10;
                var scaler = host.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;
                panel.font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei" }, 22);
                panel.CreateWindowArt();
                panel.CreateMinimap();
                instance = panel;
                Debug.Log("[Flotsam Tutor] Ready.");
                return 0;
            }
            catch (Exception error)
            {
                harmony.UnpatchSelf();
                if (host != null)
                    Destroy(host);
                Debug.LogError("[Flotsam Tutor] " + error);
                return 24;
            }
        }

        private void Update()
        {
            UpdateDiscardWindow();
            UpdateRecipeWindow();
            RecipeGuide.HandleMouse();
            showMinimap = PlayerPrefs.GetInt(MapSetting, 0) != 0;
            quickSailing = PlayerPrefs.GetInt(SailSetting, 1) != 0;
            KeepWindowOnScreen();
            UpdateNavigation();
        }

        private static void Position(RectTransform rect, RectTransform parent, Rect bounds)
        {
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(bounds.x, -bounds.y);
            rect.sizeDelta = bounds.size;
        }
        private Button AddButton(RectTransform parent, string caption, Rect bounds, Color color, Action action)
        {
            var image = AddImage(parent, bounds, color);
            UseSlicedArt(image, controlArt);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(() => action());
            Label(image.rectTransform, caption, new Rect(0, 0, bounds.width, bounds.height), 18,
                Ink, TextAnchor.MiddleCenter);
            return button;
        }

        private static Image AddImage(RectTransform parent, Rect bounds, Color color)
        {
            var image = new GameObject("Background", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            Position(image.rectTransform, parent, bounds);
            image.color = color;
            return image;
        }

        private Text Label(RectTransform parent, string caption, Rect bounds, int size, Color color,
            TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            var label = new GameObject("Label", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            Position(label.rectTransform, parent, bounds);
            label.font = font;
            label.fontSize = size;
            label.color = color;
            label.alignment = alignment;
            label.raycastTarget = false;
            label.text = caption;
            return label;
        }

    }
}
