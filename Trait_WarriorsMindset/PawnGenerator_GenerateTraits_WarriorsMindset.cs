using HarmonyLib;
using RimWorld;
using Verse;

namespace ABWarriorsMindset_Code
{
    [HarmonyPatch(typeof(PawnGenerator), "GenerateTraits", null)]
    public static class PawnGenerator_GenerateTraits_WarriorsMindset
    {
        public static void Postfix(Pawn pawn)
        {
            Trait trait = (Trait)null;
            foreach (Trait allTrait in pawn.story.traits.allTraits)
            {
                trait = allTrait;
                if (!trait.Label.Equals("ABYautjaWarriorsMindset"))
                    trait = (Trait)null;
                else
                    break;
            }
        }
    }
}