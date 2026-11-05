using System;
using System.Collections.Generic;
using UnityEngine;

namespace RuneArena.Arena.Editor
{
    // Scenery around the arena, built as one mesh with a submesh per material: the trim on the arena
    // wall, the seats in tiers, the arcade at the top. Angles are in radians, 0 is +Z, growing toward +X,
    // like the arena wall. UVs are in meters, so the materials tile by world size.
    static class ColosseumDecor
    {
        public const int Stone = 0;
        public const int Trim = 1;
        public const int Plinth = 2;
        public const int Seats = 3;
        public const int Dark = 4;
        public const int Wood = 5;
        public const int Banner = 6;
        public const int Shade = 7;
        public const int SubmeshCount = 8;

        [Flags]
        enum Faces
        {
            Inner = 1,
            Outer = 2,
            Top = 4,
            Bottom = 8,
            Ends = 16,
        }

        const int k_Segments = 256;
        const float k_PlinthHeight = 0.35f;
        const float k_PlinthDepth = 0.06f;
        const float k_FrameWidth = 0.25f;
        const float k_FrameDepth = 0.08f;
        const float k_CorniceHeight = 0.28f;
        const float k_ParapetHeight = 1f;
        const float k_ParapetThickness = 0.35f;
        const float k_EntranceWidth = 1.6f;
        const float k_EntranceDepth = 1.4f;
        const float k_GalleryWidth = 3f;
        const float k_ArcadeThickness = 1.2f;
        const float k_BannerWidth = 1.2f;
        const float k_BannerHeight = 3f;
        const float k_SeatLip = 0.06f;
        const float k_SeatLipHeight = 0.1f;
        const float k_TunnelLength = 7f;
        const float k_TunnelLit = 2f;

        static float s_Middle;

        public static MeshBuilder Build(ColosseumBuilder b)
        {
            var m = new MeshBuilder(SubmeshCount);
            s_Middle = b.Radius - b.Thickness * 0.5f;
            WallTrim(m, b);
            Banners(m, b);
            Tunnels(m, b);
            if (b.BuildStands)
                Stands(m, b);
            return m;
        }

        // ---- Arena wall ----

        static void WallTrim(MeshBuilder m, ColosseumBuilder b)
        {
            float ri = b.InnerRadius;
            float h = b.Height;
            float half = b.DoorWidth * 0.5f;
            float spring = b.DoorHeight - half;

            // Plinth along the foot of the wall, stopping at the door frames.
            var gaps = new List<Vector2>();
            for (int i = 0; i < b.DoorCount; i++)
                gaps.Add(new Vector2(DoorAngle(b, i), (half + k_FrameWidth) / s_Middle));
            Band(m, Plinth, ri - k_PlinthDepth, ri, 0f, k_PlinthHeight, Faces.Inner | Faces.Top | Faces.Ends, gaps);

            // Two-step cornice right under the top, over the doors too.
            Band(m, Trim, ri - 0.08f, ri, h - k_CorniceHeight, h - 0.2f, Faces.Inner | Faces.Bottom, null);
            Band(m, Trim, ri - 0.18f, ri, h - 0.2f, h, Faces.Inner | Faces.Bottom | Faces.Top, null);

            // Parapet on top, with a coping stone.
            Band(m, Trim, ri, ri + k_ParapetThickness, h, h + k_ParapetHeight, Faces.Inner | Faces.Outer, null);
            Band(m, Trim, ri - 0.06f, ri + k_ParapetThickness + 0.06f, h + k_ParapetHeight, h + k_ParapetHeight + 0.12f,
                 Faces.Inner | Faces.Outer | Faces.Top | Faces.Bottom, null);

            for (int i = 0; i < b.DoorCount; i++)
            {
                float center = DoorAngle(b, i);
                DoorFrame(m, ri, center, half, spring);
                // Keystone, sticking out a bit more than the frame.
                Box(m, Trim, center, ri - 0.07f, spring + half - 0.05f, spring + half + k_FrameWidth + 0.12f, 0.45f, 0.14f, true);
            }

            // Pilasters between the banners, with a base and a capital.
            int slots = b.BannersPerSection + 1;
            float pilasterTop = h - k_CorniceHeight;
            foreach (float angle in SectionAngles(b, slots, 0.5f))
            {
                Box(m, Trim, angle, ri - 0.05f, k_PlinthHeight, pilasterTop, 0.55f, 0.1f, false);
                Box(m, Trim, angle, ri - 0.08f, k_PlinthHeight, k_PlinthHeight + 0.15f, 0.7f, 0.16f, true);
                Box(m, Trim, angle, ri - 0.08f, pilasterTop - 0.18f, pilasterTop, 0.7f, 0.16f, true);
            }
        }

