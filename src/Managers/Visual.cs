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

namespace PolyMod.Managers;

/// <summary>
/// Manages visual aspects of the game, including sprites, UI, and in-game objects.
/// </summary>
public static class Visual
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
	}

	/// <summary>Represents information about a sprite, such as its pivot and pixels per unit.</summary>
	public record SpriteInfo(float? pixelsPerUnit, Vector2? pivot);

	/// <summary>Represents information about a custom skin.</summary>
	public record SkinInfo(int idx, string id, SkinData? skinData);

	/// <summary>A dictionary of custom widths for basic popups.</summary>
	public static Dictionary<int, int> basicPopupWidths = new();

	private const int BASIC_POPUP_LEGACY_ID = 29;
	/// <summary>
	/// A dictionary of skin types of tribes, which can flood tiles, keyed by custom flood tile effect.
	/// </summary>
	internal static Dictionary<TileData.EffectType, SkinType> customFloodingSkins = new();
	private static bool isTakingSnapshot = false;
	private static float? baseOrthographicCameraSize = null;
	private static bool enableOutlines = false;
	private static bool seenWarningWCPopup = false;
	public record PreviewInfo(int arrayIdx, SaveStateData saveStateData, PreviewTile[] customPreview);

    private static Dictionary<string, string>? cachedSpriteDataReverse = null;
	private static Dictionary<string, string> cachedReversedNames = new();

	#region General

	/// <summary>A placeholder patch for the TechItem.SetupComplete method.</summary>
	[HarmonyPostfix]
	[HarmonyPatch(typeof(TechItem), nameof(TechItem.SetupComplete))]
	private static void TechItem_SetupComplete()
	{
	}

	/// <summary>Patches the sprite atlas manager to load custom sprites.</summary> TODOOOO
	// [HarmonyPrefix]
	// [HarmonyPatch(typeof(SpriteAtlasManager), nameof(SpriteAtlasManager.LoadSprite), typeof(string), typeof(string), typeof(SpriteCallback))]
	private static bool SpriteAtlasManager_LoadSprite(SpriteAtlasManager __instance, string atlas, string sprite, SpriteCallback completion)
	{
		bool found = false;
		__instance.LoadSpriteAtlas(atlas, (Il2CppSystem.Action<UnityEngine.U2D.SpriteAtlas>)GetAtlas);

		return !found;

		void GetAtlas(SpriteAtlas spriteAtlas)
		{
			if (spriteAtlas != null)
			{
				List<string> names = sprite.Split('_').ToList();
				List<string> filteredNames = new List<string>(names);
				string style = "";
				foreach (string item in names)
				{
					string upperitem = char.ToUpper(item[0]) + item[1..];
					if (EnumCache<TribeType>.TryGetType(item, out TribeType tribe) || EnumCache<SkinType>.TryGetType(item, out SkinType skin)
					|| EnumCache<TribeType>.TryGetType(upperitem, out TribeType tribeUpper) || EnumCache<SkinType>.TryGetType(upperitem, out SkinType skinUpper))
					{
						filteredNames.Remove(item);
						style = item;
						continue;
					}
				}
				string name = string.Join("_", filteredNames);
				Sprite? newSprite = null; // Registry.GetSprite(name, style);
				if (newSprite != null)
				{
					completion?.Invoke(atlas, sprite, newSprite);
					found = true;
				}
			}
		}
	}

	/// <summary>Patches the sprite atlas manager to look up custom sprites.</summary>
	internal static void CacheSpriteNames()
	{
		cachedSpriteDataReverse = new();
		foreach (Polytopia.Data.TerrainData.Type terrainType in Enum.GetValues<Polytopia.Data.TerrainData.Type>())
        {
            string result = SpriteData.TerrainToString(terrainType);
            if(terrainType == Polytopia.Data.TerrainData.Type.Wetland)
            {
                cachedSpriteDataReverse.TryAdd(result, EnumCache<Polytopia.Data.TerrainData.Type>.GetName(Polytopia.Data.TerrainData.Type.Field) + "_flooded");
                continue;
            }

            if(result == SpriteData.TILE_UNKNOWN)
                continue;

            cachedSpriteDataReverse.TryAdd(result, EnumCache<Polytopia.Data.TerrainData.Type>.GetName(terrainType));
        }

        foreach (ResourceData.Type resourceType in Enum.GetValues<ResourceData.Type>())
        {
            string result = SpriteData.ResourceToString(resourceType);
            if(result == SpriteData.IMPROVEMENT_PLACEHOLDER)
                continue;

            cachedSpriteDataReverse.TryAdd(result, EnumCache<ResourceData.Type>.GetName(resourceType));
        }

        foreach (ImprovementData.Type improvementType in Enum.GetValues<ImprovementData.Type>())
        {
            string result = SpriteData.ImprovementToString(improvementType);
            if(result == SpriteData.IMPROVEMENT_PLACEHOLDER)
                continue;

            cachedSpriteDataReverse.TryAdd(result, EnumCache<ImprovementData.Type>.GetName(improvementType));
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
        Sprite? sprite = Registry.GetSprite(baseName, tribe, skin, level);
		if (sprite != null)
			__result.sprite = sprite;

		string formattedName = FormatSpriteName(baseName);
        sprite = Registry.GetSprite(formattedName, tribe, skin, level);
		if (sprite != null)
			__result.sprite = sprite;
	}

	#endregion
	#region Units

	/// <summary>Enables outlines when the interaction bar is shown.</summary>
	[HarmonyPrefix]
	[HarmonyPatch(typeof(InteractionBar), nameof(InteractionBar.Show))]
	private static bool InteractionBar_Show(InteractionBar __instance, bool instant, bool force)
	{
		enableOutlines = true; // bro
		return true;
	}

	/// <summary>Prevents outlines from being created when they are disabled.</summary>
	[HarmonyPrefix]
	[HarmonyPatch(typeof(UISpriteDuplicator), nameof(UISpriteDuplicator.CreateImage), typeof(SpriteRenderer), typeof(Transform), typeof(Transform), typeof(float), typeof(Vector2), typeof(bool))]
	private static bool UISpriteDuplicator_CreateImage(SpriteRenderer spriteRenderer, Transform source, Transform destination, float scale, Vector2 offset, bool forceFullAlpha)
	{
		return !(spriteRenderer.sortingOrder == -1 && !enableOutlines); // uhhh tf is this hack
	}

	/// <summary>Disables outlines after the interaction bar is shown.</summary>
	[HarmonyPostfix]
	[HarmonyPatch(typeof(InteractionBar), nameof(InteractionBar.Show))]
	private static void InteractionBar_Show_Postfix(InteractionBar __instance, bool instant, bool force)
	{
		enableOutlines = false;
	}

	#endregion
	#region Level

	/// <summary>Updates the terrain graphics with custom sprites.</summary>
	[HarmonyPostfix]
	[HarmonyPatch(typeof(TerrainRenderer), nameof(TerrainRenderer.UpdateGraphics))] // recheck that
	private static void TerrainRenderer_UpdateGraphics(TerrainRenderer __instance, Tile tile)
	{
		string terrain = EnumCache<Polytopia.Data.TerrainData.Type>.GetName(tile.data.terrain) ?? string.Empty;

		TribeType tribe = tile.data.climate;
		SkinType skinType = tile.data.Skin;

		string flood = "";
		if (
			tile.data.effects.Contains(TileData.EffectType.Flooded) ||
			(tribe == TribeType.Aquarion && tile.data.terrain == Polytopia.Data.TerrainData.Type.Mountain)
		) // TODO: Check all tribes with flooded abil instead of Aquarion only.
		{
			foreach (var effect in tile.data.effects)
			{
				if(customFloodingSkins.ContainsKey(effect))
				{
					skinType = customFloodingSkins[effect];
					break;
				}
			}
			flood = "_flooded";
		}
		if (tile.data.terrain is Polytopia.Data.TerrainData.Type.Forest or Polytopia.Data.TerrainData.Type.Mountain)
		{
			string propertyName = terrain.ToLower();
			terrain = "field";

			PropertyInfo? rendererProperty = tile.GetType().GetProperty(propertyName + "Renderer",
				BindingFlags.Public | BindingFlags.Instance);

			if (rendererProperty != null)
			{
				PolytopiaSpriteRenderer? renderer = (PolytopiaSpriteRenderer?)rendererProperty.GetValue(tile);
				if (renderer != null)
				{
					Sprite? additionalSprite = Registry.GetSprite(propertyName + flood, tribe, skinType);
					if (additionalSprite != null)
					{
						renderer.Sprite = additionalSprite;
						rendererProperty.SetValue(tile, renderer);
					}
				}
			}
		}

		Sprite? sprite = Registry.GetSprite(terrain + flood, tribe, skinType);
		if (sprite != null)
		{
			__instance.spriteRenderer.Sprite = sprite;
		}
	}

	/// <summary>Adds custom flood tile effect when tile is flooded for custom aqua skins.</summary>
	[HarmonyPostfix]
	[HarmonyPatch(typeof(TileData), nameof(TileData.Flood))]
	private static void TileData_Flood(TileData __instance, PlayerState playerState)
	{
		if(customFloodingSkins.ContainsValue(playerState.skinType))
		{
			TileData.EffectType effectType = customFloodingSkins.FirstOrDefault(x => x.Value == playerState.skinType).Key;
			__instance.AddEffect(effectType);	
		}
	}

	/// <summary>Removes custom flood effect loc from tile tip header.</summary>
	[HarmonyPostfix]
	[HarmonyPatch(typeof(TileTipGenerator), nameof(TileTipGenerator.GetTileTipHeader))]
	public static void GetTileTipHeader(ref string __result, TileData tileData, SkinType skin,
											PlayerState playerState, GameState gameState, GameLogicData gameLogicData)
	{
		foreach(var effect in tileData.effects)
		{
			if(customFloodingSkins.ContainsKey(effect))
			{
				string localizedEffect = Localization.GetSkinned(skin, playerState.tribe, $"tile.effect.{effect.GetName()}");
				__result = __result.Replace($", {localizedEffect}", string.Empty);
			}
		}
	}

	/// <summary>Forces an update of the mesh for a PolytopiaSpriteRenderer with a custom sprite.</summary>
	[HarmonyPostfix]
	[HarmonyPatch(typeof(PolytopiaSpriteRenderer), nameof(PolytopiaSpriteRenderer.ForceUpdateMesh))]
	private static void PolytopiaSpriteRenderer_ForceUpdateMesh(PolytopiaSpriteRenderer __instance)
	{
		if (__instance.sprite != null && string.IsNullOrEmpty(__instance.atlasName))
		{
			MaterialPropertyBlock materialPropertyBlock = new();
			materialPropertyBlock.SetVector("_Flip", new Vector4(1f, 1f, 0f, 0f));
			materialPropertyBlock.SetTexture("_MainTex", __instance.sprite.texture);
			__instance.meshRenderer.SetPropertyBlock(materialPropertyBlock);
		}
	}

	#endregion
	#region Tribe Preview

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

	#endregion
	#region UI
	// check if still needed

	/// <summary>Provides custom sprites for improvements in the UI.</summary>
	[HarmonyPostfix]
	[HarmonyPatch(typeof(UIUtils), nameof(UIUtils.GetImprovementSprite), typeof(ImprovementData.Type), typeof(TribeType), typeof(SkinType), typeof(SpriteAtlasManager))]
	private static void UIUtils_GetImprovementSprite(ref Sprite __result, ImprovementData.Type improvement, TribeType tribe, SkinType skin, SpriteAtlasManager atlasManager)
	{
		Sprite? sprite = Registry.GetSprite(EnumCache<ImprovementData.Type>.GetName(improvement), tribe, skin);
		Console.Write(EnumCache<ImprovementData.Type>.GetName(improvement));
		Console.Write(sprite == null);
		if (sprite != null)
		{
			__result = sprite;
		}
	}

	/// <summary>Provides custom sprites for improvements in the UI.</summary>
	[HarmonyPostfix]
	[HarmonyPatch(typeof(UIUtils), nameof(UIUtils.GetImprovementSprite), typeof(SkinVisualsTransientData), typeof(ImprovementData.Type), typeof(SpriteAtlasManager))]
	private static void UIUtils_GetImprovementSprite_2(ref Sprite __result, SkinVisualsTransientData data, ImprovementData.Type improvement, SpriteAtlasManager atlasManager)
	{
		UIUtils_GetImprovementSprite(ref __result, improvement, data.foundingTribeSettings.tribe, data.foundingTribeSettings.skin, atlasManager);
	}

	/// <summary>Provides custom sprites for resources in the UI.</summary>
	[HarmonyPostfix]
	[HarmonyPatch(typeof(UIUtils), nameof(UIUtils.GetResourceSprite))]
	private static void UIUtils_GetResourceSprite(ref Sprite __result, SkinVisualsTransientData data, ResourceData.Type resource, SpriteAtlasManager atlasManager)
	{
		TribeAndSkin tileClimateSettings = data.tileClimateSettings;
		Sprite? sprite = Registry.GetSprite(EnumCache<ResourceData.Type>.GetName(resource), tileClimateSettings.tribe, tileClimateSettings.skin);
		if (sprite != null)
		{
			__result = sprite;
		}
	}

	#endregion
	#region Popups

	/// <summary>Updates the width of a basic popup if a custom width is set.</summary>
	[HarmonyPostfix]
	[HarmonyPatch(typeof(PopupBase), nameof(PopupBase.RefreshHeightWhenHeightIsKnownAndDontUseCoroutines))]
	private static void PopupBase_RefreshHeightWhenHeightIsKnownAndDontUseCoroutines(PopupBase __instance)
	{
		UpdateWidth(__instance);
	}

	private static void UpdateWidth(PopupBase __instance)
	{
		int id = __instance.GetInstanceID();
		if (basicPopupWidths.ContainsKey(id))
			__instance.rectTransform.SetWidth(basicPopupWidths[id]);
	}

	/// <summary>Removes a popup's custom width when it is hidden.</summary>
	[HarmonyPostfix]
	[HarmonyPatch(typeof(PopupBase), nameof(PopupBase.Hide))]
	private static void PopupBase_Hide(PopupBase __instance)
	{
		basicPopupWidths.Remove(__instance.GetInstanceID());
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(StartScreen_UI2), nameof(StartScreen_UI2.OnWeeklyChallengeClicked))]
	private static bool StartScreen_OnWeeklyChallengeClicked(StartScreen_UI2 __instance)
	{
		if(seenWarningWCPopup)
			return true;

		BasicPopup popup = PopupManager.GetBasicPopup();
		popup.Header = Localization.Get("polymod.hub");
		popup.Description = Localization.Get("polymod.wc.warning", new Il2CppSystem.Object[] { Localization.Get("weeklychallenge", new Il2CppSystem.Object[] { }) });

		void WCProceed()
		{
			seenWarningWCPopup = true;
			__instance.OnWeeklyChallengeClicked();
		}
		List<PopupBase.PopupButtonData> popupButtons = new()
		{
			new("buttons.back"),
			new(
				"polymod.wc.proceed",
				PopupBase.PopupButtonData.States.None,
				callback: DelegateSupport.ConvertDelegate<Il2CppSystem.Action>(WCProceed),
				customColorStates: ColorConstants.redButtonColorStates
			)
		};
		popup.buttonData = popupButtons.ToArray();
		popup.Show();
		return false;
	}

	/// <summary>Shows a basic popup with a custom width.</summary>
	public static void ShowSetWidth(this BasicPopupLegacy self, int width)
	{
		basicPopupWidths.Add(self.GetInstanceID(), width);
		self.Show();
	}

	#endregion

	internal static BasicPopupLegacy GetBasicPopupLegacy()
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

	/// <summary>Builds a sprite from raw byte data.</summary>
	/// <param name="data">The raw byte data of the image.</param>
	/// <param name="pivot">The pivot point of the sprite.</param>
	/// <param name="pixelsPerUnit">The number of pixels per unit for the sprite.</param>
	/// <returns>The created sprite.</returns>
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

	/// <summary>Builds a sprite from a texture.</summary>
	/// <param name="texture">The texture to create the sprite from.</param>
	/// <param name="pivot">The pivot point of the sprite.</param>
	/// <param name="pixelsPerUnit">The number of pixels per unit for the sprite.</param>
	/// <returns>The created sprite.</returns>
	public static Sprite BuildSpriteWithTexture(Texture2D texture, Vector2? pivot = null, float? pixelsPerUnit = 2112f)
	{
		return Sprite.Create(
			texture,
			new(0, 0, texture.width, texture.height),
			pivot ?? new(0.5f, 0.5f),
			pixelsPerUnit ?? 2112f
		);
	}

	/// <summary>Initializes the Visual manager by patching the necessary methods.</summary>
	internal static void Init()
	{
		Harmony.CreateAndPatchAll(typeof(Visual));
		Harmony.CreateAndPatchAll(typeof(PrefabFix));
	}
}
