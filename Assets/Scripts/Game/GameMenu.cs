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
        [SerializeField] Button m_QuitButton;

        [Header("Pause")]
        [SerializeField] GameObject m_PausePanel;
        [SerializeField] TMP_Text m_PauseInfo;
        [SerializeField] Button m_ResumeButton;
        [SerializeField] Button m_PauseRestartButton;
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

        static readonly Color[] k_TierColors =
        {
            new Color(0.5f, 0.33f, 0.2f),
            new Color(0.5f, 0.53f, 0.58f),
            new Color(0.72f, 0.57f, 0.12f),
            new Color(0.25f, 0.68f, 0.8f),
        };

        GameManager m_Game;
        Transform m_Head;

        void Awake()
        {
            m_Game = FindAnyObjectByType<GameManager>();

            m_EasyButton.onClick.AddListener(() => m_Game.StartGame(Difficulty.Easy));
            m_NormalButton.onClick.AddListener(() => m_Game.StartGame(Difficulty.Normal));
            m_HardButton.onClick.AddListener(() => m_Game.StartGame(Difficulty.Hard));
            m_QuitButton.onClick.AddListener(() => m_Game.Quit());

            m_ResumeButton.onClick.AddListener(() => m_Game.Resume());
            m_PauseRestartButton.onClick.AddListener(() => m_Game.Restart());
            m_PauseMenuButton.onClick.AddListener(() => m_Game.BackToMenu());

            m_RetryButton.onClick.AddListener(() => m_Game.Restart());
            m_GameOverMenuButton.onClick.AddListener(() => m_Game.BackToMenu());
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

        // Keeps facing the player while open, so it reads right from wherever they walk; if they walk
        // well away from it, it comes back in front of them.
        void LateUpdate()
        {
            if (m_Head == null || !m_Canvas.gameObject.activeInHierarchy)
                return;

            Transform t = m_Canvas.transform;
            Vector3 toMenu = Vector3.ProjectOnPlane(t.position - m_Head.position, Vector3.up);
            if (toMenu.magnitude > m_Distance * 2f)
            {
                PlaceInFront();
                return;
            }
            if (toMenu.sqrMagnitude > 1e-4f)
                t.rotation = Quaternion.LookRotation(toMenu);
        }

        public void ShowStart()
        {
            Show(m_StartPanel);
        }

        public void ShowPause()
        {
            m_PauseInfo.text = $"Oleada {m_Game.Wave}   ·   {m_Game.Score} puntos";
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
            if (m_UpgradeInfo != null)
            {
                string reward = stageReward ? "<b>Recompensa de etapa: mínimo Oro</b>\n" : "";
                m_UpgradeInfo.text = reward +
                    $"Bronce {Odds(upgrades, UpgradeTier.Bronze)}   Plata {Odds(upgrades, UpgradeTier.Silver)}   " +
                    $"Oro {Odds(upgrades, UpgradeTier.Gold)}   Diamante {Odds(upgrades, UpgradeTier.Diamond)}\n" +
                    $"<size=80%>Saltear: +{upgrades.SkipMana:0} de maná y mejores chances la próxima vez</size>";
            }
            if (m_SkipButton != null)
            {
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
                    $"<b>{upgrade.Title}</b>  <size=70%>{Upgrades.TierName(upgrade.Tier)}</size>\n<size=70%>{upgrade.Description}</size>";
                button.GetComponent<Image>().color = k_TierColors[(int)upgrade.Tier];
                button.onClick.AddListener(() => picked(upgrade));
            }
            Show(m_UpgradePanel);
        }

        public void Hide()
        {
            m_Canvas.gameObject.SetActive(false);
        }

        void Show(GameObject panel)
        {
            m_StartPanel.SetActive(panel == m_StartPanel);
            m_PausePanel.SetActive(panel == m_PausePanel);
            m_GameOverPanel.SetActive(panel == m_GameOverPanel);
            if (m_UpgradePanel != null)
                m_UpgradePanel.SetActive(panel == m_UpgradePanel);
            m_Canvas.gameObject.SetActive(true);
            PlaceInFront();
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

        static string Odds(Upgrades upgrades, UpgradeTier tier)
        {
            return $"{upgrades.Odds(tier):0.#}%";
        }

        static string Name(Difficulty difficulty)
        {
            return difficulty == Difficulty.Easy ? "fácil" : difficulty == Difficulty.Hard ? "difícil" : "normal";
        }
    }
}
