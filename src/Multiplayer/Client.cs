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
    [HarmonyPatch(typeof(MultiplayerSelectionScreen), nameof(MultiplayerSelectionScreen.Show))]
    public static void MultiplayerScreen_Show(MultiplayerSelectionScreen __instance)
    {
        __instance.TournamentsButton.gameObject.SetActive(false);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(StartScreen_UI2), nameof(StartScreen_UI2.RunLayout))]
    private static void StartScreen_UI2_RunLayout(StartScreen_UI2 __instance)
    {
        __instance.highscoreButton.gameObject.SetActive(false);
        __instance.weeklyChallengeButton.gameObject.SetActive(false);
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


    [HarmonyPrefix]
    [HarmonyPatch(typeof(ClientBase), nameof(ClientBase.SendCommand))]
    private static bool ClientBase_SendCommand(
        ClientBase __instance,
        CommandBase command)
    {

        Plugin.logger.LogInfo("Multiplayer> ClientBase_SendCommand");
        Il2CppSystem.Threading.Tasks.Task<ServerResponse<BoolResponseViewModel>> task = new();
        var taskCompletionSource = new Il2CppSystem.Threading.Tasks.TaskCompletionSource<ServerResponse<BoolResponseViewModel>>();

        _ = ClientBase_SendCommand_Async(taskCompletionSource, __instance, command);

        task = taskCompletionSource.Task;

        return false;
    }

    private static async System.Threading.Tasks.Task ClientBase_SendCommand_Async(
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


            client.GameState.TryGetPlayer(client.GameState.CurrentPlayer, out PlayerState playerState);
            var currentPlayerId = "";
            if(playerState.AccountId.HasValue)
            {
                currentPlayerId = playerState.AccountId.Value.ToString();
            }
            var setupGameDataViewModel = new ModdedGameStateViewModel
            {
                gameId = client.gameId.ToString(),
                serializedGameState = serializedGameState,
                serializedGameSummary = serializedGameSummary,
                gameSettingsJson = "",
                currentPlayerId = currentPlayerId,
                IsEndTurnCommand = command.GetCommandType() == CommandType.EndTurn
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

        _ = BackendAdapter_StartLobbyGame_Async(taskCompletionSource, __instance, model);

        __result = taskCompletionSource.Task;

        return false;
    }

    private static async System.Threading.Tasks.Task BackendAdapter_StartLobbyGame_Async(
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
            var setupGameDataViewModel = new ModdedGameStateViewModel
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
                tribeMix = TribeType.None, // TribeMix is byte too
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

    // FIX FOR NATURE PLAYER. BOTS ARENT IMPLEMENTED YET

    [HarmonyPrefix]
    [HarmonyPatch(typeof(GameState), nameof(GameState.EndPlayerTurn))]
	private static bool GameState_EndPlayerTurn(GameState __instance, bool newTurn = false)
	{
        Console.Write("GameState_EndPlayerTurn");
		__instance.CurrentPlayerIndex++;
		if (__instance.CurrentPlayerIndex >=__instance. PlayerStates.Count)
		{
			__instance.CurrentPlayerIndex = 0;
			newTurn = true;
		}

        var currentPlayer = __instance.PlayerStates[__instance.CurrentPlayerIndex];
		if (!currentPlayer.IsAlive(__instance))
		{
			__instance.EndPlayerTurn(newTurn);
		}
		else if (newTurn)
		{
			__instance.CurrentTurn++;
		}

        if(currentPlayer.AutoPlay)
        {
            __instance.CommandStack.Add(new EndTurnCommand(currentPlayer.Id));
        }
        Console.Write("finished");
        return false;
	}
}
