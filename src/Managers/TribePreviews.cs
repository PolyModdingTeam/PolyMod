using HarmonyLib;
using Polytopia.Data;
using UnityEngine;
using Il2CppSystem.Linq;
using PolyMod.Json;
using System.Text.Json.Serialization;
using PolytopiaBackendBase.Common;
using TMPro;

namespace PolyMod.Managers;

public static class TribePreviews
{
    /// <summary>
	/// Represents a tile in a tribe preview.
	/// </summary>
	public class PreviewTile
	{
		/// <summary>The x-coordinate of the tile.</summary>
		[JsonInclude]
		public int? x = null;
		/// <summary>The y-coordinate of the tile.</summary>
		[JsonInclude]
		public int? y = null;
		/// <summary>The terrain type of the tile.</summary>
		[JsonInclude]
		[JsonConverter(typeof(EnumCacheJson<Polytopia.Data.TerrainData.Type>))]
		public Polytopia.Data.TerrainData.Type terrainType = Polytopia.Data.TerrainData.Type.None;
		/// <summary>The resource type on the tile.</summary>
		[JsonInclude]
		[JsonConverter(typeof(EnumCacheJson<ResourceData.Type>))]
		public ResourceData.Type? resourceType = null;
		/// <summary>The unit type on the tile.</summary>
		[JsonInclude]
		[JsonConverter(typeof(EnumCacheJson<UnitData.Type>))]
		public UnitData.Type? unitType = null;
		/// <summary>The improvement type on the tile.</summary>
		[JsonInclude]
		[JsonConverter(typeof(EnumCacheJson<ImprovementData.Type>))]
		public ImprovementData.Type? improvementType = null;
		/// <summary>The effect types on the tile.</summary>
		[JsonInclude]
		[JsonConverter(typeof(EnumCacheListJson<TileData.EffectType>))]
		public List<TileData.EffectType> tileEffects = new();
	}

    public record PreviewInfo(int arrayIdx, SaveStateData saveStateData, PreviewTile[] customPreview);

	private static bool isTakingSnapshot = false;
	private static float? baseOrthographicCameraSize = null;

