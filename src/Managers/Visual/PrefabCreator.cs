using System.Text.Json;
using System.Text.Json.Serialization;
using PolyMod.Json;
using Polytopia.Data;
using PolytopiaBackendBase.Common;
using UnityEngine;
using static PolyMod.Managers.PrefabRegistry;
using static SkinVisualsReference;

namespace PolyMod.Managers;

public static class PrefabCreator
{
	internal static Material? cachedUnitMaterial;
	internal static Dictionary<string, List<byte[]>> prefabsFilesToProcess = new();

	public static List<UnitPrefabInfo> unitInfos = new();

	public static List<ImprovementPrefabInfo> improvementInfos = new();

	public static List<ResourcePrefabInfo> resourceInfos = new();

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
    }

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
	}

    public class ResourcePrefabInfo : BasePrefabInfo
	{
		[JsonInclude]
		[JsonConverter(typeof(EnumCacheJson<ResourceData.Type>))]
        public ResourceData.Type? resourceType = null;
	}

    public class PrefabsInfo
	{
		[JsonInclude]
		public List<UnitPrefabInfo> units = new();

		[JsonInclude]
		public List<ImprovementPrefabInfo> improvements = new();

		[JsonInclude]
		public List<ResourcePrefabInfo> resources = new();
	}

	internal static void ProcessPrefabs()
	{
		foreach (var keyValuePair in prefabsFilesToProcess)
		{
			if(keyValuePair.Key == "all")
			{
				foreach (var bytes in keyValuePair.Value)
				{
					var prefab = JsonSerializer.Deserialize<PrefabCreator.PrefabsInfo>(bytes, new JsonSerializerOptions
					{
						Converters = { new Vector2Json() },
						PropertyNameCaseInsensitive = true,
					});
					if (prefab == null)
						return;

					unitInfos.AddRange(prefab.units);
					improvementInfos.AddRange(prefab.improvements);
					resourceInfos.AddRange(prefab.resources);
				}
			}
			else if(keyValuePair.Key == "units")
			{
				foreach (var bytes in keyValuePair.Value)
				{
					var unitPrefab = JsonSerializer.Deserialize<PrefabCreator.UnitPrefabInfo>(bytes, new JsonSerializerOptions
					{
						Converters = { new Vector2Json() },
						PropertyNameCaseInsensitive = true,
					});
					if (unitPrefab == null)
						return;

					unitInfos.Add(unitPrefab);
				}
			}
			else if(keyValuePair.Key == "improvements")
			{
				foreach (var bytes in keyValuePair.Value)
				{
					var improvementPrefab = JsonSerializer.Deserialize<PrefabCreator.ImprovementPrefabInfo>(bytes, new JsonSerializerOptions
					{
						Converters = { new Vector2Json() },
						PropertyNameCaseInsensitive = true,
					});
					if (improvementPrefab == null)
						return;

					improvementInfos.Add(improvementPrefab);
				}
			}
			else if(keyValuePair.Key == "resources")
			{
				foreach (var bytes in keyValuePair.Value)
				{
					var resourcePrefab = JsonSerializer.Deserialize<PrefabCreator.ResourcePrefabInfo>(bytes, new JsonSerializerOptions
					{
						Converters = { new Vector2Json() },
						PropertyNameCaseInsensitive = true,
					});
					if (resourcePrefab == null)
						return;

					PrefabCreator.resourceInfos.Add(resourcePrefab);
				}
			}
		}
		foreach (var unitPrefabInfo in unitInfos)
		{
			ProcessUnitPrefabInfo(unitPrefabInfo);
		}

		foreach (var improvementPrefabInfo in improvementInfos)
		{
			ProcessImprovementPrefabInfo(improvementPrefabInfo);
		}

		foreach (var resourcePrefabInfo in resourceInfos)
		{
			ProcessResourcePrefabInfo(resourcePrefabInfo);
		}

		foreach (var item in unitsNew)
		{
			var skinnedKey = item.Key;
			var unit = item.Value;
			SkinVisualsReference skinVisualsReference = unit.GetComponent<SkinVisualsReference>();
			if (skinVisualsReference == null)
			{
				skinVisualsReference = unit.GetComponentInChildren<SkinVisualsReference>(true);
			}
			if (skinVisualsReference == null || skinVisualsReference.visualParts == null)
			{
				Plugin.logger.LogWarning($"{skinnedKey.unitType} {skinnedKey.tribeType} {skinnedKey.skinType}: no SkinVisualsReference");
				continue;
			}

			for (int partIndex = 0; partIndex < skinVisualsReference.visualParts.Length; partIndex++)
			{
				VisualPart currentPart = skinVisualsReference.visualParts[partIndex];
				Plugin.logger.LogInfo($"{skinnedKey.unitType} {skinnedKey.tribeType} {skinnedKey.skinType} part {partIndex}");
				Plugin.logger.LogInfo($"    visualPart: {DescribeVisualObject(currentPart.visualPart)}");
				Plugin.logger.LogInfo($"    outline:    {DescribeVisualObject(currentPart.outline)}");
			}
		}
	}

    private static string DescribeVisualObject(GameObject targetObject)
    {
        if (targetObject == null)
        {
            return "null";
        }

        string layerName = LayerMask.LayerToName(targetObject.layer);
        string description = $"name={targetObject.name}, layer={targetObject.layer} ({layerName}), activeSelf={targetObject.activeSelf}";

        SpriteRenderer spriteRenderer = targetObject.GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            description += $", SpriteRenderer(enabled={spriteRenderer.enabled}, sortingLayer={SortingLayer.IDToName(spriteRenderer.sortingLayerID)}, sortingOrder={spriteRenderer.sortingOrder}, hasSprite={spriteRenderer.sprite != null})";
        }

        PolytopiaSpriteRenderer polytopiaSpriteRenderer = targetObject.GetComponent<PolytopiaSpriteRenderer>();
        if (polytopiaSpriteRenderer != null)
        {
            description += $", PolytopiaSpriteRenderer(enabled={polytopiaSpriteRenderer.enabled}, sortingLayer={SortingLayer.IDToName(polytopiaSpriteRenderer.SortingLayer)}, sortingOrder={polytopiaSpriteRenderer.SortingOrder}, hasSprite={polytopiaSpriteRenderer.HasSprite()})";
        }

        return description;
    }

	private static List<SkinVisualsReference.VisualPart> ApplyVisualParts(
		UnitPrefabInfo prefab,
		Transform spriteContainer)
	{

		List<SkinVisualsReference.VisualPart> parts = new();

		if (prefab.visualParts.Count == 0)
			return parts;

		int nextPartSortingOrder = 0;
		int nextOutlineSortingOrder = -prefab.visualParts.Count;
		foreach (var info in prefab.visualParts)
		{
			parts.Add(CreateUnitVisualPart(info, spriteContainer, nextPartSortingOrder, nextOutlineSortingOrder));
			if(nextOutlineSortingOrder < -1)
			{
				nextOutlineSortingOrder++;
			}
			nextPartSortingOrder++;
			Console.Write($"{prefab.visualParts.Count} New orders {nextPartSortingOrder} {nextOutlineSortingOrder}");
		}

		return parts;
	}

	private static SkinVisualsReference.VisualPart CreateUnitVisualPart(
		VisualPartInfo info,
		Transform parent,
		int visualPartSortingOrder,
		int outlineSortingOrder)
	{
		var visualPartObj = new GameObject(info.defaultSpriteName);
		visualPartObj.transform.SetParent(parent);

		var outlineObj = new GameObject($"{info.defaultSpriteName}_Outline");
		outlineObj.transform.SetParent(visualPartObj.transform);

		visualPartObj.transform.position = outlineObj.transform.position = info.coordinates;
		visualPartObj.transform.rotation = outlineObj.transform.rotation = Quaternion.Euler(0f, 0f, info.rotation);

		// this bs was hunting me for so long. that is the thing which makes outline hidden in ui
		outlineObj.SetActive(false);

		var visualPart = new SkinVisualsReference.VisualPart
		{
			DefaultSpriteName = info.defaultSpriteName,
			visualPart = visualPartObj,
			outline = outlineObj,
			tintable = info.tintable
		};

		var renderer = visualPartObj.AddComponent<SpriteRenderer>();
		renderer.material = cachedUnitMaterial;
		renderer.sortingOrder = visualPartSortingOrder;
		renderer.sortingLayerName = "Units";
		visualPart.renderer = new SkinVisualsReference.RendererUnion { spriteRenderer = renderer };

		var outlineRenderer = outlineObj.AddComponent<SpriteRenderer>();
		outlineRenderer.material = cachedUnitMaterial;
		outlineRenderer.sortingOrder = outlineSortingOrder;
		outlineRenderer.sortingLayerName = "Units";
		visualPart.outlineRenderer = new SkinVisualsReference.RendererUnion { spriteRenderer = outlineRenderer };

		return visualPart;
	}

	private static SkinVisualsReference.VisualPart CreateImprovementVisualPart(
		VisualPartInfo info,
		Transform parent,
		int visualPartSortingOrder,
		int outlineSortingOrder)
	{
		return null;
	}

	private static SkinVisualsReference.VisualPart CreateResourceVisualPart(
		VisualPartInfo info,
		Transform parent,
		int visualPartSortingOrder,
		int outlineSortingOrder)
	{
		return null;
	}

	internal static void ProcessUnitPrefabInfo(UnitPrefabInfo unitPrefabInfo)
	{
			var baseUnit = PrefabManager.GetPrefab(UnitData.Type.Warrior, TribeType.Imperius, SkinType.Default);
			if (baseUnit == null)
				return;

			var unitInstance = GameObject.Instantiate(baseUnit);
			if (unitInstance == null)
				return;

			GameObject.Destroy(unitInstance.spriteContainer.gameObject);

			var spriteContainerObj = new GameObject("SpriteContainer");
			spriteContainerObj.transform.SetParent(unitInstance.transform, false);
			var spriteContainer = spriteContainerObj.transform;
			unitInstance.spriteContainer = spriteContainer;

			var visualParts = ApplyVisualParts(unitPrefabInfo, spriteContainer);

			Transform? headPositionMarker = null;
			foreach (var vp in visualParts)
			{
				if (vp.visualPart.gameObject.name == unitPrefabInfo.headPositionMarker)
				{
					headPositionMarker = vp.visualPart.gameObject.transform;
					break;
				}
			}

			unitInstance.headPositionMarker = headPositionMarker ?? visualParts[0].visualPart.transform;

			var svr = unitInstance.GetComponent<SkinVisualsReference>();
			svr.visualParts = visualParts.ToArray();

			GameObject.DontDestroyOnLoad(unitInstance.gameObject);

			PrefabRegistry.unitsNew[unitPrefabInfo.skinnedKey] = unitInstance;
	}

	internal static void ProcessImprovementPrefabInfo(ImprovementPrefabInfo unitPrefabInfo)
	{
	}

	internal static void ProcessResourcePrefabInfo(ResourcePrefabInfo unitPrefabInfo)
	{
	}
}