        // A marble band around the door opening on the arena side: up the jambs and over the arch.
        static void DoorFrame(MeshBuilder m, float ri, float center, float half, float spring)
        {
            // Points around the opening in (along the wall, height), and the direction away from the opening.
            var path = new List<Vector2>();
            var away = new List<Vector2>();
            path.Add(new Vector2(-half, 0f));
            away.Add(Vector2.left);
            const int arch = 16;
            for (int k = 0; k <= arch; k++)
            {
                float t = Mathf.PI * (1f - (float)k / arch);
                var n = new Vector2(Mathf.Cos(t), Mathf.Sin(t));
                path.Add(new Vector2(0f, spring) + n * half);
                away.Add(n);
            }
            path.Add(new Vector2(half, 0f));
            away.Add(Vector2.right);

            float front = ri - k_FrameDepth;
            Vector3 inward = -Out(center);
            for (int i = 0; i < path.Count - 1; i++)
            {
                Vector2 p0 = path[i], p1 = path[i + 1];
                Vector2 q0 = p0 + away[i] * k_FrameWidth, q1 = p1 + away[i + 1] * k_FrameWidth;
                Vector3 a0 = WallPoint(front, center, p0), a1 = WallPoint(front, center, p1);
                Vector3 b0 = WallPoint(front, center, q0), b1 = WallPoint(front, center, q1);

                m.Quad(Trim, a0, a1, b1, b0, inward,
                       new Vector2(p0.x, p0.y), new Vector2(p1.x, p1.y), new Vector2(q1.x, q1.y), new Vector2(q0.x, q0.y));

                // The edge on the opening side and the outer edge.
                Vector3 n0 = WallDirection(center, -away[i]), n1 = WallDirection(center, -away[i + 1]);
                m.Quad(Trim, a0, a1, WallPoint(ri, center, p1), WallPoint(ri, center, p0), n0, n1, n1, n0,
                       new Vector2(p0.x, 0f), new Vector2(p1.x, 0f), new Vector2(p1.x, k_FrameDepth), new Vector2(p0.x, k_FrameDepth));
                m.Quad(Trim, b0, b1, WallPoint(ri, center, q1), WallPoint(ri, center, q0), -n0, -n1, -n1, -n0,
                       new Vector2(q0.x, 0f), new Vector2(q1.x, 0f), new Vector2(q1.x, k_FrameDepth), new Vector2(q0.x, k_FrameDepth));
            }
        }

        // Only the rods: the cloths are built apart by BannerCloths, so each rune's can be shown on its own.
        static void Banners(MeshBuilder m, ColosseumBuilder b)
        {
            if (b.BannersPerSection == 0)
                return;
            float top = b.Height - k_CorniceHeight - 0.04f;
            float r = b.InnerRadius - 0.13f;
            foreach (float angle in SectionAngles(b, b.BannersPerSection + 1, 1f))
                Box(m, Wood, angle, r + 0.01f, top - 0.03f, top + 0.05f, k_BannerWidth + 0.2f, 0.06f, true);
        }

