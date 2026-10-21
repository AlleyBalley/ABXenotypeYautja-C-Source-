using System;
using RimWorld;
using UnityEngine;
using Verse;

namespace ABXenotypeYautja.BloodingSystem
{
    /// <summary>
    /// Utility class containing all calculation and progression logic for the Blooding system.
    /// Responsible for:
    ///   - Calculating the Combat Power Score of any creature.
    ///   - Determining whether a kill is "worthy".
    ///   - Calculating severity progression increments for the Blooding Hediff.
    ///   - Applying or evolving the Hediff on the hunter pawn.
    /// </summary>
    public static class BloodingUtility
    {
        // ────────────────────────────────────────────────────────────────
        // CONFIGURATION CONSTANTS
        // Adjust these values to tune gameplay balance and progression pacing.
        // ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Minimum fraction of the killer's score required for a victim to count as worthy.
        /// If victim score is lower than (killer score * MinWorthyKillFactor),
        /// the kill is ignored.
        /// Original HMS default was 0.35 (35% of killer's power).
        /// </summary>
        private const float MinWorthyKillFactor = 0.35f;

        /// <summary>
        /// Absolute minimum combat score the victim must have.
        /// Prevents trivial creatures (rats, squirrels) from granting progression,
        /// even to low-stat hunters.
        /// </summary>
        private const float MinAbsoluteScore = 5f;

        /// <summary>
        /// Divisor used to convert victim score into a severity increment.
        /// Higher values mean more kills are required to reach subsequent ranks.
        /// Example: with divisor 250, a score-50 kill awards +0.20 severity.
        /// </summary>
        private const float SeverityDivisor = 250f;

        /// <summary>
        /// Initial severity applied when a hunter secures their first worthy kill.
        /// Corresponds to the baseline of Stage 0 ("Young Blood").
        /// </summary>
        private const float InitialSeverity = 0.05f;

        /// <summary>
        /// Maximum allowed severity on the Hediff (caps at highest rank).
        /// </summary>
        private const float MaxSeverity = 1.0f;

        // ────────────────────────────────────────────────────────────────
        // COMBAT POWER SCORE CALCULATION
        // ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Calculates the Combat Power Score of a pawn (alive or recently killed).
        /// 
        /// Adapted and modernized from the original StatWorker_MarkScore formula:
        ///   Score = MeleeScore + (BaseScore + (HealthScore + SizeScore) * (1 + ArmourScore))
        /// 
        /// Operates directly on RaceProperties and Tools to avoid external XML StatDef dependencies.
        /// </summary>
        /// <param name="pawn">The pawn to evaluate.</param>
        /// <returns>Score >= 0. Returns 0 if pawn is invalid.</returns>
        public static float CalculateCombatPowerScore(Pawn pawn)
        {
            if (pawn?.def?.race == null)
                return 0f;

            ThingDef def = pawn.def;
            RaceProperties race = def.race;

            // ── Base Score ──
            // Starts at 1.0, with bonuses for humanlikes, predators, faction leaders, and intelligence.
            float baseScore = 1f;

            if (race.Humanlike)
                baseScore += 0.5f;

            if (race.predator)
                baseScore += 0.75f;

            if (pawn.kindDef != null && pawn.kindDef.factionLeader)
                baseScore += 1.0f;

            // Intelligence: ToolUser=1, Humanlike=2
            baseScore += (float)race.intelligence;

            // ── Melee Score ──
            // Sums DPS across all available tools (claws, teeth, fists, blades).
            float totalMeleePower = 0f;
            float totalCooldown = 0f;

            if (def.tools != null)
            {
                foreach (Tool tool in def.tools)
                {
                    totalMeleePower += tool.power;
                    totalCooldown += tool.cooldownTime;
                }
            }

            float meleeDPS = (totalCooldown > 0f) ? (totalMeleePower / totalCooldown) : 0f;

            // Damage multiplier based on life stage (young animals hit weaker)
            float meleeDamageFactor = 1f;
            if (pawn.ageTracker?.CurLifeStage != null)
                meleeDamageFactor = pawn.ageTracker.CurLifeStage.meleeDamageFactor;

            // Standard RimWorld abstract melee chances
            float meleeHitChance = SafeGetStatAbstract(def, StatDefOf.MeleeHitChance);
            float meleeDodgeChance = SafeGetStatAbstract(def, StatDefOf.MeleeDodgeChance);

            float meleeScore = meleeDPS * meleeDamageFactor * (1f * (meleeHitChance + meleeDodgeChance));

            // ── Health Score ──
            float baseHealthScale = race.baseHealthScale;
            float healthScaleFactor = 1f;
            if (pawn.ageTracker?.CurLifeStage != null)
                healthScaleFactor = pawn.ageTracker.CurLifeStage.healthScaleFactor;

            float healthScore = baseHealthScale * healthScaleFactor;

            // ── Size Score ──
            float baseBodySize = race.baseBodySize;
            float bodySizeFactor = 1f;
            if (pawn.ageTracker?.CurLifeStage != null)
                bodySizeFactor = pawn.ageTracker.CurLifeStage.bodySizeFactor;

            float sizeScore = baseBodySize * bodySizeFactor * 0.75f;

            // ── Armour Score ──
            float armorBlunt = SafeGetStatAbstract(def, StatDefOf.ArmorRating_Blunt);
            float armorSharp = SafeGetStatAbstract(def, StatDefOf.ArmorRating_Sharp);
            float armorHeat = SafeGetStatAbstract(def, StatDefOf.ArmorRating_Heat);
            float armourScore = (armorBlunt + armorSharp + armorHeat) / 3f;

            // ── Final Combined Score ──
            float finalScore = meleeScore + (baseScore + (healthScore + sizeScore) * (1f + armourScore));

            return Mathf.Max(0f, finalScore);
        }