	[HarmonyPostfix]
	[HarmonyPatch(typeof(TribePreviewRegistry), nameof(TribePreviewRegistry.PreloadTribePreviews))]
	public static void PreloadTribePreviews()
	{
		Dictionary<string, PreviewInfo> previewSaveStateDatas = new();

		SaveStateData fallbackSaveStateData = TribePreviewRegistry.saveStateDatas[TribePreviewRegistry.fallbackStateIndex];
		foreach(string tribe in Registry.tribePreviews.Keys)
		{
			string previewId = GetTribePreviewName(tribe);
			PreviewTile[] customPreview = Registry.tribePreviews[tribe];
			previewSaveStateDatas[previewId] = new(TribePreviewRegistry.fallbackStateIndex, fallbackSaveStateData, customPreview);
		}

		for (int i = 0; i < TribePreviewRegistry.saveStateDatas.Length; i++)
		{
			SaveStateData saveStateData = TribePreviewRegistry.saveStateDatas[i];
			if(previewSaveStateDatas.ContainsKey(saveStateData.name))
			{
				previewSaveStateDatas[saveStateData.name] = new(i, saveStateData, previewSaveStateDatas[saveStateData.name].customPreview);
			}
		}

		foreach (string previewId in previewSaveStateDatas.Keys)
		{
			var data = previewSaveStateDatas[previewId];
			SaveStateData? customSaveStateData = ApplyCustomPreview(previewId, data.saveStateData, data.customPreview);
			if(customSaveStateData == null)
				continue;

			if(data.arrayIdx == TribePreviewRegistry.fallbackStateIndex)
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

	private static string GetTribePreviewName(string tribeType)
	{
		return "worldpreview_" + tribeType;
	}

	private static SaveStateData? ApplyCustomPreview(string previewId, SaveStateData originalPreview, PreviewTile[] preview)
	{
		SaveStateData? saveStateData = null;
		if (DiskSerializationHelpers.FromLZ4CompressedByteArray<ClientSerializationWrapper>(
			originalPreview.byteArray, out ClientSerializationWrapper clientSerializationWrapper, out int version))
		{
			GameState currentGameState = clientSerializationWrapper.GetCurrentGameState();
			foreach (var previewTile in preview)
			{
				if(previewTile.x == null || previewTile.y == null)
					continue;

				WorldCoordinates coordinates = new((int)previewTile.x, (int)previewTile.y);
				TileData? tileData = currentGameState.Map.GetTile(coordinates);
				if(tileData == null)
					continue;

				if(previewTile.terrainType != Polytopia.Data.TerrainData.Type.None)
					tileData.terrain = previewTile.terrainType;

				if(previewTile.resourceType != null)
				{
					ResourceState? resourceState = null;
					ResourceData.Type resourceType = (ResourceData.Type)previewTile.resourceType;
					if(resourceType != ResourceData.Type.None)
					{
						resourceState = new ResourceState { type = resourceType };
					}
					tileData.resource = resourceState;
				}

				if(previewTile.unitType != null)
				{
					tileData.unit = null;
					if(previewTile.unitType != Polytopia.Data.UnitData.Type.None &&
						currentGameState.TryGetPlayer(currentGameState.CurrentPlayer, out PlayerState playerState) &&
						currentGameState.GameLogicData.TryGetData((UnitData.Type)previewTile.unitType, out UnitData unitData))
					{
						UnitState unitState = ActionUtils.TrainUnit(currentGameState, playerState, tileData, unitData);
						unitState.moved = false;
						unitState.attacked = false;
					}
				}


				if(previewTile.improvementType != null)
				{
					tileData.improvement = null;
					ImprovementData.Type improvementType = (ImprovementData.Type)previewTile.improvementType;
					if(
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

	[HarmonyPrefix]
	[HarmonyPatch(typeof(SpriteCamera), nameof(SpriteCamera.TakeSnapshotOfMapState))]
	private static bool SpriteCamera_TakeSnapshotOfMapState(ref Texture2D __result, SpriteCamera.SnapshotMapData mapSnapshot,
		float w, float h, TribeType tribe, SkinType skin, TribeType climate, int color, bool clearMapAfterwards)
	{
		if(!Plugin.config.debug)
			return true;

		var instance = SpriteCamera.instance;
		if(baseOrthographicCameraSize == null)
			baseOrthographicCameraSize = instance.spriteCamera.orthographicSize;

		instance.spriteCamera.orthographicSize = (float)baseOrthographicCameraSize * 2;
		isTakingSnapshot = true;

		return true;
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(SpriteCamera), nameof(SpriteCamera.TakeSnapshotOfMapState))]
	private static void SpriteCamera_TakeSnapshotOfMapState_Postfix(ref Texture2D __result, SpriteCamera.SnapshotMapData mapSnapshot,
		float w, float h, TribeType tribe, SkinType skin, TribeType climate, int color, bool clearMapAfterwards)
	{
		if(isTakingSnapshot)
			isTakingSnapshot = false;
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(Tile), nameof(Tile.RenderDebug))]
	public static void Tile_RenderDebug(Tile __instance)
	{
		if(!isTakingSnapshot)
			return;

		GameObject textObj = new GameObject("CoordinateText");
		textObj.transform.SetParent(__instance.transform);
		textObj.transform.localPosition = new Vector3(0, 0.0f, 0);

		TextMeshPro textMesh = textObj.AddComponent<TextMeshPro>();
		textMesh.text = __instance.Data.coordinates.ToString();
		textMesh.fontSize = 3;
		textMesh.alignment = TextAlignmentOptions.Center;
		textMesh.color = Color.white;

		MeshRenderer renderer = textObj.GetComponent<MeshRenderer>();
		renderer.sortingLayerID = MeshCache.TERRAIN_LAYER_ID;
		renderer.sortingOrder = __instance.Depth + 10;
	}
}