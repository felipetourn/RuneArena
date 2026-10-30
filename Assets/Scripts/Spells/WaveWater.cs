using UnityEngine;

namespace RuneArena.Spells
{
    // Slides the water texture along the wave, and keeps it the same size in the world as the wave narrows.
    public class WaveWater : MonoBehaviour
    {
        static readonly int k_BaseMapSt = Shader.PropertyToID("_BaseMap_ST");

        [Tooltip("How fast the water drifts sideways, in tiles per second.")]
        [SerializeField] float m_Flow = 0.35f;
        [Tooltip("Width of one tile of the water texture, in meters.")]
        [SerializeField] float m_TileWidth = 1.5f;

        Renderer m_Renderer;
        MaterialPropertyBlock m_Block;

        void Awake()
        {
            m_Renderer = GetComponent<Renderer>();
            m_Block = new MaterialPropertyBlock();
        }

        void LateUpdate()
        {
            float tiles = transform.lossyScale.x / m_TileWidth;
            m_Renderer.GetPropertyBlock(m_Block);
            m_Block.SetVector(k_BaseMapSt, new Vector4(tiles, 1f, Time.time * m_Flow, 0f));
            m_Renderer.SetPropertyBlock(m_Block);
        }
    }
}
