using HarmonyLib;
using Polytopia.Data;
using PolytopiaBackendBase.Common;

namespace PolyMod.Multiplayer;

/// <summary>
/// Serialization fixes for custom (modded) content.
///
/// GamePlayerSummary stores the tribe as a single byte, which custom tribe ids (>= 1000) overflow into garbage.
/// Rewriting the record in managed code is not an option. Reading Il2CppSystem.Nullable<Guid> members (PolytopiaId) from managed patches returns corrupted guids.
/// Instead the tribe is clamped to None before the untouched native serializer runs and menu summaries show a generic icon for custom tribes, nothing more.
/// </summary>
public static class SerializationUtils
{
    internal static void Init()
    {
        Harmony.CreateAndPatchAll(typeof(SerializationUtils));
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(GamePlayerSummary), nameof(GamePlayerSummary.Serialize))]
    public static bool GamePlayerSummary_Serialize(GamePlayerSummary __instance)
    {
        if ((int)__instance.TribeType >= byte.MaxValue)
        {
            __instance.TribeType = TribeType.None;
        }

        return true;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PlayerState), nameof(PlayerState.Deserialize))]
    public static void PlayerState_Deserialize(PlayerState __instance, Il2CppSystem.IO.BinaryReader reader, int version)
    {
        if ((int)__instance.tribe >= Plugin.AUTOIDX_STARTS_FROM)
        {
            __instance.climate = __instance.tribe;
        }
    }
}
