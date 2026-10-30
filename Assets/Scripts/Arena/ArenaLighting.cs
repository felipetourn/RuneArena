using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace RuneArena.Arena
{
    // Time of day for the arena: sun, sky, ambient light and fog, switched between presets.
    // Everything is realtime (nothing baked), so switching works in the editor and in the game.
    public class ArenaLighting : MonoBehaviour
    {
        [Serializable]
        public class Preset
        {
            public string Name;

            [Header("Sun")]
            [Tooltip("Degrees above the horizon. Must match the sky texture (tools/textures/arena/sky.py).")]
            public float Elevation;
            [Tooltip("Rotation around Y, the light's own Y angle.")]
            public float Azimuth;
            public Color SunColor = Color.white;
            public float SunIntensity = 1f;
            [Range(0f, 1f)] public float ShadowStrength = 0.9f;

            [Header("Sky")]
            public Cubemap Sky;
            public float SkyExposure = 1f;
            [Tooltip("Turns the sky around Y, in case its sun doesn't line up with the light.")]
            public float SkyRotation;

            [Header("Ambient")]
            public Color AmbientSky = Color.grey;
            public Color AmbientEquator = Color.grey;
            public Color AmbientGround = Color.grey;
            [Range(0f, 2f)] public float ReflectionIntensity = 1f;

            [Header("Fog")]
            public Color FogColor = Color.grey;
            [Tooltip("Exponential fog. 0.005 leaves about 80% of the far door visible.")]
            public float FogDensity = 0.005f;
        }

        [SerializeField] Light m_Sun;
        [Tooltip("A material with the Skybox/Cubemap shader. The preset's sky goes in it.")]
        [SerializeField] Material m_SkyMaterial;
        [SerializeField] int m_Current;
        [SerializeField] Preset[] m_Presets =
        {
            new Preset
            {
                Name = "Mediodía",
                Elevation = 62f,
                Azimuth = 140f,
                SunColor = new Color(1f, 0.96f, 0.9f),
                SunIntensity = 1.3f,
                ShadowStrength = 0.85f,
                AmbientSky = new Color(0.52f, 0.62f, 0.78f),
                AmbientEquator = new Color(0.56f, 0.56f, 0.54f),
                AmbientGround = new Color(0.36f, 0.31f, 0.25f),
                FogColor = new Color(0.69f, 0.78f, 0.88f),
                FogDensity = 0.004f,
            },
            new Preset
            {
                Name = "Atardecer",
                Elevation = 20f,
                Azimuth = 250f,
                SunColor = new Color(1f, 0.66f, 0.4f),
                SunIntensity = 1.4f,
                ShadowStrength = 0.8f,
                AmbientSky = new Color(0.36f, 0.38f, 0.58f),
                AmbientEquator = new Color(0.6f, 0.44f, 0.38f),
                AmbientGround = new Color(0.26f, 0.2f, 0.2f),
                FogColor = new Color(0.78f, 0.55f, 0.45f),
                FogDensity = 0.006f,
            },
        };

        Material m_RuntimeSky;

        public int Current => m_Current;
        public int Count => m_Presets.Length;
        public string CurrentName => m_Presets.Length > 0 ? m_Presets[m_Current].Name : "";

        void Start()
        {
            Apply(m_Current);
        }

        public void Next()
        {
            if (m_Presets.Length > 0)
                Apply((m_Current + 1) % m_Presets.Length);
        }

        public void Apply(int index)
        {
            if (m_Presets.Length == 0)
                return;
            m_Current = Mathf.Clamp(index, 0, m_Presets.Length - 1);
            Preset p = m_Presets[m_Current];

            if (m_Sun != null)
            {
                m_Sun.transform.rotation = Quaternion.Euler(p.Elevation, p.Azimuth, 0f);
                m_Sun.color = p.SunColor;
                m_Sun.intensity = p.SunIntensity;
                m_Sun.shadowStrength = p.ShadowStrength;
                RenderSettings.sun = m_Sun;
            }

            Material sky = SkyMaterial();
            if (sky != null && p.Sky != null)
            {
                sky.SetTexture("_Tex", p.Sky);
                sky.SetFloat("_Exposure", p.SkyExposure);
                sky.SetFloat("_Rotation", p.SkyRotation);
                RenderSettings.skybox = sky;
                // Shiny surfaces reflect the same sky.
                RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
                RenderSettings.customReflectionTexture = p.Sky;
            }
            RenderSettings.reflectionIntensity = p.ReflectionIntensity;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = p.AmbientSky;
            RenderSettings.ambientEquatorColor = p.AmbientEquator;
            RenderSettings.ambientGroundColor = p.AmbientGround;

            RenderSettings.fog = p.FogDensity > 0f;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = p.FogColor;
            RenderSettings.fogDensity = p.FogDensity;

            DynamicGI.UpdateEnvironment();
        }

        // In play mode, a copy, so switching presets doesn't change the material asset.
        Material SkyMaterial()
        {
            if (m_SkyMaterial == null || !Application.isPlaying)
                return m_SkyMaterial;
            if (m_RuntimeSky == null)
                m_RuntimeSky = new Material(m_SkyMaterial);
            return m_RuntimeSky;
        }

        void OnDestroy()
        {
            if (m_RuntimeSky != null)
                Destroy(m_RuntimeSky);
        }

#if UNITY_EDITOR
        // Fills in the references when the component is added.
        void Reset()
        {
            m_Sun = GetComponentInChildren<Light>();
            if (m_Sun == null)
                m_Sun = RenderSettings.sun;

            const string folder = "Assets/Textures/Arena";
            if (m_Presets.Length > 0)
                m_Presets[0].Sky = UnityEditor.AssetDatabase.LoadAssetAtPath<Cubemap>($"{folder}/Sky_Noon.png");
            if (m_Presets.Length > 1)
                m_Presets[1].Sky = UnityEditor.AssetDatabase.LoadAssetAtPath<Cubemap>($"{folder}/Sky_Sunset.png");

            const string materialPath = "Assets/Materials/Arena Sky.mat";
            m_SkyMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (m_SkyMaterial == null)
            {
                m_SkyMaterial = new Material(Shader.Find("Skybox/Cubemap"));
                UnityEditor.AssetDatabase.CreateAsset(m_SkyMaterial, materialPath);
            }
        }
#endif
    }
}
