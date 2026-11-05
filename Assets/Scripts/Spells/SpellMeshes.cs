using System.Collections.Generic;
using UnityEngine;

namespace RuneArena.Spells
{
    // Shapes for the spells, built in code: a breaking wave, an icicle, thorny roots and a wall of rock. Also the
    // rune stones the enemies drop.
    // Their v coordinate runs from base to tip, which is what the Spell Surface shader colors and grows along.
    static class SpellMeshes
    {
        static Mesh s_Wave;
        static Mesh s_Icicle;

        // Unit-sized curling wave, x across it, y up, z the way it travels: the back rises to a crest
        // that curls forward over a hollow front. Lower and set back at the ends.
        public static Mesh Wave => s_Wave != null ? s_Wave : s_Wave = BuildWave();

        // Ice spike pointing along +Z with smaller shards around it, about one unit long.
        public static Mesh Icicle => s_Icicle != null ? s_Icicle : s_Icicle = BuildIcicle();

        // A fresh tangle of vines bursting out of the ground within radius: submesh 0 the vines,
        // submesh 1 their thorns.
        public static Mesh Roots(float radius, int seed)
        {
            var random = new System.Random(seed);
            var shape = new Shape(2);
            int count = Mathf.Clamp(Mathf.RoundToInt(radius * 4f), 5, 16);
            for (int i = 0; i < count; i++)
                Vine(shape, radius, random);
            return shape.ToMesh("Ivy Roots");
        }

        // A row of rough rock slabs filling the unit box (x across, y up, z through), with an uneven top.
        public static Mesh Rocks(int seed)
        {
            var random = new System.Random(seed);
            float Range(float lo, float hi) => lo + (float)random.NextDouble() * (hi - lo);

            var shape = new Shape(1);
            int count = 7;
            for (int i = 0; i < count; i++)
            {
                float x = -0.5f + (i + 0.5f) / count + Range(-0.03f, 0.03f);
                float halfWidth = 0.5f / count * Range(1.6f, 2f);
                float halfDepth = Range(0.4f, 0.55f);
                float top = Range(0.3f, 0.5f);
                var center = new Vector3(x, -0.5f, Range(-0.08f, 0.08f));
                Slab(shape, center, halfWidth, halfDepth, top, Range(-8f, 8f), random);
            }
            return shape.ToMesh("Earth Rocks");
        }

        // A tapering five-sided slab from the floor up to top, leaning a few degrees, with a broken point.
        static void Slab(Shape shape, Vector3 bottom, float halfWidth, float halfDepth, float top, float lean, System.Random random)
        {
            const int sides = 5;
            float height = top + 0.5f;
            Quaternion tilt = Quaternion.Euler(0f, 0f, lean);
            var low = new Vector3[sides];
            var high = new Vector3[sides];
            for (int k = 0; k < sides; k++)
            {
                float a = 2f * Mathf.PI * (k + 0.3f * (float)random.NextDouble()) / sides;
                var ring = new Vector3(Mathf.Cos(a) * halfWidth, 0f, Mathf.Sin(a) * halfDepth);
                low[k] = bottom + ring;
                high[k] = bottom + tilt * (ring * 0.82f + Vector3.up * (height * (0.86f + 0.1f * (float)random.NextDouble())));
            }
            Vector3 apex = bottom + tilt * (Vector3.up * height) + new Vector3(0f, 0f, ((float)random.NextDouble() - 0.5f) * halfDepth);
            Vector3 inside = bottom + Vector3.up * (height * 0.4f);

            for (int k = 0; k < sides; k++)
            {
                int n = (k + 1) % sides;
                shape.Facet(0, low[k], low[n], high[n], inside, V(low[k]), V(low[n]), V(high[n]));
                shape.Facet(0, low[k], high[n], high[k], inside, V(low[k]), V(high[n]), V(high[k]));
                shape.Facet(0, high[k], high[n], apex, inside, V(high[k]), V(high[n]), V(apex));
            }

            // v is the height in the box, so the color ramps from the base of the wall to its top.
            float V(Vector3 p) => Mathf.Clamp01(p.y + 0.5f);
        }

        // ---- Wave ----

