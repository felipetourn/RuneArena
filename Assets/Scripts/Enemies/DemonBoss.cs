using RuneArena.Effects;
using RuneArena.Spells;
using RuneArena.Strokes;
using RuneArena.Waves;
using UnityEngine;
using UnityEngine.AI;

namespace RuneArena.Enemies
{
    // The skeletal reaper that closes the third stage. It doesn't walk: it vanishes in smoke and comes back
    // next to the player, or leaps onto them. Its plain blow is the combo; each time its fury fills it lets
    // out the next move of a fixed cycle:
    //   leap -> whirl -> cleave
    // The combo and the whirl start with a blink: a ring marks where it is about to appear. At half health
    // it falls apart into a heap of bones that can't be hurt while skeletons climb out of the floor around
    // it, then puts itself back together and fights faster.
    public class DemonBoss : GroundEnemy
    {
        enum Move
        {
            Combo,
            Leap,
            Whirl,
            Cleave,
        }

        enum State
        {
            Ready,
            Vanished,
            Telegraph,
            Collapsed,
        }

        static readonly Move[] k_Pattern = { Move.Leap, Move.Whirl, Move.Cleave };
        static Material s_Material;

        public override bool IsBoss => true;

        [Header("Pattern")]
        [Tooltip("Seconds standing between two moves.")]
        [SerializeField] float m_Rest = 1.2f;
        [Tooltip("Wind-ups and rests are multiplied by this once it has put itself back together.")]
        [SerializeField] float m_AngerTime = 0.7f;

        [Header("Blink")]
        [Tooltip("Seconds gone in the smoke, while a ring shows where it comes back.")]
        [SerializeField] float m_VanishTime = 0.8f;
        [Tooltip("How far from the player it comes back.")]
        [SerializeField] float m_BlinkDistance = 2.2f;
        [SerializeField] Color m_SmokeColor = new Color(0.45f, 0.2f, 0.8f);

        [Header("Combo")]
        [SerializeField] float m_ComboWindUp = 0.8f;
        [SerializeField] float m_ComboRange = 2.8f;
        [Tooltip("Degrees in front of it the blades cover.")]
        [SerializeField] float m_ComboAngle = 110f;
        [SerializeField] float m_ComboDamage = 18f;

        [Header("Whirl")]
        [SerializeField] float m_WhirlWindUp = 0.9f;
        [SerializeField] float m_WhirlRadius = 3f;
        [SerializeField] float m_WhirlDamage = 15f;

        [Header("Leap")]
        [SerializeField] float m_LeapWindUp = 2f;
        [Tooltip("Last seconds of the wind-up, in which it flies over to where the player was.")]
        [SerializeField] float m_LeapAirborne = 0.6f;
        [SerializeField] float m_LeapRadius = 2.5f;
        [SerializeField] float m_LeapDamage = 25f;

        [Header("Cleave")]
        [SerializeField] float m_CleaveWindUp = 1.3f;
        [SerializeField] float m_CleaveLength = 7f;
        [SerializeField] float m_CleaveWidth = 1.6f;
        [SerializeField] float m_CleaveDamage = 30f;
        [SerializeField] Color m_DangerColor = new Color(0.8f, 0.1f, 0.2f);

        [Header("Collapse")]
        [SerializeField, Range(0f, 1f)] float m_CollapseHealth = 0.5f;
        [Tooltip("Length of its falling apart and of its putting itself back together, the clips' own.")]
        [SerializeField] float m_CollapseTime = 2.8f;
        [SerializeField] float m_ReformTime = 2.7f;
        [Tooltip("Seconds as a heap of bones between the two.")]
        [SerializeField] float m_HeapTime = 5f;
        [SerializeField] Enemy m_Minion;
        [SerializeField] int m_MinionCount = 3;
        [SerializeField] float m_EmergeTime = 1.5f;

        State m_State;
        Move m_Move;
        int m_Step;
        float m_Until;
        bool m_Collapsed;
        bool m_Summoned;
        bool m_Reforming;
        Vector3 m_Spot;
        Vector3 m_LeapFrom;
        GameObject m_Marker;
        WaveSpawner m_Spawner;

        float TimeScale => m_Collapsed ? m_AngerTime : 1f;

        protected override void Start()
        {
            base.Start();
            m_Spawner = FindAnyObjectByType<WaveSpawner>();
            m_Until = Time.time + m_Rest;
        }

