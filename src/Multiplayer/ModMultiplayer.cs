using HarmonyLib;
using Il2CppMicrosoft.AspNetCore.SignalR.Client;
using Newtonsoft.Json;
using PolyMod.Managers;
using PolyMod.Multiplayer.ViewModels;
using Polytopia.Data;
using PolytopiaBackendBase;
using PolytopiaBackendBase.Common;
using PolytopiaBackendBase.Game;
using PolytopiaBackendBase.Game.BindingModels;
using UnityEngine;

namespace PolyMod.Multiplayer;

public class ModMultiplayer
{
    internal static void Init()
    {
        if (Compatibility.IsClientOnly())
        {
            Plugin.logger?.LogInfo($"All loaded mods are client only. Skipping modded multiplayer initialization.");

            return;
        }

        Plugin.logger?.LogInfo($"Starting modded multiplayer initialization.");

        Harmony.CreateAndPatchAll(typeof(ModMultiplayer));


        Plugin.logger?.LogInfo($"Finished modded multiplayer initialization.");
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(BackendAdapter), nameof(BackendAdapter.StartLobbyGame))]
    private static bool BackendAdapter_StartLobbyGame_Modded(
        ref Il2CppSystem.Threading.Tasks.Task<ServerResponse<LobbyGameViewModel>> __result,
        BackendAdapter __instance,
        StartLobbyBindingModel model)
    {
        // On Android, let the game's original StartLobbyGame handle it
        if (Application.platform == RuntimePlatform.Android) return true;

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

            Plugin.logger.LogInfo("Multiplayer> GameState and Settings created");

            var setupGameDataViewModel = new SetupGameDataViewModel
            {
                lobbyId = lobbyGameViewModel.Id.ToString(),
                serializedGameState = serializedGameState,
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