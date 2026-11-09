using HarmonyLib;
using Polytopia.Data;
using UnityEngine;
using Il2CppSystem.Linq;
using PolytopiaBackendBase.Common;
using System.Text.Json.Serialization;
using PolyMod.Json;
using TMPro;

namespace PolyMod.Managers;

/// <summary>
/// The manager for sprites, textures, terrain graphics, and popups.
/// It also contains the Harmony patches that change how the game shows these items.
/// </summary>
public static class Visual
{
    /// <summary>
    /// The settings that PolyMod uses to make a sprite, such as the pivot and the pixels per unit.
    /// A mod gives these settings in a <c>sprites.json</c> file.
    /// </summary>
    public class SpriteInfo
    {
        /// <summary>
        /// The number of texture pixels in one world unit.
        /// If this value is null, PolyMod uses 2112 for a mod texture and the original value for a game sprite.
        /// </summary>
        [JsonInclude]
        public float? pixelsPerUnit;

        /// <summary>
        /// The pivot of the sprite, as a fraction of the sprite size.
        /// If this value is null, PolyMod uses a preset pivot for a mod texture and the original pivot for a game sprite.
        /// </summary>
        [JsonInclude]
        public Vector2? pivot;

        /// <summary>
        /// The source of the sprite image.
        /// If this value is null, PolyMod uses the mod texture that has the same name as the sprite.
        /// </summary>
        [JsonInclude]
        public SpriteOrigin? spriteOrigin = null;

        /// <summary>
        /// If true, PolyMod changes the image into a white silhouette with a border before it makes the sprite.
        /// </summary>
        [JsonInclude]
        public bool transformToOutline = false;

        /// <summary>
        /// The radius of the Gaussian blur that PolyMod applies to the image.
        /// If this value is null, PolyMod does not apply a blur.
        /// </summary>
        [JsonInclude]
        public float? gaussianBlurRadius = null;

        /// <summary>
        /// Makes a new <see cref="SpriteInfo"/> with the given settings.
        /// </summary>
        /// <param name="pixelsPerUnit">The number of texture pixels in one world unit, or null to use the default value.</param>
        /// <param name="pivot">The pivot of the sprite, or null to use the default pivot.</param>
        /// <param name="transformToOutline">If true, the image changes into a white silhouette with a border.</param>
        /// <param name="spriteOrigin">The source of the sprite image, or null to use the texture with the same name.</param>
        /// <param name="gaussianBlurRadius">The radius of the Gaussian blur, or null for no blur.</param>
        public SpriteInfo(float? pixelsPerUnit, Vector2? pivot, bool transformToOutline = false, SpriteOrigin? spriteOrigin = null, float? gaussianBlurRadius = null)
        {
            this.pixelsPerUnit = pixelsPerUnit;
            this.pivot = pivot;
            this.spriteOrigin = spriteOrigin;
            this.transformToOutline = transformToOutline;
            this.gaussianBlurRadius = gaussianBlurRadius;
        }
    }

    /// <summary>
    /// The source of the image for a sprite that a <see cref="SpriteInfo"/> describes.
    /// </summary>
    public class SpriteOrigin
    {
        /// <summary>
        /// The name of the source image.
        /// For <see cref="OriginType.Outer"/>, it is the name of a mod texture.
        /// For <see cref="OriginType.Inner"/>, it is the name of a game sprite.
        /// </summary>
        [JsonInclude]
        public string? name;

        /// <summary>
        /// The type of the source. The default value is <see cref="OriginType.Outer"/>.
        /// </summary>
        [JsonInclude]
        [JsonConverter(typeof(EnumNameJson<OriginType>))]
        public OriginType originType = OriginType.Outer;
    }

    /// <summary>
    /// The types of source for the image of a sprite.
    /// </summary>
    public enum OriginType
    {
        /// <summary>The image comes from a texture in a mod.</summary>
        Outer, // Outer means sprite comes from a mod, and not from game's files
        /// <summary>The image comes from a sprite in the game files.</summary>
        Inner // Inner means sprite is to be fetched from game's files
    }

    /// <summary>
    /// The data about a custom skin that a mod adds.
    /// </summary>
    /// <param name="idx">The enum value that PolyMod gives to the skin.</param>
    /// <param name="id">The ID of the skin in the GLD.</param>
    /// <param name="skinData">The skin data, or null if PolyMod did not make it.</param>
    public record SkinInfo(int idx, string id, SkinData? skinData);

    /// <summary>
    /// The custom widths of shown popups, keyed by the Unity instance ID of the popup.
    /// PolyMod removes an entry when the popup closes.
    /// </summary>
    public static Dictionary<int, PopupWidth> basicPopupWidths = new();

    /// <summary>
    /// The custom width settings of one popup.
    /// </summary>
    /// <param name="popup">The popup that uses the custom width.</param>
    /// <param name="share">The share of the usable screen width, from 0 to 1. A value of 0 or less gives a fixed width.</param>
    /// <param name="maximum">The maximum width in canvas units. With a fixed width, this is the width of the popup.</param>
    public record PopupWidth(BasicPopupLegacy popup, float share, float maximum);

    private const int BASIC_POPUP_LEGACY_ID = 29;

    /// <summary>
    /// The width of the game's own popups, in canvas units.
    /// A popup with a screen share never gets a smaller width than this value.
    /// </summary>
    private const float DEFAULT_POPUP_WIDTH = 402f;

    /// <summary>
    /// The horizontal gap between two popup buttons in one row, in canvas units.
    /// If a button and this gap do not fit in the row, the button goes to a new row.
    /// </summary>
    private const float POPUP_BUTTON_GAP = 8f;

    /// <summary>
    /// The vertical gap between two rows of popup buttons, in canvas units.
    /// </summary>
    private const float POPUP_BUTTON_ROW_GAP = 10f;

    private const byte INVISIBLE_ALPHA = 2;
    private const int ALPHA_BLEED_RINGS = 16;
    private const int PREMULTIPLIED_MIN_EDGE_PIXELS = 64;
    private const float PREMULTIPLIED_MAX_OUTLIER_SHARE = 0.005f;

    /// <summary>
    /// The custom skins that can flood tiles, keyed by the custom flood tile effect of each skin.
    /// The loader adds an entry for each custom skin that has the <c>Flooded</c> tribe ability.
    /// </summary>
    internal static Dictionary<TileData.EffectType, SkinType> customFloodingSkins = new();

    private static readonly Color WEEKLY_CHALLENGE_DISABLED_COLOR = new(0.42f, 0.42f, 0.42f, 1f);

    private static Dictionary<string, string>? cachedSpriteDataReverse = null;
    private static Dictionary<string, string> cachedReversedNames = new();

    #region General

