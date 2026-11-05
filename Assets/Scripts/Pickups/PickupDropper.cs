using RuneArena.Enemies;
using RuneArena.Player;
using RuneArena.Waves;
using UnityEngine;

namespace RuneArena.Pickups
{
    // Goes next to the WaveSpawner. Each enemy that dies may leave health or mana behind.
    public class PickupDropper : MonoBehaviour
    {
        static readonly int k_BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int k_EmissionColor = Shader.PropertyToID("_EmissionColor");

        [SerializeField] WaveSpawner m_Spawner;
        [Tooltip("Material of the orbs. Empty for the Pickup Orb in Resources. A primitive left without one gets " +
                 "Unity's built-in default in a build, which URP can't draw: pink, and doubled in the headset.")]
        [SerializeField] Material m_Material;
        [Tooltip("How bright the orbs glow in their color.")]
        [SerializeField] float m_Glow = 1.5f;

        [Header("Health")]
        [SerializeField, Range(0f, 1f)] float m_HealthChance = 0.15f;
        [SerializeField] float m_HealthAmount = 15f;
        [SerializeField] Color m_HealthColor = new Color(0.2f, 0.9f, 0.3f);

        [Header("Mana")]
        [SerializeField, Range(0f, 1f)] float m_ManaChance = 0.3f;
        [SerializeField] float m_ManaAmount = 25f;
        [SerializeField] Color m_ManaColor = new Color(0.25f, 0.5f, 1f);

        [Header("Pickup")]
        [Tooltip("How close the player has to walk to pick it up.")]
        [SerializeField] float m_Radius = 1.2f;
        [SerializeField] float m_Lifetime = 15f;
        [SerializeField] float m_Height = 1f;
        [SerializeField] float m_Size = 0.3f;

        PlayerHealth m_Health;
        PlayerMana m_Mana;

        void Awake()
        {
            if (m_Spawner == null)
                m_Spawner = GetComponent<WaveSpawner>();
        }

        void Start()
        {
            m_Health = FindAnyObjectByType<PlayerHealth>();
            m_Mana = FindAnyObjectByType<PlayerMana>();
            if (m_Material == null)
                m_Material = Resources.Load<Material>("Pickup Orb");
        }

        void OnEnable()
        {
            if (m_Spawner != null)
                m_Spawner.EnemyDied += Drop;
        }

        void OnDisable()
        {
            if (m_Spawner != null)
                m_Spawner.EnemyDied -= Drop;
        }

        // One roll: health is checked first so its chance stays what the Inspector says.
        void Drop(Enemy enemy)
        {
            float roll = Random.value;
            if (roll < m_HealthChance)
                Spawn(enemy, PickupKind.Health, m_HealthAmount, m_HealthColor);
            else if (roll < m_HealthChance + m_ManaChance)
                Spawn(enemy, PickupKind.Mana, m_ManaAmount, m_ManaColor);
        }

        void Spawn(Enemy enemy, PickupKind kind, float amount, Color color)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = $"{kind} Pickup";
            Destroy(go.GetComponent<Collider>());
            go.transform.localScale = Vector3.one * m_Size;

            Vector3 p = enemy.transform.position;
            float floor = m_Health != null ? m_Health.transform.position.y : 0f;
            go.transform.position = new Vector3(p.x, floor + m_Height, p.z);

            var renderer = go.GetComponent<Renderer>();
            if (m_Material != null)
                renderer.sharedMaterial = m_Material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var block = new MaterialPropertyBlock();
            block.SetColor(k_BaseColor, color);
            block.SetColor(k_EmissionColor, color * m_Glow);
            renderer.SetPropertyBlock(block);

            go.AddComponent<Pickup>().Setup(kind, amount, m_Radius, m_Lifetime, m_Health, m_Mana);
        }
    }
}
