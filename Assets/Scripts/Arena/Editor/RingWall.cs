using System.Collections.Generic;
using UnityEngine;

namespace RuneArena.Arena.Editor
{
    // A ring of wall between two radii, with round-topped openings cut through it at even spacing.
    // Used for the arena wall (the openings are the doors), the walls between the tiers of seats and
    // the arcade at the top. Distances along the ring are measured on the middle radius.
    struct Arcade
    {
        public float Inner;
        public float Outer;
        public float Bottom;
        public float Top;
        // 0 for a solid ring.
        public int Arches;
        public float ArchWidth;
        // Height of the top of the arch above Bottom.
        public float ArchHeight;
        // Angle of the middle of the first opening, in degrees. 0 is +Z.
        public float StartAngle;
        public int ArchSegments;
        // Segments around the full circle for the plain wall.
        public int Segments;
        public bool Cap;
    }

    static class RingWall
    {
        readonly struct Column
        {
            public readonly float U;
            public readonly float Bottom;

            public Column(float u, float bottom)
            {
                U = u;
                Bottom = bottom;
            }
        }

        public static void Build(MeshBuilder m, int submesh, Arcade a)
        {
            float middle = (a.Outer + a.Inner) * 0.5f;
            int sections = Mathf.Max(a.Arches, 1);
            float sectionLength = 2f * Mathf.PI * middle / sections;
            float maxColumn = 2f * Mathf.PI * middle / a.Segments;
            float start = a.StartAngle * Mathf.Deg2Rad * middle;
            float half = a.ArchWidth * 0.5f;
            float spring = a.ArchHeight - half;

            for (int s = 0; s < sections; s++)
            {
                float center = start + s * sectionLength;
                List<Column> columns = SectionColumns(a, sectionLength, maxColumn);

                for (int c = 0; c < columns.Count - 1; c++)
                {
                    Column c0 = columns[c];
                    Column c1 = columns[c + 1];
                    if (Mathf.Approximately(c0.U, c1.U))
                        continue;

                    float u0 = center + c0.U;
                    float u1 = center + c1.U;
                    float b0 = a.Bottom + c0.Bottom;
                    float b1 = a.Bottom + c1.Bottom;
                    Face(m, submesh, a.Inner, u0, b0, u1, b1, a.Top, middle, -1f);
                    Face(m, submesh, a.Outer, u0, b0, u1, b1, a.Top, middle, 1f);
                    if (a.Cap)
                        Top(m, submesh, a.Inner, a.Outer, u0, u1, a.Top, middle);
                    if (c0.Bottom > 0f || c1.Bottom > 0f)
                        Intrados(m, submesh, a, c0, c1, center, spring, middle);
                }

                if (a.Arches > 0)
                {
                    Jamb(m, submesh, a.Inner, a.Outer, center - half, a.Bottom, a.Bottom + spring, middle, 1f);
                    Jamb(m, submesh, a.Inner, a.Outer, center + half, a.Bottom, a.Bottom + spring, middle, -1f);
                }
            }
        }

        // Center angle of each opening, in radians, for things placed around them.
        public static float ArchAngle(Arcade a, int i)
        {
            return (a.StartAngle + i * 360f / Mathf.Max(a.Arches, 1)) * Mathf.Deg2Rad;
        }

        // Column boundaries across one section, centered on its opening: plain wall, the arch, plain wall.
        // Bottom is how far above the wall's bottom the solid part starts.
        static List<Column> SectionColumns(Arcade a, float sectionLength, float maxColumn)
        {
            var columns = new List<Column>();
            float halfSection = sectionLength * 0.5f;

            if (a.Arches == 0)
            {
                int n = Mathf.CeilToInt(sectionLength / maxColumn);
                for (int i = 0; i <= n; i++)
                    columns.Add(new Column(-halfSection + sectionLength * i / n, 0f));
                return columns;
            }

            float half = a.ArchWidth * 0.5f;
            float spring = a.ArchHeight - half;
            float side = halfSection - half;
            int sideColumns = Mathf.Max(1, Mathf.CeilToInt(side / maxColumn));

            for (int i = 0; i <= sideColumns; i++)
                columns.Add(new Column(-halfSection + side * i / sideColumns, 0f));

            for (int k = 0; k <= a.ArchSegments; k++)
            {
                float t = Mathf.PI * k / a.ArchSegments;
                columns.Add(new Column(-half * Mathf.Cos(t), spring + half * Mathf.Sin(t)));
            }

            for (int i = 0; i <= sideColumns; i++)
                columns.Add(new Column(half + side * i / sideColumns, 0f));

            return columns;
        }

