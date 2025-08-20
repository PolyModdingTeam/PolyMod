using System.Reflection;
using System.Text.Json;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using PolyMod.Managers;
using UnityEngine;

namespace PolyMod;

[BepInPlugin("com.polymod", "PolyMod", VERSION)]
internal partial class Plugin : BepInEx.Unity.IL2CPP.BasePlugin
{
	internal record PolyConfig(
		bool debug = false,
		bool autoUpdate = true,
		bool updatePrerelease = false
	);


#pragma warning disable CS8618
	internal static PolyConfig config;
	internal static ManualLogSource logger;
#pragma warning restore CS8618

	public override void Load()
	{
		try
		{
			config = JsonSerializer.Deserialize<PolyConfig>(File.ReadAllText(Constants.CONFIG_PATH))!;
		}
		catch
		{
			config = new();
		}
		WriteConfig();
		UpdateConsole();
		logger = Log;
		ConfigFile.CoreConfig[new("Logging.Disk", "WriteUnityLog")].BoxedValue = true;

		Compatibility.Init();

		Audio.Init();
		AutoUpdate.Init();
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

	internal static void WriteConfig()
	{
		File.WriteAllText(Constants.CONFIG_PATH, JsonSerializer.Serialize(config));
	}

	internal static void UpdateConsole()
	{
		if (config.debug)
		{
			ConsoleManager.CreateConsole();
		}
		else
		{
			ConsoleManager.DetachConsole();
		}
	}
}
