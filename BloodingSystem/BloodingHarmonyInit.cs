using System.Reflection;
using HarmonyLib;
using Verse;

namespace ABXenotypeYautja.BloodingSystem
{
    /// <summary>
    /// Harmony patch initialization for the Blooding progression module.
    /// 
    /// Note: If the main mod already executes Harmony.PatchAll() elsewhere,
    /// this class can be removed safely, as PatchAll will discover and apply
    /// BloodingHarmonyPatch automatically.
    /// </summary>
    [StaticConstructorOnStartup]
    internal static class BloodingHarmonyInit
    {
        static BloodingHarmonyInit()
        {
            Harmony harmony = new Harmony("com.abx.rimworld.bloodingsystem");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            Log.Message("[ABX Blooding] Harmony patches applied successfully.");
        }
    }
}
