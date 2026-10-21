using HarmonyLib;
using Polytopia.Data;
using UnityEngine;
using Il2CppSystem.Linq;
using PolytopiaBackendBase.Common;
using Il2CppInterop.Runtime;

namespace PolyMod.Managers;

/// <summary>
/// Manages visual aspects of the game, including sprites, UI, and in-game objects.
/// </summary>
public static class Visual
{
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
	private static bool seenWarningWCPopup = false;

    private static Dictionary<string, string>? cachedSpriteDataReverse = null;
	private static Dictionary<string, string> cachedReversedNames = new();

	#region General

	/// <summary>A placeholder patch for the TechItem.SetupComplete method.</summary>
	[HarmonyPostfix]
	[HarmonyPatch(typeof(TechItem), nameof(TechItem.SetupComplete))]
	private static void TechItem_SetupComplete()
	{
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
        return formattedName;
    }

	[HarmonyPostfix]
	[HarmonyPatch(typeof(SpriteAtlasManager), nameof(SpriteAtlasManager.TrySolveSpriteNameForLevelSprites))]
	private static void SpriteAtlasManager_TrySolveSpriteNameForLevelSprites(
		ref bool __result,
		SpriteAtlasManager __instance,
		Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStringArray spriteNameVariants, int level, ref string result)
	{
		if(!__result) __result = true; // will crash original lookup if false....
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(SpriteAtlasManager), nameof(SpriteAtlasManager.DoSpriteLookup))]
	private static void SpriteAtlasManager_DoSpriteLookup(
		ref SpriteAtlasManager.SpriteLookupResult __result,
		SpriteAtlasManager __instance,
		string baseName, TribeType tribe, SkinType skin, int level)
	{
		ApplyRegisteredSprites(ref __result, baseName, tribe, skin, level);

		string formattedName = FormatSpriteName(baseName);
		if (formattedName != baseName)
		{
			ApplyRegisteredSprites(ref __result, formattedName, tribe, skin, level);
		}
	}

	private static void ApplyRegisteredSprites(
		ref SpriteAtlasManager.SpriteLookupResult lookupResult,
		string spriteName, TribeType tribe, SkinType skin, int level)
	{
		Sprite? registeredSprite = Registry.GetSprite(spriteName, tribe, skin, level);
		if (registeredSprite != null)
			lookupResult.sprite = registeredSprite;

		Sprite? registeredOutlineSprite = Registry.GetSprite(spriteName + "_outline", tribe, skin, level);
		if (registeredOutlineSprite != null)
			lookupResult.outlineSprite = registeredOutlineSprite;
	}

	[HarmonyPrefix] // its obsolete so may be removed soon by midjiwan
	[HarmonyPatch(typeof(SpriteAtlasManager), nameof(SpriteAtlasManager.LoadSprite), new Type[]{ typeof(string), typeof(string), typeof(SpriteCallback) })]
    private static bool LoadSprite(string atlas, string sprite, SpriteCallback completion)
	{
		string formattedName = FormatSpriteName(sprite);

		Sprite? resolvedSprite = Registry.GetSprite(formattedName);

		if (resolvedSprite == null && formattedName != sprite)
		{
			resolvedSprite = Registry.GetSprite(sprite);
		}

		if (resolvedSprite == null)
		{
			return true;
		}

		completion.Invoke(atlas, sprite, resolvedSprite);
		return false;
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(SpriteData), nameof(SpriteData.TerrainToString))]
	private static void SpriteData_TerrainToString(ref string __result, Polytopia.Data.TerrainData.Type terrain)
	{
		if((int)terrain >= Plugin.AUTOIDX_STARTS_FROM)
			__result = EnumCache<Polytopia.Data.TerrainData.Type>.GetName(terrain);
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(SpriteData), nameof(SpriteData.ResourceToString))]
	private static void SpriteData_ResourceToString(ref string __result, ResourceData.Type resource)
	{
		if((int)resource >= Plugin.AUTOIDX_STARTS_FROM)
			__result = EnumCache<ResourceData.Type>.GetName(resource);
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(SpriteData), nameof(SpriteData.ImprovementToString))]
	private static void SpriteData_ImprovementToString(ref string __result, ImprovementData.Type improvement)
	{
		if((int)improvement >= Plugin.AUTOIDX_STARTS_FROM)
			__result = EnumCache<ImprovementData.Type>.GetName(improvement);
	}

	#endregion
	#region Level

	private static string GetLandSpriteName(TileData tileData)
	{
		if (!tileData.HasEffect(TileData.EffectType.Flooded))
		{
			return SpriteData.TerrainToString(Polytopia.Data.TerrainData.Type.Field);
		}

		string wetlandSpriteName = SpriteData.TerrainToString(Polytopia.Data.TerrainData.Type.Wetland);

		if (tileData.HasEffect(TileData.EffectType.Swamped))
		{
			return wetlandSpriteName + "_swamp";
		}

		foreach (TileData.EffectType effectType in tileData.effects)
		{
			if (customFloodingSkins.TryGetValue(effectType, out SkinType skinType))
			{
				return $"{wetlandSpriteName}_{EnumCache<SkinType>.GetName(skinType)}";
			}
		}

		return wetlandSpriteName;
	}

	/// <summary>Updates the terrain graphics with custom sprites.</summary>
	[HarmonyPrefix]
	[HarmonyPatch(typeof(TerrainRenderer), nameof(TerrainRenderer.UpdateGraphics))] // Why prefix? I really hate repeating the logic. I would have to do the same bs 2 times.... anyway reviewers tell me ok?
	private static bool TerrainRenderer_UpdateGraphics(TerrainRenderer __instance, Tile tile, TribeType climate, SkinType skin, bool shouldDesaturate)
	{

		string spriteName = tile.Data.terrain switch
		{
			Polytopia.Data.TerrainData.Type.Water => __instance.waterSprites.GetSpriteName(tile),
			Polytopia.Data.TerrainData.Type.Ocean => __instance.oceanSprites.GetSpriteName(tile),
			Polytopia.Data.TerrainData.Type.Ice   => __instance.iceSprites.GetSpriteName(tile),
			_                                     => GetLandSpriteName(tile.Data),
		};

		SpriteAtlasManager.SpriteLookupResult spriteLookupResult = GameManager.GetSpriteAtlasManager().DoSpriteLookup(spriteName, climate, skin);
		if (spriteLookupResult.HasSprite())
		{
			__instance.spriteRenderer.Sprite = spriteLookupResult.sprite;
		}
		if (shouldDesaturate != __instance.isDesaturated)
		{
			TerrainMaterialHelper.SetSpriteSaturated(__instance.SpriteRenderer, shouldDesaturate);
			__instance.isDesaturated = shouldDesaturate;
		}

		return false;
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
		if (__instance.sprite == null || !string.IsNullOrEmpty(__instance.atlasName))
			return;

		MaterialPropertyBlock materialPropertyBlock = new();
		materialPropertyBlock.SetVector("_Flip", new Vector4(1f, 1f, 0f, 0f));
		materialPropertyBlock.SetTexture("_MainTex", __instance.sprite.texture);
		__instance.meshRenderer.SetPropertyBlock(materialPropertyBlock);
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
		// Color[] pixels = texture.GetPixels();
		// for (int i = 0; i < pixels.Length; i++)
		// {
		// 	pixels[i] = new Color(pixels[i].r, pixels[i].g, pixels[i].b, pixels[i].a);
		// }
		// texture.SetPixels(pixels);
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
		Harmony.CreateAndPatchAll(typeof(Prefabs));
		Harmony.CreateAndPatchAll(typeof(TribePreviews));
	}
}
