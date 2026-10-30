using System.Collections.Generic;
using UnityEngine;

namespace RuneArena.Arena.Editor
{
    // The pillars in the arena, in a few styles. All stand on y = 0 around the origin, fit in about the
    // same footprint (they are cover, so their size matters to the game) and use the same material slots.
    static class PillarMesh
    {
        public const int Stone = 0;
        public const int Plinth = 1;
        public const int Trim = 2;
        public const int Bronze = 3;
        public const int Glow = 4;
        public const int SubmeshCount = 5;

        const int k_Sides = 40;
        const int k_Flutes = 20;
        const float k_FluteDepth = 0.06f;

        public static Mesh Build(ColosseumBuilder.PillarStyle style, float width, float height)
        {
            var m = new MeshBuilder(SubmeshCount);
            float trim = Mathf.Min(0.3f, height * 0.15f);
            switch (style)
            {
                case ColosseumBuilder.PillarStyle.Column:
                    Column(m, width * 0.5f, height, trim);
                    break;
                case ColosseumBuilder.PillarStyle.Broken:
                    Broken(m, width * 0.5f, height, trim);
                    break;
                case ColosseumBuilder.PillarStyle.Brazier:
                    Brazier(m, width, height);
                    break;
                case ColosseumBuilder.PillarStyle.Stele:
                    Stele(m, width, height);
                    break;
                case ColosseumBuilder.PillarStyle.Blocks:
                    Blocks(m, width, height);
                    break;
            }
            return m.ToMesh();
        }

        // ---- Styles ----

        // Fluted marble column: plinth, a rounded base, the shaft and a simple capital.
        static void Column(MeshBuilder m, float s, float height, float trim)
        {
            float shaftBottom = ColumnBase(m, Trim, s, trim);
            float shaftTop = height - trim * 1.1f;
            FlutedShaft(m, Trim, s, shaftBottom, t => shaftTop);

            var capital = new List<Lathe.Step>();
            float wide = s * 1.3f;
            float ring = trim * 0.12f;
            Lathe.Arc(capital, new Vector2(s, shaftTop + ring), ring, -90f, 90f, 6);
            float echinus = height - trim * 0.45f;
            float bulge = echinus - shaftTop - 2f * ring;
            Lathe.Arc(capital, new Vector2(s, echinus), bulge, -90f, 0f, 8);
            Lathe.Straight(capital, new Vector2(s + bulge, echinus), new Vector2(wide, echinus));
            Lathe.Straight(capital, new Vector2(wide, echinus), new Vector2(wide, height));
            Lathe.Straight(capital, new Vector2(wide, height), new Vector2(0f, height));
            Lathe.Revolve(m, Trim, capital, k_Sides);
        }

        // The same column in travertine, snapped off at an uneven height.
        static void Broken(MeshBuilder m, float s, float height, float trim)
        {
            float shaftBottom = ColumnBase(m, Stone, s, trim);
            float Top(float t) => height * 0.8f + s * (0.5f * Mathf.Sin(2f * t + 1f) + 0.28f * Mathf.Sin(5f * t + 2f) + 0.1f * Mathf.Sin(13f * t));
            FlutedShaft(m, Stone, s, shaftBottom, Top);

            // The broken face: a fan from a point a bit below the highest edge.
            var center = new Vector3(0f, height * 0.8f - s * 0.1f, 0f);
            int sides = k_Flutes * 4;
            for (int i = 0; i < sides; i++)
            {
                float t0 = 2f * Mathf.PI * i / sides, t1 = 2f * Mathf.PI * (i + 1) / sides;
                Vector3 p0 = Lathe.Out(t0) * Radius(s, t0) + Vector3.up * Top(t0);
                Vector3 p1 = Lathe.Out(t1) * Radius(s, t1) + Vector3.up * Top(t1);
                Vector3 n = Vector3.Cross(p0 - center, p1 - center).normalized;
                if (n.y < 0f)
                    n = -n;
                m.Quad(Stone, center, center, p1, p0, n,
                       Vector2.zero, Vector2.zero, new Vector2(p1.x, p1.z), new Vector2(p0.x, p0.z));
            }
        }

