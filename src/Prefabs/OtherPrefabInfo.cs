
using System.Text.Json.Serialization;
using PolyMod.Json;
using Polytopia.Data;
using DG.Tweening;

namespace PolyMod;

public class BasePrefabInfo
{
    [JsonInclude]
    public List<VisualPartInfo> visualParts = new();
}
public class UnitPrefabInfo : BasePrefabInfo
{
    [JsonInclude]
    public SkinnedKey skinnedKey;

    [JsonInclude]
    public string headPositionMarker = "";

    [JsonInclude]
    public bool uiOverride = false;
}

public class ImprovementPrefabInfo : BasePrefabInfo
{
    [JsonInclude]
    [JsonConverter(typeof(EnumCacheJson<ImprovementData.Type>))]
    public ImprovementData.Type? improvementType = null;

    [JsonInclude]
    public bool needsOutlines = false;
}

public class ResourcePrefabInfo : BasePrefabInfo
{
    [JsonInclude]
    [JsonConverter(typeof(EnumCacheJson<ResourceData.Type>))]
    public ResourceData.Type? resourceType = null;
}

internal class WeaponPrefabInfo
{
    [JsonInclude]
    [JsonConverter(typeof(EnumCacheJson<UnitData.WeaponEnum>))]
    internal UnitData.WeaponEnum? weaponEnum = null;

    [JsonInclude]
    internal string spriteName = "";

    [JsonInclude]
    [JsonConverter(typeof(EnumNameJson<PathType>))]
    internal PathType pathType = PathType.CatmullRom;

    [JsonInclude]
    [JsonConverter(typeof(EnumNameJson<PathMode>))]
    internal PathMode pathMode = PathMode.Sidescroller2D;

    [JsonInclude]
    internal int pathResolution = 10;

    [JsonInclude]
    [JsonConverter(typeof(EnumNameJson<Ease>))]
    internal Ease pathEase = Ease.Linear;

    [JsonInclude]
    internal float lookAheadOffset = 0.01f;

    [JsonInclude]
    internal float handleAngleOffset = 0.3f;

    [JsonInclude]
    [JsonConverter(typeof(EnumCacheJson<SFXTypes>))]
    internal SFXTypes sfxType = SFXTypes.None;

    [JsonInclude]
    internal WeaponPuffInfo? weaponPuffInfo = null;

    [JsonInclude]
    internal WeaponRangedInfo? weaponRangedInfo = null;
}

internal class WeaponPuffInfo
{
    [JsonInclude]
    internal bool sway = false;

    internal float animationMultiplier = 1f;

    internal Action? initialDelegate = null;

    internal Action? animationDelegate = null;

    // [JsonInclude] Nuh uh, I wont do ts
    // public List<string> puffsToSpawn = new();
}

internal class WeaponRangedInfo
{
    [JsonInclude]
    internal float centerOffset = 0.15f;
}

internal class GFXPrefabInfo
{
    [JsonInclude]
    [JsonConverter(typeof(EnumCacheJson<GFXType>))]
    internal GFXType? gfxType = null;
}

internal class UnitEffectPrefabInfo
{
    [JsonInclude]
    [JsonConverter(typeof(EnumCacheJson<UnitEffect>))]
    internal UnitEffect? unitEffect = null;
}
