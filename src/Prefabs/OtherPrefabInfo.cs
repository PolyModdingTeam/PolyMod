
using System.Text.Json.Serialization;
using PolyMod.Json;
using Polytopia.Data;

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