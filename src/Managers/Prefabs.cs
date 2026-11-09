using HarmonyLib;
using Polytopia.Data;
using PolytopiaBackendBase.Common;
using UnityEngine;
using static PrefabManager;
using static SkinVisualsReference;

namespace PolyMod.Managers;

/// <summary>
/// The manager that makes custom unit, improvement, and resource prefabs from mod data.
/// It also replaces the unit prefab lookup of <see cref="PrefabManager"/>.
/// </summary>
public static class Prefabs
{
    /// <summary>
    /// The lookup of unit prefabs by <see cref="SkinnedKey"/>.
    /// It contains the unit prefabs of the game and the custom unit prefabs that are not UI overrides.
    /// <see cref="ResolveUnitPrefab"/> also adds fallback prefabs to it.
    /// </summary>
    internal static Dictionary<SkinnedKey, Unit> unitsNew = new();
    /// <summary>
    /// The lookup of unit prefabs for each <c>OverrideCondition</c>, for example Christmas.
    /// The key is the condition, and the value gives the prefab for each unit type.
    /// It contains only the override prefabs of the game.
    /// </summary>
    internal static Dictionary<OverrideCondition, Dictionary<UnitData.Type, Unit>> unitOverridesNew = new(); // maybe in the future will implement
    /// <summary>
    /// The lookup of unit prefabs that the UI shows in place of the usual unit prefab, by <see cref="SkinnedKey"/>.
    /// It contains the UI override prefabs of the game and the custom prefabs that have <see cref="UnitPrefabInfo.uiOverride"/> set.
    /// </summary>
    internal static Dictionary<SkinnedKey, Unit> unitUIOverridesNew = new();

    /// <summary>
    /// The shared material of the first unit sprite renderer that <see cref="PrefabManager_Awake_Postfix"/> finds.
    /// Custom unit visual parts and their outlines use this material.
    /// </summary>
    internal static Material? cachedUnitMaterial;
    /// <summary>
    /// The loaded material that has the name <see cref="CACHED_SHARED_MATERIAL_NAME"/>.
    /// Custom improvement and resource visual parts use this material.
    /// </summary>
    internal static Material? cachedSharedMaterial;
    /// <summary>
    /// The name of the material that <see cref="CreatePrefabs"/> finds and keeps in <see cref="cachedSharedMaterial"/>.
    /// </summary>
    internal const string CACHED_SHARED_MATERIAL_NAME = "SpriteMaterial";

    /// <summary>
    /// The descriptions of the custom unit prefabs from the mod prefab files.
    /// <see cref="CreatePrefabs"/> makes one prefab for each item.
    /// </summary>
    public static List<UnitPrefabInfo> unitInfos = new();

    /// <summary>
    /// The descriptions of the custom resource prefabs.
    /// It contains the items from the mod prefab files and the default items that <see cref="CreatePrefabs"/> adds.
    /// <see cref="CreatePrefabs"/> makes one prefab for each item.
    /// </summary>
    public static List<ResourcePrefabInfo> resourceInfos = new();

    /// <summary>
    /// The descriptions of the custom improvement prefabs.
    /// It contains the items from the mod prefab files and the default items that <see cref="CreatePrefabs"/> adds.
    /// <see cref="CreatePrefabs"/> makes one prefab for each item.
    /// </summary>
    public static List<ImprovementPrefabInfo> improvementInfos = new();
    /// <summary>
    /// The improvement types that get a default prefab.
    /// The loader adds each GLD improvement that has the <c>prefab</c> value set to true.
    /// The value is true when the GLD <c>maxLevel</c> of the improvement is more than 0.
    /// </summary>
    internal static Dictionary<ImprovementData.Type, bool> improvementPrefabInfosToCreate = new();
    /// <summary>
    /// The resource types that get a default prefab.
    /// The loader adds each GLD resource that has the <c>prefab</c> value set to true.
    /// </summary>
    internal static List<ResourceData.Type> resourcePrefabsInfosToCreate = new();

