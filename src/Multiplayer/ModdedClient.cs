using HarmonyLib;
using Il2CppMicrosoft.AspNetCore.SignalR.Client;
using PolyMod.Managers;
using PolyMod.Multiplayer.ViewModels;
using Polytopia.Data;
using PolytopiaBackendBase;
using PolytopiaBackendBase.Game;
using PolytopiaBackendBase.Game.BindingModels;
using UnityEngine;

namespace PolyMod.Multiplayer;

/// <summary>
/// Client-authoritative command flow for modded games.
/// The server never evaluates modded game state. The acting client executes commands (plus auto-play follow-up turns) the way the vanilla server would, on a shadow copy of the authoritative state and uploads the result via UpdateGameStateModded. Other clients receive the commands through the vanilla OnCommand relay.
/// </summary>
public static class ModdedClient
{
    private static readonly HashSet<string> _moddedGameIds = new();
    private static readonly Dictionary<string, byte[]> _shadowStates = new();
    private static readonly SemaphoreSlim _sendLock = new(1, 1);
    private const string EmptyGuid = "00000000-0000-0000-0000-000000000000";

    internal static void Init()
    {
        Harmony.CreateAndPatchAll(typeof(ModdedClient));
    }

    internal static void RegisterModdedGame(string gameId, string? checksum)
    {
        lock (_moddedGameIds)
        {
            _moddedGameIds.Add(gameId.ToLowerInvariant());
        }

        try
        {
            File.WriteAllText(SignaturesPath(gameId), checksum ?? Compatibility.checksum);
        }
        catch (Exception e)
        {
            Plugin.logger.LogWarning($"Multiplayer> Could not write signatures for {gameId}: {e.Message}");
        }
    }

    internal static bool IsModdedGame(string gameId)
    {
        lock (_moddedGameIds)
        {
            if (_moddedGameIds.Contains(gameId.ToLowerInvariant())) return true;
        }

        return File.Exists(SignaturesPath(gameId));
    }

    internal static void SetShadowState(string gameId, byte[] stateBytes)
    {
        lock (_shadowStates)
        {
            _shadowStates[gameId.ToLowerInvariant()] = stateBytes;
        }
    }

    private static byte[]? GetShadowState(string gameId)
    {
        lock (_shadowStates)
        {
            return _shadowStates.TryGetValue(gameId.ToLowerInvariant(), out var bytes) ? bytes : null;
        }
    }

    private static void DropShadowState(string gameId)
    {
        lock (_shadowStates)
        {
            _shadowStates.Remove(gameId.ToLowerInvariant());
        }
    }

    private static string SignaturesPath(string gameId) =>
        Path.Combine(Application.persistentDataPath, $"{gameId}.signatures");

