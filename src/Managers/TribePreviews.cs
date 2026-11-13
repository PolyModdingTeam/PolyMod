using HarmonyLib;
using Polytopia.Data;
using UnityEngine;
using Il2CppSystem.Linq;
using PolyMod.Json;
using System.Text.Json.Serialization;
using PolytopiaBackendBase.Common;

namespace PolyMod.Managers;

/// <summary>
/// The manager for the world previews that the tribe selection screen shows.
/// It changes the preview maps of tribes with the preview tiles from mods.
/// </summary>
public static class TribePreviews
{
    /// <summary>
    /// The changes to one tile of a tribe preview map. A mod gives these changes in the <c>preview</c> field of a tribe.
    /// </summary>
    public class PreviewTile
    {
        /// <summary>The x coordinate of the tile. If it is null, PolyMod does not change a tile.</summary>
        [JsonInclude]
        public int? x = null;
        /// <summary>The y coordinate of the tile. If it is null, PolyMod does not change a tile.</summary>
        [JsonInclude]
        public int? y = null;
        /// <summary>The new terrain type of the tile. If it is <c>None</c>, PolyMod keeps the terrain.</summary>
        [JsonInclude]
        [JsonConverter(typeof(EnumCacheJson<Polytopia.Data.TerrainData.Type>))]
        public Polytopia.Data.TerrainData.Type terrainType = Polytopia.Data.TerrainData.Type.None;
        /// <summary>
        /// The new resource type on the tile. If it is null, PolyMod keeps the resource.
        /// If it is <c>None</c>, PolyMod removes the resource.
        /// </summary>
        [JsonInclude]
        [JsonConverter(typeof(EnumCacheJson<ResourceData.Type>))]
        public ResourceData.Type? resourceType = null;
        /// <summary>
        /// The new unit type on the tile. If it is null, PolyMod keeps the unit.
        /// If it is <c>None</c>, PolyMod removes the unit.
        /// </summary>
        [JsonInclude]
        [JsonConverter(typeof(EnumCacheJson<UnitData.Type>))]
        public UnitData.Type? unitType = null;
        /// <summary>
        /// The new improvement type on the tile. If it is null, PolyMod keeps the improvement.
        /// If it is <c>None</c>, PolyMod removes the improvement.
        /// </summary>
        [JsonInclude]
        [JsonConverter(typeof(EnumCacheJson<ImprovementData.Type>))]
        public ImprovementData.Type? improvementType = null;
        /// <summary>
        /// The new effect types on the tile. If the list is empty, PolyMod keeps the effects.
        /// If not, the list replaces all effects of the tile.
        /// </summary>
        [JsonInclude]
        [JsonConverter(typeof(EnumCacheListJson<TileData.EffectType>))]
        public List<TileData.EffectType> tileEffects = new();
    }

    /// <summary>
    /// The data that PolyMod uses to make the preview of one tribe.
    /// </summary>
    /// <param name="arrayIdx">
    /// The index of the preview in <c>TribePreviewRegistry.saveStateDatas</c>.
    /// For a tribe without a game preview, it is the index of the fallback preview.
    /// </param>
    /// <param name="saveStateData">The game preview that PolyMod changes.</param>
    /// <param name="customPreview">The tile changes from the mod.</param>
    public record PreviewInfo(int arrayIdx, SaveStateData saveStateData, PreviewTile[] customPreview);

