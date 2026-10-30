using UnityEngine;

namespace RuneArena.Effects
{
    // The sound effects, found by Sfx in Resources. RuneArena > Create Effects fills it from
    // Assets/Audio/Sfx, which tools/textures/generate.py writes.
    public class SfxLibrary : ScriptableObject
    {
        [Header("Runes")]
        public AudioClip RuneCharged;
        public AudioClip RuneFail;
        public AudioClip RuneLaunch;
        [Tooltip("Loops while a rune is being drawn.")]
        public AudioClip DrawHum;

        [Header("Spells")]
        public AudioClip FireLaunch;
        public AudioClip FireExplode;
        public AudioClip LightningStrike;
        public AudioClip WaveSplash;
        public AudioClip EarthRise;
        public AudioClip EarthBreak;
        public AudioClip IvyGrow;
        public AudioClip IceLaunch;
        public AudioClip IceShatter;
    }
}