        // Cross-section from the bottom of the back, over the crest, around the curl, to the front foot.
        static readonly Vector2[] k_WaveProfile =
        {
            new Vector2(-0.5f, -0.5f), new Vector2(-0.22f, -0.38f), new Vector2(0.02f, -0.1f),
            new Vector2(0.18f, 0.22f), new Vector2(0.28f, 0.44f), new Vector2(0.4f, 0.52f),
            new Vector2(0.5f, 0.42f), new Vector2(0.47f, 0.26f), new Vector2(0.37f, 0.16f),
            new Vector2(0.31f, 0.0f), new Vector2(0.33f, -0.28f), new Vector2(0.42f, -0.5f),
        };

        static Mesh BuildWave()
        {
            const int across = 24;
            int rows = k_WaveProfile.Length;
            var lengths = new float[rows];
            for (int j = 1; j < rows; j++)
                lengths[j] = lengths[j - 1] + Vector2.Distance(k_WaveProfile[j - 1], k_WaveProfile[j]);

            var shape = new Shape(1);
            for (int i = 0; i <= across; i++)
            {
                float x = (float)i / across - 0.5f;
                float edge = Mathf.Pow(Mathf.Abs(2f * x), 3f);
                for (int j = 0; j < rows; j++)
                {
                    Vector2 p = k_WaveProfile[j];
                    float y = -0.5f + (p.y + 0.5f) * (1f - 0.65f * edge);
                    float z = p.x - 0.2f * edge;
                    shape.Vertex(new Vector3(x, y, z), Vector3.up, new Vector2(i / (float)across, lengths[j] / lengths[rows - 1]));
                }
            }
            for (int i = 0; i < across; i++)
            {
                for (int j = 0; j < rows - 1; j++)
                {
                    int a = i * rows + j, b = (i + 1) * rows + j;
                    shape.Triangle(0, a, b, b + 1);
                    shape.Triangle(0, a, b + 1, a + 1);
                }
            }
            Mesh mesh = shape.ToMesh("Wave");
            mesh.RecalculateNormals();
            return mesh;
        }

        // ---- Ice ----

        static Mesh BuildIcicle()
        {
            var shape = new Shape(1);
            Crystal(shape, new Vector3(0f, 0f, -0.05f), Vector3.forward, 0.12f, 0.55f, 0.3f, 6);
            for (int k = 0; k < 4; k++)
            {
                float a = (45f + 90f * k) * Mathf.Deg2Rad;
                var radial = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                Vector3 axis = (radial * 0.8f + Vector3.back * 0.6f).normalized;
                Crystal(shape, radial * 0.08f + Vector3.back * 0.12f, axis, 0.045f, 0.22f, 0.05f, 5);
            }
            return shape.ToMesh("Icicle");
        }

