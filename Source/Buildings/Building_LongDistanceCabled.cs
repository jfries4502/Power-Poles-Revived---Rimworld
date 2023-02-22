using System.Collections.Generic;
using System.Threading.Tasks;
using RimForge.Effects;
using UnityEngine;
using Verse;

namespace RimForge.Buildings
{
    public abstract class Building_LongDistanceCabled : Building_LongDistancePower
    {
        public static readonly Color DefaultCableColor = new Color(150 / 255f, 85 / 255f, 11 / 255f);
        private static readonly Dictionary<Color, Material> cableMaterialsCache = new Dictionary<Color, Material>();

        public static Material GetCableMaterial(Color color)
        {
            if (cableMaterialsCache.TryGetValue(color, out var found))
                return found;

            var mat = MaterialPool.MatFrom("RF/Buildings/PowerPoleCable", ShaderDatabase.Cutout, color);
            cableMaterialsCache.Add(color, mat);
            return mat;
        }

        public virtual bool IgnoreMaterialColor => true;
        public override Color DrawColor
        {
            get => IgnoreMaterialColor ? Color.white : base.DrawColor;
            set
            {
                if (IgnoreMaterialColor)
                    return;
                base.DrawColor = value;
            }
        }
        public override float MaxLinkDistance => Settings.CableMaxDistance;

        private readonly Dictionary<Building_LongDistanceCabled, List<Vector2>> connectionToPoints = new Dictionary<Building_LongDistanceCabled, List<Vector2>>();
        private Material cableMatCached;

        public virtual List<Vector2> GeneratePoints(Building_LongDistanceCabled poleA, Building_LongDistanceCabled poleB, int? pointCount = null, Vector2? p1 = null, Vector2? p2 = null, List<Vector2> points = null)
        {
            if (poleA.DestroyedOrNull() || poleB.DestroyedOrNull())
                return points;

            points ??= new List<Vector2>(128);
            points.Clear();

            Vector2 start = poleA.GetFlatConnectionPoint();
            Vector2 end = poleB.GetFlatConnectionPoint();

            if (p1 == null)
            {
                Vector2 midA = Vector2.Lerp(start, end, 0.3f);
                p1 = midA + new Vector2(0, -1.2f);
            }
            if (p2 == null)
            {
                Vector2 midB = Vector2.Lerp(start, end, 0.7f);
                p2 = midB + new Vector2(0, -1.2f);
            }

            int pc = pointCount ?? GetCablePointCount(start, end);
            if (pc < 3)
                pc = 3;
            
            for (int i = 0; i < pc; i++)
            {
                float t = (float)i / (pc - 1);
                Vector2 bezier = Bezier.Evaluate(t, start, p1.Value, p2.Value, end);
                points.Add(bezier);
            }

            return points;
        }

        public void GeneratePointsAsync(Building_LongDistanceCabled dom, Building_LongDistanceCabled sub, int? pointCount = null, Vector2? p1 = null, Vector2? p2 = null)
        {
            if (dom.DestroyedOrNull() || sub.DestroyedOrNull())
                return;

            if (dom.connectionToPoints.ContainsKey(sub))
                dom.connectionToPoints[sub] = null;
            else
                dom.connectionToPoints.Add(sub, null);

            Task.Run(() =>
            {
                var list = GeneratePoints(dom, sub, pointCount, p1, p2, null);
                dom.connectionToPoints[sub] = list;
            });
        }

        public virtual int GetCablePointCount(Vector2 a, Vector2 b)
        {
            return Mathf.Clamp(Mathf.RoundToInt((a - b).magnitude * Settings.CableSegmentsPerCell), 10, 100);
        }

        /// <summary>
        /// Gets the point that the cables should link up to.
        /// By default simply returns the draw position, however overriding this method and adding an offset
        /// to match the graphics and current rotation is probably a good idea.
        /// </summary>
        /// <returns>The world space connection point. Note that it is a flat Vector2, not a Vector3.</returns>
        public virtual Vector2 GetFlatConnectionPoint()
        {
            return DrawPos.WorldToFlat();
        }

        public virtual Color GetCableColor()
        {
            return DefaultCableColor;
        }

        public void UpdateCableColor()
        {
            cableMatCached = GetCableMaterial(GetCableColor());
        }

        public override void Draw()
        {
            base.Draw();

            if (connectionToPoints == null)
                return;

            if (cableMatCached == null)
                UpdateCableColor();

            foreach (var pair in connectionToPoints)
            {
                if (pair.Key.DestroyedOrNull())
                    continue;

                var points = pair.Value;
                if (points == null || points.Count < 2)
                    continue;

                float height = AltitudeLayer.Skyfaller.AltitudeFor();
                for (int i = 1; i < points.Count; i++)
                {
                    var last = points[i - 1].FlatToWorld(height);
                    var current = points[i].FlatToWorld(height);

                    GenDraw.DrawLineBetween(last, current, cableMatCached);
                }
            }
        }

        protected override void PreLinksReset()
        {
            base.PreLinksReset();

            connectionToPoints.Clear();
        }

        protected override void UponLinkAdded(Building_LongDistancePower to, bool isOwner)
        {
            base.UponLinkAdded(to, isOwner);

            if (!isOwner)
                return;

            if(to is Building_LongDistanceCabled cabled)
                GeneratePointsAsync(this, cabled);
        }

        protected override void UponLinkRemoved(Building_LongDistancePower from, bool isOwner)
        {
            base.UponLinkRemoved(from, isOwner);

            if (!isOwner)
                return;

            if (from is Building_LongDistanceCabled cabled && connectionToPoints.ContainsKey(cabled))
                connectionToPoints.Remove(cabled);
        }

        public virtual void RegenerateCables()
        {
            connectionToPoints.Clear();

            foreach (var conn in base.OwnedConnectionsSanitized)
            {
                if (conn is Building_LongDistanceCabled cabled)
                    GeneratePointsAsync(this, cabled);
            }
        }
    }
}