    private static List<WeaponPrefabInfo> weaponInfos = new();
    private static Dictionary<UnitData.WeaponEnum, bool> weaponProjectilesCache = new();
    private static Dictionary<UnitData.WeaponEnum, float> weaponAnimTimeCache = new()
    {
        {UnitData.WeaponEnum.Sting, 0.5f},
        {UnitData.WeaponEnum.IceArrow, 1f},
        {UnitData.WeaponEnum.Gun, 1.5f}, // __instance.Sway(); __instance.Tile.SpawnDarkPuff();
        {UnitData.WeaponEnum.FireBlow, 2f}, // __instance.Tile.SpawnDarkPuff();
        {UnitData.WeaponEnum.Poison, 2f}, // __instance.Tile.SpawnPoison(__instance.Owner.GetVisualSkin());
        {UnitData.WeaponEnum.Pierce, 1f},
        {UnitData.WeaponEnum.Water, 1.5f}, // __instance.Sway(); __instance.Tile.SpawnPuff();
    };

    /// <summary>
    /// Patches <c>PrefabManager.Awake</c> as a postfix.
    /// Fills the PolyMod unit lookups from the prefab lists of the game.
    /// </summary>
    /// <remarks>
    /// The method clears <see cref="unitsNew"/>, <see cref="unitOverridesNew"/>, and <see cref="unitUIOverridesNew"/>.
    /// Then it adds the unit prefabs, the unit override prefabs, and the unit UI override prefabs of the game.
    /// If <see cref="cachedUnitMaterial"/> is null, it keeps the shared material of the first unit sprite renderer that it finds.
    /// The method also clears the custom unit prefabs that <see cref="CreatePrefabs"/> added before.
    /// </remarks>
    /// <param name="__instance">The <see cref="PrefabManager"/> that the game starts.</param>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(PrefabManager), nameof(PrefabManager.Awake))]
    private static void PrefabManager_Awake_Postfix(PrefabManager __instance)
    {
        unitsNew.Clear();
        unitOverridesNew.Clear();
        unitUIOverridesNew.Clear();

        foreach (UnitPrefabData unitPrefabData in __instance.unitPrefabs)
        {
            if (cachedUnitMaterial == null)
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

    /// <summary>
    /// Patches <c>PrefabManager.HasUIUnitOverride</c> as a prefix.
    /// Replaces the original check with a check in <see cref="unitUIOverridesNew"/>.
    /// </summary>
    /// <param name="__result">Set to true when <see cref="unitUIOverridesNew"/> contains the key, otherwise false.</param>
    /// <param name="unitType">The unit type.</param>
    /// <param name="tribe">The tribe.</param>
    /// <param name="skin">The skin.</param>
    /// <returns>Always false, so the original method does not run.</returns>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(PrefabManager), nameof(PrefabManager.HasUIUnitOverride))]
    public static bool HasUIUnitOverride(ref bool __result, UnitData.Type unitType, TribeType tribe, SkinType skin)
    {
        __result = unitUIOverridesNew.ContainsKey(new SkinnedKey(unitType, tribe, skin));
        return false;
    }

    /// <summary>
    /// Patches <c>PrefabManager.GetUnitOrUiOverride</c> as a prefix.
    /// Replaces the original method with a lookup in the PolyMod unit lookups.
    /// </summary>
    /// <remarks>
    /// The method first finds the key in <see cref="unitUIOverridesNew"/>.
    /// If the key is not there, it uses <see cref="ResolveUnitPrefab"/>.
    /// </remarks>
    /// <param name="__result">Set to the UI override prefab, or to the usual unit prefab.</param>
    /// <param name="unitType">The unit type.</param>
    /// <param name="tribe">The tribe.</param>
    /// <param name="skin">The skin.</param>
    /// <returns>Always false, so the original method does not run.</returns>
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

    /// <summary>
    /// Patches <c>PrefabManager.GetPrefab(UnitData.Type, TribeType, SkinType)</c> as a prefix.
    /// Replaces the original method with <see cref="ResolveUnitPrefab"/>.
    /// </summary>
    /// <param name="__result">Set to the unit prefab that <see cref="ResolveUnitPrefab"/> gives.</param>
    /// <param name="type">The unit type.</param>
    /// <param name="tribe">The tribe.</param>
    /// <param name="skin">The skin.</param>
    /// <returns>Always false, so the original method does not run.</returns>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(PrefabManager), nameof(PrefabManager.GetPrefab), new Type[] { typeof(UnitData.Type), typeof(TribeType), typeof(SkinType) })]
    public static bool PrefabManager_GetPrefab(ref Unit __result, UnitData.Type type, TribeType tribe, SkinType skin)
    {
        __result = ResolveUnitPrefab(type, tribe, skin);
        return false;
    }

    /// <summary>
    /// Patches <c>SkinVisualsReference.SetupVisualPart</c> as a postfix.
    /// Does the outline check for the visual part again and sets <c>hasValidOutline</c>.
    /// </summary>
    /// <remarks>
    /// If the outline is not a child of the visual part object, the method uses the first child as the outline.
    /// It does this only when the visual part object has children.
    /// Then it sets <c>hasValidOutline</c> to true only when the outline name is the default sprite name plus <c>_Outline</c>.
    /// </remarks>
    /// <param name="visualSetting">The visual part that the original method configured.</param>
    /// <param name="isImprovement">The value from the original call. The method does not use it.</param>
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

    // [HarmonyPrefix] // overrides again.......
    // [HarmonyPatch(typeof(Unit), nameof(Unit.IsProjectile))]
    // private static bool Unit_IsProjectile(ref bool __result, UnitData.WeaponEnum weapon)
    // {
    //     if(!weaponProjectilesCache.ContainsKey(weapon))
    //         return true;

    //     __result = weaponProjectilesCache[weapon];
    //     return false;
    // }

    // [HarmonyPrefix] // overrides again.......
    // [HarmonyPatch(typeof(Unit), nameof(Unit.Attack))]
    // private static bool Unit_Attack(Unit __instance, WorldCoordinates target, bool moveToTarget, Action onComplete)
    // {
    //     UnitData.WeaponEnum weaponEnum = __instance.unitData.weapon;
    //     List<WeaponPrefabInfo> weaponPrefabInfos = weaponInfos.Where(weaponInfo => weaponInfo.weaponEnum == weaponEnum).ToList();
    //     if(weaponPrefabInfos.Count == 0)
    //         return true;

    //     WeaponPrefabInfo weaponPrefabInfo = weaponPrefabInfos.Last();
    //     float chebyshevDistance = MapDataExtensions.ChebyshevDistance(__instance.Tile.Coordinates, target);

    //     float animationTime = 0.2f + chebyshevDistance * 0.05f;
    //     Tile targetTile = MapRenderer.Current.GetTileInstance(target);

    //     WeaponGFX pooledObject = ObjectPool.GetPooledObject<WeaponGFX>(GetPrefab(weaponEnum).gameObject);
    //     pooledObject.IsUsed = true;
    //     Vector3 center = SkinVisualsRenderer.GetSpriteBounds(__instance.skinVisuals).center;
    //     pooledObject.transform.position = __instance.transform.position + center;
    //     pooledObject.SetSkin(__instance.Owner.GetVisualSkin());

    //     if ((bool)targetTile.Unit)
    //     {
    //         targetTile.Unit.Flipped = targetTile.Unit.UnitState.flipped;
    //     }
    //     __instance.Flipped = __instance.UnitState.flipped;

    //     if(weaponPrefabInfo.weaponPuffInfo != null)
    //     {
    //         animationTime *= weaponPrefabInfo.weaponPuffInfo.animationMultiplier;
    //         if(weaponPrefabInfo.weaponPuffInfo.sway)
    //             __instance.Sway();
    //     }
    //     if (Unit.IsProjectile(weaponEnum))
    //     {
    //         if(weaponPrefabInfo.weaponPuffInfo != null && weaponPrefabInfo.weaponPuffInfo!.animationDelegate != null)
    //             pooledObject.AnimateRanged(animationTime, target, DelegateSupport.ConvertDelegate<Il2CppSystem.Action>(weaponPrefabInfo.weaponPuffInfo!.animationDelegate!));
    //         else
    //             pooledObject.AnimateRanged(animationTime, target);
    //     }
    //     else
    //     {
    //         pooledObject.AnimateCloseRangeAttack(0.2f);
    //     }

    //     if (__instance.unitData.GetRange() < 2)
    //     {
    //         __instance.AnimateMeleeAttackMovement(targetTile, moveToTarget, onComplete);
    //     }
    //     else
    //     {
    //         GameManager.DelayCall((int)(animationTime * 1000f), onComplete);
    //     }

    //     return false;
    // }

    // [HarmonyPrefix] // testing.......
    // [HarmonyPatch(typeof(WeaponGFX), nameof(WeaponGFX.AnimateCloseRangeAttack))]
    // public static bool WeaponGFX_AnimateCloseRangeAttack(ref float animTime, Action onComplete)
    // {
    //     animTime = 100000f;
    //     return true;
    // }

    // [HarmonyPrefix] // testing.......
    // [HarmonyPatch(typeof(WeaponGFX), nameof(WeaponGFX.AnimateRanged))]
    // public static bool WeaponGFX_AnimateRanged(ref float animTime, WorldCoordinates to, Action onComplete)
    // {
    //     animTime = 100000f;
    //     return true;
    // }

    /// <summary>
    /// Finds the unit prefab for a unit type, a tribe, and a skin.
    /// </summary>
    /// <remarks>
    /// The method tries these steps in this sequence:
    /// <list type="number">
    /// <item><description>If it is the Christmas season, it uses the Christmas override prefab for the unit type.</description></item>
    /// <item><description>It uses the prefab in <see cref="unitsNew"/> for the exact key.</description></item>
    /// <item><description>If the skin is not <c>SkinType.Default</c> or the tribe is not <c>TribeType.None</c>, it tries the key with no tribe, then the key with the default skin, then the key with no tribe and the default skin. It adds the prefab that it finds to <see cref="unitsNew"/> for the exact key.</description></item>
    /// <item><description>It writes a log message and uses the prefab for the default unit type with no tribe and the default skin.</description></item>
    /// </list>
    /// </remarks>
    /// <param name="unitType">The unit type.</param>
    /// <param name="tribeType">The tribe.</param>
    /// <param name="skinType">The skin.</param>
    /// <returns>The unit prefab, or null if the last step also finds no prefab.</returns>
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

    /// <summary>
    /// Makes all custom prefabs from the mod data. <see cref="Main.Load"/> calls this method after the mod files are loaded.
    /// </summary>
    /// <remarks>
    /// First, the method finds the material <see cref="CACHED_SHARED_MATERIAL_NAME"/> and keeps it in <see cref="cachedSharedMaterial"/>.
    /// Then it adds a default description to <see cref="improvementInfos"/> for each item in <see cref="improvementPrefabInfosToCreate"/>.
    /// It also adds a default description to <see cref="resourceInfos"/> for each item in <see cref="resourcePrefabsInfosToCreate"/>.
    /// Each default description has one visual part, and its sprite name is the <c>EnumCache</c> name of the type.
    /// Last, it makes one prefab for each item in <see cref="unitInfos"/>, <see cref="improvementInfos"/>, and <see cref="resourceInfos"/>.
    /// </remarks>
    internal static void CreatePrefabs()
    {
        // get materials
        Material[] allLoadedMaterials = Resources.FindObjectsOfTypeAll<Material>();
        foreach (Material loadedMaterial in allLoadedMaterials)
        {
            // Plugin.logger.LogInfo($"Material: {loadedMaterial.name}, shader: {loadedMaterial.shader.name}");

            if (loadedMaterial.name == CACHED_SHARED_MATERIAL_NAME)
            {
                cachedSharedMaterial = loadedMaterial;
                GameObject.DontDestroyOnLoad(cachedSharedMaterial);
                break;
            }
        }

        // creates dummy improvements. those have one visual part, and default name is the enum cache name of the type
        foreach (KeyValuePair<ImprovementData.Type, bool> improvementInfo in improvementPrefabInfosToCreate)
        {
            ImprovementData.Type improvementType = improvementInfo.Key;
            improvementInfos.Add(
                new ImprovementPrefabInfo()
                {
                    improvementType = improvementType,
                    visualParts = new()
                    {
                        new()
                        {
                            defaultSpriteName = EnumCache<ImprovementData.Type>.GetName(improvementType),
                            levels = improvementInfo.Value,
                        }
                    }
                }
            );
        }

        // creates dummy resources. those have one visual part, and default name is the enum cache name of the type
        foreach (ResourceData.Type resourceType in resourcePrefabsInfosToCreate)
        {
            resourceInfos.Add(
                new ResourcePrefabInfo()
                {
                    resourceType = resourceType,
                    visualParts = new()
                    {
                        new()
                        {
                            defaultSpriteName = EnumCache<ResourceData.Type>.GetName(resourceType),
                        }
                    }
                }
            );
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
    /// <summary>
    /// Makes a unit prefab from a <see cref="UnitPrefabInfo"/> and adds it to a unit lookup.
    /// </summary>
    /// <remarks>
    /// The method makes a copy of the default Imperius Warrior prefab.
    /// It removes the sprite container of the copy and adds an empty <c>SpriteContainer</c> object.
    /// Then it adds the visual parts from <see cref="ApplyUnitVisualParts"/>.
    /// The head position marker is the visual part with the name <see cref="UnitPrefabInfo.headPositionMarker"/>.
    /// If no visual part has this name, the method uses the first visual part that it made.
    /// <para>
    /// The method keeps the prefab when the scene changes.
    /// If <see cref="UnitPrefabInfo.uiOverride"/> is true, it adds the prefab to <see cref="unitUIOverridesNew"/>, otherwise to <see cref="unitsNew"/>.
    /// The key is <see cref="UnitPrefabInfo.skinnedKey"/>.
    /// If the method cannot get the Warrior prefab or make a copy, it does nothing.
    /// </para>
    /// </remarks>
    /// <param name="unitPrefabInfo">The description of the unit prefab.</param>
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

        if (unitPrefabInfo.uiOverride)
        {
            unitUIOverridesNew[unitPrefabInfo.skinnedKey] = unitInstance;
            return;
        }
        unitsNew[unitPrefabInfo.skinnedKey] = unitInstance;
    }

    /// <summary>
    /// Makes the visual parts of a custom unit prefab.
    /// </summary>
    /// <remarks>
    /// The method reverses the sequence of <see cref="BasePrefabInfo.visualParts"/> in the description itself.
    /// Then it makes the visual parts in the new sequence with <see cref="CreateUnitVisualPart"/>.
    /// The first part gets the sorting order 0, and each next part gets a sorting order that is 1 more.
    /// The outline sorting order starts at minus the number of parts and increases by 1 for each part, to a maximum of -1.
    /// <see cref="VisualPartInfo.sortingOrderOverride"/> and <see cref="VisualPartInfo.outlineSortingOrderOverride"/> replace these values when they are set.
    /// </remarks>
    /// <param name="unitPrefabInfo">The description of the unit prefab.</param>
    /// <param name="spriteContainer">The transform that becomes the parent of the visual parts.</param>
    /// <returns>The visual parts, or an empty list if the description has no visual parts.</returns>
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
            if (nextOutlineSortingOrder < -1)
            {
                nextOutlineSortingOrder++;
            }
            nextPartSortingOrder++;
        }

        return parts;
    }

    /// <summary>
    /// Makes one visual part of a custom unit prefab, with its outline.
    /// </summary>
    /// <remarks>
    /// The visual part object has the name <see cref="VisualPartInfo.defaultSpriteName"/>.
    /// The outline object has the same name plus <c>_Outline</c>, and it is a child of the visual part object.
    /// The method sets the world position and the z rotation in degrees of both objects from the description.
    /// It deactivates the outline object, which keeps the outline hidden in the UI.
    /// Both objects get a <c>SpriteRenderer</c> that uses <see cref="cachedUnitMaterial"/> in the <c>Units</c> sorting layer.
    /// </remarks>
    /// <param name="info">The description of the visual part.</param>
    /// <param name="parent">The transform that becomes the parent of the visual part object.</param>
    /// <param name="visualPartSortingOrder">The sorting order of the visual part renderer.</param>
    /// <param name="outlineSortingOrder">The sorting order of the outline renderer.</param>
    /// <returns>The visual part.</returns>
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

        var visualPart = info.ToVisualPart(false);
        visualPart.visualPart = visualPartObj;
        visualPart.outline = outlineObj;

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
    /// <summary>
    /// Makes an improvement prefab from an <see cref="ImprovementPrefabInfo"/> and puts it in <c>PrefabManager.improvements</c>.
    /// </summary>
    /// <remarks>
    /// The method makes a copy of the Lumber Hut prefab and removes all children of the copy.
    /// Then it adds the visual parts from <see cref="ApplyBuildingVisualParts"/>.
    /// It keeps the prefab when the scene changes.
    /// The prefab replaces the prefab of the same improvement type in <c>PrefabManager.improvements</c>.
    /// If <see cref="ImprovementPrefabInfo.improvementType"/> is null, or the method cannot get the Lumber Hut prefab or make a copy, it does nothing.
    /// </remarks>
    /// <param name="improvementPrefabInfo">The description of the improvement prefab.</param>
    internal static void ProcessImprovementPrefabInfo(ImprovementPrefabInfo improvementPrefabInfo)
    {
        if (improvementPrefabInfo.improvementType == null)
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

    /// <summary>
    /// Makes the visual parts of a custom improvement prefab.
    /// </summary>
    /// <remarks>
    /// The method makes the visual parts in the sequence of <see cref="BasePrefabInfo.visualParts"/> with <see cref="CreateBuildingVisualPart"/>.
    /// The first part gets the sorting order 0, and each next part gets a sorting order that is 1 more.
    /// The outline sorting order starts at minus the number of parts and increases by 1 for each part, to a maximum of -1.
    /// The method does not use <see cref="VisualPartInfo.sortingOrderOverride"/> or <see cref="VisualPartInfo.outlineSortingOrderOverride"/>.
    /// </remarks>
    /// <param name="improvementPrefabInfo">The description of the improvement prefab.</param>
    /// <param name="spriteContainer">The transform that becomes the parent of the visual parts.</param>
    /// <returns>The visual parts, or an empty list if the description has no visual parts.</returns>
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
            if (nextOutlineSortingOrder < -1)
            {
                nextOutlineSortingOrder++;
            }
            nextPartSortingOrder++;
        }

        return parts;
    }

