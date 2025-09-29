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
		public Polytopia.Data.TerrainData.Type terrainType = Polytopia.Data.TerrainData.Type.Ocean;
		/// <summary>The resource type on the tile.</summary>
		[JsonInclude]
		[JsonConverter(typeof(EnumCacheJson<ResourceData.Type>))]
		public ResourceData.Type resourceType = ResourceData.Type.None;
		/// <summary>The unit type on the tile.</summary>
		[JsonInclude]
		[JsonConverter(typeof(EnumCacheJson<UnitData.Type>))]
		public UnitData.Type unitType = UnitData.Type.None;
		/// <summary>The improvement type on the tile.</summary>
		[JsonInclude]
		[JsonConverter(typeof(EnumCacheJson<ImprovementData.Type>))]
		public ImprovementData.Type improvementType = ImprovementData.Type.None;
	}

	/// <summary>Represents information about a sprite, such as its pivot and pixels per unit.</summary>
	public record SpriteInfo(float? pixelsPerUnit, Vector2? pivot);

	/// <summary>Represents information about a custom skin.</summary>
	public record SkinInfo(int idx, string id, SkinData? skinData);

	/// <summary>A dictionary of custom widths for basic popups.</summary>
	public static Dictionary<int, int> basicPopupWidths = new();
	private static bool firstTimeOpeningPreview = true;
	private static UnitData.Type currentUnitTypeUI = UnitData.Type.None;
	private static TribeType attackerTribe = TribeType.None;

	/// <summary>The type of a custom prefab.</summary>
	public enum PrefabType
	{
		Unit,
		Improvement,
		Resource
	}

	/// <summary>Represents information about a custom prefab.</summary>
	public record PrefabInfo(PrefabType type, string name, List<VisualPartInfo> visualParts);

	/// <summary>Represents information about a visual part of a prefab.</summary>
	public record VisualPartInfo(
		string gameObjectName,
		string baseName,
		float rotation = 0f,
		Vector2 coordinates = new Vector2(),
		Vector2 scale = new Vector2(),
		bool tintable = false,
		bool headPositionMarker = false
	);
	private static bool enableOutlines = false;
	private static bool seenWarningWCPopup = false;

	#region General

	/// <summary>A placeholder patch for the TechItem.SetupComplete method.</summary>
	[HarmonyPostfix]
	[HarmonyPatch(typeof(TechItem), nameof(TechItem.SetupComplete))]
	private static void TechItem_SetupComplete()
	{
	}

	/// <summary>Resets the firstTimeOpeningPreview flag when the start screen is shown.</summary>
	[HarmonyPrefix]
	[HarmonyPatch(typeof(StartScreen), nameof(StartScreen.Start))]
	private static void StartScreen_Start()
	{
		firstTimeOpeningPreview = true;
	}

	/// <summary>Patches the sprite atlas manager to load custom sprites.</summary>
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
					if (EnumCache<TribeType>.TryGetType(item, out TribeType tribe) || EnumCache<SkinType>.TryGetType(item, out SkinType skin)
					|| EnumCache<TribeType>.TryGetType(upperitem, out TribeType tribeUpper) || EnumCache<SkinType>.TryGetType(upperitem, out SkinType skinUpper))
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

	/// <summary>Patches the sprite atlas manager to look up custom sprites.</summary>
	[HarmonyPostfix]
	[HarmonyPatch(typeof(SpriteAtlasManager), nameof(SpriteAtlasManager.DoSpriteLookup))]
	private static void SpriteAtlasManager_DoSpriteLookup(ref SpriteAtlasManager.SpriteLookupResult __result, SpriteAtlasManager __instance, string baseName, TribeType tribe, SkinType skin, bool checkForOutline, int level)
	{
		baseName = Util.FormatSpriteName(baseName);

		Sprite? sprite = Registry.GetSprite(baseName, Util.GetStyle(tribe, skin), level);
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
		enableOutlines = true;
		return true;
	}

	/// <summary>Prevents outlines from being created when they are disabled.</summary>
	[HarmonyPrefix]
	[HarmonyPatch(typeof(UISpriteDuplicator), nameof(UISpriteDuplicator.CreateImage), typeof(SpriteRenderer), typeof(Transform), typeof(Transform), typeof(float), typeof(Vector2), typeof(bool))]
	private static bool UISpriteDuplicator_CreateImage(SpriteRenderer spriteRenderer, Transform source, Transform destination, float scale, Vector2 offset, bool forceFullAlpha)
	{
		return !(spriteRenderer.sortingOrder == -1 && !enableOutlines);
	}

	/// <summary>Disables outlines after the interaction bar is shown.</summary>
	[HarmonyPostfix]
	[HarmonyPatch(typeof(InteractionBar), nameof(InteractionBar.Show))]
	private static void InteractionBar_Show_Postfix(InteractionBar __instance, bool instant, bool force)
	{
		enableOutlines = false;
	}

	/// <summary>Sets the current unit type being rendered in the UI.</summary>
	[HarmonyPrefix]
	[HarmonyPatch(typeof(UIUnitRenderer), nameof(UIUnitRenderer.CreateUnit))]
	private static bool UIUnitRenderer_CreateUnit_Prefix(UIUnitRenderer __instance)
	{
		currentUnitTypeUI = __instance.unitType;
		return true;
	}

	/// <summary>Resets the current unit type being rendered in the UI.</summary>
	[HarmonyPostfix]
	[HarmonyPatch(typeof(UIUnitRenderer), nameof(UIUnitRenderer.CreateUnit))]
	private static void UIUnitRenderer_CreateUnit_Postfix(UIUnitRenderer __instance)
	{
		currentUnitTypeUI = UnitData.Type.None;
	}

	/// <summary>Skins the visual parts of a unit with custom sprites.</summary>
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

	/// <summary>Updates the visual parts of a resource with custom sprites.</summary>
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

	/// <summary>Updates a building with a custom sprite.</summary>
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

	/// <summary>Updates the terrain graphics with custom sprites.</summary>
	[HarmonyPostfix]
	[HarmonyPatch(typeof(TerrainRenderer), nameof(TerrainRenderer.UpdateGraphics))]
	private static void TerrainRenderer_UpdateGraphics(TerrainRenderer __instance, Tile tile)
	{
		string terrain = EnumCache<Polytopia.Data.TerrainData.Type>.GetName(tile.data.terrain) ?? string.Empty;

		TribeType tribe = GameManager.GameState.GameLogicData.GetTribeTypeFromStyle(tile.data.climate);
		SkinType skinType = tile.data.Skin;

		string flood = "";
		if (tile.data.effects.Contains(TileData.EffectType.Flooded) || (tribe == TribeType.Aquarion && tile.data.terrain == Polytopia.Data.TerrainData.Type.Mountain))
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

	/// <summary>Executes a flood command when a tile is flooded.</summary>
	[HarmonyPostfix]
	[HarmonyPatch(typeof(TileData), nameof(TileData.Flood))]
	private static void TileData_Flood(TileData __instance, PlayerState playerState)
	{
		if (GameManager.Instance.isLevelLoaded)
		{
			GameManager.Client.ActionManager.ExecuteCommand(new FloodCommand(playerState.Id, __instance.coordinates), out string error);
		}
	}

	/// <summary>Ensures that flood commands are always considered valid.</summary>
	[HarmonyPrefix]
	[HarmonyPatch(typeof(FloodCommand), nameof(FloodCommand.IsValid))]
	private static bool FloodCommand_IsValid(ref bool __result, FloodCommand __instance, GameState state, ref string validationError)
	{
		__result = true;
		return false;
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
	#region TribePreview

	/// <summary>Provides custom data for the tribe preview.</summary>
	[HarmonyPostfix]
	[HarmonyPatch(typeof(UIWorldPreviewData), nameof(UIWorldPreviewData.TryGetData))]
	private static void UIWorldPreviewData_TryGetData(ref bool __result, UIWorldPreviewData __instance, Vector2Int position, TribeType tribeType, ref UITileData uiTile)
	{
		PreviewTile[]? preview = null;
		if (Registry.tribePreviews.ContainsKey(EnumCache<TribeType>.GetName(tribeType).ToLower()))
		{
			preview = Registry.tribePreviews[EnumCache<TribeType>.GetName(tribeType).ToLower()];
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

	/// <summary>Modifies the tribe preview for debugging purposes.</summary>
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

	/// <summary>Provides custom sprites for improvements in the UI.</summary>
	[HarmonyPostfix]
	[HarmonyPatch(typeof(UIUtils), nameof(UIUtils.GetImprovementSprite), typeof(ImprovementData.Type), typeof(TribeType), typeof(SkinType), typeof(SpriteAtlasManager))]
	private static void UIUtils_GetImprovementSprite(ref Sprite __result, ImprovementData.Type improvement, TribeType tribe, SkinType skin, SpriteAtlasManager atlasManager)
	{
		Sprite? sprite = Registry.GetSprite(EnumCache<ImprovementData.Type>.GetName(improvement), Util.GetStyle(tribe, skin));
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
		Sprite? sprite = Registry.GetSprite(EnumCache<ResourceData.Type>.GetName(resource), Util.GetStyle(data.tileClimateSettings.tribe, data.tileClimateSettings.skin));
		if (sprite != null)
		{
			__result = sprite;
		}
	}

	#endregion
	#region Houses

	/// <summary>Provides custom sprites for houses in the city view.</summary>
	[HarmonyPostfix]
	[HarmonyPatch(typeof(CityRenderer), nameof(CityRenderer.GetHouse))]
	private static void CityRenderer_GetHouse(ref PolytopiaSpriteRenderer __result, CityRenderer __instance, TribeType tribe, int type, SkinType skinType)
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

	/// <summary>Provides custom sprites for houses in the UI city renderer.</summary>
	[HarmonyPostfix]
	[HarmonyPatch(typeof(UICityRenderer), nameof(UICityRenderer.GetResource))]
	private static void UICityRenderer_GetResource(ref GameObject __result, string baseName, TribeType tribe, SkinType skin)
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

	/// <summary>Provides custom sprites for UI icons.</summary>
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

	/// <summary>Provides custom sprites for face icons in the game info row.</summary>
	[HarmonyPostfix]
	[HarmonyPatch(typeof(GameInfoRow), nameof(GameInfoRow.LoadFaceIcon), typeof(TribeType), typeof(SkinType))]
	private static void GameInfoRow_LoadFaceIcon(GameInfoRow __instance, TribeType type, SkinType skinType)
	{
		string style = EnumCache<TribeType>.GetName(type);

		if (style == "None")
		{
			for (int i = 0; i < 20; i++)
			{
				type += byte.MaxValue + 1;
				style = EnumCache<TribeType>.GetName(type);

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

	/// <summary>Provides custom sprites for player info icons.</summary>
	[HarmonyPostfix]
	[HarmonyPatch(typeof(PlayerInfoIcon), nameof(PlayerInfoIcon.SetData), typeof(TribeType), typeof(SkinType), typeof(SpriteData.SpecialFaceIcon), typeof(Color), typeof(DiplomacyRelationState), typeof(PlayerInfoIcon.Mood))]
	private static void PlayerInfoIcon_SetData(PlayerInfoIcon __instance, TribeType tribe, SkinType skin, SpriteData.SpecialFaceIcon face, Color color, DiplomacyRelationState diplomacyState, PlayerInfoIcon.Mood mood)
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

	/// <summary>Updates the width of a basic popup if a custom width is set.</summary>
	[HarmonyPostfix]
	[HarmonyPatch(typeof(BasicPopup), nameof(BasicPopup.Update))]
	private static void BasicPopup_Update(BasicPopup __instance)
	{
		int id = __instance.GetInstanceID();
		if (basicPopupWidths.ContainsKey(id))
			__instance.rectTransform.SetWidth(basicPopupWidths[id]);
	}

	/// <summary>Sets the attacker's tribe before a unit attacks.</summary>
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

	/// <summary>Sets the skin of a weapon's graphics, using custom sprites if available.</summary>
	[HarmonyPostfix]
	[HarmonyPatch(typeof(WeaponGFX), nameof(WeaponGFX.SetSkin))]
	private static void WeaponGFX_SetSkin(WeaponGFX __instance, SkinType skinType)
	{
		if (attackerTribe != TribeType.None)
		{
			Sprite? sprite = Registry.GetSprite(__instance.defaultSprite.name, Util.GetStyle(attackerTribe, skinType));
			if (sprite != null)
			{
				__instance.spriteRenderer.sprite = sprite;
			}
			attackerTribe = TribeType.None;
		}
	}

	/// <summary>Removes a popup's custom width when it is hidden.</summary>
	[HarmonyPostfix]
	[HarmonyPatch(typeof(PopupBase), nameof(PopupBase.Hide))]
	private static void PopupBase_Hide(PopupBase __instance)
	{
		basicPopupWidths.Remove(__instance.GetInstanceID());
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(StartScreen), nameof(StartScreen.OnWeeklyChallengedButtonClick))]
	private static bool StartScreen_OnWeeklyChallengedButtonClick(StartScreen __instance)
	{
		if(seenWarningWCPopup)
			return true;
		BasicPopup popup = PopupManager.GetBasicPopup();
		popup.Header = Localization.Get("polymod.hub");
		popup.Description = Localization.Get("polymod.wc.warning", new Il2CppSystem.Object[] { Localization.Get("weeklychallenge", new Il2CppSystem.Object[] { }) });
		List<PopupBase.PopupButtonData> popupButtons = new()
		{
			new("buttons.back"),
			new(
				"polymod.wc.proceed",
				PopupBase.PopupButtonData.States.None,
				callback: (UIButtonBase.ButtonAction)((_, _) =>
				{
					seenWarningWCPopup = true;
					__instance.OnWeeklyChallengedButtonClick();
				}),
				customColorStates: ColorConstants.redButtonColorStates
			)
		};
		popup.buttonData = popupButtons.ToArray();
		popup.Show();
		return false;
	}

	/// <summary>Shows a basic popup with a custom width.</summary>
	public static void ShowSetWidth(this BasicPopup self, int width)
	{
		basicPopupWidths.Add(self.GetInstanceID(), width);
		self.Show();
	}

	#endregion

	/// <summary>Updates a visual part with a custom sprite.</summary>
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
	}
}
