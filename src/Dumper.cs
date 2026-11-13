using System.Text.Encodings.Web;
using System.Text.Json;
using Il2CppSystem.Linq;
using PolyMod.Json;
using PolytopiaBackendBase.Common;
using UnityEngine;

namespace PolyMod.Managers;

/// <summary>
/// The manager that writes the data of the game to disk as a reference for mod authors.
/// It writes the game logic, the localization, the sprites, the tribe previews, and the prefabs.
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

    /// <summary>
    /// Gets the full path of the folder that contains the dump.
    /// </summary>
    internal static string RootPath => Path.GetFullPath(Plugin.DUMPED_DATA_PATH);

    /// <summary>
    /// Makes the dump folder and writes all parts of the dump.
    /// </summary>
    /// <remarks>
    /// The method calls <see cref="DumpLogic"/>, <see cref="DumpLocalization"/>, <see cref="DumpSprites"/>, <see cref="DumpTribePreviews"/>, and <see cref="DumpPrefabs"/>.
    /// Each of these methods writes an error to the log if it fails, and the next method runs.
    /// </remarks>
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
    /// Writes each version of the game logic data and of the avatar data to <see cref="Plugin.LOGIC_DUMP_PATH"/>.
    /// </summary>
    /// <remarks>
    /// The file names are <c>GameLogicData{version}.json</c> and <c>AvatarData{version}.json</c>.
    /// If an error occurs, the method writes it to the log.
    /// </remarks>
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

    /// <summary>
    /// Writes the localization of the game to <see cref="Plugin.LOCALIZATION_DUMP_PATH"/>.
    /// </summary>
    /// <remarks>
    /// The method uses the first localization source of the game.
    /// For each language, it writes a <c>language_{code}.json</c> file with all terms that have a translation.
    /// For each category, it writes a <c>localization_{category}.csv</c> file.
    /// If an error occurs, the method writes it to the log.
    /// </remarks>
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

    /// <summary>
    /// Writes the sprites of the loaded atlases to <see cref="Plugin.SPRITES_DUMP_PATH"/>.
    /// </summary>
    /// <remarks>
    /// The method uses the atlas list in <c>AtlasData.raw</c> and skips each atlas that is not loaded.
    /// For each atlas, it makes a folder with the atlas name.
    /// It writes each sprite of the atlas as a PNG file in that folder.
    /// It also writes the sprite information of the atlas to <c>sprites_{atlas}.json</c>.
    /// If an error occurs, the method writes it to the log.
    /// </remarks>
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

    /// <summary>
    /// Writes the tribe preview map of each tribe to <see cref="Plugin.DUMPED_DATA_PATH"/>.
    /// </summary>
    /// <remarks>
    /// The method uses only the preview of the default skin.
    /// For each tribe that has a preview, it reads the save state of the preview.
    /// It writes the terrain, resource, unit, improvement, and tile effects of each tile to <c>preview_{tribe}.json</c>.
    /// If the method cannot read a save state, it writes a warning and skips that tribe.
    /// If an error occurs, the method writes it to the log.
    /// </remarks>
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

    /// <summary>
    /// Writes the unit, improvement, and resource prefabs to <c>prefabs.json</c> in <see cref="Plugin.PREFABS_DUMP_PATH"/>.
    /// </summary>
    /// <remarks>
    /// The method writes the file in the <see cref="PrefabsInfo"/> format.
    /// The units come from <see cref="Prefabs.unitsNew"/>, and the improvements and resources come from <c>PrefabManager</c>.
    /// The method skips each prefab that has no <c>SkinVisualsReference</c>.
    /// It does not write weapon prefabs.
    /// If an error occurs, the method writes it to the log.
    /// </remarks>
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

            // TODO
            // foreach (var entry in PrefabManager.weapons)
            // {
            //     WeaponGFX weaponGFX = entry.Value;
            //     prefabsInfo.weapons.Add(new WeaponPrefabInfo
            //     {
            //         weaponEnum = entry.Key,
            //         spriteName = weaponGFX.defaultSprite.name,
            //         pathType = weaponGFX.pathType,
            //         pathMode = weaponGFX.pathMode,
            //         pathResolution = weaponGFX.pathResolution,
            //         pathEase = weaponGFX.pathEase,
            //         lookAheadOffset = weaponGFX.lookAheadOffset,
            //         handleAngleOffset = weaponGFX.handleAngleOffset,
            //         sfxType = SFXTypes.None, //TODO
            //         weaponPuffInfo = null, //TODO
            //         weaponRangedInfo = null //TODO
            //     });
            // }

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

    /// <summary>
    /// Gets the descriptions of the visual parts of a prefab.
    /// </summary>
    /// <remarks>
    /// The method skips each visual part that is null or that has no visual part object.
    /// If the prefab has no <c>SkinVisualsReference</c> or no visual parts, the method writes a warning.
    /// </remarks>
    /// <param name="prefab">The prefab to read.</param>
    /// <param name="key">The key of the prefab. The method uses it only in the warning.</param>
    /// <returns>The descriptions of the visual parts, or null if the prefab has no <c>SkinVisualsReference</c> or no visual parts.</returns>
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
