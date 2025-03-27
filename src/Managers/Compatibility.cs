using HarmonyLib;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PolyMod.Managers;
internal static class Compatibility
{
    internal static string signature = string.Empty;
    internal static string looseSignature = string.Empty;
    private static bool sawSignatureWarning;

    public static void HashSignatures(StringBuilder looseSignatureString, StringBuilder signatureString)
    {
        looseSignature = Util.Hash(looseSignatureString);
        signature = Util.Hash(signatureString);
    }

    private static bool CheckSignatures(Action<int, BaseEventData> action, int id, BaseEventData eventData, Il2CppSystem.Guid gameId)
    {
        if (sawSignatureWarning)
        {
            sawSignatureWarning = false;
            return true;
        }

        string[] signatures = { string.Empty, string.Empty };
        try
        {
            signatures = File.ReadAllLines(Path.Combine(Application.persistentDataPath, $"{gameId}.signatures"));
        }
        catch { }
        if (signatures[0] == string.Empty && signatures[1] == string.Empty) return true;
        if (Plugin.config.debug) return true;
        if (looseSignature != signatures[0])
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
        if (signature != signatures[1])
        {
            PopupManager.GetBasicPopup(new(
                Localization.Get("polymod.signature.mismatch"),
                Localization.Get("polymod.signature.maybe.incompatible"),
                new(new PopupBase.PopupButtonData[] {
                    new(
                        "OK",
                        callback: (UIButtonBase.ButtonAction)((int _, BaseEventData _) => {
                            sawSignatureWarning = true;
                            action(id, eventData);
                        })
                    )
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
        Version incompatibilityWarningLastVersion = Plugin.POLYTOPIA_VERSION.CutRevision();
        try
        {
            incompatibilityWarningLastVersion = new(File.ReadAllText(Plugin.INCOMPATIBILITY_WARNING_LAST_VERSION_PATH));
        }
        catch (FileNotFoundException) { }
        if (VersionManager.SemanticVersion.Cast().CutRevision() > incompatibilityWarningLastVersion)
        {
            File.WriteAllText(
                Plugin.INCOMPATIBILITY_WARNING_LAST_VERSION_PATH,
                VersionManager.SemanticVersion.Cast().CutRevision().ToString()
            );
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
        File.WriteAllLinesAsync(
            Path.Combine(Application.persistentDataPath, $"{gameId}.signatures"),
            new string[] { looseSignature, signature }
        );
    }

    internal static void Init()
    {
        Harmony.CreateAndPatchAll(typeof(Compatibility));
    }
}
