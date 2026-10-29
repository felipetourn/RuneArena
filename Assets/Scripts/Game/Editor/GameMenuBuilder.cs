using RuneArena.Waves;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace RuneArena.Game.Editor
{
    // Builds the menu canvas into the open scene and adds the GameManager next to the WaveSpawner.
    // Running it again replaces the canvas, so restyle it after the layout is settled.
    static class GameMenuBuilder
    {
        const float k_Scale = 0.001f;
        static readonly Vector2 k_PanelSize = new Vector2(700f, 560f);
        static readonly Color k_PanelColor = new Color(0.08f, 0.08f, 0.1f, 0.92f);
        static readonly Color k_ButtonColor = new Color(0.25f, 0.27f, 0.32f);

        [MenuItem("RuneArena/Create Game UI")]
        static void Create()
        {
            var spawner = Object.FindAnyObjectByType<WaveSpawner>();
            if (spawner == null)
            {
                Debug.LogError("Create Game UI: add the WaveSpawner first (on a 'Game' object).");
                return;
            }

            GameObject existing = GameObject.Find("Game UI");
            if (existing != null)
                Undo.DestroyObjectImmediate(existing);

            var root = new GameObject("Game UI", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(root, "Create Game UI");
            root.layer = LayerMask.NameToLayer("UI");

            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            root.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 10f;
            root.AddComponent<GraphicRaycaster>();
            root.AddComponent<TrackedDeviceGraphicRaycaster>();

            var rect = (RectTransform)root.transform;
            rect.sizeDelta = k_PanelSize;
            rect.localScale = Vector3.one * k_Scale;

            var menu = root.AddComponent<GameMenu>();
            var so = new SerializedObject(menu);
            so.FindProperty("m_Canvas").objectReferenceValue = canvas;

            GameObject start = Panel(root.transform, "Start Panel", "RuneArena", "Dibujá runas en el aire para lanzar hechizos.\nElegí la dificultad:", out _);
            so.FindProperty("m_StartPanel").objectReferenceValue = start;
            so.FindProperty("m_EasyButton").objectReferenceValue = ButtonIn(start, "Fácil");
            so.FindProperty("m_NormalButton").objectReferenceValue = ButtonIn(start, "Normal");
            so.FindProperty("m_HardButton").objectReferenceValue = ButtonIn(start, "Difícil");
            so.FindProperty("m_QuitButton").objectReferenceValue = ButtonIn(start, "Salir");

            GameObject pause = Panel(root.transform, "Pause Panel", "Pausa", "", out TMP_Text pauseInfo);
            so.FindProperty("m_PausePanel").objectReferenceValue = pause;
            so.FindProperty("m_PauseInfo").objectReferenceValue = pauseInfo;
            so.FindProperty("m_ResumeButton").objectReferenceValue = ButtonIn(pause, "Continuar");
            so.FindProperty("m_PauseRestartButton").objectReferenceValue = ButtonIn(pause, "Reiniciar");
            so.FindProperty("m_PauseMenuButton").objectReferenceValue = ButtonIn(pause, "Menú principal");

            GameObject over = Panel(root.transform, "Game Over Panel", "Caíste", "", out TMP_Text overInfo);
            so.FindProperty("m_GameOverPanel").objectReferenceValue = over;
            so.FindProperty("m_GameOverInfo").objectReferenceValue = overInfo;
            so.FindProperty("m_RetryButton").objectReferenceValue = ButtonIn(over, "Reintentar");
            so.FindProperty("m_GameOverMenuButton").objectReferenceValue = ButtonIn(over, "Menú principal");
            GameObject upgrades = Panel(root.transform, "Upgrade Panel", "Elegí una mejora", "", out TMP_Text upgradeInfo);
            so.FindProperty("m_UpgradePanel").objectReferenceValue = upgrades;
            so.FindProperty("m_UpgradeInfo").objectReferenceValue = upgradeInfo;
            upgradeInfo.fontSize = 24f;
            // Taller than the other panels: three cards plus the skip button.
            var upgradesRect = (RectTransform)upgrades.transform;
            upgradesRect.anchorMin = upgradesRect.anchorMax = new Vector2(0.5f, 0.5f);
            upgradesRect.sizeDelta = new Vector2(k_PanelSize.x + 60f, 780f);
            SerializedProperty buttons = so.FindProperty("m_UpgradeButtons");
            buttons.arraySize = 3;
            for (int i = 0; i < 3; i++)
            {
                Button button = ButtonIn(upgrades, $"Mejora {i + 1}");
                button.GetComponent<LayoutElement>().preferredHeight = 100f;
                buttons.GetArrayElementAtIndex(i).objectReferenceValue = button;
            }
            so.FindProperty("m_SkipButton").objectReferenceValue = ButtonIn(upgrades, "Saltear");
            so.ApplyModifiedPropertiesWithoutUndo();

            pause.SetActive(false);
            over.SetActive(false);
            upgrades.SetActive(false);

            GameObject game = spawner.gameObject;
            var manager = game.GetComponent<GameManager>();
            if (manager == null)
                manager = Undo.AddComponent<GameManager>(game);
            if (game.GetComponent<Upgrades>() == null)
                Undo.AddComponent<Upgrades>(game);

            var managerSo = new SerializedObject(manager);
            managerSo.FindProperty("m_Spawner").objectReferenceValue = spawner;
            managerSo.FindProperty("m_Menu").objectReferenceValue = menu;
            managerSo.ApplyModifiedPropertiesWithoutUndo();

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.scene);
            Selection.activeObject = root;
        }

        static GameObject Panel(Transform parent, string name, string title, string info, out TMP_Text infoText)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            panel.layer = parent.gameObject.layer;
            panel.transform.SetParent(parent, false);
            Stretch((RectTransform)panel.transform);
            panel.GetComponent<Image>().color = k_PanelColor;

            var layout = panel.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(60, 60, 40, 40);
            layout.spacing = 16f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            Text(panel.transform, "Title", title, 64f, 80f, FontStyles.Bold);
            infoText = Text(panel.transform, "Info", info, 30f, 120f, FontStyles.Normal);
            return panel;
        }

        static TMP_Text Text(Transform parent, string name, string value, float size, float height, FontStyles style)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(LayoutElement));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().preferredHeight = height;

            var text = go.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            return text;
        }

        static Button ButtonIn(GameObject panel, string label)
        {
            GameObject go = TMP_DefaultControls.CreateButton(new TMP_DefaultControls.Resources());
            go.name = $"{label} Button";
            go.layer = panel.layer;
            foreach (Transform child in go.transform)
                child.gameObject.layer = panel.layer;
            go.transform.SetParent(panel.transform, false);
            go.AddComponent<LayoutElement>().preferredHeight = 64f;

            go.GetComponent<Image>().color = k_ButtonColor;
            var text = go.GetComponentInChildren<TMP_Text>();
            text.text = label;
            text.fontSize = 32f;
            text.color = Color.white;

            var button = go.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(0.55f, 0.6f, 0.75f);
            colors.pressedColor = new Color(0.8f, 0.7f, 0.3f);
            button.colors = colors;
            return button;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
