using System;
using RuneArena.Arena;
using RuneArena.Effects;
using RuneArena.Player;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Comfort;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;

namespace RuneArena.Game
{
    // In the order the options menu lists them.
    public enum Setting
    {
        ControllerLine,
        TimeOfDay,
        Turn,
        Vignette,
        MoveSpeed,
    }

    // In the order the sound menu lists them.
    public enum Volume
    {
        Music,
        Spells,
        Enemies,
        Crowd,
    }

    // The options menu: each setting is a list of choices, and each volume a slider. Saved between sessions
    // and put on the scene's systems when the game starts and each time one changes. Until one is picked, a
    // setting keeps what the scene already had.
    public class GameSettings : MonoBehaviour
    {
        const string k_Key = "RuneArena.Setting.";
        const string k_VolumeKey = "RuneArena.Sound.";
        static readonly string[] k_LineNames = { "Siempre", "Solo en menús" };
        static readonly string[] k_TurnNames = { "Suave", "Por pasos" };
        static readonly string[] k_VignetteNames = { "No", "Sí" };
        static readonly string[] k_SpeedNames = { "Lenta", "Normal", "Rápida" };
        static readonly float[] k_Speeds = { 0.75f, 1f, 1.33f };
        static readonly string[] k_VolumeNames = { "Música", "Hechizos", "Enemigos", "Público" };
        // Where each slider starts until the player moves it.
        static readonly float[] k_VolumeDefaults = { 1f, 0.75f, 1f, 0.5f };

        [Tooltip("Background music. Found by its object's name, Music, if left empty.")]
        [SerializeField] AudioSource m_Music;

        readonly int[] m_Values = new int[Enum.GetValues(typeof(Setting)).Length];
        readonly float[] m_Volumes = new float[Enum.GetValues(typeof(Volume)).Length];
        ArenaLighting m_Lighting;
        PlayerMovement m_Movement;
        TunnelingVignetteController m_Vignette;
        CurveVisualController[] m_Lines;
        ControllerInputActionManager[] m_TurnHands;
        float m_MusicVolume;
        bool m_MenuOpen;

        // While a menu is open the controller line always shows: it is what points at the buttons.
        public bool MenuOpen
        {
            set
            {
                m_MenuOpen = value;
                ShowLines();
            }
        }

        void Start()
        {
            m_Lighting = FindAnyObjectByType<ArenaLighting>();
            m_Movement = FindAnyObjectByType<PlayerMovement>();
            m_Vignette = FindAnyObjectByType<TunnelingVignetteController>(FindObjectsInactive.Include);
            m_Lines = FindObjectsByType<CurveVisualController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            // The hand that doesn't move the player is the one that turns it.
            m_TurnHands = Array.FindAll(
                FindObjectsByType<ControllerInputActionManager>(FindObjectsInactive.Include, FindObjectsSortMode.None),
                hand => !hand.smoothMotionEnabled);
            if (m_Music == null)
            {
                GameObject music = GameObject.Find("Music");
                if (music != null)
                    m_Music = music.GetComponent<AudioSource>();
            }
            if (m_Music != null)
                m_MusicVolume = m_Music.volume;

            foreach (Setting setting in Enum.GetValues(typeof(Setting)))
            {
                int value = PlayerPrefs.GetInt(k_Key + setting, SceneValue(setting));
                m_Values[(int)setting] = Mathf.Clamp(value, 0, Choices(setting) - 1);
                Apply(setting);
            }
            foreach (Volume volume in Enum.GetValues(typeof(Volume)))
                ApplyVolume(volume, PlayerPrefs.GetFloat(k_VolumeKey + volume, k_VolumeDefaults[(int)volume]));
        }

        public string Name(Volume volume)
        {
            return k_VolumeNames[(int)volume];
        }

        public float GetVolume(Volume volume)
        {
            return m_Volumes[(int)volume];
        }

        // Saved with Save, when the sound menu closes: a slider being dragged changes it every frame.
        public void SetVolume(Volume volume, float value)
        {
            PlayerPrefs.SetFloat(k_VolumeKey + volume, Mathf.Clamp01(value));
            ApplyVolume(volume, value);
        }

