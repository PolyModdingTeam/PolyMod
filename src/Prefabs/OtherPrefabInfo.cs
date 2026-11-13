
using System.Text.Json.Serialization;
using PolyMod.Json;
using Polytopia.Data;
using DG.Tweening;

namespace PolyMod;

/// <summary>
/// The base description of a prefab that PolyMod makes from mod JSON.
/// </summary>
public class BasePrefabInfo
{
    /// <summary>
    /// The descriptions of the visual parts of the prefab.
    /// </summary>
    [JsonInclude]
    public List<VisualPartInfo> visualParts = new();
}
/// <summary>
/// The description of a custom unit prefab.
/// It comes from the <c>units</c> list of a <c>prefabs</c> file or from a <c>unitPrefab</c> file.
/// </summary>
public class UnitPrefabInfo : BasePrefabInfo
{
    /// <summary>
    /// The unit type, the tribe, and the skin that the prefab is for.
    /// </summary>
    [JsonInclude]
    public SkinnedKey skinnedKey;

    /// <summary>
    /// The name of the visual part that is the head position marker of the unit.
    /// If no visual part has this name, PolyMod uses the first visual part that it makes.
    /// </summary>
    [JsonInclude]
    public string headPositionMarker = "";

    /// <summary>
    /// The flag that makes the prefab a UI override.
    /// When it is true, the UI shows this prefab in place of the usual unit prefab.
    /// </summary>
    [JsonInclude]
    public bool uiOverride = false;
}

/// <summary>
/// The description of a custom improvement prefab.
/// It comes from the <c>improvements</c> list of a <c>prefabs</c> file or from an <c>improvementPrefab</c> file.
/// PolyMod also makes a default description for each GLD improvement that has the <c>prefab</c> value set to true.
/// </summary>
public class ImprovementPrefabInfo : BasePrefabInfo
{
    /// <summary>
    /// The improvement type that the prefab is for.
    /// If it is null, PolyMod does not make the prefab.
    /// </summary>
    [JsonInclude]
    [JsonConverter(typeof(EnumCacheJson<ImprovementData.Type>))]
    public ImprovementData.Type? improvementType = null;

    /// <summary>
    /// The flag that adds a renderer to the outline of each visual part.
    /// When it is false, the outline objects have no renderer.
    /// </summary>
    [JsonInclude]
    public bool needsOutlines = false;
}

/// <summary>
/// The description of a custom resource prefab.
/// It comes from the <c>resources</c> list of a <c>prefabs</c> file or from a <c>resourcePrefab</c> file.
/// PolyMod also makes a default description for each GLD resource that has the <c>prefab</c> value set to true.
/// </summary>
public class ResourcePrefabInfo : BasePrefabInfo
{
    /// <summary>
    /// The resource type that the prefab is for.
    /// If it is null, PolyMod does not make the prefab.
    /// </summary>
    [JsonInclude]
    [JsonConverter(typeof(EnumCacheJson<ResourceData.Type>))]
    public ResourceData.Type? resourceType = null;
}

/// <summary>
/// The description of a custom weapon prefab.
/// PolyMod does not read or use this type now.
/// </summary>
internal class WeaponPrefabInfo
{
    /// <summary>
    /// The weapon type that the prefab is for.
    /// </summary>
    [JsonInclude]
    [JsonConverter(typeof(EnumCacheJson<UnitData.WeaponEnum>))]
    internal UnitData.WeaponEnum? weaponEnum = null;

    /// <summary>
    /// The name of the sprite of the weapon.
    /// </summary>
    [JsonInclude]
    internal string spriteName = "";

    /// <summary>
    /// The DOTween path type of the weapon movement.
    /// </summary>
    [JsonInclude]
    [JsonConverter(typeof(EnumNameJson<PathType>))]
    internal PathType pathType = PathType.CatmullRom;

    /// <summary>
    /// The DOTween path mode of the weapon movement.
    /// </summary>
    [JsonInclude]
    [JsonConverter(typeof(EnumNameJson<PathMode>))]
    internal PathMode pathMode = PathMode.Sidescroller2D;

    /// <summary>
    /// The DOTween path resolution of the weapon movement.
    /// </summary>
    [JsonInclude]
    internal int pathResolution = 10;

    /// <summary>
    /// The DOTween ease of the weapon movement.
    /// </summary>
    [JsonInclude]
    [JsonConverter(typeof(EnumNameJson<Ease>))]
    internal Ease pathEase = Ease.Linear;

    /// <summary>
    /// The look-ahead offset of the weapon movement.
    /// </summary>
    [JsonInclude]
    internal float lookAheadOffset = 0.01f;

    /// <summary>
    /// The handle angle offset of the weapon path.
    /// </summary>
    [JsonInclude]
    internal float handleAngleOffset = 0.3f;

    /// <summary>
    /// The sound effect of the weapon.
    /// </summary>
    [JsonInclude]
    [JsonConverter(typeof(EnumCacheJson<SFXTypes>))]
    internal SFXTypes sfxType = SFXTypes.None;

    /// <summary>
    /// The puff settings of the weapon, or null.
    /// </summary>
    [JsonInclude]
    internal WeaponPuffInfo? weaponPuffInfo = null;

    /// <summary>
    /// The ranged attack settings of the weapon, or null.
    /// </summary>
    [JsonInclude]
    internal WeaponRangedInfo? weaponRangedInfo = null;
}

/// <summary>
/// The puff settings of a custom weapon.
/// PolyMod does not use this type now.
/// </summary>
internal class WeaponPuffInfo
{
    /// <summary>
    /// The flag that makes the attacking unit sway.
    /// </summary>
    [JsonInclude]
    internal bool sway = false;

    /// <summary>
    /// The value that multiplies the animation time of the attack.
    /// </summary>
    internal float animationMultiplier = 1f;

    /// <summary>
    /// The action to call at the start of the attack, or null.
    /// </summary>
    internal Action? initialDelegate = null;

    /// <summary>
    /// The action to call during the ranged animation, or null.
    /// </summary>
    internal Action? animationDelegate = null;

    // [JsonInclude] Nuh uh, I wont do ts
    // public List<string> puffsToSpawn = new();
}

/// <summary>
/// The ranged attack settings of a custom weapon.
/// PolyMod does not use this type now.
/// </summary>
internal class WeaponRangedInfo
{
    /// <summary>
    /// The center offset of the ranged attack.
    /// </summary>
    [JsonInclude]
    internal float centerOffset = 0.15f;
}

/// <summary>
/// The description of a custom GFX prefab.
/// PolyMod does not use this type now.
/// </summary>
internal class GFXPrefabInfo
{
    /// <summary>
    /// The GFX type that the prefab is for.
    /// </summary>
    [JsonInclude]
    [JsonConverter(typeof(EnumCacheJson<GFXType>))]
    internal GFXType? gfxType = null;
}

/// <summary>
/// The description of a custom unit effect prefab.
/// PolyMod does not use this type now.
/// </summary>
internal class UnitEffectPrefabInfo
{
    /// <summary>
    /// The unit effect that the prefab is for.
    /// </summary>
    [JsonInclude]
    [JsonConverter(typeof(EnumCacheJson<UnitEffect>))]
    internal UnitEffect? unitEffect = null;
}
