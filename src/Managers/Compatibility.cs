using HarmonyLib;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PolyMod.Managers;

/// <summary>
/// Manages compatibility checks for mods, including checksum verification and version warnings.
/// </summary>
internal static class Compatibility
{
    /// <summary>
    /// The checksum of all loaded mods.
    /// </summary>
    internal static string checksum = string.Empty;

    /// <summary>
    /// Whether the game settings should be reset due to a change in mods.
    /// </summary>
    internal static bool shouldResetSettings = false;
    private static bool sawSignatureWarning;

    /// <summary>
    /// Hashes the signatures of all loaded mods to create a checksum.
    /// </summary>
    /// <param name="checksumString">A string builder containing the signatures to hash.</param>
    public static void HashSignatures(StringBuilder checksumString)
    {
        checksum = Util.Hash(checksumString);
    }

    /// <summary>
    /// Checks the signature of a saved game to ensure it is compatible with the current mods.
    /// </summary>
    /// <returns>True if the signatures match or if the check is ignored, false otherwise.</returns>
    private static bool CheckSignatures(Action<int, BaseEventData> action, int id, BaseEventData eventData, Il2CppSystem.Guid gameId)
    {
        if (sawSignatureWarning)
        {
            sawSignatureWarning = false;
            return true;
        }

        string stateChecksum = string.Empty;
        try
        {
            Plugin.logger.LogInfo($"Getting checksum for state {gameId}");
            stateChecksum = File.ReadAllText(Path.Combine(Application.persistentDataPath, $"{gameId}.signatures"));
            Plugin.logger.LogInfo($"Checksum found.");
        }
        catch
        {
            Plugin.logger.LogInfo($"Failed to get checksum.");
        }
        if (stateChecksum == string.Empty)
        {
            Plugin.logger.LogInfo($"State checksum is empty, ignoring.");
            return true;
        }
        bool doChecksumsMatch = stateChecksum == checksum;
        Plugin.logger.LogInfo($"State checksum: '{stateChecksum}', global checksum: '{checksum}', comparison result : {doChecksumsMatch}");
        if (Plugin.config.debug)
        {
            Plugin.logger.LogInfo($"Debug detected, ignoring.");
            return true;
        }
        if (!doChecksumsMatch)
        {
            PopupManager.GetBasicPopup(new(
                Localization.Get("polymod.signature.mismatch"),
                Localization.Get("polymod.signature.incompatible"),
                new(new PopupBase.PopupButtonData[] {
                    new("OK")
                })
            )).Show();
            return false;
        }
        return true;
    }