    /// <summary>
    /// A Harmony postfix on <c>TribePreviewRegistry.PreloadTribePreviews</c>.
    /// It applies the preview tiles in <see cref="Registry.tribePreviews"/> to the preview of each tribe.
    /// The preview of a tribe has the name <c>worldpreview_</c> and the tribe name.
    /// If the game has a preview with that name, the patch replaces it with the changed copy.
    /// If not, the patch changes a copy of the fallback preview and adds it to the list of previews.
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(TribePreviewRegistry), nameof(TribePreviewRegistry.PreloadTribePreviews))]
    public static void PreloadTribePreviews()
    {
        Dictionary<string, PreviewInfo> previewSaveStateDatas = new();

        SaveStateData fallbackSaveStateData = TribePreviewRegistry.saveStateDatas[TribePreviewRegistry.fallbackStateIndex];
        foreach (string tribe in Registry.tribePreviews.Keys)
        {
            string previewId = "worldpreview_" + tribe;
            PreviewTile[] customPreview = Registry.tribePreviews[tribe];
            previewSaveStateDatas[previewId] = new(TribePreviewRegistry.fallbackStateIndex, fallbackSaveStateData, customPreview);
        }

        for (int i = 0; i < TribePreviewRegistry.saveStateDatas.Length; i++)
        {
            SaveStateData saveStateData = TribePreviewRegistry.saveStateDatas[i];
            if (previewSaveStateDatas.TryGetValue(saveStateData.name, out PreviewInfo? existingPreview))
            {
                previewSaveStateDatas[saveStateData.name] = new(i, saveStateData, existingPreview.customPreview);
            }
        }

        foreach (string previewId in previewSaveStateDatas.Keys)
        {
            var data = previewSaveStateDatas[previewId];
            SaveStateData? customSaveStateData = ApplyCustomPreview(previewId, data.saveStateData, data.customPreview);
            if (customSaveStateData == null)
                continue;

            if (data.arrayIdx == TribePreviewRegistry.fallbackStateIndex)
            {
                List<SaveStateData> saveStateDatas = TribePreviewRegistry.saveStateDatas.ToList();
                saveStateDatas.Add(customSaveStateData);
                TribePreviewRegistry.saveStateDatas = saveStateDatas.ToArray();
            }
            else
            {
                TribePreviewRegistry.saveStateDatas[data.arrayIdx] = customSaveStateData;
            }
        }
    }

    /// <summary>
    /// Makes a changed copy of a preview with the given tile changes.
    /// The method reads the game state from the compressed bytes of the preview and changes the tiles.
    /// It skips a tile change without coordinates or with coordinates that are not on the map.
    /// A new unit belongs to the current player and can move and attack.
    /// A new improvement starts at level 0, and the current player is its founder.
    /// </summary>
    /// <param name="previewId">The name of the new preview.</param>
    /// <param name="originalPreview">The preview to copy. The method does not change it.</param>
    /// <param name="preview">The tile changes to apply.</param>
    /// <returns>The new preview, or null if the method cannot read the bytes of the original preview.</returns>
    private static SaveStateData? ApplyCustomPreview(string previewId, SaveStateData originalPreview, PreviewTile[] preview)
    {
        SaveStateData? saveStateData = null;
        if (DiskSerializationHelpers.FromLZ4CompressedByteArray<ClientSerializationWrapper>(
            originalPreview.byteArray, out ClientSerializationWrapper clientSerializationWrapper, out int version))
        {
            GameState currentGameState = clientSerializationWrapper.GetCurrentGameState();
            foreach (var previewTile in preview)
            {
                if (previewTile.x == null || previewTile.y == null)
                    continue;

                WorldCoordinates coordinates = new((int)previewTile.x, (int)previewTile.y);
                TileData? tileData = currentGameState.Map.GetTile(coordinates);
                if (tileData == null)
                    continue;

                if (previewTile.terrainType != Polytopia.Data.TerrainData.Type.None)
                    tileData.terrain = previewTile.terrainType;

                if (previewTile.resourceType != null)
                {
                    ResourceState? resourceState = null;
                    ResourceData.Type resourceType = (ResourceData.Type)previewTile.resourceType;
                    if (resourceType != ResourceData.Type.None)
                    {
                        resourceState = new ResourceState { type = resourceType };
                    }
                    tileData.resource = resourceState;
                }

                if (previewTile.unitType != null)
                {
                    tileData.unit = null;
                    if (previewTile.unitType != Polytopia.Data.UnitData.Type.None &&
                        currentGameState.TryGetPlayer(currentGameState.CurrentPlayer, out PlayerState playerState) &&
                        currentGameState.GameLogicData.TryGetData((UnitData.Type)previewTile.unitType, out UnitData unitData))
                    {
                        UnitState unitState = ActionUtils.TrainUnit(currentGameState, playerState, tileData, unitData);
                        unitState.moved = false;
                        unitState.attacked = false;
                    }
                }


                if (previewTile.improvementType != null)
                {
                    tileData.improvement = null;
                    ImprovementData.Type improvementType = (ImprovementData.Type)previewTile.improvementType;
                    if (
                        previewTile.improvementType != ImprovementData.Type.None &&
                        currentGameState.GameLogicData.TryGetData(
                            improvementType,
                            out ImprovementData improvementData)
                        )
                    {
                        tileData.improvement = new ImprovementState
                        {
                            type = improvementData.type,
                            borderSize = (ushort)improvementData.borderSize,
                            level = 0,
                            xp = 0,
                            production = 1,
                            founded = (ushort)currentGameState.CurrentTurn,
                            baseScore = (ushort)improvementData.GetScoreReward(),
                            founder = currentGameState.CurrentPlayer
                        };
                    }
                }

                if (previewTile.tileEffects.Count > 0)
                    tileData.effects = new();

                foreach (TileData.EffectType tileEffect in previewTile.tileEffects)
                {
                    tileData.AddEffect(tileEffect);
                }
            }

            saveStateData = ScriptableObject.CreateInstance<SaveStateData>();
            saveStateData.name = previewId;
            saveStateData.byteArray = DiskSerializationHelpers.ToLZ4CompressedByteArray(clientSerializationWrapper, version);
        }

        return saveStateData;
    }

    /// <summary>
    /// A Harmony postfix on <c>SelectTribePopup.SetBackground</c>.
    /// If the <c>showCoordinates</c> setting is on, it replaces the world preview image with a new snapshot.
    /// The snapshot uses two times the camera size, so it shows a larger area of the preview map.
    /// If the snapshot fails, the patch writes a warning to the log.
    /// </summary>
    /// <param name="__instance">The tribe selection popup.</param>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(SelectTribePopup), nameof(SelectTribePopup.SetBackground))]
    private static void SelectTribePopup_SetBackground(SelectTribePopup __instance)
    {
        if (!Plugin.config.showCoordinates)
            return;

        try
        {
            TribeType tribeType = (__instance.mixTribeData != null) ? __instance.mixTribeData.type : __instance.tribeData.type;

            __instance.mapSnapshot = new SpriteCamera.SnapshotMapData();
            __instance.mapSnapshot.LoadMapState(
                TribePreviewRegistry.GetTribePreviewData(tribeType, __instance.SkinType)
            );

            float width = __instance.worldPreviewRawImage.rectTransform.GetWidth();
            float height = __instance.worldPreviewRawImage.rectTransform.GetHeight();

            int tribeColor = PolytopiaDataManager.GetGameLogicData(
                VersionManager.GameLogicDataVersion
            ).GetTribeColor(
                tribeType,
                __instance.SkinType
            );

            var instance = SpriteCamera.instance;
            float baseOrthographicCameraSize = instance.spriteCamera.orthographicSize;

            instance.spriteCamera.orthographicSize = (float)baseOrthographicCameraSize * 2;

            Texture2D texture2D = SpriteCamera.TakeSnapshotOfMapState(
                __instance.mapSnapshot,
                width,
                height,
                tribeType,
                __instance.SkinType,
                tribeType,
                tribeColor
            );

            instance.spriteCamera.orthographicSize = baseOrthographicCameraSize;

            __instance.worldPreviewRawImage.texture = texture2D;
            __instance.worldPreviewRawImage.color = Color.white;
        }
        catch (Exception ex)
        {
            Plugin.logger.LogWarning($"Failed to load debug tribe preview {ex.Message}");
        }
    }

    /// <summary>
    /// Applies all Harmony patches in the <see cref="TribePreviews"/> class.
    /// </summary>
    internal static void Init()
    {
        Harmony.CreateAndPatchAll(typeof(TribePreviews));
    }
}
