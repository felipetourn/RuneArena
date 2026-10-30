using UnityEngine;
using UnityEngine.XR;

namespace RuneArena.Effects
{
    // One-shot sounds in the world, and the controllers' vibration.
    public static class Sfx
    {
        static SfxLibrary s_Library;
        static bool s_Loaded;

        public static SfxLibrary Library
        {
            get
            {
                if (!s_Loaded)
                {
                    s_Library = Resources.Load<SfxLibrary>("SfxLibrary");
                    s_Loaded = true;
                }
                return s_Library;
            }
        }

        // Plays where it happens, a little higher or lower each time so repeats don't sound identical.
        public static AudioSource Play(AudioClip clip, Vector3 position, float volume = 1f, float pitchJitter = 0.08f)
        {
            if (clip == null)
                return null;
            var go = new GameObject("Sfx " + clip.name);
            go.transform.position = position;
            AudioSource source = Source(go, clip, volume);
            source.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            source.Play();
            Object.Destroy(go, clip.length / Mathf.Max(source.pitch, 0.1f) + 0.1f);
            return source;
        }

        // A looping sound following target, silent until started. Turn it up and down with its volume.
        public static AudioSource Loop(AudioClip clip, Transform target)
        {
            if (clip == null)
                return null;
            var go = new GameObject("Sfx Loop " + clip.name);
            go.transform.SetParent(target, false);
            AudioSource source = Source(go, clip, 0f);
            source.loop = true;
            source.Play();
            return source;
        }

        public static void Haptic(XRNode hand, float amplitude, float seconds)
        {
            InputDevice device = InputDevices.GetDeviceAtXRNode(hand);
            if (device.isValid)
                device.SendHapticImpulse(0u, Mathf.Clamp01(amplitude), seconds);
        }

        static AudioSource Source(GameObject go, AudioClip clip, float volume)
        {
            var source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.volume = volume;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = 2f;
            source.maxDistance = 60f;
            source.dopplerLevel = 0f;
            return source;
        }
    }
}
