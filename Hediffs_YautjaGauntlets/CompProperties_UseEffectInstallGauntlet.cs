using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ABXenotypeYautja
{
    /// <summary>
    /// Configuration properties for CompUseEffect_InstallGauntlet.
    /// Attached to a gauntlet or gauntlet module ThingDef alongside CompUsable.
    /// Governs target Hediff, target body parts, and prerequisite conditions.
    /// Compatible with RimWorld 1.5 and 1.6+.
    /// </summary>
    public class CompProperties_UseEffectInstallGauntlet : CompProperties_UseEffect
    {
        /// <summary>
        /// The HediffDef to apply to the pawn when the item is used.
        /// </summary>
        public HediffDef hediffDef;

        /// <summary>
        /// Optional single target body part def (e.g., ABYautja_Forearm, Arm, Hand).
        /// If specified, installation searches for an available part matching this def.
        /// </summary>
        public BodyPartDef bodyPart;

        /// <summary>
        /// Optional list of allowed target body part defs if multiple limb types are valid.
        /// </summary>
        public List<BodyPartDef> bodyParts;

        /// <summary>
        /// Hediff tags that must NOT be present on the target body part.
        /// Example: "GauntletTag" prevents installing multiple base gauntlets on the same limb.
        /// </summary>
        public List<string> incompatibleHediffTags;

        /// <summary>
        /// When true (e.g., for blade/utility modules), requires that the target body part
        /// already has an installed base gauntlet.
        /// </summary>
        public bool requiresExistingGauntlet = false;

        /// <summary>
        /// Optional job def to use when installing the gauntlet.
        /// If omitted, dynamically falls back to CompUsable.Props.useJob or DefDatabase lookup.
        /// </summary>
        public JobDef useJob;

        /// <summary>
        /// Sound played when the installation is successfully completed.
        /// </summary>
        public SoundDef soundOnInstall;

        /// <summary>
        /// Custom failure string shown in the float menu if already installed on all limbs.
        /// </summary>
        public string customFailReasonAlreadyInstalled;

        public CompProperties_UseEffectInstallGauntlet()
        {
            compClass = typeof(CompUseEffect_InstallGauntlet);
        }
    }
}
