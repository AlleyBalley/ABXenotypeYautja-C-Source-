using RimWorld;
using Verse;

namespace ABXenotypeYautja.BloodingSystem
{
    /// <summary>
    /// Static DefOf references used by the Blooding progression system.
    /// The [DefOf] attribute instructs RimWorld to automatically bind these fields
    /// to their corresponding loaded XML Defs matching the field name.
    /// </summary>
    [DefOf]
    public static class BloodingDefOf
    {
        /// <summary>
        /// Main HediffDef for the Blooding system.
        /// Hunter progression is controlled by the severity of this Hediff.
        /// Stages defined in XML correspond to hunter ranks.
        /// </summary>
        public static HediffDef ABX_Hediff_Blooding;

        /// <summary>
        /// GeneDef that marks a pawn as eligible for the Blooding system.
        /// Only pawns with this active gene will receive kill progression.
        /// </summary>
        public static GeneDef ABX_BloodingSystem;

        static BloodingDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(BloodingDefOf));
        }
    }
}
