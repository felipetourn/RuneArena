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
        public AudioClip LightningLaunch;
        public AudioClip LightningStrike;
        public AudioClip WaveLaunch;
        [Tooltip("The seed lands and the earth wall rises.")]
        public AudioClip EarthRise;
        public AudioClip EarthBreak;
        [Tooltip("Ivy lands on the floor.")]
        public AudioClip IvyGrow;
        [Tooltip("Ivy lands by an earth wall and its roots come out (the combo).")]
        public AudioClip IvyRoots;
        public AudioClip IceLaunch;
        public AudioClip IceShatter;

        [Header("Pickups")]
        public AudioClip HealthPickup;
        public AudioClip ManaPickup;

        [Header("Enemies")]
        [Tooltip("The boss laughing, when it gets angry and when it raises skeletons.")]
        public AudioClip BossLaugh;
        [Tooltip("The first stage's boss rising from the floor.")]
        public AudioClip BossRise;

        [Header("Crowd")]
        [Tooltip("The stands murmuring, made to loop.")]
        public AudioClip CrowdAmbience;
        [Tooltip("Short cheers when an enemy goes down, one or the other.")]
        public AudioClip CrowdKill1;
        public AudioClip CrowdKill2;
        public AudioClip CrowdWave;
        public AudioClip CrowdStage;
        [Tooltip("The last stage cleared.")]
        public AudioClip CrowdVictory;
    }
}
