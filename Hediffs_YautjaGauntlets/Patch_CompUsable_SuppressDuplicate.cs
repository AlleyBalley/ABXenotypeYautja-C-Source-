using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ABXenotypeYautja
{
    /// <summary>
    /// Harmony patch suppressing the default generic CompUsable float menu option
    /// when the item contains our specialized CompUseEffect_InstallGauntlet.
    /// This prevents duplicate menu options and unifies the float menu
    /// to display only our clean, limb-specific installation commands.
    /// Strictly compatible with C# 7.3 (.NET Framework 4.7.2) and RimWorld 1.6+.
    /// </summary>
    [HarmonyPatch(typeof(CompUsable), nameof(CompUsable.CompFloatMenuOptions))]
    public static class Patch_CompUsable_SuppressDuplicate
    {
        [HarmonyPrefix]
        public static bool Prefix(CompUsable __instance, Pawn myPawn, ref IEnumerable<FloatMenuOption> __result)
        {
            if (__instance != null && __instance.parent != null && __instance.parent.GetComp<CompUseEffect_InstallGauntlet>() != null)
            {
                __result = Enumerable.Empty<FloatMenuOption>();
                return false;
            }
            return true;
        }
    }
}
