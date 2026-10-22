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
    /// When ordered via the float menu, the pawn uses the item and receives the configured Hediff.
    /// </summary>
    public class CompUseEffect_InstallGauntlet : CompUseEffect
    {
        public CompProperties_UseEffectInstallGauntlet Props =>
            (CompProperties_UseEffectInstallGauntlet)props;

        /// <summary>
        /// Validates whether the given pawn can self-install this gauntlet or module.
        /// Checks humanoid capability, manipulation, limb availability, and prerequisites.
        /// </summary>
        /// <param name="p">The pawn attempting to install the item.</param>
        /// <param name="failReason">Out parameter containing the reason if installation is rejected.</param>
        /// <returns>True if the item can be installed, false otherwise.</returns>
        public override bool CanBeUsedBy(Pawn p, out string failReason)
        {
            failReason = null;

            if (p == null || p.Dead)
                return false;

            if (!p.RaceProps.Humanlike)
            {
                failReason = "Must be humanlike.";
                return false;
            }

            if (!p.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation))
            {
                failReason = "Incapable of manipulation.";
                return false;
            }

            if (Props.hediffDef == null)
            {
                failReason = "Misconfigured item (missing target HediffDef).";
                return false;
            }

            // If the item specifies target body parts, find an eligible limb
            bool hasPartRequirements = Props.bodyPart != null || (Props.bodyParts != null && Props.bodyParts.Count > 0);
            if (hasPartRequirements)
            {
                BodyPartRecord candidatePart = GetEligibleBodyPart(p, out string partFailReason);
                if (candidatePart == null)
                {
                    failReason = partFailReason;
                    return false;
                }
            }
            else
            {
                // Whole-body installation: ensure the pawn doesn't already have the Hediff
                if (p.health.hediffSet.HasHediff(Props.hediffDef))
                {
                    failReason = Props.customFailReasonAlreadyInstalled ?? "Already installed.";
                    return false;
                }
            }

            return true;
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
                targetPart = GetEligibleBodyPart(usedBy, out _);
                if (targetPart == null)
                {
                    Log.Warning($"[ABXenotypeYautja] {usedBy.LabelShort} attempted to install {parent.LabelCap}, but no eligible body part was found.");
                    return;
                }
            }

            // Apply the Hediff
            Hediff hediff = HediffMaker.MakeHediff(Props.hediffDef, usedBy, targetPart);
            usedBy.health.AddHediff(hediff, targetPart, null, null);

            // Play installation sound if specified
            if (Props.soundOnInstall != null && usedBy.Map != null)
            {
                Props.soundOnInstall.PlayOneShot(SoundInfo.InMap(new TargetInfo(usedBy.Position, usedBy.Map)));
            }

            // Notification message
            string targetPartLabel = targetPart != null ? $" ({targetPart.Label})" : string.Empty;
            Messages.Message("ABX_InstalledGauntlet".Translate(usedBy.LabelShortCap, parent.LabelCap) + targetPartLabel,
                usedBy, MessageTypeDefOf.PositiveEvent);
        }

        /// <summary>
        /// Finds the first non-missing limb eligible to receive this gauntlet or module.
        /// </summary>
        private BodyPartRecord GetEligibleBodyPart(Pawn p, out string failReason)
        {
            failReason = null;

            if (p?.RaceProps?.body == null)
            {
                failReason = "Invalid pawn body structure.";
                return null;
            }

            List<BodyPartRecord> candidateParts = p.RaceProps.body.AllParts
                .Where(part => IsMatchingPartDef(part.def))
                .ToList();

            if (candidateParts.Count == 0)
            {
                failReason = "Body lacks suitable limbs.";
                return null;
            }

            // Check non-missing parts
            List<BodyPartRecord> intactParts = candidateParts
                .Where(part => !p.health.hediffSet.PartIsMissing(part))
                .ToList();

            if (intactParts.Count == 0)
            {
                failReason = "Required limb is missing.";
                return null;
            }

            // Check parts that don't already have this exact hediff
            List<BodyPartRecord> withoutSameHediff = intactParts
                .Where(part => !p.health.hediffSet.HasHediff(Props.hediffDef, part))
                .ToList();

            if (withoutSameHediff.Count == 0)
            {
                failReason = Props.customFailReasonAlreadyInstalled ?? "Already installed on all available limbs.";
                return null;
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
            {
                failReason = "Limb already has an incompatible item installed.";
                return null;
            }

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
                failReason = Props.requiresExistingGauntlet
                    ? "Requires an installed gauntlet on this limb."
                    : "No compatible limb found.";
                return null;
            }

            return finalEligibleParts.FirstOrDefault();
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