    /// <summary>
    /// Makes one visual part of a custom improvement prefab, with its outline.
    /// </summary>
    /// <remarks>
    /// The visual part object has the name <see cref="VisualPartInfo.defaultSpriteName"/>.
    /// The outline object has the same name plus <c>_Outline</c>, and it is a child of the visual part object.
    /// The method sets the world position and the z rotation in degrees of both objects from the description.
    /// It deactivates the outline object.
    /// The visual part object gets a <c>PolytopiaSpriteRenderer</c> from <see cref="AddPolytopiaSpriteRenderer"/>.
    /// The outline object gets a renderer only when <paramref name="needsOutline"/> is true.
    /// </remarks>
    /// <param name="info">The description of the visual part.</param>
    /// <param name="parent">The transform that becomes the parent of the visual part object.</param>
    /// <param name="visualPartSortingOrder">The sorting order of the visual part renderer.</param>
    /// <param name="outlineSortingOrder">The sorting order of the outline renderer.</param>
    /// <param name="needsOutline">True to add a renderer to the outline object.</param>
    /// <returns>The visual part, marked as an improvement part.</returns>
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

        var visualPart = info.ToVisualPart(true);
        visualPart.visualPart = visualPartObj;
        visualPart.outline = outlineObj;

        var renderer = AddPolytopiaSpriteRenderer(visualPartObj, visualPartSortingOrder);
        visualPart.renderer = new SkinVisualsReference.RendererUnion { polytopiaSpriteRenderer = renderer };

