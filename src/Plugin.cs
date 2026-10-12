using System.IO.Pipes;
using System.Reflection;
using System.Text.Json;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using PolyMod.Managers;

namespace PolyMod;

/// <summary>
/// Main plugin class for PolyMod.
/// </summary>
[BepInPlugin("com.polymod", "PolyMod", VERSION)]
public partial class Plugin : BepInEx.Unity.IL2CPP.BasePlugin
{
    internal static string? Token { get; set; }
	internal static bool ValidLaunch { get; set; } = false;
	internal static bool Disabled { get; set; } = false;

	/// <summary>
	/// Represents the configuration for PolyMod.
	/// </summary>
	/// <param name="debug">Whether to enable debug mode.</param>
	internal record PolyConfig(
		bool debug = false,
		bool allowUnsafeIndexes = false,
		string overrideDeviceId = ""
	)
	{
    	public List<string> disabledMods { get; init; } = new();
	};

	/// <summary>
	/// The starting index for automatically assigned IDs.
	/// </summary>
	internal const int AUTOIDX_STARTS_FROM = 1000;

	/// <summary>
	/// The key used to store the last version for which the incompatibility warning was shown.
	/// </summary>
	internal const string INCOMPATIBILITY_WARNING_LAST_VERSION_KEY
		= "INCOMPATIBILITY_WARNING_LAST_VERSION";

	internal const int LAUNCH_DATA_TIMEOUT = 2000;

	/// <summary>
	/// The base path for PolyMod files.
	/// </summary>
	public static readonly string BASE_PATH = Path.Combine(BepInEx.Paths.BepInExRootPath, "..");

	/// <summary>
	/// The path to the mods directory.
	/// </summary>
	public static readonly string MODS_PATH = Path.Combine(BASE_PATH, "Mods");

	/// <summary>
	/// The path to the directory where game data is dumped.
	/// </summary>
	public static readonly string DUMPED_DATA_PATH = Path.Combine(BASE_PATH, "DumpedData");

	/// <summary>
	/// The path to the directory where localization is dumped.
	/// </summary>
	public static readonly string LOGIC_DUMP_PATH = Path.Combine(DUMPED_DATA_PATH, "Logic");

	/// <summary>
	/// The path to the directory where gameLogicData and avatarData are dumped.
	/// </summary>
	public static readonly string LOCALIZATION_DUMP_PATH = Path.Combine(DUMPED_DATA_PATH, "Localization");

	/// <summary>
	/// The path to the directory where sprites and sprite infos are dumped.
	/// </summary>
	public static readonly string SPRITES_DUMP_PATH = Path.Combine(DUMPED_DATA_PATH, "Sprites");

	/// <summary>
	/// The path to the directory where prefabs datas are dumped.
	/// </summary>
	public static readonly string PREFABS_DUMP_PATH = Path.Combine(DUMPED_DATA_PATH, "Prefabs");

	/// <summary>
	/// The path to the PolyMod configuration file.
	/// </summary>
	internal static readonly string CONFIG_PATH = Path.Combine(BASE_PATH, "PolyMod.json");

	/// <summary>
	/// The path to the checksum file.
	/// </summary>
	internal static readonly string CHECKSUM_PATH
		= Path.Combine(BASE_PATH, "CHECKSUM");

	/// <summary>
	/// The link to the PolyMod Discord server.
	/// </summary>
	internal static readonly string DISCORD_LINK = "https://discord.gg/eWPdhWtfVy";

	/// <summary>
	/// A list of log messages to ignore.
	/// </summary>
	internal static readonly List<string> LOG_MESSAGES_IGNORE = new()
	{
		"Failed to find atlas",
		"Could not find sprite",
		"Couldn't find prefab for type",
		"MARKET: id:",
		"Missing name for value",
	};


#pragma warning disable CS8618
	/// <summary>
	/// The PolyMod configuration.
	/// </summary>
	internal static PolyConfig config;

	/// <summary>
	/// The logger instance for PolyMod.
	/// </summary>
	internal static ManualLogSource logger;
#pragma warning restore CS8618

	/// <summary>
	/// The entry point for the plugin.
	/// </summary>
	public override void Load()
	{
		try
		{
			config = JsonSerializer.Deserialize<PolyConfig>(File.ReadAllText(CONFIG_PATH))!;
		}
		catch
		{
			config = new();
		}
		WriteConfig();
		UpdateConsole();
		logger = Log;
		ConfigFile.CoreConfig[new("Logging.Disk", "WriteUnityLog")].BoxedValue = true;

		CaptureLaunchData();

		if(Disabled)
			return;

		Compatibility.Init();
		Audio.Init();
		Loc.Init();
		Visual.Init();
		Hub.Init();

		Main.Init();
	}

	internal static void CaptureLaunchData()
	{
		try
		{
			using var client = new NamedPipeClientStream(
				".", "PolyMod.Launcher", PipeDirection.In, PipeOptions.CurrentUserOnly);
			client.Connect(LAUNCH_DATA_TIMEOUT);

			using var reader = new StreamReader(client);
			var line = reader.ReadLine();
			if (string.IsNullOrEmpty(line)) return;
			using var doc = JsonDocument.Parse(line);
			var root = doc.RootElement;

			ValidLaunch = root.GetProperty("validLaunch").GetBoolean();
			Disabled = root.GetProperty("disabled").GetBoolean();
			Token = root.GetProperty("token").GetString() ?? "";
		}
		catch
		{
			ValidLaunch = false;
		}
		ValidLaunch = true;
	}

	/// <summary>
	/// Gets a resource stream from the assembly.
	/// </summary>
	/// <param name="id">The ID of the resource.</param>
	/// <returns>The resource stream.</returns>
	internal static Stream GetResource(string id)
	{
		return Assembly.GetExecutingAssembly().GetManifestResourceStream(
			$"{typeof(Plugin).Namespace}.resources.{id}"
		)!;
	}

	/// <summary>
	/// Writes the configuration to disk.
	/// </summary>
	internal static void WriteConfig()
	{
		File.WriteAllText(CONFIG_PATH, JsonSerializer.Serialize(config));
	}

	/// <summary>
	/// Updates the console based on the debug configuration.
	/// </summary>
	internal static void UpdateConsole()
	{
		if (config.debug)
		{
			ConsoleManager.CreateConsole();
		}
		else if (OperatingSystem.IsWindows())
		{
			// BepInEx's Unix console driver throws unsupported on detach. Off-Windows there is no separate console window, so there is nothing to detach.
			ConsoleManager.DetachConsole();
		}
	}
}