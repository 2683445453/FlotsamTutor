using System;
using System.Reflection;
using HarmonyLib;
using PajamaLlama.Flotsam.World;
using UnityEngine;

namespace FlotsamTutor
{
    public sealed partial class TutorPanel
    {
        private static class SailingTerrain
        {
            private static readonly FieldInfo Tiles = AccessTools.Field(typeof(TerrainManager), "_tiles");
            private static readonly FieldInfo Region = AccessTools.Field(Tiles.FieldType.GetElementType(), "RegionType");
            private static readonly FieldInfo Tile = AccessTools.Field(Tiles.FieldType.GetElementType(), "Tile");
            private static readonly MethodInfo Pool = AccessTools.Method(typeof(TerrainManager), "PoolTile");
            private static readonly Action<TerrainManager, int, int> InitializeTile =
                AccessTools.MethodDelegate<Action<TerrainManager, int, int>>(
                    AccessTools.Method(typeof(TerrainManager), "InitializeTileAtPosition"));
            private static readonly Func<TerrainManager, int, int, Vector3> SpawnPosition =
                AccessTools.MethodDelegate<Func<TerrainManager, int, int, Vector3>>(
                    AccessTools.Method(typeof(TerrainManager), "GetTileSpawnPosition"));
            private static readonly Func<TerrainManager, Vector3, WorldRegionType> RegionAt =
                AccessTools.MethodDelegate<Func<TerrainManager, Vector3, WorldRegionType>>(
                    AccessTools.Method(typeof(TerrainManager), "GetRegionFromTileSpawnPositon"));

            private static TerrainManager terrain;
            private static Vector2 offset;

            internal static void RestoreGrid()
            {
                if (terrain == null || offset == Vector2.zero)
                    return;

                offset = Vector2.zero;
                var tiles = (Array)Tiles.GetValue(terrain);
                for (int x = 0; x < tiles.GetLength(0); x++)
                for (int y = 0; y < tiles.GetLength(1); y++)
                {
                    var tile = (GameObject)Tile.GetValue(tiles.GetValue(x, y));
                    if (tile != null)
                        tile.transform.position = SpawnPosition(terrain, x, y);
                }
            }

            internal static void Advance(Vector3 displacement)
            {
                if (terrain == null)
                    return;
                var settings = GameManager.Settings.GameplaySettings.TerrainProperties;
                if (settings.TileWidth <= 0 || settings.TileLength <= 0)
                    return;
                var tiles = (Array)Tiles.GetValue(terrain);
                int width = tiles.GetLength(0);
                int height = tiles.GetLength(1);
                offset -= new Vector2(displacement.x, displacement.z);
                int shiftX = Mathf.RoundToInt(offset.x / settings.TileWidth);
                int shiftY = Mathf.RoundToInt(offset.y / settings.TileLength);
                offset -= new Vector2(shiftX * settings.TileWidth, shiftY * settings.TileLength);

                if (shiftX != 0 || shiftY != 0)
                {
                    var shifted = Array.CreateInstance(Tiles.FieldType.GetElementType(), width, height);
                    for (int x = 0; x < width; x++)
                    for (int y = 0; y < height; y++)
                    {
                        var entry = tiles.GetValue(x, y);
                        int nextX = x + shiftX;
                        int nextY = y + shiftY;
                        if ((uint)nextX < (uint)width && (uint)nextY < (uint)height)
                            shifted.SetValue(entry, nextX, nextY);
                        else if ((GameObject)Tile.GetValue(entry) != null)
                            Pool.Invoke(terrain, new[] { entry });
                    }
                    Tiles.SetValue(terrain, shifted);
                    tiles = shifted;
                }

                for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                {
                    var tile = (GameObject)Tile.GetValue(tiles.GetValue(x, y));
                    if (tile != null)
                        tile.transform.position = SpawnPosition(terrain, x, y);
                    else if (shiftX != 0 || shiftY != 0)
                        InitializeTile(terrain, x, y);
                }
            }

            [HarmonyPatch(typeof(TerrainManager), nameof(TerrainManager.Initialize))]
            private static class TrackTerrain
            {
                private static void Postfix(TerrainManager __instance)
                {
                    terrain = __instance;
                    offset = Vector2.zero;
                }
            }

            [HarmonyPatch(typeof(TerrainManager), "GetTileSpawnPosition")]
            private static class ApplyTileOffset
            {
                private static void Postfix(TerrainManager __instance, ref Vector3 __result)
                {
                    if (__instance == terrain)
                        __result += new Vector3(offset.x, 0, offset.y);
                }
            }

