using HarmonyLib;
using PolytopiaBackendBase;
using PolytopiaBackendBase.Auth;
using UnityEngine;

namespace PolyMod.Android;

public static class AndroidHandler
{
    internal static void Init()
    {
        if (Application.platform != RuntimePlatform.Android) return;

        Harmony.CreateAndPatchAll(typeof(AndroidHandler));
    }

    /// <summary>
    /// On Android, bypass multiplayer requirements that depend on
    /// Google Play login, push notifications, and purchases — none of which work
    /// when running as a wrapper app with a different package identity.
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(GameManager), nameof(GameManager.IsMultiplayerEnabled), MethodType.Getter)]
    public static bool GameManager_IsMultiplayerEnabled(ref bool __result)
    {
        __result = true;
        return false;
    }

    /// <summary>
    /// Replace the Android login flow to skip Google Play Games SDK entirely.
    /// Uses deviceUniqueIdentifier as the auth code for the Polydystopia backend.
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(PolytopiaBackendAdapter), "LoginPlatformAndroid")]
    public static bool LoginPlatformAndroid_Prefix(
        ref Il2CppSystem.Threading.Tasks.Task<ServerResponse<PolytopiaToken>> __result,
        PolytopiaBackendAdapter __instance)
    {
        // Mark social login as cached so the post-login flow doesn't bail out
        __instance.HasSocialLoginCached = true;

        var model = new LoginGooglePlayBindingModel();
        model.AuthCode = SystemInfo.deviceUniqueIdentifier;
        model.DeviceId = SystemInfo.deviceUniqueIdentifier;

        Plugin.logger.LogInfo($"Multiplayer> Android login with DeviceId: {model.DeviceId}");
        __result = __instance.LoginGooglePlay(model);
        return false;
    }

    /// <summary>
    /// On android Firebase cannot initialize inside the launcher process (its config lives in the game APK's resources, and the native lib may be unreachable there).
    /// We try to skip Firebase completely.
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(AnalyticsManager), nameof(AnalyticsManager.IsAnalyticsEnabled))]
    private static bool AnalyticsManager_IsAnalyticsEnabled(ref bool __result)
    {
        __result = false;
        return false;
    }

    /// <summary>
    /// On android Firebase cannot initialize inside the launcher process (its config lives in the game APK's resources, and the native lib may be unreachable there).
    /// We try to skip Firebase completely. isFirebaseInitialized deliberately stays false:
    /// pretending Firebase is up could wake isFirebaseInitialized-guarded code paths
    /// (e.g. HandleOpenedThroughNotification on every app resume).
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(FirebaseMessagingManager), nameof(FirebaseMessagingManager.Init))]
    private static bool FirebaseMessagingManager_Init()
    {
        return false;
    }

    /// <summary>
    /// RequestPushNotificationPermissions (the push-notification row in LoginDetails) calls
    /// InitAsync directly, bypassing Init — with isFirebaseInitialized kept false that would
    /// still reach Firebase, so hand back a completed task instead.
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(FirebaseMessagingManager), nameof(FirebaseMessagingManager.InitAsync))]
    private static bool FirebaseMessagingManager_InitAsync(ref Il2CppSystem.Threading.Tasks.Task __result)
    {
        __result = Il2CppSystem.Threading.Tasks.Task.CompletedTask;
        return false;
    }
}