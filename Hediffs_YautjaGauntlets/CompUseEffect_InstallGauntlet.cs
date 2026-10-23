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
    /// Operates identically to the Biotech Mechlink installation mechanism.
    /// Provides dynamic limb targeting in the right-click float menu (e.g., Left Forearm, Right Forearm),
    /// passes the chosen limb to the installation job, and preserves the item's material (Stuff).
    /// Strictly compatible with C# 7.3 (.NET Framework 4.7.2) and RimWorld 1.6+.
    /// </summary>
    public class CompUseEffect_InstallGauntlet : CompUseEffect
    {
        /// <summary>
        /// Stores the chosen limb when the player selects a specific installation option from the float menu.
        /// Persisted across save/load using Scribe_BodyParts.
        /// </summary>
        public BodyPartRecord targetPartForUse;

        public CompProperties_UseEffectInstallGauntlet Props =>
            (CompProperties_UseEffectInstallGauntlet)props;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_BodyParts.Look(ref targetPartForUse, "targetPartForUse");
        }

        /// <summary>
        /// Generates dynamic limb-specific installation options in the right-click menu.
        /// Unifies the interface so only clean, limb-targeted options appear (e.g., 'Install on Left Forearm').
        /// </summary>
        public override IEnumerable<FloatMenuOption> CompFloatMenuOptions(Pawn myPawn)
        {
            if (myPawn == null || myPawn.Dead)
                yield break;

            if (!myPawn.RaceProps.Humanlike)
            {
                yield return new FloatMenuOption("Install gauntlet (Must be humanlike)", null);
                yield break;
            }

            if (!myPawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation))
            {
                yield return new FloatMenuOption("Install gauntlet (Incapable of manipulation)", null);
                yield break;
            }

            List<BodyPartRecord> candidateParts = GetCandidateParts(myPawn);
            if (candidateParts.Count == 0)
            {
                // Item does not target specific limbs (whole-body implant)
                if (Props.bodyPart == null && (Props.bodyParts == null || Props.bodyParts.Count == 0))
                {
                    if (myPawn.health.hediffSet.HasHediff(Props.hediffDef))
                    {
                        string reason = Props.customFailReasonAlreadyInstalled ?? "Already installed";
                        yield return new FloatMenuOption($"Install {parent.LabelCap} ({reason})", null);
                    }
                    else
                    {
                        yield return CreateFloatMenuOption(myPawn, null, $"Install {parent.LabelCap}");
                    }
                }
                else
                {
                    yield return new FloatMenuOption($"Install {parent.LabelCap} (No suitable limbs)", null);
                }
                yield break;
            }

            // Generate an explicit option for each eligible limb
            for (int i = 0; i < candidateParts.Count; i++)
            {
                BodyPartRecord part = candidateParts[i];
                string failReason = GetPartFailReason(myPawn, part);

                if (failReason != null)
                {
                    string disabledLabel = $"Install on {part.LabelCap} ({failReason})";
                    yield return new FloatMenuOption(disabledLabel, null);
                }
                else
                {
                    string optionLabel = $"Install on {part.LabelCap}";
                    yield return CreateFloatMenuOption(myPawn, part, optionLabel);
                }
            }
        }

        private FloatMenuOption CreateFloatMenuOption(Pawn pawn, BodyPartRecord part, string label)
        {
            if (parent.Map != null)
            {
                if (!pawn.CanReach(parent, PathEndMode.Touch, Danger.Deadly))
                {
                    return new FloatMenuOption($"{label} ({"CannotReach".Translate()})", null);
                }

                if (!pawn.CanReserve(parent))
                {
                    return new FloatMenuOption($"{label} ({"Reserved".Translate()})", null);
                }
            }

            BodyPartRecord targetPart = part; // Local capture for closure in C# 7.3
            return new FloatMenuOption(label, delegate
            {
                StartInstallJob(pawn, targetPart);
            });
        }

        private void StartInstallJob(Pawn pawn, BodyPartRecord part)
        {
            if (pawn == null || parent == null || parent.Destroyed)
                return;

            targetPartForUse = part;

            CompUsable compUsable = parent.GetComp<CompUsable>();
            if (compUsable != null)
            {
                // RimWorld 1.6+ signature: TryStartUseJob(Pawn pawn, LocalTargetInfo extraTarget, bool forced)
                // CompUsable automatically creates and manages the job using its own configured Props.useJob from XML.
                compUsable.TryStartUseJob(pawn, LocalTargetInfo.Invalid, false);
            }
            else
            {
                // Dynamic resolution without any hardcoded JobDefOf references
                JobDef resolvedJob = Props.useJob;
                if (resolvedJob == null && compUsable != null && compUsable.Props != null)
                {
                    resolvedJob = compUsable.Props.useJob;
                }
                if (resolvedJob == null)
                {
                    resolvedJob = DefDatabase<JobDef>.GetNamedSilentFail("UseItem");
                }

                if (resolvedJob != null)
                {
                    Job job = JobMaker.MakeJob(resolvedJob, parent);
                    pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                }
                else
                {
                    Log.Error("[ABXenotypeYautja] Could not start gauntlet install job: item lacks CompUsable and no useJob was defined.");
                }
            }
        }

        /// <summary>
        /// Validates whether the pawn can install this item overall.
        /// Modern RimWorld 1.6+ AcceptanceReport signature.
        /// </summary>
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

            bool hasPartRequirements = Props.bodyPart != null || (Props.bodyParts != null && Props.bodyParts.Count > 0);
            if (hasPartRequirements)
            {
                AcceptanceReport partReport = GetEligibleBodyPart(p, out _);
                if (!partReport.Accepted)
                    return partReport;
            }
            else
            {
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
        /// Installs the Hediff onto the chosen limb, preserves the item's material (Stuff),
        /// plays sounds, and notifies the player.
        /// </summary>
        public override void DoEffect(Pawn usedBy)
        {
            base.DoEffect(usedBy);

            if (usedBy == null || usedBy.Dead || Props.hediffDef == null)
                return;

            BodyPartRecord targetPart = targetPartForUse;
            targetPartForUse = null; // Reset after consumption

            bool hasPartRequirements = Props.bodyPart != null || (Props.bodyParts != null && Props.bodyParts.Count > 0);
            if (hasPartRequirements)
            {
                if (targetPart == null || usedBy.health.hediffSet.PartIsMissing(targetPart))
                {
                    AcceptanceReport partReport = GetEligibleBodyPart(usedBy, out targetPart);
                    if (!partReport.Accepted || targetPart == null)
                    {
                        Log.Warning($"[ABXenotypeYautja] {usedBy.LabelShort} attempted to install {parent.LabelCap}, but no eligible body part was found.");
                        return;
                    }
                }
            }

            // 1. Capture the item's material (Stuff) before destruction
            ThingDef itemStuff = parent.Stuff;

            // 2. Instantiate the Hediff
            Hediff hediff = HediffMaker.MakeHediff(Props.hediffDef, usedBy, targetPart);

            // 3. Store the material in HediffComp_RemoveGauntlet for future detachment
            HediffComp_RemoveGauntlet removeComp = hediff.TryGetComp<HediffComp_RemoveGauntlet>();
            if (removeComp != null)
            {
                removeComp.stuff = itemStuff;
            }

            // 4. Apply the Hediff to the pawn
            usedBy.health.AddHediff(hediff, targetPart, null, null);

            // 5. Play installation sound
            if (Props.soundOnInstall != null && usedBy.Map != null)
            {
                Props.soundOnInstall.PlayOneShot(SoundInfo.InMap(usedBy));
            }

            // 6. Notification message
            string targetPartLabel = (targetPart != null) ? $" ({targetPart.Label})" : string.Empty;
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

        private List<BodyPartRecord> GetCandidateParts(Pawn p)
        {
            List<BodyPartRecord> result = new List<BodyPartRecord>();
            if (p?.RaceProps?.body == null)
                return result;

            List<BodyPartRecord> allParts = p.RaceProps.body.AllParts;
            for (int i = 0; i < allParts.Count; i++)
            {
                if (IsMatchingPartDef(allParts[i].def))
                {
                    result.Add(allParts[i]);
                }
            }
            return result;
        }

        private string GetPartFailReason(Pawn pawn, BodyPartRecord part)
        {
            if (pawn.health.hediffSet.PartIsMissing(part))
            {
                return "Missing limb";
            }

            if (pawn.health.hediffSet.hediffs.Any(h => h.def == Props.hediffDef && h.Part == part))
            {
                return Props.customFailReasonAlreadyInstalled ?? "Already installed";
            }

            if (Props.incompatibleHediffTags != null && Props.incompatibleHediffTags.Count > 0)
            {
                bool hasConflict = pawn.health.hediffSet.hediffs.Any(h =>
                    h.Part == part &&
                    h.def.tags != null &&
                    Props.incompatibleHediffTags.Any(t => h.def.tags.Contains(t)));

                if (hasConflict)
                {
                    return "Incompatible item installed";
                }
            }

            if (Props.requiresExistingGauntlet)
            {
                bool hasGauntlet = pawn.health.hediffSet.hediffs.Any(h =>
                    h.Part == part &&
                    ((h.def.tags != null && h.def.tags.Contains("GauntletTag")) ||
                     h.def.GetModExtension<DefExtension_GauntletModuleHediff>() != null));

                if (!hasGauntlet)
                {
                    return "Requires base gauntlet";
                }
            }

            return null;
        }

        private AcceptanceReport GetEligibleBodyPart(Pawn p, out BodyPartRecord eligiblePart)
        {
            eligiblePart = null;

            List<BodyPartRecord> candidates = GetCandidateParts(p);
            if (candidates.Count == 0)
                return new AcceptanceReport("Body lacks suitable limbs.");

            for (int i = 0; i < candidates.Count; i++)
            {
                BodyPartRecord candidate = candidates[i];
                if (GetPartFailReason(p, candidate) == null)
                {
                    eligiblePart = candidate;
                    return AcceptanceReport.WasAccepted;
                }
            }

            return new AcceptanceReport("No compatible limb available.");
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
