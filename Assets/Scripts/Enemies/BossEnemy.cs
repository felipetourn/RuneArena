using RuneArena.Spells;
using RuneArena.Strokes;
using RuneArena.Waves;
using UnityEngine;
using UnityEngine.AI;

namespace RuneArena.Enemies
{
    // Fights in a fixed, learnable cycle, walking towards the player between moves:
    //   charge -> slam -> volley           (below half health: charge -> slam -> summon -> volley, faster)
    // Every move is telegraphed. The charge is the one to play around: it goes straight along a marked
    // strip, smashes earth walls, and if it runs into a wall, a pillar or an earth wall the boss is stunned.
    public class BossEnemy : GroundEnemy
    {
        enum Move
        {
            Charge,
            Slam,
            Volley,
            Summon,
            // Its plain blows, between the moves of the cycle, at whoever comes close.
            Strike,
        }

        enum State
        {
            Ready,
            Approach,
            Telegraph,
            Acting,
        }

        static readonly Move[] k_Calm = { Move.Charge, Move.Slam, Move.Volley };
        static readonly Move[] k_Angry = { Move.Charge, Move.Slam, Move.Summon, Move.Volley };
        static readonly Collider[] s_Hits = new Collider[8];
        static Material s_Material;

        public override bool IsBoss => true;

        [Header("Pattern")]
        [Tooltip("Seconds walking towards the player between two moves.")]
        [SerializeField] float m_Recover = 1.5f;
        [SerializeField, Range(0f, 1f)] float m_AngerHealth = 0.5f;
        [SerializeField] float m_AngerSpeed = 1.3f;
        [Tooltip("Wind-ups and pauses are multiplied by this while angry.")]
        [SerializeField] float m_AngerTime = 0.7f;

        [Header("Strikes")]
        [Tooltip("Plain blows between moves, when the player is this close. Three different ones, in turn.")]
        [SerializeField] float m_StrikeRange = 3.2f;
        [SerializeField] float m_StrikeAngle = 120f;
        [SerializeField] float m_StrikeWindUp = 0.7f;
        [SerializeField] float m_StrikeDamage = 12f;
        [SerializeField] float m_StrikeCooldown = 1.2f;

        [Header("Charge")]
        [SerializeField] float m_ChargeWindUp = 1.2f;
        [SerializeField] float m_ChargeSpeed = 12f;
        [SerializeField] float m_ChargeDistance = 14f;
        [SerializeField] float m_ChargeDamage = 25f;
        [SerializeField] float m_ChargeHitRadius = 1.6f;
        [Tooltip("Stun when the charge ends against a wall, a pillar or an earth wall.")]
        [SerializeField] float m_WallStun = 2.5f;
        [SerializeField] Color m_ChargeColor = new Color(0.9f, 0.35f, 0.1f);

        [Header("Slam")]
        [SerializeField] float m_SlamRange = 3f;
        [SerializeField] float m_SlamRadius = 3.5f;
        [SerializeField] float m_SlamDamage = 30f;
        [SerializeField] float m_SlamWindUp = 1.5f;
        [Tooltip("Gives up getting close after this long and slams anyway.")]
        [SerializeField] float m_SlamApproach = 3f;
        [Tooltip("Seconds it stays planted after the slam lands, while it pulls the pitchfork out of the floor.")]
        [SerializeField] float m_SlamPlanted = 1.9f;
        [SerializeField] Color m_SlamColor = new Color(0.8f, 0.1f, 0.1f);

        [Header("Volley")]
        [SerializeField] EnemyProjectile m_Projectile;
        [SerializeField] Transform m_Muzzle;
        [SerializeField] int m_Shots = 3;
        [Tooltip("Degrees between shots in the fan.")]
        [SerializeField] float m_Spread = 12f;
        [SerializeField] int m_Bursts = 2;
        [SerializeField] float m_BurstGap = 0.5f;
        [SerializeField] float m_ShotDamage = 12f;
        [SerializeField] float m_ShotSpeed = 7f;
        [SerializeField] float m_VolleyWindUp = 1.2f;

        [Header("Summon")]
        [SerializeField] Enemy m_Minion;
        [SerializeField] int m_MinionCount = 2;
        [SerializeField] float m_SummonWindUp = 1f;
        [Tooltip("Seconds the skeletons take to climb out of the floor, during which they can't move or attack.")]
        [SerializeField] float m_EmergeTime = 1.5f;
        [SerializeField] Color m_SummonColor = new Color(0.55f, 0.25f, 1f);

        State m_State;
        Move m_Move;
        int m_Step;
        float m_Until;
        float m_RecoverUntil;
        float m_PlantedUntil;
        bool m_WasAngry;
        WaveSpawner m_Spawner;
        GameObject m_Marker;

        Vector3 m_ChargeDirection;
        float m_Charged;
        bool m_ChargeHit;
        int m_BurstsLeft;

