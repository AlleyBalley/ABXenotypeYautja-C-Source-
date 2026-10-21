using HarmonyLib;
using RimWorld;
using Verse;

namespace ABXenotypeYautja.BloodingSystem
{
    /// <summary>
    /// Harmony Postfix intercepting pawn kill notifications.
    /// 
    /// Patched Method: RecordsUtility.Notify_PawnKilled(Pawn killed, Pawn killer)
    /// Patch Type: Postfix
    /// 
    /// This is the same clean hook point utilized by the original HunterMarkingSystem.
    /// RecordsUtility.Notify_PawnKilled is triggered by vanilla RimWorld whenever a pawn
    /// dies from damage inflicted by another pawn (melee or ranged).
    /// </summary>
    [HarmonyPatch(typeof(RecordsUtility), nameof(RecordsUtility.Notify_PawnKilled))]
    public static class BloodingHarmonyPatch
    {
        /// <summary>
        /// Postfix executed after RecordsUtility.Notify_PawnKilled.
        /// Fully guarded against null references (environmental deaths, missing defs, non-colonists).
        /// </summary>
        /// <param name="killed">The slain pawn.</param>
        /// <param name="killer">The killer pawn (null if killed by environmental hazard, bleedout without source, etc.).</param>
        [HarmonyPostfix]
        public static void OnPawnKilled(Pawn killed, Pawn killer)
        {
            // Guard 1: Valid killer exists
            if (killer == null)
                return;

            // Guard 2: Killer is an eligible colonist carrying the active Blooding gene
            if (!BloodingUtility.IsEligibleForBlooding(killer))
                return;

            // Guard 3: Target has valid race definitions
            if (killed?.def?.race == null)
                return;

            // Guard 4: Prevent progression from team/faction fratricide
            if (killed.Faction != null && killed.Faction == killer.Faction)
                return;

            // Guard 5: Verify kill meets power threshold
            if (!BloodingUtility.IsWorthyKill(killed, killer, out float killedScore))
                return;

            // Apply progression
            BloodingUtility.ApplyOrEvolveBloodingHediff(killer, killed, killedScore);
        }
    }
}
