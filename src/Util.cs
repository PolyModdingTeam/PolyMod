using System.Security.Cryptography;
using System.Text;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Injection;
using Newtonsoft.Json.Linq;
using Polytopia.Data;
using PolytopiaBackendBase.Common;

namespace PolyMod;

/// <summary>
/// A collection of utility methods for the PolyMod framework.
/// </summary>
internal static class Util
{
    /// <summary>
    /// Wraps a managed type for use in the Il2Cpp runtime.
    /// </summary>
    /// <typeparam name="T">The type to wrap.</typeparam>
    /// <returns>The Il2Cpp representation of the type.</returns>
    internal static Il2CppSystem.Type WrapType<T>() where T : class
    {
        if (!ClassInjector.IsTypeRegisteredInIl2Cpp<T>())
            ClassInjector.RegisterTypeInIl2Cpp<T>();
        return Il2CppType.From(typeof(T));
    }

    /// <summary>
    /// Computes the SHA256 hash of an object's string representation.
    /// </summary>
    /// <param name="data">The object to hash.</param>
    /// <returns>The hexadecimal string representation of the hash.</returns>
    internal static string Hash(object data)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(data.ToString()!)));
    }

    /// <summary>
    /// Gets the name of a JToken from its path.
    /// </summary>
    /// <param name="token">The JToken.</param>
    /// <param name="n">The part of the path to retrieve, from the end.</param>
    /// <returns>The name of the JToken.</returns>
    internal static string GetJTokenName(JToken token, int n = 1)
    {
        return token.Path.Split('.')[^n];
    }

    /// <summary>
    /// Converts an Il2CppSystem.Version to a System.Version.
    /// </summary>
    /// <param name="self">The Il2CppSystem.Version to convert.</param>
    /// <returns>The equivalent System.Version.</returns>
    internal static Version Cast(this Il2CppSystem.Version self)
    {
        return new(self.ToString());
    }

    /// <summary>
    /// Synchronously unwraps the result of a Task.
    /// </summary>
    /// <typeparam name="T">The result type of the Task.</typeparam>
    /// <param name="self">The Task to unwrap.</param>
    /// <returns>The result of the Task.</returns>
    internal static T UnwrapAsync<T>(this Task<T> self)
    {
        return self.GetAwaiter().GetResult();
    }

    /// <summary>
    /// Removes the revision component from a Version.
    /// </summary>
    /// <param name="self">The Version to modify.</param>
    /// <returns>A new Version with the revision set to -1.</returns>
    internal static Version CutRevision(this Version self)
    {
        return new(self.Major, self.Minor, self.Build);
    }

    /// <summary>
    /// Compares two version strings to see if the first is older or equal to the second.
    /// Handles pre-release identifiers.
    /// </summary>
    /// <param name="version1">The first version string.</param>
    /// <param name="version2">The second version string.</param>
    /// <returns>True if version1 is older or equal to version2, false otherwise.</returns>
    internal static bool IsVersionOlderOrEqual(this string version1, string version2)
    {
        Version version1_ = new(version1.Split('-')[0]);
        Version version2_ = new(version2.Split('-')[0]);

        if (version1_ < version2_) return true;
        if (version1_ > version2_) return false;

        string pre1 = version1.Contains('-') ? version1.Split('-')[1] : "";
        string pre2 = version2.Contains('-') ? version2.Split('-')[1] : "";

        if (string.IsNullOrEmpty(pre1) && !string.IsNullOrEmpty(pre2)) return false;
        if (!string.IsNullOrEmpty(pre1) && string.IsNullOrEmpty(pre2)) return true;

        return string.Compare(pre1, pre2, StringComparison.Ordinal) <= 0;
    }

    /// <summary>
    /// Gets the style name for a tribe and skin combination.
    /// </summary>
    /// <param name="tribe">The tribe.</param>
    /// <param name="skin">The skin.</param>
    /// <returns>The style name.</returns>
    internal static string GetStyle(TribeType tribe, SkinType skin)
    {
        return skin != SkinType.Default ? EnumCache<SkinType>.GetName(skin) : EnumCache<TribeType>.GetName(tribe);
    }

    /// <summary>
    /// Formats a sprite name to match the enum naming convention.
    /// This is a workaround for inconsistencies in the game's sprite naming.
    /// </summary>
    /// <param name="baseName">The original sprite name.</param>
    /// <returns>The formatted sprite name.</returns>
    internal static string FormatSpriteName(string baseName) // I cant believe i had to do this shit #MIDJIWANFIXYOURSHITCODE
    {
        // This method is necessary because the sprite names in the game's data do not always
        // match the names in the corresponding enums. This method replaces the hardcoded sprite
        // names with the enum names to ensure consistency.
        baseName = baseName.Replace(SpriteData.IMPROVEMENT_AQUA_FARM, EnumCache<ImprovementData.Type>.GetName(ImprovementData.Type.Aquafarm));
        baseName = baseName.Replace(SpriteData.IMPROVEMENT_ATOLL, EnumCache<ImprovementData.Type>.GetName(ImprovementData.Type.Atoll));
        baseName = baseName.Replace(SpriteData.IMPROVEMENT_BURN_FOREST, EnumCache<ImprovementData.Type>.GetName(ImprovementData.Type.BurnForest));
        baseName = baseName.Replace(SpriteData.IMPROVEMENT_CLEAR_FOREST, EnumCache<ImprovementData.Type>.GetName(ImprovementData.Type.ClearForest));
        baseName = baseName.Replace(SpriteData.IMPROVEMENT_CUSTOMS_HOUSE, EnumCache<ImprovementData.Type>.GetName(ImprovementData.Type.CustomsHouse));
        baseName = baseName.Replace(SpriteData.IMPROVEMENT_FARM, EnumCache<ImprovementData.Type>.GetName(ImprovementData.Type.Farm));
        baseName = baseName.Replace(SpriteData.IMPROVEMENT_FOREST_TEMPLE, EnumCache<ImprovementData.Type>.GetName(ImprovementData.Type.ForestTemple));
        baseName = baseName.Replace(SpriteData.IMPROVEMENT_FORGE, EnumCache<ImprovementData.Type>.GetName(ImprovementData.Type.Forge));
        baseName = baseName.Replace(SpriteData.IMPROVEMENT_GROW_FOREST, EnumCache<ImprovementData.Type>.GetName(ImprovementData.Type.GrowForest));
        baseName = baseName.Replace(SpriteData.IMPROVEMENT_ICE_BANK, EnumCache<ImprovementData.Type>.GetName(ImprovementData.Type.IceBank));
        baseName = baseName.Replace(SpriteData.IMPROVEMENT_ICE_PORT, EnumCache<ImprovementData.Type>.GetName(ImprovementData.Type.Outpost));
        baseName = baseName.Replace(SpriteData.IMPROVEMENT_ICE_TEMPLE, EnumCache<ImprovementData.Type>.GetName(ImprovementData.Type.IceTemple));
        baseName = baseName.Replace(SpriteData.IMPROVEMENT_LUMBER_HUT, EnumCache<ImprovementData.Type>.GetName(ImprovementData.Type.LumberHut));
        baseName = baseName.Replace(SpriteData.IMPROVEMENT_MARKET, EnumCache<ImprovementData.Type>.GetName(ImprovementData.Type.Market));
        baseName = baseName.Replace(SpriteData.IMPROVEMENT_MINE, EnumCache<ImprovementData.Type>.GetName(ImprovementData.Type.Mine));
        baseName = baseName.Replace(SpriteData.IMPROVEMENT_MOUNTAIN_TEMPLE, EnumCache<ImprovementData.Type>.GetName(ImprovementData.Type.MountainTemple));
        baseName = baseName.Replace(SpriteData.IMPROVEMENT_PORT, EnumCache<ImprovementData.Type>.GetName(ImprovementData.Type.Port));
        baseName = baseName.Replace(SpriteData.IMPROVEMENT_ROAD, EnumCache<ImprovementData.Type>.GetName(ImprovementData.Type.Road));
        baseName = baseName.Replace(SpriteData.IMPROVEMENT_RUIN, EnumCache<ImprovementData.Type>.GetName(ImprovementData.Type.Ruin));
        baseName = baseName.Replace(SpriteData.IMPROVEMENT_SANCTUARY, EnumCache<ImprovementData.Type>.GetName(ImprovementData.Type.Sanctuary));
        baseName = baseName.Replace(SpriteData.IMPROVEMENT_SAWMILL, EnumCache<ImprovementData.Type>.GetName(ImprovementData.Type.Sawmill));
        baseName = baseName.Replace(SpriteData.IMPROVEMENT_TEMPLE, EnumCache<ImprovementData.Type>.GetName(ImprovementData.Type.Temple));
        baseName = baseName.Replace(SpriteData.IMPROVEMENT_WATER_TEMPLE, EnumCache<ImprovementData.Type>.GetName(ImprovementData.Type.WaterTemple));
        baseName = baseName.Replace(SpriteData.IMPROVEMENT_WINDMILL, EnumCache<ImprovementData.Type>.GetName(ImprovementData.Type.Windmill));

        baseName = baseName.Replace(SpriteData.RESOURCE_AQUACROP, EnumCache<ResourceData.Type>.GetName(ResourceData.Type.AquaCrop));
        baseName = baseName.Replace(SpriteData.RESOURCE_CROP, EnumCache<ResourceData.Type>.GetName(ResourceData.Type.Crop));
        baseName = baseName.Replace(SpriteData.RESOURCE_FISH, EnumCache<ResourceData.Type>.GetName(ResourceData.Type.Fish));
        baseName = baseName.Replace(SpriteData.RESOURCE_FRUIT, EnumCache<ResourceData.Type>.GetName(ResourceData.Type.Fruit));
        baseName = baseName.Replace(SpriteData.RESOURCE_GAME, EnumCache<ResourceData.Type>.GetName(ResourceData.Type.Game));
        baseName = baseName.Replace(SpriteData.RESOURCE_METAL, EnumCache<ResourceData.Type>.GetName(ResourceData.Type.Metal));
        baseName = baseName.Replace(SpriteData.RESOURCE_SPORES, EnumCache<ResourceData.Type>.GetName(ResourceData.Type.Spores));
        baseName = baseName.Replace(SpriteData.RESOURCE_STARFISH, EnumCache<ResourceData.Type>.GetName(ResourceData.Type.Starfish));
        baseName = baseName.Replace(SpriteData.RESOURCE_WHALE, EnumCache<ResourceData.Type>.GetName(ResourceData.Type.Whale));

        baseName = baseName.Replace(SpriteData.TILE_FIELD, EnumCache<TerrainData.Type>.GetName(TerrainData.Type.Field));
        baseName = baseName.Replace(SpriteData.TILE_FOREST, EnumCache<TerrainData.Type>.GetName(TerrainData.Type.Forest));
        baseName = baseName.Replace(SpriteData.TILE_ICE, EnumCache<TerrainData.Type>.GetName(TerrainData.Type.Ice));
        baseName = baseName.Replace(SpriteData.TILE_MOUNTAIN, EnumCache<TerrainData.Type>.GetName(TerrainData.Type.Mountain));
        baseName = baseName.Replace(SpriteData.TILE_OCEAN, EnumCache<TerrainData.Type>.GetName(TerrainData.Type.Ocean));
        baseName = baseName.Replace(SpriteData.TILE_UNKNOWN, EnumCache<TerrainData.Type>.GetName(TerrainData.Type.Field));
        baseName = baseName.Replace(SpriteData.TILE_WATER, EnumCache<TerrainData.Type>.GetName(TerrainData.Type.Water));
        baseName = baseName.Replace(SpriteData.TILE_WETLAND, EnumCache<TerrainData.Type>.GetName(TerrainData.Type.Field) + "_flooded");

        baseName = baseName.Replace("UI_", "");
        return baseName;
    }
}