        int m_Strikes;
        float m_StrikeReadyAt;

        bool Angry => Health <= MaxHealth * m_AngerHealth;
        float TimeScale => Angry ? m_AngerTime : 1f;
        protected override float SpeedBonus => Angry ? m_AngerSpeed : 1f;

        protected override void Start()
        {
            base.Start();
            m_Spawner = FindAnyObjectByType<WaveSpawner>();
            m_RecoverUntil = Time.time + m_Recover;
        }

        protected override void Think(float deltaTime)
        {
            Vector3 feet = Player.Feet;

            if (!m_WasAngry && Angry)
            {
                m_WasAngry = true;
                m_Step = 0;
            }

            switch (m_State)
            {
                case State.Ready:
                    if (Time.time < m_PlantedUntil)
                    {
                        Stop();
                        return;
                    }
                    if (Time.time < m_RecoverUntil)
                    {
                        if (FlatDistance(transform.position, feet) <= m_StrikeRange && Time.time >= m_StrikeReadyAt)
                        {
                            m_Move = Move.Strike;
                            m_Strikes++;
                            Telegraph(m_StrikeWindUp, $"Strike{(m_Strikes - 1) % 3 + 1}");
                            return;
                        }
                        MoveTo(feet);
                        return;
                    }
                    Move[] pattern = Angry ? k_Angry : k_Calm;
                    Begin(pattern[m_Step % pattern.Length], feet);
                    break;

                case State.Approach:
                    MoveTo(feet);
                    if (FlatDistance(transform.position, feet) <= m_SlamRange || Time.time >= m_Until)
                    {
                        Telegraph(m_SlamWindUp);
                        ShowRing();
                    }
                    break;

                case State.Telegraph:
                    Stop();
                    if (m_Move == Move.Charge)
                        FaceTowards(transform.position + m_ChargeDirection, 720f, deltaTime);
                    else
                        FaceTowards(feet, 180f, deltaTime);
                    if (Time.time >= m_Until)
                        Act(feet);
                    break;

                case State.Acting:
                    if (m_Move == Move.Charge)
                        UpdateCharge(feet, deltaTime);
                    else if (m_Move == Move.Volley)
                        UpdateVolley();
                    break;
            }
        }

        void Begin(Move move, Vector3 feet)
        {
            m_Move = move;
            switch (move)
            {
                case Move.Charge:
                    Vector3 direction = feet - transform.position;
                    direction.y = 0f;
                    m_ChargeDirection = direction.sqrMagnitude > 0.01f ? direction.normalized : transform.forward;
                    ShowStrip();
                    Telegraph(m_ChargeWindUp);
                    break;
                case Move.Slam:
                    m_State = State.Approach;
                    m_Until = Time.time + m_SlamApproach;
                    break;
                case Move.Volley:
                    Telegraph(m_VolleyWindUp);
                    break;
                case Move.Summon:
                    if (m_Minion == null)
                        End();
                    else
                    {
                        Telegraph(m_SummonWindUp);
                    }
                    break;
            }
        }

        void Telegraph(float windUp, string name = null)
        {
            m_State = State.Telegraph;
            m_Until = Time.time + windUp * TimeScale;
            BeginAttack(windUp * TimeScale, name ?? m_Move.ToString());
        }

        void Act(Vector3 feet)
        {
            HideMarker();
            switch (m_Move)
            {
                case Move.Charge:
                    m_State = State.Acting;
                    m_Charged = 0f;
                    m_ChargeHit = false;
                    break;

                case Move.Slam:
                    // Out of the ring by the time it lands: no damage.
                    if (FlatDistance(transform.position, feet) <= m_SlamRadius)
                        Player.Damage(m_SlamDamage * DamageMultiplier);
                    Flash(m_SlamRadius, m_SlamColor);
                    End();
                    m_PlantedUntil = Time.time + m_SlamPlanted * TimeScale;
                    break;

                case Move.Volley:
                    m_State = State.Acting;
                    m_BurstsLeft = m_Bursts;
                    m_Until = Time.time;
                    break;

                case Move.Summon:
                    Summon();
                    End();
                    break;

                case Move.Strike:
                    Vector3 to = feet - transform.position;
                    to.y = 0f;
                    if (to.magnitude <= m_StrikeRange && Vector3.Angle(transform.forward, to) <= m_StrikeAngle * 0.5f)
                        Player.Damage(m_StrikeDamage * DamageMultiplier);
                    // Back to walking about; the cycle's next move still comes when it was due.
                    m_State = State.Ready;
                    Telegraphing = false;
                    m_StrikeReadyAt = Time.time + m_StrikeCooldown * TimeScale;
                    break;
            }
        }

