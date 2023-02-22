using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimForge.Comps;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimForge.Patches
{
    [HarmonyPatch(typeof(OverlayDrawer), "RenderBrokenDownOverlay")]
    static class Patch_OverlayDrawer_RenderBrokenDownOverlay
    {
        private static Dictionary<string, Material> matCache = new Dictionary<string, Material>();

        private static MethodInfo method;
        private static readonly object[] args = new object[4];

        static bool Prefix(OverlayDrawer __instance, Thing t)
        {
            if (t is ICustomOverlayDrawer d)
            {
                string texPath = d.OverlayTexturePath;
                if (texPath == null)
                    return false;

                if (method == null)
                {
                    method = AccessTools.DeclaredMethod(typeof(OverlayDrawer), "RenderPulsingOverlay", new Type[]
                    {
                        typeof(Thing), typeof(Material), typeof(int), typeof(bool)
                    });
                    args[2] = 5;
                    args[3] = true;
                }

                if (!matCache.TryGetValue(texPath, out var mat))
                {
                    mat = MaterialPool.MatFrom(texPath, ShaderDatabase.MetaOverlay);
                    matCache.Add(texPath, mat);
                }
                if (mat == null)
                {
                    Log.ErrorOnce($"Failed to load meta overlay material from texture path '{texPath}' for building '{t.LabelCap}'.", texPath.GetHashCode());
                    return false;
                }

                args[0] = t;
                args[1] = mat;
                method.Invoke(__instance, args);
                args[0] = null;

                return false;
            }

            return true;
        }
    }
}
