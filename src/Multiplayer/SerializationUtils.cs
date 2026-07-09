using HarmonyLib;
using Polytopia.Data;
using PolytopiaBackendBase.Common;

namespace PolyMod.Multiplayer;

public static class SerializationUtils
{
    internal static void Init()
    {
        Harmony.CreateAndPatchAll(typeof(SerializationUtils));
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(GamePlayerSummary), nameof(GamePlayerSummary.Serialize))]
	public static bool GamePlayerSummary_Serialize(GamePlayerSummary __instance, Il2CppSystem.IO.BinaryWriter writer, int version)
	{
        Plugin.logger.LogInfo("Multiplayer> GamePlayerSummary_Serialize");
		var memoryStream = new Il2CppSystem.IO.MemoryStream();
		var binaryWriter = new Il2CppSystem.IO.BinaryWriter(memoryStream);
		binaryWriter.Write(__instance.Id);
		binaryWriter.Write(__instance.PolytopiaId.ToString());
		binaryWriter.Write(__instance.UserName ?? "");
		binaryWriter.Write((int)__instance.TribeType);
		binaryWriter.Write(__instance.AutoPlay);
		binaryWriter.Write(__instance.HasChosenTribe);
		binaryWriter.Write(__instance.Handicap);
		binaryWriter.Write(__instance.IsDead);
		if (version >= 86)
		{
			binaryWriter.Write((int)__instance.SkinType);
		}
		writer.Write((int)memoryStream.Length);
		memoryStream.WriteTo(writer.BaseStream);
        return false;
	}

    [HarmonyPrefix]
    [HarmonyPatch(typeof(GamePlayerSummary), nameof(GamePlayerSummary.Deserialize))]
	public static bool GamePlayerSummary_Deserialize(GamePlayerSummary __instance, Il2CppSystem.IO.BinaryReader reader, int version)
	{
        Plugin.logger.LogInfo("Multiplayer> GamePlayerSummary_Deserialize");
		int num = reader.ReadInt32();
		long position = reader.BaseStream.Position;
		__instance.Id = reader.ReadByte();
		string g = reader.ReadString();
        Il2CppSystem.Guid parsed;
        Il2CppSystem.Nullable<Il2CppSystem.Guid> nullableGuid;
        if (Il2CppSystem.Guid.TryParse(g, out parsed))
            nullableGuid = new Il2CppSystem.Nullable<Il2CppSystem.Guid>(parsed);
        else
            nullableGuid = new Il2CppSystem.Nullable<Il2CppSystem.Guid>();
        __instance.PolytopiaId = nullableGuid;
		__instance.UserName = reader.ReadString();
		__instance.TribeType = (TribeType)reader.ReadInt32();
		__instance.AutoPlay = reader.ReadBoolean();
		__instance.HasChosenTribe = reader.ReadBoolean();
		__instance.Handicap = reader.ReadInt32();
		__instance.IsDead = reader.ReadBoolean();
		if (version >= 86)
		{
			__instance.SkinType = (SkinType)reader.ReadInt32();
		}
		reader.BaseStream.Position = position + num;
        return false;
	}

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PlayerState), nameof(PlayerState.Deserialize))]
	public static void PlayerState_Deserialize(PlayerState __instance, Il2CppSystem.IO.BinaryReader reader, int version)
	{
		__instance.climate = __instance.tribe;
	}
}