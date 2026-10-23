using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ABXenotypeYautja
{
    /// <summary>
    /// Use-effect component enabling pawns to self-install gauntlets and gauntlet modules.
    /// Operates identically to the Biotech Mechlink installation mechanism using CompUsable.
    /// Uses modern RimWorld 1.5/1.6+ API (AcceptanceReport instead of obsolete out string failReason).
    /// </summary>
    public class CompUseEffect_InstallGauntlet : CompUseEffect
    {
        public CompProperties_UseEffectInstallGauntlet Props =>
            (CompProperties_UseEffectInstallGauntlet)props;

        /// <summary>
        /// Validates whether the given pawn can self-install this gauntlet or module.
        /// Modern 1.5/1.6+ signature returning AcceptanceReport.
        /// </summary>
        /// <param name="p">The pawn attempting to install the item.</param>
        /// <returns>AcceptanceReport indicating whether the action is permitted, with failure reason if rejected.</returns>
        public override AcceptanceReport CanBeUsedBy(Pawn p)
        {
            if (p == null || p.Dead)
                return false;

            if (!p.RaceProps.Humanlike)
                return new AcceptanceReport("Must be humanlike.");

            if (!p.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation))
                return new AcceptanceReport("Incapable of manipulation.");

            if (Props.hediffDef == null)
                return new AcceptanceReport("Misconfigured item (missing target HediffDef).");

            // If the item specifies target body parts, find an eligible limb
            bool hasPartRequirements = Props.bodyPart != null || (Props.bodyParts != null && Props.bodyParts.Count > 0);
            if (hasPartRequirements)
            {
                AcceptanceReport partReport = GetEligibleBodyPart(p, out _);
                if (!partReport.Accepted)
                    return partReport;
            }
            else
            {
                // Whole-body installation: ensure the pawn doesn't already have the Hediff
                if (p.health.hediffSet.HasHediff(Props.hediffDef))
                {
                    string reason = Props.customFailReasonAlreadyInstalled ?? "Already installed.";
                    return new AcceptanceReport(reason);
                }
            }

            return AcceptanceReport.WasAccepted;
        }

        /// <summary>
        /// Executes the installation effect upon completing the use interaction.
        /// Installs the Hediff onto the pawn (or target limb), plays sounds, and notifies the player.
        /// </summary>
        /// <param name="usedBy">The pawn who performed the installation.</param>
        public override void DoEffect(Pawn usedBy)
        {
            base.DoEffect(usedBy);

            if (usedBy == null || usedBy.Dead || Props.hediffDef == null)
                return;

            BodyPartRecord targetPart = null;
            bool hasPartRequirements = Props.bodyPart != null || (Props.bodyParts != null && Props.bodyParts.Count > 0);
            if (hasPartRequirements)
            {
                AcceptanceReport partReport = GetEligibleBodyPart(usedBy, out targetPart);
                if (!partReport.Accepted || targetPart == null)
                {
                    Log.Warning($"[ABXenotypeYautja] {usedBy.LabelShort} attempted to install {parent.LabelCap}, but no eligible body part was found: {partReport.Reason}");
                    return;
                }
            }

            // Apply the Hediff
            Hediff hediff = HediffMaker.MakeHediff(Props.hediffDef, usedBy, targetPart);
            usedBy.health.AddHediff(hediff, targetPart, null, null);

            // Play installation sound if specified
            if (Props.soundOnInstall != null && usedBy.Map != null)
            {
                Props.soundOnInstall.PlayOneShot(SoundInfo.InMap(usedBy));
            }

            // Notification message
            string targetPartLabel = targetPart != null ? $" ({targetPart.Label})" : string.Empty;
            TaggedString msg;
            if ("ABX_InstalledGauntlet".CanTranslate())
            {
                msg = "ABX_InstalledGauntlet".Translate(usedBy.LabelShortCap, parent.LabelCap) + targetPartLabel;
            }
            else
            {
                msg = $"{usedBy.LabelShortCap} equipped {parent.LabelCap}{targetPartLabel}.";
            }

            Messages.Message(msg, usedBy, MessageTypeDefOf.PositiveEvent);
        }

        /// <summary>
        /// Finds the first non-missing limb eligible to receive this gauntlet or module.
        /// Returns AcceptanceReport with descriptive failure reasons if no limb is eligible.
        /// </summary>
        private AcceptanceReport GetEligibleBodyPart(Pawn p, out BodyPartRecord eligiblePart)
        {
            eligiblePart = null;

            if (p?.RaceProps?.body == null)
                return new AcceptanceReport("Invalid pawn body structure.");

            List<BodyPartRecord> candidateParts = p.RaceProps.body.AllParts
                .Where(part => IsMatchingPartDef(part.def))
                .ToList();

            if (candidateParts.Count == 0)
                return new AcceptanceReport("Body lacks suitable limbs.");

            // Check non-missing parts
            List<BodyPartRecord> intactParts = candidateParts
                .Where(part => !p.health.hediffSet.PartIsMissing(part))
                .ToList();

            if (intactParts.Count == 0)
                return new AcceptanceReport("Required limb is missing.");

            // Check parts that don't already have this exact hediff
            List<BodyPartRecord> withoutSameHediff = intactParts
                .Where(part => !p.health.hediffSet.hediffs.Any(h => h.def == Props.hediffDef && h.Part == part))
                .ToList();

            if (withoutSameHediff.Count == 0)
            {
                string reason = Props.customFailReasonAlreadyInstalled ?? "Already installed on all available limbs.";
                return new AcceptanceReport(reason);
            }

            // Filter by tag compatibility (e.g., prevent two base gauntlets on the same limb)
            List<BodyPartRecord> tagCompatibleParts = withoutSameHediff.Where(part =>
            {
                if (Props.incompatibleHediffTags == null || Props.incompatibleHediffTags.Count == 0)
                    return true;

                return !p.health.hediffSet.hediffs.Any(h =>
                    h.Part == part &&
                    h.def.tags != null &&
                    Props.incompatibleHediffTags.Any(tag => h.def.tags.Contains(tag)));
            }).ToList();

            if (tagCompatibleParts.Count == 0)
                return new AcceptanceReport("Limb already has an incompatible item installed.");

            // Filter by prerequisite gauntlet requirement (for modules)
            List<BodyPartRecord> finalEligibleParts = tagCompatibleParts.Where(part =>
            {
                if (!Props.requiresExistingGauntlet)
                    return true;

                return p.health.hediffSet.hediffs.Any(h =>
                    h.Part == part &&
                    ((h.def.tags != null && h.def.tags.Contains("GauntletTag")) ||
                     h.def.GetModExtension<DefExtension_GauntletModuleHediff>() != null));
            }).ToList();

            if (finalEligibleParts.Count == 0)
            {
                string reason = Props.requiresExistingGauntlet
                    ? "Requires an installed gauntlet on this limb."
                    : "No compatible limb found.";
                return new AcceptanceReport(reason);
            }

            eligiblePart = finalEligibleParts.FirstOrDefault();
            return AcceptanceReport.WasAccepted;
        }

        private bool IsMatchingPartDef(BodyPartDef def)
        {
            if (def == null)
                return false;

            if (Props.bodyPart != null && def == Props.bodyPart)
                return true;

            if (Props.bodyParts != null && Props.bodyParts.Contains(def))
                return true;

            return false;
        }
    }
}
