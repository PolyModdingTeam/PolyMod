
using System.Text.Json.Serialization;

namespace PolyMod;

/// <summary>
/// The content of a <c>prefabs</c> file of a mod.
/// The dumper also writes the prefabs of the game in this format.
/// </summary>
public class PrefabsInfo
{
    /// <summary>
    /// The descriptions of the unit prefabs.
    /// </summary>
    [JsonInclude]
    public List<UnitPrefabInfo> units = new();

    /// <summary>
    /// The descriptions of the improvement prefabs.
    /// </summary>
    [JsonInclude]
    public List<ImprovementPrefabInfo> improvements = new();

    /// <summary>
    /// The descriptions of the resource prefabs.
    /// </summary>
    [JsonInclude]
    public List<ResourcePrefabInfo> resources = new();

    // [JsonInclude]
    private List<WeaponPrefabInfo> weapons = new();
}