        // Square granite pedestal holding a bronze bowl for a fire.
        static void Brazier(MeshBuilder m, float width, float height)
        {
            float die = height - 0.75f;
            Box(m, Plinth, 0f, width * 1.25f, width * 1.25f, 0f, 0.25f);
            Box(m, Plinth, 0f, width * 0.95f, width * 0.95f, 0.25f, die);
            Box(m, Plinth, 0f, width * 1.15f, width * 1.15f, die, die + 0.2f);
            // Bronze bands every few meters up the shaft.
            for (float y = 3f; y < die - 1f; y += 3f)
                Box(m, Bronze, 0f, width, width, y, y + 0.12f);

            float foot = die + 0.2f;
            float bottom = height - 0.4f;
            float rim = width * 0.47f;
            float stem = 0.14f;
            var steps = new List<Lathe.Step>();
            Lathe.Straight(steps, new Vector2(stem, foot), new Vector2(stem, bottom));
            // Outside of the bowl, a quarter ellipse from the stem up to the rim.
            const int curve = 10;
            for (int i = 0; i < curve; i++)
            {
                float t0 = 0.5f * Mathf.PI * i / curve, t1 = 0.5f * Mathf.PI * (i + 1) / curve;
                Vector2 a = BowlPoint(stem, rim, bottom, height, t0), b = BowlPoint(stem, rim, bottom, height, t1);
                steps.Add(new Lathe.Step(a, b, BowlNormal(stem, rim, bottom, height, t0), BowlNormal(stem, rim, bottom, height, t1)));
            }
            Lathe.Straight(steps, new Vector2(rim, height), new Vector2(rim, height + 0.05f));
            Lathe.Straight(steps, new Vector2(rim, height + 0.05f), new Vector2(rim - 0.06f, height + 0.05f));
            Lathe.Straight(steps, new Vector2(rim - 0.06f, height + 0.05f), new Vector2(0f, height - 0.25f));
            Lathe.Revolve(m, Bronze, steps, k_Sides);
        }

        // Tapering granite stone with a capped point and a glowing rune carved on each side.
        static void Stele(MeshBuilder m, float width, float height)
        {
            float y0 = 0.2f, y1 = height - width * 0.5f;
            float b = width * 0.45f, t = width * 0.33f;
            Box(m, Plinth, 0f, width * 1.2f, width * 1.2f, 0f, y0);

            var apex = new Vector3(0f, height, 0f);
            for (int side = 0; side < 4; side++)
            {
                Vector3 a = SideAxis(side), right = Vector3.Cross(Vector3.up, -a);
                Vector3 n = (a * (y1 - y0) + Vector3.up * (b - t)).normalized;
                Vector3 p0 = a * b - right * b + Vector3.up * y0, p1 = a * b + right * b + Vector3.up * y0;
                Vector3 p2 = a * t + right * t + Vector3.up * y1, p3 = a * t - right * t + Vector3.up * y1;
                m.Quad(Plinth, p0, p1, p2, p3, n,
                       new Vector2(-b, y0), new Vector2(b, y0), new Vector2(t, y1), new Vector2(-t, y1));

                Vector3 cap = Vector3.Cross(p2 - p3, apex - p3).normalized;
                if (Vector3.Dot(cap, a) < 0f)
                    cap = -cap;
                m.Quad(Plinth, p3, p2, apex, apex, cap,
                       new Vector2(-t, 0f), new Vector2(t, 0f), new Vector2(0f, t), new Vector2(0f, t));

                Rune(m, s_Runes[side], a, right, n, b, t, y0, y1, Mathf.Min(1.7f, (y0 + y1) * 0.5f));
            }
        }

