using HarmonyLib;
using System;
using System.Collections.Generic;
using RimForge.Buildings;
using Verse;

namespace RimForge.Patches
{
    [HarmonyPatch(typeof(GenAdj), "CellsAdjacentCardinal", new Type[] { typeof(Thing) })]
    static class Patch_GenAdj_CellsAdjacentCardinal
    {
        static void Postfix(Thing t, ref IEnumerable<IntVec3> __result)
        {
            if (t is Building_LongDistancePower pp)
            {
                __result = AddConnected(__result, pp);
            }
        }

        static IEnumerable<IntVec3> AddConnected(IEnumerable<IntVec3> existing, Building_LongDistancePower thing)
        {
            foreach (var pos in existing)
                yield return pos;

            // Do not sanitize, for speed.
            foreach (var other in thing.GetAllLinked(false))
            {
                if (other != null)
                    yield return other.Position;
            }
        }
    }
}