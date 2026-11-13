using BepInEx.Unity.IL2CPP.Logging;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using Polytopia.Data;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using UnityEngine;
using PolytopiaBackendBase.Common;

namespace PolyMod.Managers;

/// <summary>
/// The main manager of PolyMod. It registers and loads the mods, and it patches game logic.
/// </summary>
public static class Main
{
    private static readonly string[] polytopiaAuthors = { "Midjiwan AB" };

    /// <summary>
    /// The flag that shows that the mod dependencies contain a cycle.
    /// When it is true, <see cref="Load"/> loads only the PolyMod localization.
    /// </summary>
    internal static bool dependencyCycle;

    /// <summary>
    /// The highest tech tier that gets an ID in <c>TechItem.techTierFirebaseId</c>.
    /// <see cref="Load"/> adds the IDs for the tiers from 0 to this value.
    /// </summary>
    internal const int MAX_TECH_TIER = 100;

    /// <summary>
    /// The stopwatch that measures the time to register and load the mods.
    /// It measures the sum of the time in <see cref="Init"/> and in <see cref="Load"/>.
    /// </summary>
    internal static readonly Stopwatch stopwatch = new();

    /// <summary>
    /// The flag that shows that <see cref="Load"/> ran.
    /// <see cref="GameLogicData_AddGameLogicPlaceholders"/> uses it to call <see cref="Load"/> only one time.
    /// </summary>
    internal static bool fullyInitialized;

    /// <summary>
    /// The embark overrides from the GLD, by ID.
    /// The key is the ID of a unit, and the value is the ID of the unit that it embarks into.
    /// </summary>
    internal static Dictionary<string, string> embarkNames = new();

    /// <summary>
    /// The embark overrides, by unit type.
    /// The key is the type of a unit, and the value is the type of the unit that it embarks into.
    /// </summary>
    internal static Dictionary<UnitData.Type, UnitData.Type> embarkOverrides = new();

    /// <summary>
    /// The flag that shows that an <c>EmbarkAction</c> runs now.
    /// <see cref="EmbarkAction_Execute_Prefix"/> sets it to true, and <see cref="ActionUtils_TrainUnit"/> sets it to false.
    /// </summary>
    internal static bool currentlyEmbarking = false;

    /// <summary>
    /// The resources that improvements attract, by ID, from the GLD.
    /// The key is the ID of an improvement, and the value is the ID of the resource.
    /// </summary>
    internal static Dictionary<string, string> attractsResourceNames = new();

    /// <summary>
    /// The terrains on which improvements make attracted resources, by ID, from the GLD.
    /// The key is the ID of an improvement, and the value is the ID of the terrain.
    /// </summary>
    internal static Dictionary<string, string> attractsTerrainNames = new();

    /// <summary>
    /// The resources that improvements attract, by type.
    /// If an improvement is not in this lookup, <see cref="StartTurnAction_Execute"/> uses the Game resource.
    /// </summary>
    internal static Dictionary<ImprovementData.Type, ResourceData.Type> attractsResourceOverrides = new();

    /// <summary>
    /// The terrains on which improvements make attracted resources, by type.
    /// If an improvement is not in this lookup, <see cref="StartTurnAction_Execute"/> uses the Forest terrain.
    /// </summary>
    internal static Dictionary<ImprovementData.Type, Polytopia.Data.TerrainData.Type> attractsTerrainOverrides = new();

    /// <summary>
    /// Patches <c>GameLogicData.AddGameLogicPlaceholders</c> as a prefix.
    /// Calls <see cref="Load"/> the first time that the game parses the GLD.
    /// The original method always runs after this prefix.
    /// </summary>
    /// <param name="__instance">The game logic data that the game parses.</param>
    /// <param name="rootObject">The root JSON object of the GLD. <see cref="Load"/> merges the mod patches into it.</param>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(GameLogicData), nameof(GameLogicData.AddGameLogicPlaceholders))]
    private static void GameLogicData_AddGameLogicPlaceholders(GameLogicData __instance, ref JObject rootObject)
    {
        if (!fullyInitialized)
        {
            Load(__instance, rootObject);
            fullyInitialized = true;
        }
    }

