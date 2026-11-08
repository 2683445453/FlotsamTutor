using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace FlotsamTutor
{
    internal static class Acquisition
    {
        internal static IEnumerable<(BuildableProperties Station, ProductionRecipeProperties Recipe)> Recipes(ItemProperties item)
        {
            foreach (var station in GameManager.Settings.BuildableSettings.Buildables)
            {
                if (station == null || station.Prefab == null || !station.Prefab.TryGetComponent<Producer>(out var producer))
                    continue;
                foreach (var recipe in producer.ProductionProperties.Recipes)
                    if (recipe != null && recipe.ReturnProducesItem(item))
                        yield return (station, recipe);
            }
        }

        internal static IEnumerable<string> Sources(ItemProperties item)
        {
            var locations = new HashSet<string>();
            AddLandmarks(item, locations);
            AddFloatingDebris(item, locations);
            if ((item.Tags & Item.Tags.FishMarker) != 0 && locations.Count == 0)
                locations.Add("海上鱼群");
            if (GameManager.Settings.SessionSettings.StartingScenario.StartingResources
                .Any(entry => entry.ItemProperties == item && entry.Amount > 0))
                locations.Add("开局物资");
            if (GameManager.Settings.BuildableSettings.Decorations.OfType<CropDecorationProperties>()
                .Any(crop => crop.Yield?.ItemProperties == item))
                locations.Add("种植区");
            foreach (var station in GameManager.Settings.BuildableSettings.Buildables)
            {
                if (station != null && station.Prefab != null && station.Prefab.TryGetComponent<AquaFarm>(out var farm) &&
                    farm.AvailableFishProperties.Any(fish => fish != null && fish.HarvestProperties == item))
                    locations.Add(station.Name);
            }

            return locations.OrderBy(location => location);
        }

        private static void AddLandmarks(ItemProperties item, HashSet<string> locations)
        {
            var prefabField = AccessTools.Field(typeof(LandmarkBehaviour), "_landmarkPrefab");
            var variationsField = AccessTools.Field(typeof(LandmarkSalvageable), "_variations");
            foreach (var landmark in Resources.FindObjectsOfTypeAll<LandmarkBehaviour>())
            {
                if (landmark == null || !(prefabField.GetValue(landmark) is Landmark prefab) || prefab == null)
                    continue;
                foreach (var salvage in prefab.GetComponentsInChildren<LandmarkSalvageable>(true))
                {
                    var variations = (LandmarkSalvageable[])variationsField.GetValue(salvage);
                    for (int i = -1; i < (variations?.Length ?? 0); i++)
                    {
                        var target = i < 0 ? salvage : variations[i];
                        if (target != null && salvage.ReturnAssetComposition(i)?.Any(entry =>
                            entry != null && entry.ItemProperties == item && entry.Amount > 0) == true)
                        {
                            string site = landmark.Name;
                            string landmarkObject = LandmarkObjectName(target.name);
                            if (landmarkObject != null)
                                site += " · " + landmarkObject;
                            locations.Add(site);
                        }
                    }
                }
            }
        }

        private static string LandmarkObjectName(string name)
        {
            string lower = name.ToLowerInvariant();
            if (lower.Contains("windmill") || lower.Contains("turbine")) return "风车";
            if (lower.Contains("truck") || lower.Contains("lorry")) return "卡车";
            if (lower.Contains("car") && !lower.Contains("cargo")) return "小汽车";
            return null;
        }

        private static void AddFloatingDebris(ItemProperties item, HashSet<string> locations)
        {
            var directField = AccessTools.Field(typeof(PointOfInterestProperties), "_flotsamItems");
            var compositeField = AccessTools.Field(typeof(PointOfInterestProperties), "_compositedFlotsam");
            foreach (var point in Resources.FindObjectsOfTypeAll<PointOfInterestProperties>())
            {
                if (point == null)
                    continue;
                var direct = (List<WeightedItemProperty>)directField.GetValue(point);
                var composite = (List<WeightedCompositeProperty>)compositeField.GetValue(point);
                if (direct.Any(entry => entry.ItemProperties == item) ||
                    composite.Any(entry => entry.CompositedFlotsamProperties != null &&
                        entry.CompositedFlotsamProperties.Composition.Any(part => part.ItemProperties == item && part.Amount > 0)))
                    locations.Add(string.IsNullOrWhiteSpace(point.Title) ? "海上漂浮物" : (string)point.Title);
            }
        }
    }
}