        // Rough pier of travertine blocks, stacked a little out of line, with a marble cap.
        static void Blocks(MeshBuilder m, float width, float height)
        {
            Box(m, Plinth, 0f, width * 1.25f, width * 1.25f, 0f, 0.2f);
            float top = height - 0.25f;
            float[] turn = { 2f, -3f, 1f, -2f, 3f, -1f, 2f };
            int courses = Mathf.Max(3, Mathf.RoundToInt((top - 0.2f) / 0.6f));
            float course = (top - 0.2f) / courses;
            for (int i = 0; i < courses; i++)
            {
                float size = width * (i % 2 == 0 ? 0.95f : 0.86f);
                Box(m, Stone, turn[i % turn.Length], size, size * 1.04f, 0.2f + course * i, 0.2f + course * (i + 1));
            }
            Box(m, Trim, -1f, width * 1.15f, width * 1.15f, top, height);
        }

        // ---- Runes ----

        // Glyph outlines in -1..1, one per side of the stele: the runes the player draws.
        static readonly Vector2[][] s_Runes =
        {
            new[] { new Vector2(-0.55f, 0.9f), new Vector2(0.55f, 0.25f), new Vector2(-0.55f, -0.25f), new Vector2(0.55f, -0.9f) },
            Circle(0.75f, 20),
            new[] { new Vector2(0f, 0.85f), new Vector2(0.8f, -0.65f), new Vector2(-0.8f, -0.65f), new Vector2(0f, 0.85f) },
            new[] { new Vector2(-0.7f, 0.7f), new Vector2(0.7f, 0.7f), new Vector2(0.7f, -0.7f), new Vector2(-0.7f, -0.7f), new Vector2(-0.7f, 0.7f) },
        };

        static Vector2[] Circle(float radius, int segments)
        {
            var points = new Vector2[segments + 1];
            for (int i = 0; i <= segments; i++)
            {
                float a = 2f * Mathf.PI * i / segments;
                points[i] = new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * radius;
            }
            return points;
        }

        // A thin glowing line along the glyph, just in front of one tapered side of the stele.
        // center is the height of the middle of the glyph: about eye level, where the player sees it.
        static void Rune(MeshBuilder m, Vector2[] glyph, Vector3 axis, Vector3 right, Vector3 normal, float bottom, float top, float y0, float y1, float center)
        {
            const float size = 0.3f;
            const float line = 0.05f;

            Vector3 OnFace(Vector2 g)
            {
                float y = center + g.y * size;
                float half = Mathf.Lerp(bottom, top, (y - y0) / (y1 - y0));
                return axis * half + right * (g.x * size) + Vector3.up * y + normal * 0.012f;
            }

            for (int i = 0; i < glyph.Length - 1; i++)
            {
                Vector2 a = glyph[i], b = glyph[i + 1];
                Vector2 d = (b - a).normalized;
                Vector2 across = new Vector2(-d.y, d.x) * (line * 0.5f / size);
                // Stretched a little along the line so corners don't leave gaps.
                Vector2 a2 = a - d * (line * 0.5f / size), b2 = b + d * (line * 0.5f / size);
                m.Quad(Glow, OnFace(a2 - across), OnFace(b2 - across), OnFace(b2 + across), OnFace(a2 + across), normal,
                       Vector2.zero, Vector2.right, Vector2.one, Vector2.up);
            }
        }

        // ---- Shapes ----

        // Plinth and rounded base of a column. Returns where the shaft starts.
        static float ColumnBase(MeshBuilder m, int submesh, float s, float trim)
        {
            var steps = new List<Lathe.Step>();
            float wide = s * 1.3f;
            float torus = trim * 0.3f;
            Lathe.Straight(steps, new Vector2(wide, 0f), new Vector2(wide, trim * 0.55f));
            Lathe.Straight(steps, new Vector2(wide, trim * 0.55f), new Vector2(s * 1.18f - torus, trim * 0.55f));
            Lathe.Arc(steps, new Vector2(s * 1.18f - torus, trim * 0.55f + torus), torus, -90f, 90f, 8);
            Lathe.Straight(steps, new Vector2(s * 1.18f - torus, trim * 0.55f + 2f * torus), new Vector2(s, trim * 0.55f + 2f * torus));
            Lathe.Revolve(m, submesh, steps, k_Sides);
            return trim * 0.55f + 2f * torus;
        }