        // Faceted double point: a ring of sides around center, a long point ahead and a short one behind.
        // Every face keeps its own vertices so the facets catch the light separately.
        static void Crystal(Shape shape, Vector3 center, Vector3 axis, float radius, float front, float back, int sides)
        {
            Vector3 side = Vector3.Cross(axis, Mathf.Abs(axis.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            Vector3 other = Vector3.Cross(axis, side);
            Vector3 tip = center + axis * front, tail = center - axis * back;
            var ring = new Vector3[sides];
            for (int i = 0; i < sides; i++)
            {
                float a = 2f * Mathf.PI * i / sides;
                ring[i] = center + (side * Mathf.Cos(a) + other * Mathf.Sin(a)) * radius;
            }
            for (int i = 0; i < sides; i++)
            {
                Vector3 r0 = ring[i], r1 = ring[(i + 1) % sides];
                shape.Facet(0, tip, r0, r1, center);
                shape.Facet(0, tail, r1, r0, center);
            }
        }

        // ---- Rune stone ----

        // A rough standing stone about a unit tall, flat on its two faces, with a rune cut into both: submesh 0
        // the stone, submesh 1 the rune. glyph is one stroke, from about -1 to 1 up and down.
        public static Mesh RuneStone(IReadOnlyList<Vector2> glyph, int seed)
        {
            const int sides = 9;
            const float halfDepth = 0.13f;
            const float face = 0.8f;
            var random = new System.Random(seed);
            float Range(float lo, float hi) => lo + (float)random.NextDouble() * (hi - lo);

            // Taller than wide, a little narrower at the top, every corner nudged so no two look alike.
            var outline = new Vector2[sides];
            for (int i = 0; i < sides; i++)
            {
                float a = 2f * Mathf.PI * (i + Range(-0.2f, 0.2f)) / sides;
                float width = 0.36f * (Mathf.Sin(a) > 0f ? 0.85f : 1f);
                outline[i] = new Vector2(Mathf.Cos(a) * width, Mathf.Sin(a) * 0.5f) * Range(0.9f, 1.05f);
            }

            var shape = new Shape(2);
            Vector3 center = Vector3.zero;
            var front = new Vector3[sides];
            var middle = new Vector3[sides];
            var back = new Vector3[sides];
            for (int i = 0; i < sides; i++)
            {
                front[i] = new Vector3(outline[i].x * face, outline[i].y * face, -halfDepth);
                middle[i] = new Vector3(outline[i].x, outline[i].y, Range(-0.02f, 0.02f));
                back[i] = new Vector3(outline[i].x * face, outline[i].y * face, halfDepth);
            }
            for (int i = 0; i < sides; i++)
            {
                int j = (i + 1) % sides;
                shape.Facet(0, front[i], front[j], new Vector3(0f, 0f, -halfDepth), center);
                shape.Facet(0, back[i], back[j], new Vector3(0f, 0f, halfDepth), center);
                shape.Facet(0, front[i], middle[i], middle[j], center);
                shape.Facet(0, front[i], middle[j], front[j], center);
                shape.Facet(0, back[i], middle[j], middle[i], center);
                shape.Facet(0, back[i], back[j], middle[j], center);
            }

            // The rune a hair off each face, as flat strips with their own color.
            const float scale = 0.28f;
            const float stroke = 0.045f;
            for (int side = -1; side <= 1; side += 2)
            {
                float z = side * (halfDepth + 0.004f);
                Vector3 inside = new Vector3(0f, 0f, z - side);
                for (int k = 0; k + 1 < glyph.Count; k++)
                {
                    Vector3 a = new Vector3(glyph[k].x * scale, glyph[k].y * scale, z);
                    Vector3 b = new Vector3(glyph[k + 1].x * scale, glyph[k + 1].y * scale, z);
                    Vector3 along = (b - a).normalized;
                    // Run past the corners a little so the joints close.
                    a -= along * (stroke * 0.5f);
                    b += along * (stroke * 0.5f);
                    Vector3 across = Vector3.Cross(along, Vector3.forward) * (stroke * 0.5f);
                    shape.Facet(1, a - across, a + across, b + across, inside);
                    shape.Facet(1, a - across, b + across, b - across, inside);
                }
            }
            return shape.ToMesh("Rune Stone");
        }

        // ---- Roots ----

        // One vine coming out of the ground: either an arch that dives back in, or a spike reaching up.
        static void Vine(Shape shape, float radius, System.Random random)
        {
            float Range(float lo, float hi) => lo + (float)random.NextDouble() * (hi - lo);

            float distance = Mathf.Sqrt(Range(0f, 1f)) * radius * 0.85f;
            float around = Range(0f, 2f * Mathf.PI);
            var start = new Vector3(Mathf.Cos(around) * distance, -0.05f, Mathf.Sin(around) * distance);
            float heading = Range(0f, 2f * Mathf.PI);
            var direction = new Vector3(Mathf.Cos(heading), 0f, Mathf.Sin(heading));
            float height = Range(0.35f, 1.1f);
            float length = Range(0.6f, 1.4f);
            bool spike = random.NextDouble() < 0.45;

            Vector3 control = start + direction * (length * 0.4f) + Vector3.up * (height * 1.6f);
            Vector3 end = spike
                ? start + direction * (length * 0.55f) + Vector3.up * height
                : start + direction * length + Vector3.down * 0.05f;
            float thickness = Range(0.035f, 0.06f);

            const int segments = 12;
            const int sides = 6;
            var points = new Vector3[segments + 1];
            var radii = new float[segments + 1];
            for (int k = 0; k <= segments; k++)
            {
                float t = (float)k / segments;
                points[k] = (1 - t) * (1 - t) * start + 2 * (1 - t) * t * control + t * t * end;
                radii[k] = spike ? thickness * Mathf.Lerp(1f, 0.08f, t) : thickness * (1f - 0.35f * Mathf.Sin(Mathf.PI * t));
            }

            int first = shape.VertexCount;
            var normals = new Vector3[segments + 1];
            var binormals = new Vector3[segments + 1];
            for (int k = 0; k <= segments; k++)
            {
                Vector3 tangent = (points[Mathf.Min(k + 1, segments)] - points[Mathf.Max(k - 1, 0)]).normalized;
                Vector3 n = Vector3.Cross(tangent, direction).normalized;
                if (n.sqrMagnitude < 1e-4f)
                    n = Vector3.Cross(tangent, Vector3.right).normalized;
                normals[k] = n;
                binormals[k] = Vector3.Cross(tangent, n);
                for (int s = 0; s <= sides; s++)
                {
                    float a = 2f * Mathf.PI * s / sides;
                    Vector3 outward = n * Mathf.Cos(a) + binormals[k] * Mathf.Sin(a);
                    shape.Vertex(points[k] + outward * radii[k], outward, new Vector2((float)s / sides, (float)k / segments));
                }
            }
            for (int k = 0; k < segments; k++)
            {
                for (int s = 0; s < sides; s++)
                {
                    int a = first + k * (sides + 1) + s, b = a + sides + 1;
                    shape.Triangle(0, a, a + 1, b + 1);
                    shape.Triangle(0, a, b + 1, b);
                }
            }

            // Thorns sticking out along it, leaning toward the tip.
            for (int k = 2; k < segments - 1; k += 2)
            {
                float a = Range(0f, 2f * Mathf.PI);
                Vector3 outward = normals[k] * Mathf.Cos(a) + binormals[k] * Mathf.Sin(a);
                Vector3 along = (points[k + 1] - points[k - 1]).normalized;
                Vector3 baseCenter = points[k] + outward * (radii[k] * 0.7f);
                Vector3 tip = points[k] + outward * (radii[k] + Range(0.05f, 0.1f)) + along * 0.03f;
                Vector3 u = Vector3.Cross(outward, along).normalized * 0.018f, v = along * 0.018f;
                Vector3[] corners = { baseCenter + u, baseCenter + v, baseCenter - u, baseCenter - v };
                // Thorns appear when the vine has grown up to them.
                float reached = (float)k / segments;
                for (int c = 0; c < 4; c++)
                    shape.Facet(1, tip, corners[c], corners[(c + 1) % 4], baseCenter, reached, reached, reached);
            }
        }

        // ---- Building ----

        class Shape
        {
            readonly List<Vector3> m_Vertices = new List<Vector3>();
            readonly List<Vector3> m_Normals = new List<Vector3>();
            readonly List<Vector2> m_Uvs = new List<Vector2>();
            readonly List<int>[] m_Triangles;

            public Shape(int submeshes)
            {
                m_Triangles = new List<int>[submeshes];
                for (int i = 0; i < submeshes; i++)
                    m_Triangles[i] = new List<int>();
            }

            public int VertexCount => m_Vertices.Count;

            public void Vertex(Vector3 p, Vector3 n, Vector2 uv)
            {
                m_Vertices.Add(p);
                m_Normals.Add(n);
                m_Uvs.Add(uv);
            }

            public void Triangle(int submesh, int a, int b, int c)
            {
                m_Triangles[submesh].Add(a);
                m_Triangles[submesh].Add(b);
                m_Triangles[submesh].Add(c);
            }

            // Flat triangle facing away from inside (a point within the solid it belongs to).
            public void Facet(int submesh, Vector3 a, Vector3 b, Vector3 c, Vector3 inside)
            {
                Facet(submesh, a, b, c, inside, 1f, 0f, 0f);
            }

            // Same, with the v coordinate of each corner.
            public void Facet(int submesh, Vector3 a, Vector3 b, Vector3 c, Vector3 inside, float va, float vb, float vc)
            {
                Vector3 n = Vector3.Cross(b - a, c - a).normalized;
                if (Vector3.Dot(n, (a + b + c) / 3f - inside) < 0f)
                {
                    (b, c) = (c, b);
                    (vb, vc) = (vc, vb);
                    n = -n;
                }
                int i = m_Vertices.Count;
                Vertex(a, n, new Vector2(0.5f, va));
                Vertex(b, n, new Vector2(0f, vb));
                Vertex(c, n, new Vector2(1f, vc));
                Triangle(submesh, i, i + 1, i + 2);
            }

            public Mesh ToMesh(string name)
            {
                var mesh = new Mesh { name = name };
                mesh.SetVertices(m_Vertices);
                mesh.SetNormals(m_Normals);
                mesh.SetUVs(0, m_Uvs);
                mesh.subMeshCount = m_Triangles.Length;
                for (int i = 0; i < m_Triangles.Length; i++)
                    mesh.SetTriangles(m_Triangles[i], i);
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