        void ApplyVolume(Volume volume, float value)
        {
            value = Mathf.Clamp01(value);
            m_Volumes[(int)volume] = value;
            switch (volume)
            {
                case Volume.Music:
                    if (m_Music != null)
                        m_Music.volume = m_MusicVolume * value;
                    break;
                case Volume.Spells:
                    Sfx.SetVolume(SoundGroup.Spells, value);
                    break;
                case Volume.Enemies:
                    Sfx.SetVolume(SoundGroup.Enemies, value);
                    break;
                case Volume.Crowd:
                    Sfx.SetVolume(SoundGroup.Crowd, value);
                    break;
            }
        }

        public void Save()
        {
            PlayerPrefs.Save();
        }

        public string Label(Setting setting)
        {
            int value = m_Values[(int)setting];
            switch (setting)
            {
                case Setting.ControllerLine: return $"Línea del control: {k_LineNames[value]}";
                case Setting.TimeOfDay: return $"Hora: {(m_Lighting != null ? m_Lighting.CurrentName : "-")}";
                case Setting.Turn: return $"Giro: {k_TurnNames[value]}";
                case Setting.Vignette: return $"Viñeta al moverse: {k_VignetteNames[value]}";
                case Setting.MoveSpeed: return $"Velocidad: {k_SpeedNames[value]}";
                default: return setting.ToString();
            }
        }

        // On to the setting's next choice, back to the first after the last.
        public void Next(Setting setting)
        {
            int value = (m_Values[(int)setting] + 1) % Choices(setting);
            m_Values[(int)setting] = value;
            PlayerPrefs.SetInt(k_Key + setting, value);
            PlayerPrefs.Save();
            Apply(setting);
        }

        int Choices(Setting setting)
        {
            switch (setting)
            {
                case Setting.ControllerLine: return k_LineNames.Length;
                case Setting.TimeOfDay: return m_Lighting != null ? Mathf.Max(m_Lighting.Count, 1) : 1;
                case Setting.Turn: return k_TurnNames.Length;
                case Setting.Vignette: return k_VignetteNames.Length;
                case Setting.MoveSpeed: return k_Speeds.Length;
                default: return 1;
            }
        }

        // What the scene is set to, for a setting never picked.
        int SceneValue(Setting setting)
        {
            switch (setting)
            {
                case Setting.TimeOfDay:
                    return m_Lighting != null ? m_Lighting.Current : 0;
                case Setting.Turn:
                    return m_TurnHands.Length > 0 && !m_TurnHands[0].smoothTurnEnabled ? 1 : 0;
                case Setting.Vignette:
                    return m_Vignette != null && m_Vignette.locomotionVignetteProviders.Exists(p => p.enabled) ? 1 : 0;
                case Setting.MoveSpeed:
                    return Array.IndexOf(k_Speeds, 1f);
                default:
                    return 0;
            }
        }

        void Apply(Setting setting)
        {
            int value = m_Values[(int)setting];
            switch (setting)
            {
                case Setting.ControllerLine:
                    ShowLines();
                    break;
                case Setting.TimeOfDay:
                    if (m_Lighting != null && m_Lighting.Current != value)
                        m_Lighting.Apply(value);
                    break;
                case Setting.Turn:
                    foreach (ControllerInputActionManager hand in m_TurnHands)
                        hand.smoothTurnEnabled = value == 0;
                    break;
                case Setting.Vignette:
                    if (m_Vignette != null)
                    {
                        foreach (LocomotionVignetteProvider provider in m_Vignette.locomotionVignetteProviders)
                            provider.enabled = value == 1;
                    }
                    break;
                case Setting.MoveSpeed:
                    if (m_Movement != null)
                        m_Movement.SetComfortSpeed(k_Speeds[value]);
                    break;
            }
        }

        void ShowLines()
        {
            if (m_Lines == null)
                return;
            bool show = m_MenuOpen || m_Values[(int)Setting.ControllerLine] == 0;
            foreach (CurveVisualController line in m_Lines)
            {
                if (line == null)
                    continue;
                // The line turns its renderer back on by itself every frame it is enabled.
                line.enabled = show;
                if (!show && line.lineRenderer != null)
                    line.lineRenderer.enabled = false;
            }
        }
    }
}