        protected override void Think(float deltaTime)
        {
            Vector3 feet = Player.Feet;
            if (!m_Collapsed && Health <= MaxHealth * m_CollapseHealth)
            {
                Collapse();
                return;
            }

            switch (m_State)
            {
                case State.Ready:
                    Stop();
                    FaceTowards(feet, 240f, deltaTime);
                    BuildFury(deltaTime / TimeScale);
                    if (Time.time < m_Until)
                        break;
                    if (FuryFull)
                    {
                        SpendFury();
                        Begin(k_Pattern[m_Step++ % k_Pattern.Length], feet);
                    }
                    else
                    {
                        Begin(Move.Combo, feet);
                    }
                    break;

                case State.Vanished:
                    Stop();
                    if (Time.time >= m_Until)
                        Reappear(feet);
                    break;

                case State.Telegraph:
                    Stop();
                    if (m_Move == Move.Leap)
                        Fly();
                    else if (m_Move != Move.Cleave)
                        FaceTowards(feet, 360f, deltaTime);
                    if (Time.time >= m_Until)
                        Land(feet);
                    break;

                case State.Collapsed:
                    UpdateCollapse();
                    break;
            }
        }

        void Begin(Move move, Vector3 feet)
        {
            m_Move = move;
            switch (move)
            {
                case Move.Combo:
                case Move.Whirl:
                    Vanish(feet);
                    break;

                case Move.Leap:
                    m_LeapFrom = transform.position;
                    m_Spot = feet;
                    DangerZone.Show(feet, m_LeapRadius, m_DangerColor, m_LeapWindUp * TimeScale);
                    WindUp(m_LeapWindUp);
                    break;

                case Move.Cleave:
                    Vector3 toward = feet - transform.position;
                    toward.y = 0f;
                    if (toward.sqrMagnitude > 0.01f)
                        transform.rotation = Quaternion.LookRotation(toward);
                    ShowStrip();
                    WindUp(m_CleaveWindUp);
                    break;
            }
        }

        void WindUp(float seconds)
        {
            m_State = State.Telegraph;
            m_Until = Time.time + seconds * TimeScale;
            BeginAttack(seconds * TimeScale, m_Move.ToString());
        }

