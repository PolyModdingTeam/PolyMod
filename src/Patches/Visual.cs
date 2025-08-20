using System.Reflection;
using HarmonyLib;
using Polytopia.Data;
using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.UI;
using Il2CppSystem.Linq;
using PolyMod.Json;
using System.Text.Json.Serialization;

namespace PolyMod.Managers;

/// <summary>
/// Manages visual aspects of the game, including sprites, UI elements, and in-game object appearances.
/// </summary>
internal static class Visual
{
	/// <summary>
	/// Represents a single tile in a tribe's world preview.
	/// </summary>
	public class PreviewTile
	{
		/// <summary>The X-coordinate of the tile.</summary>
		[JsonInclude]
		public int? x = null;
		/// <summary>The Y-coordinate of the tile.</summary>
		[JsonInclude]
		public int? y = null;
		/// <summary>The type of terrain on the tile.</summary>
		[JsonInclude]
		[JsonConverter(typeof(EnumCacheJson<Polytopia.Data.TerrainData.Type>))]
		public Polytopia.Data.TerrainData.Type terrainType = Polytopia.Data.TerrainData.Type.Ocean;
		/// <summary>The type of resource on the tile.</summary>
		[JsonInclude]
		[JsonConverter(typeof(EnumCacheJson<ResourceData.Type>))]
		public ResourceData.Type resourceType = ResourceData.Type.None;
		/// <summary>The type of unit on the tile.</summary>
		[JsonInclude]
		[JsonConverter(typeof(EnumCacheJson<UnitData.Type>))]
		public UnitData.Type unitType = UnitData.Type.None;
		/// <summary>The type of improvement on the tile.</summary>
		[JsonInclude]
		[JsonConverter(typeof(EnumCacheJson<ImprovementData.Type>))]
		public ImprovementData.Type improvementType = ImprovementData.Type.None;
	}

	/// <summary>
	/// Holds information for creating a custom sprite.
	/// </summary>
	/// <param name="pixelsPerUnit">The number of pixels per unit for the sprite.</param>
	/// <param name="pivot">The pivot point of the sprite.</param>
	public record SpriteInfo(float? pixelsPerUnit, Vector2? pivot);

	/// <summary>
	/// Holds information about a custom skin.
	/// </summary>
	/// <param name="idx">The index of the skin.</param>
	/// <param name="id">The unique identifier for the skin.</param>
	/// <param name="skinData">The data associated with the skin.</param>
	public record SkinInfo(int idx, string id, SkinData? skinData);

	/// <summary>
	/// A dictionary mapping BasicPopup instance IDs to their custom widths.
	/// </summary>
	public static Dictionary<int, int> basicPopupWidths = new();
	private static bool firstTimeOpeningPreview = true;
	private static UnitData.Type currentUnitTypeUI = UnitData.Type.None;
	private static TribeData.Type attackerTribe = TribeData.Type.None;

	/// <summary>
	/// Defines the types of prefabs that can be customized.
	/// </summary>
	public enum PrefabType
	{
		/// <summary>A unit prefab.</summary>
		Unit,
		/// <summary>An improvement prefab.</summary>
		Improvement,
		/// <summary>A resource prefab.</summary>
		Resource
	}

	/// <summary>
	/// Holds information for a custom prefab.
	/// </summary>
	/// <param name="type">The type of the prefab.</param>
	/// <param name="name">The name of the prefab.</param>
	/// <param name="visualParts">A list of visual parts that make up the prefab.</param>
	public record PrefabInfo(PrefabType type, string name, List<VisualPartInfo> visualParts);

	/// <summary>
	/// Represents a visual part of a custom prefab.
	/// </summary>
	/// <param name="gameObjectName">The name of the GameObject for this visual part.</param>
	/// <param name="baseName">The base name for sprite lookups.</param>
	/// <param name="rotation">The rotation of the visual part.</param>
	/// <param name="coordinates">The local position of the visual part.</param>
	/// <param name="scale">The local scale of the visual part.</param>
	/// <param name="tintable">Whether the visual part can be tinted.</param>
	public record VisualPartInfo(
		string gameObjectName,
		string baseName,
		float rotation = 0f,
		Vector2 coordinates = new Vector2(),
		Vector2 scale = new Vector2(),
		bool tintable = false
	);
	private static bool enableOutlines = false;

