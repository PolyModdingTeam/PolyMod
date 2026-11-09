using System.IO.Pipes;
using System.Reflection;
using System.Text.Json;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using PolyMod.Managers;

namespace PolyMod;

/// <summary>
/// The BepInEx plugin of PolyMod. BepInEx loads this plugin and calls <see cref="Load"/>.
/// </summary>
[BepInPlugin("com.polymod", "PolyMod", VERSION)]
public partial class Plugin : BepInEx.Unity.IL2CPP.BasePlugin
{
    /// <summary>
    /// Gets or sets the token that the PolyMod launcher sends, or null if PolyMod did not get a token.
    /// </summary>
    internal static string? Token { get; set; }
    /// <summary>
    /// Gets or sets a value that tells if the game start is valid.
    /// <see cref="CaptureLaunchData"/> sets this value.
    /// If it is false, PolyMod shows an error popup on the start screen, but not on Android.
    /// </summary>
    internal static bool ValidLaunch { get; set; } = false;
    /// <summary>
    /// Gets or sets a value that is true when the PolyMod launcher tells PolyMod not to start.
    /// If it is true, <see cref="Load"/> stops before it starts the managers.
    /// </summary>
    internal static bool Disabled { get; set; } = false;

    /// <summary>
    /// The configuration of PolyMod. PolyMod reads it from <see cref="CONFIG_PATH"/> and writes it to the same file.
    /// </summary>
    /// <param name="debug">If true, PolyMod shows the BepInEx console.</param>
    /// <param name="showCoordinates">If true, PolyMod shows the coordinates of the tiles.</param>
    /// <param name="allowUnsafeIndexes">If true, a mod can give a fixed <c>idx</c> to a new enum value, if the <c>idx</c> is larger than all existing values of the enum.</param>
    /// <param name="overrideDeviceId">A device id value. No code in PolyMod reads this value at this time.</param>
    internal record PolyConfig(
        bool debug = false,
        bool showCoordinates = false,
        bool allowUnsafeIndexes = false,
        string overrideDeviceId = ""
    )
    {
        /// <summary>
        /// Gets the ids of the mods that PolyMod does not register.
        /// </summary>
        public List<string> disabledMods { get; init; } = new();
    };

    /// <summary>
    /// The first index that PolyMod gives to an automatically indexed enum value.
    /// PolyMod also uses this value to identify enum values that mods add.
    /// </summary>
    internal const int AUTOIDX_STARTS_FROM = 1000;

    /// <summary>
    /// The player prefs key that keeps the last game version for which PolyMod showed the incompatibility warning.
    /// </summary>
    internal const string INCOMPATIBILITY_WARNING_LAST_VERSION_KEY
        = "INCOMPATIBILITY_WARNING_LAST_VERSION";

    /// <summary>
    /// The prefix of the player prefs keys that keep the mod checksum of each saved game.
    /// </summary>
    internal const string STATE_CHECKSUM_KEY_PREFIX = "POLYMOD_STATE_CHECKSUM_";

    /// <summary>
    /// The maximum time, in milliseconds, that <see cref="CaptureLaunchData"/> waits to connect to the PolyMod launcher.
    /// </summary>
    internal const int LAUNCH_DATA_TIMEOUT = 2000;

    /// <summary>
    /// The root directory of PolyMod files. This is the parent directory of the BepInEx root directory.
    /// </summary>
    public static readonly string BASE_PATH = Path.Combine(BepInEx.Paths.BepInExRootPath, "..");

    /// <summary>
    /// The path of the directory that contains the mods.
    /// </summary>
    public static readonly string MODS_PATH = Path.Combine(BASE_PATH, "Mods");

    /// <summary>
    /// The path of the directory to which PolyMod writes the dumped game data.
    /// </summary>
    public static readonly string DUMPED_DATA_PATH = Path.Combine(BASE_PATH, "DumpedData");

    /// <summary>
    /// The path of the directory to which PolyMod writes the dumped GLD and avatar data.
    /// </summary>
    public static readonly string LOGIC_DUMP_PATH = Path.Combine(DUMPED_DATA_PATH, "Logic");

    /// <summary>
    /// The path of the directory to which PolyMod writes the dumped localization.
    /// </summary>
    public static readonly string LOCALIZATION_DUMP_PATH = Path.Combine(DUMPED_DATA_PATH, "Localization");

