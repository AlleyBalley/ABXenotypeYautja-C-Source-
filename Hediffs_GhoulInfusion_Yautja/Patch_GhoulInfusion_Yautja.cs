using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace GhoulInfusion_Yautja
{
    [HarmonyPatch(typeof(Recipe_GhoulInfusion), "ApplyOnPawn")]
    public static class Patch_GhoulInfusion_Yautja
    {
        private static readonly Dictionary<int, HeadTypeDef> _prevHead = new Dictionary<int, HeadTypeDef>();

        public static void Prefix(Pawn __0)
        {
            Pawn pawn = __0;
            if (pawn == null || ((Thing)pawn).def == null || ((Def)((Thing)pawn).def).defName != "ABAlien_Yautja" || pawn.story == null || pawn.story.headType == null)
                return;
            Patch_GhoulInfusion_Yautja._prevHead[((Thing)pawn).thingIDNumber] = pawn.story.headType;
        }

            public static void Postfix(Pawn __0)
        {
            Pawn pawn = __0;
            if (pawn == null || ((Thing)pawn).def == null || ((Def)((Thing)pawn).def).defName != "ABAlien_Yautja")
                return;
            HeadTypeDef headTypeDef;
            if (Patch_GhoulInfusion_Yautja._prevHead.TryGetValue(((Thing)pawn).thingIDNumber, out headTypeDef))
            {
                Patch_GhoulInfusion_Yautja._prevHead.Remove(((Thing)pawn).thingIDNumber);
                if (pawn.story != null)
                    pawn.story.headType = headTypeDef;
            }
        }
    }
}
