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

    // [HarmonyPrefix]
    // [HarmonyPatch(typeof(GamePlayerSummary), nameof(GamePlayerSummary.Serialize))]
	// public static bool GamePlayerSummary_Serialize(GamePlayerSummary __instance, Il2CppSystem.IO.BinaryWriter writer, int version)
	// {
    //     Plugin.logger.LogInfo("Multiplayer> GamePlayerSummary_Serialize");
	// 	var memoryStream = new Il2CppSystem.IO.MemoryStream();
	// 	var binaryWriter = new Il2CppSystem.IO.BinaryWriter(memoryStream);
	// 	binaryWriter.Write(__instance.Id);
	// 	binaryWriter.Write(__instance.PolytopiaId.ToString());
	// 	binaryWriter.Write(__instance.UserName ?? "");
	// 	binaryWriter.Write((int)__instance.TribeType);
	// 	binaryWriter.Write(__instance.AutoPlay);
	// 	binaryWriter.Write(__instance.HasChosenTribe);
	// 	binaryWriter.Write(__instance.Handicap);
	// 	binaryWriter.Write(__instance.IsDead);
	// 	if (version >= 86)
	// 	{
	// 		binaryWriter.Write((int)__instance.SkinType);
	// 	}
	// 	writer.Write((int)memoryStream.Length);
	// 	memoryStream.WriteTo(writer.BaseStream);
    //     return false;
	// }

    // [HarmonyPrefix]
    // [HarmonyPatch(typeof(GamePlayerSummary), nameof(GamePlayerSummary.Deserialize))]
	// public static bool GamePlayerSummary_Deserialize(GamePlayerSummary __instance, Il2CppSystem.IO.BinaryReader reader, int version)
	// {
    //     Plugin.logger.LogInfo("Multiplayer> GamePlayerSummary_Deserialize");
	// 	int num = reader.ReadInt32();
	// 	long position = reader.BaseStream.Position;
	// 	__instance.Id = reader.ReadByte();
	// 	string g = reader.ReadString();
    //     Il2CppSystem.Guid parsed;
    //     Il2CppSystem.Nullable<Il2CppSystem.Guid> nullableGuid;
    //     if (Il2CppSystem.Guid.TryParse(g, out parsed))
    //         nullableGuid = new Il2CppSystem.Nullable<Il2CppSystem.Guid>(parsed);
    //     else
    //         nullableGuid = new Il2CppSystem.Nullable<Il2CppSystem.Guid>();
    //     __instance.PolytopiaId = nullableGuid;
	// 	__instance.UserName = reader.ReadString();
	// 	__instance.TribeType = (TribeType)reader.ReadInt32();
	// 	__instance.AutoPlay = reader.ReadBoolean();
	// 	__instance.HasChosenTribe = reader.ReadBoolean();
	// 	__instance.Handicap = reader.ReadInt32();
	// 	__instance.IsDead = reader.ReadBoolean();
	// 	if (version >= 86)
	// 	{
	// 		__instance.SkinType = (SkinType)reader.ReadInt32();
	// 	}
	// 	reader.BaseStream.Position = position + num;
    //     return false;
	// }

    // [HarmonyPrefix]
    // [HarmonyPatch(typeof(PlayerState), nameof(PlayerState.Serialize))]
	// public static bool PlayerState_Serialize(PlayerState __instance, Il2CppSystem.IO.BinaryWriter writer, int version)
	// {
	// 	writer.Write(__instance.Id);
	// 	if (__instance.UserName == null)
	// 	{
	// 		__instance.UserName = "";
	// 	}
	// 	writer.Write(__instance.UserName);
	// 	writer.Write(__instance.AccountId.ToString());
	// 	writer.Write(__instance.AutoPlay);
	// 	__instance.startTile.Serialize(writer, version);
	// 	writer.Write((ushort)__instance.tribe);
	// 	writer.Write(__instance.hasChosenTribe);
	// 	writer.Write(__instance.handicap);
	// 	if (version < 113)
	// 	{
	// 		writer.Write((ushort)((__instance.aggressions != null) ? ((uint)__instance.aggressions.Count) : 0u));
	// 		foreach (Il2CppSystem.Collections.Generic.KeyValuePair<byte, int> aggression in __instance.aggressions)
	// 		{
	// 			writer.Write(aggression.Key);
	// 			writer.Write(aggression.Value);
	// 		}
	// 	}
	// 	writer.Write(__instance.currency);
	// 	writer.Write(__instance.score);
	// 	writer.Write(__instance.endScore);
	// 	writer.Write((ushort)__instance.cities);
	// 	writer.Write((ushort)((__instance.availableTech != null) ? ((uint)__instance.availableTech.Count) : 0u));
	// 	if (__instance.availableTech != null)
	// 	{
	// 		for (int i = 0; i < __instance.availableTech.Count; i++)
	// 		{
	// 			writer.Write((ushort)__instance.availableTech[i]);
	// 		}
	// 	}
	// 	writer.Write((ushort)((__instance.knownPlayers != null) ? ((uint)__instance.knownPlayers.Count) : 0u));
	// 	if (__instance.knownPlayers != null)
	// 	{
	// 		for (int j = 0; j < __instance.knownPlayers.Count; j++)
	// 		{
	// 			writer.Write(__instance.knownPlayers[j]);
	// 		}
	// 	}
	// 	ushort num = (ushort)((__instance.tasks != null) ? ((uint)__instance.tasks.Count) : 0u);
	// 	writer.Write(num);
	// 	for (int k = 0; k < num; k++)
	// 	{
	// 		TaskBase.SerializeTask(__instance.tasks[k], writer, version);
	// 	}
	// 	writer.Write(__instance.kills);
	// 	writer.Write(__instance.casualities);
	// 	writer.Write(__instance.wipeOuts);
	// 	writer.Write(__instance.color);
	// 	writer.Write((byte)__instance.tribeMix);
	// 	writer.Write((ushort)((__instance.builtUniqueImprovements != null) ? ((uint)__instance.builtUniqueImprovements.Count) : 0u));
	// 	if (__instance.builtUniqueImprovements != null)
	// 	{
	// 		for (int l = 0; l < __instance.builtUniqueImprovements.Count; l++)
	// 		{
	// 			writer.Write((short)__instance.builtUniqueImprovements[l]);
	// 		}
	// 	}
	// 	if (version < 60)
	// 	{
	// 		return false;
	// 	}
	// 	writer.Write((ushort)__instance.relations.Count);
	// 	foreach (Il2CppSystem.Collections.Generic.KeyValuePair<byte, DiplomacyRelation> relation in __instance.relations)
	// 	{
	// 		writer.Write(relation.Key);
	// 		relation.Value.Serialize(writer, version);
	// 	}
	// 	writer.Write((ushort)__instance.messages.Count);
	// 	foreach (DiplomacyMessage message in __instance.messages)
	// 	{
	// 		message.Serialize(writer, version);
	// 	}
	// 	writer.Write(__instance.killerId);
	// 	writer.Write(__instance.killedTurn);
	// 	if (version < 70)
	// 	{
	// 		return false;
	// 	}
	// 	writer.Write(__instance.resignedAtCommandIndex);
	// 	writer.Write(__instance.wipedAtCommandIndex);
	// 	if (version < 86)
	// 	{
	// 		return false;
	// 	}
	// 	writer.Write((ushort)__instance.skinType);
	// 	if (version >= 93)
	// 	{
	// 		writer.Write(__instance.resignedTurn);
	// 		if (version >= 121)
	// 		{
	// 			writer.Write((int)__instance.climate);
	// 		}
	// 	}
    //     return false;
	// }

    // [HarmonyPrefix]
    // [HarmonyPatch(typeof(PlayerState), nameof(PlayerState.Deserialize))]
	// public static bool Deserialize(PlayerState __instance, Il2CppSystem.IO.BinaryReader reader, int version)
	// {
	// 	__instance.Id = reader.ReadByte();
	// 	__instance.UserName = reader.ReadString();
	// 	string g = reader.ReadString();
    //     Il2CppSystem.Guid parsed;
    //     Il2CppSystem.Nullable<Il2CppSystem.Guid> nullableGuid;
    //     if (Il2CppSystem.Guid.TryParse(g, out parsed))
    //         nullableGuid = new Il2CppSystem.Nullable<Il2CppSystem.Guid>(parsed);
    //     else
    //         nullableGuid = new Il2CppSystem.Nullable<Il2CppSystem.Guid>();
	// 	__instance.AccountId = nullableGuid;
	// 	__instance.AutoPlay = reader.ReadBoolean();
	// 	__instance.startTile = new WorldCoordinates(reader, version);
	// 	__instance.tribe = (TribeType)reader.ReadUInt16();
	// 	__instance.climate = __instance.tribe;
	// 	__instance.hasChosenTribe = reader.ReadBoolean();
	// 	__instance.handicap = reader.ReadInt32();
	// 	if (version < 113)
	// 	{
	// 		int num = reader.ReadUInt16();
	// 		for (int i = 0; i < num; i++)
	// 		{
	// 			byte key = reader.ReadByte();
	// 			__instance.aggressions[key] = reader.ReadInt32();
	// 		}
	// 	}
	// 	__instance.currency = reader.ReadInt32();
	// 	__instance.score = reader.ReadUInt32();
	// 	__instance.endScore = reader.ReadUInt32();
	// 	__instance.cities = reader.ReadUInt16();
	// 	int num2 = reader.ReadUInt16();
	// 	if (__instance.availableTech == null || __instance.availableTech.Count < num2)
	// 	{
	// 		__instance.availableTech = new Il2CppSystem.Collections.Generic.List<TechData.Type>(num2);
	// 	}
	// 	for (int j = 0; j < num2; j++)
	// 	{
	// 		if (j < __instance.availableTech.Count)
	// 		{
	// 			__instance.availableTech[j] = (TechData.Type)reader.ReadUInt16();
	// 		}
	// 		else
	// 		{
	// 			__instance.availableTech.Add((TechData.Type)reader.ReadUInt16());
	// 		}
	// 	}
	// 	int num3 = reader.ReadUInt16();
	// 	if (__instance.knownPlayers == null || __instance.knownPlayers.Count < num3)
	// 	{
	// 		__instance.knownPlayers = new Il2CppSystem.Collections.Generic.List<byte>();
	// 	}
	// 	for (int k = 0; k < num3; k++)
	// 	{
	// 		if (k < __instance.knownPlayers.Count)
	// 		{
	// 			__instance.knownPlayers[k] = reader.ReadByte();
	// 		}
	// 		else
	// 		{
	// 			__instance.knownPlayers.Add(reader.ReadByte());
	// 		}
	// 	}
	// 	ushort num4 = reader.ReadUInt16();
	// 	if (__instance.tasks == null)
	// 	{
	// 		__instance.tasks = new Il2CppSystem.Collections.Generic.List<TaskBase>(num4);
	// 	}
	// 	else
	// 	{
	// 		__instance.tasks.Clear();
	// 	}
	// 	for (int l = 0; l < num4; l++)
	// 	{
	// 		__instance.tasks.Add(TaskBase.DeserializeTask(reader, version));
	// 	}
	// 	__instance.kills = reader.ReadUInt32();
	// 	__instance.casualities = reader.ReadUInt32();
	// 	__instance.wipeOuts = reader.ReadUInt32();
	// 	__instance.color = reader.ReadInt32();
	// 	__instance.tribeMix = (TribeType)reader.ReadByte();
	// 	ushort num5 = reader.ReadUInt16();
	// 	if (__instance.builtUniqueImprovements == null)
	// 	{
	// 		__instance.builtUniqueImprovements = new Il2CppSystem.Collections.Generic.List<ImprovementData.Type>(num5);
	// 	}
	// 	else
	// 	{
	// 		__instance.builtUniqueImprovements.Clear();
	// 	}
	// 	for (int m = 0; m < num5; m++)
	// 	{
	// 		__instance.builtUniqueImprovements.Add((ImprovementData.Type)reader.ReadInt16());
	// 	}
	// 	if (__instance.color == -1 && version < 86)
	// 	{
	// 		__instance.color = __instance.GetPlayerColor(version, __instance.tribe);
	// 	}
	// 	if (version < 60)
	// 	{
	// 		return false;
	// 	}
	// 	__instance.relations.Clear();
	// 	ushort num6 = reader.ReadUInt16();
	// 	for (int n = 0; n < num6; n++)
	// 	{
	// 		byte key2 = reader.ReadByte();
	// 		DiplomacyRelation diplomacyRelation = new DiplomacyRelation();
	// 		diplomacyRelation.Deserialize(reader, version);
	// 		__instance.relations[key2] = diplomacyRelation;
	// 	}
	// 	__instance.messages.Clear();
	// 	ushort num7 = reader.ReadUInt16();
	// 	for (int num8 = 0; num8 < num7; num8++)
	// 	{
	// 		DiplomacyMessage diplomacyMessage = new DiplomacyMessage();
	// 		diplomacyMessage.Deserialize(reader, version);
	// 		__instance.messages.Add(diplomacyMessage);
	// 	}
	// 	__instance.killerId = reader.ReadByte();
	// 	__instance.killedTurn = reader.ReadUInt32();
	// 	if (version < 70)
	// 	{
	// 		return false;
	// 	}
	// 	__instance.resignedAtCommandIndex = reader.ReadInt32();
	// 	__instance.wipedAtCommandIndex = reader.ReadInt32();
	// 	if (version < 86)
	// 	{
	// 		return false;
	// 	}
	// 	__instance.skinType = (SkinType)reader.ReadUInt16();
	// 	if (__instance.color == -1)
	// 	{
	// 		__instance.color = __instance.GetPlayerColor(version, __instance.tribe, __instance.skinType);
	// 	}
	// 	if (version >= 93)
	// 	{
	// 		__instance.resignedTurn = reader.ReadInt32();
	// 		if (version >= 121)
	// 		{
	// 			__instance.climate = (TribeType)reader.ReadInt32();
	// 		}
	// 	}
    //     return false;
	// }
}