    /// <summary>
    /// Patches <c>PurchaseManager.IsSkinUnlocked</c> as a prefix.
    /// Unlocks all custom skins.
    /// </summary>
    /// <param name="__result">Set to true when the skin is a custom skin, otherwise false.</param>
    /// <param name="skinType">The skin to check.</param>
    /// <returns>
    /// False when the skin ID is <see cref="Plugin.AUTOIDX_STARTS_FROM"/> or more and the skin is not <c>SkinType.Test</c>.
    /// Then the original method does not run. True for all other skins, so the original method runs.
    /// </returns>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(PurchaseManager), nameof(PurchaseManager.IsSkinUnlocked))]
    private static bool PurchaseManager_IsSkinUnlockedInternal(ref bool __result, SkinType skinType)
    {
        __result = (int)skinType >= Plugin.AUTOIDX_STARTS_FROM && skinType != SkinType.Test;
        return !__result;
    }

    /// <summary>
    /// Patches <c>PurchaseManager.IsTribeUnlocked</c> as a postfix.
    /// Unlocks all custom tribes.
    /// </summary>
    /// <param name="__result">Set to true when the tribe ID is <see cref="Plugin.AUTOIDX_STARTS_FROM"/> or more. Other tribes keep the original result.</param>
    /// <param name="type">The tribe to check.</param>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(PurchaseManager), nameof(PurchaseManager.IsTribeUnlocked))]
    private static void PurchaseManager_IsTribeUnlocked(ref bool __result, TribeType type)
    {
        __result = (int)type >= Plugin.AUTOIDX_STARTS_FROM || __result;
    }

    /// <summary>
    /// Patches <c>SteamPlatformPurchaseManager.IsProductUnlocked</c> as a prefix.
    /// Gives true for a null product. Other products use the original method.
    /// </summary>
    /// <param name="__result">Set to true when <paramref name="iapProduct"/> is null, otherwise false.</param>
    /// <param name="iapProduct">The product to check.</param>
    /// <returns>False when <paramref name="iapProduct"/> is null, so the original method does not run. Otherwise true.</returns>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(SteamPlatformPurchaseManager), nameof(SteamPlatformPurchaseManager.IsProductUnlocked))]
    private static bool SteamPlatformPurchaseManager_IsProductUnlocked(ref bool __result, IAPProduct iapProduct)
    {
        __result = iapProduct == null;
        return iapProduct != null;
    }

    /// <summary>
    /// Patches <c>PurchaseManager.GetUnlockedTribes</c> as a postfix.
    /// Adds each tribe in <see cref="Registry.customTribes"/> to the list of unlocked tribes.
    /// </summary>
    /// <param name="__result">The list of unlocked tribes. The method adds the custom tribes to it.</param>
    /// <param name="forceUpdate">The value from the original call. The method does not use it.</param>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(PurchaseManager), nameof(PurchaseManager.GetUnlockedTribes))]
    private static void PurchaseManager_GetUnlockedTribes(
        ref Il2CppSystem.Collections.Generic.List<TribeType> __result,
        bool forceUpdate = false
    )
    {
        foreach (var tribe in Registry.customTribes) __result.Add(tribe);
    }

    /// <summary>
    /// Patches <c>IL2CPPUnityLogSource.UnityLogCallback</c> as a prefix.
    /// Stops Unity log messages that contain a text from <see cref="Plugin.LOG_MESSAGES_IGNORE"/>.
    /// </summary>
    /// <param name="logLine">The text of the log message.</param>
    /// <param name="exception">The exception text of the log message. The method does not use it.</param>
    /// <param name="type">The type of the log message. The method does not use it.</param>
    /// <returns>False when the message contains a text to ignore, so the message is not logged. Otherwise true.</returns>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(IL2CPPUnityLogSource), nameof(IL2CPPUnityLogSource.UnityLogCallback))]
    private static bool IL2CPPUnityLogSource_UnityLogCallback(string logLine, string exception, LogType type)
    {
        foreach (string stringToIgnore in Plugin.LOG_MESSAGES_IGNORE)
        {
            if (logLine.Contains(stringToIgnore))
                return false;
        }
        return true;
    }