        // The banner cloths, one mesh per kind: going around the arena they take turns, so each kind
        // ends up spread out evenly.
        public static MeshBuilder[] BannerCloths(ColosseumBuilder b, int kinds)
        {
            var cloths = new MeshBuilder[kinds];
            for (int i = 0; i < kinds; i++)
                cloths[i] = new MeshBuilder();
            if (b.BannersPerSection == 0)
                return cloths;

            float top = b.Height - k_CorniceHeight - 0.04f;
            float r = b.InnerRadius - 0.13f;
            int k = 0;
            foreach (float angle in SectionAngles(b, b.BannersPerSection + 1, 1f))
            {
                Vector3 c = P(r, angle, 0f);
                Vector3 side = Along(angle) * (k_BannerWidth * 0.5f);
                Vector3 up = Vector3.up;
                Vector3 bottom = c + up * (top - k_BannerHeight);
                cloths[k++ % kinds].Quad(0, bottom - side, bottom + side, c + side + up * top, c - side + up * top, -Out(angle),
                                         new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f));
            }
            return cloths;
        }

        // Passage behind each door, under the seats, so the open gate shows a dark tunnel instead of the sky.
        // Straight, as wide and tall as the door, getting darker toward the end.
        static void Tunnels(MeshBuilder m, ColosseumBuilder b)
        {
            float half = b.DoorWidth * 0.5f;
            float spring = b.DoorHeight - half;
            // The door's edges on the outside face of the wall.
            float spread = half / s_Middle;
            float width = b.Radius * Mathf.Sin(spread);
            float start = b.Radius * Mathf.Cos(spread) - 0.05f;
            float end = b.Radius + k_TunnelLength;

            // Around the opening: up one side, over the vault, down the other, with normals into the tunnel.
            var profile = new List<Vector2>();
            var normals = new List<Vector2>();
            profile.Add(new Vector2(-width, 0f));
            normals.Add(Vector2.right);
            const int vault = 12;
            for (int k = 0; k <= vault; k++)
            {
                float t = Mathf.PI * (1f - (float)k / vault);
                profile.Add(new Vector2(width * Mathf.Cos(t), spring + half * Mathf.Sin(t)));
                normals.Add(-new Vector2(Mathf.Cos(t) / width, Mathf.Sin(t) / half).normalized);
            }
            profile.Add(new Vector2(width, 0f));
            normals.Add(Vector2.left);

            for (int i = 0; i < b.DoorCount; i++)
            {
                float angle = DoorAngle(b, i);
                Vector3 o = Out(angle), side = Along(angle);

                Vector3 At(Vector2 p, float distance) => o * distance + side * p.x + Vector3.up * p.y;
                Vector3 Dir(Vector2 n) => (side * n.x + Vector3.up * n.y).normalized;

                void Section(int submesh, float d0, float d1)
                {
                    for (int k = 0; k < profile.Count - 1; k++)
                    {
                        Vector2 p0 = profile[k], p1 = profile[k + 1];
                        Vector3 n0 = Dir(normals[k]), n1 = Dir(normals[k + 1]);
                        m.Quad(submesh, At(p0, d0), At(p1, d0), At(p1, d1), At(p0, d1), n0, n1, n1, n0,
                               new Vector2(k, d0), new Vector2(k + 1, d0), new Vector2(k + 1, d1), new Vector2(k, d1));
                    }
                }

                float lit = Mathf.Min(start + k_TunnelLit, end);
                Section(Stone, start, lit);
                Section(Shade, lit, end);

                // Dark wall closing the far end.
                float top = spring + half;
                m.Quad(Dark, At(new Vector2(-width, 0f), end), At(new Vector2(width, 0f), end),
                       At(new Vector2(width, top), end), At(new Vector2(-width, top), end), -o,
                       Vector2.zero, Vector2.right, Vector2.one, Vector2.up);

                // Floor past the one the enemies spawn on.
                float floor = b.Radius + b.OutsideDepth;
                if (floor < end)
                    m.Quad(Plinth, At(new Vector2(-width, 0f), floor), At(new Vector2(width, 0f), floor),
                           At(new Vector2(width, 0f), end), At(new Vector2(-width, 0f), end), Vector3.up,
                           new Vector2(-width, floor), new Vector2(width, floor), new Vector2(width, end), new Vector2(-width, end));
            }
        }

        // ---- Seats and arcade ----