	#region General

	[HarmonyPostfix]
	[HarmonyPatch(typeof(TechItem), nameof(TechItem.SetupComplete))]
	private static void TechItem_SetupComplete()
	{
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(StartScreen), nameof(StartScreen.Start))]
	private static void StartScreen_Start()
	{
		firstTimeOpeningPreview = true;
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(SpriteAtlasManager), nameof(SpriteAtlasManager.LoadSprite), typeof(string), typeof(string), typeof(SpriteCallback))]
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
					if (EnumCache<TribeData.Type>.TryGetType(item, out TribeData.Type tribe) || EnumCache<SkinType>.TryGetType(item, out SkinType skin)
					|| EnumCache<TribeData.Type>.TryGetType(upperitem, out TribeData.Type tribeUpper) || EnumCache<SkinType>.TryGetType(upperitem, out SkinType skinUpper))
					{
						filteredNames.Remove(item);
						style = item;
						continue;
					}
				}
				string name = string.Join("_", filteredNames);
				Sprite? newSprite = Registry.GetSprite(name, style);
				if (newSprite != null)
				{
					completion?.Invoke(atlas, sprite, newSprite);
					found = true;
				}
			}
		}
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(SpriteAtlasManager), nameof(SpriteAtlasManager.DoSpriteLookup))]
	private static void SpriteAtlasManager_DoSpriteLookup(ref SpriteAtlasManager.SpriteLookupResult __result, SpriteAtlasManager __instance, string baseName, TribeData.Type tribe, SkinType skin, bool checkForOutline, int level)
	{
		baseName = Util.FormatSpriteName(baseName);

		Sprite? sprite = Registry.GetSprite(baseName, Util.GetStyle(tribe, skin), level);
		if (sprite != null)
			__result.sprite = sprite;
	}

	#endregion
	#region Units

	// lobotomy

	[HarmonyPrefix]
	[HarmonyPatch(typeof(InteractionBar), nameof(InteractionBar.Show))]
	private static bool InteractionBar_Show(InteractionBar __instance, bool instant, bool force)
	{
		enableOutlines = true;
		return true;
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(UISpriteDuplicator), nameof(UISpriteDuplicator.CreateImage), typeof(SpriteRenderer), typeof(Transform), typeof(Transform), typeof(float), typeof(Vector2), typeof(bool))]
	private static bool UISpriteDuplicator_CreateImage(SpriteRenderer spriteRenderer, Transform source, Transform destination, float scale, Vector2 offset, bool forceFullAlpha)
	{
		return !(spriteRenderer.sortingOrder == -1 && !enableOutlines);
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(InteractionBar), nameof(InteractionBar.Show))]
	private static void InteractionBar_Show_Postfix(InteractionBar __instance, bool instant, bool force)
	{
		enableOutlines = false;
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(UIUnitRenderer), nameof(UIUnitRenderer.CreateUnit))]
	private static bool UIUnitRenderer_CreateUnit_Prefix(UIUnitRenderer __instance)
	{
		currentUnitTypeUI = __instance.unitType;
		return true;
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(UIUnitRenderer), nameof(UIUnitRenderer.CreateUnit))]
	private static void UIUnitRenderer_CreateUnit_Postfix(UIUnitRenderer __instance)
	{
		currentUnitTypeUI = UnitData.Type.None;
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(SkinVisualsRenderer), nameof(SkinVisualsRenderer.SkinWorldObject))]
	private static void SkinVisualsRenderer_SkinWorldObject(
		SkinVisualsRenderer.SkinWorldType type,
		SkinVisualsReference skinVisuals,
		SkinVisualsTransientData transientSkinData,
		bool checkOutlines,
		int level)
	{
		if (type != SkinVisualsRenderer.SkinWorldType.Unit || skinVisuals == null || transientSkinData == null)
			return;

		Unit unit = skinVisuals.gameObject.GetComponent<Unit>();
		string unitTypeName = unit?.unitData != null
			? EnumCache<UnitData.Type>.GetName(unit.unitData.type)
			: EnumCache<UnitData.Type>.GetName(UnitData.Type.Warrior);
		if (currentUnitTypeUI != UnitData.Type.None)
			unitTypeName = EnumCache<UnitData.Type>.GetName(currentUnitTypeUI);

		string style = Util.GetStyle(transientSkinData.unitSettings.tribe, transientSkinData.unitSettings.skin);

		foreach (var visualPart in skinVisuals.visualParts)
		{
			UpdateVisualPart(visualPart, $"{visualPart.visualPart.name}_{unitTypeName}", style);
		}
	}

	#endregion
	#region Level

	[HarmonyPostfix]
	[HarmonyPatch(typeof(Resource), nameof(Resource.UpdateObject), typeof(SkinVisualsTransientData))]
	private static void Resource_UpdateObject(Resource __instance, SkinVisualsTransientData transientSkinData)
	{
		if (__instance.data != null)
		{
			string style = Util.GetStyle(GameManager.GameState.GameLogicData.GetTribeTypeFromStyle(__instance.tile.data.climate), __instance.tile.data.Skin);
			string name = EnumCache<ResourceData.Type>.GetName(__instance.tile.data.resource.type);

			foreach (SkinVisualsReference.VisualPart visualPart in __instance.GetSkinVisualsReference().visualParts)
			{
				UpdateVisualPart(visualPart, name, style);
			}
		}
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(Building), nameof(Building.UpdateObject), typeof(SkinVisualsTransientData))]
	private static void Building_UpdateObject(Building __instance, SkinVisualsTransientData transientSkinData)
	{
		string style = Util.GetStyle(transientSkinData.foundingTribeSettings.tribe, transientSkinData.foundingTribeSettings.skin);
		string name = EnumCache<ImprovementData.Type>.GetName(__instance.tile.data.improvement.type);
		Sprite? sprite = Registry.GetSprite(name, style, __instance.Level);
		if (sprite != null)
		{
			__instance.Sprite = sprite;
		}
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(TerrainRenderer), nameof(TerrainRenderer.UpdateGraphics))]
	private static void TerrainRenderer_UpdateGraphics(TerrainRenderer __instance, Tile tile)
	{
		string terrain = EnumCache<Polytopia.Data.TerrainData.Type>.GetName(tile.data.terrain) ?? string.Empty;

		TribeData.Type tribe = GameManager.GameState.GameLogicData.GetTribeTypeFromStyle(tile.data.climate);
		SkinType skinType = tile.data.Skin;

		string flood = "";
		if (tile.data.effects.Contains(TileData.EffectType.Flooded))
		{
			Il2CppSystem.Collections.Generic.List<CommandBase> newStack = new Il2CppSystem.Collections.Generic.List<CommandBase>();
			foreach (CommandBase command in GameManager.GameState.CommandStack)
			{
				newStack.Add(command);
			}
			newStack.Reverse();
			foreach (CommandBase command in GameManager.GameState.CommandStack)
			{
				if (command.GetCommandType() == CommandType.Flood)
				{
					FloodCommand floodCommand = command.Cast<FloodCommand>();
					if (floodCommand.Coordinates == tile.Coordinates)
					{
						if (GameManager.GameState.TryGetPlayer(floodCommand.PlayerId, out PlayerState playerState))
						{
							skinType = playerState.skinType;
						}
						break;
					}
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
					Sprite? additionalSprite = Registry.GetSprite(propertyName + flood, Util.GetStyle(tribe, skinType));
					if (additionalSprite != null)
					{
						renderer.Sprite = additionalSprite;
						rendererProperty.SetValue(tile, renderer);
					}
				}
			}
		}

		Sprite? sprite = Registry.GetSprite(terrain + flood, Util.GetStyle(tribe, skinType));
		if (sprite != null)
		{
			__instance.spriteRenderer.Sprite = sprite;
		}
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(TileData), nameof(TileData.Flood))]
	private static void TileData_Flood(TileData __instance, PlayerState playerState)
	{
		if (GameManager.Instance.isLevelLoaded)
		{
			GameManager.Client.ActionManager.ExecuteCommand(new FloodCommand(playerState.Id, __instance.coordinates), out string error);
		}
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(FloodCommand), nameof(FloodCommand.IsValid))]
	private static bool FloodCommand_IsValid(ref bool __result, FloodCommand __instance, GameState state, ref string validationError)
	{
		__result = true;
		return false;
	}

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
	#region TribePreview

	[HarmonyPostfix]
	[HarmonyPatch(typeof(UIWorldPreviewData), nameof(UIWorldPreviewData.TryGetData))]
	private static void UIWorldPreviewData_TryGetData(ref bool __result, UIWorldPreviewData __instance, Vector2Int position, TribeData.Type tribeType, ref UITileData uiTile)
	{
		PreviewTile[]? preview = null;
		if (Registry.tribePreviews.ContainsKey(EnumCache<TribeData.Type>.GetName(tribeType).ToLower()))
		{
			preview = Registry.tribePreviews[EnumCache<TribeData.Type>.GetName(tribeType).ToLower()];
		}
		if (preview != null)
		{
			PreviewTile? previewTile = preview.FirstOrDefault(tileInPreview => tileInPreview.x == position.x && tileInPreview.y == position.y);
			if (previewTile != null)
			{
				uiTile = new UITileData
				{
					Position = position,
					terrainType = previewTile.terrainType,
					resourceType = previewTile.resourceType,
					unitType = previewTile.unitType,
					improvementType = previewTile.improvementType,
					tileEffects = new Il2CppSystem.Collections.Generic.List<TileData.EffectType>()
				};
				__result = true;
			}
		}
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(UIWorldPreview), nameof(UIWorldPreview.SetPreview), new Type[] { })]
	private static void UIWorldPreview_SetPreview(UIWorldPreview __instance)
	{
		if (Plugin.config.debug && UIManager.Instance.CurrentScreen == UIConstants.Screens.TribeSelector)
		{
			if (firstTimeOpeningPreview)
			{
				RectMask2D mask = __instance.gameObject.GetComponent<RectMask2D>();
				GameObject.Destroy(mask);
				__instance.gameObject.transform.localScale = new Vector3(0.5f, 0.5f, 1f);
				__instance.gameObject.transform.position -= new Vector3(-5f, 40f, 0f);
				firstTimeOpeningPreview = false;
			}
			foreach (UITile tile in __instance.tiles)
			{
				tile.DebugText.gameObject.SetActive(true);
			}
		}
	}

	#endregion
	#region UI

	[HarmonyPostfix]
	[HarmonyPatch(typeof(UIUtils), nameof(UIUtils.GetImprovementSprite), typeof(ImprovementData.Type), typeof(TribeData.Type), typeof(SkinType), typeof(SpriteAtlasManager))]
	private static void UIUtils_GetImprovementSprite(ref Sprite __result, ImprovementData.Type improvement, TribeData.Type tribe, SkinType skin, SpriteAtlasManager atlasManager)
	{
		Sprite? sprite = Registry.GetSprite(EnumCache<ImprovementData.Type>.GetName(improvement), Util.GetStyle(tribe, skin));
		if (sprite != null)
		{
			__result = sprite;
		}
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(UIUtils), nameof(UIUtils.GetImprovementSprite), typeof(SkinVisualsTransientData), typeof(ImprovementData.Type), typeof(SpriteAtlasManager))]
	private static void UIUtils_GetImprovementSprite_2(ref Sprite __result, SkinVisualsTransientData data, ImprovementData.Type improvement, SpriteAtlasManager atlasManager)
	{
		UIUtils_GetImprovementSprite(ref __result, improvement, data.foundingTribeSettings.tribe, data.foundingTribeSettings.skin, atlasManager);
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(UIUtils), nameof(UIUtils.GetResourceSprite))]
	private static void UIUtils_GetResourceSprite(ref Sprite __result, SkinVisualsTransientData data, ResourceData.Type resource, SpriteAtlasManager atlasManager)
	{
		Sprite? sprite = Registry.GetSprite(EnumCache<ResourceData.Type>.GetName(resource), Util.GetStyle(data.tileClimateSettings.tribe, data.tileClimateSettings.skin));
		if (sprite != null)
		{
			__result = sprite;
		}
	}

	#endregion
	#region Houses

	[HarmonyPostfix]
	[HarmonyPatch(typeof(CityRenderer), nameof(CityRenderer.GetHouse))]
	private static void CityRenderer_GetHouse(ref PolytopiaSpriteRenderer __result, CityRenderer __instance, TribeData.Type tribe, int type, SkinType skinType)
	{
		PolytopiaSpriteRenderer polytopiaSpriteRenderer = __result;

		if (type != __instance.HOUSE_WORKSHOP && type != __instance.HOUSE_PARK)
		{
			Sprite? sprite = Registry.GetSprite("house", Util.GetStyle(tribe, skinType), type);
			if (sprite != null)
			{
				polytopiaSpriteRenderer.Sprite = sprite;
				TerrainMaterialHelper.SetSpriteSaturated(polytopiaSpriteRenderer, __instance.IsEnemyCity);
				__result = polytopiaSpriteRenderer;
			}
		}
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(UICityRenderer), nameof(UICityRenderer.GetResource))]
	private static void UICityRenderer_GetResource(ref GameObject __result, string baseName, Polytopia.Data.TribeData.Type tribe, Polytopia.Data.SkinType skin)
	{
		Image imageComponent = __result.GetComponent<Image>();
		string[] tokens = baseName.Split('_');
		if (tokens.Length > 0)
		{
			if (tokens[0] == "House")
			{
				int level = 0;
				if (tokens.Length > 1)
				{
					_ = int.TryParse(tokens[1], out level);
				}

				Sprite? sprite = Registry.GetSprite("house", Util.GetStyle(tribe, skin), level);
				if (sprite == null)
				{
					return;
				}
				imageComponent.sprite = sprite;
				imageComponent.SetNativeSize();
			}
		}
	}

	#endregion
	#region Icons

	[HarmonyPostfix]
	[HarmonyPatch(typeof(UIIconData), nameof(UIIconData.GetImage))]
	private static void UIIconData_GetImage(ref Image __result, string id)
	{
		Sprite? sprite;
		if (GameManager.LocalPlayer != null)
		{
			sprite = Registry.GetSprite(id, Util.GetStyle(GameManager.LocalPlayer.tribe, GameManager.LocalPlayer.skinType));
		}
		else
		{
			sprite = Registry.GetSprite(id);
		}
		if (sprite != null)
		{
			__result.sprite = sprite;
			__result.useSpriteMesh = true;
			__result.SetNativeSize();
		}
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(GameInfoRow), nameof(GameInfoRow.LoadFaceIcon), typeof(TribeData.Type), typeof(SkinType))]
	private static void GameInfoRow_LoadFaceIcon(GameInfoRow __instance, TribeData.Type type, SkinType skinType)
	{
		string style = EnumCache<TribeData.Type>.GetName(type);

		if (style == "None")
		{
			for (int i = 0; i < 20; i++)
			{
				type += byte.MaxValue + 1;
				style = EnumCache<TribeData.Type>.GetName(type);

				if (style != "None")
				{
					break;
				}
			}
		}

		Sprite? sprite = Registry.GetSprite("head", Util.GetStyle(type, skinType));

		if (sprite != null)
		{
			__instance.SetFaceIcon(sprite);
		}

		if (__instance.icon.sprite == null)
		{
			__instance.LoadFaceIcon(SpriteData.SpecialFaceIcon.neutral);
		}
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(PlayerInfoIcon), nameof(PlayerInfoIcon.SetData), typeof(TribeData.Type), typeof(SkinType), typeof(SpriteData.SpecialFaceIcon), typeof(Color), typeof(DiplomacyRelationState), typeof(PlayerInfoIcon.Mood))]
	private static void PlayerInfoIcon_SetData(PlayerInfoIcon __instance, TribeData.Type tribe, SkinType skin, SpriteData.SpecialFaceIcon face, Color color, DiplomacyRelationState diplomacyState, PlayerInfoIcon.Mood mood)
	{
		if (face == SpriteData.SpecialFaceIcon.tribe)
		{
			Sprite? sprite = Registry.GetSprite("head", Util.GetStyle(tribe, skin));
			if (sprite != null)
			{
				__instance.HeadImage.sprite = sprite;
				Vector2 size = sprite.rect.size;
				__instance.HeadImage.rectTransform.sizeDelta = size * __instance.rectTransform.GetHeight() / 512f;
			}
		}
	}

	#endregion
	#region Popups

	[HarmonyPostfix]
	[HarmonyPatch(typeof(BasicPopup), nameof(BasicPopup.Update))]
	private static void BasicPopup_Update(BasicPopup __instance)
	{
		int id = __instance.GetInstanceID();
		if (basicPopupWidths.ContainsKey(id))
			__instance.rectTransform.SetWidth(basicPopupWidths[id]);
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(Unit), nameof(Unit.Attack))]
	private static bool Unit_Attack(Unit __instance, WorldCoordinates target, bool moveToTarget, Il2CppSystem.Action onComplete)
	{
		if (__instance.Owner != null)
		{
			attackerTribe = __instance.Owner.tribe;
		}
		return true;
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(WeaponGFX), nameof(WeaponGFX.SetSkin))]
	private static void WeaponGFX_SetSkin(WeaponGFX __instance, SkinType skinType)
	{
		if (attackerTribe != TribeData.Type.None)
		{
			Sprite? sprite = Registry.GetSprite(__instance.defaultSprite.name, Util.GetStyle(attackerTribe, skinType));
			if (sprite != null)
			{
				__instance.spriteRenderer.sprite = sprite;
			}
			attackerTribe = TribeData.Type.None;
		}
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(PopupBase), nameof(PopupBase.Hide))]
	private static void PopupBase_Hide(PopupBase __instance)
	{
		basicPopupWidths.Remove(__instance.GetInstanceID());
	}

	/// <summary>
	/// Shows a BasicPopup and sets its width.
	/// </summary>
	/// <param name="self">The BasicPopup instance.</param>
	/// <param name="width">The desired width of the popup.</param>
	public static void ShowSetWidth(this BasicPopup self, int width)
	{
		basicPopupWidths.Add(self.GetInstanceID(), width);
		self.Show();
	}

	#endregion

	private static void UpdateVisualPart(SkinVisualsReference.VisualPart visualPart, string name, string style)
	{
		Sprite? sprite = Registry.GetSprite(name, style) ?? Registry.GetSprite(visualPart.visualPart.name, style);
		if (sprite != null)
		{
			if (visualPart.renderer.spriteRenderer != null)
				visualPart.renderer.spriteRenderer.sprite = sprite;
			else if (visualPart.renderer.polytopiaSpriteRenderer != null)
				visualPart.renderer.polytopiaSpriteRenderer.sprite = sprite;
		}

		Sprite? outlineSprite = Registry.GetSprite($"{name}_outline", style) ?? Registry.GetSprite($"{visualPart.visualPart.name}_outline", style);
		if (outlineSprite != null)
		{
			if (visualPart.outlineRenderer.spriteRenderer != null)
				visualPart.outlineRenderer.spriteRenderer.sprite = outlineSprite;
			else if (visualPart.outlineRenderer.polytopiaSpriteRenderer != null)
				visualPart.outlineRenderer.polytopiaSpriteRenderer.sprite = outlineSprite;
		}
	}

	/// <summary>
	/// Creates a <see cref="Sprite"/> from raw image data.
	/// </summary>
	/// <param name="data">The byte array containing the image data.</param>
	/// <param name="pivot">The pivot point for the sprite. Defaults to the center.</param>
	/// <param name="pixelsPerUnit">The number of pixels per unit for the sprite.</param>
	/// <returns>A new <see cref="Sprite"/> instance.</returns>
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

	/// <summary>
	/// Creates a <see cref="Sprite"/> from an existing <see cref="Texture2D"/>.
	/// </summary>
	/// <param name="texture">The texture to create the sprite from.</param>
	/// <param name="pivot">The pivot point for the sprite. Defaults to the center.</param>
	/// <param name="pixelsPerUnit">The number of pixels per unit for the sprite.</param>
	/// <returns>A new <see cref="Sprite"/> instance.</returns>
	public static Sprite BuildSpriteWithTexture(Texture2D texture, Vector2? pivot = null, float? pixelsPerUnit = 2112f)
	{
		return Sprite.Create(
			texture,
			new(0, 0, texture.width, texture.height),
			pivot ?? new(0.5f, 0.5f),
			pixelsPerUnit ?? 2112f
		);
	}

	internal static void Init()
	{
		Harmony.CreateAndPatchAll(typeof(Visual));
	}
}
