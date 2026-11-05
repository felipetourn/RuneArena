using RuneArena.Effects;
using UnityEngine;

namespace RuneArena.Spells
{
    // Square. Raises a wall where the aim meets the floor (enemies in the way are ignored), facing the player.
    // Blocks walking enemies, enemy shots and the player's own spells. Flying enemies go over it.
    public class EarthWallSpell : RuneSpell
    {
        [SerializeField] Color m_Color = new Color(0.6f, 0.4f, 0.2f);
        [SerializeField] float m_ManaCost = 35f;
        [SerializeField] float m_Range = 30f;
        [Tooltip("Speed (m/s) of the square flying to the floor. The wall comes up when it lands.")]
        [SerializeField] float m_TravelSpeed = 15f;
        [SerializeField] float m_Width = 4f;
        [SerializeField] float m_Height = 2.5f;
        [SerializeField] float m_Thickness = 0.6f;
        [SerializeField] float m_Duration = 8f;
        [SerializeField] float m_RiseTime = 0.4f;
        [Tooltip("Push speed (m/s) for enemies standing where the wall comes up.")]
        [SerializeField] float m_Push = 6f;

        public override string Label => "square";
        public override Color Color => m_Color;
        public override float ManaCost => m_ManaCost;

        public float Width => m_Width;
        public float Height => m_Height;
        public float Thickness => m_Thickness;
        public float Duration => m_Duration;
        public float RiseTime => m_RiseTime;
        public float PushSpeed => m_Push;

        public override void Cast(in SpellCast cast)
        {
            // Enemies don't stop the aim, but walls do: aiming at the colosseum wall or a gate raises nothing,
            // instead of going through and raising a wall on the floor outside.
            if (!Physics.Raycast(cast.Aim, out RaycastHit hit, m_Range, FloorMask | WallMask, QueryTriggerInteraction.Ignore))
                return;
            if (!IsInLayer(hit.collider, FloorMask))
                return;

            Vector3 from = cast.Player != null ? cast.Player.Feet : cast.Origin;
            Vector3 facing = Flat(hit.point - from);
            if (facing.sqrMagnitude < 0.01f)
                facing = Flat(cast.Aim.direction);
            if (facing.sqrMagnitude < 0.01f)
                return;

            Material earth = Earth(cast.Material, out Color tint);
            GameObject seed = CreateVisual(PrimitiveType.Cube, "Earth Seed", earth, tint);
            seed.transform.SetPositionAndRotation(cast.Origin, Quaternion.LookRotation(facing.normalized));
            seed.transform.localScale = Vector3.one * 0.3f;
            SpellFx.SeedTrail(seed.transform);
            seed.AddComponent<EarthSeed>().Launch(this, hit.point, cast.Material, m_Duration * cast.Power);
        }

        public void RaiseWall(Vector3 ground, Quaternion rotation, Material material, float duration)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Earth Wall";
            go.layer = LayerMask.NameToLayer("Wall");
            Tint(go.GetComponent<Renderer>(), Earth(material, out Color tint), tint);
            Rocks(go);
            go.AddComponent<EarthWall>().Raise(this, ground, rotation, duration);
            SpellFx.Dust(ground, Width, true);
        }

        // With effects the box is drawn as a row of rock slabs instead; it still is what blocks.
        static void Rocks(GameObject wall)
        {
            FxLibrary fx = Fx.Library;
            if (fx == null || fx.Rock == null)
                return;
            wall.GetComponent<Renderer>().enabled = false;
            var rocks = new GameObject("Rocks");
            rocks.transform.SetParent(wall.transform, false);
            rocks.AddComponent<MeshFilter>().sharedMesh = SpellMeshes.Rocks(Random.Range(0, int.MaxValue));
            rocks.AddComponent<MeshRenderer>().sharedMaterial = fx.Rock;
        }

        // The packed earth texture when there is one; otherwise the plain material in the spell's color.
        Material Earth(Material fallback, out Color tint)
        {
            FxLibrary fx = Fx.Library;
            bool textured = fx != null && fx.Earth != null;
            tint = textured ? Color.white : m_Color;
            return textured ? fx.Earth : fallback;
        }

        public float TravelSpeed => m_TravelSpeed;
    }
}
