using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace RuneArena.Arena.Editor
{
    // Collects quads into a mesh, one submesh per material. Every quad gets its own vertices,
    // so edges stay hard; that's what stone blocks want anyway.
    class MeshBuilder
    {
        readonly List<Vector3> m_Vertices = new List<Vector3>();
        readonly List<Vector3> m_Normals = new List<Vector3>();
        readonly List<Vector2> m_Uvs = new List<Vector2>();
        readonly List<int>[] m_Triangles;
        // Submesh of each quad, in the order they were added (quad i uses vertices 4i to 4i + 3).
        readonly List<int> m_QuadSubmesh = new List<int>();
        int[] m_TriangleStarts;

        public MeshBuilder(int submeshes = 1)
        {
            m_Triangles = new List<int>[submeshes];
            for (int i = 0; i < submeshes; i++)
                m_Triangles[i] = new List<int>();
        }

        public bool IsEmpty => m_Vertices.Count == 0;
        public int VertexCount => m_Vertices.Count;

        // Adds a quad and flips its winding if needed so the face points along its normals.
        public void Quad(int submesh,
                         Vector3 a, Vector3 b, Vector3 c, Vector3 d,
                         Vector3 na, Vector3 nb, Vector3 nc, Vector3 nd,
                         Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
        {
            int i = m_Vertices.Count;
            m_QuadSubmesh.Add(submesh);
            m_Vertices.Add(a); m_Vertices.Add(b); m_Vertices.Add(c); m_Vertices.Add(d);
            m_Normals.Add(na); m_Normals.Add(nb); m_Normals.Add(nc); m_Normals.Add(nd);
            m_Uvs.Add(ua); m_Uvs.Add(ub); m_Uvs.Add(uc); m_Uvs.Add(ud);

            List<int> tris = m_Triangles[submesh];
            Vector3 face = Vector3.Cross(b - a, c - a) + Vector3.Cross(c - a, d - a);
            if (Vector3.Dot(face, na + nb + nc + nd) >= 0f)
            {
                tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
                tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
            }
            else
            {
                tris.Add(i); tris.Add(i + 2); tris.Add(i + 1);
                tris.Add(i); tris.Add(i + 3); tris.Add(i + 2);
            }
        }

        // Flat quad.
        public void Quad(int submesh, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 n,
                         Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
        {
            Quad(submesh, a, b, c, d, n, n, n, n, ua, ub, uc, ud);
        }

        // Splits the quads into pie slices around the Y axis, by the angle of their center. Keeps each mesh
        // small: under 65k vertices Unity uses 16-bit indices, and static batching and culling work per slice.
        public List<MeshBuilder> SplitByAngle(int slices, float startAngle)
        {
            var parts = new List<MeshBuilder>();
            for (int i = 0; i < slices; i++)
                parts.Add(new MeshBuilder(m_Triangles.Length));

            for (int q = 0; q < m_QuadSubmesh.Count; q++)
            {
                int v = q * 4;
                Vector3 center = (m_Vertices[v] + m_Vertices[v + 1] + m_Vertices[v + 2] + m_Vertices[v + 3]) * 0.25f;
                float angle = Mathf.Repeat(Mathf.Atan2(center.x, center.z) * Mathf.Rad2Deg - startAngle, 360f);
                int slice = Mathf.Min((int)(angle / 360f * slices), slices - 1);
                // The winding was already fixed when the quad was added, so copy the triangles as they are.
                parts[slice].CopyQuad(this, q);
            }
            return parts;
        }

        void CopyQuad(MeshBuilder from, int quad)
        {
            int v = quad * 4;
            int submesh = from.m_QuadSubmesh[quad];
            int i = m_Vertices.Count;
            m_QuadSubmesh.Add(submesh);
            for (int k = 0; k < 4; k++)
            {
                m_Vertices.Add(from.m_Vertices[v + k]);
                m_Normals.Add(from.m_Normals[v + k]);
                m_Uvs.Add(from.m_Uvs[v + k]);
            }

            // Find this quad's six indices in its submesh: they are stored in quad order there too.
            List<int> source = from.m_Triangles[submesh];
            int start = from.TriangleStart(quad);
            List<int> tris = m_Triangles[submesh];
            for (int k = 0; k < 6; k++)
                tris.Add(source[start + k] - v + i);
        }

        // Where a quad's indices start in its submesh's list.
        int TriangleStart(int quad)
        {
            if (m_TriangleStarts == null)
            {
                m_TriangleStarts = new int[m_QuadSubmesh.Count];
                var counts = new int[m_Triangles.Length];
                for (int q = 0; q < m_QuadSubmesh.Count; q++)
                {
                    int s = m_QuadSubmesh[q];
                    m_TriangleStarts[q] = counts[s];
                    counts[s] += 6;
                }
            }
            return m_TriangleStarts[quad];
        }

        public Mesh ToMesh()
        {
            var mesh = new Mesh();
            if (m_Vertices.Count > 65535)
                mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(m_Vertices);
            mesh.SetNormals(m_Normals);
            mesh.SetUVs(0, m_Uvs);
            mesh.subMeshCount = m_Triangles.Length;
            for (int i = 0; i < m_Triangles.Length; i++)
                mesh.SetTriangles(m_Triangles[i], i);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
