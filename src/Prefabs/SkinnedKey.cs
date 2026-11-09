using System.Text.Json.Serialization;
using PolyMod.Json;
using Polytopia.Data;
using PolytopiaBackendBase.Common;

namespace PolyMod;

/// <summary>
/// The key of a unit prefab: a unit type, a tribe, and a skin.
/// </summary>
public readonly struct SkinnedKey : IEquatable<SkinnedKey>
{
    /// <summary>
    /// Gets the unit type.
    /// </summary>
    [JsonInclude]
    [JsonConverter(typeof(EnumCacheJson<UnitData.Type>))]
    public UnitData.Type unitType { get; }

    /// <summary>
    /// Gets the tribe.
    /// </summary>
    [JsonInclude]
    [JsonConverter(typeof(EnumCacheJson<TribeType>))]
    public TribeType tribeType { get; }

    /// <summary>
    /// Gets the skin.
    /// </summary>
    [JsonInclude]
    [JsonConverter(typeof(EnumCacheJson<SkinType>))]
    public SkinType skinType { get; }

    /// <summary>
    /// Makes a key from a unit type, a tribe, and a skin.
    /// </summary>
    /// <param name="unitType">The unit type.</param>
    /// <param name="tribeType">The tribe.</param>
    /// <param name="skinType">The skin.</param>
    [JsonConstructor]
    public SkinnedKey(UnitData.Type unitType, TribeType tribeType, SkinType skinType)
    {
        this.unitType = unitType;
        this.tribeType = tribeType;
        this.skinType = skinType;
    }

    /// <summary>
    /// Compares this key with another key.
    /// </summary>
    /// <param name="other">The key to compare.</param>
    /// <returns>True when the unit type, the tribe, and the skin are all equal, otherwise false.</returns>
    public bool Equals(SkinnedKey other)
    {
        return unitType == other.unitType
            && tribeType == other.tribeType
            && skinType == other.skinType;
    }

    /// <summary>
    /// Compares this key with an object.
    /// </summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns>True when the object is a <see cref="SkinnedKey"/> that is equal to this key, otherwise false.</returns>
    public override bool Equals(object? obj)
    {
        return obj is SkinnedKey otherKey && Equals(otherKey);
    }

    /// <summary>
    /// Gets the hash code of the key.
    /// </summary>
    /// <returns>A hash code that combines the unit type, the tribe, and the skin.</returns>
    public override int GetHashCode()
    {
        return HashCode.Combine((int)unitType, (int)tribeType, (int)skinType);
    }

    /// <summary>
    /// Compares two keys.
    /// </summary>
    /// <param name="left">The first key.</param>
    /// <param name="right">The second key.</param>
    /// <returns>True when the keys are equal, otherwise false.</returns>
    public static bool operator ==(SkinnedKey left, SkinnedKey right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// Compares two keys.
    /// </summary>
    /// <param name="left">The first key.</param>
    /// <param name="right">The second key.</param>
    /// <returns>True when the keys are not equal, otherwise false.</returns>
    public static bool operator !=(SkinnedKey left, SkinnedKey right)
    {
        return !left.Equals(right);
    }
}
