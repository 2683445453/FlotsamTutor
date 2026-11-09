using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using HarmonyLib;
using I2.Loc;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace FlotsamTutor
{
    internal sealed class QuestItemLinks : MonoBehaviour
    {
        private const string LinkPrefix = "flotsam-item-";
        private static ItemSettings itemSettings;
        private static string language;
        private static Dictionary<string, ItemProperties> items;
        private static Dictionary<string, ItemProperties> nativeLinks;
        private static Regex matcher;

        private readonly List<ItemProperties> links = new List<ItemProperties>();
        private TextMeshProUGUI label;
        private string linkedText;

        internal static void Apply(TextMeshProUGUI label)
        {
            var settings = GameManager.Settings?.ItemSettings;
            if (label == null || settings == null)
                return;

            var currentLanguage = LocalizationManager.CurrentLanguageCode;
            if (itemSettings != settings || language != currentLanguage)
                BuildMatcher(settings, currentLanguage);

            var binding = label.GetComponent<QuestItemLinks>();
            if (binding != null && label.text == binding.linkedText)
                return;

            var matchedItems = new List<ItemProperties>();
            bool hasNativeItemLink = false;
            var text = matcher.Replace(label.text ?? "", match =>
            {
                if (match.Groups["id"].Success && nativeLinks.ContainsKey(match.Groups["id"].Value))
                {
                    hasNativeItemLink = true;
                    var content = match.Groups["content"];
                    int start = content.Index - match.Index;
                    return match.Value.Insert(start + content.Length, "</color>").Insert(start, "<color=#E9AB18>");
                }
                if (!match.Groups["item"].Success || !items.TryGetValue(match.Value, out var item))
                    return match.Value;

                var index = matchedItems.Count;
                matchedItems.Add(item);
                return $"<link=\"{LinkPrefix}{index}\"><color=#E9AB18>{match.Value}</color></link>";
            });

            bool hasLinks = matchedItems.Count > 0 || hasNativeItemLink;
            if (binding == null && !hasLinks)
                return;
            if (binding == null)
                binding = label.gameObject.AddComponent<QuestItemLinks>();

            binding.label = label;
            binding.links.Clear();
            binding.links.AddRange(matchedItems);
            binding.linkedText = text;
            if (hasLinks)
            {
                label.richText = true;
                label.text = text;
                label.raycastTarget = true;
            }
        }

        private static void BuildMatcher(ItemSettings settings, string currentLanguage)
        {
            itemSettings = settings;
            language = currentLanguage;
            items = new Dictionary<string, ItemProperties>(StringComparer.OrdinalIgnoreCase);
            nativeLinks = new Dictionary<string, ItemProperties>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in settings.ItemProperties)
            {
                if (item == null)
                    continue;
                nativeLinks[item.SurvivalGuideIdentifier] = item;
                item.ReturnNameAndNutritionalValue(out var name, out _);
                if (!string.IsNullOrWhiteSpace(name) && name.IndexOf('<') < 0 && !items.ContainsKey(name))
                    items.Add(name, item);
            }

            // Match longer names first and skip existing markup and links.
            var names = items.Keys.OrderByDescending(name => name.Length).Select(name =>
            {
                var pattern = Regex.Escape(name);
                if (name[0] <= 127 && char.IsLetterOrDigit(name[0]))
                    pattern = @"(?<![\p{L}\p{N}])" + pattern;
                if (name[name.Length - 1] <= 127 && char.IsLetterOrDigit(name[name.Length - 1]))
                    pattern += @"(?![\p{L}\p{N}])";
                return pattern;
            });
            var alternatives = string.Join("|", names);
            var markup = @"<link\s*=\s*[""']?(?<id>[^>""'\s]+)[""']?\s*>(?<content>.*?)</link\s*>|<link\b[^>]*>.*?</link\s*>|<[^>]+>";
            matcher = new Regex(alternatives.Length == 0 ? markup : markup + "|(?<item>" + alternatives + ")",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline);
        }

        internal ItemProperties FindItem(RaycastResult hit)
        {
            if (label.text != linkedText)
                return null;
            int linkIndex = TMP_TextUtilities.FindIntersectingLink(label, Input.mousePosition, hit.module.eventCamera);
            if (linkIndex < 0)
                return null;

            var id = label.textInfo.linkInfo[linkIndex].GetLinkID();
            if (nativeLinks.TryGetValue(id, out var nativeItem))
                return nativeItem;
            return id.StartsWith(LinkPrefix, StringComparison.Ordinal) &&
                int.TryParse(id.Substring(LinkPrefix.Length), out int index) && index >= 0 && index < links.Count
                ? links[index] : null;
        }

        [HarmonyPatch(typeof(QuestLogPage), nameof(QuestLogPage.Initialize))]
        private static class QuestDescription
        {
            private static void Postfix(TextMeshProUGUI ____descriptionField) => Apply(____descriptionField);
        }

        [HarmonyPatch(typeof(QuestLogObjective), nameof(QuestLogObjective.Initialize))]
        private static class QuestLogGoal
        {
            private static void Postfix(TextMeshProUGUI ____titleField) => Apply(____titleField);
        }

        [HarmonyPatch(typeof(QuestObjectiveDisplay), nameof(QuestObjectiveDisplay.UpdateDisplay))]
        private static class TrackedGoal
        {
            private static void Postfix(TextMeshProUGUI ____objectiveText) => Apply(____objectiveText);
        }
    }
}
