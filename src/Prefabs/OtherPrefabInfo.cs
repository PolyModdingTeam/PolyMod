
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

public class WeaponPrefabInfo
{
    [JsonInclude]
    [JsonConverter(typeof(EnumCacheJson<UnitData.WeaponEnum>))]
    public UnitData.WeaponEnum? weaponEnum = null;

    [JsonInclude]
    public string spriteName = "";

    [JsonInclude]
    [JsonConverter(typeof(EnumNameJson<PathType>))]
	public PathType pathType = PathType.CatmullRom;

    [JsonInclude]
    [JsonConverter(typeof(EnumNameJson<PathMode>))]
	public PathMode pathMode = PathMode.Sidescroller2D;

    [JsonInclude]
	public int pathResolution = 10;

    [JsonInclude]
    [JsonConverter(typeof(EnumNameJson<Ease>))]
	public Ease pathEase = Ease.Linear;

    [JsonInclude]
	public float lookAheadOffset = 0.01f;

    [JsonInclude]
	public float handleAngleOffset = 0.3f;

    [JsonInclude]
    [JsonConverter(typeof(EnumCacheJson<SFXTypes>))]
	public SFXTypes sfxType = SFXTypes.None;

    [JsonInclude]
    public WeaponPuffInfo? weaponPuffInfo = null;

    [JsonInclude]
	public WeaponRangedInfo? weaponRangedInfo = null;
}

public class WeaponPuffInfo
{
    [JsonInclude]
    public bool sway = false;

    public float animationMultiplier = 1f;

    public Action? initialDelegate = null;

    public Action? animationDelegate = null;

    // [JsonInclude] Nuh uh, I wont do ts
    // public List<string> puffsToSpawn = new();
}

public class WeaponRangedInfo
{
    [JsonInclude]
	public float centerOffset = 0.15f;
}

public class GFXPrefabInfo
{
    [JsonInclude]
    [JsonConverter(typeof(EnumCacheJson<GFXType>))]
    public GFXType? gfxType = null;
}

public class UnitEffectPrefabInfo
{
    [JsonInclude]
    [JsonConverter(typeof(EnumCacheJson<UnitEffect>))]
    public UnitEffect? unitEffect = null;
}