        if (needsOutline)
        {
            var outlineRenderer = AddPolytopiaSpriteRenderer(outlineObj, outlineSortingOrder);
            visualPart.outlineRenderer = new SkinVisualsReference.RendererUnion { polytopiaSpriteRenderer = outlineRenderer };
        }

        return visualPart;
    }

    /// <summary>
    /// Adds a <c>MeshFilter</c>, a <c>MeshRenderer</c>, and a <c>PolytopiaSpriteRenderer</c> to an object.
    /// </summary>
    /// <remarks>
    /// The mesh renderer does not cast or receive shadows, and it does not use light probes or reflection probes.
    /// The method sets the sorting layer and the sorting order of the sprite renderer.
    /// If <see cref="cachedSharedMaterial"/> is not null, the sprite renderer uses it as the shared material.
    /// </remarks>
    /// <param name="targetGameObject">The object that gets the components.</param>
    /// <param name="sortingOrder">The sorting order of the sprite renderer.</param>
    /// <param name="sortingLayerName">The name of the sorting layer of the sprite renderer. The default is <c>Terrain</c>.</param>
    /// <returns>The new <c>PolytopiaSpriteRenderer</c>.</returns>
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

    /// <summary>
    /// Makes a resource prefab from a <see cref="ResourcePrefabInfo"/> and puts it in <c>PrefabManager.resources</c>.
    /// </summary>
    /// <remarks>
    /// The method makes a copy of the Game resource prefab and removes all children of the copy.
    /// Then it adds the visual parts from <see cref="ApplyResourceVisualParts"/>.
    /// It keeps the prefab when the scene changes.
    /// The prefab replaces the prefab of the same resource type in <c>PrefabManager.resources</c>.
    /// If <see cref="ResourcePrefabInfo.resourceType"/> is null, or the method cannot get the Game prefab or make a copy, it does nothing.
    /// </remarks>
    /// <param name="resourcePrefabInfo">The description of the resource prefab.</param>
    internal static void ProcessResourcePrefabInfo(ResourcePrefabInfo resourcePrefabInfo)
    {
        if (resourcePrefabInfo.resourceType == null)
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

    /// <summary>
    /// Makes the visual parts of a custom resource prefab.
    /// </summary>
    /// <remarks>
    /// The method makes the visual parts in the sequence of <see cref="BasePrefabInfo.visualParts"/> with <see cref="CreateResourceVisualPart"/>.
    /// The first part gets the sorting order 0, and each next part gets a sorting order that is 1 more.
    /// The outline sorting order starts at minus the number of parts and increases by 1 for each part, to a maximum of -1.
    /// The method does not use <see cref="VisualPartInfo.sortingOrderOverride"/> or <see cref="VisualPartInfo.outlineSortingOrderOverride"/>.
    /// </remarks>
    /// <param name="resourcePrefabInfo">The description of the resource prefab.</param>
    /// <param name="spriteContainer">The transform that becomes the parent of the visual parts.</param>
    /// <returns>The visual parts, or an empty list if the description has no visual parts.</returns>
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
            if (nextOutlineSortingOrder < -1)
            {
                nextOutlineSortingOrder++;
            }
            nextPartSortingOrder++;
        }

        return parts;
    }

    /// <summary>
    /// Makes one visual part of a custom resource prefab, with its outline.
    /// </summary>
    /// <remarks>
    /// The visual part object has the name <see cref="VisualPartInfo.defaultSpriteName"/>.
    /// The outline object has the same name plus <c>_Outline</c>, and it is a child of the visual part object.
    /// The method sets the world position and the z rotation in degrees of both objects from the description.
    /// It deactivates the outline object.
    /// Both objects get a <c>PolytopiaSpriteRenderer</c> from <see cref="AddPolytopiaSpriteRenderer"/>.
    /// </remarks>
    /// <param name="info">The description of the visual part.</param>
    /// <param name="parent">The transform that becomes the parent of the visual part object.</param>
    /// <param name="visualPartSortingOrder">The sorting order of the visual part renderer.</param>
    /// <param name="outlineSortingOrder">The sorting order of the outline renderer.</param>
    /// <returns>The visual part.</returns>
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

        var visualPart = info.ToVisualPart(false);
        visualPart.visualPart = visualPartObj;
        visualPart.outline = outlineObj;

        var renderer = AddPolytopiaSpriteRenderer(visualPartObj, visualPartSortingOrder);
        visualPart.renderer = new SkinVisualsReference.RendererUnion { polytopiaSpriteRenderer = renderer };

        var outlineRenderer = AddPolytopiaSpriteRenderer(outlineObj, outlineSortingOrder);
        visualPart.outlineRenderer = new SkinVisualsReference.RendererUnion { polytopiaSpriteRenderer = outlineRenderer };

        return visualPart;
    }

    #endregion

    /// <summary>
    /// Applies the Harmony patches of this class.
    /// </summary>
    internal static void Init()
    {
        Harmony.CreateAndPatchAll(typeof(Prefabs));
    }
}
