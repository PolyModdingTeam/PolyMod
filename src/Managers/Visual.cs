using System.Reflection;
using HarmonyLib;
using Polytopia.Data;
using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.UI;
using Il2CppSystem.Linq;
using PolyMod.Json;
using System.Text.Json.Serialization;

namespace PolyMod.Managers
{
	public static class Visual
	{
		public class PreviewTile
		{
			[JsonInclude]
			public int? x = null;
			[JsonInclude]
			public int? y = null;
			[JsonInclude]
			[JsonConverter(typeof(EnumCacheJson<Polytopia.Data.TerrainData.Type>))]
			public Polytopia.Data.TerrainData.Type terrainType = Polytopia.Data.TerrainData.Type.Ocean;
			[JsonInclude]
			[JsonConverter(typeof(EnumCacheJson<ResourceData.Type>))]
			public ResourceData.Type resourceType = ResourceData.Type.None;
			[JsonInclude]
			[JsonConverter(typeof(EnumCacheJson<UnitData.Type>))]
			public UnitData.Type unitType = UnitData.Type.None;
			[JsonInclude]
			[JsonConverter(typeof(EnumCacheJson<ImprovementData.Type>))]
			public ImprovementData.Type improvementType = ImprovementData.Type.None;
		}
		public static Dictionary<int, int> basicPopupWidths = new();
		private static bool firstTimeOpeningPreview = true;
		private static UnitData.Type currentUnitTypeUI = UnitData.Type.None;

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

		[HarmonyPostfix]
		[HarmonyPatch(typeof(SpriteAtlasManager), nameof(SpriteAtlasManager.GetSpriteFromAtlas), typeof(SpriteAtlas), typeof(string))]
		private static void SpriteAtlasManager_GetSpriteFromAtlas(ref Sprite __result, SpriteAtlas spriteAtlas, string sprite)
		{
			try
			{
				string[] names = sprite.Split('_');
				Sprite? newSprite = Main.GetSprite(names[0], names[1]);
				if (newSprite != null)
				{
					__result = newSprite;
				}
				return;
			}
			catch { }
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(SpriteAtlasManager), nameof(SpriteAtlasManager.DoSpriteLookup))]
		private static void SpriteAtlasManager_DoSpriteLookup(ref SpriteAtlasManager.SpriteLookupResult __result, SpriteAtlasManager __instance, string baseName, TribeData.Type tribe, SkinType skin, bool checkForOutline, int level)
		{
			baseName = Utility.ReverseSpriteData(baseName);

			Sprite? sprite = Main.GetSprite(baseName, Utility.GetStyle(tribe, skin), level);
			if(sprite != null)
				__result.sprite = sprite;
		}

		#endregion
		#region Units

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