        static void Stands(MeshBuilder m, ColosseumBuilder b)
        {
            float r = b.Radius;
            float y = b.Height;

            // Walk around the top of the arena wall.
            Tread(m, r, r + b.TerraceWidth, y);
            r += b.TerraceWidth;

            ColosseumBuilder.StandTier[] tiers = b.Tiers ?? Array.Empty<ColosseumBuilder.StandTier>();
            for (int t = 0; t < tiers.Length; t++)
            {
                if (t > 0)
                {
                    TierWall(m, b, r, y);
                    r += ColosseumBuilder.TierWallThickness;
                    y += b.TierWallHeight;
                }

                for (int s = 0; s < tiers[t].Steps; s++)
                {
                    Seat(m, r, y, y + tiers[t].Rise);
                    y += tiers[t].Rise;
                    Tread(m, r, r + tiers[t].Depth, y);
                    r += tiers[t].Depth;
                }

                if (t < tiers.Length - 1)
                {
                    Tread(m, r, r + b.WalkwayWidth, y);
                    r += b.WalkwayWidth;
                }
            }

            Tread(m, r, r + k_GalleryWidth, y);
            r += k_GalleryWidth;
            ArcadeAndAttic(m, b, r, y);
        }

        // Front of one row of seats: the riser, and a lighter lip along the edge of the seat. Seen from the
        // arena floor the seats themselves are out of sight, so the lips and their shadow are what mark the rows.
        static void Seat(MeshBuilder m, float r, float y0, float y1)
        {
            float lip = y1 - k_SeatLipHeight;
            Band(m, Seats, r, r, y0, lip, Faces.Inner, null);
            Band(m, Trim, r - k_SeatLip, r, lip, y1, Faces.Inner | Faces.Bottom | Faces.Top, null);
        }

        static void Tread(MeshBuilder m, float r0, float r1, float y)
        {
            Band(m, Seats, r0, r1, y, y, Faces.Top, null);
        }

        // Wall between two groups of seats, with the entrances (vomitoria) cut through it.
        static void TierWall(MeshBuilder m, ColosseumBuilder b, float r, float y)
        {
            float archHeight = Mathf.Min(2.1f, b.TierWallHeight - 0.3f);
            var wall = new Arcade
            {
                Inner = r,
                Outer = r + ColosseumBuilder.TierWallThickness,
                Bottom = y,
                Top = y + b.TierWallHeight,
                Arches = b.Entrances,
                ArchWidth = k_EntranceWidth,
                ArchHeight = archHeight,
                // Halfway between the arena doors' directions, so they don't line up with the gates.
                StartAngle = b.DoorAngle + (b.Entrances > 0 ? 180f / b.Entrances : 0f),
                ArchSegments = 10,
                Segments = k_Segments,
                Cap = true,
            };
            RingWall.Build(m, Stone, wall);

            // Behind each entrance, a dark passage so the opening doesn't show the empty space under the seats.
            float middle = (wall.Inner + wall.Outer) * 0.5f;
            float halfWidth = k_EntranceWidth * 0.5f * wall.Outer / middle + 0.02f;
            for (int i = 0; i < b.Entrances; i++)
            {
                float angle = RingWall.ArchAngle(wall, i);
                float start = wall.Outer - 0.02f;
                float end = start + k_EntranceDepth;
                Vector3 o = Out(angle), side = Along(angle);
                Vector3 a = P(start, angle, 0f), e = P(end, angle, 0f);
                float y0 = y, y1 = y + archHeight + 0.02f;
                Vector3 l0 = a - side * halfWidth, r0 = a + side * halfWidth;
                Vector3 l1 = e - side * halfWidth, r1 = e + side * halfWidth;
                Vector3 up = Vector3.up;
                Vector2 uv0 = Vector2.zero, uv1 = Vector2.right, uv2 = Vector2.one, uv3 = Vector2.up;
                m.Quad(Dark, l1 + up * y0, r1 + up * y0, r1 + up * y1, l1 + up * y1, -o, uv0, uv1, uv2, uv3);
                m.Quad(Dark, l0 + up * y0, l1 + up * y0, l1 + up * y1, l0 + up * y1, side, uv0, uv1, uv2, uv3);
                m.Quad(Dark, r0 + up * y0, r1 + up * y0, r1 + up * y1, r0 + up * y1, -side, uv0, uv1, uv2, uv3);
                m.Quad(Dark, l0 + up * y1, r0 + up * y1, r1 + up * y1, l1 + up * y1, -up, uv0, uv1, uv2, uv3);
                m.Quad(Dark, l0 + up * y0, r0 + up * y0, r1 + up * y0, l1 + up * y0, up, uv0, uv1, uv2, uv3);
            }
        }

