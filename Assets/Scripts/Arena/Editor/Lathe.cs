using System.Collections.Generic;
using UnityEngine;

namespace RuneArena.Arena.Editor
{
    // Shapes turned around an axis from a profile of (radius, height) steps. The axis is local Y;
    // turn and offset place the result.
    static class Lathe
    {
        // One step of the profile: from A to B, with the profile normals at both ends.
        public readonly struct Step
        {
            public readonly Vector2 A, B, NA, NB;

            public Step(Vector2 a, Vector2 b, Vector2 na, Vector2 nb)
            {
                A = a; B = b; NA = na; NB = nb;
            }
        }

        public static void Straight(List<Step> steps, Vector2 a, Vector2 b)
        {
            Vector2 d = (b - a).normalized;
            var n = new Vector2(d.y, -d.x);
            steps.Add(new Step(a, b, n, n));
        }

        // Part of a circle in the profile, from angle a0 to a1 (0 = pointing out, 90 = up).
        public static void Arc(List<Step> steps, Vector2 center, float radius, float a0, float a1, int segments)
        {
            for (int i = 0; i < segments; i++)
            {
                float t0 = Mathf.Lerp(a0, a1, (float)i / segments) * Mathf.Deg2Rad;
                float t1 = Mathf.Lerp(a0, a1, (float)(i + 1) / segments) * Mathf.Deg2Rad;
                var n0 = new Vector2(Mathf.Cos(t0), Mathf.Sin(t0));
                var n1 = new Vector2(Mathf.Cos(t1), Mathf.Sin(t1));
                steps.Add(new Step(center + n0 * radius, center + n1 * radius, n0, n1));
            }
        }

        public static void Revolve(MeshBuilder m, int submesh, List<Step> steps, int sides)
        {
            Revolve(m, submesh, steps, sides, Quaternion.identity, Vector3.zero);
        }

        public static void Revolve(MeshBuilder m, int submesh, List<Step> steps, int sides, Quaternion turn, Vector3 offset)
        {
            foreach (Step step in steps)
            {
                for (int i = 0; i < sides; i++)
                {
                    float t0 = 2f * Mathf.PI * i / sides, t1 = 2f * Mathf.PI * (i + 1) / sides;
                    Vector3 o0 = Out(t0), o1 = Out(t1);
                    m.Quad(submesh,
                           Place(o0 * step.A.x + Vector3.up * step.A.y), Place(o1 * step.A.x + Vector3.up * step.A.y),
                           Place(o1 * step.B.x + Vector3.up * step.B.y), Place(o0 * step.B.x + Vector3.up * step.B.y),
                           turn * Normal(o0, step.NA), turn * Normal(o1, step.NA), turn * Normal(o1, step.NB), turn * Normal(o0, step.NB),
                           new Vector2(t0 * step.A.x, step.A.y), new Vector2(t1 * step.A.x, step.A.y),
                           new Vector2(t1 * step.B.x, step.B.y), new Vector2(t0 * step.B.x, step.B.y));
                }
            }

            Vector3 Place(Vector3 p) => turn * p + offset;
        }

        public static Vector3 Out(float angle)
        {
            return new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
        }

        public static Vector3 Along(float angle)
        {
            return new Vector3(Mathf.Cos(angle), 0f, -Mathf.Sin(angle));
        }

        static Vector3 Normal(Vector3 outward, Vector2 profile)
        {
            return (outward * profile.x + Vector3.up * profile.y).normalized;
        }
    }
}
