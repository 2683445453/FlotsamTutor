using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FlotsamTutor
{
    [HarmonyPatch(typeof(GameplaySettingsPanel), nameof(GameplaySettingsPanel.Load))]
    internal static class ModSettings
    {
        private static void Postfix(GameplaySettingsPanel __instance, Toggle ____toggleGeneralStorageFilters)
        {
            var source = ____toggleGeneralStorageFilters;
            var row = source.transform;
            while (row.GetComponentInChildren<TextMeshProUGUI>(true) == null && row.parent != __instance.transform)
                row = row.parent;
            Add(row, "显示小地图", TutorPanel.MapSetting, false);
            Add(row, "小地图快捷航行", TutorPanel.SailSetting, true);
            Add(row, "实验功能 · 航行贴图滚动", TutorPanel.SailingTexturesSetting, false);
            AddShortcut(row);
        }

        private static void AddShortcut(Transform row)
        {
            const string name = "FlotsamTutor.RecipeShortcut";
            if (row.parent.Find(name) != null)
                return;
            var clone = Object.Instantiate(row.gameObject, row.parent, false);
            clone.name = name;
            foreach (var localize in clone.GetComponentsInChildren<I2.Loc.Localize>(true))
                localize.enabled = false;
            var toggle = clone.GetComponentInChildren<Toggle>(true);
            toggle.group = null;
            toggle.onValueChanged = new Toggle.ToggleEvent();
            clone.AddComponent<RecipeShortcutControl>().Initialize(toggle, clone.GetComponentInChildren<TextMeshProUGUI>(true));
        }

        private static void Add(Transform row, string caption, string key, bool fallback)
        {
            if (row.parent.Find(key) != null)
                return;
            var clone = Object.Instantiate(row.gameObject, row.parent, false);
            clone.name = key;
            var toggle = clone.GetComponentInChildren<Toggle>(true);
            toggle.group = null;
            toggle.onValueChanged = new Toggle.ToggleEvent();
            foreach (var localize in clone.GetComponentsInChildren<I2.Loc.Localize>(true))
                localize.enabled = false;
            var label = clone.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null)
                label.text = caption;
            toggle.SetIsOnWithoutNotify(PlayerPrefs.GetInt(key, fallback ? 1 : 0) != 0);
            toggle.onValueChanged.AddListener(value =>
            {
                PlayerPrefs.SetInt(key, value ? 1 : 0);
                PlayerPrefs.Save();
            });
        }
    }
    internal sealed class RecipeShortcutControl : MonoBehaviour
    {
        private Toggle toggle;
        private TextMeshProUGUI label;
        private int captureFrame;

        internal void Initialize(Toggle control, TextMeshProUGUI caption)
        {
            toggle = control;
            label = caption;
            StopCapture();
            toggle.onValueChanged.AddListener(value =>
            {
                if (!value)
                {
                    StopCapture();
                    return;
                }
                RecipeShortcut.Capturing = true;
                captureFrame = Time.frameCount;
                label.text = "按下新的查询键 · Esc 取消";
            });
        }

        private void Update()
        {
            if (toggle == null || !toggle.isOn || Time.frameCount == captureFrame)
                return;
            if (Input.GetKeyDown(KeyCode.Escape) || RecipeShortcut.TryCapture())
                StopCapture();
        }

        private void OnDisable()
        {
            if (toggle != null && toggle.isOn)
                StopCapture();
        }

        private void StopCapture()
        {
            RecipeShortcut.Capturing = false;
            toggle.SetIsOnWithoutNotify(false);
            label.text = $"模组 · 配方查询键：{RecipeShortcut.Caption}";
        }
    }
}
