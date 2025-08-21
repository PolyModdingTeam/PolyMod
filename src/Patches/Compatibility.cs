using System.Text;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PolyMod.Patches;

internal static class Compatibility
{
    internal static string checksum = string.Empty;
    internal static bool shouldResetSettings = false;
    private static bool sawSignatureWarning;

    public static void HashSignatures(StringBuilder checksumString)
    {
        checksum = Util.Hash(checksumString);
    }

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

    [HarmonyPostfix]
    [HarmonyPatch(typeof(StartScreen), nameof(StartScreen.Start))]
    private static void StartScreen_Start()
    {
        string lastChecksum = checksum;
        try
        {
            lastChecksum = new(File.ReadAllText(Constants.CHECKSUM_PATH));
        }
        catch (FileNotFoundException) { }

        File.WriteAllText(
            Constants.CHECKSUM_PATH,
            checksum
        );
        if (lastChecksum != checksum)
        {
            shouldResetSettings = true;
        }

        Version incompatibilityWarningLastVersion = new(PlayerPrefs.GetString(
            Constants.INCOMPATIBILITY_WARNING_LAST_VERSION_KEY,
            Constants.POLYTOPIA_VERSION.CutRevision().ToString()
        ));
        if (VersionManager.SemanticVersion.Cast().CutRevision() > incompatibilityWarningLastVersion)
        {
            PlayerPrefs.SetString(
                Constants.INCOMPATIBILITY_WARNING_LAST_VERSION_KEY,
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
                        (Il2CppSystem.Action)Application.Quit,
                        closesPopup: false
                    )
                }))
            ).Show();
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(GameInfoPopup), nameof(GameInfoPopup.OnMainButtonClicked))]
    private static bool GameInfoPopup_OnMainButtonClicked(GameInfoPopup __instance, int id, BaseEventData eventData)
    {
        return CheckSignatures(__instance.OnMainButtonClicked, id, eventData, __instance.gameId);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(StartScreen), nameof(StartScreen.OnResumeButtonClick))]
    private static bool StartScreen_OnResumeButtonClick(StartScreen __instance, int id, BaseEventData eventData)
    {
        return CheckSignatures(__instance.OnResumeButtonClick, id, eventData, ClientBase.GetSinglePlayerSessions()[0]);
    }

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
        foreach (var gameId in ClientBase.GetSinglePlayerSessions())
        {
            File.Delete(Path.Combine(Application.persistentDataPath, $"{gameId}.signatures"));
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(GameManager), nameof(GameManager.MatchEnded))]
    private static void GameManager_MatchEnded(bool localPlayerIsWinner, ScoreDetails scoreDetails, byte winnerId)
    {
        File.Delete(Path.Combine(Application.persistentDataPath, $"{GameManager.Client.gameId}.signatures"));
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(ClientBase), nameof(ClientBase.CreateSession), typeof(GameSettings), typeof(Il2CppSystem.Guid))]
    private static void ClientBase_CreateSession(GameSettings settings, Il2CppSystem.Guid gameId)
    {
        File.WriteAllTextAsync(
            Path.Combine(Application.persistentDataPath, $"{gameId}.signatures"),
            checksum
        );
    }

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

    internal static void RestorePreliminaryGameSettings()
    {
        GameManager.PreliminaryGameSettings.disabledTribes.Clear();
        GameManager.PreliminaryGameSettings.selectedSkins.Clear();
        GameManager.PreliminaryGameSettings.SaveToDisk();
    }

    internal static void Init()
    {
        Harmony.CreateAndPatchAll(typeof(Compatibility));
    }
}