    /// <summary>
    /// The path of the directory to which PolyMod writes the dumped sprites and sprite infos.
    /// </summary>
    public static readonly string SPRITES_DUMP_PATH = Path.Combine(DUMPED_DATA_PATH, "Sprites");

    /// <summary>
    /// The path of the directory to which PolyMod writes the dumped prefab data.
    /// </summary>
    public static readonly string PREFABS_DUMP_PATH = Path.Combine(DUMPED_DATA_PATH, "Prefabs");

    /// <summary>
    /// The path of the PolyMod configuration file, <c>PolyMod.json</c>.
    /// </summary>
    internal static readonly string CONFIG_PATH = Path.Combine(BASE_PATH, "PolyMod.json");

    /// <summary>
    /// The path of the file that keeps the mod checksum of the last game start.
    /// </summary>
    internal static readonly string CHECKSUM_PATH
        = Path.Combine(BASE_PATH, "CHECKSUM");

    /// <summary>
    /// The path of the directory that PolyMod gives to the game as <c>Application.persistentDataPath</c>.
    /// </summary>
    internal static readonly string PERSISTENT_DATA_PATH = Path.Combine(BASE_PATH, "PersistentData");

    /// <summary>
    /// The link to the PolyMod Discord server.
    /// </summary>
    internal static readonly string DISCORD_LINK = "https://discord.gg/eWPdhWtfVy";

    /// <summary>
    /// The text parts of Unity log messages that PolyMod does not write to the log.
    /// If a log message contains one of these texts, PolyMod removes the message.
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
    /// The current configuration of PolyMod. <see cref="Load"/> reads it from <see cref="CONFIG_PATH"/>.
    /// </summary>
    internal static PolyConfig config;

    /// <summary>
    /// The BepInEx log source of PolyMod.
    /// </summary>
    internal static ManualLogSource logger;
#pragma warning restore CS8618

    /// <summary>
    /// Starts PolyMod. BepInEx calls this method when it loads the plugin.
    /// </summary>
    /// <remarks>
    /// The method reads the configuration from <see cref="CONFIG_PATH"/>.
    /// If the file is missing or not valid, it uses the default configuration.
    /// Then it writes the configuration back to the file and updates the console.
    /// It sets the BepInEx <c>Logging.Disk.WriteUnityLog</c> setting to true and calls <see cref="CaptureLaunchData"/>.
    /// If <see cref="Disabled"/> is true, the method stops there.
    /// Otherwise, it starts the PolyMod managers.
    /// </remarks>
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

        if (Disabled)
            return;

        Compatibility.Init();

        Audio.Init();
        Loc.Init();

        Visual.Init();
        Prefabs.Init();
        TribePreviews.Init();

        Hub.Init();
        Main.Init();
    }

    /// <summary>
    /// Gets the launch data from the PolyMod launcher through the named pipe <c>PolyMod.Launcher</c>.
    /// </summary>
    /// <remarks>
    /// The method waits a maximum of <see cref="LAUNCH_DATA_TIMEOUT"/> milliseconds to connect.
    /// It reads one line of JSON and sets <see cref="ValidLaunch"/>, <see cref="Disabled"/> and <see cref="Token"/> from it.
    /// If the line is empty, the method stops and does not change these values.
    /// If an error occurs, the method sets <see cref="ValidLaunch"/> to false.
    /// In all cases other than an empty line, the method then sets <see cref="ValidLaunch"/> to true.
    /// </remarks>
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
    /// Opens a stream to a resource that is embedded in the PolyMod assembly.
    /// The full resource name is <c>PolyMod.resources.</c> followed by <paramref name="id"/>.
    /// </summary>
    /// <param name="id">The file name of the resource, for example <c>localization.json</c>.</param>
    /// <returns>The stream of the resource. The value is null if the resource does not exist.</returns>
    internal static Stream GetResource(string id)
    {
        return Assembly.GetExecutingAssembly().GetManifestResourceStream(
            $"{typeof(Plugin).Namespace}.resources.{id}"
        )!;
    }

    /// <summary>
    /// Writes the current <see cref="config"/> as JSON to <see cref="CONFIG_PATH"/>.
    /// </summary>
    internal static void WriteConfig()
    {
        File.WriteAllText(CONFIG_PATH, JsonSerializer.Serialize(config));
    }

    /// <summary>
    /// Shows or hides the BepInEx console. The <c>debug</c> value of <see cref="config"/> controls this.
    /// If <c>debug</c> is true, the method makes the console.
    /// If <c>debug</c> is false, the method detaches the console on Windows and does nothing on other operating systems.
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