        void End()
        {
            HideMarker();
            m_State = State.Ready;
            m_Step++;
            m_RecoverUntil = Time.time + m_Recover * TimeScale;
            Telegraphing = false;
        }

        void UpdateCharge(Vector3 feet, float deltaTime)
        {
            float step = m_ChargeSpeed * deltaTime;
            Vector3 before = transform.position;
            Agent.Move(KeepInside(before + m_ChargeDirection * step) - before);
            float moved = FlatDistance(before, transform.position);
            m_Charged += moved;

            if (!m_ChargeHit && FlatDistance(transform.position, feet) <= m_ChargeHitRadius)
            {
                m_ChargeHit = true;
                Player.Damage(m_ChargeDamage * DamageMultiplier);
            }

            // Walls stop it two ways: the NavMesh edge (arena wall, pillars, carved earth walls) and the collider ahead.
            bool blocked = moved < step * 0.3f;
            Vector3 ahead = transform.position + m_ChargeDirection * 1.1f + Vector3.up * 1.3f;
            int count = Physics.OverlapSphereNonAlloc(ahead, 0.5f, s_Hits, LayerMask.GetMask("Wall"), QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                blocked = true;
                EarthWall wall = s_Hits[i].GetComponent<EarthWall>();
                if (wall != null)
                    wall.Break();
            }

            if (blocked)
            {
                End();
                ForceStun(m_WallStun);
            }
            else if (m_Charged >= m_ChargeDistance)
            {
                End();
            }
        }

        void UpdateVolley()
        {
            if (Time.time < m_Until)
                return;

            Vector3 from = m_Muzzle != null ? m_Muzzle.position : transform.position + Vector3.up * 2f;
            Vector3 aim = (Player.Head.position - from).normalized;
            float start = -m_Spread * (m_Shots - 1) * 0.5f;
            for (int i = 0; i < m_Shots; i++)
            {
                Vector3 direction = Quaternion.AngleAxis(start + i * m_Spread, Vector3.up) * aim;
                EnemyProjectile shot = Instantiate(m_Projectile, from, Quaternion.LookRotation(direction));
                shot.Launch(direction * m_ShotSpeed, m_ShotDamage * DamageMultiplier, Player);
            }

            m_BurstsLeft--;
            m_Until = Time.time + m_BurstGap;
            if (m_BurstsLeft <= 0)
                End();
        }

        void Summon()
        {
            for (int i = 0; i < m_MinionCount; i++)
            {
                Vector2 offset = Random.insideUnitCircle.normalized * 2.5f;
                Vector3 spot = transform.position + new Vector3(offset.x, 0f, offset.y);
                if (!NavMesh.SamplePosition(spot, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                    continue;
                Enemy minion = m_Spawner != null
                    ? m_Spawner.SpawnExtra(m_Minion, hit.position, transform.rotation)
                    : Instantiate(m_Minion, hit.position, transform.rotation);
                minion.Emerge(m_EmergeTime);
                SpellFx.Summoning(hit.position, m_SummonColor, m_EmergeTime);
            }
            Flash(1.5f, m_SlamColor);
        }


        void ShowStrip()
        {
            m_Marker = RuneSpell.CreateVisual(PrimitiveType.Cube, "Charge Strip", LineMaterial(), m_ChargeColor);
            Vector3 p = transform.position + m_ChargeDirection * (m_ChargeDistance * 0.5f);
            m_Marker.transform.SetPositionAndRotation(p + Vector3.up * 0.02f, Quaternion.LookRotation(m_ChargeDirection));
            m_Marker.transform.localScale = new Vector3(m_ChargeHitRadius, 0.01f, m_ChargeDistance);
        }

        void ShowRing()
        {
            m_Marker = RuneSpell.CreateVisual(PrimitiveType.Cylinder, "Slam Ring", LineMaterial(), m_SlamColor);
            m_Marker.transform.SetParent(transform, false);
            m_Marker.transform.localPosition = Vector3.up * 0.02f;
            m_Marker.transform.localScale = new Vector3(m_SlamRadius * 2f, 0.005f, m_SlamRadius * 2f);
        }

        void Flash(float radius, Color color)
        {
            GameObject flash = RuneSpell.CreateVisual(PrimitiveType.Cylinder, "Boss Flash", LineMaterial(), color);
            flash.transform.position = transform.position + Vector3.up * 0.03f;
            var from = new Vector3(radius * 2f, 0.005f, radius * 2f);
            SpellVisual.Spawn(flash, from, new Vector3(from.x * 1.15f, from.y, from.z * 1.15f), 0.15f);
        }

        void HideMarker()
        {
            if (m_Marker != null)
                Destroy(m_Marker);
            m_Marker = null;
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

        // A stun cancels the move in progress; the cycle picks up from the next one.
        protected override void Interrupt()
        {
            base.Interrupt();
            if (m_State != State.Ready)
                End();
        }
    }
}
