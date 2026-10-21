using System.Collections.Generic;
using Verse;

namespace ABXenotypeYautja.BloodingSystem
{
    /// <summary>
    /// HediffComp that tracks individual kill data for each hunter pawn.
    /// Attached to ABX_Hediff_Blooding via XML.
    /// 
    /// Tracks:
    ///   - Total worthy kills count
    ///   - Accumulated score sum
    ///   - Most recent kill details (race, name, score)
    ///   - All-time highest-scoring kill details
    /// 
    /// Fully persisted via RimWorld's Scribe system.
    /// </summary>
    public class HediffComp_BloodingTracker : HediffComp
    {
        // ── Persisted state ──
        public int totalWorthyKills;
        public float totalAccumulatedScore;

        public string lastKillRaceDefName;
        public string lastKillName;
        public float lastKillScore;

        public string bestKillRaceDefName;
        public string bestKillName;
        public float bestKillScore;

        public HediffCompProperties_BloodingTracker Props =>
            (HediffCompProperties_BloodingTracker)props;

        /// <summary>
        /// Logs a new worthy kill into the tracker.
        /// Updates counters, recent kill info, and personal best.
        /// </summary>
        public void RecordKill(Pawn killed, float score)
        {
            if (killed == null)
                return;

            totalWorthyKills++;
            totalAccumulatedScore += score;

            lastKillRaceDefName = killed.def?.defName ?? "Unknown";
            lastKillScore = score;

            if (killed.RaceProps != null && killed.RaceProps.Humanlike && killed.Name != null)
            {
                lastKillName = killed.Name.ToStringShort;
            }
            else
            {
                lastKillName = killed.LabelShortCap;
            }

            if (score > bestKillScore)
            {
                bestKillScore = score;
                bestKillRaceDefName = lastKillRaceDefName;
                bestKillName = lastKillName;
            }
        }

        /// <summary>
        /// Extra tooltip lines displayed in the pawn health inspect tab.
        /// </summary>
        public override string CompTipStringExtra
        {
            get
            {
                if (totalWorthyKills <= 0)
                    return null;

                List<string> lines = new List<string>();
                lines.Add("ABX_Blooding_TotalKills".Translate(totalWorthyKills));
                lines.Add("ABX_Blooding_TotalScore".Translate(totalAccumulatedScore.ToString("F1")));

                if (!string.IsNullOrEmpty(lastKillName))
                {
                    lines.Add("ABX_Blooding_LastKill".Translate(lastKillName, lastKillScore.ToString("F1")));
                }

                if (!string.IsNullOrEmpty(bestKillName) && bestKillScore > lastKillScore)
                {
                    lines.Add("ABX_Blooding_BestKill".Translate(bestKillName, bestKillScore.ToString("F1")));
                }

                return string.Join("\n", lines);
            }
        }

        /// <summary>
        /// Extra label in brackets next to the Hediff name (e.g., "(12 kills)").
        /// </summary>
        public override string CompLabelInBracketsExtra
        {
            get
            {
                if (totalWorthyKills <= 0)
                    return null;

                return totalWorthyKills + " " + (totalWorthyKills == 1 ? "kill" : "kills");
            }
        }

        /// <summary>
        /// Saves and loads tracker data using RimWorld's Scribe system.
        /// </summary>
        public override void CompExposeData()
        {
            base.CompExposeData();

            Scribe_Values.Look(ref totalWorthyKills, "totalWorthyKills", 0);
            Scribe_Values.Look(ref totalAccumulatedScore, "totalAccumulatedScore", 0f);

            Scribe_Values.Look(ref lastKillRaceDefName, "lastKillRaceDefName", "");
            Scribe_Values.Look(ref lastKillName, "lastKillName", "");
            Scribe_Values.Look(ref lastKillScore, "lastKillScore", 0f);

            Scribe_Values.Look(ref bestKillRaceDefName, "bestKillRaceDefName", "");
            Scribe_Values.Look(ref bestKillName, "bestKillName", "");
            Scribe_Values.Look(ref bestKillScore, "bestKillScore", 0f);
        }

        /// <summary>
        /// Debug string visible when selecting pawn with Dev Mode active.
        /// </summary>
        public override string CompDebugString()
        {
            return $"Kills: {totalWorthyKills}, AccScore: {totalAccumulatedScore:F1}, " +
                   $"Severity: {parent.Severity:F3}, " +
                   $"Best: {bestKillName} ({bestKillScore:F1})";
        }
    }
}