    /// <summary>
    /// A Harmony postfix on <c>TechItem.SetupComplete</c>. It is empty and does not change the game.
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(TechItem), nameof(TechItem.SetupComplete))]
    private static void TechItem_SetupComplete()
    {
    }

    /// <summary>
    /// Fills the cache that maps game sprite names to PolyMod enum names.
    /// The cache contains the names of terrain, resource, and improvement sprites.
    /// The wetland sprite name maps to the field name with the <c>_flooded</c> suffix.
    /// Unknown terrain and placeholder sprite names are not in the cache.
    /// </summary>
    internal static void CacheSpriteNames()
    {
        cachedSpriteDataReverse = new();
        foreach (Polytopia.Data.TerrainData.Type terrainType in Enum.GetValues<Polytopia.Data.TerrainData.Type>())
        {
            string result = SpriteData.TerrainToString(terrainType);
            if (terrainType == Polytopia.Data.TerrainData.Type.Wetland)
            {
                cachedSpriteDataReverse.TryAdd(result, EnumCache<Polytopia.Data.TerrainData.Type>.GetName(Polytopia.Data.TerrainData.Type.Field) + "_flooded");
                continue;
            }

            if (result == SpriteData.TILE_UNKNOWN)
                continue;

            cachedSpriteDataReverse.TryAdd(result, EnumCache<Polytopia.Data.TerrainData.Type>.GetName(terrainType));
        }

        foreach (ResourceData.Type resourceType in Enum.GetValues<ResourceData.Type>())
        {
            string result = SpriteData.ResourceToString(resourceType);
            if (result == SpriteData.IMPROVEMENT_PLACEHOLDER)
                continue;

            cachedSpriteDataReverse.TryAdd(result, EnumCache<ResourceData.Type>.GetName(resourceType));
        }

        foreach (ImprovementData.Type improvementType in Enum.GetValues<ImprovementData.Type>())
        {
            string result = SpriteData.ImprovementToString(improvementType);
            if (result == SpriteData.IMPROVEMENT_PLACEHOLDER)
                continue;

            cachedSpriteDataReverse.TryAdd(result, EnumCache<ImprovementData.Type>.GetName(improvementType));
        }
    }
    /// <summary>
    /// Changes a game sprite name into a name that uses PolyMod enum names.
    /// The method replaces each cached game sprite name in the text with its enum name.
    /// It fills the name cache on the first call and keeps each result in a second cache.
    /// </summary>
    /// <param name="baseName">The sprite name that the game uses.</param>
    /// <returns>The sprite name with enum names. If no part matches, the method gives the same name.</returns>
    internal static string FormatSpriteName(string baseName)
    {
        if (cachedSpriteDataReverse == null)
            CacheSpriteNames();

        if (cachedReversedNames.TryGetValue(baseName, out string? cachedReversedName))
            return cachedReversedName;

        string formattedName = baseName;
        foreach (var valuePair in cachedSpriteDataReverse!)
        {
            formattedName = formattedName.Replace(valuePair.Key, valuePair.Value);
        }
        cachedReversedNames[baseName] = formattedName;
        return formattedName;
    }

    /// <summary>
    /// A Harmony postfix on <c>SpriteAtlasManager.TrySolveSpriteNameForLevelSprites</c>.
    /// It always sets the result to true, because a false result makes the original sprite lookup fail.
    /// </summary>
    /// <param name="__result">The result of the original method. The patch sets it to true.</param>
    /// <param name="__instance">The sprite atlas manager.</param>
    /// <param name="spriteNameVariants">The sprite name variants that the original method examines.</param>
    /// <param name="level">The level of the sprite.</param>
    /// <param name="result">The sprite name that the original method found. The patch does not change it.</param>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(SpriteAtlasManager), nameof(SpriteAtlasManager.TrySolveSpriteNameForLevelSprites))]
    private static void SpriteAtlasManager_TrySolveSpriteNameForLevelSprites(
        ref bool __result,
        SpriteAtlasManager __instance,
        Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStringArray spriteNameVariants, int level, ref string result)
    {
        if (!__result) __result = true; // will crash original lookup if false....
    }

    /// <summary>
    /// A Harmony postfix on <c>SpriteAtlasManager.DoSpriteLookup</c>.
    /// It replaces the sprite and the outline sprite in the result with registered mod sprites.
    /// First, it uses the original sprite name. Then it uses the name from <see cref="FormatSpriteName"/>, if that name is different.
    /// A sprite from the second name replaces a sprite from the first name.
    /// </summary>
    /// <param name="__result">The lookup result of the original method. The patch can change its sprites.</param>
    /// <param name="__instance">The sprite atlas manager.</param>
    /// <param name="baseName">The sprite name that the game looks for.</param>
    /// <param name="tribe">The tribe of the sprite.</param>
    /// <param name="skin">The skin of the sprite.</param>
    /// <param name="level">The level of the sprite.</param>
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

    /// <summary>
    /// Puts the registered mod sprite and outline sprite for a name into a lookup result.
    /// If the registry has no sprite of a type, the method keeps the sprite of that type in the result.
    /// </summary>
    /// <param name="lookupResult">The lookup result to change.</param>
    /// <param name="spriteName">The sprite name to find in the registry.</param>
    /// <param name="tribe">The tribe of the sprite.</param>
    /// <param name="skin">The skin of the sprite.</param>
    /// <param name="level">The level of the sprite.</param>
    private static void ApplyRegisteredSprites(
        ref SpriteAtlasManager.SpriteLookupResult lookupResult,
        string spriteName, TribeType tribe, SkinType skin, int level)
    {
        Sprite? registeredSprite = Registry.GetSprite(spriteName, tribe, skin, level);
        if (registeredSprite != null)
            lookupResult.sprite = registeredSprite;

        Sprite? registeredOutlineSprite = Registry.GetSprite(spriteName, tribe, skin, level, true);
        if (registeredOutlineSprite != null)
            lookupResult.outlineSprite = registeredOutlineSprite;
    }

    /// <summary>
    /// A Harmony postfix on <c>SpriteAtlasManager.GetSprite(string, string)</c>.
    /// If the original method found no sprite, the patch gives the registered mod sprite with the lowercase name.
    /// </summary>
    /// <param name="__result">The sprite that the original method found. The patch sets it only if it is null.</param>
    /// <param name="spriteName">The name of the sprite.</param>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(SpriteAtlasManager), nameof(SpriteAtlasManager.GetSprite), new Type[] { typeof(string), typeof(string) })]
    private static void SpriteAtlasManager_GetSprite(ref Sprite __result, string spriteName)
    {
        if (__result != null || string.IsNullOrEmpty(spriteName)) return;
        if (Registry.sprites.TryGetValue(spriteName.ToLowerInvariant(), out Sprite? sprite))
            __result = sprite;
    }

    /// <summary>
    /// A Harmony postfix on <c>MapDataExtensions.GenerateShoreLines</c>.
    /// For each visible shoreline of a tile, it sets the sprite suffix from the skin and effects of the adjacent tile.
    /// </summary>
    /// <param name="map">The map that has the shorelines.</param>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(MapDataExtensions), nameof(MapDataExtensions.GenerateShoreLines))]
    private static void MapDataExtensions_GenerateShoreLines(MapData map)
    {
        int width = map.Width;
        var tiles = map.Tiles;
        for (int i = 0; i < tiles.Length; i++)
        {
            TileData.Shorelines shorelines = tiles[i].shorelines;
            if (!shorelines.any) continue;

            if (shorelines.S.visible) SetShorelineSuffix(tiles[i - width], shorelines.S);
            if (shorelines.N.visible) SetShorelineSuffix(tiles[i + width], shorelines.N);
            if (shorelines.W.visible) SetShorelineSuffix(tiles[i - 1], shorelines.W);
            if (shorelines.E.visible) SetShorelineSuffix(tiles[i + 1], shorelines.E);
        }
    }

    /// <summary>
    /// Sets the sprite suffix of a shoreline from the skin and effects of a tile.
    /// The suffix is <c>_swamp</c> for a swamp skin or a swamped tile.
    /// For a different custom or tribe skin, the suffix is an underscore and the skin name.
    /// A custom flood effect on the tile replaces the suffix with the name of the related skin.
    /// If no rule applies, the suffix is empty.
    /// </summary>
    /// <param name="neighbour">The tile that gives the skin and the effects.</param>
    /// <param name="shoreline">The shoreline to change.</param>
    private static void SetShorelineSuffix(TileData neighbour, TileData.Shoreline shoreline)
    {
        string suffix = neighbour.Skin == SkinType.Swamp || neighbour.HasEffect(TileData.EffectType.Swamped)
            ? "_swamp"
            : "";
        if (neighbour.Skin != SkinType.None && neighbour.Skin != SkinType.Default && neighbour.Skin != SkinType.Swamp)
            suffix = "_" + EnumCache<SkinType>.GetName(neighbour.Skin);
        foreach (var effectToSkin in customFloodingSkins)
        {
            if (neighbour.HasEffect(effectToSkin.Key))
            {
                suffix = "_" + EnumCache<SkinType>.GetName(effectToSkin.Value);
                break;
            }
        }
        shoreline.spriteExt = suffix;
    }

    /// <summary>
    /// A Harmony prefix on <c>SpriteAtlasManager.LoadSprite(string, string, SpriteCallback)</c>.
    /// It finds a registered mod sprite with the name from <see cref="FormatSpriteName"/>, and then with the original name.
    /// If it finds a sprite, it calls the callback with that sprite.
    /// </summary>
    /// <param name="atlas">The name of the atlas.</param>
    /// <param name="sprite">The name of the sprite.</param>
    /// <param name="completion">The callback that gets the sprite.</param>
    /// <returns>False if the patch found a mod sprite, which skips the original method. True if not.</returns>
    [HarmonyPrefix] // its obsolete so may be removed soon by midjiwan
    [HarmonyPatch(typeof(SpriteAtlasManager), nameof(SpriteAtlasManager.LoadSprite), new Type[] { typeof(string), typeof(string), typeof(SpriteCallback) })]
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

    /// <summary>
    /// A Harmony postfix on <c>SpriteData.TerrainToString</c>.
    /// For a custom terrain type, it replaces the sprite name with the enum name of the terrain.
    /// </summary>
    /// <param name="__result">The sprite name that the original method gives.</param>
    /// <param name="terrain">The terrain type.</param>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(SpriteData), nameof(SpriteData.TerrainToString))]
    private static void SpriteData_TerrainToString(ref string __result, Polytopia.Data.TerrainData.Type terrain)
    {
        if ((int)terrain >= Plugin.AUTOIDX_STARTS_FROM)
            __result = EnumCache<Polytopia.Data.TerrainData.Type>.GetName(terrain);
    }

    /// <summary>
    /// A Harmony postfix on <c>SpriteData.ResourceToString</c>.
    /// For a custom resource type, it replaces the sprite name with the enum name of the resource.
    /// </summary>
    /// <param name="__result">The sprite name that the original method gives.</param>
    /// <param name="resource">The resource type.</param>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(SpriteData), nameof(SpriteData.ResourceToString))]
    private static void SpriteData_ResourceToString(ref string __result, ResourceData.Type resource)
    {
        if ((int)resource >= Plugin.AUTOIDX_STARTS_FROM)
            __result = EnumCache<ResourceData.Type>.GetName(resource);
    }

    /// <summary>
    /// A Harmony postfix on <c>SpriteData.ImprovementToString</c>.
    /// For a custom improvement type, it replaces the sprite name with the enum name of the improvement.
    /// </summary>
    /// <param name="__result">The sprite name that the original method gives.</param>
    /// <param name="improvement">The improvement type.</param>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(SpriteData), nameof(SpriteData.ImprovementToString))]
    private static void SpriteData_ImprovementToString(ref string __result, ImprovementData.Type improvement)
    {
        if ((int)improvement >= Plugin.AUTOIDX_STARTS_FROM)
            __result = EnumCache<ImprovementData.Type>.GetName(improvement);
    }

    #endregion
    #region Level

    /// <summary>
    /// A Harmony postfix on <c>Tile.RenderDebug</c>.
    /// If the <c>showCoordinates</c> setting is on, it adds a child object with white text that shows the tile coordinates.
    /// The text uses the terrain sorting layer, above the tile.
    /// </summary>
    /// <param name="__instance">The tile.</param>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Tile), nameof(Tile.RenderDebug))]
    public static void Tile_RenderDebug(Tile __instance)
    {
        if (!Plugin.config.showCoordinates)
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

    /// <summary>
    /// Gets the terrain sprite name for a land tile.
    /// A tile that is not flooded uses the field sprite name.
    /// A flooded tile uses the wetland sprite name.
    /// A swamped tile adds the <c>_swamp</c> suffix to the wetland name.
    /// A tile with a custom flood effect adds an underscore and the name of the related skin.
    /// </summary>
    /// <param name="tileData">The data of the tile.</param>
    /// <returns>The terrain sprite name for the tile.</returns>
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

    /// <summary>
    /// A Harmony prefix on <c>TerrainRenderer.UpdateGraphics</c> that replaces the original method.
    /// Water, ocean, and ice tiles use the sprite names from the sprite sets of the renderer.
    /// Other tiles use the name from <see cref="GetLandSpriteName"/>.
    /// The patch finds the sprite for the climate and skin, and sets it if the lookup has a sprite.
    /// It also changes the desaturation of the sprite when the requested state is different.
    /// </summary>
    /// <param name="__instance">The terrain renderer.</param>
    /// <param name="tile">The tile to show.</param>
    /// <param name="climate">The tribe that gives the climate of the tile.</param>
    /// <param name="skin">The skin of the tile.</param>
    /// <param name="shouldDesaturate">If true, the renderer shows the sprite without color saturation.</param>
    /// <returns>Always false, which skips the original method.</returns>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(TerrainRenderer), nameof(TerrainRenderer.UpdateGraphics))] // Why prefix? I really hate repeating the logic. I would have to do the same bs 2 times.... anyway reviewers tell me ok?
    private static bool TerrainRenderer_UpdateGraphics(TerrainRenderer __instance, Tile tile, TribeType climate, SkinType skin, bool shouldDesaturate)
    {

        string spriteName = tile.Data.terrain switch
        {
            Polytopia.Data.TerrainData.Type.Water => __instance.waterSprites.GetSpriteName(tile),
            Polytopia.Data.TerrainData.Type.Ocean => __instance.oceanSprites.GetSpriteName(tile),
            Polytopia.Data.TerrainData.Type.Ice => __instance.iceSprites.GetSpriteName(tile),
            _ => GetLandSpriteName(tile.Data),
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

    /// <summary>
    /// A Harmony postfix on <c>TileData.Flood</c>.
    /// If the skin of the player is in <see cref="customFloodingSkins"/>, it adds the custom flood effect of that skin to the tile.
    /// </summary>
    /// <param name="__instance">The tile that is flooded.</param>
    /// <param name="playerState">The player that floods the tile.</param>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(TileData), nameof(TileData.Flood))]
    private static void TileData_Flood(TileData __instance, PlayerState playerState)
    {
        if (customFloodingSkins.ContainsValue(playerState.skinType))
        {
            TileData.EffectType effectType = customFloodingSkins.FirstOrDefault(x => x.Value == playerState.skinType).Key;
            __instance.AddEffect(effectType);
        }
    }

    /// <summary>
    /// A Harmony postfix on <c>TileTipGenerator.GetTileTipHeader</c>.
    /// It removes the localized name of each custom flood effect on the tile from the header text.
    /// </summary>
    /// <param name="__result">The header text that the original method gives.</param>
    /// <param name="tileData">The data of the tile.</param>
    /// <param name="skin">The skin that the localization uses.</param>
    /// <param name="playerState">The player whose tribe the localization uses.</param>
    /// <param name="gameState">The state of the game. The patch does not use it.</param>
    /// <param name="gameLogicData">The game logic data. The patch does not use it.</param>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(TileTipGenerator), nameof(TileTipGenerator.GetTileTipHeader))]
    public static void GetTileTipHeader(ref string __result, TileData tileData, SkinType skin,
                                            PlayerState playerState, GameState gameState, GameLogicData gameLogicData)
    {
        foreach (var effect in tileData.effects)
        {
            if (customFloodingSkins.ContainsKey(effect))
            {
                string localizedEffect = Localization.GetSkinned(skin, playerState.tribe, $"tile.effect.{effect.GetName()}");
                __result = __result.Replace($", {localizedEffect}", string.Empty);
            }
        }
    }

    /// <summary>
    /// A Harmony postfix on <c>PolytopiaSpriteRenderer.ForceUpdateMesh</c>.
    /// For a renderer that has a sprite and no atlas name, it sets a material property block on the mesh renderer.
    /// The block sets <c>_MainTex</c> to the sprite texture and sets <c>_Flip</c> to no flip.
    /// </summary>
    /// <param name="__instance">The sprite renderer.</param>
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

    /// <summary>
    /// A Harmony postfix on <c>PopupBase.RefreshHeightWhenHeightIsKnownAndDontUseCoroutines</c>.
    /// It applies the custom width of the popup, if the popup has one.
    /// </summary>
    /// <param name="__instance">The popup.</param>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(PopupBase), nameof(PopupBase.RefreshHeightWhenHeightIsKnownAndDontUseCoroutines))]
    private static void PopupBase_RefreshHeightWhenHeightIsKnownAndDontUseCoroutines(PopupBase __instance)
    {
        UpdateWidth(__instance);
    }

    /// <summary>
    /// Applies the custom width from <see cref="basicPopupWidths"/> to a popup.
    /// If the popup is a <c>BasicPopupLegacy</c> with a button container, the method also arranges the buttons again.
    /// If the popup has no custom width, the method does nothing.
    /// </summary>
    /// <param name="popup">The popup to change.</param>
    private static void UpdateWidth(PopupBase popup)
    {
        if (!basicPopupWidths.TryGetValue(popup.GetInstanceID(), out PopupWidth? width)) return;

        popup.rectTransform.SetWidth(GetResponsivePopupWidth(width.share, width.maximum));
        BasicPopupLegacy? legacy = popup.TryCast<BasicPopupLegacy>();
        if (legacy != null && legacy.buttonContainer != null)
            LayoutPopupButtons(legacy.buttonContainer);
    }

    /// <summary>
    /// A Harmony postfix on <c>SystemEvents.ScreenSizeChanged</c>.
    /// It refreshes the size of each shown popup that has a custom width.
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(SystemEvents), nameof(SystemEvents.ScreenSizeChanged))]
    private static void SystemEvents_ScreenSizeChanged()
    {
        RelayoutSizedPopups();
    }

    /// <summary>
    /// A Harmony postfix on <c>SystemEvents.SafeAreaChanged</c>.
    /// It refreshes the size of each shown popup that has a custom width.
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(SystemEvents), nameof(SystemEvents.SafeAreaChanged))]
    private static void SystemEvents_SafeAreaChanged()
    {
        RelayoutSizedPopups();
    }

    /// <summary>
    /// Refreshes the size of each shown popup in <see cref="basicPopupWidths"/>.
    /// The method calls <c>RefreshHeightWhenHeightIsKnownAndDontUseCoroutines</c> with the safe screen height.
    /// It writes an error to the log for each popup that fails, and continues with the next popup.
    /// </summary>
    private static void RelayoutSizedPopups()
    {
        foreach (PopupWidth width in new List<PopupWidth>(basicPopupWidths.Values))
        {
            try
            {
                if (width.popup == null || !width.popup.IsShowing()) continue;
                width.popup.RefreshHeightWhenHeightIsKnownAndDontUseCoroutines(ScreenManager.SafeHeight);
            }
            catch (Exception exception)
            {
                Plugin.logger.LogError($"Could not lay a popup out after a screen size change: {exception}");
            }
        }
    }

    /// <summary>
    /// Changes a share of the usable screen width into a popup width in canvas units.
    /// The method changes the safe screen width from screen pixels to canvas units with the UI scale of the game.
    /// If <paramref name="share"/> is 0 or less, the method gives <paramref name="maximum"/> as a fixed width.
    /// </summary>
    /// <param name="share">The share of the usable width, from 0 to 1.</param>
    /// <param name="maximum">The maximum width in canvas units. It keeps text easy to read on wide screens.</param>
    /// <returns>
    /// The width in canvas units, rounded to an integer.
    /// For a share above 0, the width is not smaller than the width of the game's own popups.
    /// </returns>
    public static int GetResponsivePopupWidth(float share, float maximum)
    {
        if (share <= 0f) return Mathf.RoundToInt(maximum);
        float usable = ScreenManager.SafeWidth * UICanvasScalerHelper.GetInvertedUIScale();
        float width = Mathf.Min(usable * share, maximum);
        return Mathf.RoundToInt(Mathf.Max(width, DEFAULT_POPUP_WIDTH));
    }

    /// <summary>
    /// A Harmony postfix on <c>PopupButtonContainer.SetButtonData</c>.
    /// It replaces the button positions that the game sets with the positions from <see cref="LayoutPopupButtons"/>.
    /// The game puts all buttons in one row, so buttons can show on top of each other in a narrow popup.
    /// The patch applies to the buttons of all popups.
    /// </summary>
    /// <param name="__instance">The button container of the popup.</param>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(PopupButtonContainer), nameof(PopupButtonContainer.SetButtonData))]
    private static void PopupButtonContainer_SetButtonData(PopupButtonContainer __instance)
    {
        LayoutPopupButtons(__instance);
    }

    /// <summary>
    /// Arranges the buttons of a popup in rows that fit the width of the container.
    /// All rows have the height of the highest button, with <see cref="POPUP_BUTTON_ROW_GAP"/> between them.
    /// The method sets the height of the container to the height of all rows, and puts the first row at the top.
    /// If the container has no buttons or no width, the method does nothing.
    /// </summary>
    /// <param name="container">The button container of the popup.</param>
    internal static void LayoutPopupButtons(PopupButtonContainer container)
    {
        if (container.buttons == null || container.buttons.Length == 0) return;

        float available = container.rectTransform.rect.width;
        if (available <= 0f) return;

        List<List<UITextButton>> rows = PackButtonsIntoRows(container, available);

        float rowHeight = 0f;
        foreach (UITextButton button in container.buttons)
        {
            rowHeight = Mathf.Max(rowHeight, button.rectTransform.rect.height);
        }
        float stackHeight = rows.Count * rowHeight + (rows.Count - 1) * POPUP_BUTTON_ROW_GAP;

        // The container sits at the bottom of the popup with a bottom pivot, so it grows upwards.
        container.rectTransform.SetHeight(stackHeight);

        for (int row = 0; row < rows.Count; row++)
        {
            float offsetY = stackHeight / 2f - rowHeight / 2f - row * (rowHeight + POPUP_BUTTON_ROW_GAP);
            PlaceButtonRow(rows[row], offsetY);
        }
    }

    /// <summary>
    /// Puts the buttons of a container into rows, in their original sequence.
    /// The method adds each button to the current row.
    /// If the button and the gap do not fit in the remaining width, the method starts a new row.
    /// A row always contains at least one button, also if that button is wider than the container.
    /// </summary>
    /// <param name="container">The button container of the popup.</param>
    /// <param name="available">The width of the container, in canvas units.</param>
    /// <returns>The rows of buttons, from the first row to the last row.</returns>
    private static List<List<UITextButton>> PackButtonsIntoRows(PopupButtonContainer container, float available)
    {
        List<List<UITextButton>> rows = new() { new() };
        float rowWidth = 0f;
        foreach (UITextButton button in container.buttons)
        {
            float width = button.rectTransform.rect.width;
            if (rows[^1].Count > 0 && rowWidth + POPUP_BUTTON_GAP + width > available)
            {
                rows.Add(new());
                rowWidth = 0f;
            }
            rowWidth += (rows[^1].Count > 0 ? POPUP_BUTTON_GAP : 0f) + width;
            rows[^1].Add(button);
        }
        return rows;
    }

    /// <summary>
    /// Puts one row of buttons across the width of the container.
    /// The method anchors each button to a fraction of the container width, from the left edge to the right edge.
    /// A row with one button goes to the center.
    /// </summary>
    /// <param name="row">The buttons in the row.</param>
    /// <param name="offsetY">The vertical position of the row from the center of the container, in canvas units.</param>
    private static void PlaceButtonRow(List<UITextButton> row, float offsetY)
    {
        for (int i = 0; i < row.Count; i++)
        {
            // Anchoring to a fraction of the container keeps the row spread as the popup resizes,
            // which is how the game places the buttons of its own popups.
            Vector2 anchor = new((row.Count == 1) ? 0.5f : (i / (row.Count - 1.0f)), 0.5f);
            RectTransform rect = row[i].rectTransform;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = new Vector2(0f, offsetY);
        }
    }

    /// <summary>
    /// A Harmony postfix on <c>PopupBase.Hide</c>.
    /// It removes the entry of the popup from <see cref="basicPopupWidths"/>.
    /// </summary>
    /// <param name="__instance">The popup that closes.</param>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(PopupBase), nameof(PopupBase.Hide))]
    private static void PopupBase_Hide(PopupBase __instance)
    {
        basicPopupWidths.Remove(__instance.GetInstanceID());
    }

    /// <summary>
    /// A Harmony postfix on <c>StartScreen_UI2.OnShow</c>.
    /// It calls <see cref="CreateInnerOriginSprites"/> to make the sprites that use game sprites as a source.
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(StartScreen_UI2), nameof(StartScreen_UI2.OnShow))]
    private static void StartScreen_UI2_OnShow()
    {
        CreateInnerOriginSprites();
    }

    /// <summary>
    /// A Harmony prefix on <c>StartScreen_UI2.OnWeeklyChallengeClicked</c>.
    /// It shows a popup that tells the player that the weekly challenge is only for the game without mods.
    /// The popup has one OK button.
    /// </summary>
    /// <returns>Always false, which skips the original method.</returns>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(StartScreen_UI2), nameof(StartScreen_UI2.OnWeeklyChallengeClicked))]
    private static bool StartScreen_OnWeeklyChallengeClicked()
    {
        BasicPopup popup = PopupManager.GetBasicPopup();
        popup.Header = Localization.Get("polymod.hub");
        popup.Description = Localization.Get(
            "polymod.wc.vanillaonly",
            new Il2CppSystem.Object[] { Localization.Get("weeklychallenge", Array.Empty<Il2CppSystem.Object>()) }
        );
        popup.buttonData = new PopupBase.PopupButtonData[] { new("buttons.ok") };
        popup.Show();
        return false;
    }

    /// <summary>
    /// A Harmony postfix on <c>StartScreen_UI2.RefreshWeeklyChallengeButton</c>.
    /// It sets all colors of the weekly challenge button to gray, to show that the button is not available.
    /// If the start screen has no weekly challenge button, the patch does nothing.
    /// </summary>
    /// <param name="__instance">The start screen.</param>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(StartScreen_UI2), nameof(StartScreen_UI2.RefreshWeeklyChallengeButton))]
    private static void StartScreen_RefreshWeeklyChallengeButton(StartScreen_UI2 __instance)
    {
        WeeklyChallengesButton weeklyChallengeButton = __instance.weeklyChallengeButton;
        if (weeklyChallengeButton == null)
            return;

        weeklyChallengeButton.SetColors(
            WEEKLY_CHALLENGE_DISABLED_COLOR,
            WEEKLY_CHALLENGE_DISABLED_COLOR,
            WEEKLY_CHALLENGE_DISABLED_COLOR
        );
    }

    private const string MIGHTYLEADER = "mightyleader";

    /// <summary>
    /// A Harmony postfix on <c>PopupContentBuilder.GetMightyLeaderTopImage</c>.
    /// It replaces the image with the registered <c>mightyleader</c> sprite for the tribe and visual skin of the player, if one exists.
    /// </summary>
    /// <param name="__result">The image that the original method gives.</param>
    /// <param name="player">The player that the popup is for.</param>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(PopupContentBuilder), nameof(PopupContentBuilder.GetMightyLeaderTopImage))]
    public static void PopupContentBuilder_GetMightyLeaderTopImage(ref Sprite __result, PlayerState player)
    {
        Sprite? sprite = Registry.GetSprite(MIGHTYLEADER, player.tribe, player.GetVisualSkin());
        if (sprite != null)
            __result = sprite;
    }

    /// <summary>
    /// Shows a popup with a fixed custom width.
    /// The method adds the width to <see cref="basicPopupWidths"/> and then shows the popup.
    /// </summary>
    /// <param name="self">The popup to show.</param>
    /// <param name="width">The width of the popup, in canvas units.</param>
    public static void ShowSetWidth(this BasicPopupLegacy self, int width)
    {
        basicPopupWidths[self.GetInstanceID()] = new(self, 0f, width);
        self.Show();
    }

    /// <summary>
    /// Shows a popup with a width that is a share of the usable screen width.
    /// The method adds the width settings to <see cref="basicPopupWidths"/> and then shows the popup.
    /// <see cref="GetResponsivePopupWidth"/> calculates the width.
    /// </summary>
    /// <param name="self">The popup to show.</param>
    /// <param name="share">The share of the usable screen width, from 0 to 1.</param>
    /// <param name="maximum">The maximum width of the popup, in canvas units.</param>
    public static void ShowSetWidth(this BasicPopupLegacy self, float share, float maximum)
    {
        basicPopupWidths[self.GetInstanceID()] = new(self, share, maximum);
        self.Show();
    }

    #endregion

    /// <summary>
    /// Makes a new <c>BasicPopupLegacy</c> from the popup prefab of the game.
    /// The method copies the button container of the "What's new" popup into the new popup.
    /// It sets the popup ID to <c>basicPopupLegacy</c> and calls <c>Init</c> on the popup.
    /// It also puts the popup in front of the other popups.
    /// </summary>
    /// <returns>The new popup. It is not shown.</returns>
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

    /// <summary>
    /// Makes a texture from the bytes of an image file, such as a PNG file.
    /// If the image looks premultiplied, the method changes it back to straight alpha.
    /// The method also copies color into the transparent pixels near the edges of the image.
    /// The texture uses trilinear filter mode, clamp wrap mode, and mipmaps.
    /// </summary>
    /// <param name="data">The bytes of the image file.</param>
    /// <returns>The new texture.</returns>
    public static Texture2D BuildTexture(byte[] data)
    {
        Texture2D texture = new(1, 1, TextureFormat.RGBA32, true);
        texture.LoadImage(data);
        Color32[] pixels = texture.GetPixels32();
        if (LooksPremultiplied(pixels))
            Unpremultiply(pixels);
        BleedAlphaEdges(pixels, texture.width, texture.height);
        texture.SetPixels32(pixels);
        texture.filterMode = FilterMode.Trilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.Apply();
        return texture;
    }

    /// <summary>
    /// Finds if the pixels of an image look premultiplied by alpha.
    /// The method examines only the pixels that are partly transparent.
    /// In a premultiplied image, no color channel of such a pixel is more than its alpha.
    /// </summary>
    /// <param name="pixels">The pixels of the image.</param>
    /// <returns>
    /// True if the image has at least 64 partly transparent pixels, and not more than 0.5% of them have a color channel above alpha.
    /// False if not.
    /// </returns>
    internal static bool LooksPremultiplied(Color32[] pixels)
    {
        int edge = 0;
        int brighterThanAlpha = 0;
        foreach (Color32 pixel in pixels)
        {
            if (pixel.a == 0 || pixel.a == 255) continue;
            edge++;
            if (Math.Max(pixel.r, Math.Max(pixel.g, pixel.b)) > pixel.a)
                brighterThanAlpha++;
        }
        return edge >= PREMULTIPLIED_MIN_EDGE_PIXELS
            && brighterThanAlpha <= edge * PREMULTIPLIED_MAX_OUTLIER_SHARE;
    }

    /// <summary>
    /// Changes premultiplied pixels to straight alpha in the same array.
    /// The method divides the color channels of each partly transparent pixel by its alpha, with a maximum of 255.
    /// It does not change fully transparent pixels and fully opaque pixels.
    /// </summary>
    /// <param name="pixels">The pixels to change.</param>
    internal static void Unpremultiply(Color32[] pixels)
    {
        for (int i = 0; i < pixels.Length; i++)
        {
            byte alpha = pixels[i].a;
            if (alpha == 0 || alpha == byte.MaxValue) continue;
            pixels[i] = new Color32(
                (byte)Math.Min(byte.MaxValue, pixels[i].r * byte.MaxValue / alpha),
                (byte)Math.Min(byte.MaxValue, pixels[i].g * byte.MaxValue / alpha),
                (byte)Math.Min(byte.MaxValue, pixels[i].b * byte.MaxValue / alpha),
                alpha
            );
        }
    }

    /// <summary>
    /// Copies the color of visible pixels into the adjacent transparent pixels, in the same array.
    /// This stops dark edges around a sprite when the GPU filters the texture.
    /// A pixel with an alpha of 2 or less has no color for this method.
    /// The method spreads the color one pixel at a time, for a maximum of 16 pixels from the visible area.
    /// It keeps the alpha of each pixel.
    /// If all pixels or no pixels are visible, the method does nothing.
    /// </summary>
    /// <param name="pixels">The pixels of the image, row by row.</param>
    /// <param name="width">The width of the image, in pixels.</param>
    /// <param name="height">The height of the image, in pixels.</param>
    internal static void BleedAlphaEdges(Color32[] pixels, int width, int height)
    {
        bool[] hasColour = new bool[pixels.Length];
        List<int> frontier = new();
        for (int i = 0; i < pixels.Length; i++)
        {
            if (pixels[i].a <= INVISIBLE_ALPHA) continue;
            hasColour[i] = true;
            frontier.Add(i);
        }
        if (frontier.Count == 0 || frontier.Count == pixels.Length) return;

        for (int ring = 0; ring < ALPHA_BLEED_RINGS && frontier.Count > 0; ring++)
        {
            List<int> nextFrontier = new();
            foreach (int index in frontier)
            {
                int x = index % width;
                int y = index / width;
                Color32 source = pixels[index];

                for (int offsetY = -1; offsetY <= 1; offsetY++)
                {
                    int neighbourY = y + offsetY;
                    if (neighbourY < 0 || neighbourY >= height) continue;

                    for (int offsetX = -1; offsetX <= 1; offsetX++)
                    {
                        int neighbourX = x + offsetX;
                        if (neighbourX < 0 || neighbourX >= width) continue;

                        int neighbour = neighbourY * width + neighbourX;
                        if (hasColour[neighbour]) continue;

                        hasColour[neighbour] = true;
                        pixels[neighbour] = new Color32(
                            source.r, source.g, source.b, pixels[neighbour].a
                        );
                        nextFrontier.Add(neighbour);
                    }
                }
            }
            frontier = nextFrontier;
        }
    }

    /// <summary>
    /// Makes a sprite from the bytes of an image file.
    /// The method makes the texture with <see cref="BuildTexture"/> and the sprite with <see cref="BuildSpriteWithTexture"/>.
    /// </summary>
    /// <param name="data">The bytes of the image file.</param>
    /// <param name="pivot">The pivot of the sprite, as a fraction of the sprite size. If null, the pivot is the center.</param>
    /// <param name="pixelsPerUnit">The number of texture pixels in one world unit.</param>
    /// <param name="transformToOutline">If true, the image changes into a white silhouette with a border.</param>
    /// <param name="gaussianBlurRadius">The radius of the Gaussian blur, or null for no blur.</param>
    /// <returns>The new sprite.</returns>
    public static Sprite BuildSprite(byte[] data, Vector2? pivot = null, float pixelsPerUnit = 2112f, bool transformToOutline = false, float? gaussianBlurRadius = null)
    {
        return BuildSpriteWithTexture(BuildTexture(data), pivot, pixelsPerUnit, transformToOutline, gaussianBlurRadius);
    }

    /// <summary>
    /// Makes a sprite that uses all of a texture.
    /// If <paramref name="transformToOutline"/> is true, the method first changes the texture with <see cref="TransformToOutline"/>.
    /// Then, if the blur radius is not null, it applies <see cref="ApplyGaussianBlur"/>.
    /// </summary>
    /// <param name="texture">The texture for the sprite.</param>
    /// <param name="pivot">The pivot of the sprite, as a fraction of the sprite size. If null, the pivot is the center.</param>
    /// <param name="pixelsPerUnit">The number of texture pixels in one world unit. If null, the method uses 2112.</param>
    /// <param name="transformToOutline">If true, the image changes into a white silhouette with a border.</param>
    /// <param name="gaussianBlurRadius">The radius of the Gaussian blur, or null for no blur.</param>
    /// <returns>The new sprite.</returns>
    public static Sprite BuildSpriteWithTexture(Texture2D texture, Vector2? pivot = null, float? pixelsPerUnit = 2112f, bool transformToOutline = false, float? gaussianBlurRadius = null)
    {
        if (transformToOutline) texture = TransformToOutline(texture);

        if (gaussianBlurRadius != null) texture = ApplyGaussianBlur(texture, gaussianBlurRadius.Value);

        return Sprite.Create(
            texture,
            new(0, 0, texture.width, texture.height),
            pivot ?? new(0.5f, 0.5f),
            pixelsPerUnit ?? 2112f
        ); ;
    }

    /// <summary>
    /// Makes a silhouette texture with a border from the visible pixels of a texture.
    /// The new texture is larger than the source texture by the border size on each side.
    /// Each pixel within the border size of a visible source pixel gets the given color.
    /// All other pixels are transparent.
    /// A source pixel is visible if its alpha is more than 0.01.
    /// </summary>
    /// <param name="sourceTexture">The texture that gives the shape.</param>
    /// <param name="borderSizeInPixels">The width of the border around the shape, in pixels.</param>
    /// <param name="color">The color of the silhouette. If null, the method uses white.</param>
    /// <returns>A new RGBA32 texture without mipmaps.</returns>
    public static Texture2D TransformToOutline(Texture2D sourceTexture, int borderSizeInPixels = 8, Color? color = null)
    {
        Plugin.logger.LogInfo("Transforming texture to outline...");
        if (color == null)
            color = Color.white;

        int sourceWidthInPixels = sourceTexture.width;
        int sourceHeightInPixels = sourceTexture.height;
        int outputWidthInPixels = sourceWidthInPixels + borderSizeInPixels * 2;
        int outputHeightInPixels = sourceHeightInPixels + borderSizeInPixels * 2;

        Color[] sourcePixels = sourceTexture.GetPixels();
        bool[] filledMask = new bool[outputWidthInPixels * outputHeightInPixels];
        int borderSquared = borderSizeInPixels * borderSizeInPixels;

        for (int pixelY = 0; pixelY < sourceHeightInPixels; pixelY++)
        {
            for (int pixelX = 0; pixelX < sourceWidthInPixels; pixelX++)
            {
                if (sourcePixels[pixelY * sourceWidthInPixels + pixelX].a <= 0.01f) continue;

                int centerX = pixelX + borderSizeInPixels;
                int centerY = pixelY + borderSizeInPixels;

                for (int offsetY = -borderSizeInPixels; offsetY <= borderSizeInPixels; offsetY++)
                {
                    for (int offsetX = -borderSizeInPixels; offsetX <= borderSizeInPixels; offsetX++)
                    {
                        if (offsetX * offsetX + offsetY * offsetY > borderSquared) continue;

                        filledMask[(centerY + offsetY) * outputWidthInPixels + (centerX + offsetX)] = true;
                    }
                }
            }
        }

        Color[] outputPixels = new Color[filledMask.Length];
        for (int pixelIndex = 0; pixelIndex < outputPixels.Length; pixelIndex++)
        {
            outputPixels[pixelIndex] = filledMask[pixelIndex] ? color.Value : Color.clear;
        }

        Texture2D whiteSilhouetteTexture = new Texture2D(outputWidthInPixels, outputHeightInPixels, TextureFormat.RGBA32, false);
        whiteSilhouetteTexture.SetPixels(outputPixels);
        whiteSilhouetteTexture.Apply();
        return whiteSilhouetteTexture;
    }

    /// <summary>
    /// Makes the weights of a one-dimensional Gaussian kernel. The sum of the weights is 1.
    /// </summary>
    /// <param name="sigma">The standard deviation of the Gaussian function, in pixels.</param>
    /// <param name="kernelRadiusInPixels">The number of weights on each side of the center weight.</param>
    /// <returns>An array of <c>2 * kernelRadiusInPixels + 1</c> weights.</returns>
    private static float[] BuildGaussianKernelWeights(float sigma, int kernelRadiusInPixels)
    {
        float[] kernelWeights = new float[kernelRadiusInPixels * 2 + 1];
        float kernelWeightSum = 0f;
        for (int kernelIndex = 0; kernelIndex < kernelWeights.Length; kernelIndex++)
        {
            int distanceFromCenter = kernelIndex - kernelRadiusInPixels;
            kernelWeights[kernelIndex] = MathF.Exp(-(distanceFromCenter * distanceFromCenter) / (2f * sigma * sigma));
            kernelWeightSum += kernelWeights[kernelIndex];
        }
        for (int kernelIndex = 0; kernelIndex < kernelWeights.Length; kernelIndex++)
        {
            kernelWeights[kernelIndex] /= kernelWeightSum;
        }
        return kernelWeights;
    }

    /// <summary>
    /// Changes a value from the range 0 to 1 into a byte from 0 to 255, rounded to the nearest integer.
    /// The method clamps values that are out of the range.
    /// </summary>
    /// <param name="unitValue">The value to change.</param>
    /// <returns>The byte value.</returns>
    private static byte ConvertUnitFloatToByte(float unitValue)
    {
        return (byte)Math.Clamp(unitValue * 255f + 0.5f, 0f, 255f);
    }

    // basically ai generated as my bs was working, but 15 times slower
    /// <summary>
    /// Makes a blurred copy of a texture with a Gaussian blur.
    /// The method blurs the rows and then the columns, on more than one thread.
    /// It blurs premultiplied colors, so transparent pixels do not make the edges dark.
    /// At the edges of the texture, the method uses the nearest edge pixel.
    /// The standard deviation is half of the radius, and the kernel reaches three standard deviations.
    /// </summary>
    /// <param name="sourceTexture">The texture to blur. The method does not change it.</param>
    /// <param name="radius">The radius of the blur, in pixels.</param>
    /// <returns>A new RGBA32 texture without mipmaps.</returns>
    public static Texture2D ApplyGaussianBlur(Texture2D sourceTexture, float radius = 2f)
    {
        Plugin.logger.LogInfo($"Applying blur with radius {radius}...");
        int textureWidthInPixels = sourceTexture.width;
        int textureHeightInPixels = sourceTexture.height;

        float sigma = Math.Max(radius / 2f, 0.01f);
        int kernelRadiusInPixels = Math.Max(1, (int)MathF.Ceiling(sigma * 3f));
        float[] kernelWeights = BuildGaussianKernelWeights(sigma, kernelRadiusInPixels);

        Color32[] sourcePixels = sourceTexture.GetPixels32();
        int totalPixelCount = sourcePixels.Length;
        const float byteToUnitFloat = 1f / 255f;

        float[] premultipliedChannels = new float[totalPixelCount * 4];
        for (int pixelIndex = 0; pixelIndex < totalPixelCount; pixelIndex++)
        {
            Color32 sourcePixel = sourcePixels[pixelIndex];
            float alpha = sourcePixel.a * byteToUnitFloat;
            int channelOffset = pixelIndex * 4;
            premultipliedChannels[channelOffset + 0] = sourcePixel.r * byteToUnitFloat * alpha;
            premultipliedChannels[channelOffset + 1] = sourcePixel.g * byteToUnitFloat * alpha;
            premultipliedChannels[channelOffset + 2] = sourcePixel.b * byteToUnitFloat * alpha;
            premultipliedChannels[channelOffset + 3] = alpha;
        }

        float[] horizontallyBlurredChannels = new float[premultipliedChannels.Length];
        Parallel.For(0, textureHeightInPixels, pixelY =>
        {
            int rowStartPixelIndex = pixelY * textureWidthInPixels;
            for (int pixelX = 0; pixelX < textureWidthInPixels; pixelX++)
            {
                float accumulatedRed = 0f, accumulatedGreen = 0f, accumulatedBlue = 0f, accumulatedAlpha = 0f;
                for (int kernelIndex = 0; kernelIndex < kernelWeights.Length; kernelIndex++)
                {
                    int sampleX = pixelX + kernelIndex - kernelRadiusInPixels;
                    if (sampleX < 0) sampleX = 0;
                    else if (sampleX >= textureWidthInPixels) sampleX = textureWidthInPixels - 1;

                    int sampleOffset = (rowStartPixelIndex + sampleX) * 4;
                    float kernelWeight = kernelWeights[kernelIndex];
                    accumulatedRed += premultipliedChannels[sampleOffset + 0] * kernelWeight;
                    accumulatedGreen += premultipliedChannels[sampleOffset + 1] * kernelWeight;
                    accumulatedBlue += premultipliedChannels[sampleOffset + 2] * kernelWeight;
                    accumulatedAlpha += premultipliedChannels[sampleOffset + 3] * kernelWeight;
                }
                int outputOffset = (rowStartPixelIndex + pixelX) * 4;
                horizontallyBlurredChannels[outputOffset + 0] = accumulatedRed;
                horizontallyBlurredChannels[outputOffset + 1] = accumulatedGreen;
                horizontallyBlurredChannels[outputOffset + 2] = accumulatedBlue;
                horizontallyBlurredChannels[outputOffset + 3] = accumulatedAlpha;
            }
        });

        Color32[] blurredPixels = new Color32[totalPixelCount];
        Parallel.For(0, textureHeightInPixels, pixelY =>
        {
            for (int pixelX = 0; pixelX < textureWidthInPixels; pixelX++)
            {
                float accumulatedRed = 0f, accumulatedGreen = 0f, accumulatedBlue = 0f, accumulatedAlpha = 0f;
                for (int kernelIndex = 0; kernelIndex < kernelWeights.Length; kernelIndex++)
                {
                    int sampleY = pixelY + kernelIndex - kernelRadiusInPixels;
                    if (sampleY < 0) sampleY = 0;
                    else if (sampleY >= textureHeightInPixels) sampleY = textureHeightInPixels - 1;

                    int sampleOffset = (sampleY * textureWidthInPixels + pixelX) * 4;
                    float kernelWeight = kernelWeights[kernelIndex];
                    accumulatedRed += horizontallyBlurredChannels[sampleOffset + 0] * kernelWeight;
                    accumulatedGreen += horizontallyBlurredChannels[sampleOffset + 1] * kernelWeight;
                    accumulatedBlue += horizontallyBlurredChannels[sampleOffset + 2] * kernelWeight;
                    accumulatedAlpha += horizontallyBlurredChannels[sampleOffset + 3] * kernelWeight;
                }

                Color32 blurredPixel = default;
                if (accumulatedAlpha > 0.0001f)
                {
                    blurredPixel.r = ConvertUnitFloatToByte(accumulatedRed / accumulatedAlpha);
                    blurredPixel.g = ConvertUnitFloatToByte(accumulatedGreen / accumulatedAlpha);
                    blurredPixel.b = ConvertUnitFloatToByte(accumulatedBlue / accumulatedAlpha);
                    blurredPixel.a = ConvertUnitFloatToByte(accumulatedAlpha);
                }
                blurredPixels[pixelY * textureWidthInPixels + pixelX] = blurredPixel;
            }
        });

        Texture2D blurredTexture = new Texture2D(textureWidthInPixels, textureHeightInPixels, TextureFormat.RGBA32, false);
        blurredTexture.SetPixels32(blurredPixels);
        blurredTexture.Apply();
        return blurredTexture;
    }

    /// <summary>
    /// The sprite names and sprite infos of the sprites that have an <see cref="OriginType.Inner"/> origin.
    /// <see cref="CreateSprites"/> fills this list, and <see cref="CreateInnerOriginSprites"/> makes the sprites and clears it.
    /// </summary>
    internal static List<KeyValuePair<string, SpriteInfo>> pendingInnerOriginSpriteInfos = new();

    /// <summary>
    /// Gets the default pivot for a sprite from the part of its name before the first underscore.
    /// </summary>
    /// <param name="spriteName">The name of the sprite.</param>
    /// <returns>
    /// The pivot (0.5, 0) for <c>field</c>, the pivot (0.5, -0.375) for <c>mountain</c>, and the center for all other names.
    /// </returns>
    private static Vector2 GetPivotPreset(string spriteName)
    {
        return spriteName.Split("_")[0] switch
        {
            "field" => new Vector2(0.5f, 0.0f),
            "mountain" => new Vector2(0.5f, -0.375f),
            _ => new Vector2(0.5f, 0.5f),
        };
    }

    /// <summary>
    /// Adds a sprite to the <c>Heads</c> cache of the sprite atlas manager and to <see cref="Registry.sprites"/>.
    /// If a sprite with the same name exists, the method replaces it.
    /// The method writes the sprite name to the log.
    /// </summary>
    /// <param name="spriteAtlasManager">The sprite atlas manager of the game.</param>
    /// <param name="spriteName">The name of the sprite.</param>
    /// <param name="sprite">The sprite to add.</param>
    internal static void RegisterSprite(SpriteAtlasManager spriteAtlasManager, string spriteName, Sprite sprite)
    {
        Plugin.logger.LogInfo($"Registering sprite {spriteName}");
        spriteAtlasManager.cachedSprites.TryAdd("Heads", new());
        spriteAtlasManager.cachedSprites["Heads"][spriteName] = sprite;
        Registry.sprites[spriteName] = sprite;
    }

    // Runs early: only touches your own textures, nothing from SpriteAtlasManager's lookup state.
    /// <summary>
    /// Makes and registers the sprites from mod textures.
    /// First, the method makes a sprite for each entry in <see cref="Registry.spriteInfos"/>.
    /// It uses the texture with the sprite name, or else the texture with the <see cref="OriginType.Outer"/> origin name.
    /// It skips an entry without a texture, and keeps <see cref="OriginType.Inner"/> entries for <see cref="CreateInnerOriginSprites"/>.
    /// It marks the texture entry with the sprite name as processed in <see cref="Registry.textures"/>.
    /// Then it makes a sprite with the default settings for each texture that is not processed.
    /// A texture that only an origin name uses is not processed, so it also gets a sprite with its own name.
    /// </summary>
    public static void CreateSprites()
    {
        Plugin.logger.LogInfo($"Creating sprites with outer origin...");
        pendingInnerOriginSpriteInfos.Clear();

        SpriteAtlasManager spriteAtlasManager = GameManager.GetSpriteAtlasManager();
        const float defaultPixelsPerUnit = 2112f;

        foreach (KeyValuePair<string, SpriteInfo> spriteInfoPair in Registry.spriteInfos)
        {
            string spriteName = spriteInfoPair.Key;
            SpriteInfo spriteInfo = spriteInfoPair.Value;

            bool isInnerOrigin = spriteInfo.spriteOrigin != null
                && spriteInfo.spriteOrigin.originType == OriginType.Inner;

            if (isInnerOrigin)
            {
                pendingInnerOriginSpriteInfos.Add(spriteInfoPair);
                continue;
            }

            KeyValuePair<Texture2D, bool> textureEntry;
            if (!Registry.textures.TryGetValue(spriteName, out textureEntry)) // search texture
            {
                if (spriteInfo.spriteOrigin == null ||
                    spriteInfo.spriteOrigin.originType != OriginType.Outer ||
                    spriteInfo.spriteOrigin.name == null ||
                    !Registry.textures.TryGetValue(spriteInfo.spriteOrigin.name, out textureEntry)) // search texture in outer sprite origin
                {
                    continue;
                }
            }


            Vector2 pivot = spriteInfo.pivot ?? GetPivotPreset(spriteName);
            float pixelsPerUnit = spriteInfo.pixelsPerUnit ?? defaultPixelsPerUnit;
            bool transformToOutline = spriteInfo.transformToOutline;
            float? gaussianBlurRadius = spriteInfo.gaussianBlurRadius;

            Sprite sprite = BuildSpriteWithTexture(textureEntry.Key, pivot, pixelsPerUnit, transformToOutline, gaussianBlurRadius);
            Registry.textures[spriteName] = new KeyValuePair<Texture2D, bool>(textureEntry.Key, true);
            RegisterSprite(spriteAtlasManager, spriteName, sprite);
        }

        foreach (KeyValuePair<string, KeyValuePair<Texture2D, bool>> texturePair in Registry.textures)
        {
            if (texturePair.Value.Value)
            {
                continue;
            }

            string spriteName = texturePair.Key;
            Sprite sprite = BuildSpriteWithTexture(texturePair.Value.Key, GetPivotPreset(spriteName), defaultPixelsPerUnit);
            RegisterSprite(spriteAtlasManager, spriteName, sprite);
        }
    }

    /// <summary>
    /// Changes a sprite into PNG bytes and a sprite info.
    /// The method draws the sprite into a new texture with <see cref="BakeSpriteToTexture"/> and encodes that texture as PNG.
    /// </summary>
    /// <param name="sprite">The sprite to change.</param>
    /// <returns>The PNG bytes, and a sprite info with the pixels per unit and the pivot of the sprite.</returns>
    internal static (byte[], SpriteInfo) SpriteToPng(Sprite sprite)
    {
        Texture2D bakedTexture = BakeSpriteToTexture(sprite);

        byte[] pngBytes = ImageConversion.EncodeToPNG(bakedTexture);
        UnityEngine.Object.Destroy(bakedTexture);

        Vector2 normalizedPivot = GetNormalizedPivot(sprite);

        return (pngBytes, new SpriteInfo(sprite.pixelsPerUnit, normalizedPivot));
    }

    /// <summary>
    /// Gets the pivot of a sprite as a fraction of the sprite size.
    /// </summary>
    /// <param name="sourceSprite">The sprite.</param>
    /// <returns>The pivot, with values from 0 to 1 for a pivot in the sprite rectangle.</returns>
    private static Vector2 GetNormalizedPivot(Sprite sourceSprite)
    {
        return new Vector2(
            sourceSprite.pivot.x / sourceSprite.rect.width,
            sourceSprite.pivot.y / sourceSprite.rect.height
        );
    }

    /// <summary>
    /// Draws the mesh of a sprite into a new texture that has the size of the sprite rectangle.
    /// The method uses the <c>Sprites/Default</c> shader and a temporary render texture.
    /// It changes the pixels to straight alpha and copies color into the transparent edge pixels.
    /// It sets the active render texture back to the previous value.
    /// </summary>
    /// <param name="sourceSprite">The sprite to draw. It can be a part of an atlas.</param>
    /// <returns>A new RGBA32 texture without mipmaps, with clamp wrap mode.</returns>
    private static Texture2D BakeSpriteToTexture(Sprite sourceSprite)
    {
        Texture2D atlasTexture = sourceSprite.texture;
        int spriteWidth = (int)sourceSprite.rect.width;
        int spriteHeight = (int)sourceSprite.rect.height;
        float sourcePixelsPerUnit = sourceSprite.pixelsPerUnit;
        Vector2 sourcePivotInPixels = sourceSprite.pivot;

        var meshVertices = sourceSprite.vertices;
        var meshUvs = sourceSprite.uv;
        var meshTriangles = sourceSprite.triangles;

        RenderTexture renderTexture = RenderTexture.GetTemporary(
            spriteWidth, spriteHeight, 0,
            RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        RenderTexture previousActiveRenderTexture = RenderTexture.active;
        RenderTexture.active = renderTexture;
        GL.Clear(true, true, Color.clear);

        Material spriteMaterial = new Material(Shader.Find("Sprites/Default"));
        spriteMaterial.mainTexture = atlasTexture;
        spriteMaterial.SetPass(0);

        GL.PushMatrix();
        GL.LoadPixelMatrix(0, spriteWidth, 0, spriteHeight);
        GL.Begin(GL.TRIANGLES);
        for (int triangleCornerIndex = 0; triangleCornerIndex < meshTriangles.Length; triangleCornerIndex++)
        {
            int vertexIndex = meshTriangles[triangleCornerIndex];
            Vector2 uv = meshUvs[vertexIndex];
            Vector2 vertex = meshVertices[vertexIndex];

            GL.TexCoord(new Vector3(uv.x, uv.y, 0));
            GL.Vertex3(
                vertex.x * sourcePixelsPerUnit + sourcePivotInPixels.x,
                vertex.y * sourcePixelsPerUnit + sourcePivotInPixels.y,
                0);
        }
        GL.End();
        GL.PopMatrix();

        Texture2D bakedTexture = new Texture2D(spriteWidth, spriteHeight, TextureFormat.RGBA32, false);
        bakedTexture.ReadPixels(new Rect(0, 0, spriteWidth, spriteHeight), 0, 0);

        Color32[] bakedPixels = bakedTexture.GetPixels32();
        Unpremultiply(bakedPixels);
        BleedAlphaEdges(bakedPixels, spriteWidth, spriteHeight);
        bakedTexture.SetPixels32(bakedPixels);

        bakedTexture.Apply();
        bakedTexture.wrapMode = TextureWrapMode.Clamp;

        RenderTexture.active = previousActiveRenderTexture;
        RenderTexture.ReleaseTemporary(renderTexture);
        UnityEngine.Object.Destroy(spriteMaterial);

        return bakedTexture;
    }

    /// <summary>
    /// Makes and registers the sprites in <see cref="pendingInnerOriginSpriteInfos"/>, and then clears that list.
    /// For each entry, the method finds the game sprite with the origin name.
    /// If the game sprite does not exist, it writes a warning to the log and skips the entry.
    /// If the entry has a pivot or pixels per unit, the method makes a new sprite with these settings.
    /// Only a new sprite gets the outline and blur settings.
    /// Without a pivot and pixels per unit, the method registers the game sprite with no changes.
    /// </summary>
    public static void CreateInnerOriginSprites()
    {
        Plugin.logger.LogInfo($"Creating sprites with inner origin...");
        SpriteAtlasManager spriteAtlasManager = GameManager.GetSpriteAtlasManager();

        foreach (KeyValuePair<string, SpriteInfo> spriteInfoPair in pendingInnerOriginSpriteInfos)
        {
            string spriteName = spriteInfoPair.Key;
            SpriteInfo spriteInfo = spriteInfoPair.Value;

            Sprite originalSprite = spriteAtlasManager
                .DoSpriteLookup(spriteInfo.spriteOrigin!.name, TribeType.None, SkinType.Default)
                .sprite;

            if (originalSprite == null)
            {
                Plugin.logger.LogWarning($"Inner origin sprite '{spriteInfo.spriteOrigin.name}' not found for '{spriteName}'");
                continue;
            }

            bool hasPivotOverride = spriteInfo.pivot != null;
            bool hasPixelsPerUnitOverride = spriteInfo.pixelsPerUnit != null;

            Sprite sprite = originalSprite;

            if (hasPivotOverride || hasPixelsPerUnitOverride)
            {
                Vector2 normalizedOriginalPivot = new(
                    originalSprite.pivot.x / originalSprite.rect.width,
                    originalSprite.pivot.y / originalSprite.rect.height
                );

                Vector2 finalPivot = hasPivotOverride ? spriteInfo.pivot!.Value : normalizedOriginalPivot;
                float finalPixelsPerUnit = hasPixelsPerUnitOverride ? spriteInfo.pixelsPerUnit!.Value : originalSprite.pixelsPerUnit;
                bool transformToOutline = spriteInfo.transformToOutline;
                float? gaussianBlurRadius = spriteInfo.gaussianBlurRadius;

                Texture2D bakedTexture = BakeSpriteToTexture(originalSprite);
                sprite = BuildSpriteWithTexture(bakedTexture, finalPivot, finalPixelsPerUnit, transformToOutline, gaussianBlurRadius);
                sprite.name = spriteName;
            }

            RegisterSprite(spriteAtlasManager, spriteName, sprite);
        }

        pendingInnerOriginSpriteInfos.Clear();
    }

    /// <summary>
    /// Applies all Harmony patches in the <see cref="Visual"/> class.
    /// </summary>
    internal static void Init()
    {
        Harmony.CreateAndPatchAll(typeof(Visual));
    }
}