        // The top of the building: a ring of open arches, a plain attic with windows, a cornice and the
        // masts that held the awning.
        static void ArcadeAndAttic(MeshBuilder m, ColosseumBuilder b, float r, float y)
        {
            float outer = r + k_ArcadeThickness;
            float bay = 2f * Mathf.PI * (r + k_ArcadeThickness * 0.5f) / b.ArcadeArches;
            float offset = 180f / b.ArcadeArches;

            var arcade = new Arcade
            {
                Inner = r,
                Outer = outer,
                Bottom = y,
                Top = y + b.ArcadeHeight,
                Arches = b.ArcadeArches,
                ArchWidth = bay * 0.6f,
                ArchHeight = b.ArcadeHeight - 0.9f,
                StartAngle = b.DoorAngle + offset,
                ArchSegments = 12,
                Segments = k_Segments,
                Cap = false,
            };
            RingWall.Build(m, Stone, arcade);
            // Floor of the openings, across the thickness of the wall.
            Band(m, Seats, r, outer, y, y, Faces.Top, null);

            // Half columns between the arches, and a band where the attic starts.
            for (int i = 0; i < b.ArcadeArches; i++)
            {
                float angle = RingWall.ArchAngle(arcade, i) + Mathf.PI / b.ArcadeArches;
                Box(m, Trim, angle, r - 0.08f, y, arcade.Top - 0.3f, 0.55f, 0.16f, false);
                Box(m, Trim, angle, r - 0.1f, arcade.Top - 0.3f, arcade.Top, 0.75f, 0.2f, true);
            }
            Band(m, Trim, r - 0.22f, r, arcade.Top, arcade.Top + 0.25f, Faces.Inner | Faces.Top | Faces.Bottom, null);

            float attic = arcade.Top + 0.25f;
            float lower = 0.9f;
            float upper = Mathf.Max(b.AtticHeight - lower - 1.8f, 0.6f);
            Band(m, Stone, r, outer, arcade.Top, attic + lower, Faces.Inner | Faces.Outer, null);

            var windows = new Arcade
            {
                Inner = r,
                Outer = outer,
                Bottom = attic + lower,
                Top = attic + b.AtticHeight - upper,
                Arches = b.ArcadeArches / 2,
                ArchWidth = 0.9f,
                ArchHeight = Mathf.Min(1.6f, b.AtticHeight - lower - upper),
                StartAngle = b.DoorAngle + offset,
                ArchSegments = 8,
                Segments = k_Segments,
                Cap = false,
            };
            RingWall.Build(m, Stone, windows);
            // Window sills.
            Band(m, Stone, r, outer, windows.Bottom, windows.Bottom, Faces.Top, null);

            float top = attic + b.AtticHeight;
            // The cornice covers its top.
            Band(m, Stone, r, outer, windows.Top, top, Faces.Inner | Faces.Outer, null);
            Band(m, Trim, r - 0.3f, outer + 0.1f, top - 0.35f, top, Faces.Inner | Faces.Outer | Faces.Top | Faces.Bottom, null);

            if (b.MastHeight <= 0f)
                return;
            for (int i = 0; i < b.ArcadeArches; i += 2)
            {
                float angle = RingWall.ArchAngle(arcade, i);
                Cylinder(m, Wood, P(outer - 0.35f, angle, 0f), 0.12f, top, top + b.MastHeight, 6);
            }
        }

        // ---- Shapes ----