    /// <summary>
    /// Patches <c>TechView.CreateNode</c> as a prefix.
    /// Replaces the original method with a version that calculates the angle between branches of the tech tree.
    /// </summary>
    /// <remarks>
    /// The original method uses a fixed angle of 72 degrees between branches.
    /// This method divides 360 by the number of techs that the Basic tech unlocks for the tribe of the local player.
    /// The division uses integers, so the angle has no decimal part.
    /// For each tech that <paramref name="data"/> unlocks, the method makes a tech item with the tribe override of the tech.
    /// It calls <c>CreateNode</c> again for each tech that unlocks more techs.
    /// At the end, it calls <c>OnItemsRefreshed</c> if it is set.
    /// </remarks>
    /// <param name="__instance">The tech view that makes the tree.</param>
    /// <param name="data">The tech whose unlocked techs become the new nodes.</param>
    /// <param name="parentItem">The tech item of <paramref name="data"/>, or null for the root of the tree.</param>
    /// <param name="angle">The angle of the parent item, in degrees.</param>
    /// <returns>Always false, so the original method does not run.</returns>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(TechView), nameof(TechView.CreateNode))]
    public static bool TechView_CreateNode(TechView __instance, TechData data, TechItem parentItem, float angle)
    {
        // This patch is a reimplementation of the original CreateNode method to fix layout issues with custom techs.
        // It recalculates the angles for each node to ensure they are displayed correctly.
        GameLogicData gameLogicData = GameManager.GameState.GameLogicData;
        TribeData tribeData = gameLogicData.GetTribeData(GameManager.LocalPlayer.tribe);
        float baseAngle = 360 / gameLogicData.GetOverride(gameLogicData.GetTechData(TechData.Type.Basic), tribeData).techUnlocks.Count;
        float childAngle = 0f;
        if (parentItem != null)
            childAngle = angle + baseAngle * (data.techUnlocks.Count - 1) / 2f;
        foreach (var techData in data.techUnlocks)
        {
            if (gameLogicData.TryGetData(techData.type, out TechData techData2))
            {
                TechData @override = gameLogicData.GetOverride(techData, tribeData);
                TechItem techItem = __instance.CreateTechItem(@override, parentItem, childAngle);
                __instance.currTechIdx++;
                if (@override.techUnlocks != null && @override.techUnlocks.Count > 0)
                    __instance.CreateNode(@override, techItem, childAngle);
                childAngle -= baseAngle;
            }
        }
        Il2CppSystem.Action<TechView> onItemsRefreshed = __instance.OnItemsRefreshed;
        if (onItemsRefreshed == null)
        {
            return false;
        }
        onItemsRefreshed.Invoke(__instance);
        return false;
    }

    /// <summary>
    /// Patches <c>EmbarkAction.Execute</c> as a prefix.
    /// Sets <see cref="currentlyEmbarking"/> to true.
    /// </summary>
    /// <param name="__instance">The embark action. The method does not use it.</param>
    /// <param name="gameState">The game state. The method does not use it.</param>
    /// <returns>Always true, so the original method runs.</returns>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(EmbarkAction), nameof(EmbarkAction.Execute))]
    private static bool EmbarkAction_Execute_Prefix(EmbarkAction __instance, GameState gameState)
    {
        currentlyEmbarking = true;
        return true;
    }

    /// <summary>
    /// Patches <c>ActionUtils.TrainUnit</c> as a prefix.
    /// Changes the trained unit to the embark override during an embark action.
    /// </summary>
    /// <remarks>
    /// The method does nothing if the tile or the unit on the tile is null.
    /// If <see cref="currentlyEmbarking"/> is true, it finds the type of the unit on the tile in <see cref="embarkOverrides"/>.
    /// If the type is there, it replaces <paramref name="unitData"/> with the data of the override type.
    /// Then it sets <see cref="currentlyEmbarking"/> to false.
    /// </remarks>
    /// <param name="__result">The unit state that the original method gives. The method does not change it.</param>
    /// <param name="gameState">The game state.</param>
    /// <param name="playerState">The player that trains the unit. The method does not use it.</param>
    /// <param name="tile">The tile of the unit that embarks.</param>
    /// <param name="unitData">The data of the unit to train. The method can replace it.</param>
    /// <returns>Always true, so the original method runs.</returns>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(ActionUtils), nameof(ActionUtils.TrainUnit))]
    private static bool ActionUtils_TrainUnit(ref UnitState __result, GameState gameState, PlayerState playerState, TileData tile, ref UnitData unitData)
    {
        if (tile == null || tile.unit == null)
        {
            return true;
        }

        if (currentlyEmbarking)
        {
            if (embarkOverrides.TryGetValue(tile.unit.type, out UnitData.Type newType))
            {
                gameState.GameLogicData.TryGetData(newType, out unitData);
            }
            currentlyEmbarking = false;
        }
        return true;
    }

