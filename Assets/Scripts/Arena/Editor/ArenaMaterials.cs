using System.IO;
using UnityEditor;
using UnityEngine;

namespace RuneArena.Arena.Editor
{
    // Materials for the colosseum, made from the textures that tools/textures/generate.py writes.
    // Only fills in what's missing: a material that already has a texture is left as it is, so
    // changes made by hand in the Inspector survive a rebuild.
    static class ArenaMaterials
    {
        public const string TextureFolder = "Assets/Textures/Arena";
        const string k_MaterialFolder = "Assets/Materials";

        // World size of one tile of each texture, in meters. Keep in sync with generate.py.
        const float k_SandTile = 3f;
        const float k_TravertineTile = 3f;
        const float k_MarbleTile = 2f;
        const float k_GraniteTile = 1.5f;

        // URP Lit multiplies the base color by the detail texture times this (linear 0.5 grey reads as 1).
        const float k_DetailNeutral = 4.59479380f;

        public static void Setup(ColosseumBuilder b)
        {
            b.WallMaterial = Stone(b.WallMaterial, "Arena Wall", "Travertine", k_TravertineTile, Color.white);
            b.TrimMaterial = Stone(b.TrimMaterial, "Arena Trim", "Marble", k_MarbleTile, Color.white);
            b.SeatMaterial = Stone(b.SeatMaterial, "Arena Seats", "Marble", k_MarbleTile, new Color(0.9f, 0.86f, 0.8f));
            b.PlinthMaterial = Stone(b.PlinthMaterial, "Arena Plinth", "Granite", k_GraniteTile, Color.white);
            b.FloorMaterial = Stone(b.FloorMaterial, "Arena Floor", "Sand", k_SandTile, Color.white);
            b.GateMaterial = Cutout(b.GateMaterial, "Arena Gate", "Gate", 0.4f, false);
            b.BannerMaterial = Cutout(b.BannerMaterial, "Arena Banner", "Banner", 0.15f, true);
            b.DarkMaterial = Plain(b.DarkMaterial, "Arena Dark", new Color(0.05f, 0.045f, 0.04f), 0f);
            b.WoodMaterial = Plain(b.WoodMaterial, "Arena Wood", new Color(0.33f, 0.23f, 0.16f), 0.15f);
            b.TunnelMaterial = Stone(b.TunnelMaterial, "Arena Tunnel", "Travertine", k_TravertineTile, new Color(0.3f, 0.28f, 0.25f));
            b.BronzeMaterial = Plain(b.BronzeMaterial, "Arena Bronze", new Color(0.62f, 0.42f, 0.22f), 0.55f, 1f);
            b.GlowMaterial = Glowing(b.GlowMaterial, "Arena Rune Glow", new Color(0.35f, 0.85f, 1f) * 2.5f);
            AssetDatabase.SaveAssets();
        }

        // The floor's material shows the sand tiled over the arena, times a layout texture that covers
        // it once: shade at the foot of the wall and around the pillars, worn paths from the doors, the
        // ring in the middle. The floor mesh's UVs go from 0 to 1 across the whole arena.
        public static void SetupFloor(ColosseumBuilder b, string layoutPath)
        {
            Material material = b.FloorMaterial;
            float tiles = 2f * b.Radius / k_SandTile;
            material.SetTextureScale("_BaseMap", Vector2.one * tiles);

            Texture2D layout = FloorLayout(b, layoutPath);
            material.SetTexture("_DetailAlbedoMap", layout);
            material.SetTextureScale("_DetailAlbedoMap", Vector2.one / tiles);
            material.SetFloat("_DetailAlbedoMapScale", 1f);
            material.EnableKeyword("_DETAIL_MULX2");
            EditorUtility.SetDirty(material);
        }

        static Material Stone(Material material, string name, string texture, float tile, Color tint)
        {
            material = FindOrCreate(material, name);
            if (material.GetTexture("_BaseMap") != null)
                return material;

            var albedo = Load(texture + "_Albedo");
            if (albedo == null)
                return material;

            material.SetTexture("_BaseMap", albedo);
            material.SetColor("_BaseColor", tint);
            material.SetTextureScale("_BaseMap", Vector2.one / tile);
            // Smoothness is in the albedo's alpha.
            material.SetFloat("_Smoothness", 1f);
            material.SetFloat("_SmoothnessTextureChannel", 1f);
            material.EnableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
            SetNormal(material, texture);
            EditorUtility.SetDirty(material);
            return material;
        }

        // The banner of one rune, from its Banner_<label> textures.
        public static Material Banner(string label)
        {
            return Cutout(null, $"Arena Banner {label}", $"Banner_{label}", 0.15f, true);
        }

