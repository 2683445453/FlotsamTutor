using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace FlotsamTutor
{
    internal static class ItemUses
    {
        internal static IEnumerable<(string Location, float Cost, IReadOnlyList<CountedItemProperty> Output)> Salvage(ItemProperties item)
        {
            foreach (var target in Acquisition.LandmarkTargets())
            {
                var category = target.Salvage.ReturnCategoryAsset(target.Variant);
                if (category == null || category.RequiredItem.ItemProperties != item || category.RequiredItem.Amount <= 0)
                    continue;
                var output = target.Salvage.ReturnAssetComposition(target.Variant)?.Where(entry =>
                    entry != null && entry.ItemProperties != null && entry.Amount > 0).ToArray();
                if (output?.Length > 0)
                    yield return (target.Location, category.RequiredItem.Amount, output);
            }
        }

        internal static IEnumerable<(BuildableProperties Building, ModuleProperties Module, int Amount, bool Available)>
            Upgrades(ItemProperties item, IEnumerable<Buildable> nearestBuildings)
        {
            var existing = nearestBuildings.ToLookup(building => building.Properties);
            var moduleCost = AccessTools.Field(typeof(ModuleProperties), "_cost");
            foreach (var properties in GameManager.Settings.BuildableSettings.Buildables)
            {
                if (properties == null || properties.Prefab == null)
                    continue;
                if (properties.Upgrade != null)
                {
                    int amount = Count(properties.UpgradeResources, item);
                    if (amount > 0)
                        yield return (properties, null, amount, existing.Contains(properties) && properties.Upgrade.IsUnlocked());
                }
                if (properties.Modules == null)
                    continue;
                foreach (var module in properties.Modules.Where(module => module != null).Distinct())
                {
                    int baseAmount = Count((CountedItemProperty[])moduleCost.GetValue(module), item);
                    if (baseAmount == 0)
                        continue;
                    var target = module.IsUnlocked() ? existing[properties].FirstOrDefault(building =>
                        building.TryReturnBuildableExtendable<ModuleManager>(out var manager) &&
                        !manager.IsActiveModule(module)) : null;
                    var costBuilding = target ?? properties.Prefab;
                    int amount = costBuilding.Properties != null ? Count(module.GetCost(costBuilding, false), item) : baseAmount;
                    if (amount > 0)
                        yield return (properties, module, amount, target != null);
                }
            }
        }

        internal static IEnumerable<(string Kind, string Detail, BuildableProperties Building)> Other(ItemProperties item)
        {
            bool isMedication = (item.Tags & Item.Tags.Medicine) != 0 &&
                Resources.FindObjectsOfTypeAll<Disease>().Any(disease => disease != null && disease.Medication == item);
            foreach (var building in GameManager.Settings.BuildableSettings.Buildables)
            {
                if (building == null || building.Prefab == null)
                    continue;
                var prefab = building.Prefab;
                if (prefab.TryGetComponent<EnergyItemProducer>(out var generator) && generator.EnergyItemProperties == item)
                    yield return ("燃料", $"每份燃烧 {generator.MaxBurnTime:0.##} 秒", building);
                if (prefab.TryGetComponent<AquaFarm>(out var farm))
                    foreach (var fish in farm.AvailableFishProperties)
                        if (fish != null && fish.FeedItemProperties == item)
                            yield return ("消耗品", $"养殖饲料 · {fish.HarvestProperties?.LocalizedName}", building);
                if (prefab.TryGetComponent<BirdHouse>(out var birds) && birds.FoodSource == item)
                    yield return ("消耗品", "鸟类饲料", building);
                if (isMedication && prefab.TryGetComponent<MedPod>(out _))
                    yield return ("消耗品", "治疗药品", building);
            }

            if ((item.Tags & Item.Tags.Food) != 0)
                yield return ("消耗品", "漂流者食用", null);
            if ((item.Tags & Item.Tags.Drink) != 0)
                yield return ("消耗品", "漂流者饮用", null);
        }

        private static int Count(IEnumerable<CountedItemProperty> entries, ItemProperties item) =>
            entries?.Where(entry => entry != null && entry.ItemProperties == item && entry.Amount > 0)
                .Sum(entry => entry.Amount) ?? 0;
    }
}
