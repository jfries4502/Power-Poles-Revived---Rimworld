using UnityEngine;
using Verse;

namespace RimForge.Buildings
{
    public class Building_PowerPole : Building_LongDistanceCabled
    {
        public static Color GetColor(Building building)
        {
            
            return new Color32(140, 160, 160, 255);
        }

        public override string Name => "RF.PowerPoleName".Translate();

        public override bool CanConnectedBeUnderRoof => false;
        public override bool CanHaveConnectionsUnderRoof => false;

        public override Vector2 GetFlatConnectionPoint()
        {
            // NESW are 0123

            Vector2 root = DrawPos.WorldToFlat() + new Vector2(0, 0.5f);
            switch (Rotation.AsInt)
            {
                case 0:
                    return root + new Vector2(0, -0.0578f);
                case 1:
                    return root + new Vector2(0.291f, 0.2f);
                case 2:
                    return root + new Vector2(0, -0.0578f);
                case 3:
                    return root + new Vector2(-0.291f, 0.2f);
            }
            return root;
        }

        public override Color GetCableColor()
        {
            return GetColor(this);
        }
    }
}