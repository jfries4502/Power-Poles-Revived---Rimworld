using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace RimForge.Buildings
{
    class PlaceWorker_ShowConnectRadius : PlaceWorker
    {
        private static Dictionary<Type, Building_LongDistancePower> classes = new Dictionary<Type, Building_LongDistancePower>();

        public override void DrawGhost(ThingDef def, IntVec3 center, Rot4 rot, Color ghostCol, Thing thing = null)
        {
            var klass = def?.thingClass;
            if (klass == null || !klass.IsSubclassOf(typeof(Building_LongDistancePower)))
                return;

            if(!classes.TryGetValue(klass, out var found))
            {
                found = Activator.CreateInstance(klass) as Building_LongDistancePower;
                classes.Add(klass, found);
            }

            if (found == null)
                return; // Should not be possible but hey ho.

            if (!found.DrawLinkRadiusWhenPlacing)
                return;

            float radius = found.MaxLinkDistance;
            if (radius <= 0f || radius > 500)
                return;

            GenDraw.DrawRadiusRing(center, radius, ghostCol);
        }
    }
}
