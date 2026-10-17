using System.Text.Json.Serialization;
using PolyMod.Json;
using UnityEngine;

namespace PolyMod;

public class VisualPartInfo
{
    [JsonInclude]
    public string? defaultSpriteName;

    [JsonInclude]
    [JsonConverter(typeof(EnumNameJson<SkinVisualsReference.SkinningLogic>))]
    public SkinVisualsReference.SkinningLogic skinningLogic = SkinVisualsReference.SkinningLogic.UseTribe;

    [JsonInclude]
    [JsonConverter(typeof(EnumNameJson<SkinVisualsReference.ImprovementSkinningLogic>))]
    public SkinVisualsReference.ImprovementSkinningLogic improvementSkinningLogic = SkinVisualsReference.ImprovementSkinningLogic.FoundingTribe; // improvement specific

    [JsonInclude]
    public Vector2 coordinates = new Vector2();

    [JsonInclude]
    public float rotation = 0f;

    [JsonInclude]
    public bool tintable = false;

    [JsonInclude]
    public bool levels = false; // improvement specific

    [JsonInclude]
    public int? sortingOrderOverride = null;

    [JsonInclude]
    public int? outlineSortingOrderOverride = null;
}