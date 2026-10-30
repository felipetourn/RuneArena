using RuneArena.Spells;
using UnityEngine;

namespace RuneArena.Arena
{
    // On the colosseum's banners, which the Colosseum's Build makes: a child per rune, named Prefix plus
    // its label, shown once the player can cast that rune. Locked runes' banners go up as they unlock.
    public class RuneBanners : MonoBehaviour
    {
        public const string Prefix = "Banner ";

        // The runes with a banner, in the order they take turns around the arena.
        public static readonly string[] Labels = { "circle", "lightning", "triangle", "square", "spiral", "v" };

        const float k_CheckInterval = 0.5f;

        SpellBook m_Book;
        float m_NextCheck;

        void Start()
        {
            m_Book = FindAnyObjectByType<SpellBook>(FindObjectsInactive.Include);
            Refresh();
        }

        void Update()
        {
            if (Time.time < m_NextCheck)
                return;
            m_NextCheck = Time.time + k_CheckInterval;
            Refresh();
        }

        void Refresh()
        {
            foreach (Transform banner in transform)
            {
                if (!banner.name.StartsWith(Prefix))
                    continue;
                string label = banner.name.Substring(Prefix.Length);
                banner.gameObject.SetActive(m_Book == null || m_Book.IsUnlocked(label));
            }
        }
    }
}
