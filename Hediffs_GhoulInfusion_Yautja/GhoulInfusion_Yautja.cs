using HarmonyLib;
using Verse;

namespace GhoulInfusion_Yautja
{
    [StaticConstructorOnStartup]
    public class GhoulInfusion_Yautja
    {
        static GhoulInfusion_Yautja() => new Harmony("Yautja.ghoulinfusion").PatchAll();
    }
}
