using RimWorld;
using Verse;

namespace RimForge.Buildings
{
    public class PlaceWorker_WallConnector : PlaceWorker
    {
        public override AcceptanceReport AllowsPlacing(
            BuildableDef def,
            IntVec3 center,
            Rot4 rot,
            Map map,
            Thing thingToIgnore = null,
            Thing thing = null)
        {
            if (!center.Impassable(map))
                return "Must place on a wall.";

            IntVec3 c2 = center + IntVec3.North.RotatedBy(rot);
            if (c2.Impassable(map))
                return "RF.WallConnector.MustPlaceWithFreeSpace".Translate();
            Frame firstThing2 = c2.GetFirstThing<Frame>(map);
            if (firstThing2 != null && firstThing2.def.entityDefToBuild != null && firstThing2.def.entityDefToBuild.passability == Traversability.Impassable)
                return "RF.WallConnector.MustPlaceWithFreeSpace".Translate();
            Blueprint firstThing4 = c2.GetFirstThing<Blueprint>(map);
            return firstThing4 != null && firstThing4.def.entityDefToBuild != null && firstThing4.def.entityDefToBuild.passability == Traversability.Impassable ? "RF.WallConnector.MustPlaceWithFreeSpace".Translate() : (AcceptanceReport)true;
        }
    }
}