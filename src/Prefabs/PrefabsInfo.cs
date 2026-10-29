
using System.Text.Json.Serialization;

namespace PolyMod;

public class PrefabsInfo
{
    [JsonInclude]
    public List<UnitPrefabInfo> units = new();

    [JsonInclude]
    public List<ImprovementPrefabInfo> improvements = new();

    [JsonInclude]
    public List<ResourcePrefabInfo> resources = new();

    [JsonInclude]
    public List<WeaponPrefabInfo> weapons = new();
}