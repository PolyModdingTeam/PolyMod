using System.Reflection;
using HarmonyLib;
using Polytopia.Data;
using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.UI;
using Il2CppSystem.Linq;
using PolyMod.Json;
using System.Text.Json.Serialization;
using PolytopiaBackendBase.Common;
using Il2CppInterop.Runtime;
using TMPro;
using PolytopiaBackendBase;

namespace PolyMod.Managers;

/// <summary>
/// Manages visual aspects of the game, including sprites, UI, and in-game objects.
/// </summary>
public static class Visual2
{
    private const int BASIC_POPUP_LEGACY_ID = 29;
    private static Dictionary<string, string>? cachedSpriteDataReverse = null;
	private static Dictionary<string, string> cachedReversedNames = new();
    public record SpriteInfo2(float? pixelsPerUnit, Vector2? pivot);

    internal static void Init()
	{
		Harmony.CreateAndPatchAll(typeof(Visual2));
	}

	internal static void CacheSpriteNames()
	{
		cachedSpriteDataReverse = new();
		foreach (Polytopia.Data.TerrainData.Type terrainType in Enum.GetValues<Polytopia.Data.TerrainData.Type>())
        {
            string result = SpriteData.TerrainToString(terrainType);
            if(terrainType == Polytopia.Data.TerrainData.Type.Wetland)
            {
                cachedSpriteDataReverse.Add(result, EnumCache<Polytopia.Data.TerrainData.Type>.GetName(Polytopia.Data.TerrainData.Type.Field) + "_flooded");
                continue;
            }

            if(result == SpriteData.TILE_UNKNOWN)
                continue;

            cachedSpriteDataReverse.Add(result, EnumCache<Polytopia.Data.TerrainData.Type>.GetName(terrainType));
        }

        foreach (ResourceData.Type resourceType in Enum.GetValues<ResourceData.Type>())
        {
            string result = SpriteData.ResourceToString(resourceType);
            if(result == SpriteData.IMPROVEMENT_PLACEHOLDER)
                continue;

            cachedSpriteDataReverse.Add(result, EnumCache<ResourceData.Type>.GetName(resourceType));
        }

        foreach (ImprovementData.Type improvementType in Enum.GetValues<ImprovementData.Type>())
        {
            string result = SpriteData.ImprovementToString(improvementType);
            if(result == SpriteData.IMPROVEMENT_PLACEHOLDER)
                continue;

            cachedSpriteDataReverse.Add(result, EnumCache<ImprovementData.Type>.GetName(improvementType));
        }
	}
    internal static string FormatSpriteName(string baseName)
    {
		if(cachedSpriteDataReverse == null)
			CacheSpriteNames();

		if(cachedReversedNames.ContainsKey(baseName))
			return cachedReversedNames[baseName];

		string formattedName = baseName;
		foreach (var valuePair in cachedSpriteDataReverse!)
		{
			formattedName = formattedName.Replace(valuePair.Key, valuePair.Value);
		}
		cachedReversedNames[baseName] = formattedName;
        return baseName;
    }

	[HarmonyPostfix]
	[HarmonyPatch(typeof(SpriteAtlasManager), nameof(SpriteAtlasManager.DoSpriteLookup))]
	private static void SpriteAtlasManager_DoSpriteLookup(ref SpriteAtlasManager.SpriteLookupResult __result, SpriteAtlasManager __instance,
        string baseName, TribeType tribe, SkinType skin, int level)
	{
        string tribeText = (tribe == TribeType.None) ? "" : EnumCache<TribeType>.GetName(tribe);
		string skinText = (skin == SkinType.Default) ? "" : EnumCache<SkinType>.GetName(skin);

        Sprite? sprite = Registry.GetSprite2(baseName, tribeText, skinText, level);
		if (sprite != null)
			__result.sprite = sprite;

		string formattedName = FormatSpriteName(baseName);
        sprite = Registry.GetSprite2(formattedName, tribeText, skinText, level);
		if (sprite != null)
			__result.sprite = sprite;
	}

