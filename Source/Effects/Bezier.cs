using UnityEngine;

namespace RimForge.Effects
{
    public static class Bezier
    {
        public static Vector2 Evaluate(float t, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3)
        {
            // This hot garbage is from stack overflow and I really can't be arsed to clean it up.
            // It works, and that's enough.

            float tt = t * t;
            float ttt = t * t * t;
            float u = 1 - t;
            float uuu = u * u * u;
            float q3 = uuu;
            // q1 and q2 changed:
            float q2 = 3f * ttt - 6f * tt + 3f * t;
            float q1 = -3f * ttt + 3f * tt;
            float q0 = ttt;
            Vector2 p = (p0 * q3 +
                         p1 * q2 +
                         p2 * q1 +
                         p3 * q0);
            // No division by 6.

            return p;
        }
    }
}