        // Shaft with shallow grooves running up it; its radius changes with the angle. top gives the
        // height of the upper edge at each angle.
        static void FlutedShaft(MeshBuilder m, int submesh, float s, float y0, System.Func<float, float> top)
        {
            int sides = k_Flutes * 4;
            for (int i = 0; i < sides; i++)
            {
                float t0 = 2f * Mathf.PI * i / sides, t1 = 2f * Mathf.PI * (i + 1) / sides;
                Vector3 p0 = Lathe.Out(t0) * Radius(s, t0), p1 = Lathe.Out(t1) * Radius(s, t1);
                Vector3 n0 = FluteNormal(s, t0), n1 = FluteNormal(s, t1);
                float h0 = top(t0), h1 = top(t1);
                m.Quad(submesh, p0 + Vector3.up * y0, p1 + Vector3.up * y0, p1 + Vector3.up * h1, p0 + Vector3.up * h0,
                       n0, n1, n1, n0,
                       new Vector2(t0 * s, y0), new Vector2(t1 * s, y0), new Vector2(t1 * s, h1), new Vector2(t0 * s, h0));
            }
        }

        // Box around the Y axis, turned by yaw degrees. Without a bottom, since it always sits on something.
        static void Box(MeshBuilder m, int submesh, float yaw, float sizeX, float sizeZ, float y0, float y1)
        {
            Quaternion turn = Quaternion.Euler(0f, yaw, 0f);
            Vector3 x = turn * Vector3.right * (sizeX * 0.5f), z = turn * Vector3.forward * (sizeZ * 0.5f);
            Vector3 b = Vector3.up * y0, t = Vector3.up * y1;
            Face(m, submesh, z, x, b, t, sizeX);
            Face(m, submesh, -z, -x, b, t, sizeX);
            Face(m, submesh, x, -z, b, t, sizeZ);
            Face(m, submesh, -x, z, b, t, sizeZ);
            m.Quad(submesh, t - x - z, t + x - z, t + x + z, t - x + z, Vector3.up,
                   new Vector2(-sizeX * 0.5f, -sizeZ * 0.5f), new Vector2(sizeX * 0.5f, -sizeZ * 0.5f),
                   new Vector2(sizeX * 0.5f, sizeZ * 0.5f), new Vector2(-sizeX * 0.5f, sizeZ * 0.5f));
            // The underside too: it shows from the floor wherever a box overhangs the one below.
            m.Quad(submesh, b - x + z, b + x + z, b + x - z, b - x - z, Vector3.down,
                   new Vector2(-sizeX * 0.5f, sizeZ * 0.5f), new Vector2(sizeX * 0.5f, sizeZ * 0.5f),
                   new Vector2(sizeX * 0.5f, -sizeZ * 0.5f), new Vector2(-sizeX * 0.5f, -sizeZ * 0.5f));
        }

        // One side of a box: offset goes from the center to the face, along runs across it.
        static void Face(MeshBuilder m, int submesh, Vector3 offset, Vector3 along, Vector3 bottom, Vector3 top, float width)
        {
            Vector3 n = offset.normalized;
            m.Quad(submesh, offset - along + bottom, offset + along + bottom, offset + along + top, offset - along + top, n,
                   new Vector2(0f, bottom.y), new Vector2(width, bottom.y), new Vector2(width, top.y), new Vector2(0f, top.y));
        }

        static Vector2 BowlPoint(float stem, float rim, float bottom, float top, float t)
        {
            return new Vector2(stem + (rim - stem) * Mathf.Sin(t), top - (top - bottom) * Mathf.Cos(t));
        }

        static Vector2 BowlNormal(float stem, float rim, float bottom, float top, float t)
        {
            return new Vector2((top - bottom) * Mathf.Sin(t), -(rim - stem) * Mathf.Cos(t)).normalized;
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
            return (Lathe.Out(t) * r - Lathe.Along(t) * slope).normalized;
        }

        // The four sides of the stele: +Z (toward the middle of the arena once placed), +X, -Z, -X.
        static Vector3 SideAxis(int side)
        {
            switch (side)
            {
                case 0: return Vector3.forward;
                case 1: return Vector3.right;
                case 2: return Vector3.back;
                default: return Vector3.left;
            }
        }

    }
}