	public static BasicPopupLegacy GetBasicPopupLegacy()
	{
		WhatsNewPopup whatsNewPopup = PopupManager.GetWhatsNewPopup();
		BasicPopupLegacy original = PopupManager.instance.popupPrefabs[BASIC_POPUP_LEGACY_ID].Cast<BasicPopupLegacy>();
		BasicPopupLegacy basicPopupLegacy = UnityEngine.Object.Instantiate(original, PopupManager.instance.transform);
		basicPopupLegacy.buttonContainer = GameObject.Instantiate(whatsNewPopup.buttonContainer, basicPopupLegacy.transform);
		basicPopupLegacy.popupId = "basicPopupLegacy";
		basicPopupLegacy.popupManager = PopupManager.instance;
		basicPopupLegacy.Init();
		basicPopupLegacy.identifier = null;
		basicPopupLegacy.rectTransform.SetAsLastSibling();
		return basicPopupLegacy;
	}

	public static Sprite BuildSprite(byte[] data, Vector2? pivot = null, float pixelsPerUnit = 2112f)
	{
		Texture2D texture = new(1, 1, TextureFormat.RGBA32, true);
		texture.LoadImage(data);
		Color[] pixels = texture.GetPixels();
		for (int i = 0; i < pixels.Length; i++)
		{
			pixels[i] = new Color(pixels[i].r, pixels[i].g, pixels[i].b, pixels[i].a);
		}
		texture.SetPixels(pixels);
		texture.filterMode = FilterMode.Trilinear;
		texture.Apply();
		return BuildSpriteWithTexture(texture, pivot, pixelsPerUnit);
	}

	public static Sprite BuildSpriteWithTexture(Texture2D texture, Vector2? pivot = null, float? pixelsPerUnit = 2112f)
	{
		return Sprite.Create(
			texture,
			new(0, 0, texture.width, texture.height),
			pivot ?? new(0.5f, 0.5f),
			pixelsPerUnit ?? 2112f
		);
	}






	public class PreviewTile
	{
		[JsonInclude]
		public int? x = null;

		[JsonInclude]
		public int? y = null;

		[JsonInclude]
		[JsonConverter(typeof(EnumCacheJson<Polytopia.Data.TerrainData.Type>))]
		public Polytopia.Data.TerrainData.Type terrainType = Polytopia.Data.TerrainData.Type.None;

		[JsonInclude]
		[JsonConverter(typeof(EnumCacheJson<ResourceData.Type>))]
		public ResourceData.Type? resourceType = null;

		[JsonInclude]
		[JsonConverter(typeof(EnumCacheJson<UnitData.Type>))]
		public UnitData.Type? unitType = null;

		[JsonInclude]
		[JsonConverter(typeof(EnumCacheJson<ImprovementData.Type>))]
		public ImprovementData.Type? improvementType = null;
	}

	public record PreviewInfo(int arrayIdx, SaveStateData saveStateData, PreviewTile[] customPreview);
	private static float? baseOrthographicCameraSize = null;
	private static bool isTakingSnapshot = false;


	[HarmonyPostfix]
	[HarmonyPatch(typeof(TribePreviewRegistry), nameof(TribePreviewRegistry.PreloadTribePreviews))]
	public static void PreloadTribePreviews()
	{
		Dictionary<string, PreviewInfo> previewSaveStateDatas = new();

		SaveStateData fallbackSaveStateData = TribePreviewRegistry.saveStateDatas[TribePreviewRegistry.fallbackStateIndex];
		foreach(string tribe in Registry.tribePreviews.Keys)
		{
			string previewId = GetTribePreviewName(tribe);
			PreviewTile[] customPreview = Registry.tribePreviews2[tribe];
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
			}

			saveStateData = ScriptableObject.CreateInstance<SaveStateData>();
			saveStateData.name = previewId;
			saveStateData.byteArray = DiskSerializationHelpers.ToLZ4CompressedByteArray(clientSerializationWrapper, version);
		}

		return saveStateData;
	}
}