using System.Text.Json;
using System.Text.Json.Serialization;
using PolyMod.Json;
using Polytopia.Data;
using PolytopiaBackendBase.Common;
using UnityEngine;
using static PolyMod.Managers.PrefabRegistry;

namespace PolyMod.Managers;

public static class PrefabCreator
{

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
	}

	/// <returns>The material of the original sprite parts.</returns>
	private static Material? ClearExistingPartsAndExtractMaterial(Transform spriteContainer)
	{
		Material? material = null;
		for (int i = 0; i < spriteContainer.childCount; i++)
		{
			var child = spriteContainer.GetChild(i);
			if (child.gameObject.name == "Head")
			{
				var renderer = child.GetComponent<SpriteRenderer>();
				if (renderer != null)
					material = renderer.material;
			}
			GameObject.Destroy(child.gameObject);
		}
		return material;
	}

	private static List<SkinVisualsReference.VisualPart> ApplyVisualParts(
		UnitPrefabInfo prefab,
		Transform spriteContainer,
		Material? material)
	{
		if (prefab.visualParts.Count == 0)
			return new List<SkinVisualsReference.VisualPart>();

		List<SkinVisualsReference.VisualPart> parts = new();

		foreach (var info in prefab.visualParts)
		{
			parts.Add(CreateVisualPart(info, spriteContainer, material));
		}

		return parts;
	}

	private static SkinVisualsReference.VisualPart CreateVisualPart(
		VisualPartInfo info,
		Transform parent,
		Material? material)
	{
		var visualPartObj = new GameObject(info.defaultSpriteName);
		visualPartObj.transform.SetParent(parent);
		visualPartObj.transform.position = info.coordinates;
		visualPartObj.transform.rotation = Quaternion.Euler(0f, 0f, info.rotation);

		var outlineObj = new GameObject("Outline");
		outlineObj.transform.SetParent(visualPartObj.transform);
		outlineObj.transform.position = info.coordinates;
		outlineObj.transform.rotation = Quaternion.Euler(0f, 0f, info.rotation);

		var visualPart = new SkinVisualsReference.VisualPart
		{
			DefaultSpriteName = info.defaultSpriteName,
			visualPart = visualPartObj,
			outline = outlineObj,
			tintable = info.tintable
		};

		var renderer = visualPartObj.AddComponent<SpriteRenderer>();
		renderer.material = material;
		renderer.sortingLayerName = "Units";
		renderer.sortingOrder = info.tintable ? 0 : 1;

		visualPart.renderer = new SkinVisualsReference.RendererUnion { spriteRenderer = renderer };

		var outlineRenderer = outlineObj.AddComponent<SpriteRenderer>();
		outlineRenderer.material = material;
		outlineRenderer.sortingLayerName = "Units";
		outlineRenderer.sortingOrder = -1;

		visualPart.outlineRenderer = new SkinVisualsReference.RendererUnion { spriteRenderer = outlineRenderer };

		return visualPart;
	}

	internal static void ProcessUnitPrefabInfo(UnitPrefabInfo unitPrefabInfo)
	{
			var baseUnit = PrefabManager.GetPrefab(UnitData.Type.Warrior, TribeType.Imperius, SkinType.Default);
			if (baseUnit == null)
				return;

			var unitInstance = GameObject.Instantiate(baseUnit);
			if (unitInstance == null)
				return;

			var spriteContainer = unitInstance.transform.GetChild(0);
			var material = ClearExistingPartsAndExtractMaterial(spriteContainer);

			var visualParts = ApplyVisualParts(unitPrefabInfo, spriteContainer, material);

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