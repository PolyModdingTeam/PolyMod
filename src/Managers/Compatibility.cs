using HarmonyLib;
using Il2CppInterop.Runtime;
using UnityEngine;

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
	private const string downloadLink = "https://polymod.dev/download/";

	public static void ComputeChecksums(Dictionary<string, Mod> mods)
	{
		foreach (var (_, mod) in mods)
		{
			mod.checksum = ComputeModChecksum(mod);
		}

		List<string> signatures = new();
		foreach (var (id, mod) in mods)
		{
			if (mod.status != Mod.Status.Success) continue;
			if (mod.client || id == "polytopia") continue;

			signatures.Add($"{id}:{mod.checksum}");
		}
		signatures.Sort(StringComparer.Ordinal);

		checksum = Util.Hash(string.Join("\n", signatures));
	}

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
	/// Gets the player prefs key holding the checksum a saved game was created with.
	/// </summary>
	/// <param name="gameId">The id of the saved game.</param>
	/// <returns>The player prefs key.</returns>
	private static string StateChecksumKey(object gameId)
	{
		return Plugin.STATE_CHECKSUM_KEY_PREFIX + NormalizeGameId(gameId);
	}

	/// <summary>
	/// Normalizes a game id so that the same save always maps to one key. The game hands ids over
	/// as a Guid in some places and as a string in others, and those do not always match in case
	/// or in whether they are wrapped in braces.
	/// </summary>
	/// <param name="gameId">The id of the saved game, as a Guid or a string.</param>
	/// <returns>The normalized id.</returns>
	internal static string NormalizeGameId(object gameId)
	{
		return gameId.ToString()!.Trim('{', '}').ToLowerInvariant();
	}

	/// <summary>
	/// Gets the path of the signature file older versions wrote for a saved game.
	/// </summary>
	/// <param name="gameId">The id of the saved game.</param>
	/// <returns>The path of the legacy signature file.</returns>
	private static string LegacyStateChecksumPath(object gameId)
	{
		return Path.Combine(Application.persistentDataPath, $"{NormalizeGameId(gameId)}.signatures");
	}

	/// <summary>
	/// Stores the current mod checksum for a saved game.
	/// </summary>
	/// <param name="gameId">The id of the saved game.</param>
	internal static void SaveStateChecksum(object gameId)
	{
		PlayerPrefs.SetString(StateChecksumKey(gameId), checksum);
		PlayerPrefs.Save();
	}

	/// <summary>
	/// Reads the mod checksum a saved game was created with, migrating it out of the
	/// signature file older versions used.
	/// </summary>
	/// <param name="gameId">The id of the saved game.</param>
	/// <returns>The stored checksum, or an empty string when there is none.</returns>
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

	internal static void DeleteStateChecksum(object gameId)
	{
		PlayerPrefs.DeleteKey(StateChecksumKey(gameId));
		PlayerPrefs.Save();

		string legacyPath = LegacyStateChecksumPath(gameId);
		if (File.Exists(legacyPath))
			File.Delete(legacyPath);
	}

	/// <summary>
	/// Checks the signature of a saved game to ensure it is compatible with the current mods.
	/// </summary>
	/// <returns>True if the signatures match or if the check is ignored, false otherwise.</returns>
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
	/// Performs compatibility checks when the start screen is shown.
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
	/// Checks the signature of a pass-and-play game before loading it.
	/// </summary>
	[HarmonyPrefix]
	[HarmonyPatch(typeof(GameInfoPopup), nameof(GameInfoPopup.OnMainButtonClicked))]
	private static bool GameInfoPopup_OnMainButtonClicked(GameInfoPopup __instance)
	{
		return CheckSignatures(__instance.OnMainButtonClicked, __instance.gameId);
	}

	/// <summary>
	/// Checks the signature of a single-player game before resuming it.
	/// </summary>
	[HarmonyPrefix]
	[HarmonyPatch(typeof(StartScreen_UI2), nameof(StartScreen_UI2.OnResumeButtonLongPress))]
	private static bool StartScreen_OnResumeButtonClick(StartScreen_UI2 __instance)
	{
		return CheckSignatures(__instance.OnResumeButtonLongPress, LocalSaveFileUtils.GetSaveFiles(PolytopiaBackendBase.Game.GameType.SinglePlayer)[0]);
	}

	/// <summary>
	/// Deletes the stored checksum of a pass-and-play game when it is deleted.
	/// </summary>
	[HarmonyPostfix]
	[HarmonyPatch(typeof(GameInfoPopup), nameof(GameInfoPopup.DeletePaPGame))]
	private static void GameInfoPopup_DeletePaPGame(GameInfoPopup __instance)
	{
		DeleteStateChecksum(__instance.gameId);
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(LocalSaveFileUtils), nameof(LocalSaveFileUtils.DeleteSaveFile))]
	private static void LocalSaveFileUtils_DeleteSaveFile(PolytopiaBackendBase.Game.GameType gameType, string gameId, bool localOnly)
	{
		DeleteStateChecksum(gameId);
	}

	/// <summary>
	/// Deletes the stored checksums of all saved games of a type when they are deleted.
	/// </summary>
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
	/// Deletes the stored checksum of a game when the match ends.
	/// </summary>
	[HarmonyPrefix]
	[HarmonyPatch(typeof(GameManager), nameof(GameManager.MatchEnded))]
	private static void GameManager_MatchEnded(bool localPlayerIsWinner, ScoreDetails scoreDetails, byte winnerId)
	{
		DeleteStateChecksum(GameManager.Client.gameId);
	}

	/// <summary>
	/// Stores the current mod checksum when a new game session is created.
	/// </summary>
	[HarmonyPostfix]
	[HarmonyPatch(typeof(ClientBase), nameof(ClientBase.CreateSession), typeof(GameSettings), typeof(Il2CppSystem.Guid))]
	private static void ClientBase_CreateSession(GameSettings settings, Il2CppSystem.Guid gameId)
	{
		SaveStateChecksum(gameId);
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
