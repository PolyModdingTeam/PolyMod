using System.Text.Encodings.Web;
using System.Text.Json;
using Il2CppSystem.Linq;
using PolyMod.Json;
using PolytopiaBackendBase.Common;
using UnityEngine;

namespace PolyMod.Managers;

/// <summary>
/// Writes the game's own data (logic, localization, sprites, tribe previews and prefabs) to disk
/// so that mod authors can use it as a reference.
/// </summary>
internal static class Dumper
{
	private static readonly JsonSerializerOptions INDENTED = new()
	{
		WriteIndented = true,
	};

	private static readonly JsonSerializerOptions INDENTED_WITH_VECTORS = new()
	{
		WriteIndented = true,
		Converters = { new Vector2Json() },
	};

	private static readonly JsonSerializerOptions LOCALIZATION = new()
	{
		WriteIndented = true,
		Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
	};

	internal static string RootPath => Path.GetFullPath(Plugin.DUMPED_DATA_PATH);

	internal static void DumpAll()
	{
		Directory.CreateDirectory(Plugin.DUMPED_DATA_PATH);
		DumpLogic();
		DumpLocalization();
		DumpSprites();
		DumpTribePreviews();
		DumpPrefabs();
	}

	/// <summary>
	/// Dumps every version of the game logic and avatar data.
	/// </summary>
	internal static void DumpLogic()
	{
		try
		{
			Directory.CreateDirectory(Plugin.LOGIC_DUMP_PATH);
			for (int version = 1; version <= VersionManager.GameLogicDataVersion; version++)
			{
				File.WriteAllText(
					Path.Combine(Plugin.LOGIC_DUMP_PATH, $"GameLogicData{version}.json"),
					PolytopiaDataManager.provider.LoadGameLogicData(version)
				);
			}
			for (int version = 1; version <= VersionManager.AvatarDataVersion; version++)
			{
				File.WriteAllText(
					Path.Combine(Plugin.LOGIC_DUMP_PATH, $"AvatarData{version}.json"),
					PolytopiaDataManager.provider.LoadAvatarData(version)
				);
			}
		}
		catch (Exception exception)
		{
			Plugin.logger.LogError($"Dumping logic failed: {exception}");
		}
	}

	internal static void DumpLocalization()
	{
		try
		{
			Directory.CreateDirectory(Plugin.LOCALIZATION_DUMP_PATH);
			var source = I2.Loc.LocalizationManager.Sources[0];

			foreach (var language in source.GetLanguages())
			{
				int languageIndex = source.GetLanguageIndex(language);
				Dictionary<string, string> translations = new();
				foreach (var term in source.mTerms)
				{
					string translation = term.GetTranslation(languageIndex);
					if (!string.IsNullOrEmpty(translation))
						translations[term.Term] = translation;
				}
				File.WriteAllText(
					Path.Combine(
						Plugin.LOCALIZATION_DUMP_PATH,
						$"language_{source.mLanguages[languageIndex].Code}.json"
					),
					JsonSerializer.Serialize(translations, LOCALIZATION)
				);
			}

			foreach (var category in source.GetCategories())
			{
				File.WriteAllText(
					Path.Combine(Plugin.LOCALIZATION_DUMP_PATH, $"localization_{category}.csv"),
					source.Export_CSV(category)
				);
			}
		}
		catch (Exception exception)
		{
			Plugin.logger.LogError($"Dumping localization failed: {exception}");
		}
	}

	internal static void DumpSprites()
	{
		try
		{
			Directory.CreateDirectory(Plugin.SPRITES_DUMP_PATH);
			SpriteAtlasManager spriteAtlasManager = GameManager.GetSpriteAtlasManager();

			for (int i = 0; i < AtlasData.raw.Count; i++)
			{
				if (spriteAtlasManager.loadedAtlases[i] == null) continue;

				string[] row = AtlasData.raw[i];
				string atlasName = row[0];
				string directory = Path.Combine(Plugin.SPRITES_DUMP_PATH, atlasName);
				Directory.CreateDirectory(directory);

				Dictionary<string, Visual.SpriteInfo> atlasSpriteInfo = new();
				for (int j = 1; j < row.Length; j++)
				{
					string spriteName = row[j];
					Sprite sprite = spriteAtlasManager.GetSprite(spriteName, atlasName);
					if (sprite == null) continue;

					(byte[] bytes, Visual.SpriteInfo spriteInfo) = Visual.SpriteToPng(sprite);
					File.WriteAllBytes(Path.Combine(directory, $"{spriteName}.png"), bytes);
					atlasSpriteInfo[spriteName] = spriteInfo;
				}
				File.WriteAllText(
					Path.Combine(directory, $"sprites_{atlasName}.json"),
					JsonSerializer.Serialize(atlasSpriteInfo, INDENTED_WITH_VECTORS)
				);
			}
		}
		catch (Exception exception)
		{
			Plugin.logger.LogError($"Dumping sprites failed: {exception}");
		}
	}

