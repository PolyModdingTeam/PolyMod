using HarmonyLib;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace PolyMod.Managers;

/// <summary>
/// The compatibility checks for mods. They compare mod checksums with the checksums of saved games.
/// They also show the warnings for an invalid launch and for a new game version.
/// </summary>
internal static class Compatibility
{
    /// <summary>
    /// The global checksum of all loaded mods that are not client-side mods.
    /// </summary>
    internal static string checksum = string.Empty;

    /// <summary>
    /// The flag that tells if the tribe and skin selection must go back to the default values.
    /// It is true when the global checksum is different from the checksum of the last start.
    /// </summary>
    internal static bool shouldResetSettings = false;
    private static bool sawSignatureWarning;
    private const string downloadLink = "https://polymod.dev/download/";

    /// <summary>
    /// Calculates the checksum of each mod and the global checksum.
    /// The method uses only the mods that loaded successfully, and skips client-side mods and the <c>polytopia</c> mod.
    /// It puts each checksum in <see cref="Mod.checksum"/> and the global checksum in <see cref="checksum"/>.
    /// </summary>
    /// <param name="mods">The mods, with their ids as keys.</param>
    public static void ComputeChecksums(Dictionary<string, Mod> mods)
    {
        List<string> signatures = new();
        foreach (var (id, mod) in mods)
        {
            if (mod.status != Mod.Status.Success) continue;
            if (mod.client || id == "polytopia") continue;

            mod.checksum = ComputeModChecksum(mod);
            signatures.Add($"{id}:{mod.checksum}");
        }
        signatures.Sort(StringComparer.Ordinal);

        checksum = Util.Hash(string.Join("\n", signatures));
    }

    /// <summary>
    /// Calculates the checksum of one mod from its id, its version, and the names and contents of its files.
    /// The method skips directory entries and sorts the files by name, so the order of the files has no effect.
    /// </summary>
    /// <param name="mod">The mod to calculate the checksum for.</param>
    /// <returns>The SHA256 checksum of the mod as a hexadecimal string.</returns>
    public static string ComputeModChecksum(Mod mod)
    {
        List<string> entries = new();
        foreach (Mod.File file in mod.files)
        {
            string name = file.name.Replace('\\', '/');
            if (name.EndsWith('/')) continue;

            entries.Add($"{name}:{Util.HashBytes(file.bytes)}");
        }
        entries.Sort(StringComparer.Ordinal);

        return Util.Hash($"{mod.id}:{mod.version}\n{string.Join("\n", entries)}");
    }

    /// <summary>
    /// Gets the player prefs key that keeps the global checksum for a saved game.
    /// </summary>
    /// <param name="gameId">The id of the saved game, as a Guid or a string.</param>
    /// <returns>The player prefs key.</returns>
    private static string StateChecksumKey(object gameId)
    {
        return Plugin.STATE_CHECKSUM_KEY_PREFIX + NormalizeGameId(gameId);
    }

    /// <summary>
    /// Changes a game id to one standard text form, so that one saved game always has one key.
    /// The game gives ids as a Guid in some methods and as a string in other methods.
    /// The method removes the braces and changes all letters to lowercase.
    /// </summary>
    /// <param name="gameId">The id of the saved game, as a Guid or a string.</param>
    /// <returns>The game id in lowercase, without braces.</returns>
    internal static string NormalizeGameId(object gameId)
    {
        return gameId.ToString()!.Trim('{', '}').ToLowerInvariant();
    }

    /// <summary>
    /// Gets the path of the legacy <c>.signatures</c> file for a saved game.
    /// The file is in the persistent data folder of the game.
    /// </summary>
    /// <param name="gameId">The id of the saved game, as a Guid or a string.</param>
    /// <returns>The path of the legacy signature file.</returns>
    private static string LegacyStateChecksumPath(object gameId)
    {
        return Path.Combine(Application.persistentDataPath, $"{NormalizeGameId(gameId)}.signatures");
    }