        // A ring between two radii and two heights, minus the gaps (x = center angle, y = half angle).
        // Zero thickness or zero height is fine for a single face.
        static void Band(MeshBuilder m, int submesh, float inner, float outer, float bottom, float top, Faces faces, List<Vector2> gaps)
        {
            foreach (Vector2 span in Spans(gaps))
            {
                float a0 = span.x, a1 = span.y;
                int n = Mathf.Max(1, Mathf.CeilToInt(k_Segments * (a1 - a0) / (2f * Mathf.PI)));
                float mid = (inner + outer) * 0.5f;
                for (int i = 0; i < n; i++)
                {
                    float t0 = Mathf.Lerp(a0, a1, (float)i / n);
                    float t1 = Mathf.Lerp(a0, a1, (float)(i + 1) / n);
                    Vector3 o0 = Out(t0), o1 = Out(t1);

                    if ((faces & Faces.Inner) != 0)
                        m.Quad(submesh, P(inner, t0, bottom), P(inner, t1, bottom), P(inner, t1, top), P(inner, t0, top),
                               -o0, -o1, -o1, -o0,
                               new Vector2(t0 * inner, bottom), new Vector2(t1 * inner, bottom), new Vector2(t1 * inner, top), new Vector2(t0 * inner, top));
                    if ((faces & Faces.Outer) != 0)
                        m.Quad(submesh, P(outer, t0, bottom), P(outer, t1, bottom), P(outer, t1, top), P(outer, t0, top),
                               o0, o1, o1, o0,
                               new Vector2(t0 * outer, bottom), new Vector2(t1 * outer, bottom), new Vector2(t1 * outer, top), new Vector2(t0 * outer, top));
                    if ((faces & Faces.Top) != 0)
                        m.Quad(submesh, P(inner, t0, top), P(outer, t0, top), P(outer, t1, top), P(inner, t1, top), Vector3.up,
                               new Vector2(t0 * mid, inner), new Vector2(t0 * mid, outer), new Vector2(t1 * mid, outer), new Vector2(t1 * mid, inner));
                    if ((faces & Faces.Bottom) != 0)
                        m.Quad(submesh, P(inner, t0, bottom), P(outer, t0, bottom), P(outer, t1, bottom), P(inner, t1, bottom), Vector3.down,
                               new Vector2(t0 * mid, inner), new Vector2(t0 * mid, outer), new Vector2(t1 * mid, outer), new Vector2(t1 * mid, inner));
                }

                if ((faces & Faces.Ends) != 0 && gaps != null && gaps.Count > 0)
                {
                    EndCap(m, submesh, inner, outer, bottom, top, a0, -1f);
                    EndCap(m, submesh, inner, outer, bottom, top, a1, 1f);
                }
            }
        }

        static void EndCap(MeshBuilder m, int submesh, float inner, float outer, float bottom, float top, float angle, float side)
        {
            Vector3 n = Along(angle) * side;
            m.Quad(submesh, P(inner, angle, bottom), P(outer, angle, bottom), P(outer, angle, top), P(inner, angle, top), n,
                   new Vector2(inner, bottom), new Vector2(outer, bottom), new Vector2(outer, top), new Vector2(inner, top));
        }

        // Angle ranges of a full ring minus the gaps, as (start, end) with end > start.
        static IEnumerable<Vector2> Spans(List<Vector2> gaps)
        {
            if (gaps == null || gaps.Count == 0)
            {
                yield return new Vector2(0f, 2f * Mathf.PI);
                yield break;
            }

            var sorted = new List<Vector2>(gaps);
            sorted.Sort((a, b) => a.x.CompareTo(b.x));
            for (int i = 0; i < sorted.Count; i++)
            {
                float start = sorted[i].x + sorted[i].y;
                Vector2 next = sorted[(i + 1) % sorted.Count];
                float end = next.x - next.y + (i == sorted.Count - 1 ? 2f * Mathf.PI : 0f);
                if (end > start)
                    yield return new Vector2(start, end);
            }
        }

