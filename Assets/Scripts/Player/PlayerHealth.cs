using System;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;

namespace RuneArena.Player
{
    // Goes on the XR Origin. Enemies aim at the head and the feet below it.
    public class PlayerHealth : MonoBehaviour
    {
        [SerializeField] float m_MaxHealth = 100f;
        [Tooltip("Seconds without taking damage after a hit.")]
        [SerializeField] float m_InvulnerableTime = 0.4f;
        [Tooltip("Optional text showing the health. The WristDisplay already shows it.")]
        [SerializeField] TMP_Text m_Label;

        public event Action<float> Damaged;
        public event Action Died;

        public float Health { get; private set; }
        public float MaxHealth => m_MaxHealth;
        public bool IsDead => Health <= 0f;
        public Transform Head { get; private set; }
        public Vector3 Feet => new Vector3(Head.position.x, transform.position.y, Head.position.z);

        float m_VulnerableAt;
        int m_ExtraLives;

        public int ExtraLives => m_ExtraLives;

        public void AddExtraLife()
        {
            m_ExtraLives++;
        }

        void Awake()
        {
            Head = GetComponent<XROrigin>().Camera.transform;
            Health = m_MaxHealth;
            UpdateLabel();
        }

        public void Damage(float amount)
        {
            if (IsDead || Time.time < m_VulnerableAt)
                return;

            Health = Mathf.Max(Health - amount, 0f);
            m_VulnerableAt = Time.time + m_InvulnerableTime;

            // Second chance: back up at half health, with a longer grace period.
            if (Health <= 0f && m_ExtraLives > 0)
            {
                m_ExtraLives--;
                Health = m_MaxHealth * 0.5f;
                m_VulnerableAt = Time.time + 2f;
            }
            UpdateLabel();
            Damaged?.Invoke(amount);
            if (IsDead)
                Died?.Invoke();
        }

        public void AddMaxHealth(float amount)
        {
            m_MaxHealth += amount;
            Health += amount;
            UpdateLabel();
        }

        public void Heal(float amount)
        {
            Health = Mathf.Min(Health + amount, m_MaxHealth);
            UpdateLabel();
        }

        void UpdateLabel()
        {
            if (m_Label == null)
                return;
            m_Label.text = IsDead ? "DEAD" : $"HP {Mathf.CeilToInt(Health)}";
            m_Label.color = Health > m_MaxHealth * 0.3f ? Color.white : new Color(1f, 0.3f, 0.3f);
        }
    }
}
