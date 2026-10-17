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

    public SkinVisualsReference.VisualPart ToVisualPart(bool isImprovement)
    {
        return new SkinVisualsReference.VisualPart
		{
			DefaultSpriteName = defaultSpriteName,
			tintable = tintable,
			skinLogic = skinningLogic,
			improvementSkinLogic = improvementSkinningLogic,
			levels = levels,
			isImprovement = isImprovement,
		};
    }

    public void CopyFromVisualPart(SkinVisualsReference.VisualPart visualPart)
    {
        int? sortingOrderOverride = null;
        SpriteRenderer spriteRenderer = visualPart.visualPart.GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            sortingOrderOverride = spriteRenderer.sortingOrder;
        }

        int? outlineSortingOrderOverride = null;

        if (visualPart.outline != null)
        {
            spriteRenderer = visualPart.outline.GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                outlineSortingOrderOverride = spriteRenderer.sortingOrder;
            }
        }

        coordinates = visualPart.visualPart.transform.position;
        rotation = visualPart.visualPart.transform.rotation.z;
        defaultSpriteName = visualPart.DefaultSpriteName;
        tintable = visualPart.tintable;
        skinningLogic = visualPart.skinLogic;
        improvementSkinningLogic = visualPart.improvementSkinLogic;
        levels = visualPart.levels;
        this.sortingOrderOverride = sortingOrderOverride;
        this.outlineSortingOrderOverride = outlineSortingOrderOverride;
    }
}