        public static Vector3 Point(float radius, float u, float y, float middle)
        {
            float angle = u / middle;
            return new Vector3(Mathf.Sin(angle) * radius, y, Mathf.Cos(angle) * radius);
        }

        public static Vector3 Radial(float u, float middle)
        {
            float angle = u / middle;
            return new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
        }

        public static Vector3 Tangent(float u, float middle)
        {
            float angle = u / middle;
            return new Vector3(Mathf.Cos(angle), 0f, -Mathf.Sin(angle));
        }

        static void Face(MeshBuilder m, int submesh, float radius, float u0, float b0, float u1, float b1, float top, float middle, float side)
        {
            Vector3 n0 = Radial(u0, middle) * side;
            Vector3 n1 = Radial(u1, middle) * side;
            m.Quad(submesh,
                   Point(radius, u0, b0, middle), Point(radius, u1, b1, middle),
                   Point(radius, u1, top, middle), Point(radius, u0, top, middle),
                   n0, n1, n1, n0,
                   new Vector2(u0, b0), new Vector2(u1, b1), new Vector2(u1, top), new Vector2(u0, top));
        }

        static void Top(MeshBuilder m, int submesh, float inner, float outer, float u0, float u1, float top, float middle)
        {
            m.Quad(submesh,
                   Point(inner, u0, top, middle), Point(outer, u0, top, middle),
                   Point(outer, u1, top, middle), Point(inner, u1, top, middle),
                   Vector3.up,
                   new Vector2(u0, inner), new Vector2(u0, outer), new Vector2(u1, outer), new Vector2(u1, inner));
        }

        // Underside of the arch. Normals point at the arch center.
        static void Intrados(MeshBuilder m, int submesh, Arcade a, Column c0, Column c1, float center, float spring, float middle)
        {
            float u0 = center + c0.U;
            float u1 = center + c1.U;
            float b0 = a.Bottom + c0.Bottom;
            float b1 = a.Bottom + c1.Bottom;
            Vector3 n0 = (Tangent(u0, middle) * -c0.U + Vector3.up * (spring - c0.Bottom)).normalized;
            Vector3 n1 = (Tangent(u1, middle) * -c1.U + Vector3.up * (spring - c1.Bottom)).normalized;
            m.Quad(submesh,
                   Point(a.Inner, u0, b0, middle), Point(a.Outer, u0, b0, middle),
                   Point(a.Outer, u1, b1, middle), Point(a.Inner, u1, b1, middle),
                   n0, n0, n1, n1,
                   new Vector2(u0, a.Inner), new Vector2(u0, a.Outer), new Vector2(u1, a.Outer), new Vector2(u1, a.Inner));
        }

        // Straight side of the opening, below the arch. side = +1 faces along +u (into the opening from the left).
        static void Jamb(MeshBuilder m, int submesh, float inner, float outer, float u, float bottom, float spring, float middle, float side)
        {
            Vector3 n = Tangent(u, middle) * side;
            m.Quad(submesh,
                   Point(inner, u, bottom, middle), Point(outer, u, bottom, middle),
                   Point(outer, u, spring, middle), Point(inner, u, spring, middle),
                   n,
                   new Vector2(inner, bottom), new Vector2(outer, bottom), new Vector2(outer, spring), new Vector2(inner, spring));
        }
    }
}