    /// <summary>
    /// Patches <c>StartTurnAction.Execute</c> as a postfix.
    /// Replaces the resource actions of the <c>Attract</c> improvement ability with actions that use the PolyMod attract overrides.
    /// </summary>
    /// <remarks>
    /// First, the method removes all <c>CreateResource</c> actions from the action stack.
    /// Then it finds each tile of the player that has an improvement with the <c>Attract</c> ability.
    /// The improvement must have an age that is a multiple of its growth rate, and the turn must be more than 0.
    /// For each such tile, the method finds the first tile in the area with radius 1 that has no improvement and no resource.
    /// That tile must also belong to the player and have the target terrain.
    /// On that tile, the method adds a <c>CreateResourceAction</c> with the reason <c>Attract</c>.
    /// <para>
    /// The resource comes from <see cref="attractsResourceOverrides"/>, and the default is Game.
    /// The target terrain comes from <see cref="attractsTerrainOverrides"/>, and the default is Forest.
    /// </para>
    /// </remarks>
    /// <param name="__instance">The start turn action. It gives the ID of the player.</param>
    /// <param name="state">The game state.</param>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(StartTurnAction), nameof(StartTurnAction.Execute))]
    private static void StartTurnAction_Execute(StartTurnAction __instance, GameState state)
    {
        // Clear any existing CreateResource actions to prevent duplicates.
        for (int i = state.ActionStack.Count - 1; i >= 0; i--)
        {
            if (state.ActionStack[i].GetActionType() == ActionType.CreateResource)
            {
                state.ActionStack.RemoveAt(i);
            }
        }

        // Iterate through all tiles owned by the current player.
        for (int i = 0; i < state.Map.Tiles.Length; i++)
        {
            TileData tileData = state.Map.Tiles[i];
            if (tileData.owner == __instance.PlayerId && tileData.improvement != null && state.CurrentTurn > 0U)
            {
                ImprovementData improvementData;
                state.GameLogicData.TryGetData(tileData.improvement.type, out improvementData);
                if (improvementData != null)
                {
                    // If the improvement has the 'Attract' ability and is ready to spawn a resource.
                    if (improvementData.HasAbility(ImprovementAbility.Type.Attract) && tileData.improvement.GetAge(state) % improvementData.growthRate == 0)
                    {
                        ResourceData.Type resourceType = ResourceData.Type.Game;
                        if (attractsResourceOverrides.TryGetValue(tileData.improvement.type, out ResourceData.Type newType))
                        {
                            resourceType = newType;
                        }
                        Polytopia.Data.TerrainData.Type targetTerrain = Polytopia.Data.TerrainData.Type.Forest;
                        if (attractsTerrainOverrides.TryGetValue(tileData.improvement.type, out Polytopia.Data.TerrainData.Type newTerrain))
                        {
                            targetTerrain = newTerrain;
                        }

                        // Find a valid tile to spawn the resource on.
                        foreach (TileData tileData2 in state.Map.GetArea(tileData.coordinates, 1, true, false))
                        {
                            if (tileData2.owner == __instance.PlayerId && tileData2.improvement == null && tileData2.resource == null && tileData2.terrain == targetTerrain)
                            {
                                state.ActionStack.Add(new CreateResourceAction(__instance.PlayerId, resourceType, tileData2.coordinates, CreateResourceAction.CreateReason.Attract));
                                break;
                            }
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Patches <c>GamePlayerSummary.Serialize</c> as a prefix.
    /// Sets the tribe to <c>TribeType.None</c> when the tribe ID is 255 or more.
    /// </summary>
    /// <remarks>
    /// The summary writes the tribe as one byte, and a custom tribe ID does not fit in one byte.
    /// The original method then writes <c>TribeType.None</c>.
    /// Because of this, game summaries in the menu do not show the icon of a custom tribe.
    /// </remarks>
    /// <param name="__instance">The player summary to write.</param>
    /// <returns>Always true, so the original method runs.</returns>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(GamePlayerSummary), nameof(GamePlayerSummary.Serialize))]
    private static bool GamePlayerSummary_Serialize(GamePlayerSummary __instance)
    {
        if ((int)__instance.TribeType >= byte.MaxValue)
        {
            __instance.TribeType = TribeType.None;
        }

        return true;
    }

    /// <summary>
    /// Patches <c>PlayerState.Deserialize</c> as a postfix.
    /// Sets the climate of a player with a custom tribe to the tribe value.
    /// </summary>
    /// <remarks>
    /// The player state reads the climate as one byte, and a custom climate ID does not fit in one byte.
    /// The method sets the climate when the tribe ID is <see cref="Plugin.AUTOIDX_STARTS_FROM"/> or more.
    /// </remarks>
    /// <param name="__instance">The player state that the original method read.</param>
    /// <param name="reader">The reader of the data. The method does not use it.</param>
    /// <param name="version">The version of the data. The method does not use it.</param>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(PlayerState), nameof(PlayerState.Deserialize))]
    private static void PlayerState_Deserialize(PlayerState __instance, Il2CppSystem.IO.BinaryReader reader, int version)
    {
        if ((int)__instance.tribe >= Plugin.AUTOIDX_STARTS_FROM)
        {
            __instance.climate = __instance.tribe;
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(AchievementManager), nameof(AchievementManager.CheckAchievements))]
    [HarmonyPatch(typeof(AchievementManager), nameof(AchievementManager.CheckHoudini))]
    private static bool AchievementManager_CheckAchievementsAndHoudini()
    {
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(AchievementManager), nameof(AchievementManager.IsAchievementValid))]
    private static bool IsAchievementValid(ref bool __result, Achievement achievement, GameState gameState)
    {
        __result = false;
        return false;
    }

    /// <summary>
    /// Applies the Harmony patches of this class, then registers and loads the mods.
    /// </summary>
    /// <remarks>
    /// First, the method makes the <see cref="Plugin.PERSISTENT_DATA_PATH"/> folder if it does not exist.
    /// The method adds the game itself as the mod <c>polytopia</c> with the status <c>Success</c>.
    /// Then it calls <see cref="Loader.RegisterMods"/> and <see cref="Loader.LoadMods"/>.
    /// It keeps the dependency cycle result in <see cref="dependencyCycle"/>.
    /// The method measures its time with <see cref="stopwatch"/>.
    /// </remarks>
    internal static void Init()
    {
        stopwatch.Start();
        Directory.CreateDirectory(Plugin.PERSISTENT_DATA_PATH);
        Harmony.CreateAndPatchAll(typeof(Main));
        Mod.Manifest polytopia = new(
            "polytopia",
            "The Battle of Polytopia",
            null,
            new(Application.version.ToString()),
            polytopiaAuthors,
            Array.Empty<Mod.Dependency>()
        );
        Registry.mods.Add(polytopia.id, new(polytopia, Mod.Status.Success, new()));
        Loader.RegisterMods(Registry.mods);
        Loader.LoadMods(out var cycle);
        dependencyCycle = cycle;
        stopwatch.Stop();
    }

    /// <summary>
    /// Patches the getter of <c>Application.persistentDataPath</c> as a postfix.
    /// Replaces the path with <see cref="Plugin.PERSISTENT_DATA_PATH"/>.
    /// </summary>
    /// <param name="__result">Set to <see cref="Plugin.PERSISTENT_DATA_PATH"/>.</param>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(UnityEngine.Application), nameof(UnityEngine.Application.persistentDataPath), MethodType.Getter)]
    private static void Application_persistentDataPath_get(ref string __result)
    {
        __result = Plugin.PERSISTENT_DATA_PATH;
    }

    /// <summary>
    /// Loads the content of all mods that have the status <c>Success</c>.
    /// </summary>
    /// <remarks>
    /// First, the method loads the PolyMod localization. If <see cref="dependencyCycle"/> is true, it stops there.
    /// Then it reads the files of each mod and uses the file name or the file extension to select the action:
    /// <list type="bullet">
    /// <item><description><c>localization.json</c> and <c>localization_*.json</c>: calls <see cref="Loader.LoadLocalizationFile"/>.</description></item>
    /// <item><description><c>patch.json</c> and <c>patch_*.json</c>: reads the file as JSON and merges it into <paramref name="json"/> with <see cref="Loader.LoadGameLogicDataPatch"/>.</description></item>
    /// <item><description><c>prefabs</c>, <c>unitPrefab</c>, <c>improvementPrefab</c>, and <c>resourcePrefab</c> JSON files: adds the file to <see cref="Registry.filesQueueToBeProcessedAfterGLDMerge"/>.</description></item>
    /// <item><description>Files with the extension <c>.png</c>: calls <see cref="Loader.LoadTextureFile"/>.</description></item>
    /// <item><description>Files with the extension <c>.bundle</c>: calls <see cref="Loader.LoadAssetBundle"/>.</description></item>
    /// </list>
    /// The method stops reading the files of a mod when the status of the mod is not <c>Success</c>.
    /// It does not load <c>.wav</c> files.
    /// <para>
    /// After all mods, the method fills <c>TechItem.techTierFirebaseId</c> with IDs for the tiers from 0 to <see cref="MAX_TECH_TIER"/>.
    /// Then it calls <see cref="Loader.ProcessGameLogicData"/>, <see cref="Loader.ProcessFilesQueue"/>, <see cref="Visual.CreateSprites"/>, and <see cref="Prefabs.CreatePrefabs"/>.
    /// Last, it writes the load time to the log.
    /// </para>
    /// </remarks>
    /// <param name="gameLogicData">The game logic data that the game parses.</param>
    /// <param name="json">The root JSON object of the GLD. The method merges the mod patches into it.</param>
    internal static void Load(GameLogicData gameLogicData, JObject json)
    {
        stopwatch.Start();
        Loc.BuildAndLoadLocalization(
            JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(
                Plugin.GetResource("localization.json")
            )!
        );
        if (dependencyCycle) return;

        foreach (var (id, mod) in Registry.mods)
        {
            if (mod.status != Mod.Status.Success) continue;
            foreach (var file in mod.files)
            {
                if (mod.status != Mod.Status.Success) break;
                Match localizationMatch = Regex.Match(Path.GetFileName(file.name), @"^localization(_.*)?\.json$");
                if (localizationMatch.Success)
                {
                    Loader.LoadLocalizationFile(mod, file);
                    continue;
                }
                Match patchMatch = Regex.Match(Path.GetFileName(file.name), @"^patch(_.*)?\.json$");
                if (patchMatch.Success)
                {
                    var patchText = new StreamReader(new MemoryStream(file.bytes)).ReadToEnd();
                    // var template = new Api.GldConfigTemplate(patchText, mod.id);
                    // var text = template.Render();
                    // if (text is null)
                    // {
                    //     mod.status = Mod.Status.Error;
                    //     continue;
                    // }
                    Loader.LoadGameLogicDataPatch(
                        mod,
                        json,
                        JObject.Parse(patchText)
                    );
                    continue;
                }

                Match prefabsMatch = Regex.Match(Path.GetFileName(file.name), @"^prefabs(?:_(.*))?\.json$");
                if (prefabsMatch.Success)
                {
                    Registry.filesQueueToBeProcessedAfterGLDMerge.Add((mod, file));
                    continue;
                }

                Match unitPrefabMatch = Regex.Match(Path.GetFileName(file.name), @"^unitPrefab(?:_(.*))?\.json$");
                if (unitPrefabMatch.Success)
                {
                    Registry.filesQueueToBeProcessedAfterGLDMerge.Add((mod, file));
                    continue;
                }

                Match improvementPrefabMatch = Regex.Match(Path.GetFileName(file.name), @"^improvementPrefab(?:_(.*))?\.json$");
                if (improvementPrefabMatch.Success)
                {
                    Registry.filesQueueToBeProcessedAfterGLDMerge.Add((mod, file));
                    continue;
                }

                Match resourcePrefabMatch = Regex.Match(Path.GetFileName(file.name), @"^resourcePrefab(?:_(.*))?\.json$");
                if (resourcePrefabMatch.Success)
                {
                    Registry.filesQueueToBeProcessedAfterGLDMerge.Add((mod, file));
                    continue;
                }

                switch (Path.GetExtension(file.name))
                {
                    case ".png":
                        Loader.LoadTextureFile(mod, file);
                        break;
                    // case ".wav":
                    //     Loader.LoadAudioFile(mod, file);
                    //     break;
                    case ".bundle":
                        Loader.LoadAssetBundle(mod, file);
                        break;
                }
            }
        }
        TechItem.techTierFirebaseId.Clear();
        for (int i = 0; i <= MAX_TECH_TIER; i++)
        {
            TechItem.techTierFirebaseId.Add($"tech_research_{i}");
        }
        Loader.ProcessGameLogicData(gameLogicData, json);
        Loader.ProcessFilesQueue();
        Visual.CreateSprites();
        Prefabs.CreatePrefabs();
        stopwatch.Stop();
        Plugin.logger.LogInfo($"Loaded all mods in {stopwatch.ElapsedMilliseconds}ms");
    }
}
