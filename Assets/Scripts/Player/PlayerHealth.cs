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
        [Tooltip("Small label fixed to the top left of the view. Created if left empty.")]
        [SerializeField] TMP_Text m_Label;

        public event Action<float> Damaged;
        public event Action Died;

        public float Health { get; private set; }
        public float MaxHealth => m_MaxHealth;
        public bool IsDead => Health <= 0f;
        public Transform Head { get; private set; }
        public Vector3 Feet => new Vector3(Head.position.x, transform.position.y, Head.position.z);

        float m_VulnerableAt;

        void Awake()
        {
            Head = GetComponent<XROrigin>().Camera.transform;
            Health = m_MaxHealth;
            if (m_Label == null)
                CreateLabel();
            UpdateLabel();
        }

        public void Damage(float amount)
        {
            if (IsDead || Time.time < m_VulnerableAt)
                return;

            Health = Mathf.Max(Health - amount, 0f);
            m_VulnerableAt = Time.time + m_InvulnerableTime;
            UpdateLabel();
            Damaged?.Invoke(amount);
            if (IsDead)
                Died?.Invoke();
        }

        public void Heal(float amount)
        {
            Health = Mathf.Min(Health + amount, m_MaxHealth);
            UpdateLabel();
        }

        void CreateLabel()
        {
            var go = new GameObject("HealthLabel");
            go.transform.SetParent(Head, false);
            go.transform.localPosition = new Vector3(-0.12f, 0.08f, 0.4f);

            var text = go.AddComponent<TextMeshPro>();
            text.fontSize = 0.12f;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.rectTransform.sizeDelta = new Vector2(0.1f, 0.02f);
            m_Label = text;
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
