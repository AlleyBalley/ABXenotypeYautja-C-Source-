using Verse;

namespace ABXenotypeYautja.BloodingSystem
{
    /// <summary>
    /// Configuration properties for HediffComp_BloodingTracker.
    /// Declared in XML under ABX_Hediff_Blooding comps list.
    /// </summary>
    public class HediffCompProperties_BloodingTracker : HediffCompProperties
    {
        /// <summary>
        /// When true, displays kill count and scoring details in the health tab tooltip.
        /// </summary>
        public bool showKillCountInTooltip = true;

        public HediffCompProperties_BloodingTracker()
        {
            compClass = typeof(HediffComp_BloodingTracker);
        }
    }
}
