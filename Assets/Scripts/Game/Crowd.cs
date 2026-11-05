using RuneArena.Effects;
using RuneArena.Enemies;
using RuneArena.Waves;
using UnityEngine;

namespace RuneArena.Game
{
    // The crowd in the stands, all around the player: a murmur the whole time, a short cheer when an enemy
    // goes down, a bigger one when a wave is cleared, bigger for a stage and the biggest for the last stage.
    // Added by the GameManager next to the WaveSpawner.
    public class Crowd : MonoBehaviour
    {
        [SerializeField] WaveSpawner m_Spawner;
        [Tooltip("The stage whose end counts as beating the game: its boss is the last new one.")]
        [SerializeField] int m_LastStage = 3;

        [Header("Volumes")]
        [SerializeField, Range(0f, 1f)] float m_Ambience = 0.25f;
        [SerializeField, Range(0f, 1f)] float m_Kill = 0.35f;
        [SerializeField, Range(0f, 1f)] float m_Wave = 0.6f;
        [SerializeField, Range(0f, 1f)] float m_Stage = 0.8f;
        [SerializeField, Range(0f, 1f)] float m_Victory = 1f;
        [Tooltip("Seconds after a kill cheer before another, so a group going down at once is one cheer.")]
        [SerializeField] float m_KillGap = 2.5f;

        AudioSource m_Murmur;
        AudioSource m_Short;
        AudioSource m_Big;
        float m_KillReadyAt;
        bool m_Won;

        static SfxLibrary Sounds => Sfx.Library;

        void Awake()
        {
            if (m_Spawner == null)
                m_Spawner = GetComponent<WaveSpawner>();
            m_Murmur = Source(true);
            m_Short = Source(false);
            m_Big = Source(false);
        }

        void Start()
        {
            if (Sounds == null || Sounds.CrowdAmbience == null)
                return;
            m_Murmur.clip = Sounds.CrowdAmbience;
            m_Murmur.time = Random.Range(0f, m_Murmur.clip.length);
            m_Murmur.Play();
        }

        void OnEnable()
        {
            if (m_Spawner == null)
                return;
            m_Spawner.EnemyDied += OnEnemyDied;
            m_Spawner.WaveCleared += OnWaveCleared;
        }

        void OnDisable()
        {
            if (m_Spawner == null)
                return;
            m_Spawner.EnemyDied -= OnEnemyDied;
            m_Spawner.WaveCleared -= OnWaveCleared;
        }

        // Follows the slider in the options as it moves.
        void Update()
        {
            m_Murmur.volume = m_Ambience * Sfx.Volume(SoundGroup.Crowd);
        }

        // A boss's death is cheered with its wave.
        void OnEnemyDied(Enemy enemy)
        {
            if (enemy.IsBoss || Time.time < m_KillReadyAt || Sounds == null || m_Big.isPlaying)
                return;
            AudioClip clip = Random.value < 0.5f ? Sounds.CrowdKill1 : Sounds.CrowdKill2;
            if (clip == null)
                return;
            m_KillReadyAt = Time.time + m_KillGap;
            m_Short.pitch = Random.Range(0.95f, 1.05f);
            m_Short.PlayOneShot(clip, m_Kill * Sfx.Volume(SoundGroup.Crowd));
        }

        void OnWaveCleared(int wave)
        {
            if (Sounds == null)
                return;
            int perStage = Mathf.Max(m_Spawner.WavesPerStage, 1);
            int stage = (wave - 1) / perStage + 1;
            bool stageEnd = wave % perStage == 0;

            if (stageEnd && stage == m_LastStage && !m_Won)
            {
                m_Won = true;
                Cheer(Sounds.CrowdVictory, m_Victory);
            }
            else if (stageEnd)
            {
                Cheer(Sounds.CrowdStage, m_Stage);
            }
            else
            {
                Cheer(Sounds.CrowdWave, m_Wave);
            }
        }

        // A big cheer takes over from whatever cheer was going.
        void Cheer(AudioClip clip, float volume)
        {
            if (clip == null)
                return;
            m_Short.Stop();
            m_Big.clip = clip;
            m_Big.volume = volume * Sfx.Volume(SoundGroup.Crowd);
            m_Big.Play();
        }

        // Not placed anywhere: the stands are all around.
        AudioSource Source(bool loop)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            return source;
        }
    }
}
