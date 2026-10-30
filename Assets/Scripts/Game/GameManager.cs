using System;
using System.Collections.Generic;
using RuneArena.Enemies;
using RuneArena.Player;
using RuneArena.Strokes;
using RuneArena.Waves;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace RuneArena.Game
{
    public enum GameState
    {
        Menu,
        Playing,
        Paused,
        // Between waves, picking an upgrade.
        Choosing,
        GameOver,
    }

    // Start menu -> playing (with pause) -> game over. Restarting reloads the scene, which resets everything.
    // Each cleared wave scores points for the wave, for being quick and for the health and mana left.
    public class GameManager : MonoBehaviour
    {
        const string k_BestKey = "RuneArena.Best.";

        [SerializeField] WaveSpawner m_Spawner;
        [SerializeField] GameMenu m_Menu;
        [Tooltip("Menu button on the left controller, Escape or P on the keyboard.")]
        [SerializeField] InputActionProperty m_PauseAction = CreatePauseAction();

        [Header("Score")]
        [Tooltip("Points per cleared wave, times the wave number.")]
        [SerializeField] int m_WavePoints = 100;
        [Tooltip("Seconds a wave is expected to take. Each second under it scores Time Points.")]
        [SerializeField] float m_ParTime = 60f;
        [SerializeField] int m_TimePoints = 5;
        [Tooltip("Points for finishing a wave at full health; less with less health.")]
        [SerializeField] int m_HealthPoints = 100;
        [SerializeField] int m_ManaPoints = 50;

        [Header("Upgrades")]
        [Tooltip("Waves within a stage (1 is its first) after which an upgrade is offered. The boss wave always offers one.")]
        [SerializeField] int[] m_EasyUpgradeWaves = { 1, 2, 3, 4 };
        [SerializeField] int[] m_NormalUpgradeWaves = { 2, 4 };
        [SerializeField] int[] m_HardUpgradeWaves = { 3 };

        public event Action<GameState> StateChanged;

        public GameState State { get; private set; }
        public int Score { get; private set; }
        public Difficulty Difficulty => GameSession.Difficulty;
        public int Wave => m_Spawner != null ? m_Spawner.CurrentWave : 0;
        public int Best => PlayerPrefs.GetInt(k_BestKey + GameSession.Difficulty, 0);

        PlayerHealth m_Health;
        PlayerMana m_Mana;
        StrokeRecorder m_Recorder;
        RuneCaster m_Caster;
        Upgrades m_Upgrades;
        float m_WaveStartedAt;

        void Awake()
        {
            if (m_Spawner == null)
                m_Spawner = GetComponent<WaveSpawner>();
            if (m_Menu == null)
                m_Menu = FindAnyObjectByType<GameMenu>();
            m_Upgrades = GetComponent<Upgrades>();
            if (m_Spawner != null)
                m_Spawner.StartOnPlay = false;
        }

        void Start()
        {
            m_Health = FindAnyObjectByType<PlayerHealth>();
            m_Mana = FindAnyObjectByType<PlayerMana>();
            m_Recorder = FindAnyObjectByType<StrokeRecorder>(FindObjectsInactive.Include);
            m_Caster = FindAnyObjectByType<RuneCaster>(FindObjectsInactive.Include);
            if (m_Health != null && m_Health.GetComponent<ThreatIndicators>() == null)
                m_Health.gameObject.AddComponent<ThreatIndicators>();

            if (m_Spawner != null)
            {
                m_Spawner.WaveStarted += OnWaveStarted;
                m_Spawner.WaveCleared += OnWaveCleared;
            }
            if (m_Health != null)
                m_Health.Died += OnPlayerDied;

            if (GameSession.StartImmediately)
            {
                GameSession.StartImmediately = false;
                StartGame(GameSession.Difficulty);
            }
            else
            {
                Enter(GameState.Menu);
            }
        }

        void OnEnable()
        {
            if (m_PauseAction.reference == null)
                m_PauseAction.action.Enable();
        }

        void OnDisable()
        {
            if (m_PauseAction.reference == null)
                m_PauseAction.action.Disable();
            Time.timeScale = 1f;
        }

        void Update()
        {
            if (!m_PauseAction.action.WasPressedThisFrame())
                return;
            if (State == GameState.Playing)
                Pause();
            else if (State == GameState.Paused)
                Resume();
        }

        public void StartGame(Difficulty difficulty)
        {
            GameSession.Difficulty = difficulty;
            Score = 0;
            if (m_Spawner != null)
                m_Spawner.Difficulty = difficulty;
            if (m_Mana != null)
                m_Mana.SetDifficulty(difficulty);
            // Easy shows the enemies the player isn't looking at from the start; normal has to earn it.
            var threats = FindAnyObjectByType<ThreatIndicators>();
            if (threats != null)
                threats.Active = difficulty == Difficulty.Easy;

            Enter(GameState.Playing);
            if (m_Spawner != null)
                m_Spawner.StartWaves();
        }

        public void Pause()
        {
            if (State == GameState.Playing)
                Enter(GameState.Paused);
        }

        public void Resume()
        {
            if (State == GameState.Paused)
                Enter(GameState.Playing);
        }

        public void Restart()
        {
            GameSession.StartImmediately = true;
            Reload();
        }

        public void BackToMenu()
        {
            GameSession.StartImmediately = false;
            Reload();
        }

        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void Enter(GameState state)
        {
            State = state;
            Time.timeScale = state == GameState.Paused ? 0f : 1f;
            SetCasting(state == GameState.Playing);

            if (m_Menu != null)
            {
                switch (state)
                {
                    case GameState.Menu: m_Menu.ShowStart(); break;
                    case GameState.Paused: m_Menu.ShowPause(); break;
                    case GameState.GameOver: m_Menu.ShowGameOver(); break;
                    case GameState.Choosing: break;
                    default: m_Menu.Hide(); break;
                }
            }
            StateChanged?.Invoke(state);
        }

        // Runes can only be drawn while playing, so the trigger and the mouse are free for the menus.
        void SetCasting(bool enabled)
        {
            if (m_Recorder != null)
                m_Recorder.enabled = enabled;
            if (m_Caster != null)
                m_Caster.enabled = enabled;
        }

        void OnWaveStarted(int wave)
        {
            m_WaveStartedAt = Time.time;
        }

        void OnWaveCleared(int wave)
        {
            float time = Time.time - m_WaveStartedAt;
            int points = wave * m_WavePoints + Mathf.RoundToInt(Mathf.Max(0f, m_ParTime - time) * m_TimePoints);
            if (m_Health != null)
                points += Mathf.RoundToInt(m_HealthPoints * m_Health.Health / m_Health.MaxHealth);
            if (m_Mana != null && m_Mana.MaxMana > 0f)
                points += Mathf.RoundToInt(m_ManaPoints * m_Mana.Mana / m_Mana.MaxMana);

            Score += points;
            m_Spawner.Announce($"Oleada {wave} superada  +{points}", 2.5f);

            if (m_Upgrades == null || m_Menu == null || State != GameState.Playing)
                return;
            // The wave that ends a stage is the boss's: its reward is better.
            int perStage = Mathf.Max(m_Spawner.WavesPerStage, 1);
            int inStage = (wave - 1) % perStage + 1;
            bool stageEnd = inStage == perStage;
            if (!stageEnd && Array.IndexOf(UpgradeWaves(), inStage) < 0)
                return;

            IReadOnlyList<Upgrade> offer = m_Upgrades.Roll(stageEnd ? UpgradeTier.Gold : UpgradeTier.Bronze);
            if (offer.Count == 0)
                return;

            m_Spawner.HoldBeforeNextWave = true;
            Enter(GameState.Choosing);
            m_Menu.ShowUpgrades(offer, m_Upgrades, stageEnd, ChooseUpgrade);
        }

        int[] UpgradeWaves()
        {
            switch (GameSession.Difficulty)
            {
                case Difficulty.Easy: return m_EasyUpgradeWaves;
                case Difficulty.Hard: return m_HardUpgradeWaves;
                default: return m_NormalUpgradeWaves;
            }
        }

        void ChooseUpgrade(Upgrade upgrade)
        {
            if (State != GameState.Choosing)
                return;
            if (upgrade != null)
            {
                m_Upgrades.Choose(upgrade);
            }
            else
            {
                m_Upgrades.Skip();
                m_Spawner.Announce("Próxima oferta: mejores chances", 2.5f);
            }
            m_Spawner.HoldBeforeNextWave = false;
            Enter(GameState.Playing);
        }

        void OnPlayerDied()
        {
            ClearArena();

            if (Score > Best)
            {
                PlayerPrefs.SetInt(k_BestKey + GameSession.Difficulty, Score);
                PlayerPrefs.Save();
            }
            Enter(GameState.GameOver);
        }

        // Out of the way of the game over menu: enemies, their shots and anything they left behind.
        void ClearArena()
        {
            if (m_Spawner != null)
                m_Spawner.StopWaves();
            foreach (Enemy enemy in new List<Enemy>(Enemy.All))
                enemy.Despawn();
            foreach (EnemyProjectile shot in FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None))
                Destroy(shot.gameObject);
            foreach (Pickups.Pickup pickup in FindObjectsByType<Pickups.Pickup>(FindObjectsSortMode.None))
                Destroy(pickup.gameObject);
        }

        void Reload()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        static InputActionProperty CreatePauseAction()
        {
            var action = new InputAction("Pause", InputActionType.Button);
            action.AddBinding("<XRController>{LeftHand}/{MenuButton}");
            action.AddBinding("<Keyboard>/escape");
            action.AddBinding("<Keyboard>/p");
            return new InputActionProperty(action);
        }
    }
}