            [HarmonyPatch(typeof(TerrainManager), "OnTownheartMoved")]
            private static class ResetOffsetForNativeMovement
            {
                private static void Prefix(TerrainManager __instance)
                {
                    if (__instance == terrain && !synchronizingSail)
                        offset = Vector2.zero;
                }
            }

            [HarmonyPatch(typeof(TerrainManager), "InitializeTileAtPosition")]
            private static class KeepSailingTerrainTile
            {
                private static bool Prefix(TerrainManager __instance, int x, int y)
                {
                    if (__instance != terrain || !synchronizingSail)
                        return true;
                    var current = ((Array)Tiles.GetValue(__instance)).GetValue(x, y);
                    return (GameObject)Tile.GetValue(current) == null ||
                        (WorldRegionType)Region.GetValue(current) != RegionAt(__instance, SpawnPosition(__instance, x, y));
                }
            }
        }

        private static readonly int[] SurfaceTextureIds =
        {
            Shader.PropertyToID("_Surface1"), Shader.PropertyToID("_Surface2"),
            Shader.PropertyToID("_Surface2BlendMask"), Shader.PropertyToID("_HeightFoamTex")
        };

        private sealed class SailingTexture
        {
            internal int Property;
            internal Vector2 OriginalOffset;

            internal Vector2 Phase;
            internal Vector2 RepeatsPerUnit;
        }

        private readonly System.Collections.Generic.List<SailingTexture> sailingTextures =
            new System.Collections.Generic.List<SailingTexture>(SurfaceTextureIds.Length);
        private Material sailingWaterMaterial;
        private World sailingWaterWorld;
        private Vector2 pendingWaterTravel;
        private bool experimentalSailingTextures;

        private void AdvanceSailSurface(Vector3 displacement)
        {
            SailingTerrain.Advance(displacement);
            pendingWaterTravel += new Vector2(displacement.x, displacement.z);
        }

        private void UpdateSailSurface()
        {
            if (!experimentalSailingTextures)
            {
                ResetSailSurface();
                return;
            }

            var world = GameManager.WorldManager?.World;
            if (sailingWaterWorld != world)
            {
                RestoreSailingTextures();
                sailingWaterWorld = world;
                pendingWaterTravel = Vector2.zero;
            }
            if (world == null || LoadingScreen.IsLoading || UIManager.State == UIState.Architect)
            {
                RestoreSailingTextures();
                pendingWaterTravel = Vector2.zero;
                return;
            }

            var displacement = pendingWaterTravel;
            pendingWaterTravel = Vector2.zero;
            if (displacement.sqrMagnitude == 0)
                return;

            var material = WaterManager.ReturnMaterial();
            if (sailingWaterMaterial != material)
            {
                RestoreSailingTextures();
                if (material == null || material.shader == null || material.shader.name != "URPWater")
                    return;

                sailingWaterMaterial = material;
                foreach (int property in SurfaceTextureIds)
                {
                    if (!material.HasProperty(property) || !(material.GetTexture(property) is Texture2D source) ||
                        source.wrapMode != TextureWrapMode.Repeat)
                        continue;

                    sailingTextures.Add(new SailingTexture
                    {
                        Property = property,
                        OriginalOffset = material.GetTextureOffset(property),
                        RepeatsPerUnit = material.GetTextureScale(property) / 340f
                    });
                }
            }

            // Keep the native textures bound throughout rendering.
            foreach (var surface in sailingTextures)
            {
                var phase = surface.Phase + Vector2.Scale(displacement, surface.RepeatsPerUnit);
                surface.Phase = new Vector2(Mathf.Repeat(phase.x, 1), Mathf.Repeat(phase.y, 1));
                sailingWaterMaterial.SetTextureOffset(surface.Property, surface.OriginalOffset + surface.Phase);
            }
        }

        private void OnDestroy() => ResetSailSurface();

        private void ResetSailSurface()
        {
            SailingTerrain.RestoreGrid();
            RestoreSailingTextures();
            sailingWaterWorld = null;
            pendingWaterTravel = Vector2.zero;
        }

        private void RestoreSailingTextures()
        {
            if (sailingWaterMaterial != null)
                foreach (var surface in sailingTextures)
                    sailingWaterMaterial.SetTextureOffset(surface.Property, surface.OriginalOffset);
            sailingTextures.Clear();
            sailingWaterMaterial = null;
        }
    }
}