			string style = Utility.GetStyle(transientSkinData.unitSettings.tribe, transientSkinData.unitSettings.skin);

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
				string style = Utility.GetStyle(GameManager.GameState.GameLogicData.GetTribeTypeFromStyle(__instance.tile.data.climate), __instance.tile.data.Skin);
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
			string style = Utility.GetStyle(transientSkinData.foundingTribeSettings.tribe, transientSkinData.foundingTribeSettings.skin);
			string name = EnumCache<ImprovementData.Type>.GetName(__instance.tile.data.improvement.type);
			Sprite? sprite = Main.GetSprite(name, style, __instance.Level);
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
			if(tile.data.effects.Contains(TileData.EffectType.Flooded))
			{
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
					if(renderer != null)
					{
						Sprite? additionalSprite = Main.GetSprite(propertyName + flood, Utility.GetStyle(tribe, skinType));
						if (additionalSprite != null)
						{
							renderer.Sprite = additionalSprite;
							rendererProperty.SetValue(tile, renderer);
						}
					}
				}
			}

			Sprite? sprite = Main.GetSprite(terrain + flood, Utility.GetStyle(tribe, skinType));
			if (sprite != null)
			{
				__instance.spriteRenderer.Sprite = sprite;
			}
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
			if (Main.tribePreviews.ContainsKey(EnumCache<TribeData.Type>.GetName(tribeType).ToLower()))
			{
				preview = Main.tribePreviews[EnumCache<TribeData.Type>.GetName(tribeType).ToLower()];
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
			Sprite? sprite = Main.GetSprite(EnumCache<ImprovementData.Type>.GetName(improvement), Utility.GetStyle(tribe, skin));
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
			Sprite? sprite = Main.GetSprite(EnumCache<ResourceData.Type>.GetName(resource), Utility.GetStyle(data.tileClimateSettings.tribe, data.tileClimateSettings.skin));
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
				Sprite? sprite = Main.GetSprite("house", Utility.GetStyle(tribe, skinType), type);
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

					Sprite? sprite = Main.GetSprite("house", Utility.GetStyle(tribe, skin), level);
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
				sprite = Main.GetSprite(id, Utility.GetStyle(GameManager.LocalPlayer.tribe, GameManager.LocalPlayer.skinType));
			}
			else
			{
				sprite = Main.GetSprite(id);
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

			Sprite? sprite = Main.GetSprite("head", Utility.GetStyle(type, skinType));

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
				Sprite? sprite = Main.GetSprite("head", Utility.GetStyle(tribe, skin));
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
            if (Visual.basicPopupWidths.ContainsKey(id))
                __instance.rectTransform.SetWidth(basicPopupWidths[id]);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(PopupBase), nameof(PopupBase.Hide))]
        private static void PopupBase_Hide(PopupBase __instance)
        {
            basicPopupWidths.Remove(__instance.GetInstanceID());
        }

        public static void ShowSetWidth(this BasicPopup self, int width)
        {
            basicPopupWidths.Add(self.GetInstanceID(), width);
            self.Show();
        }

		#endregion
		
		private static void UpdateVisualPart(SkinVisualsReference.VisualPart visualPart, string name, string style)
		{
			Sprite? sprite = Main.GetSprite(name, style) ?? Main.GetSprite(visualPart.visualPart.name, style);
			if (sprite != null)
			{
				if (visualPart.renderer.spriteRenderer != null) 
					visualPart.renderer.spriteRenderer.sprite = sprite;
				else if (visualPart.renderer.polytopiaSpriteRenderer != null) 
					visualPart.renderer.polytopiaSpriteRenderer.sprite = sprite;
			}

			Sprite? outlineSprite = Main.GetSprite($"{name}_outline", style) ?? Main.GetSprite($"{visualPart.visualPart.name}_outline", style);
			if (outlineSprite != null)
			{
				if (visualPart.outlineRenderer.spriteRenderer != null) 
					visualPart.outlineRenderer.spriteRenderer.sprite = outlineSprite;
				else if (visualPart.outlineRenderer.polytopiaSpriteRenderer != null) 
					visualPart.outlineRenderer.polytopiaSpriteRenderer.sprite = outlineSprite;
			}
		}

		public static Sprite BuildSprite(byte[] data, Vector2? pivot = null, float pixelsPerUnit = 2112f)
		{
			Texture2D tempTexture = new(1, 1);
			tempTexture.LoadImage(data);
			Texture2D texture = new(tempTexture.width, tempTexture.height)
			{
				filterMode = FilterMode.Trilinear
			};
			texture.LoadImage(data);
			Color[] pixels = texture.GetPixels();
			for (int i = 0; i < pixels.Length; i++)
			{
				if (Mathf.Approximately(pixels[i].a, 0))
					pixels[i] = new Color();
			}
			texture.SetPixels(pixels);
			texture.Apply();
			return Sprite.Create(
				texture,
				new(0, 0, texture.width, texture.height),
				pivot ?? new(0.5f, 0.5f),
				pixelsPerUnit
			);
		}

		internal static void Init()
		{
			Harmony.CreateAndPatchAll(typeof(Visual));
		}
	}
}