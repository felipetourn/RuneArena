using RuneArena.Effects;
using RuneArena.Enemies;
using RuneArena.Player;
using RuneArena.Spells;
using RuneArena.Waves;
using UnityEngine;

namespace RuneArena.Pickups
{
    // Goes next to the WaveSpawner. Each enemy that dies may leave health or mana behind: a dark rune stone
    // with its rune glowing, berkano (growth) in green for health and dagaz (awakening) in blue for mana,
    // neither one a spell's rune.
    public class PickupDropper : MonoBehaviour
    {
        static readonly int k_BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int k_EmissionColor = Shader.PropertyToID("_EmissionColor");
        static readonly int k_Smoothness = Shader.PropertyToID("_Smoothness");

        static readonly Vector2[] k_Berkano =
        {
            new Vector2(-0.35f, -1f), new Vector2(-0.35f, 1f), new Vector2(0.4f, 0.5f),
            new Vector2(-0.35f, 0f), new Vector2(0.4f, -0.5f), new Vector2(-0.35f, -1f),
        };
        static readonly Vector2[] k_Dagaz =
        {
            new Vector2(-0.55f, 0.8f), new Vector2(-0.55f, -0.8f), new Vector2(0.55f, 0.8f),
            new Vector2(0.55f, -0.8f), new Vector2(-0.55f, 0.8f),
        };
        static Mesh s_HealthStone;
        static Mesh s_ManaStone;

        [SerializeField] WaveSpawner m_Spawner;
        [Tooltip("Material of the stones and their runes. Empty for the Pickup Orb in Resources. A renderer left " +
                 "without one gets Unity's built-in default in a build, which URP can't draw: pink, and doubled in the headset.")]
        [SerializeField] Material m_Material;
        [Tooltip("How bright the runes glow in their color.")]
        [SerializeField] float m_Glow = 1.5f;
        [SerializeField] Color m_StoneColor = new Color(0.2f, 0.2f, 0.22f);

        [Header("Health")]
        [SerializeField, Range(0f, 1f)] float m_HealthChance = 0.15f;
        [SerializeField] float m_HealthAmount = 15f;
        [SerializeField] Color m_HealthColor = new Color(0.3f, 1f, 0.6f);

        [Header("Mana")]
        [SerializeField, Range(0f, 1f)] float m_ManaChance = 0.3f;
        [SerializeField] float m_ManaAmount = 25f;
        [SerializeField] Color m_ManaColor = new Color(0.25f, 0.5f, 1f);

        [Header("Pickup")]
        [Tooltip("How close the player has to walk to pick it up.")]
        [SerializeField] float m_Radius = 1.2f;
        [SerializeField] float m_Lifetime = 15f;
        [SerializeField] float m_Height = 1f;
        [SerializeField] float m_Size = 0.4f;

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
            var go = new GameObject($"{kind} Pickup");
            go.AddComponent<MeshFilter>().sharedMesh = Stone(kind);
            go.transform.localScale = Vector3.one * m_Size;

            Vector3 p = enemy.transform.position;
            float floor = m_Health != null ? m_Health.transform.position.y : 0f;
            go.transform.position = new Vector3(p.x, floor + m_Height, p.z);

            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = new[] { m_Material, m_Material };
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var stone = new MaterialPropertyBlock();
            stone.SetColor(k_BaseColor, m_StoneColor);
            stone.SetColor(k_EmissionColor, Color.black);
            stone.SetFloat(k_Smoothness, 0.15f);
            renderer.SetPropertyBlock(stone, 0);
            var rune = new MaterialPropertyBlock();
            rune.SetColor(k_BaseColor, color);
            rune.SetColor(k_EmissionColor, color * m_Glow);
            renderer.SetPropertyBlock(rune, 1);

            // A soft glow around the rune, like the runes on the menu frames.
            if (Fx.Library != null)
            {
                Fx.Attach(go.transform, new FxBurst
                {
                    Material = Fx.Library.Glow,
                    Color = new Color(color.r, color.g, color.b, 0.35f),
                    Count = 8,
                    Size = m_Size * 1.4f,
                    Speed = 0.02f,
                    Lifetime = 1.2f,
                    Radius = m_Size * 0.1f,
                    Growth = FxGrowth.Swell,
                }, 5f, 0f);
            }

            go.AddComponent<Pickup>().Setup(kind, amount, m_Radius, m_Lifetime, m_Health, m_Mana);
        }

        static Mesh Stone(PickupKind kind)
        {
            if (kind == PickupKind.Health)
                return s_HealthStone != null ? s_HealthStone : s_HealthStone = SpellMeshes.RuneStone(k_Berkano, 11);
            return s_ManaStone != null ? s_ManaStone : s_ManaStone = SpellMeshes.RuneStone(k_Dagaz, 23);
        }
    }
}
