using System.Reflection;
using System.Text.Json;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using PolyMod.Managers;
using UnityEngine;

namespace PolyMod;
[BepInPlugin("com.polymod", "PolyMod", VERSION)]
public partial class Plugin : BepInEx.Unity.IL2CPP.BasePlugin
{
	internal record PolyConfig(
		bool debug = false
	);

	internal const int AUTOIDX_STARTS_FROM = 1000;
	public static readonly string BASE_PATH = Path.Combine(BepInEx.Paths.BepInExRootPath, "..");
	public static readonly string MODS_PATH = Path.Combine(BASE_PATH, "Mods");
	public static readonly string DUMPED_DATA_PATH = Path.Combine(BASE_PATH, "DumpedData");
	internal static readonly string CONFIG_PATH = Path.Combine(BASE_PATH, "PolyMod.json");
	internal static readonly string INCOMPATIBILITY_WARNING_LAST_VERSION_PATH
		= Path.Combine(BASE_PATH, "IncompatibilityWarningLastVersion");
	internal static readonly string CHECKSUM_PATH
		= Path.Combine(BASE_PATH, "CHECKSUM");
	internal static readonly string DISCORD_LINK = "https://discord.gg/eWPdhWtfVy";
	internal static readonly List<string> LOG_MESSAGES_IGNORE = new()
	{
		"Failed to find atlas",
		"Could not find sprite",
		"Couldn't find prefab for type",
		"MARKET: id:",
		"Missing name for value",
	};


#pragma warning disable CS8618
	internal static PolyConfig config;
	internal static ManualLogSource logger;
#pragma warning restore CS8618

	public override void Load()
	{
		try
		{
			config = JsonSerializer.Deserialize<PolyConfig>(File.ReadAllText(CONFIG_PATH))!;
		}
		catch
		{
			config = new();
			File.WriteAllText(CONFIG_PATH, JsonSerializer.Serialize(config));
		}
		if (!config.debug) ConsoleManager.DetachConsole();
		logger = Log;
		ConfigFile.CoreConfig[new("Logging.Disk", "WriteUnityLog")].BoxedValue = true;

		Compatibility.Init();

		Audio.Init();
		Loc.Init();
		Visual.Init();
		Hub.Init();

		Main.Init();
	}

	internal static Stream GetResource(string id)
	{
		return Assembly.GetExecutingAssembly().GetManifestResourceStream(
			$"{typeof(Plugin).Namespace}.resources.{id}"
		)!;
	}
}
