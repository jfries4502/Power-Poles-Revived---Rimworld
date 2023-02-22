using HarmonyLib;
using RimWorld.IO;
using System;
using System.IO;
using UnityEngine;
using Verse;

namespace RimForge.Patches
{
    [HarmonyPatch(typeof(ModContentLoader<Texture2D>), "LoadTexture", new Type[] { typeof(VirtualFile) })]
    static class Patch_ModContentLoaderTex2D_LoadTexture
    {
        private static bool found = false;

        [HarmonyPriority(Priority.First)]
        static bool Prefix(VirtualFile file, ref Texture2D __result)
        {
            if (found)
                return true;

            string filePath = file.FullPath;
            if (!filePath.EndsWith("PowerPoleCable.png"))
            {
                return true;
            }
            found = true;

            Texture2D texture2D = null;

            if (File.Exists(filePath))
            {
                byte[] data = file.ReadAllBytes();
                texture2D = new Texture2D(2, 2, TextureFormat.Alpha8, false);
                texture2D.LoadImage(data);
                texture2D.name = Path.GetFileNameWithoutExtension(filePath);
                texture2D.filterMode = FilterMode.Bilinear;
                texture2D.Apply(true, true);
                Core.Log($"Texture {file.FullPath} was loaded with no mipmaps, bilinear.");
            }

            if (texture2D != null)
            {
                __result = texture2D;
                return false;
            }

            return true;
        }

    }
}