    /// <summary>
    /// Whenever the client subscribes to a game, ask the server whether it is modded so the send path can route accordingly.
    /// Also covers games created/joined on another device.
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(BackendAdapter), nameof(BackendAdapter.SubscribeToGame))]
    private static void BackendAdapter_SubscribeToGame(BackendAdapter __instance, SubscribeToGameBindingModel model)
    {
        _ = FetchModdedGameInfo(__instance, model.GameId);
    }

    private static async System.Threading.Tasks.Task FetchModdedGameInfo(BackendAdapter adapter, Il2CppSystem.Guid gameIdGuid)
    {
        var gameId = gameIdGuid.ToString();
        try
        {
            var json = await adapter.HubConnection.InvokeAsync<string>(
                "GetModdedGameInfo",
                gameId,
                Il2CppSystem.Threading.CancellationToken.None
            );

            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("isModded", out var isModdedProperty) || !isModdedProperty.GetBoolean())
            {
                return;
            }

            string? checksum = root.TryGetProperty("checksum", out var checksumProperty)
                ? checksumProperty.GetString()
                : null;

            RegisterModdedGame(gameId, checksum);
            Plugin.logger.LogInfo($"Multiplayer> Game {gameId} is modded");

            if (checksum != null && checksum != Compatibility.checksum)
            {
                Plugin.logger.LogWarning($"Multiplayer> Mod checksum mismatch for game {gameId}");
                PopupManager.GetBasicPopupWithData(new(
                    Localization.Get("polymod.signature.mismatch"),
                    Localization.Get("polymod.signature.incompatible"),
                    new(new PopupBase.PopupButtonData[] {
                        new("OK")
                    })
                )).Show();
                return;
            }

            if (GetShadowState(gameId) == null)
            {
                await FetchShadowState(gameIdGuid);
            }
        }
        catch (Exception e)
        {
            Plugin.logger.LogWarning($"Multiplayer> Could not fetch modded game info for {gameId}: {e.Message}");
        }
    }

    /// <summary>
    /// Fetches the server's stored authoritative state into the shadow cache.
    /// </summary>
    private static async System.Threading.Tasks.Task<byte[]?> FetchShadowState(Il2CppSystem.Guid gameIdGuid)
    {
        try
        {
            var gameResponse = await PolytopiaBackendAdapter.Instance.JoinGameHttp(new JoinGameBindingModel
            {
                GameId = gameIdGuid
            });

            byte[]? stateBytes = gameResponse?.Data?.CurrentGameStateData ?? gameResponse?.Data?.InitialGameStateData;
            if (stateBytes == null)
            {
                Plugin.logger.LogWarning($"Multiplayer> Could not fetch state for game {gameIdGuid}");
                return null;
            }

            SetShadowState(gameIdGuid.ToString(), stateBytes);
            return stateBytes;
        }
        catch (Exception e)
        {
            Plugin.logger.LogWarning($"Multiplayer> Could not fetch state for game {gameIdGuid}: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// The modded replacement for the vanilla send. T
    /// he client has already executed the command locally, recompute it (plus auto-play follow-ups) on the shadow state and upload the result instead of calling the vanilla SendCommand hub method.
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(BackendAdapter), nameof(BackendAdapter.SendCommand))]
    private static bool BackendAdapter_SendCommand(
        ref Il2CppSystem.Threading.Tasks.Task<ServerResponse<ResponseViewModel>> __result,
        SendCommandBindingModel model)
    {
        if (!IsModdedGame(model.GameId.ToString())) return true;

        var taskCompletionSource =
            new Il2CppSystem.Threading.Tasks.TaskCompletionSource<ServerResponse<ResponseViewModel>>();

        _ = HandleModdedSendCommand(taskCompletionSource, model);

        __result = taskCompletionSource.Task;

        return false;
    }

    private static async System.Threading.Tasks.Task HandleModdedSendCommand(
        Il2CppSystem.Threading.Tasks.TaskCompletionSource<ServerResponse<ResponseViewModel>> tcs,
        SendCommandBindingModel model)
    {
        await _sendLock.WaitAsync();
        try
        {
            var gameIdGuid = model.GameId;
            var gameId = gameIdGuid.ToString();
            int expectedIndex = model.Command.CommandIndex;

            if (!CommandBase.FromByteArray(model.Command.SerializedData, out var command, out _))
            {
                Plugin.logger.LogError("Multiplayer> Could not deserialize own command");
                tcs.SetResult(FailedResponse());
                return;
            }

            var shadow = GetShadowState(gameId);
            if (shadow == null || CountCommands(shadow) != expectedIndex)
            {
                shadow = await FetchShadowState(gameIdGuid);
            }

            if (shadow == null)
            {
                tcs.SetResult(FailedResponse());
                return;
            }

            var (update, followUps, newStateBytes) =
                ComputeModdedUpdate(shadow, gameId, command, null, expectedIndex);
            if (update == null)
            {
                DropShadowState(gameId);
                tcs.SetResult(FailedResponse());
                return;
            }

            var uploaded = await UploadModdedUpdate(update);
            if (!uploaded)
            {
                DropShadowState(gameId);
                tcs.SetResult(FailedResponse());
                return;
            }

            SetShadowState(gameId, newStateBytes!);
            FeedFollowUpsToLiveClient(gameId, followUps);
            tcs.SetResult(SuccessResponse());
        }
        catch (Exception ex)
        {
            Plugin.logger.LogError("Multiplayer> Error during HandleModdedSendCommand: " + ex.Message);
            tcs.SetException(new Il2CppSystem.Exception(ex.Message));
        }
        finally
        {
            _sendLock.Release();
        }
    }

    /// <summary>
    /// Auto-play follow-up commands (resigned players' turns) computed on the shadow are fed to the live client exactly like a vanilla server push.
    /// </summary>
    private static void FeedFollowUpsToLiveClient(string gameId, List<byte[]> followUps)
    {
        if (followUps.Count == 0) return;

        var client = GameManager.Client;
        if (client == null || !client.CurrentGameId.HasValue ||
            client.CurrentGameId.Value.ToString() != gameId)
        {
            return;
        }

        var followUpCommands = new Il2CppSystem.Collections.Generic.List<CommandBase>();
        foreach (var serialized in followUps)
        {
            if (CommandBase.FromByteArray(serialized, out var followUpCommand, out _))
            {
                followUpCommands.Add(followUpCommand);
            }
        }

        if (followUpCommands.Count > 0)
        {
            _ = client.ReceiveCommand(followUpCommands);
        }
    }

    private static int CountCommands(byte[] stateBytes)
    {
        if (!SerializationHelpers.FromByteArray<GameState>(stateBytes, out GameState state))
        {
            return -1;
        }

        return state.CommandStack.Count;
    }

    /// <summary>
    /// Runs the command on a copy of the authoritative state.
    /// Returns the serialized follow-up commands and the new state separately.
    /// </summary>
    internal static (ModdedGameStateViewModel? update, List<byte[]> followUps, byte[]? newStateBytes)
        ComputeModdedUpdate(byte[] preStateBytes, string gameId, CommandBase command,
            string? resignedAccountId, int expectedFirstIndex = -1)
    {
        var noFollowUps = new List<byte[]>();

        if (!SerializationHelpers.FromByteArray<GameState>(preStateBytes, out GameState stateCopy))
        {
            Plugin.logger.LogError("Multiplayer> Could not deserialize shadow state");
            return (null, noFollowUps, null);
        }

        new ActionManager(stateCopy).Update();

        int version = stateCopy.Version;
        int firstIndex = stateCopy.CommandStack.Count;
        if (expectedFirstIndex >= 0 && firstIndex != expectedFirstIndex)
        {
            Plugin.logger.LogWarning(
                $"Multiplayer> Shadow state out of sync ({firstIndex} commands, expected {expectedFirstIndex})");
            return (null, noFollowUps, null);
        }

        var commandList = new Il2CppSystem.Collections.Generic.List<CommandBase>();
        commandList.Add(command);
        var result = GameStateUtils.PerformCommands(stateCopy, commandList, out _, out _);
        if (result == null || !result.Success)
        {
            Plugin.logger.LogError("Multiplayer> Command execution failed on shadow state");
            return (null, noFollowUps, null);
        }

        int newCount = stateCopy.CommandStack.Count;
        var uploadCommands = new List<ModdedCommandViewModel>();
        var followUps = new List<byte[]>();
        bool isEndTurn = false;

        for (int i = firstIndex; i < newCount; i++)
        {
            var executed = stateCopy.CommandStack[i];
            if (executed.GetCommandType() == CommandType.EndTurn)
            {
                isEndTurn = true;
            }

            var serialized = CommandBase.ToByteArray(executed, version);
            uploadCommands.Add(new ModdedCommandViewModel { serializedData = serialized, commandIndex = i });
            if (i > firstIndex)
            {
                followUps.Add(serialized);
            }
        }

        string? currentPlayerId = GameStateUtils.GetCurrentPlayerAccountId(stateCopy).ToString();
        if (currentPlayerId == EmptyGuid)
        {
            currentPlayerId = null;
        }

        var newStateBytes = SerializationHelpers.ToByteArray(stateCopy, stateCopy.Version);

        var summaryBytes = Array.Empty<byte>();
        if (GameStateSummary.FromGameStateByteArray(newStateBytes, out GameStateSummary summary,
                out GameState summaryState))
        {
            summaryBytes = SerializationHelpers.ToByteArray(summary, summaryState.Version);
        }

        var update = new ModdedGameStateViewModel
        {
            gameId = gameId,
            commands = uploadCommands,
            serializedGameState = newStateBytes,
            serializedGameSummary = summaryBytes,
            newCommandCount = newCount,
            currentPlayerId = currentPlayerId,
            isEndTurn = isEndTurn,
            isGameEnded = stateCopy.CurrentState == GameState.State.Ended,
            resignedPlayerId = resignedAccountId
        };

        return (update, followUps, newStateBytes);
    }

    internal static async System.Threading.Tasks.Task<bool> UploadModdedUpdate(ModdedGameStateViewModel update)
    {
        try
        {
            var payload = System.Text.Json.JsonSerializer.Serialize(update);
            var response = await PolytopiaBackendAdapter.Instance.HubConnection
                .InvokeAsync<ServerResponse<ResponseViewModel>>(
                    "UpdateGameStateModded",
                    payload,
                    Il2CppSystem.Threading.CancellationToken.None
                );

            if (response != null && response.Success)
            {
                return true;
            }

            Plugin.logger.LogWarning($"Multiplayer> UpdateGameStateModded rejected: {response?.ErrorMessage}");
        }
        catch (Exception e)
        {
            Plugin.logger.LogError($"Multiplayer> UpdateGameStateModded failed: {e.Message}");
        }

        return false;
    }

    /// <summary>
    /// Relayed commands from other players are applied to the shadow so it tracks the authoritative state without refetching.
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(PolytopiaBackendAdapter), nameof(PolytopiaBackendAdapter.OnCommand))]
    private static void PolytopiaBackendAdapter_OnCommand(CommandArrayViewModel model)
    {
        try
        {
            var gameId = model.GameId.ToString();
            if (!IsModdedGame(gameId)) return;

            var shadow = GetShadowState(gameId);
            if (shadow == null) return;

            if (!SerializationHelpers.FromByteArray<GameState>(shadow, out GameState state))
            {
                DropShadowState(gameId);
                return;
            }

            var actionManager = new ActionManager(state);
            actionManager.Update();
            foreach (var commandViewModel in model.Commands)
            {
                int index = commandViewModel.CommandIndex;
                if (index >= 0 && index < state.CommandStack.Count)
                {
                    continue;
                }

                if (index > state.CommandStack.Count)
                {
                    DropShadowState(gameId);
                    return;
                }

                if (!CommandBase.FromByteArray(commandViewModel.SerializedData, out var command, out _) ||
                    !actionManager.ExecuteCommand(command, out _))
                {
                    DropShadowState(gameId);
                    return;
                }
            }

            SetShadowState(gameId, SerializationHelpers.ToByteArray(state, state.Version));
        }
        catch (Exception e)
        {
            Plugin.logger.LogWarning($"Multiplayer> Could not apply relayed commands to shadow: {e.Message}");
            DropShadowState(model.GameId.ToString());
        }
    }

    /// <summary>
    /// Vanilla resign asks the server to build the resign command, which the relay server cannot do for modded games. Build and execute it on the shadow state instead.
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(BackendAdapter), nameof(BackendAdapter.Resign))]
    private static bool BackendAdapter_Resign(
        ref Il2CppSystem.Threading.Tasks.Task<ServerResponse<ResponseViewModel>> __result,
        ResignBindingModel model)
    {
        if (!IsModdedGame(model.GameId.ToString())) return true;

        var taskCompletionSource =
            new Il2CppSystem.Threading.Tasks.TaskCompletionSource<ServerResponse<ResponseViewModel>>();

        _ = HandleModdedResign(taskCompletionSource, model.GameId);

        __result = taskCompletionSource.Task;

        return false;
    }

    private static async System.Threading.Tasks.Task HandleModdedResign(
        Il2CppSystem.Threading.Tasks.TaskCompletionSource<ServerResponse<ResponseViewModel>> tcs,
        Il2CppSystem.Guid gameIdGuid)
    {
        await _sendLock.WaitAsync();
        try
        {
            var gameId = gameIdGuid.ToString();
            var ownAccountId = AccountManager.PlayerAccountId;

            var shadow = GetShadowState(gameId) ?? await FetchShadowState(gameIdGuid);
            if (shadow == null ||
                !SerializationHelpers.FromByteArray<GameState>(shadow, out GameState state) ||
                !state.TryGetPlayer(ownAccountId, out PlayerState ownPlayer))
            {
                tcs.SetResult(FailedResponse());
                return;
            }

            var resignCommand = new ResignCommand(state.CurrentPlayer, ownPlayer.Id, 0, false);
            var (update, followUps, newStateBytes) =
                ComputeModdedUpdate(shadow, gameId, resignCommand, ownAccountId.ToString());
            if (update == null)
            {
                DropShadowState(gameId);
                tcs.SetResult(FailedResponse());
                return;
            }

            var uploaded = await UploadModdedUpdate(update);
            if (!uploaded)
            {
                DropShadowState(gameId);
                tcs.SetResult(FailedResponse());
                return;
            }

            SetShadowState(gameId, newStateBytes!);

            var client = GameManager.Client;
            if (client != null && client.CurrentGameId.HasValue &&
                client.CurrentGameId.Value.ToString() == gameId)
            {
                var commands = new Il2CppSystem.Collections.Generic.List<CommandBase>();
                commands.Add(resignCommand);
                foreach (var serialized in followUps)
                {
                    if (CommandBase.FromByteArray(serialized, out var followUpCommand, out _))
                    {
                        commands.Add(followUpCommand);
                    }
                }

                _ = client.ReceiveCommand(commands);
            }

            tcs.SetResult(SuccessResponse());
        }
        catch (Exception ex)
        {
            Plugin.logger.LogError("Multiplayer> Error during HandleModdedResign: " + ex.Message);
            tcs.SetException(new Il2CppSystem.Exception(ex.Message));
        }
        finally
        {
            _sendLock.Release();
        }
    }

    /// <summary>
    /// Skipping is not supported yet for modded games.
    /// Returns true to suppress errors.
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(BackendAdapter), nameof(BackendAdapter.SkipTurn))]
    private static bool BackendAdapter_SkipTurn(
        ref Il2CppSystem.Threading.Tasks.Task<ServerResponse<ResponseViewModel>> __result,
        SkipTurnBindingModel model)
    {
        if (!IsModdedGame(model.GameId.ToString())) return true;

        Plugin.logger.LogInfo("Multiplayer> SkipTurn is not supported for modded games yet");

        var taskCompletionSource =
            new Il2CppSystem.Threading.Tasks.TaskCompletionSource<ServerResponse<ResponseViewModel>>();
        taskCompletionSource.SetResult(SuccessResponse());
        __result = taskCompletionSource.Task;

        return false;
    }

    /// <summary>
    ///Matchmaking is blocked while gameplay mods are loaded.
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(BackendAdapter), nameof(BackendAdapter.SubmitMatchmakingRequest))]
    private static bool BackendAdapter_SubmitMatchmakingRequest(
        ref Il2CppSystem.Threading.Tasks.Task<ServerResponse<MatchmakingSubmissionViewModel>> __result)
    {
        Plugin.logger.LogWarning("Multiplayer> Matchmaking blocked: gameplay mods are loaded");
        PopupManager.GetBasicPopupWithData(new(
            "Matchmaking disabled",
            "Matchmaking is unavailable while gameplay mods are loaded.",
            new(new PopupBase.PopupButtonData[] {
                new("OK")
            })
        )).Show();

        var taskCompletionSource =
            new Il2CppSystem.Threading.Tasks.TaskCompletionSource<ServerResponse<MatchmakingSubmissionViewModel>>();
        taskCompletionSource.SetResult(new ServerResponse<MatchmakingSubmissionViewModel>
        {
            Success = false,
            ErrorCode = ErrorCode.StateProhibitsOperation,
            ErrorMessage = "Matchmaking is disabled while gameplay mods are loaded."
        });
        __result = taskCompletionSource.Task;

        return false;
    }

    private static ServerResponse<ResponseViewModel> SuccessResponse() =>
        new() { Success = true, Data = new ResponseViewModel() };

    private static ServerResponse<ResponseViewModel> FailedResponse() =>
        new()
        {
            Success = false,
            ErrorCode = ErrorCode.StateProhibitsOperation,
            ErrorMessage = "Modded game operation failed."
        };
}
