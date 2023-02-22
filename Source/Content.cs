
using RimForge.Buildings;
using RimForge.Comps;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimForge
{
  [StaticConstructorOnStartup]
  public static class Content
  {
    public static readonly Texture2D LinkIcon = ContentFinder<Texture2D>.Get("RF/UI/Link");

        public static void DrawCustomOverlay(this Thing drawer)
        {
            if (!(drawer is ICustomOverlayDrawer))
                Core.Warn(((Entity)drawer).LabelCap + " cannot draw a custom overlay since it's building class does not implement the ICustomOverlayDrawer interface.");
            else
                drawer.Map.overlayDrawer.DrawOverlay(drawer, (OverlayTypes)64);
        }


        public static Vector2 WorldToFlat(this Vector3 vector) => new Vector2(vector.x, vector.z);

    public static Vector3 FlatToWorld(this Vector2 vector, float height) => new Vector3(vector.x, height, vector.y);
  }
}
