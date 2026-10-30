using System.Collections.Generic;
using UnityEngine;

namespace RuneArena.Arena.Editor
{
    // A round column turned on a lathe: plinth, a rounded base, a fluted shaft and a simple capital.
    // Built around the origin, standing on y = 0, matching the base/shaft/cap colliders of the arena pillars.
    static class ColumnMesh
    {
        const int k_Sides = 40;
        const int k_Flutes = 20;
        const float k_FluteDepth = 0.06f;

        // One step of the profile: from (r0, y0) to (r1, y1), with the profile normals at both ends.
        readonly struct Step
        {
            public readonly Vector2 A, B, NA, NB;

            public Step(Vector2 a, Vector2 b, Vector2 na, Vector2 nb)
            {
                A = a; B = b; NA = na; NB = nb;
            }
        }

        public static Mesh Build(float shaftRadius, float height, float trim)
        {
            float s = shaftRadius;
            float wide = s * 1.3f;
            var steps = new List<Step>();

            // Plinth, and its top.
            Straight(steps, new Vector2(wide, 0f), new Vector2(wide, trim * 0.55f));
            // Rounded base (a torus), then a little step into the shaft.
            float torus = trim * 0.3f;
            Straight(steps, new Vector2(wide, trim * 0.55f), new Vector2(s * 1.18f - torus, trim * 0.55f));
            Arc(steps, new Vector2(s * 1.18f - torus, trim * 0.55f + torus), torus, -90f, 90f, 8);
            Straight(steps, new Vector2(s * 1.18f - torus, trim * 0.55f + 2f * torus), new Vector2(s, trim * 0.55f + 2f * torus));

            float shaftBottom = trim * 0.55f + 2f * torus;
            float shaftTop = height - trim * 1.1f;

            // Capital: a thin ring, a curved echinus and the flat abacus on top.
            var capital = new List<Step>();
            float ring = trim * 0.12f;
            Arc(capital, new Vector2(s, shaftTop + ring), ring, -90f, 90f, 6);
            float echinus = height - trim * 0.45f;
            float bulge = echinus - shaftTop - 2f * ring;
            Arc(capital, new Vector2(s, echinus), bulge, -90f, 0f, 8);
            Straight(capital, new Vector2(s + bulge, echinus), new Vector2(wide, echinus));
            Straight(capital, new Vector2(wide, echinus), new Vector2(wide, height));
            Straight(capital, new Vector2(wide, height), new Vector2(0f, height));

            var m = new MeshBuilder();
            foreach (Step step in steps)
                Revolve(m, step);
            FlutedShaft(m, s, shaftBottom, shaftTop);
            foreach (Step step in capital)
                Revolve(m, step);
            return m.ToMesh();
        }

        static void Straight(List<Step> steps, Vector2 a, Vector2 b)
        {
            Vector2 d = (b - a).normalized;
            var n = new Vector2(d.y, -d.x);
            steps.Add(new Step(a, b, n, n));
        }

        // Quarter or half circle in the profile, from angle a0 to a1 (0 = pointing out, 90 = up).
        static void Arc(List<Step> steps, Vector2 center, float radius, float a0, float a1, int segments)
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

        static void Revolve(MeshBuilder m, Step step)
        {
            for (int i = 0; i < k_Sides; i++)
            {
                float t0 = 2f * Mathf.PI * i / k_Sides, t1 = 2f * Mathf.PI * (i + 1) / k_Sides;
                Vector3 o0 = Out(t0), o1 = Out(t1);
                m.Quad(0,
                       o0 * step.A.x + Vector3.up * step.A.y, o1 * step.A.x + Vector3.up * step.A.y,
                       o1 * step.B.x + Vector3.up * step.B.y, o0 * step.B.x + Vector3.up * step.B.y,
                       Normal(o0, step.NA), Normal(o1, step.NA), Normal(o1, step.NB), Normal(o0, step.NB),
                       new Vector2(t0 * step.A.x, step.A.y), new Vector2(t1 * step.A.x, step.A.y),
                       new Vector2(t1 * step.B.x, step.B.y), new Vector2(t0 * step.B.x, step.B.y));
            }
        }

        // The shaft has shallow grooves running up it, so its radius changes with the angle.
        static void FlutedShaft(MeshBuilder m, float s, float y0, float y1)
        {
            int sides = k_Flutes * 4;
            for (int i = 0; i < sides; i++)
            {
                float t0 = 2f * Mathf.PI * i / sides, t1 = 2f * Mathf.PI * (i + 1) / sides;
                Vector3 p0 = Out(t0) * Radius(s, t0), p1 = Out(t1) * Radius(s, t1);
                Vector3 n0 = FluteNormal(s, t0), n1 = FluteNormal(s, t1);
                m.Quad(0, p0 + Vector3.up * y0, p1 + Vector3.up * y0, p1 + Vector3.up * y1, p0 + Vector3.up * y1,
                       n0, n1, n1, n0,
                       new Vector2(t0 * s, y0), new Vector2(t1 * s, y0), new Vector2(t1 * s, y1), new Vector2(t0 * s, y1));
            }
        }

        static float Radius(float s, float t)
        {
            return s * (1f - k_FluteDepth * (0.5f + 0.5f * Mathf.Cos(k_Flutes * t)));
        }

        static Vector3 FluteNormal(float s, float t)
        {
            float r = Radius(s, t);
            float slope = s * k_FluteDepth * 0.5f * k_Flutes * Mathf.Sin(k_Flutes * t);
            // d(position)/dt = r' * out + r * along; the normal is out minus the slope along the surface.
            return (Out(t) * r - Along(t) * slope).normalized;
        }

        static Vector3 Normal(Vector3 outward, Vector2 profile)
        {
            return (outward * profile.x + Vector3.up * profile.y).normalized;
        }

        static Vector3 Out(float angle)
        {
            return new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
        }

        static Vector3 Along(float angle)
        {
            return new Vector3(Mathf.Cos(angle), 0f, -Mathf.Sin(angle));
        }
    }
}
