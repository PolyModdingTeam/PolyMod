using HarmonyLib;
using PolyMod;
using Polytopia.Data;
using PolytopiaBackendBase.Common;
using static PrefabManager;

public static class PrefabFix
{
    public readonly struct SkinnedKey : IEquatable<SkinnedKey>
    {
        private readonly UnitData.Type unitType;
        private readonly TribeType tribeType;
        private readonly SkinType skinType;

        public SkinnedKey(UnitData.Type unitType, TribeType tribeType, SkinType skinType)
        {
            this.unitType = unitType;
            this.tribeType = tribeType;
            this.skinType = skinType;
        }

        public bool Equals(SkinnedKey other)
        {
            return unitType == other.unitType
                && tribeType == other.tribeType
                && skinType == other.skinType;
        }

        public override bool Equals(object? other)
        {
            return other is SkinnedKey otherKey && Equals(otherKey);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine((int)unitType, (int)tribeType, (int)skinType);
        }
    }

    private static readonly Dictionary<SkinnedKey, Unit> unitsNew = new();
    private static readonly Dictionary<OverrideCondition, Dictionary<UnitData.Type, Unit>> unitOverridesNew = new();
    private static readonly Dictionary<SkinnedKey, Unit> unitUIOverridesNew = new();

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PrefabManager), nameof(PrefabManager.Awake))]
    private static void PrefabManager_Awake_Postfix(PrefabManager __instance)
    {
        unitsNew.Clear();
        unitOverridesNew.Clear();
        unitUIOverridesNew.Clear();

        foreach (UnitPrefabData unitPrefabData in __instance.unitPrefabs)
        {
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