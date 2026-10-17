using HarmonyLib;
using Polytopia.Data;
using PolytopiaBackendBase.Common;
using UnityEngine;
using static PrefabManager;
using static SkinVisualsReference;

namespace PolyMod.Managers;

public static class Prefabs
{
    internal static Dictionary<SkinnedKey, Unit> unitsNew = new();
    internal static Dictionary<OverrideCondition, Dictionary<UnitData.Type, Unit>> unitOverridesNew = new();
    internal static Dictionary<SkinnedKey, Unit> unitUIOverridesNew = new();

	internal static Material? cachedUnitMaterial;
	internal static Material? cachedSharedMaterial;

	internal static Dictionary<string, List<byte[]>> prefabsFilesToProcess = new();

	public static List<UnitPrefabInfo> unitInfos = new();

	public static List<ImprovementPrefabInfo> improvementInfos = new();

	public static List<ResourcePrefabInfo> resourceInfos = new();

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PrefabManager), nameof(PrefabManager.Awake))]
    private static void PrefabManager_Awake_Postfix(PrefabManager __instance)
    {
        unitsNew.Clear();
        unitOverridesNew.Clear();
        unitUIOverridesNew.Clear();

        foreach (UnitPrefabData unitPrefabData in __instance.unitPrefabs)
        {
            if(cachedUnitMaterial == null)
            {
                SkinVisualsReference skinVisualsReference = unitPrefabData.prefab.GetComponent<SkinVisualsReference>();
                if (skinVisualsReference != null && skinVisualsReference.visualParts != null)
                {
                    foreach (VisualPart currentPart in skinVisualsReference.visualParts)
                    {
                        if (currentPart.visualPart == null)
                        {
                            continue;
                        }

                        SpriteRenderer headSpriteRenderer = currentPart.visualPart.GetComponent<SpriteRenderer>();
                        if (headSpriteRenderer != null)
                        {
                            cachedUnitMaterial = headSpriteRenderer.sharedMaterial;
                            break;
                        }
                    }
                }
            }
            unitsNew[new SkinnedKey(unitPrefabData.type, unitPrefabData.tribe, unitPrefabData.skin)] = unitPrefabData.prefab;
        }

        foreach (UnitOverridePrefabData unitOverridePrefabData in __instance.unitOverridePrefabs)
        {
            if (!unitOverridesNew.TryGetValue(unitOverridePrefabData.overrideCondition, out Dictionary<UnitData.Type, Unit>? overridesForCondition))
            {
                overridesForCondition = new Dictionary<UnitData.Type, Unit>();
                unitOverridesNew[unitOverridePrefabData.overrideCondition] = overridesForCondition;
            }
            overridesForCondition[unitOverridePrefabData.type] = unitOverridePrefabData.prefab;
        }

        foreach (UnitPrefabData unitUIOverridePrefabData in __instance.unitUIOverridePrefabs)
        {
            unitUIOverridesNew[new SkinnedKey(unitUIOverridePrefabData.type, unitUIOverridePrefabData.tribe, unitUIOverridePrefabData.skin)] = unitUIOverridePrefabData.prefab;
        }

        Plugin.logger.LogInfo($"Built lookups: {unitsNew.Count} units, {unitUIOverridesNew.Count} UI overrides");
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(PrefabManager), nameof(PrefabManager.HasUIUnitOverride))]
    public static bool HasUIUnitOverride(ref bool __result, UnitData.Type unitType, TribeType tribe, SkinType skin)
    {
        __result = unitUIOverridesNew.ContainsKey(new SkinnedKey(unitType, tribe, skin));
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(PrefabManager), nameof(PrefabManager.GetUnitOrUiOverride))]
    public static bool GetUnitOrUiOverride(ref Unit __result, UnitData.Type unitType, TribeType tribe, SkinType skin)
    {
        if (unitUIOverridesNew.TryGetValue(new SkinnedKey(unitType, tribe, skin), out Unit? uiOverridePrefab))
        {
            __result = uiOverridePrefab;
        }
        else
        {
            __result = ResolveUnitPrefab(unitType, tribe, skin);
        }
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(PrefabManager), nameof(PrefabManager.GetPrefab), new Type[] { typeof(UnitData.Type), typeof(TribeType), typeof(SkinType) })]
    public static bool PrefabManager_GetPrefab(ref Unit __result, UnitData.Type type, TribeType tribe, SkinType skin)
    {
        __result = ResolveUnitPrefab(type, tribe, skin);
        return false;
    }

    [HarmonyPostfix] // prolly remove later
    [HarmonyPatch(typeof(SkinVisualsReference), nameof(SkinVisualsReference.SetupVisualPart))]
    public static void SetupVisualPart(ref VisualPart visualSetting, bool isImprovement)
    {
        bool flag = false;
		flag = visualSetting.outline != null && visualSetting.outline.transform.parent == visualSetting.visualPart.transform;
		if (!flag && visualSetting.visualPart.transform.childCount > 0)
		{
			visualSetting.outline = visualSetting.visualPart.transform.GetChild(0).gameObject;
			flag = true;
		}
		bool flag2 = false;
		if (flag)
		{
			string defaultSpriteName = visualSetting.DefaultSpriteName;
			if (visualSetting.outline != null && visualSetting.outline.name == defaultSpriteName + "_Outline")
			{
				flag2 = true;
			}
		}
		visualSetting.hasValidOutline = flag2;
    }

    private static Unit ResolveUnitPrefab(UnitData.Type unitType, TribeType tribeType, SkinType skinType)
    {
        if (SeasonManager.IsChristmas()
            && unitOverridesNew.TryGetValue(OverrideCondition.Christmas, out Dictionary<UnitData.Type, Unit>? christmasOverrides)
            && christmasOverrides.TryGetValue(unitType, out Unit? christmasPrefab)
            && christmasPrefab != null)
        {
            return christmasPrefab;
        }

        SkinnedKey exactKey = new SkinnedKey(unitType, tribeType, skinType);
        if (unitsNew.TryGetValue(exactKey, out Unit? exactPrefab))
        {
            return exactPrefab;
        }

        if (skinType != SkinType.Default || tribeType != TribeType.None)
        {
            SkinnedKey noTribeKey = new SkinnedKey(unitType, TribeType.None, skinType);
            SkinnedKey defaultSkinKey = new SkinnedKey(unitType, tribeType, SkinType.Default);
            SkinnedKey noTribeDefaultSkinKey = new SkinnedKey(unitType, TribeType.None, SkinType.Default);

            if (unitsNew.TryGetValue(noTribeKey, out Unit? fallbackPrefab)
                || unitsNew.TryGetValue(defaultSkinKey, out fallbackPrefab)
                || unitsNew.TryGetValue(noTribeDefaultSkinKey, out fallbackPrefab))
            {
                unitsNew[exactKey] = fallbackPrefab;
                return fallbackPrefab;
            }
        }

        Plugin.logger.LogInfo($"Couldn't find prefab for type: {unitType}");
        unitsNew.TryGetValue(new SkinnedKey(default, TribeType.None, SkinType.Default), out Unit? lastResortPrefab);
        return lastResortPrefab!;
    }

	internal static void CreatePrefabs()
	{
		// get materials
		Material[] allLoadedMaterials = Resources.FindObjectsOfTypeAll<Material>();
		foreach (Material loadedMaterial in allLoadedMaterials)
		{
			if(loadedMaterial.name == "SpriteMaterial")
			{
				cachedSharedMaterial = loadedMaterial;
				GameObject.DontDestroyOnLoad(cachedSharedMaterial);
				break;
			}
			Plugin.logger.LogInfo($"Material: {loadedMaterial.name}, shader: {loadedMaterial.shader.name}");
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

	#region Units
	internal static void ProcessUnitPrefabInfo(UnitPrefabInfo unitPrefabInfo)
	{
		// maybe just init new unit? hmmmmm will try later
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

		var visualParts = ApplyUnitVisualParts(unitPrefabInfo, spriteContainer);

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

		unitsNew[unitPrefabInfo.skinnedKey] = unitInstance;
	}

	private static List<SkinVisualsReference.VisualPart> ApplyUnitVisualParts(
		UnitPrefabInfo unitPrefabInfo,
		Transform spriteContainer)
	{
		List<SkinVisualsReference.VisualPart> parts = new();
		List<VisualPartInfo> visualPartInfos = unitPrefabInfo.visualParts;

		if (visualPartInfos.Count == 0)
			return parts;

		int nextPartSortingOrder = 0;
		int nextOutlineSortingOrder = -visualPartInfos.Count;
		var newVisualInfo = visualPartInfos;
		newVisualInfo.Reverse();
		foreach (var info in newVisualInfo)
		{
			parts.Add(CreateUnitVisualPart(info, spriteContainer, info.sortingOrderOverride ?? nextPartSortingOrder, info.outlineSortingOrderOverride ?? nextOutlineSortingOrder));
			if(nextOutlineSortingOrder < -1)
			{
				nextOutlineSortingOrder++;
			}
			nextPartSortingOrder++;
			Console.Write($"{visualPartInfos.Count} New orders {nextPartSortingOrder} {nextOutlineSortingOrder}");
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

		visualPartObj.transform.position = info.coordinates;
		outlineObj.transform.position = info.coordinates;
		visualPartObj.transform.rotation = outlineObj.transform.rotation = Quaternion.Euler(0f, 0f, info.rotation);

		// this bs was hunting me for so long. that is the thing which makes outline hidden in ui
		outlineObj.SetActive(false);

		var visualPart = new SkinVisualsReference.VisualPart
		{
			DefaultSpriteName = info.defaultSpriteName,
			visualPart = visualPartObj,
			outline = outlineObj,
			tintable = info.tintable,
			skinLogic = info.skinningLogic,
			improvementSkinLogic = info.improvementSkinningLogic,
			levels = info.levels
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

	#endregion
	#region Improvements
	internal static void ProcessImprovementPrefabInfo(ImprovementPrefabInfo improvementPrefabInfo)
	{
		if(improvementPrefabInfo.improvementType == null)
			return;

		var baseBuilding = PrefabManager.GetPrefab(ImprovementData.Type.LumberHut);
		if (baseBuilding == null)
			return;

		var buildingInstance = GameObject.Instantiate(baseBuilding);
		if (buildingInstance == null)
			return;

		Transform buildingTransform = buildingInstance.transform;
		for (int childIndex = buildingTransform.childCount - 1; childIndex >= 0; childIndex--)
		{
			GameObject.Destroy(buildingTransform.GetChild(childIndex).gameObject);
		}

		var visualParts = ApplyBuildingVisualParts(improvementPrefabInfo, buildingTransform);

		var svr = buildingInstance.GetComponent<SkinVisualsReference>();
		svr.visualParts = visualParts.ToArray();

		GameObject.DontDestroyOnLoad(buildingInstance.gameObject);

		PrefabManager.improvements[(Polytopia.Data.ImprovementData.Type)improvementPrefabInfo.improvementType] = buildingInstance;
	}

	private static List<SkinVisualsReference.VisualPart> ApplyBuildingVisualParts(
		ImprovementPrefabInfo improvementPrefabInfo,
		Transform spriteContainer)
	{
		List<SkinVisualsReference.VisualPart> parts = new();
		List<VisualPartInfo> visualPartInfos = improvementPrefabInfo.visualParts;

		if (visualPartInfos.Count == 0)
			return parts;

		int nextPartSortingOrder = 0;
		int nextOutlineSortingOrder = -visualPartInfos.Count;
		foreach (var info in visualPartInfos)
		{
			parts.Add(CreateBuildingVisualPart(info, spriteContainer, nextPartSortingOrder, nextOutlineSortingOrder, improvementPrefabInfo.needsOutlines));
			if(nextOutlineSortingOrder < -1)
			{
				nextOutlineSortingOrder++;
			}
			nextPartSortingOrder++;
		}

		return parts;
	}

	private static SkinVisualsReference.VisualPart CreateBuildingVisualPart(
		VisualPartInfo info,
		Transform parent,
		int visualPartSortingOrder,
		int outlineSortingOrder,
		bool needsOutline = false)
	{
		var visualPartObj = new GameObject(info.defaultSpriteName);
		visualPartObj.transform.SetParent(parent);

		var outlineObj = new GameObject($"{info.defaultSpriteName}_Outline");
		outlineObj.transform.SetParent(visualPartObj.transform);

		visualPartObj.transform.position = outlineObj.transform.position = info.coordinates;
		visualPartObj.transform.rotation = outlineObj.transform.rotation = Quaternion.Euler(0f, 0f, info.rotation);

		outlineObj.SetActive(false);

		var visualPart = new SkinVisualsReference.VisualPart
		{
			DefaultSpriteName = info.defaultSpriteName,
			visualPart = visualPartObj,
			outline = outlineObj,
			tintable = info.tintable,
			skinLogic = info.skinningLogic,
			improvementSkinLogic = info.improvementSkinningLogic,
			levels = info.levels,
			isImprovement = true,
		};
		var renderer = AddPolytopiaSpriteRenderer(visualPartObj, visualPartSortingOrder);
		visualPart.renderer = new SkinVisualsReference.RendererUnion { polytopiaSpriteRenderer = renderer };

		if(needsOutline)
		{
			var outlineRenderer = AddPolytopiaSpriteRenderer(outlineObj, outlineSortingOrder);
			visualPart.outlineRenderer = new SkinVisualsReference.RendererUnion { polytopiaSpriteRenderer = outlineRenderer };
		}

		return visualPart;
	}

	private static PolytopiaSpriteRenderer AddPolytopiaSpriteRenderer(GameObject targetGameObject, int sortingOrder, string sortingLayerName = "Terrain")
	{
		MeshFilter meshFilter = targetGameObject.AddComponent<MeshFilter>();
		MeshRenderer meshRenderer = targetGameObject.AddComponent<MeshRenderer>();
		meshRenderer.receiveShadows = false;
		meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
		meshRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
		meshRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

		PolytopiaSpriteRenderer polytopiaSpriteRenderer = targetGameObject.AddComponent<PolytopiaSpriteRenderer>();
		polytopiaSpriteRenderer.meshFilter = meshFilter;
		polytopiaSpriteRenderer.meshRenderer = meshRenderer;

		polytopiaSpriteRenderer.SortingLayer = SortingLayer.NameToID(sortingLayerName);
		polytopiaSpriteRenderer.SortingOrder = sortingOrder;
		if (cachedSharedMaterial != null)
		{
			polytopiaSpriteRenderer.SharedMaterial = cachedSharedMaterial;
		}
		return polytopiaSpriteRenderer;
	}

	#endregion
	#region Resources

	internal static void ProcessResourcePrefabInfo(ResourcePrefabInfo resourcePrefabInfo)
	{
		if(resourcePrefabInfo.resourceType == null)
			return;

		var baseResource = PrefabManager.GetPrefab(ResourceData.Type.Game);
		if (baseResource == null)
			return;

		var resourceInstance = GameObject.Instantiate(baseResource);
		if (resourceInstance == null)
			return;

		Transform resourceTransform = resourceInstance.transform;
		for (int childIndex = resourceTransform.childCount - 1; childIndex >= 0; childIndex--)
		{
			GameObject.Destroy(resourceTransform.GetChild(childIndex).gameObject);
		}

		var visualParts = ApplyResourceVisualParts(resourcePrefabInfo, resourceTransform);

		var svr = resourceInstance.GetComponent<SkinVisualsReference>();
		svr.visualParts = visualParts.ToArray();

		GameObject.DontDestroyOnLoad(resourceInstance.gameObject);

		PrefabManager.resources[(Polytopia.Data.ResourceData.Type)resourcePrefabInfo.resourceType] = resourceInstance;
	}

	private static List<SkinVisualsReference.VisualPart> ApplyResourceVisualParts(
		ResourcePrefabInfo resourcePrefabInfo,
		Transform spriteContainer)
	{
		List<SkinVisualsReference.VisualPart> parts = new();
		List<VisualPartInfo> visualPartInfos = resourcePrefabInfo.visualParts;

		if (visualPartInfos.Count == 0)
			return parts;

		int nextPartSortingOrder = 0;
		int nextOutlineSortingOrder = -visualPartInfos.Count;
		foreach (var info in visualPartInfos)
		{
			parts.Add(CreateResourceVisualPart(info, spriteContainer, nextPartSortingOrder, nextOutlineSortingOrder));
			if(nextOutlineSortingOrder < -1)
			{
				nextOutlineSortingOrder++;
			}
			nextPartSortingOrder++;
			Console.Write($"{visualPartInfos.Count} New orders {nextPartSortingOrder} {nextOutlineSortingOrder}");
		}

		return parts;
	}

	private static SkinVisualsReference.VisualPart CreateResourceVisualPart(
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

		outlineObj.SetActive(false);

		var visualPart = new SkinVisualsReference.VisualPart
		{
			DefaultSpriteName = info.defaultSpriteName,
			visualPart = visualPartObj,
			outline = outlineObj,
			tintable = info.tintable,
			skinLogic = info.skinningLogic,
			improvementSkinLogic = info.improvementSkinningLogic,
			levels = info.levels
		};
		var renderer = AddPolytopiaSpriteRenderer(visualPartObj, visualPartSortingOrder);
		visualPart.renderer = new SkinVisualsReference.RendererUnion { polytopiaSpriteRenderer = renderer };

		var outlineRenderer = AddPolytopiaSpriteRenderer(outlineObj, outlineSortingOrder);
		visualPart.outlineRenderer = new SkinVisualsReference.RendererUnion { polytopiaSpriteRenderer = outlineRenderer };

		return visualPart;
	}

	#endregion
}