    /// <summary>
    /// Writes the current global checksum to the player prefs for a saved game.
    /// </summary>
    /// <param name="gameId">The id of the saved game, as a Guid or a string.</param>
    internal static void SaveStateChecksum(object gameId)
    {
        PlayerPrefs.SetString(StateChecksumKey(gameId), checksum);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Reads the global checksum that the player prefs keep for a saved game.
    /// If the player prefs do not have the checksum, the method reads the legacy signature file.
    /// It then writes that checksum to the player prefs and deletes the legacy file.
    /// </summary>
    /// <param name="gameId">The id of the saved game, as a Guid or a string.</param>
    /// <returns>The saved checksum, or an empty string if there is no saved checksum.</returns>
    internal static string ReadStateChecksum(object gameId)
    {
        string key = StateChecksumKey(gameId);
        if (PlayerPrefs.HasKey(key))
            return PlayerPrefs.GetString(key);

        string legacyPath = LegacyStateChecksumPath(gameId);
        if (!File.Exists(legacyPath))
            return string.Empty;

        string legacyChecksum = File.ReadAllText(legacyPath);
        PlayerPrefs.SetString(key, legacyChecksum);
        PlayerPrefs.Save();
        File.Delete(legacyPath);
        return legacyChecksum;
    }

    /// <summary>
    /// Deletes the saved checksum of a saved game from the player prefs.
    /// It also deletes the legacy signature file if it exists.
    /// </summary>
    /// <param name="gameId">The id of the saved game, as a Guid or a string.</param>
    internal static void DeleteStateChecksum(object gameId)
    {
        PlayerPrefs.DeleteKey(StateChecksumKey(gameId));
        PlayerPrefs.Save();

        string legacyPath = LegacyStateChecksumPath(gameId);
        if (File.Exists(legacyPath))
            File.Delete(legacyPath);
    }

    /// <summary>
    /// Compares the saved checksum of a saved game with the current global checksum.
    /// If the checksums are different and debug mode is off, the method shows a mismatch popup.
    /// If the saved checksum is empty or cannot be read, the method skips the check.
    /// If <c>sawSignatureWarning</c> is true, the method sets it to false and skips the check.
    /// </summary>
    /// <param name="action">The original game action. The method does not use this parameter.</param>
    /// <param name="gameId">The id of the saved game.</param>
    /// <returns>False if the checksums are different and debug mode is off. True in all other conditions.</returns>
    private static bool CheckSignatures(Action action, Il2CppSystem.Guid gameId)
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
            stateChecksum = ReadStateChecksum(gameId);
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
            PopupManager.GetBasicPopupWithData(new(
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
    /// <para>
    /// Postfix patch for <c>StartScreen_UI2.OnShow</c>. It does the compatibility checks when the start screen opens.
    /// If the launch is not valid and the platform is not Android, it shows a popup with the exit and download buttons, and stops.
    /// </para>
    /// <para>
    /// It writes the global checksum to the checksum file. If the checksum changed since the last start, it sets <see cref="shouldResetSettings"/>.
    /// </para>
    /// <para>
    /// If the game version is newer than the version of the last warning, it saves the game version and shows a version mismatch popup.
    /// If there is no version of the last warning, it uses the game version that PolyMod supports.
    /// </para>
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(StartScreen_UI2), nameof(StartScreen_UI2.OnShow))]
    private static void StartScreen_UI2_OnShow()
    {
        if (!Plugin.ValidLaunch && Application.platform != RuntimePlatform.Android)
        {
            PopupManager.GetBasicPopupWithData(
                new(
                    Localization.Get("polymod.title.invalid"),
                    Localization.Get("polymod.description.invalid"),
                    new PopupBase.PopupButtonData[] {
                        new(
                            "buttons.exitgame",
                            PopupBase.PopupButtonData.States.None,
                            callback: (Il2CppSystem.Action)Application.Quit,
                            closesPopup: false
                        ),
                        new(
                            "buttons.download.launcher",
                            PopupBase.PopupButtonData.States.None,
                            callback: DelegateSupport.ConvertDelegate<Il2CppSystem.Action>(OpenSite),
                            closesPopup: false
                        )
                    }
                )
            ).Show();

            void OpenSite()
            {
                NativeHelpers.OpenURL(downloadLink, false);
                Application.Quit();
            }
            return;
        }
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
            PopupManager.GetBasicPopupWithData(
                new(
                    Localization.Get("polymod.version.mismatch"),
                    Localization.Get("polymod.version.mismatch.description"),
                    new PopupBase.PopupButtonData[] {
                        new("buttons.stay", customColorStates: ColorConstants.redButtonColorStates),
                        new(
                            "buttons.exitgame",
                            PopupBase.PopupButtonData.States.None,
                            (Il2CppSystem.Action)Application.Quit,
                            closesPopup: false
                        )
                    }
                )
            ).Show();
        }
    }

    /// <summary>
    /// Prefix patch for <c>GameInfoPopup.OnMainButtonClicked</c>. It compares the checksum of the game in the popup before the game opens.
    /// </summary>
    /// <param name="__instance">The game info popup.</param>
    /// <returns>False to stop the game from opening when the checksums are different. True to let the original method run.</returns>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(GameInfoPopup), nameof(GameInfoPopup.OnMainButtonClicked))]
    private static bool GameInfoPopup_OnMainButtonClicked(GameInfoPopup __instance)
    {
        return CheckSignatures(__instance.OnMainButtonClicked, __instance.gameId);
    }

    /// <summary>
    /// Prefix patch for <c>StartScreen_UI2.OnResumeButtonLongPress</c>. The game calls that method on a long press of the resume button.
    /// The patch compares the checksum of the first single-player saved game before the original method runs.
    /// </summary>
    /// <param name="__instance">The start screen.</param>
    /// <returns>False to skip the original method when the checksums are different. True to let the original method run.</returns>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(StartScreen_UI2), nameof(StartScreen_UI2.OnResumeButtonLongPress))]
    private static bool StartScreen_OnResumeButtonClick(StartScreen_UI2 __instance)
    {
        return CheckSignatures(__instance.OnResumeButtonLongPress, LocalSaveFileUtils.GetSaveFiles(PolytopiaBackendBase.Game.GameType.SinglePlayer)[0]);
    }