        // ────────────────────────────────────────────────────────────────
        // WORTHY KILL VALIDATION
        // ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Determines whether a slain target is a worthy kill for the hunter.
        /// Conditions:
        ///   1. Victim score >= MinAbsoluteScore (filters harmless critters)
        ///   2. Victim score >= killer score * MinWorthyKillFactor
        /// </summary>
        /// <param name="killed">The slain target.</param>
        /// <param name="killer">The hunter pawn.</param>
        /// <param name="killedScore">Output: calculated combat power score of the victim.</param>
        /// <returns>True if the kill meets worthy progression standards.</returns>
        public static bool IsWorthyKill(Pawn killed, Pawn killer, out float killedScore)
        {
            killedScore = CalculateCombatPowerScore(killed);

            if (killedScore < MinAbsoluteScore)
                return false;

            float killerScore = CalculateCombatPowerScore(killer);
            float threshold = killerScore * MinWorthyKillFactor;

            return killedScore >= threshold;
        }

        // ────────────────────────────────────────────────────────────────
        // HEDIFF APPLICATION & PROGRESSION
        // ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Applies or advances the Blooding Hediff on the hunter pawn.
        /// 
        /// Behavior:
        ///   - If pawn lacks the Hediff: adds it with initial severity + kill bonus.
        ///   - If pawn already has it: increases severity proportionally to victim power.
        ///   - Severity never decreases and is clamped to MaxSeverity (1.0).
        /// </summary>
        /// <param name="killer">The hunter pawn receiving progression.</param>
        /// <param name="killed">The slain target pawn.</param>
        /// <param name="killedScore">The pre-calculated victim score.</param>
        public static void ApplyOrEvolveBloodingHediff(Pawn killer, Pawn killed, float killedScore)
        {
            if (killer?.health?.hediffSet == null)
                return;

            HediffDef bloodingDef = BloodingDefOf.ABX_Hediff_Blooding;
            if (bloodingDef == null)
            {
                Log.ErrorOnce("[ABX Blooding] ABX_Hediff_Blooding HediffDef not found! Verify your XML defName.",
                    "ABX_Blooding_NoHediffDef".GetHashCode());
                return;
            }

            // Calculate severity increment clamped within reasonable boundaries per kill
            float severityIncrement = Mathf.Clamp(killedScore / SeverityDivisor, 0.02f, 0.25f);

            Hediff existingHediff = killer.health.hediffSet.GetFirstHediffOfDef(bloodingDef);

            if (existingHediff != null)
            {
                // Already has Hediff — advance severity (unidirectional progression)
                float newSeverity = Mathf.Min(existingHediff.Severity + severityIncrement, MaxSeverity);
                existingHediff.Severity = newSeverity;

                // Log the kill details to the tracker component
                HediffComp_BloodingTracker tracker = existingHediff.TryGetComp<HediffComp_BloodingTracker>();
                tracker?.RecordKill(killed, killedScore);
            }
            else
            {
                // First worthy kill — instantiate new Hediff at starting severity
                float startSeverity = Mathf.Min(InitialSeverity + severityIncrement, MaxSeverity);

                Hediff newHediff = HediffMaker.MakeHediff(bloodingDef, killer);
                newHediff.Severity = startSeverity;
                killer.health.AddHediff(newHediff);

                // Log the kill details to the tracker component
                HediffComp_BloodingTracker tracker = newHediff.TryGetComp<HediffComp_BloodingTracker>();
                tracker?.RecordKill(killed, killedScore);
            }
        }

        // ────────────────────────────────────────────────────────────────
        // ELIGIBILITY CHECK (GENE-BASED)
        // ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Checks if a pawn is eligible for the Blooding system.
        /// 
        /// Criteria:
        ///   1. Pawn is non-null and alive (!Dead).
        ///   2. Pawn is a player-controlled colonist (IsColonist).
        ///   3. Pawn has the active gene defined in BloodingDefOf.ABX_BloodingSystem.
        ///      Using HasActiveGene ensures overridden or inactive genes are respected.
        /// </summary>
        /// <param name="pawn">Pawn to check.</param>
        /// <returns>True if eligible for blooding progression.</returns>
        public static bool IsEligibleForBlooding(Pawn pawn)
        {
            if (pawn == null || pawn.Dead)
                return false;

            if (!pawn.IsColonist)
                return false;

            GeneDef geneDef = BloodingDefOf.ABX_BloodingSystem;
            if (geneDef == null)
            {
                Log.WarningOnce("[ABX Blooding] GeneDef ABX_BloodingSystem not found in DefDatabase. " +
                                "Blooding progression requires this gene to function.",
                    "ABX_Blooding_NoGeneDef".GetHashCode());
                return false;
            }

            // Standard RimWorld Biotech API: check if the pawn possesses the active gene
            return pawn.genes != null && pawn.genes.HasActiveGene(geneDef);
        }

        // ────────────────────────────────────────────────────────────────
        // PRIVATE HELPERS
        // ────────────────────────────────────────────────────────────────

        private static float SafeGetStatAbstract(ThingDef def, StatDef stat)
        {
            try
            {
                return StatExtension.GetStatValueAbstract(def, stat);
            }
            catch (Exception)
            {
                return 0f;
            }
        }
    }
}
