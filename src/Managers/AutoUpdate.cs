using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using HarmonyLib;
using UnityEngine;

namespace PolyMod.Managers;

internal static class AutoUpdate
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(StartScreen), nameof(StartScreen.Start))]
    private static void StartScreen_Start()
    {
        if (!Plugin.config.autoUpdate) return;
        if (Environment.GetEnvironmentVariable("WINEPREFIX") != null)
        {
            Plugin.logger.LogWarning("Wine/Proton is not supported!");
            return;
        }
        HttpClient client = new();
        client.DefaultRequestHeaders.Add("User-Agent", "PolyMod");
        try
        {
            var json = JsonDocument.Parse(
                client.GetAsync("https://api.github.com/repos/PolyModdingTeam/PolyMod/releases").UnwrapAsync()
                .Content.ReadAsStringAsync().UnwrapAsync()
            );
            JsonElement? latest = null;
            for (int i = 0; i < json.RootElement.GetArrayLength(); i++)
            {
                var release = json.RootElement[i];
                if (release.GetProperty("prerelease").GetBoolean() && !Plugin.config.updatePrerelease) continue;
                latest = release;
                break;
            }
            if (
                new Version(latest?.GetProperty("tag_name").GetString()!.TrimStart('v')!.Split('-')[0]!)
                <=
                new Version(Plugin.VERSION.Split('-')[0])
            ) return;
            string bepinex_version = client.GetAsync("https://polymod.dev/data/bepinex.txt").UnwrapAsync().Content.ReadAsStringAsync().UnwrapAsync();
            string os = Application.platform switch
            {
                RuntimePlatform.WindowsPlayer => "win",
                RuntimePlatform.LinuxPlayer => "linux",
                RuntimePlatform.OSXPlayer => "macos",
                _ => "unknown",
            };
            if (os == "unknown") return;
            void Update()
            {
                Time.timeScale = 0;
                File.WriteAllBytes(
                    Path.Combine(Plugin.BASE_PATH, "PolyMod.new.dll"),
                    client.GetAsync(latest?.GetProperty("assets")[0].GetProperty("browser_download_url").GetString()!).UnwrapAsync()
                    .Content.ReadAsByteArrayAsync().UnwrapAsync()
                );
                using ZipArchive bepinex = new(client.GetAsync(bepinex_version).UnwrapAsync().Content.ReadAsStream());
                bepinex.ExtractToDirectory(Path.Combine(Plugin.BASE_PATH, "New"));
                ProcessStartInfo info = new()
                {
                    WorkingDirectory = Path.Combine(Plugin.BASE_PATH),
                    CreateNoWindow = true,
                };
                if (Application.platform == RuntimePlatform.WindowsPlayer)
                {
                    info.FileName = "cmd.exe";
                    info.Arguments =
                        "/C timeout 3" +
                        " && robocopy \"New\" . /E /MOVE /NFL /NDL /NJH /NJS /NP" +
                        " && rmdir /S /Q \"New\"" +
                        " && del /F /Q \"BepInEx\\plugins\\PolyMod.dll\"" +
                        " && move /Y \"PolyMod.new.dll\" \"BepInEx\\plugins\\PolyMod.dll\"" +
                        " && start steam://rungameid/874390";
                }
                else
                {
                    info.FileName = "/bin/bash";
                    info.Arguments =
                        "-c 'sleep 3" +
                        " && mv -f New/* . && mv -f New/.* . 2>/dev/null || true && rm -rf New" +
                        " && rm -f BepInEx/plugins/PolyMod.dll" +
                        " && mv -f PolyMod.new.dll BepInEx/plugins/PolyMod.dll" +
                        " && xdg-open steam://rungameid/874390'";
                }
                Process.Start(info);
                Application.Quit();
            }
            PopupManager.GetBasicPopup(new(
                Localization.Get("polymod.autoupdate"),
                Localization.Get("polymod.autoupdate.description"),
                new(new PopupBase.PopupButtonData[] {
                    new(
                        "polymod.autoupdate.update",
                        PopupBase.PopupButtonData.States.None,
                        (Il2CppSystem.Action)Update
                    )
                }))
            ).Show();
        }
        catch (Exception e)
        {
            Plugin.logger.LogError($"Failed to check updates: {e.Message}");
        }
    }

    internal static void Init()
    {
        Harmony.CreateAndPatchAll(typeof(AutoUpdate));
    }
}