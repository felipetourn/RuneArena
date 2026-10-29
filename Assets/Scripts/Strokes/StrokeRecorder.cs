using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace RuneArena.Strokes
{
    public class StrokeRecorder : MonoBehaviour
    {
        const int k_MaxPoints = 512;
        const int k_MinPoints = 8;

        [SerializeField] XROrigin m_Origin;
        [SerializeField] Transform m_Head;
        [SerializeField] Transform m_Tip;

        [Header("Input")]
        [SerializeField] InputActionProperty m_DrawAction = new InputActionProperty(
            new InputAction("Draw", InputActionType.Button, "<XRController>{RightHand}/{TriggerButton}"));
        [SerializeField] InputActionProperty m_NextLabelAction = new InputActionProperty(
            new InputAction("Next Label", InputActionType.Button, "<XRController>{RightHand}/{SecondaryButton}"));
        [SerializeField] InputActionProperty m_ToggleSaveAction = new InputActionProperty(
            new InputAction("Toggle Save", InputActionType.Button, "<XRController>{LeftHand}/{SecondaryButton}"));
        [Tooltip("Editor only: draw with the left mouse button on a plane in front of the camera.")]
        [SerializeField] bool m_EditorMouseDrawing = true;
        [SerializeField] float m_MouseDrawDistance = 0.5f;

        [Header("Sampling")]
        [SerializeField] float m_MinPointDistance = 0.006f;

        [Header("Recording")]
        [SerializeField] bool m_SaveStrokes = true;
        [SerializeField] string[] m_Labels = { "square", "circle", "lightning", "triangle", "noise" };
        [SerializeField] TMP_Text m_StatusText;
        [Tooltip("Small label fixed to the top right of the view showing whether strokes are being saved. Created if left empty.")]
        [SerializeField] TMP_Text m_SaveIndicator;

        [Header("Line")]
        [SerializeField] Material m_LineMaterial;
        [SerializeField] float m_LineWidth = 0.008f;
        [SerializeField] Color m_DrawColor = Color.white;

        public event Action<StrokeRecorder> StrokeStarted;
        public event Action<StrokeRecorder> StrokeCompleted;

        public IReadOnlyList<Vector4> Points => m_Points;
        public Quaternion HeadRotation => m_HeadRotation;
        public Transform Origin => m_OriginTransform;
        public Transform Tip => m_Tip;
        public LineRenderer Line => m_Line;
        public Material LineMaterial => m_LineMaterial;
        public bool IsDrawing => m_Drawing;
        public bool IsSaving => m_SaveStrokes;

        readonly List<Vector4> m_Points = new List<Vector4>(k_MaxPoints);
        readonly StringBuilder m_Json = new StringBuilder(16 * 1024);

        Transform m_OriginTransform;
        Camera m_Camera;
        Vector3 m_MouseCamPosition;
        Quaternion m_MouseCamRotation;
        bool m_UsingMouse;
        LineRenderer m_Line;
        MaterialPropertyBlock m_LineBlock;
        bool m_ShownSaveState;
        Quaternion m_HeadRotation;
        float m_StartTime;
        float m_MinSqrDistance;
        bool m_Drawing;
        int m_LabelIndex;
        int[] m_Counts;
        string m_RootPath;
        string m_Device;

        void Awake()
        {
            if (m_Origin == null)
                m_Origin = FindAnyObjectByType<XROrigin>();
            m_Camera = m_Origin.Camera;
            if (m_Head == null)
                m_Head = m_Camera.transform;
            if (m_Tip == null)
                m_Tip = transform;

            m_OriginTransform = m_Origin.transform;
            m_MinSqrDistance = m_MinPointDistance * m_MinPointDistance;
            m_Device = Application.isEditor ? "simulator" : "quest2";
            m_RootPath = Path.Combine(Application.persistentDataPath, "strokes");

            m_Counts = new int[m_Labels.Length];
            for (int i = 0; i < m_Labels.Length; i++)
            {
                string dir = Path.Combine(m_RootPath, m_Labels[i]);
                Directory.CreateDirectory(dir);
                m_Counts[i] = Directory.GetFiles(dir, "*.json").Length;
            }

            CreateLine();
            UpdateStatus();
            if (m_SaveIndicator == null)
                CreateSaveIndicator();
            UpdateSaveIndicator();
        }

        void OnEnable()
        {
            if (m_DrawAction.reference == null)
                m_DrawAction.action.Enable();
            if (m_NextLabelAction.reference == null)
                m_NextLabelAction.action.Enable();
            if (m_ToggleSaveAction.reference == null)
                m_ToggleSaveAction.action.Enable();
        }

        void OnDisable()
        {
            if (m_DrawAction.reference == null)
                m_DrawAction.action.Disable();
            if (m_NextLabelAction.reference == null)
                m_NextLabelAction.action.Disable();
            if (m_ToggleSaveAction.reference == null)
                m_ToggleSaveAction.action.Disable();
        }

        void Update()
        {
            InputAction draw = m_DrawAction.action;

            if (m_ToggleSaveAction.action.WasPressedThisFrame())
                m_SaveStrokes = !m_SaveStrokes;
            if (m_SaveStrokes != m_ShownSaveState)
                UpdateSaveIndicator();

            if (!m_Drawing)
            {
                if (draw.WasPressedThisFrame())
                    BeginStroke(false);
                else if (MouseDrawPressed())
                    BeginStroke(true);
            }

            if (m_Drawing)
            {
                AddPoint();
                bool released = m_UsingMouse ? MouseDrawReleased() : draw.WasReleasedThisFrame();
                if (released)
                    EndStroke();
            }
            else if (m_NextLabelAction.action.WasPressedThisFrame())
            {
                m_LabelIndex = (m_LabelIndex + 1) % m_Labels.Length;
                UpdateStatus();
            }
        }

        void BeginStroke(bool usingMouse)
        {
            m_UsingMouse = usingMouse;
            if (usingMouse)
            {
                Transform cam = m_Camera.transform;
                m_MouseCamPosition = m_OriginTransform.InverseTransformPoint(cam.position);
                m_MouseCamRotation = Quaternion.Inverse(m_OriginTransform.rotation) * cam.rotation;
            }

            m_Points.Clear();
            m_Line.positionCount = 0;
            m_Line.widthMultiplier = m_LineWidth;
            SetLineColor(m_DrawColor);
            m_StartTime = Time.time;
            m_HeadRotation = Quaternion.Inverse(m_OriginTransform.rotation) * m_Head.rotation;
            m_Drawing = true;
            StrokeStarted?.Invoke(this);
        }

        void AddPoint()
        {
            int count = m_Points.Count;
            if (count >= k_MaxPoints)
                return;

            Vector3 p = m_OriginTransform.InverseTransformPoint(TipPosition());
            if (count > 0 && (p - (Vector3)m_Points[count - 1]).sqrMagnitude < m_MinSqrDistance)
                return;

            m_Points.Add(new Vector4(p.x, p.y, p.z, Time.time - m_StartTime));
            m_Line.positionCount = count + 1;
            m_Line.SetPosition(count, p);
        }

        Vector3 TipPosition()
        {
#if UNITY_EDITOR
            if (m_UsingMouse)
            {
                // Cursor projected with the camera pose frozen at stroke start, so moving
                // or turning while drawing doesn't add to the stroke.
                Ray ray = m_Camera.ScreenPointToRay(Mouse.current.position.ReadValue());
                Vector3 dir = Quaternion.Inverse(m_Camera.transform.rotation) * ray.direction;
                Vector3 onPlane = dir * (m_MouseDrawDistance / dir.z);
                return m_OriginTransform.TransformPoint(m_MouseCamPosition + m_MouseCamRotation * onPlane);
            }
#endif
            return m_Tip.position;
        }

        bool MouseDrawPressed()
        {
#if UNITY_EDITOR
            return m_EditorMouseDrawing && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#else
            return false;
#endif
        }

        bool MouseDrawReleased()
        {
            return Mouse.current == null || Mouse.current.leftButton.wasReleasedThisFrame;
        }

        void EndStroke()
        {
            m_Drawing = false;

            if (m_Points.Count < k_MinPoints)
            {
                m_Line.positionCount = 0;
                return;
            }

            StrokeCompleted?.Invoke(this);

            if (m_SaveStrokes)
                Save();
        }

        void Save()
        {
            string label = m_Labels[m_LabelIndex];
            string file = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture) + ".json";
            string path = Path.Combine(m_RootPath, label, file);
            string json = BuildJson(label);

            Task.Run(() =>
            {
                try { File.WriteAllText(path, json); }
                catch (Exception e) { Debug.LogException(e); }
            });

            m_Counts[m_LabelIndex]++;
            UpdateStatus();
        }

        string BuildJson(string label)
        {
            StringBuilder sb = m_Json;
            sb.Clear();
            sb.Append("{\"label\":\"").Append(label)
              .Append("\",\"source\":\"real\",\"device\":\"").Append(m_UsingMouse ? "mouse" : m_Device)
              .Append("\",\"headRotation\":[");
            AppendFloat(m_HeadRotation.x).Append(',');
            AppendFloat(m_HeadRotation.y).Append(',');
            AppendFloat(m_HeadRotation.z).Append(',');
            AppendFloat(m_HeadRotation.w);
            sb.Append("],\"points\":[");

            for (int i = 0; i < m_Points.Count; i++)
            {
                Vector4 p = m_Points[i];
                if (i > 0)
                    sb.Append(',');
                sb.Append('[');
                AppendFloat(p.x).Append(',');
                AppendFloat(p.y).Append(',');
                AppendFloat(p.z).Append(',');
                AppendFloat(p.w);
                sb.Append(']');
            }

            sb.Append("]}");
            return sb.ToString();
        }

        StringBuilder AppendFloat(float v)
        {
            return m_Json.Append(v.ToString("0.#####", CultureInfo.InvariantCulture));
        }

        void CreateLine()
        {
            var go = new GameObject("StrokeLine");
            go.transform.SetParent(m_OriginTransform, false);

            m_Line = go.AddComponent<LineRenderer>();
            m_LineBlock = new MaterialPropertyBlock();
            m_Line.useWorldSpace = false;
            m_Line.widthMultiplier = m_LineWidth;
            m_Line.sharedMaterial = m_LineMaterial;
            m_Line.numCapVertices = 2;
            m_Line.numCornerVertices = 2;
            m_Line.shadowCastingMode = ShadowCastingMode.Off;
            m_Line.receiveShadows = false;
            m_Line.positionCount = 0;

            if (m_LineMaterial == null)
                Debug.LogWarning("StrokeRecorder: no line material assigned", this);
        }

        public void SetLineColor(Color color)
        {
            m_Line.GetPropertyBlock(m_LineBlock);
            m_LineBlock.SetColor("_BaseColor", color);
            m_Line.SetPropertyBlock(m_LineBlock);
        }

        public void ClearLine()
        {
            m_Line.positionCount = 0;
        }

        void CreateSaveIndicator()
        {
            var go = new GameObject("SaveIndicator");
            go.transform.SetParent(m_Camera.transform, false);
            go.transform.localPosition = new Vector3(0.12f, 0.08f, 0.4f);

            var text = go.AddComponent<TextMeshPro>();
            text.fontSize = 0.12f;
            text.alignment = TextAlignmentOptions.TopRight;
            text.rectTransform.sizeDelta = new Vector2(0.1f, 0.02f);
            m_SaveIndicator = text;
        }

        void UpdateSaveIndicator()
        {
            m_ShownSaveState = m_SaveStrokes;
            // The per-rune counts are only useful while recording.
            if (m_StatusText != null)
                m_StatusText.gameObject.SetActive(m_SaveStrokes);
            if (m_SaveIndicator == null)
                return;

            m_SaveIndicator.text = m_SaveStrokes ? "REC" : "REC OFF";
            m_SaveIndicator.color = m_SaveStrokes ? new Color(1f, 0.3f, 0.3f) : new Color(0.6f, 0.6f, 0.6f);
        }

        void UpdateStatus()
        {
            if (m_StatusText == null)
                return;

            var sb = new StringBuilder(128);
            for (int i = 0; i < m_Labels.Length; i++)
            {
                sb.Append(i == m_LabelIndex ? "> " : "  ")
                  .Append(m_Labels[i]).Append(": ").Append(m_Counts[i]).Append('\n');
            }
            m_StatusText.text = sb.ToString();
        }
    }
}
