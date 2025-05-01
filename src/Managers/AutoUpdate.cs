using System.Diagnostics;
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
                new Version(latest?.GetProperty("tag_name").GetString()!.TrimStart('v')!)
                <=
                new Version(Plugin.VERSION)
            ) return;
            void Update()
            {
                File.WriteAllBytes(
                    Path.Combine(Plugin.BASE_PATH, "BepInEx", "plugins", "PolyMod.new.dll"),
                    client.GetAsync(latest?.GetProperty("assets")[0].GetProperty("browser_download_url").GetString()!).UnwrapAsync()
                    .Content.ReadAsByteArrayAsync().UnwrapAsync()
                );
                ProcessStartInfo info = new()
                {
                    WorkingDirectory = Path.Combine(Plugin.BASE_PATH, "BepInEx", "plugins"),
                    CreateNoWindow = true,
                };
                if (Application.platform == RuntimePlatform.WindowsPlayer)
                {
                    info.FileName = "cmd.exe";
                    info.Arguments
                        = "/C timeout 3 && del /F /Q PolyMod.dll && move /Y PolyMod.new.dll PolyMod.dll && start steam://rungameid/874390";
                }
                else
                {
                    info.FileName = "/bin/bash";
                    info.Arguments
                        = "-c 'sleep 3 && rm -f PolyMod.dll && mv PolyMod.new.dll PolyMod.dll && xdg-open steam://rungameid/874390'";
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
                        (Il2CppSystem.Action)Update,
                        closesPopup: false
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