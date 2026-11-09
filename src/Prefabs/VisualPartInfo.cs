using System.Text.Json.Serialization;
using PolyMod.Json;
using UnityEngine;

namespace PolyMod;

/// <summary>
/// The description of one visual part of a prefab, as mod JSON gives it.
/// </summary>
public class VisualPartInfo
{
    /// <summary>
    /// The default sprite name of the visual part.
    /// PolyMod also uses it as the name of the visual part object.
    /// </summary>
    [JsonInclude]
    public string? defaultSpriteName;

    /// <summary>
    /// The logic that selects the skin of the sprite. The default is <c>UseTribe</c>.
    /// </summary>
    [JsonInclude]
    [JsonConverter(typeof(EnumNameJson<SkinVisualsReference.SkinningLogic>))]
    public SkinVisualsReference.SkinningLogic skinningLogic = SkinVisualsReference.SkinningLogic.UseTribe;

    /// <summary>
    /// The logic that selects the skin of the sprite for an improvement. The default is <c>FoundingTribe</c>.
    /// Only improvements use this value.
    /// </summary>
    [JsonInclude]
    [JsonConverter(typeof(EnumNameJson<SkinVisualsReference.ImprovementSkinningLogic>))]
    public SkinVisualsReference.ImprovementSkinningLogic improvementSkinningLogic = SkinVisualsReference.ImprovementSkinningLogic.FoundingTribe; // improvement specific

    /// <summary>
    /// The world position of the visual part.
    /// </summary>
    [JsonInclude]
    public Vector2 coordinates = new Vector2(0, 0);

    /// <summary>
    /// The rotation of the visual part around the z axis, in degrees.
    /// </summary>
    [JsonInclude]
    public float rotation = 0f;

    /// <summary>
    /// The flag that lets the game tint the sprite with the team color.
    /// </summary>
    [JsonInclude]
    public bool tintable = false;

    /// <summary>
    /// The flag that makes the sprite change with the level of the improvement.
    /// Only improvements use this value.
    /// </summary>
    [JsonInclude]
    public bool levels = false; // improvement specific

    /// <summary>
    /// The sorting order of the visual part renderer, or null to use the calculated sorting order.
    /// Only unit prefabs use this value.
    /// </summary>
    [JsonInclude]
    public int? sortingOrderOverride = null;

    /// <summary>
    /// The sorting order of the outline renderer, or null to use the calculated sorting order.
    /// Only unit prefabs use this value.
    /// </summary>
    [JsonInclude]
    public int? outlineSortingOrderOverride = null;

    /// <summary>
    /// Makes a game visual part from this description.
    /// </summary>
    /// <remarks>
    /// The method copies the sprite name, the tint flag, the skin logic values, and the level flag.
    /// It does not make the objects or the renderers of the visual part.
    /// </remarks>
    /// <param name="isImprovement">True to mark the visual part as an improvement part.</param>
    /// <returns>The new visual part.</returns>
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

    /// <summary>
    /// Sets all values of this description from a game visual part.
    /// </summary>
    /// <remarks>
    /// The sorting orders come from the <c>SpriteRenderer</c> of the visual part object and of the outline object.
    /// If an object has no <c>SpriteRenderer</c>, its sorting order is null.
    /// <see cref="coordinates"/> gets the world position of the visual part object.
    /// <see cref="rotation"/> gets the z component of the rotation quaternion, not an angle in degrees.
    /// </remarks>
    /// <param name="visualPart">The visual part to copy. Its visual part object must not be null.</param>
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