        // Alpha-tested: the gaps in the gate and the cut at the bottom of the banners.
        static Material Cutout(Material material, string name, string texture, float smoothness, bool twoSided)
        {
            material = FindOrCreate(material, name);
            if (material.GetTexture("_BaseMap") != null)
                return material;

            var albedo = Load(texture + "_Albedo");
            if (albedo == null)
                return material;

            material.SetTexture("_BaseMap", albedo);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_AlphaClip", 1f);
            material.SetFloat("_Cutoff", 0.5f);
            material.EnableKeyword("_ALPHATEST_ON");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            material.SetOverrideTag("RenderType", "TransparentCutout");
            if (twoSided)
                material.SetFloat("_Cull", 0f);
            SetNormal(material, texture);
            EditorUtility.SetDirty(material);
            return material;
        }

        static Material Plain(Material material, string name, Color color, float smoothness, float metallic = 0f)
        {
            if (material != null || Exists(name))
                return material != null ? material : FindOrCreate(null, name);
            material = FindOrCreate(null, name);
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", metallic);
            EditorUtility.SetDirty(material);
            return material;
        }

        // Lit by itself: the base color hardly matters, the emission is what shows.
        static Material Glowing(Material material, string name, Color emission)
        {
            if (material != null || Exists(name))
                return material != null ? material : FindOrCreate(null, name);
            material = FindOrCreate(null, name);
            material.SetColor("_BaseColor", new Color(0.1f, 0.2f, 0.25f));
            material.SetColor("_EmissionColor", emission);
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(material);
            return material;
        }

        static void SetNormal(Material material, string texture)
        {
            var normal = Load(texture + "_Normal");
            if (normal == null)
                return;
            material.SetTexture("_BumpMap", normal);
            material.SetFloat("_BumpScale", 1f);
            material.EnableKeyword("_NORMALMAP");
        }

        static Texture2D Load(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Texture2D>($"{TextureFolder}/{name}.png");
        }

        // Materials for what the player holds: the wand and the bracer.
        public static void Props(out Material wood, out Material bronze, out Material leather, out Material glow)
        {
            wood = Plain(null, "Arena Wood", new Color(0.33f, 0.23f, 0.16f), 0.15f);
            bronze = Plain(null, "Arena Bronze", new Color(0.62f, 0.42f, 0.22f), 0.55f, 1f);
            leather = Plain(null, "Arena Leather", new Color(0.24f, 0.14f, 0.08f), 0.3f);
            glow = Glowing(null, "Arena Rune Glow", new Color(0.35f, 0.85f, 1f) * 2.5f);
            AssetDatabase.SaveAssets();
        }

        // The grimoire's pages: one page of the Parchment texture over each, not tiled. The paper also lights
        // itself a little, so the writing can still be read with the book in shade.
        public static Material Parchment()
        {
            Material material = FindOrCreate(null, "Arena Parchment");
            if (material.GetTexture("_BaseMap") != null && material.IsKeywordEnabled("_EMISSION"))
                return material;

            var albedo = Load("Parchment_Albedo");
            material.SetTexture("_BaseMap", albedo);
            material.SetColor("_BaseColor", albedo != null ? Color.white : new Color(0.86f, 0.78f, 0.62f));
            material.SetFloat("_Smoothness", 0.1f);
            material.SetTexture("_EmissionMap", albedo);
            material.SetColor("_EmissionColor", new Color(0.45f, 0.45f, 0.45f));
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            return material;
        }

        static bool Exists(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Material>($"{k_MaterialFolder}/{name}.mat") != null;
        }

