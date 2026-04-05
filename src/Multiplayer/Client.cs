using HarmonyLib;
using Il2CppMicrosoft.AspNetCore.SignalR.Client;
using PolyMod.Multiplayer.ViewModels;
using Polytopia.Data;
using PolytopiaBackendBase;
using PolytopiaBackendBase.Common;
using PolytopiaBackendBase.Game;
using PolytopiaBackendBase.Game.BindingModels;
using UnityEngine;
using Newtonsoft.Json;

namespace PolyMod.Multiplayer;

public static class Client
{
    internal const string DEFAULT_SERVER_URL = "https://dev.polydystopia.xyz";
    internal const string LOCAL_SERVER_URL = "http://localhost:5051/";
    private const string GldMarker = "##GLD:";
    internal static bool allowGldMods = false;

    // Cache parsed GLD by game Seed to handle rewinds/reloads
    private static readonly Dictionary<int, GameLogicData> _gldCache = new();
    private static readonly Dictionary<int, int> _versionCache = new(); // Seed -> modGldVersion

    internal static void Init()
    {
        Harmony.CreateAndPatchAll(typeof(Client));
        BuildConfig buildConfig = BuildConfigHelper.GetSelectedBuildConfig();
        buildConfig.buildServerURL = BuildServerURL.Custom;
        buildConfig.customServerURL = LOCAL_SERVER_URL;

        Plugin.logger.LogInfo($"Multiplayer> Server URL set to: {Plugin.config.backendUrl}");
        Plugin.logger.LogInfo("Multiplayer> GLD patches applied");
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(MultiplayerScreen), nameof(MultiplayerScreen.Show))]
    public static void MultiplayerScreen_Show(MultiplayerScreen __instance)
    {
        __instance.multiplayerSelectionScreen.TournamentsButton.gameObject.SetActive(false);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(StartScreen), nameof(StartScreen.Start))]
    private static void StartScreen_Start(StartScreen __instance)
    {
        __instance.highscoreButton.gameObject.SetActive(false);
        __instance.weeklyChallengesButton.gameObject.SetActive(false);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(SystemInfo), nameof(SystemInfo.deviceUniqueIdentifier), MethodType.Getter)]
    public static void SteamClient_get_SteamId(ref string  __result)
    {
        if (Plugin.config.overrideDeviceId != string.Empty)
        {
            __result = Plugin.config.overrideDeviceId;
        }
    }


    // /// <summary>
    // /// After GameState deserialization, check for trailing GLD version ID and set mockedGameLogicData.
    // /// The server appends "##GLD:" + modGldVersion (int) after the normal serialized data.
    // /// </summary>
    // [HarmonyPostfix]
    // [HarmonyPatch(typeof(GameState), nameof(GameState.Deserialize))]
    // private static void Deserialize_Postfix(GameState __instance, BinaryReader __0)
    // {
    //     if(!allowGldMods) return;

    //     Plugin.logger?.LogDebug("Deserialize_Postfix: Entered");

    //     try
    //     {
    //         var reader = __0;
    //         if (reader == null)
    //         {
    //             Plugin.logger?.LogWarning("Deserialize_Postfix: reader is null");
    //             return;
    //         }

    //         var position = reader.BaseStream.Position;
    //         var length = reader.BaseStream.Length;
    //         var remaining = length - position;

    //         Plugin.logger?.LogDebug($"Deserialize_Postfix: Stream position={position}, length={length}, remaining={remaining}");

    //         // Check if there's more data after normal deserialization
    //         if (position >= length)
    //         {
    //             Plugin.logger?.LogDebug("Deserialize_Postfix: No trailing data (position >= length)");

    //             var sd = __instance.Seed;
    //             if (_gldCache.TryGetValue(sd, out var cachedGld))
    //             {
    //                 __instance.mockedGameLogicData = cachedGld;
    //                 var cachedVersion = _versionCache.GetValueOrDefault(sd, -1);
    //                 Plugin.logger?.LogInfo($"Deserialize_Postfix: Applied cached GLD for Seed={sd}, ModGldVersion={cachedVersion}");
    //             }
    //             return;
    //         }

    //         Plugin.logger?.LogDebug($"Deserialize_Postfix: Found {remaining} bytes of trailing data, attempting to read marker");

    //         var marker = reader.ReadString();
    //         Plugin.logger?.LogDebug($"Deserialize_Postfix: Read marker string: '{marker}'");

    //         if (marker != GldMarker)
    //         {
    //             Plugin.logger?.LogDebug($"Deserialize_Postfix: Marker mismatch - expected '{GldMarker}', got '{marker}'");
    //             return;
    //         }

    //         Plugin.logger?.LogInfo($"Deserialize_Postfix: Found GLD marker '{GldMarker}'");

    //         var modGldVersion = reader.ReadInt32();
    //         Plugin.logger?.LogInfo($"Deserialize_Postfix: Found embedded ModGldVersion: {modGldVersion}");

    //         Plugin.logger?.LogDebug($"Deserialize_Postfix: Fetching GLD from server for version {modGldVersion}");
    //         var gldJson = FetchGldById(modGldVersion);
    //         if (string.IsNullOrEmpty(gldJson))
    //         {
    //             Plugin.logger?.LogError($"Deserialize_Postfix: Failed to fetch GLD for ModGldVersion: {modGldVersion}");
    //             return;
    //         }

    //         Plugin.logger?.LogDebug($"Deserialize_Postfix: Parsing GLD JSON ({gldJson.Length} chars)");

    //         var customGld = new GameLogicData();
    //         customGld.Parse(gldJson);
    //         __instance.mockedGameLogicData = customGld;

    //         // Cache for subsequent deserializations (rewinds, reloads)
    //         var seed = __instance.Seed;
    //         _gldCache[seed] = customGld;
    //         _versionCache[seed] = modGldVersion;

    //         Plugin.logger?.LogInfo($"Deserialize_Postfix: Successfully set mockedGameLogicData from ModGldVersion: {modGldVersion}, cached for Seed={seed}");
    //     }
    //     catch (EndOfStreamException)
    //     {
    //         Plugin.logger?.LogDebug("Deserialize_Postfix: EndOfStreamException - no trailing data");
    //     }
    //     catch (Exception ex)
    //     {
    //         Plugin.logger?.LogError($"Deserialize_Postfix: Exception: {ex.GetType().Name}: {ex.Message}");
    //         Plugin.logger?.LogDebug($"Deserialize_Postfix: Stack trace: {ex.StackTrace}");
    //     }
    // }

    // /// <summary>
    // /// Fetch GLD from server using ModGldVersion ID
    // /// </summary>
    // private static string? FetchGldById(int modGldVersion)
    // {
    //     if(!allowGldMods) return null;
    //     try
    //     {
    //         using var client = new HttpClient();
    //         var url = $"{Plugin.config.backendUrl.TrimEnd('/')}/api/mods/gld/{modGldVersion}";
    //         Plugin.logger?.LogDebug($"FetchGldById: Requesting URL: {url}");

    //         var response = client.GetAsync(url).Result;
    //         Plugin.logger?.LogDebug($"FetchGldById: Response status: {response.StatusCode}");

    //         if (response.IsSuccessStatusCode)
    //         {
    //             var gld = response.Content.ReadAsStringAsync().Result;
    //             Plugin.logger?.LogInfo($"FetchGldById: Successfully fetched mod GLD ({gld.Length} chars)");
    //             return gld;
    //         }

    //         var errorContent = response.Content.ReadAsStringAsync().Result;
    //         Plugin.logger?.LogError($"FetchGldById: Failed with status {response.StatusCode}: {errorContent}");
    //     }
    //     catch (Exception ex)
    //     {
    //         Plugin.logger?.LogError($"FetchGldById: Exception: {ex.GetType().Name}: {ex.Message}");
    //         if (ex.InnerException != null)
    //         {
    //             Plugin.logger?.LogError($"FetchGldById: Inner exception: {ex.InnerException.Message}");
    //         }
    //     }
    //     return null;
    // }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(ClientBase), nameof(ClientBase.SendCommand))]
    private static bool ClientBase_SendCommand(
        ClientBase __instance,
        CommandBase command)
    {

        Plugin.logger.LogInfo("Multiplayer> ClientBase_SendCommand");
        Il2CppSystem.Threading.Tasks.Task<ServerResponse<BoolResponseViewModel>> task = new();
        var taskCompletionSource = new Il2CppSystem.Threading.Tasks.TaskCompletionSource<ServerResponse<BoolResponseViewModel>>();

        _ = HandleSendCommandModded(taskCompletionSource, __instance, command);

        task = taskCompletionSource.Task;

        return false;
    }

    private static async System.Threading.Tasks.Task HandleSendCommandModded(
        Il2CppSystem.Threading.Tasks.TaskCompletionSource<ServerResponse<BoolResponseViewModel>> tcs,
        ClientBase client,
        CommandBase command)
    {
        try
        {
            if (!client.CurrentGameId.HasValue)
            {
                Console.Write("Tried to perform and send command but no GameId was set");
                return;
            }
            if (!ClientActionManager.CanReceiveCommand(command, client.GameState))
            {
                Console.Write("Tried to send invalid command");
                return;
            }
            uint currentResetId = client.resets;
            int count = client.GameState.CommandStack.Count;
            var list = new Il2CppSystem.Collections.Generic.List<CommandBase>();
            list.Add(command);
            client.ActionManager.ExecuteCommands(list);
            await client.SendCommandToServer(command, count);

            var serializedGameState = SerializationHelpers.ToByteArray(client.GameState, client.GameState.Version);

            var succ = GameStateSummary.FromGameStateByteArray(serializedGameState,
                out GameStateSummary stateSummary, out var gameState);

            var serializedGameSummary = SerializationHelpers.ToByteArray(stateSummary, gameState.Version);

            var setupGameDataViewModel = new SetupGameStateViewModel
            {
                gameId = client.gameId.ToString(),
                serializedGameState = serializedGameState,
                serializedGameSummary = serializedGameSummary,
                gameSettingsJson = ""
            };

            var setupData = System.Text.Json.JsonSerializer.Serialize(setupGameDataViewModel);

            var serverResponse = await PolytopiaBackendAdapter.Instance.HubConnection.InvokeAsync<ServerResponse<BoolResponseViewModel>>(
                "UpdateGameStateModded",
                setupData,
                Il2CppSystem.Threading.CancellationToken.None
            );
            tcs.SetResult(serverResponse);
        }
        catch (Exception ex)
        {
            Plugin.logger.LogError("Multiplayer> Error during HandleSendCommandModded: " + ex.Message);
            tcs.SetException(new Il2CppSystem.Exception(ex.Message));
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(BackendAdapter), nameof(BackendAdapter.StartLobbyGame))]
    private static bool BackendAdapter_StartLobbyGame_Modded(
        ref Il2CppSystem.Threading.Tasks.Task<ServerResponse<LobbyGameViewModel>> __result,
        BackendAdapter __instance,
        StartLobbyBindingModel model)
    {
        Plugin.logger.LogInfo("Multiplayer> BackendAdapter_StartLobbyGame_Modded");
        var taskCompletionSource = new Il2CppSystem.Threading.Tasks.TaskCompletionSource<ServerResponse<LobbyGameViewModel>>();

        _ = HandleStartLobbyGameModded(taskCompletionSource, __instance, model);

        __result = taskCompletionSource.Task;

        return false;
    }

    private static async System.Threading.Tasks.Task HandleStartLobbyGameModded(
        Il2CppSystem.Threading.Tasks.TaskCompletionSource<ServerResponse<LobbyGameViewModel>> tcs,
        BackendAdapter instance,
        StartLobbyBindingModel model)
    {
        try
        {
            var lobbyResponse = await PolytopiaBackendAdapter.Instance.GetLobby(new GetLobbyBindingModel
            {
                LobbyId = model.LobbyId
            });

            Plugin.logger.LogInfo($"Multiplayer> Lobby processed {lobbyResponse.Success}");
            LobbyGameViewModel lobbyGameViewModel = lobbyResponse.Data;
            Plugin.logger.LogInfo("Multiplayer> Lobby received");

            (byte[] serializedGameState, string gameSettingsJson) = CreateMultiplayerGame(
                lobbyGameViewModel,
                VersionManager.GameVersion,
                VersionManager.GameLogicDataVersion
            );

            Plugin.logger.LogInfo("Multiplayer> GameState and Settiings created");

            var succ = GameStateSummary.FromGameStateByteArray(serializedGameState,
                out GameStateSummary stateSummary, out var gameState);

            var serializedGameSummary = SerializationHelpers.ToByteArray(stateSummary, gameState.Version);
            var setupGameDataViewModel = new SetupGameStateViewModel
            {
                lobbyId = lobbyGameViewModel.Id.ToString(),
                serializedGameState = serializedGameState,
                serializedGameSummary = serializedGameSummary,
                gameSettingsJson = gameSettingsJson
            };

            var setupData = System.Text.Json.JsonSerializer.Serialize(setupGameDataViewModel);

            var serverResponse = await instance.HubConnection.InvokeAsync<ServerResponse<LobbyGameViewModel>>(
                "StartLobbyGameModded",
                setupData,
                Il2CppSystem.Threading.CancellationToken.None
            );
            Plugin.logger.LogInfo("Multiplayer> Invoked StartLobbyGameModded");
            tcs.SetResult(serverResponse);
        }
        catch (Exception ex)
        {
            Plugin.logger.LogError("Multiplayer> Error during HandleStartLobbyGameModded: " + ex.Message);
            tcs.SetException(new Il2CppSystem.Exception(ex.Message));
        }
    }

    public static (byte[] serializedGameState, string gameSettingsJson) CreateMultiplayerGame(LobbyGameViewModel lobby,
        int gameVersion, int gameLogicVersion)
    {
        var lobbyMapSize = lobby.MapSize;
        var settings = new GameSettings();
        settings.ApplyLobbySettings(lobby);
        if (settings.LiveGamePreset)
        {
            settings.SetLiveModePreset();
        }
        foreach (var participatorViewModel in lobby.Participators)
        {
            var humanPlayer = new PlayerData
            {
                type = PlayerDataType.LocalUser,
                state = PlayerDataFriendshipState.Accepted,
                knownTribe = true,
                tribe = (TribeType)participatorViewModel.SelectedTribe,
                tribeMix = (TribeType)participatorViewModel.SelectedTribe,
                skinType = (SkinType)participatorViewModel.SelectedTribeSkin,
                defaultName = participatorViewModel.GetNameInternal()
            };
            humanPlayer.profile.id = participatorViewModel.UserId;
            humanPlayer.profile.SetName(participatorViewModel.GetNameInternal());
            SerializationHelpers.FromByteArray<AvatarState>(participatorViewModel.AvatarStateData, out var avatarState);
            humanPlayer.profile.avatarState = avatarState;

            settings.AddPlayer(humanPlayer);
        }

        foreach (var botDifficulty in lobby.Bots)
        {
            var botGuid = Il2CppSystem.Guid.NewGuid();

            var botPlayer = new PlayerData
            {
                type = PlayerDataType.Bot,
                state = PlayerDataFriendshipState.Accepted,
                knownTribe = true,
                tribe = Enum.GetValues<TribeType>().Where(t => t != TribeType.None)
                    .OrderBy(x => Il2CppSystem.Guid.NewGuid()).First()
            };
            ;
            botPlayer.botDifficulty = (BotDifficulty)botDifficulty;
            botPlayer.skinType = SkinType.Default;
            botPlayer.defaultName = "Bot" + botGuid;
            botPlayer.profile.id = botGuid;

            settings.AddPlayer(botPlayer);
        }

        GameState gameState = new GameState()
        {
            Version = gameVersion,
            Settings = settings,
            PlayerStates = new Il2CppSystem.Collections.Generic.List<PlayerState>()
        };

        for (int index = 0; index < settings.GetPlayerCount(); ++index)
        {
            PlayerData player = settings.GetPlayer(index);
            if (player.type != PlayerDataType.Bot)
            {
                var nullableGuid = new Il2CppSystem.Nullable<Il2CppSystem.Guid>(player.profile.id);
                if (!nullableGuid.HasValue)
                {
                    throw new Exception("GUID was not set properly!");
                }
                PlayerState playerState = new PlayerState()
                {
                    Id = (byte)(index + 1),
                    AccountId = nullableGuid,
                    AutoPlay = player.type == PlayerDataType.Bot,
                    UserName = player.GetNameInternal(),
                    tribe = player.tribe,
                    tribeMix = player.tribeMix,
                    hasChosenTribe = true,
                    skinType = player.skinType
                };
                gameState.PlayerStates.Add(playerState);
                Plugin.logger.LogInfo($"Multiplayer> Created player: {playerState}");
            }
            else
            {
                GameStateUtils.AddAIOpponent(gameState, GameStateUtils.GetRandomPickableTribe(gameState),
                    GameSettings.HandicapFromDifficulty(player.botDifficulty), player.skinType);
            }
        }

        GameStateUtils.SetPlayerColors(gameState);
        GameStateUtils.AddNaturePlayer(gameState);

        Plugin.logger.LogInfo("Multiplayer> Creating world...");

        ushort num = (ushort)Math.Max(lobbyMapSize,
            (int)MapDataExtensions.GetMinimumMapSize(gameState.PlayerCount));
        gameState.Map = new MapData(num, num);
        MapGeneratorSettings generatorSettings = settings.GetMapGeneratorSettings();
        new MapGenerator().Generate(gameState, generatorSettings);

        Plugin.logger.LogInfo($"Multiplayer> Creating initial state for {gameState.PlayerCount} players...");

        foreach (PlayerState player in gameState.PlayerStates)
        {
            foreach (PlayerState otherPlayer in gameState.PlayerStates)
                player.aggressions[otherPlayer.Id] = 0;

            if (player.Id != byte.MaxValue && gameState.GameLogicData.TryGetData(player.tribe, out TribeData tribeData))
            {
                player.Currency = tribeData.startingStars;
                TileData tile = gameState.Map.GetTile(player.startTile);
                UnitState unitState = ActionUtils.TrainUnitScored(gameState, player, tile, tribeData.startingUnit);
                unitState.attacked = false;
                unitState.moved = false;
            }
        }

        Plugin.logger.LogInfo("Multiplayer> Session created successfully");

        gameState.CommandStack.Add((CommandBase)new StartMatchCommand((byte)1));

        var serializedGameState = SerializationHelpers.ToByteArray(gameState, gameState.Version);

        return (serializedGameState,
            JsonConvert.SerializeObject(gameState.Settings));
    }
}
