using System;
using System.Collections.Generic;
using RuneArena.Player;
using RuneArena.Waves;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace RuneArena.Game
{
    // World-space menu panels, placed in front of the player each time one opens (not stuck to the head).
    // Pointed at with the controller ray and the trigger; in the editor the mouse works too.
    public class GameMenu : MonoBehaviour
    {
        [SerializeField] Canvas m_Canvas;
        [SerializeField] float m_Distance = 1.6f;
        [Tooltip("Height of the panel center relative to the eyes.")]
        [SerializeField] float m_HeightOffset = -0.1f;

        [Header("Start")]
        [SerializeField] GameObject m_StartPanel;
        [SerializeField] Button m_EasyButton;
        [SerializeField] Button m_NormalButton;
        [SerializeField] Button m_HardButton;
        [SerializeField] Button m_StartSettingsButton;
        [SerializeField] Button m_QuitButton;

        [Header("Pause")]
        [SerializeField] GameObject m_PausePanel;
        [SerializeField] TMP_Text m_PauseInfo;
        [SerializeField] Button m_ResumeButton;
        [SerializeField] Button m_PauseRestartButton;
        [SerializeField] Button m_PauseSettingsButton;
        [SerializeField] Button m_PauseMenuButton;

        [Header("Game over")]
        [SerializeField] GameObject m_GameOverPanel;
        [SerializeField] TMP_Text m_GameOverInfo;
        [SerializeField] Button m_RetryButton;
        [SerializeField] Button m_GameOverMenuButton;

        [Header("Upgrades")]
        [SerializeField] GameObject m_UpgradePanel;
        [SerializeField] Button[] m_UpgradeButtons;
        [SerializeField] Button m_SkipButton;
        [SerializeField] TMP_Text m_UpgradeInfo;

        [Header("Settings")]
        [SerializeField] GameObject m_SettingsPanel;
        [Tooltip("One per setting, in the order of the Setting enum.")]
        [SerializeField] Button[] m_SettingButtons;
        [SerializeField] Button m_SoundButton;
        [SerializeField] Button m_SettingsBackButton;

        [Header("Sound")]
        [SerializeField] GameObject m_SoundPanel;
        [Tooltip("One per volume, in the order of the Volume enum.")]
        [SerializeField] Slider[] m_VolumeSliders;
        [SerializeField] Button m_SoundBackButton;

        [Header("Frame")]
        [Tooltip("Height of the frame's two corners together, in canvas units. Panels that fit their content are " +
                 "rounded up to the corners plus whole runes, so the runes along the edges come out whole.")]
        [SerializeField] float m_FrameCorners = 100f;
        [SerializeField] float m_FrameRuneSpacing = 26f;

        // Tints over the light gray card (about 0.6 to 1), so they come out a shade darker than written here.
        static readonly Color[] k_TierColors =
        {
            new Color(0.95f, 0.62f, 0.36f),
            new Color(0.88f, 0.91f, 0.96f),
            new Color(1f, 0.84f, 0.3f),
            new Color(0.4f, 0.95f, 0.6f),
            new Color(0.6f, 0.93f, 1f),
        };

        GameManager m_Game;
        GameSettings m_Settings;
        Transform m_Head;
        // The panel the settings were opened from, to go back to.
        GameObject m_SettingsFrom;

        void Awake()
        {
            m_Game = FindAnyObjectByType<GameManager>();
            m_Settings = FindAnyObjectByType<GameSettings>();

            m_EasyButton.onClick.AddListener(() => m_Game.StartGame(Difficulty.Easy));
            m_NormalButton.onClick.AddListener(() => m_Game.StartGame(Difficulty.Normal));
            m_HardButton.onClick.AddListener(() => m_Game.StartGame(Difficulty.Hard));
            m_QuitButton.onClick.AddListener(() => m_Game.Quit());

            m_ResumeButton.onClick.AddListener(() => m_Game.Resume());
            m_PauseRestartButton.onClick.AddListener(() => m_Game.Restart());
            m_PauseMenuButton.onClick.AddListener(() => m_Game.BackToMenu());

            m_RetryButton.onClick.AddListener(() => m_Game.Restart());
            m_GameOverMenuButton.onClick.AddListener(() => m_Game.BackToMenu());

            if (m_SettingsPanel != null && m_Settings != null)
            {
                m_StartSettingsButton.onClick.AddListener(() => ShowSettings(m_StartPanel));
                m_PauseSettingsButton.onClick.AddListener(() => ShowSettings(m_PausePanel));
                m_SettingsBackButton.onClick.AddListener(() => Show(m_SettingsFrom));
                for (int i = 0; i < m_SettingButtons.Length; i++)
                {
                    var setting = (Setting)i;
                    m_SettingButtons[i].onClick.AddListener(() =>
                    {
                        m_Settings.Next(setting);
                        RefreshSettings();
                    });
                }
            }
            if (m_SoundPanel != null && m_Settings != null)
            {
                m_SoundButton.onClick.AddListener(ShowSound);
                m_SoundBackButton.onClick.AddListener(() =>
                {
                    m_Settings.Save();
                    RefreshSettings();
                    Show(m_SettingsPanel);
                });
                for (int i = 0; i < m_VolumeSliders.Length; i++)
                {
                    var volume = (Volume)i;
                    m_VolumeSliders[i].onValueChanged.AddListener(value => m_Settings.SetVolume(volume, value));
                }
            }
            else if (m_SoundButton != null)
            {
                m_SoundButton.gameObject.SetActive(false);
            }
            else
            {
                // Built before there were settings: no way into them.
                if (m_StartSettingsButton != null)
                    m_StartSettingsButton.gameObject.SetActive(false);
                if (m_PauseSettingsButton != null)
                    m_PauseSettingsButton.gameObject.SetActive(false);
            }
        }

        void Start()
        {
            FindHead();

            // On desktop the simulated controllers are awkward to aim, so let the mouse click too.
            if (Application.isEditor)
            {
                var module = FindAnyObjectByType<XRUIInputModule>();
                if (module != null)
                    module.enableMouseInput = true;
            }
        }

        // Stays where it opened, turning to face the player so it reads right from wherever they walk.
        void LateUpdate()
        {
            if (m_Head == null || !m_Canvas.gameObject.activeInHierarchy)
                return;

            Transform t = m_Canvas.transform;
            Vector3 toMenu = Vector3.ProjectOnPlane(t.position - m_Head.position, Vector3.up);
            if (toMenu.sqrMagnitude > 1e-4f)
                t.rotation = Quaternion.LookRotation(toMenu);
        }

        public void ShowStart()
        {
            Show(m_StartPanel);
        }

        public void ShowPause()
        {
            m_PauseInfo.text = $"Oleada {m_Game.Wave}\n{m_Game.Score} puntos";
            Show(m_PausePanel);
        }

        public void ShowGameOver()
        {
            int best = Mathf.Max(m_Game.Best, m_Game.Score);
            m_GameOverInfo.text = $"Llegaste a la oleada {m_Game.Wave}\n{m_Game.Score} puntos\nRécord ({Name(m_Game.Difficulty)}): {best}";
            Show(m_GameOverPanel);
        }

        // picked gets null when the player skips.
        public void ShowUpgrades(IReadOnlyList<Upgrade> offer, Upgrades upgrades, bool stageReward, Action<Upgrade> picked)
        {
            // The odds of each tier are for the tutorial, not for here.
            if (m_UpgradeInfo != null)
            {
                m_UpgradeInfo.text = stageReward ? "<b>Recompensa de etapa: mínimo Oro</b>" : "";
                m_UpgradeInfo.gameObject.SetActive(stageReward);
            }
            if (m_SkipButton != null)
            {
                m_SkipButton.GetComponentInChildren<TMP_Text>().text =
                    $"<b>Saltear</b>\n<size=70%>+{upgrades.SkipMana:0} de maná y mejores chances la próxima vez</size>";
                m_SkipButton.onClick.RemoveAllListeners();
                m_SkipButton.onClick.AddListener(() => picked(null));
            }

            for (int i = 0; i < m_UpgradeButtons.Length; i++)
            {
                Button button = m_UpgradeButtons[i];
                bool used = i < offer.Count;
                button.gameObject.SetActive(used);
                button.onClick.RemoveAllListeners();
                if (!used)
                    continue;

                Upgrade upgrade = offer[i];
                button.GetComponentInChildren<TMP_Text>().text =
                    $"<b>{upgrade.Title}</b>\n<size=70%>{upgrade.Description}</size>";
                button.GetComponent<Image>().color = k_TierColors[(int)upgrade.Tier];
                button.onClick.AddListener(() => picked(upgrade));
            }
            Show(m_UpgradePanel);
        }

        public void Hide()
        {
            m_Canvas.gameObject.SetActive(false);
            if (m_Settings != null)
                m_Settings.MenuOpen = false;
        }

        void ShowSettings(GameObject from)
        {
            m_SettingsFrom = from;
            RefreshSettings();
            Show(m_SettingsPanel);
        }

        void ShowSound()
        {
            for (int i = 0; i < m_VolumeSliders.Length; i++)
                m_VolumeSliders[i].SetValueWithoutNotify(m_Settings.GetVolume((Volume)i));
            Show(m_SoundPanel);
        }

        void RefreshSettings()
        {
            for (int i = 0; i < m_SettingButtons.Length; i++)
                m_SettingButtons[i].GetComponentInChildren<TMP_Text>().text = m_Settings.Label((Setting)i);
        }

        void Show(GameObject panel)
        {
            // Going in and out of the options keeps the menu where it is.
            bool placed = m_Canvas.gameObject.activeSelf &&
                          (InOptions(panel) || IsOpen(m_SettingsPanel) || IsOpen(m_SoundPanel));
            m_StartPanel.SetActive(panel == m_StartPanel);
            m_PausePanel.SetActive(panel == m_PausePanel);
            m_GameOverPanel.SetActive(panel == m_GameOverPanel);
            if (m_UpgradePanel != null)
                m_UpgradePanel.SetActive(panel == m_UpgradePanel);
            if (m_SettingsPanel != null)
                m_SettingsPanel.SetActive(panel == m_SettingsPanel);
            if (m_SoundPanel != null)
                m_SoundPanel.SetActive(panel == m_SoundPanel);
            m_Canvas.gameObject.SetActive(true);
            Fit(panel);
            if (m_Settings != null)
                m_Settings.MenuOpen = true;
            if (!placed)
                PlaceInFront();
        }

        // A panel that isn't stretched over the canvas takes the height of what it shows, so the frame closes
        // right under its last button.
        void Fit(GameObject panel)
        {
            var rect = (RectTransform)panel.transform;
            if (rect.anchorMin != rect.anchorMax)
                return;
            LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
            float height = LayoutUtility.GetPreferredHeight(rect);
            height = m_FrameCorners + Mathf.Ceil((height - m_FrameCorners) / m_FrameRuneSpacing) * m_FrameRuneSpacing;
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, height);
        }

        bool InOptions(GameObject panel)
        {
            return panel != null && (panel == m_SettingsPanel || panel == m_SoundPanel);
        }

        static bool IsOpen(GameObject panel)
        {
            return panel != null && panel.activeSelf;
        }

        // The GameManager may open a panel before this Start runs, so look the head up on demand.
        void FindHead()
        {
            if (m_Head != null)
                return;
            var player = FindAnyObjectByType<PlayerHealth>();
            if (player == null)
                return;
            m_Head = player.Head;
            m_Canvas.worldCamera = m_Head.GetComponent<Camera>();
        }

        void PlaceInFront()
        {
            FindHead();
            if (m_Head == null)
                return;
            Vector3 forward = Vector3.ProjectOnPlane(m_Head.forward, Vector3.up);
            if (forward.sqrMagnitude < 1e-4f)
                forward = Vector3.forward;
            forward.Normalize();

            Transform t = m_Canvas.transform;
            t.position = m_Head.position + forward * m_Distance + Vector3.up * m_HeightOffset;
            t.rotation = Quaternion.LookRotation(forward);
        }

        static string Name(Difficulty difficulty)
        {
            return difficulty == Difficulty.Easy ? "fácil" : difficulty == Difficulty.Hard ? "difícil" : "normal";
        }
    }
}