        // A box against a curved wall: centered at angle and radius r, width along the wall, depth toward
        // the center. Without a back face, since it always sits on something.
        static void Box(MeshBuilder m, int submesh, float angle, float r, float y0, float y1, float width, float depth, bool caps)
        {
            Vector3 o = Out(angle), side = Along(angle) * (width * 0.5f);
            Vector3 front = P(r - depth * 0.5f, angle, 0f), back = P(r + depth * 0.5f, angle, 0f);
            Vector3 up0 = Vector3.up * y0, up1 = Vector3.up * y1;
            float w = width, d = depth;

            m.Quad(submesh, front - side + up0, front + side + up0, front + side + up1, front - side + up1, -o,
                   new Vector2(0f, y0), new Vector2(w, y0), new Vector2(w, y1), new Vector2(0f, y1));
            m.Quad(submesh, front - side + up0, back - side + up0, back - side + up1, front - side + up1, -side.normalized,
                   new Vector2(0f, y0), new Vector2(d, y0), new Vector2(d, y1), new Vector2(0f, y1));
            m.Quad(submesh, front + side + up0, back + side + up0, back + side + up1, front + side + up1, side.normalized,
                   new Vector2(0f, y0), new Vector2(d, y0), new Vector2(d, y1), new Vector2(0f, y1));
            if (!caps)
                return;
            m.Quad(submesh, front - side + up1, front + side + up1, back + side + up1, back - side + up1, Vector3.up,
                   new Vector2(0f, 0f), new Vector2(w, 0f), new Vector2(w, d), new Vector2(0f, d));
            m.Quad(submesh, front - side + up0, front + side + up0, back + side + up0, back - side + up0, Vector3.down,
                   new Vector2(0f, 0f), new Vector2(w, 0f), new Vector2(w, d), new Vector2(0f, d));
        }

        static void Cylinder(MeshBuilder m, int submesh, Vector3 foot, float radius, float y0, float y1, int sides)
        {
            Vector3 top = foot + Vector3.up * y1;
            for (int i = 0; i < sides; i++)
            {
                float t0 = 2f * Mathf.PI * i / sides, t1 = 2f * Mathf.PI * (i + 1) / sides;
                Vector3 o0 = Out(t0), o1 = Out(t1);
                m.Quad(submesh, foot + o0 * radius + Vector3.up * y0, foot + o1 * radius + Vector3.up * y0,
                       foot + o1 * radius + Vector3.up * y1, foot + o0 * radius + Vector3.up * y1,
                       o0, o1, o1, o0,
                       new Vector2(t0 * radius, y0), new Vector2(t1 * radius, y0), new Vector2(t1 * radius, y1), new Vector2(t0 * radius, y1));
                m.Quad(submesh, top, top, top + o1 * radius, top + o0 * radius, Vector3.up,
                       Vector2.zero, Vector2.zero, new Vector2(o1.x, o1.z) * radius, new Vector2(o0.x, o0.z) * radius);
            }
        }

        // ---- Helpers ----

        static float DoorAngle(ColosseumBuilder b, int i)
        {
            return (b.DoorAngle + i * 360f / Mathf.Max(b.DoorCount, 1)) * Mathf.Deg2Rad;
        }

        // Evenly spaced angles between each pair of doors: slots per section, starting at shift
        // (0.5 = halfway into the first slot, 1 = at the end of it).
        static IEnumerable<float> SectionAngles(ColosseumBuilder b, int slots, float shift)
        {
            int sections = Mathf.Max(b.DoorCount, 1);
            float section = 2f * Mathf.PI / sections;
            int count = shift >= 1f ? slots - 1 : slots;
            for (int s = 0; s < sections; s++)
            {
                float start = DoorAngle(b, s);
                for (int k = 0; k < count; k++)
                    yield return start + section * (k + shift) / slots;
            }
        }

        // Point on a surface parallel to the arena wall, at radius r, from coordinates along the
        // wall's middle (x) and height (y) around a center angle.
        static Vector3 WallPoint(float r, float center, Vector2 p)
        {
            return P(r, center + p.x / s_Middle, p.y);
        }

        // Direction in the wall's surface at a center angle, from (along, up).
        static Vector3 WallDirection(float center, Vector2 d)
        {
            return (Along(center) * d.x + Vector3.up * d.y).normalized;
        }

        static Vector3 P(float r, float angle, float y)
        {
            return new Vector3(Mathf.Sin(angle) * r, y, Mathf.Cos(angle) * r);
        }

        static Vector3 Out(float angle)
        {
            return new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
        }

        // Direction of growing angle.
        static Vector3 Along(float angle)
        {
            return new Vector3(Mathf.Cos(angle), 0f, -Mathf.Sin(angle));
        }
    }
}
