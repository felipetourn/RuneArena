using UnityEngine;

namespace RuneArena.Effects
{
    // Short-lived point lights: a flash when something explodes, a glow carried by a fireball.
    // At most a few at once: on the Quest each one costs on every surface it reaches.
    public class FxLight : MonoBehaviour
    {
        const int k_MaxLights = 4;
        static int s_Count;

        Light m_Light;
        float m_Intensity;
        float m_FadeTime;
        float m_FadeStart = -1f;

        public static FxLight Flash(Vector3 position, Color color, float range, float intensity, float seconds)
        {
            FxLight light = Create(null, color, range, intensity);
            if (light == null)
                return null;
            light.transform.position = position;
            light.FadeOut(seconds);
            return light;
        }

        public static FxLight Attach(Transform parent, Color color, float range, float intensity)
        {
            return Create(parent, color, range, intensity);
        }

        static FxLight Create(Transform parent, Color color, float range, float intensity)
        {
            if (s_Count >= k_MaxLights)
                return null;
            var go = new GameObject("Fx Light");
            if (parent != null)
                go.transform.SetParent(parent, false);
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.range = range;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
            var fx = go.AddComponent<FxLight>();
            fx.m_Light = light;
            fx.m_Intensity = intensity;
            return fx;
        }

        public void FadeOut(float seconds)
        {
            m_FadeTime = Mathf.Max(seconds, 0.01f);
            m_FadeStart = Time.time;
        }

        void OnEnable()
        {
            s_Count++;
        }

        void OnDisable()
        {
            s_Count--;
        }

        void Update()
        {
            if (m_FadeStart < 0f)
                return;
            float t = (Time.time - m_FadeStart) / m_FadeTime;
            m_Light.intensity = m_Intensity * (1f - t) * (1f - t);
            if (t >= 1f)
                Destroy(gameObject);
        }
    }
}
