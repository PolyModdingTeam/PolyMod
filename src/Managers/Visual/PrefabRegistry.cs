using HarmonyLib;
using Polytopia.Data;
using PolytopiaBackendBase.Common;
using UnityEngine;
using static PrefabManager;
using static SkinVisualsReference;

namespace PolyMod.Managers;

public static class PrefabRegistry
{
    internal static Dictionary<SkinnedKey, Unit> unitsNew = new();
    internal static Dictionary<OverrideCondition, Dictionary<UnitData.Type, Unit>> unitOverridesNew = new();
    internal static Dictionary<SkinnedKey, Unit> unitUIOverridesNew = new();

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PrefabManager), nameof(PrefabManager.Awake))]
    private static void PrefabManager_Awake_Postfix(PrefabManager __instance)
    {
        unitsNew.Clear();
        unitOverridesNew.Clear();
        unitUIOverridesNew.Clear();

        foreach (UnitPrefabData unitPrefabData in __instance.unitPrefabs)
        {
            if(PrefabCreator.cachedUnitMaterial == null)
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
                            PrefabCreator.cachedUnitMaterial = headSpriteRenderer.sharedMaterial;
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
}