        // Gone in smoke; a ring shows where it will come back, beside the player.
        void Vanish(Vector3 feet)
        {
            Smoke(transform.position);
            BodyRenderer.enabled = false;
            Invulnerable = true;
            Vector2 side = Random.insideUnitCircle.normalized * m_BlinkDistance;
            m_Spot = KeepInside(feet + new Vector3(side.x, 0f, side.y));
            if (NavMesh.SamplePosition(m_Spot, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                m_Spot = hit.position;
            SpellFx.GroundRing(m_Spot, 1.6f, m_SmokeColor, m_VanishTime * TimeScale);
            m_State = State.Vanished;
            m_Until = Time.time + m_VanishTime * TimeScale;
        }

        void Reappear(Vector3 feet)
        {
            Agent.Warp(m_Spot);
            Vector3 toward = feet - m_Spot;
            toward.y = 0f;
            if (toward.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.LookRotation(toward);
            BodyRenderer.enabled = true;
            Invulnerable = false;
            Smoke(m_Spot);
            if (m_Move == Move.Whirl)
                DangerZone.Show(m_Spot, m_WhirlRadius, m_DangerColor, m_WhirlWindUp * TimeScale);
            WindUp(m_Move == Move.Combo ? m_ComboWindUp : m_WhirlWindUp);
        }

        // Over the last part of the leap's wind-up it crosses to where the player stood.
        void Fly()
        {
            float airborne = m_LeapAirborne * TimeScale;
            float t = 1f - Mathf.Clamp01((m_Until - Time.time) / airborne);
            Vector3 target = Vector3.Lerp(m_LeapFrom, m_Spot, t * t);
            if (Agent.isOnNavMesh)
                Agent.Move(KeepInside(target) - transform.position);
        }

        void Land(Vector3 feet)
        {
            HideMarker();
            Vector3 to = feet - transform.position;
            to.y = 0f;
            switch (m_Move)
            {
                case Move.Combo:
                    if (to.magnitude <= m_ComboRange && Vector3.Angle(transform.forward, to) <= m_ComboAngle * 0.5f)
                    {
                        Player.Damage(m_ComboDamage * DamageMultiplier);
                        LandedBlow();
                    }
                    break;

                case Move.Whirl:
                    if (to.magnitude <= m_WhirlRadius)
                        Player.Damage(m_WhirlDamage * DamageMultiplier);
                    SpellFx.Dust(transform.position, m_WhirlRadius);
                    break;

                case Move.Leap:
                    if (FlatDistance(feet, m_Spot) <= m_LeapRadius)
                        Player.Damage(m_LeapDamage * DamageMultiplier);
                    SpellFx.Dust(transform.position, m_LeapRadius);
                    break;

                case Move.Cleave:
                    Vector3 local = transform.InverseTransformPoint(feet);
                    if (local.z >= 0f && local.z <= m_CleaveLength && Mathf.Abs(local.x) <= m_CleaveWidth * 0.5f)
                        Player.Damage(m_CleaveDamage * DamageMultiplier);
                    SpellFx.Dust(transform.position + transform.forward * (m_CleaveLength * 0.5f), m_CleaveWidth);
                    break;
            }
            End();
        }

        void End()
        {
            HideMarker();
            BodyRenderer.enabled = true;
            Invulnerable = false;
            Telegraphing = false;
            m_State = State.Ready;
            m_Until = Time.time + m_Rest * TimeScale;
        }

        // Falls apart, lies there as bones while skeletons climb out of the floor, then gets back up.
        void Collapse()
        {
            HideMarker();
            BodyRenderer.enabled = true;
            Telegraphing = false;
            m_Collapsed = true;
            m_Summoned = false;
            m_Reforming = false;
            Invulnerable = true;
            m_State = State.Collapsed;
            m_Until = Time.time + m_CollapseTime + m_HeapTime;
            PlayAction("Collapse", m_CollapseTime + m_HeapTime);
            SpellFx.Dust(transform.position, 2f);
        }

        void UpdateCollapse()
        {
            Stop();
            if (!m_Summoned && Time.time >= m_Until - m_HeapTime)
            {
                m_Summoned = true;
                Summon();
            }
            if (!m_Reforming && Time.time >= m_Until)
            {
                m_Reforming = true;
                PlayAction("Reform", m_ReformTime);
                m_Until = Time.time + m_ReformTime;
                Smoke(transform.position);
                return;
            }
            if (m_Reforming && Time.time >= m_Until)
            {
                Invulnerable = false;
                End();
            }
        }

        void Summon()
        {
            if (m_Minion == null)
                return;
            for (int i = 0; i < m_MinionCount; i++)
            {
                float angle = (i + Random.value * 0.5f) * Mathf.PI * 2f / m_MinionCount;
                Vector3 spot = KeepInside(transform.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 3.5f);
                if (!NavMesh.SamplePosition(spot, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                    continue;
                Enemy minion = m_Spawner != null
                    ? m_Spawner.SpawnExtra(m_Minion, hit.position, Quaternion.LookRotation(hit.position - transform.position))
                    : Instantiate(m_Minion, hit.position, transform.rotation);
                minion.Emerge(m_EmergeTime);
                SpellFx.Summoning(hit.position, m_SmokeColor, m_EmergeTime);
            }
        }

        void Smoke(Vector3 ground)
        {
            SpellFx.Summoning(ground, m_SmokeColor, 0.6f, 1.2f);
        }

        void ShowStrip()
        {
            m_Marker = RuneSpell.CreateVisual(PrimitiveType.Cube, "Cleave Strip", LineMaterial(), m_DangerColor);
            m_Marker.transform.SetParent(transform, false);
            m_Marker.transform.localPosition = new Vector3(0f, 0.02f, m_CleaveLength * 0.5f);
            m_Marker.transform.localScale = new Vector3(m_CleaveWidth, 0.01f, m_CleaveLength);
        }

        void HideMarker()
        {
            if (m_Marker != null)
                Destroy(m_Marker);
            m_Marker = null;
        }

        // Stunned or frozen mid-move: the move is called off, and if it was in the smoke it comes back where it was.
        protected override void Interrupt()
        {
            base.Interrupt();
            if (m_State == State.Collapsed)
                return;
            if (BodyRenderer != null && !BodyRenderer.enabled)
                Smoke(transform.position);
            if (m_State != State.Ready)
                End();
        }

        static Material LineMaterial()
        {
            if (s_Material == null)
            {
                var recorder = FindAnyObjectByType<StrokeRecorder>(FindObjectsInactive.Include);
                s_Material = recorder != null ? recorder.LineMaterial : null;
            }
            return s_Material;
        }

        void OnDestroy()
        {
            HideMarker();
        }
    }
}