        static Material FindOrCreate(Material material, string name)
        {
            if (material != null)
                return material;
            string path = $"{k_MaterialFolder}/{name}.mat";
            material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;

            ColosseumBuilderEditor.EnsureFolder(k_MaterialFolder);
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static Texture2D FloorLayout(ColosseumBuilder b, string path)
        {
            const int size = 2048;
            float radius = b.Radius;
            float inner = b.InnerRadius;
            var pixels = new Color[size * size];

            var doors = new Vector2[b.DoorCount];
            for (int i = 0; i < b.DoorCount; i++)
            {
                float a = (b.DoorAngle + i * 360f / b.DoorCount) * Mathf.Deg2Rad;
                doors[i] = new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * inner;
            }
            var pillars = new Vector2[b.PillarCount];
            for (int i = 0; i < b.PillarCount; i++)
            {
                float a = (b.PillarAngle + i * 360f / b.PillarCount) * Mathf.Deg2Rad;
                pillars[i] = new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * b.PillarDistance;
            }
            float pillarFoot = b.PillarWidth * 0.65f;
            var wet = new Color(0.62f, 0.56f, 0.52f);
            var dust = new Color(1.08f, 1.06f, 1.02f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Texture u goes along +X and v along +Z, the way the floor mesh maps them.
                    var p = new Vector2((x + 0.5f) / size * 2f - 1f, (y + 0.5f) / size * 2f - 1f) * radius;
                    float r = p.magnitude;

                    float broad = Fbm(p.x / 7f, p.y / 7f);
                    Color c = Color.white * Mathf.Lerp(0.86f, 1.1f, broad);

                    float stain = Smooth(0.66f, 0.76f, Mathf.PerlinNoise(p.x / 2.3f + 100f, p.y / 2.3f + 40f));
                    c *= Color.Lerp(Color.white, wet, stain * 0.6f);

                    // Worn, lighter paths from each door to the middle.
                    foreach (Vector2 door in doors)
                    {
                        float d = DistanceToSegment(p, door, Vector2.zero);
                        float wobble = (Mathf.PerlinNoise(p.x / 3f, p.y / 3f) - 0.5f) * 1.5f;
                        c *= Color.Lerp(Color.white, dust, 1f - Smooth(0.8f, 2.6f, d + wobble));
                    }

                    // Contact shade where the floor meets the wall and the pillars.
                    c *= 1f - 0.45f * Smooth(inner - 2f, inner, r);
                    foreach (Vector2 pillar in pillars)
                        c *= Mathf.Lerp(0.6f, 1f, Smooth(pillarFoot, pillarFoot + 1.5f, Vector2.Distance(p, pillar)));

                    c *= 1f - 0.3f * CenterRing(p, r);

                    c.r = Mathf.LinearToGammaSpace(Mathf.Clamp01(c.r / k_DetailNeutral));
                    c.g = Mathf.LinearToGammaSpace(Mathf.Clamp01(c.g / k_DetailNeutral));
                    c.b = Mathf.LinearToGammaSpace(Mathf.Clamp01(c.b / k_DetailNeutral));
                    c.a = 1f;
                    pixels[y * size + x] = c;
                }
            }

            var texture = new Texture2D(size, size, TextureFormat.RGB24, false);
            texture.SetPixels(pixels);
            ColosseumBuilderEditor.EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // Two thin rings in the middle of the arena, with twelve marks between them.
        static float CenterRing(Vector2 p, float r)
        {
            float line = Line(Mathf.Abs(r - 4f), 0.07f) + Line(Mathf.Abs(r - 4.6f), 0.05f);
            if (r > 4f && r < 4.6f)
            {
                float angle = Mathf.Atan2(p.x, p.y) * Mathf.Rad2Deg;
                float mark = Mathf.Abs(Mathf.DeltaAngle(angle, Mathf.Round(angle / 30f) * 30f)) * Mathf.Deg2Rad * r;
                line += Line(mark, 0.05f);
            }
            return Mathf.Clamp01(line);
        }

        static float Line(float distance, float halfWidth)
        {
            return 1f - Smooth(halfWidth, halfWidth + 0.03f, distance);
        }

        static float Fbm(float x, float y)
        {
            float sum = 0f, amp = 0.5f, total = 0f;
            for (int i = 0; i < 3; i++)
            {
                sum += Mathf.PerlinNoise(x + i * 17.3f, y + i * 31.7f) * amp;
                total += amp;
                x *= 2.1f;
                y *= 2.1f;
                amp *= 0.5f;
            }
            return sum / total;
        }

        static float Smooth(float lo, float hi, float x)
        {
            float t = Mathf.Clamp01((x - lo) / (hi - lo));
            return t * t * (3f - 2f * t);
        }

        static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }
    }

    // Import settings for the generated textures, the first time each one comes in.
    class ArenaTextureImport : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            bool generated = assetPath.StartsWith(ArenaMaterials.TextureFolder + "/") || assetPath.StartsWith("Assets/Textures/Fx/");
            bool layout = assetPath.EndsWith("Floor Layout.png");
            if (!generated && !layout)
                return;

            var importer = (TextureImporter)assetImporter;
            if (!importer.importSettingsMissing && !layout)
                return;

            string name = Path.GetFileNameWithoutExtension(assetPath);
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.anisoLevel = 4;

            if (layout)
            {
                importer.sRGBTexture = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.maxTextureSize = 2048;
                return;
            }

            if (name.EndsWith("_Normal"))
            {
                importer.textureType = TextureImporterType.NormalMap;
            }
            else if (name.StartsWith("Sky_"))
            {
                // A panorama, imported as a cubemap so it works as the skybox and as the reflections.
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.textureShape = TextureImporterShape.TextureCube;
                settings.generateCubemap = TextureImporterGenerateCubemap.Cylindrical;
                settings.cubemapConvolution = TextureImporterCubemapConvolution.None;
                settings.seamlessCubemap = true;
                importer.SetTextureSettings(settings);
                importer.maxTextureSize = 4096;
                importer.anisoLevel = 0;
                importer.wrapMode = TextureWrapMode.Clamp;
            }
            else
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
            }

            // Cutouts: their mips keep the alpha coverage. Gates and banners are one picture each and clamp;
            // the ivy is tiled by its material, so it keeps repeating.
            bool ivy = name.StartsWith("Ivy_");
            if (name.StartsWith("Gate") || name.StartsWith("Banner") || ivy)
            {
                importer.wrapMode = ivy ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                importer.mipMapsPreserveCoverage = true;
                importer.alphaTestReferenceValue = 0.5f;
            }
            if (name.StartsWith("Sand"))
                importer.anisoLevel = 8;
        }
    }
}
