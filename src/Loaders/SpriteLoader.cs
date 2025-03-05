using HarmonyLib;
using PolyMod.Managers;
using Polytopia.Data;
using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.UI;

namespace PolyMod.Loaders
{
	public class SpritesLoader
	{
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
				Sprite? newSprite = ModManager.GetSprite(names[0], names[1]);
				if (newSprite != null)
				{
					__result = newSprite;
				}
				return;
			}
			catch { }
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
			Sprite? sprite = ModManager.GetSprite(name, style, __instance.Level);
			if (sprite != null)
			{
				__instance.Sprite = sprite;
			}
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(TerrainRenderer), nameof(TerrainRenderer.UpdateGraphics))]
		private static void TerrainRenderer_UpdateGraphics(TerrainRenderer __instance, Tile tile)
		{
			string name = EnumCache<Polytopia.Data.TerrainData.Type>.GetName(tile.data.terrain) ?? string.Empty;
			if (tile.data.terrain is Polytopia.Data.TerrainData.Type.Forest or Polytopia.Data.TerrainData.Type.Mountain)
			{
				name = "field";
			}

			TribeData.Type tribe = GameManager.GameState.GameLogicData.GetTribeTypeFromStyle(tile.data.climate);
			SkinType skinType = tile.data.Skin;

			if(tile.data.effects.Contains(TileData.EffectType.Flooded))
			{
				name += "_flooded";
				foreach (TileData.EffectType effect in tile.data.effects)
				{
					if(effect == TileData.EffectType.Swamped)
					{
						skinType = SkinType.Swamp;
						break;
					}
					if((int)effect >= Plugin.AUTOIDX_STARTS_FROM)
					{
						skinType = (SkinType)(int)effect;
					}
				}
			}

			Sprite? sprite = ModManager.GetSprite(name, Utility.GetStyle(tribe, skinType));
			if (sprite != null)
			{
				__instance.spriteRenderer.Sprite = sprite;
			}
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(PolytopiaSpriteRenderer), nameof(PolytopiaSpriteRenderer.ForceUpdateMesh))]
		private static void PolytopiaSpriteRenderer_ForceUpdateMesh(PolytopiaSpriteRenderer __instance)
		{
			string name = __instance.gameObject.name.ToLower();
			if (name.Contains("forest") || name.Contains("mountain"))
			{
				Transform? terrainTranform = __instance.transform.parent;
				if (terrainTranform != null)
				{
					Transform? tileTransform = terrainTranform.parent;
					if (tileTransform != null)
					{
						Tile? tile = tileTransform.GetComponent<Tile>();
						if (tile != null)
						{
							Sprite? sprite = ModManager.GetSprite(EnumCache<Polytopia.Data.TerrainData.Type>.GetName(tile.data.terrain),
								Utility.GetStyle(GameManager.GameState.GameLogicData.GetTribeTypeFromStyle(tile.data.climate), tile.data.Skin));
							if (sprite != null)
							{
								__instance.Sprite = sprite;
							}
						}
					}
				}
			}

			if (__instance.sprite != null)
			{
				MaterialPropertyBlock materialPropertyBlock = new();
				materialPropertyBlock.SetVector("_Flip", new Vector4(1f, 1f, 0f, 0f));
				materialPropertyBlock.SetTexture("_MainTex", __instance.sprite.texture);
				__instance.meshRenderer.SetPropertyBlock(materialPropertyBlock);
			}
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(TileData), nameof(TileData.Flood))]
		private static void TileData_Flood(TileData __instance, PlayerState playerState)
		{
			if (playerState == null || (int)playerState.skinType < Plugin.AUTOIDX_STARTS_FROM)
				return;
			GameLogicData gld = PolytopiaDataManager.gameLogicDatas[VersionManager.GAME_LOGIC_DATA_VERSION];
			if (gld.TryGetData(TribeData.Type.Aquarion, out TribeData tribeData) &&
				tribeData.skins.Contains(playerState.skinType))
			{
				__instance.AddEffect((TileData.EffectType)(int)playerState.skinType);
			}
		}

		#endregion
		#region TribePreview

		[HarmonyPostfix]
		[HarmonyPatch(typeof(UIWorldPreviewData), nameof(UIWorldPreviewData.TryGetData))]
		private static void UIWorldPreviewData_TryGetData(ref bool __result, UIWorldPreviewData __instance, Vector2Int position, TribeData.Type tribeType, ref UITileData uiTile)
		{
			ModManager.PreviewTile[]? preview = null;
			if (ModManager.tribePreviews.ContainsKey(EnumCache<TribeData.Type>.GetName(tribeType).ToLower()))
			{
				preview = ModManager.tribePreviews[EnumCache<TribeData.Type>.GetName(tribeType).ToLower()];
			}
			if (preview != null)
			{
				ModManager.PreviewTile? previewTile = preview.FirstOrDefault(tileInPreview => tileInPreview.x == position.x && tileInPreview.y == position.y);
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
		#region InteractionBar

		[HarmonyPostfix]
		[HarmonyPatch(typeof(InteractionBar), nameof(InteractionBar.AddImprovementButtons))]
		private static void InteractionBar_AddImprovementButtons(InteractionBar __instance, Tile tile)
		{
			PlayerState player = GameManager.LocalPlayer;
			if (player.AutoPlay)
			{
				return;
			}
			Il2CppSystem.Collections.Generic.List<CommandBase> buildableImprovementsCommands
				= CommandUtils.GetBuildableImprovements(GameManager.GameState, player, tile.Data, true);
			for (int key = 0; key < buildableImprovementsCommands.Count; ++key)
			{
				UIRoundButton uiroundButton = __instance.quickActions.buttons[key];
				BuildCommand buildCommand = buildableImprovementsCommands[key].Cast<BuildCommand>();
				GameManager.GameState.GameLogicData.TryGetData(buildCommand.Type, out ImprovementData improvementData);
				if (improvementData.CreatesUnit() == UnitData.Type.None)
				{
					if (uiroundButton.icon.sprite == null || uiroundButton.icon.sprite.name == "placeholder")
					{
						Sprite? sprite = ModManager.GetSprite(EnumCache<ImprovementData.Type>.GetName(improvementData.type), Utility.GetStyle(player.tribe, player.skinType));
						if (sprite != null)
						{
							uiroundButton.SetSprite(sprite);
						}
					}
				}
			}
		}

		#endregion
		#region UI

		[HarmonyPostfix]
		[HarmonyPatch(typeof(UIUtils), nameof(UIUtils.GetTile))]
		private static void UIUtils_GetTile(ref RectTransform __result, Polytopia.Data.TerrainData.Type type, int climate, SkinType skin) //TODO: fix crash when no sprites
		{
			RectTransform rectTransform = __result;
			TribeData.Type tribeTypeFromStyle = GameManager.GameState.GameLogicData.GetTribeTypeFromStyle(climate);
			SkinVisualsTransientData data = new SkinVisualsTransientData
			{
				tileClimateSettings = new TribeAndSkin(tribeTypeFromStyle, skin)
			};
			UIUtils.SkinnedTerrainSprites skinnedTerrainSprites = UIUtils.GetTerrainSprite(data, type, GameManager.GetSpriteAtlasManager());

			int count = 0;
			foreach (Il2CppSystem.Object child in rectTransform)
			{
				Transform childTransform = child.Cast<Transform>();
				Image image = childTransform.GetComponent<Image>();
				Sprite? sprite = count == 0 ? skinnedTerrainSprites.groundTerrain : skinnedTerrainSprites.forestOrMountainTerrain;
				image.name = sprite.name;
				image.sprite = sprite;
				image.SetNativeSize();
				count++;
			}
			__result = rectTransform;
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(UIUtils), nameof(UIUtils.GetImprovementSprite), typeof(ImprovementData.Type), typeof(TribeData.Type), typeof(SkinType), typeof(SpriteAtlasManager))]
		private static void UIUtils_GetImprovementSprite(ref Sprite __result, ImprovementData.Type improvement, TribeData.Type tribe, SkinType skin, SpriteAtlasManager atlasManager)
		{
			Sprite? sprite = ModManager.GetSprite(EnumCache<ImprovementData.Type>.GetName(improvement), Utility.GetStyle(tribe, skin));
			if (sprite != null)
			{
				__result = sprite;
			}
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(UIUtils), nameof(UIUtils.GetImprovementSprite), typeof(SkinVisualsTransientData), typeof(ImprovementData.Type), typeof(SpriteAtlasManager))]
		private static void UIUtils_GetImprovementSprite_2(ref Sprite __result, SkinVisualsTransientData data, ImprovementData.Type improvement, SpriteAtlasManager atlasManager)
		{
			TribeData.Type tribe = data.foundingTribeSettings.tribe;
			SkinType skin = data.foundingTribeSettings.skin;
			UIUtils_GetImprovementSprite(ref __result, improvement, tribe, skin, atlasManager);
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(UIUtils), nameof(UIUtils.GetResourceSprite))]
		private static void UIUtils_GetResourceSprite(ref Sprite __result, SkinVisualsTransientData data, ResourceData.Type resource, SpriteAtlasManager atlasManager)
		{
			TribeData.Type tribe = data.tileClimateSettings.tribe;
			SkinType skin = data.tileClimateSettings.skin;

			Sprite? sprite = ModManager.GetSprite(EnumCache<ResourceData.Type>.GetName(resource), Utility.GetStyle(tribe, skin));
			if (sprite != null)
			{
				__result = sprite;
			}
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(UIUtils), nameof(UIUtils.GetTerrainSprite))]
		private static void UIUtils_GetTerrainSprite(ref UIUtils.SkinnedTerrainSprites __result, SkinVisualsTransientData data, Polytopia.Data.TerrainData.Type terrain, SpriteAtlasManager atlasManager)
		{
			string style = Utility.GetStyle(data.tileClimateSettings.tribe, data.tileClimateSettings.skin);

			Sprite? sprite;
			Sprite? groundSprite = __result.groundTerrain;
			Sprite? forestOrMountainSprite = __result.forestOrMountainTerrain;

			if (terrain == Polytopia.Data.TerrainData.Type.Mountain || terrain == Polytopia.Data.TerrainData.Type.Forest)
			{
				sprite = ModManager.GetSprite("field", style);
				if (sprite != null)
				{
					groundSprite = sprite;
				}
				sprite = ModManager.GetSprite(EnumCache<Polytopia.Data.TerrainData.Type>.GetName(terrain), style);
				if (sprite != null)
				{
					forestOrMountainSprite = sprite;
				}
			}
			else
			{
				sprite = ModManager.GetSprite(EnumCache<Polytopia.Data.TerrainData.Type>.GetName(terrain), style);
				if (sprite != null)
				{
					groundSprite = sprite;
				}
			}
			__result.groundTerrain = groundSprite;
			__result.forestOrMountainTerrain = forestOrMountainSprite;
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
				Sprite? sprite = ModManager.GetSprite("house", Utility.GetStyle(tribe, skinType), type);
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

					Sprite? sprite = ModManager.GetSprite("house", Utility.GetStyle(tribe, skin), level);
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
				sprite = ModManager.GetSprite(id, Utility.GetStyle(GameManager.LocalPlayer.tribe, GameManager.LocalPlayer.skinType));
			}
			else
			{
				sprite = ModManager.GetSprite(id);
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

			Sprite? sprite = ModManager.GetSprite("head", Utility.GetStyle(type, skinType));

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
				Sprite? sprite = ModManager.GetSprite("head", Utility.GetStyle(tribe, skin));
				if (sprite != null)
				{
					__instance.HeadImage.sprite = sprite;
					Vector2 size = sprite.rect.size;
					__instance.HeadImage.rectTransform.sizeDelta = size * __instance.rectTransform.GetHeight() / 512f;
				}
			}
		}

		#endregion

		private static void UpdateVisualPart(SkinVisualsReference.VisualPart visualPart, string name, string style)
		{
			Sprite? sprite = ModManager.GetSprite(name, style) ?? ModManager.GetSprite(visualPart.visualPart.name, style);
			if (sprite != null)
			{
				if (visualPart.renderer.spriteRenderer != null) 
					visualPart.renderer.spriteRenderer.sprite = sprite;
				else if (visualPart.renderer.polytopiaSpriteRenderer != null) 
					visualPart.renderer.polytopiaSpriteRenderer.sprite = sprite;
			}

			Sprite? outlineSprite = ModManager.GetSprite($"{name}_outline", style) ?? ModManager.GetSprite($"{visualPart.visualPart.name}_outline", style);
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
			Harmony.CreateAndPatchAll(typeof(SpritesLoader));
		}
	}
}