    /// <summary>
    /// Performs compatibility checks when the start screen is shown.
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(StartScreen), nameof(StartScreen.Start))]
    private static void StartScreen_Start()
    {
        string lastChecksum = checksum;
        try
        {
            lastChecksum = new(File.ReadAllText(Plugin.CHECKSUM_PATH));
        }
        catch (FileNotFoundException) { }

        File.WriteAllText(
            Plugin.CHECKSUM_PATH,
            checksum
        );
        if (lastChecksum != checksum)
        {
            shouldResetSettings = true;
        }

        Version incompatibilityWarningLastVersion = new(PlayerPrefs.GetString(
            Plugin.INCOMPATIBILITY_WARNING_LAST_VERSION_KEY,
            Plugin.POLYTOPIA_VERSION.CutRevision().ToString()
        ));
        if (VersionManager.SemanticVersion.Cast().CutRevision() > incompatibilityWarningLastVersion)
        {
            PlayerPrefs.SetString(
                Plugin.INCOMPATIBILITY_WARNING_LAST_VERSION_KEY,
                VersionManager.SemanticVersion.Cast().CutRevision().ToString()
            );
            PlayerPrefs.Save();
            PopupManager.GetBasicPopup(new(
                Localization.Get("polymod.version.mismatch"),
                Localization.Get("polymod.version.mismatch.description"),
                new(new PopupBase.PopupButtonData[] {
                    new("buttons.stay", customColorStates: ColorConstants.redButtonColorStates),
                    new(
                        "buttons.exitgame",
                        PopupBase.PopupButtonData.States.None,
                        (UIButtonBase.ButtonAction)Quit,
                        closesPopup: false
                    )
                }))
            ).Show();

            void Quit(int buttonId, BaseEventData eventData)
            {
                Application.Quit();
            }
        }
    }

    /// <summary>
    /// Checks the signature of a pass-and-play game before loading it.
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(GameInfoPopup), nameof(GameInfoPopup.OnMainButtonClicked))]
    private static bool GameInfoPopup_OnMainButtonClicked(GameInfoPopup __instance, int id, BaseEventData eventData)
    {
        return CheckSignatures(__instance.OnMainButtonClicked, id, eventData, __instance.gameId);
    }

    /// <summary>
    /// Checks the signature of a single-player game before resuming it.
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(StartScreen), nameof(StartScreen.OnResumeButtonClick))]
    private static bool StartScreen_OnResumeButtonClick(StartScreen __instance, int id, BaseEventData eventData)
    {
        return CheckSignatures(__instance.OnResumeButtonClick, id, eventData, ClientBase.GetSinglePlayerSessions(GameManager.LocalPlayer.Id)[0]);
    }

    /// <summary>
    /// Deletes the signature file of a pass-and-play game when it is deleted.
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameInfoPopup), nameof(GameInfoPopup.DeletePaPGame))]
    private static void GameInfoPopup_DeletePaPGame(GameInfoPopup __instance)
    {
        File.Delete(Path.Combine(Application.persistentDataPath, $"{__instance.gameId}.signatures"));
    }

    /// <summary>
    /// Deletes the signature file of all singleplayer games when they are deleted.
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameInfoPopup), nameof(GameInfoPopup.DeletePaPGame))]
    private static void ClientBase_DeletePassAndPlayGame(GameInfoPopup __instance)
    {
        File.Delete(Path.Combine(Application.persistentDataPath, $"{__instance.gameId}.signatures"));
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(ClientBase), nameof(ClientBase.DeleteSinglePlayerGames))]
    private static void ClientBase_DeleteSinglePlayerGames()
    {
        foreach (var gameId in ClientBase.GetSinglePlayerSessions(GameManager.LocalPlayer.Id))
        {
            File.Delete(Path.Combine(Application.persistentDataPath, $"{gameId}.signatures"));
        }
    }

    /// <summary>
    /// Deletes the signature file of a game when the match ends.
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(GameManager), nameof(GameManager.MatchEnded))]
    private static void GameManager_MatchEnded(bool localPlayerIsWinner, ScoreDetails scoreDetails, byte winnerId)
    {
        File.Delete(Path.Combine(Application.persistentDataPath, $"{GameManager.Client.gameId}.signatures"));
    }

    /// <summary>
    /// Creates a signature file when a new game session is created.
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(ClientBase), nameof(ClientBase.CreateSession), typeof(GameSettings), typeof(Il2CppSystem.Guid))]
    private static void ClientBase_CreateSession(GameSettings settings, Il2CppSystem.Guid gameId)
    {
        File.WriteAllTextAsync(
            Path.Combine(Application.persistentDataPath, $"{gameId}.signatures"),
            checksum
        );
    }

    /// <summary>
    /// Resets game settings if necessary when the tribe selector screen is shown.
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(TribeSelectorScreen), nameof(TribeSelectorScreen.Show))]
    private static bool TribeSelectorScreen_Show(bool instant = false)
    {
        if (shouldResetSettings)
        {
            RestorePreliminaryGameSettings();
            shouldResetSettings = false;
        }
        return true;
    }

    /// <summary>
    /// Restores the preliminary game settings to their default values.
    /// </summary>
    internal static void RestorePreliminaryGameSettings()
    {
        GameManager.PreliminaryGameSettings.disabledTribes.Clear();
        GameManager.PreliminaryGameSettings.selectedSkins.Clear();
        GameManager.PreliminaryGameSettings.SaveToDisk();
    }

    /// <summary>
    /// Initializes the Compatibility manager by patching the necessary methods.
    /// </summary>
    internal static void Init()
    {
        Harmony.CreateAndPatchAll(typeof(Compatibility));
    }
}
