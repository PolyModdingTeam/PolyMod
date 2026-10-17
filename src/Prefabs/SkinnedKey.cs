using System.Text.Json.Serialization;
using PolyMod.Json;
using Polytopia.Data;
using PolytopiaBackendBase.Common;

namespace PolyMod;

public readonly struct SkinnedKey : IEquatable<SkinnedKey>
{
    [JsonInclude]
    [JsonConverter(typeof(EnumCacheJson<UnitData.Type>))]
    public UnitData.Type unitType { get; }

    [JsonInclude]
    [JsonConverter(typeof(EnumCacheJson<TribeType>))]
    public TribeType tribeType { get; }

    [JsonInclude]
    [JsonConverter(typeof(EnumCacheJson<SkinType>))]
    public SkinType skinType { get; }

    [JsonConstructor]
    public SkinnedKey(UnitData.Type unitType, TribeType tribeType, SkinType skinType)
    {
        this.unitType = unitType;
        this.tribeType = tribeType;
        this.skinType = skinType;
    }

    public bool Equals(SkinnedKey other)
    {
        return unitType == other.unitType
            && tribeType == other.tribeType
            && skinType == other.skinType;
    }

    public override bool Equals(object? other)
    {
        return other is SkinnedKey otherKey && Equals(otherKey);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine((int)unitType, (int)tribeType, (int)skinType);
    }
}