using HarmonyLib;

namespace PolyMod.Managers
{
    public class ValidationManager
    {
        [HarmonyPrefix]
		[HarmonyPatch(typeof(BuildCommand), nameof(BuildCommand.IsValid))]
        [HarmonyPatch(typeof(AttackCommand), nameof(AttackCommand.IsValid))]
        [HarmonyPatch(typeof(RecoverCommand), nameof(RecoverCommand.IsValid))]
        [HarmonyPatch(typeof(HealOthersCommand), nameof(HealOthersCommand.IsValid))]
        [HarmonyPatch(typeof(TrainCommand), nameof(TrainCommand.IsValid))]
        [HarmonyPatch(typeof(MoveCommand), nameof(MoveCommand.IsValid))]
        [HarmonyPatch(typeof(CaptureCommand), nameof(CaptureCommand.IsValid))]
        [HarmonyPatch(typeof(ResearchCommand), nameof(ResearchCommand.IsValid))]
        [HarmonyPatch(typeof(DestroyCommand), nameof(DestroyCommand.IsValid))]
        [HarmonyPatch(typeof(DisbandCommand), nameof(DisbandCommand.IsValid))]
		[HarmonyPatch(typeof(CityRewardCommand), nameof(CityRewardCommand.IsValid))]
        [HarmonyPatch(typeof(PromoteCommand), nameof(PromoteCommand.IsValid))]
        [HarmonyPatch(typeof(ExamineRuinsCommand), nameof(ExamineRuinsCommand.IsValid))]
        [HarmonyPatch(typeof(EndTurnCommand), nameof(EndTurnCommand.IsValid))]
        [HarmonyPatch(typeof(UpgradeCommand), nameof(UpgradeCommand.IsValid))]
        [HarmonyPatch(typeof(FreezeAreaCommand), nameof(FreezeAreaCommand.IsValid))]
        [HarmonyPatch(typeof(BreakIceCommand), nameof(BreakIceCommand.IsValid))]
        [HarmonyPatch(typeof(StartMatchCommand), nameof(StartMatchCommand.IsValid))]
        [HarmonyPatch(typeof(StayCommand), nameof(StayCommand.IsValid))]
        [HarmonyPatch(typeof(EndMatchCommand), nameof(EndMatchCommand.IsValid))]
		[HarmonyPatch(typeof(ExplodeCommand), nameof(ExplodeCommand.IsValid))]
        [HarmonyPatch(typeof(BoostCommand), nameof(BoostCommand.IsValid))]
        [HarmonyPatch(typeof(DecomposeCommand), nameof(DecomposeCommand.IsValid))]
        [HarmonyPatch(typeof(PeaceTreatyCommand), nameof(PeaceTreatyCommand.IsValid))]
        [HarmonyPatch(typeof(PeaceRequestResponseCommand), nameof(PeaceRequestResponseCommand.IsValid))]
        [HarmonyPatch(typeof(BreakPeaceCommand), nameof(BreakPeaceCommand.IsValid))]
        [HarmonyPatch(typeof(EstablishEmbassyCommand), nameof(EstablishEmbassyCommand.IsValid))]
        [HarmonyPatch(typeof(ResignCommand), nameof(ResignCommand.IsValid))]
		private static bool IsValid(ref bool __result, GameState state, ref string validationError)
		{
            __result = true;
            return false;
		}

		internal static void Init()
		{
			Harmony.CreateAndPatchAll(typeof(VisualManager));
		}
    }
}