using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Comfort;

namespace RuneArena.Player
{
    // Goes on the XR Origin, next to PlayerHealth. Flashes a red border through the XRI tunneling vignette
    // when the player takes damage, so a hit is noticed without looking anywhere.
    [RequireComponent(typeof(PlayerHealth))]
    public class DamageFeedback : MonoBehaviour, ITunnelingVignetteProvider
    {
        [Tooltip("The TunnelingVignette from the XRI Starter Assets, under the Main Camera. Found if left empty.")]
        [SerializeField] TunnelingVignetteController m_Vignette;
        [SerializeField] Color m_Color = new Color(0.8f, 0f, 0f, 1f);
        [Tooltip("Smaller covers more of the view.")]
        [SerializeField, Range(0f, 1f)] float m_Aperture = 0.65f;
        [SerializeField, Range(0f, 1f)] float m_Feathering = 0.4f;
        [SerializeField] float m_Hold = 0.12f;
        [SerializeField] float m_FadeOut = 0.4f;

        PlayerHealth m_Health;
        VignetteParameters m_Parameters;
        float m_EndAt;

        public VignetteParameters vignetteParameters => m_Parameters;

        void Awake()
        {
            m_Health = GetComponent<PlayerHealth>();
            if (m_Vignette == null)
                m_Vignette = GetComponentInChildren<TunnelingVignetteController>();

            m_Parameters = new VignetteParameters
            {
                apertureSize = m_Aperture,
                featheringEffect = m_Feathering,
                easeInTime = 0.05f,
                easeOutTime = m_FadeOut,
                vignetteColor = m_Color,
                vignetteColorBlend = m_Color,
            };
        }

        void OnEnable()
        {
            m_Health.Damaged += OnDamaged;
        }

        void OnDisable()
        {
            m_Health.Damaged -= OnDamaged;
        }

        void Update()
        {
            if (m_EndAt > 0f && Time.time >= m_EndAt)
            {
                m_Vignette.EndTunnelingVignette(this);
                m_EndAt = 0f;
            }
        }

        void OnDamaged(float amount)
        {
            if (m_Vignette == null)
                return;
            m_Vignette.BeginTunnelingVignette(this);
            m_EndAt = Time.time + m_Hold;
        }
    }
}
