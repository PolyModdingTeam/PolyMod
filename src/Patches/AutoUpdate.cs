using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using HarmonyLib;
using UnityEngine;

namespace PolyMod.Patches;

internal static class AutoUpdate
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(StartScreen), nameof(StartScreen.Start))]
    private static void StartScreen_Start()
    {
        if (!Plugin.config.autoUpdate) return;
        if (Environment.GetEnvironmentVariable("WINEPREFIX") != null)
        {
            Plugin.logger.LogError("Autoupdate is not supported on Wine!");
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
            string newVersion = latest?.GetProperty("tag_name").GetString()!.TrimStart('v')!;
            if (newVersion.IsVersionOlderOrEqual(Constants.POLYMOD_VERSION)) return;
            string os = Application.platform switch
            {
                RuntimePlatform.WindowsPlayer => "win",
                RuntimePlatform.LinuxPlayer => "linux",
                RuntimePlatform.OSXPlayer => "macos",
                _ => "unknown",
            };
            if (os == "unknown")
            { 
                Plugin.logger.LogError("Unsupported platform for autoupdate!");
                return;
            }
            string bepinex_url = client
                .GetAsync("https://polymod.dev/data/bepinex.txt").UnwrapAsync()
                .Content.ReadAsStringAsync().UnwrapAsync()
                .Replace("{os}", os);
            void Update()
            {
                Time.timeScale = 0;
                File.WriteAllBytes(
                    Path.Combine(Constants.BASE_PATH, "PolyMod.new.dll"),
                    client.GetAsync(latest?.GetProperty("assets")[0].GetProperty("browser_download_url").GetString()!).UnwrapAsync()
                    .Content.ReadAsByteArrayAsync().UnwrapAsync()
                );
                using ZipArchive bepinex = new(client.GetAsync(bepinex_url).UnwrapAsync().Content.ReadAsStream());
                bepinex.ExtractToDirectory(Path.Combine(Constants.BASE_PATH, "New"), overwriteFiles: true);
                ProcessStartInfo info = new()
                {
                    WorkingDirectory = Path.Combine(Constants.BASE_PATH),
                    CreateNoWindow = true,
                };
                if (Application.platform == RuntimePlatform.WindowsPlayer)
                {
                    string batchPath = Path.Combine(Constants.BASE_PATH, "update.bat");
                    File.WriteAllText(batchPath, $@"
                        @echo off
                        echo Waiting for Polytopia.exe to exit...
                        :waitloop
                        tasklist | findstr /I ""Polytopia.exe"" >nul
                        if not errorlevel 1 (
                            timeout /T 1 >nul
                            goto waitloop
                        )

                        echo Updating...
                        robocopy ""New"" . /E /MOVE /NFL /NDL /NJH /NJS /NP >nul
                        rmdir /S /Q ""New""
                        del /F /Q ""BepInEx\plugins\PolyMod.dll""
                        move /Y ""PolyMod.new.dll"" ""BepInEx\plugins\PolyMod.dll""

                        echo Launching game...
                        start steam://rungameid/874390
                        timeout /T 3 /NOBREAK >nul
                        exit
                    ");
                    info.FileName = "cmd.exe";
                    info.Arguments = $"/C start \"\" \"{batchPath}\"";
                    info.WorkingDirectory = Constants.BASE_PATH;
                    info.CreateNoWindow = true;
                    info.UseShellExecute = false;
                }
                if (Application.platform == RuntimePlatform.LinuxPlayer || Application.platform == RuntimePlatform.OSXPlayer)
                {
                    string bashPath = Path.Combine(Constants.BASE_PATH, "update.sh");
                    File.WriteAllText(bashPath, $@"
                        #!/bin/bash

                        echo ""Waiting for Polytopia to exit...""
                        while pgrep -x ""Polytopia"" > /dev/null; do
                            sleep 1
                        done

                        echo ""Updating...""
                        mv New/* . && rm -rf New
                        rm -f BepInEx/plugins/PolyMod.dll
                        mv -f PolyMod.new.dll BepInEx/plugins/PolyMod.dll

                        echo ""Launching game...""
                        xdg-open steam://rungameid/874390 &

                        sleep 3
                        exit 0
                    ");

                    System.Diagnostics.Process chmod = new System.Diagnostics.Process();
                    chmod.StartInfo.FileName = "chmod";
                    chmod.StartInfo.Arguments = $"+x \"{bashPath}\"";
                    chmod.StartInfo.UseShellExecute = false;
                    chmod.StartInfo.CreateNoWindow = true;
                    chmod.Start();
                    chmod.WaitForExit();

                    info.FileName = "/bin/bash";
                    info.Arguments = $"\"{bashPath}\"";
                    info.WorkingDirectory = Constants.BASE_PATH;
                    info.CreateNoWindow = true;
                    info.UseShellExecute = false;
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