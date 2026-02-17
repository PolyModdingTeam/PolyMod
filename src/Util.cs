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
public static class Util
{
    internal const string PLACEHOLDER = "placeholder";
    internal const string HIDDEN = "hidden";
    internal static Dictionary<string, string> cachedReversedSpriteDataNames = new();
    /// <summary>
    /// Wraps a managed type for use in the Il2Cpp runtime.
    /// </summary>
    /// <typeparam name="T">The type to wrap.</typeparam>
    /// <returns>The Il2Cpp representation of the type.</returns>
    public static Il2CppSystem.Type WrapType<T>() where T : class
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
    public static string Hash(object data)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(data.ToString()!)));
    }

    /// <summary>
    /// Gets the name of a JToken from its path.
    /// </summary>
    /// <param name="token">The JToken.</param>
    /// <param name="n">The part of the path to retrieve, from the end.</param>
    /// <returns>The name of the JToken.</returns>
    public static string GetJTokenName(JToken token, int n = 1)
    {
        return token.Path.Split('.')[^n];
    }

    /// <summary>
    /// Converts an Il2CppSystem.Version to a System.Version.
    /// </summary>
    /// <param name="self">The Il2CppSystem.Version to convert.</param>
    /// <returns>The equivalent System.Version.</returns>
    public static Version Cast(this Il2CppSystem.Version self)
    {
        return new(self.ToString());
    }

    /// <summary>
    /// Synchronously unwraps the result of a Task.
    /// </summary>
    /// <typeparam name="T">The result type of the Task.</typeparam>
    /// <param name="self">The Task to unwrap.</param>
    /// <returns>The result of the Task.</returns>
    public static T UnwrapAsync<T>(this Task<T> self)
    {
        return self.GetAwaiter().GetResult();
    }

    /// <summary>
    /// Removes the revision component from a Version.
    /// </summary>
    /// <param name="self">The Version to modify.</param>
    /// <returns>A new Version with the revision set to -1.</returns>
    public static Version CutRevision(this Version self)
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
    public static bool IsVersionOlderOrEqual(this string version1, string version2)
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
    public static string GetStyle(TribeType tribe, SkinType skin)
    {
        return skin != SkinType.Default ? EnumCache<SkinType>.GetName(skin) : EnumCache<TribeType>.GetName(tribe);
    }

    internal static void CacheReversedSpriteDataNames()
    {
        List<TerrainData.Type> terrains = Enum.GetValues(typeof(TerrainData.Type)).Cast<TerrainData.Type>().ToList();
        foreach (var terrain in terrains)
        {
            string terrainToString = SpriteData.TerrainToString(terrain);

            if(terrainToString == HIDDEN)
                continue;

            string terrainName = EnumCache<TerrainData.Type>.GetName(terrain);
            if(terrain == TerrainData.Type.Wetland)
                terrainName = EnumCache<TerrainData.Type>.GetName(TerrainData.Type.Field) + "_flooded";

            cachedReversedSpriteDataNames[terrainToString] = terrainName;
        }
        List<ResourceData.Type> resources = Enum.GetValues(typeof(ResourceData.Type)).Cast<ResourceData.Type>().ToList();
        foreach (var resource in resources)
        {
            string resourceToString = SpriteData.ResourceToString(resource);

            if(resourceToString == PLACEHOLDER)
                continue;

            cachedReversedSpriteDataNames[resourceToString] = EnumCache<ResourceData.Type>.GetName(resource);
        }
        List<ImprovementData.Type> improvements = Enum.GetValues(typeof(ImprovementData.Type)).Cast<ImprovementData.Type>().ToList();
        foreach (var improvement in improvements)
        {
            string improvementToString = SpriteData.ImprovementToString(improvement);

            if(improvementToString == PLACEHOLDER)
                continue;

            cachedReversedSpriteDataNames[improvementToString] = EnumCache<ImprovementData.Type>.GetName(improvement);
        }
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
        foreach (var key in cachedReversedSpriteDataNames.Keys)
        {
            if(baseName.Contains(SpriteData.TILE_WETLAND) && key == SpriteData.TILE_WETLAND)
            {
                Console.Write(baseName);
                baseName = baseName.Replace(key, cachedReversedSpriteDataNames[key]);
                Console.Write(baseName);
            }
            else
            {
                baseName = baseName.Replace(key, cachedReversedSpriteDataNames[key]);
            }

        }

        baseName = baseName.Replace("UI_", "");
        return baseName;
    }
}