	internal static void DumpTribePreviews()
	{
		try
		{
			foreach (TribeType type in Enum.GetValues<TribeType>())
			{
				SaveStateData saveStateData = TribePreviewRegistry.GetTribePreviewData(type, SkinType.Default); // TODO: skins
				if (saveStateData == null) continue;

				if (!DiskSerializationHelpers.FromLZ4CompressedByteArray<ClientSerializationWrapper>(
					saveStateData.byteArray, out ClientSerializationWrapper wrapper, out int _))
				{
					Plugin.logger.LogWarning($"Dump skipped preview of {type}: save state could not be read");
					continue;
				}

				List<TribePreviews.PreviewTile> previewTiles = new();
				foreach (var tileData in wrapper.GetCurrentGameState().Map.Tiles)
				{
					previewTiles.Add(new TribePreviews.PreviewTile
					{
						x = tileData.coordinates.x,
						y = tileData.coordinates.y,
						terrainType = tileData.terrain,
						resourceType = tileData.resource != null ? tileData.resource.type : null,
						unitType = tileData.unit != null ? tileData.unit.type : null,
						improvementType = tileData.improvement != null ? tileData.improvement.type : null,
						tileEffects = tileData.effects.ToArray().ToList(),
					});
				}

				File.WriteAllText(
					Path.Combine(Plugin.DUMPED_DATA_PATH, $"preview_{type}.json"),
					JsonSerializer.Serialize(previewTiles, INDENTED)
				);
			}
		}
		catch (Exception exception)
		{
			Plugin.logger.LogError($"Dumping tribe previews failed: {exception}");
		}
	}

	internal static void DumpPrefabs()
	{
		try
		{
			Directory.CreateDirectory(Plugin.PREFABS_DUMP_PATH);
			PrefabsInfo prefabsInfo = new();

			foreach (var entry in Prefabs.unitsNew)
			{
				List<VisualPartInfo>? visualParts = CollectVisualParts(entry.Value, entry.Key.unitType);
				if (visualParts == null) continue;

				prefabsInfo.units.Add(new UnitPrefabInfo
				{
					headPositionMarker = entry.Value.headPositionMarker.gameObject.name,
					skinnedKey = entry.Key,
					visualParts = visualParts,
				});
			}

			foreach (var entry in PrefabManager.improvements)
			{
				List<VisualPartInfo>? visualParts = CollectVisualParts(entry.Value, entry.Key);
				if (visualParts == null) continue;

				prefabsInfo.improvements.Add(new ImprovementPrefabInfo
				{
					improvementType = entry.Key,
					visualParts = visualParts,
				});
			}

			foreach (var entry in PrefabManager.resources)
			{
				List<VisualPartInfo>? visualParts = CollectVisualParts(entry.Value, entry.Key);
				if (visualParts == null) continue;

				prefabsInfo.resources.Add(new ResourcePrefabInfo
				{
					resourceType = entry.Key,
					visualParts = visualParts,
				});
			}

			foreach (var entry in PrefabManager.weapons)
			{
				WeaponGFX weaponGFX = entry.Value;
				prefabsInfo.weapons.Add(new WeaponPrefabInfo
				{
					weaponEnum = entry.Key,
					spriteName = weaponGFX.defaultSprite.name,
					pathType = weaponGFX.pathType,
					pathMode = weaponGFX.pathMode,
					pathResolution = weaponGFX.pathResolution,
					pathEase = weaponGFX.pathEase,
					lookAheadOffset = weaponGFX.lookAheadOffset,
					handleAngleOffset = weaponGFX.handleAngleOffset,
					sfxType = SFXTypes.None, //TODO
					weaponPuffInfo = null, //TODO
					weaponRangedInfo = null //TODO
				});
			}

			File.WriteAllText(
				Path.Combine(Plugin.PREFABS_DUMP_PATH, "prefabs.json"),
				JsonSerializer.Serialize(prefabsInfo, INDENTED_WITH_VECTORS)
			);
		}
		catch (Exception exception)
		{
			Plugin.logger.LogError($"Dumping prefabs failed: {exception}");
		}
	}

	private static List<VisualPartInfo>? CollectVisualParts(Component prefab, object key)
	{
		SkinVisualsReference skinVisualsReference = prefab.GetComponent<SkinVisualsReference>();
		if (skinVisualsReference == null || skinVisualsReference.visualParts == null)
		{
			Plugin.logger.LogWarning($"Dump skipped {key}: no SkinVisualsReference");
			return null;
		}

		List<VisualPartInfo> visualParts = new();
		foreach (var visualPart in skinVisualsReference.visualParts)
		{
			if (visualPart == null || visualPart.visualPart == null) continue;

			VisualPartInfo visualPartInfo = new();
			visualPartInfo.CopyFromVisualPart(visualPart);
			visualParts.Add(visualPartInfo);
		}
		return visualParts;
	}
}