    /// <summary>
    /// Postfix patch for <c>GameInfoPopup.DeletePaPGame</c>. It deletes the saved checksum of the pass-and-play game that the user deleted.
    /// </summary>
    /// <param name="__instance">The game info popup of the deleted game.</param>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameInfoPopup), nameof(GameInfoPopup.DeletePaPGame))]
    private static void GameInfoPopup_DeletePaPGame(GameInfoPopup __instance)
    {
        DeleteStateChecksum(__instance.gameId);
    }

    /// <summary>
    /// Prefix patch for <c>LocalSaveFileUtils.DeleteSaveFile</c>. It deletes the saved checksum of the saved game before the game deletes the save file.
    /// The original method always runs.
    /// </summary>
    /// <param name="gameType">The type of the saved game.</param>
    /// <param name="gameId">The id of the saved game.</param>
    /// <param name="localOnly">The value that tells the game to delete only the local file. The patch does not use it.</param>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(LocalSaveFileUtils), nameof(LocalSaveFileUtils.DeleteSaveFile))]
    private static void LocalSaveFileUtils_DeleteSaveFile(PolytopiaBackendBase.Game.GameType gameType, string gameId, bool localOnly)
    {
        DeleteStateChecksum(gameId);
    }

    /// <summary>
    /// Prefix patch for <c>LocalSaveFileUtils.DeleteAllSaveFilesOfType</c>. It deletes the saved checksums of all saved games of one type.
    /// The patch runs before the game deletes the save files. The original method always runs.
    /// </summary>
    /// <param name="gameType">The type of the saved games to delete.</param>
    /// <param name="localOnly">The value that tells the game to delete only the local files. The patch does not use it.</param>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(LocalSaveFileUtils), nameof(LocalSaveFileUtils.DeleteAllSaveFilesOfType))]
    private static void LocalSaveFileUtils_DeleteAllSaveFilesOfType(PolytopiaBackendBase.Game.GameType gameType, bool localOnly)
    {
        foreach (var gameId in LocalSaveFileUtils.GetSaveFiles(gameType))
        {
            DeleteStateChecksum(gameId);
        }
    }

    /// <summary>
    /// Prefix patch for <c>GameManager.MatchEnded</c>. It deletes the saved checksum of the current game when the match ends.
    /// The original method always runs.
    /// </summary>
    /// <param name="localPlayerIsWinner">The value that tells if the local player won. The patch does not use it.</param>
    /// <param name="scoreDetails">The score details of the match. The patch does not use them.</param>
    /// <param name="winnerId">The id of the winner. The patch does not use it.</param>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(GameManager), nameof(GameManager.MatchEnded))]
    private static void GameManager_MatchEnded(bool localPlayerIsWinner, ScoreDetails scoreDetails, byte winnerId)
    {
        DeleteStateChecksum(GameManager.Client.gameId);
    }

    /// <summary>
    /// Postfix patch for the <c>ClientBase.CreateSession</c> overload that takes game settings and a game id.
    /// It writes the current global checksum for the new game.
    /// </summary>
    /// <param name="settings">The settings of the new game. The patch does not use them.</param>
    /// <param name="gameId">The id of the new game.</param>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(ClientBase), nameof(ClientBase.CreateSession), typeof(GameSettings), typeof(Il2CppSystem.Guid))]
    private static void ClientBase_CreateSession(GameSettings settings, Il2CppSystem.Guid gameId)
    {
        SaveStateChecksum(gameId);
    }

    /// <summary>
    /// Prefix patch for <c>TribeSelectorScreen.Show</c>. If <see cref="shouldResetSettings"/> is true, it calls
    /// <see cref="RestorePreliminaryGameSettings"/> and sets the flag to false.
    /// </summary>
    /// <param name="instant">The value that tells the game to show the screen without animation. The patch does not use it.</param>
    /// <returns>Always true, so the original method always runs.</returns>
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
    /// Clears the disabled tribes and the selected skins in the preliminary game settings.
    /// Then it writes these settings to disk.
    /// </summary>
    internal static void RestorePreliminaryGameSettings()
    {
        GameManager.PreliminaryGameSettings.disabledTribes.Clear();
        GameManager.PreliminaryGameSettings.selectedSkins.Clear();
        GameManager.PreliminaryGameSettings.SaveToDisk();
    }

    /// <summary>
    /// Applies all Harmony patches in the <see cref="Compatibility"/> class.
    /// </summary>
    internal static void Init()
    {
        Harmony.CreateAndPatchAll(typeof(Compatibility));
    }
}
