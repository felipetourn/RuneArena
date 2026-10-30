using UnityEngine;

namespace RuneArena.Effects
{
    // The sound effects, found by Sfx in Resources. RuneArena > Create Effects fills it from
    // Assets/Audio/Sfx, where each file is named like its field with underscores: Fire_Launch for FireLaunch.
    // Credits for the files are in Assets/Audio/CREDITS.md.
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
