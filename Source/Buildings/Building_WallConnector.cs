using UnityEngine;
using Verse;

namespace RimForge.Buildings
{
    public class Building_WallConnector : Building_LongDistanceCabled
    {
        public override string Name => "RF.WallConnectorName".Translate();
        public override bool IsUnderRoof => false;
        public override bool IgnoreMaterialColor => false;

        public override Vector2 GetFlatConnectionPoint()
        {
            // NESW are 0123

            Vector2 root = DrawPos.WorldToFlat();
            switch (Rotation.AsInt)
            {
                case 0:
                    return root + new Vector2(0, 0.6f);
                case 1:
                    return root + new Vector2(0.508f, 0.028f);
                case 2:
                    return root + new Vector2(0, -0.421f);
                case 3:
                    return root + new Vector2(-0.508f, 0.028f);
            }
            return root;
        }

        // Update: Wall connectors are now allowed to connect to other wall connectors, per popular request.
        //public override bool CanLinkTo(Building_LongDistancePower other, bool checkOther = true)
        //{
        //    return base.CanLinkTo(other, checkOther) && other != null && other is not Building_WallConnector;
        //}

        public override Color GetCableColor()
        {
            return Building_PowerPole.GetColor(this);
        }
    }
}
