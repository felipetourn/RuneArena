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
        const string k_FramePath = "Assets/Textures/UI/runico_base.png";
        const string k_GlowPath = "Assets/Textures/UI/runico_brillo.png";
        // In sprite pixels: the corners are 100 px and the glow runes along the edges repeat every 52 px.
        const float k_FrameBorder = 100f;
        const float k_RuneSpacing = 52f;
        const float k_FrameScale = 2f;
        static readonly Vector2 k_PanelSize = FrameSize(new Vector2(700f, 680f));
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
            if (AssetDatabase.LoadAssetAtPath<Sprite>(k_FramePath) == null || AssetDatabase.LoadAssetAtPath<Sprite>(k_GlowPath) == null)
            {
                Debug.LogError("Create Game UI: the frame sprites are missing from Assets/Textures/UI (import them as Sprite).");
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

            GameObject start = Panel(root.transform, "Start Panel", "RuneArena", "Elegí la dificultad:", out _);
            so.FindProperty("m_StartPanel").objectReferenceValue = start;
            so.FindProperty("m_EasyButton").objectReferenceValue = ButtonIn(start, "Fácil");
            so.FindProperty("m_NormalButton").objectReferenceValue = ButtonIn(start, "Normal");
            so.FindProperty("m_HardButton").objectReferenceValue = ButtonIn(start, "Difícil");
            so.FindProperty("m_StartSettingsButton").objectReferenceValue = ButtonIn(start, "Opciones");
            so.FindProperty("m_QuitButton").objectReferenceValue = ButtonIn(start, "Salir");

            GameObject pause = Panel(root.transform, "Pause Panel", "Pausa", "", out TMP_Text pauseInfo);
            so.FindProperty("m_PausePanel").objectReferenceValue = pause;
            so.FindProperty("m_PauseInfo").objectReferenceValue = pauseInfo;
            so.FindProperty("m_ResumeButton").objectReferenceValue = ButtonIn(pause, "Continuar");
            so.FindProperty("m_PauseRestartButton").objectReferenceValue = ButtonIn(pause, "Reiniciar");
            so.FindProperty("m_PauseSettingsButton").objectReferenceValue = ButtonIn(pause, "Opciones");
            so.FindProperty("m_PauseMenuButton").objectReferenceValue = ButtonIn(pause, "Menú principal");

            GameObject over = Panel(root.transform, "Game Over Panel", "Moriste", "", out TMP_Text overInfo);
            so.FindProperty("m_GameOverPanel").objectReferenceValue = over;
            so.FindProperty("m_GameOverInfo").objectReferenceValue = overInfo;
            so.FindProperty("m_RetryButton").objectReferenceValue = ButtonIn(over, "Reintentar");
            so.FindProperty("m_GameOverMenuButton").objectReferenceValue = ButtonIn(over, "Menú principal");
            GameObject upgrades = Panel(root.transform, "Upgrade Panel", "Elegí una mejora", "", out TMP_Text upgradeInfo);
            so.FindProperty("m_UpgradePanel").objectReferenceValue = upgrades;
            so.FindProperty("m_UpgradeInfo").objectReferenceValue = upgradeInfo;
            upgradeInfo.fontSize = 24f;
            // A little room between the title and the first card.
            upgrades.transform.Find("Title").GetComponent<LayoutElement>().preferredHeight = 100f;
            // Taller than the other panels: three cards plus the skip button.
            var upgradesRect = (RectTransform)upgrades.transform;
            upgradesRect.anchorMin = upgradesRect.anchorMax = new Vector2(0.5f, 0.5f);
            upgradesRect.sizeDelta = FrameSize(new Vector2(k_PanelSize.x + 60f, 780f));
            SerializedProperty buttons = so.FindProperty("m_UpgradeButtons");
            buttons.arraySize = 3;
            for (int i = 0; i < 3; i++)
            {
                Button button = ButtonIn(upgrades, $"Mejora {i + 1}");
                button.GetComponent<LayoutElement>().preferredHeight = 100f;
                buttons.GetArrayElementAtIndex(i).objectReferenceValue = button;
            }
            Button skip = ButtonIn(upgrades, "Saltear");
            skip.GetComponent<LayoutElement>().preferredHeight = 100f;
            so.FindProperty("m_SkipButton").objectReferenceValue = skip;

            GameObject settings = Panel(root.transform, "Settings Panel", "Opciones", "", out TMP_Text settingsInfo);
            so.FindProperty("m_SettingsPanel").objectReferenceValue = settings;
            settingsInfo.gameObject.SetActive(false);
            var settingsRect = (RectTransform)settings.transform;
            settingsRect.anchorMin = settingsRect.anchorMax = new Vector2(0.5f, 0.5f);
            settingsRect.sizeDelta = upgradesRect.sizeDelta;
            // Their labels come from GameSettings when the panel opens.
            SerializedProperty settingButtons = so.FindProperty("m_SettingButtons");
            settingButtons.arraySize = System.Enum.GetValues(typeof(Setting)).Length;
            for (int i = 0; i < settingButtons.arraySize; i++)
            {
                Button button = ButtonIn(settings, ((Setting)i).ToString());
                button.GetComponentInChildren<TMP_Text>().fontSizeMax = 26f;
                settingButtons.GetArrayElementAtIndex(i).objectReferenceValue = button;
            }
            so.FindProperty("m_SoundButton").objectReferenceValue = ButtonIn(settings, "Sonido");
            so.FindProperty("m_SettingsBackButton").objectReferenceValue = ButtonIn(settings, "Volver");

            GameObject sound = Panel(root.transform, "Sound Panel", "Sonido", "", out TMP_Text soundInfo);
            so.FindProperty("m_SoundPanel").objectReferenceValue = sound;
            soundInfo.gameObject.SetActive(false);
            string[] volumes = { "Música", "Hechizos", "Enemigos", "Público" };
            SerializedProperty sliders = so.FindProperty("m_VolumeSliders");
            sliders.arraySize = volumes.Length;
            for (int i = 0; i < volumes.Length; i++)
                sliders.GetArrayElementAtIndex(i).objectReferenceValue = SliderIn(sound, volumes[i]);
            so.FindProperty("m_SoundBackButton").objectReferenceValue = ButtonIn(sound, "Volver");
            so.FindProperty("m_FrameCorners").floatValue = 2f * k_FrameBorder / k_FrameScale;
            so.FindProperty("m_FrameRuneSpacing").floatValue = k_RuneSpacing / k_FrameScale;
            so.ApplyModifiedPropertiesWithoutUndo();

            pause.SetActive(false);
            over.SetActive(false);
            upgrades.SetActive(false);
            settings.SetActive(false);
            sound.SetActive(false);

            GameObject game = spawner.gameObject;
            var manager = game.GetComponent<GameManager>();
            if (manager == null)
                manager = Undo.AddComponent<GameManager>(game);
            if (game.GetComponent<Upgrades>() == null)
                Undo.AddComponent<Upgrades>(game);
            if (game.GetComponent<GameSettings>() == null)
                Undo.AddComponent<GameSettings>(game);

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
            Frame(panel.GetComponent<Image>(), k_FramePath);

            var glow = new GameObject("Glow", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            glow.layer = panel.layer;
            glow.transform.SetParent(panel.transform, false);
            Stretch((RectTransform)glow.transform);
            glow.GetComponent<LayoutElement>().ignoreLayout = true;
            Image glowImage = glow.GetComponent<Image>();
            Frame(glowImage, k_GlowPath);
            glowImage.raycastTarget = false;

            var layout = panel.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(64, 64, 56, 56);
            layout.spacing = 16f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            OneLine(Text(panel.transform, "Title", title, 64f, 80f, FontStyles.Bold));
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
            OneLine(text);

            var button = go.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(0.55f, 0.6f, 0.75f);
            colors.pressedColor = new Color(0.8f, 0.7f, 0.3f);
            button.colors = colors;
            return button;
        }

        // A row with the name on the left and a slider from 0 to 1 filling the rest.
        static Slider SliderIn(GameObject panel, string label)
        {
            var row = new GameObject($"{label} Row", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            row.layer = panel.layer;
            row.transform.SetParent(panel.transform, false);
            row.GetComponent<LayoutElement>().preferredHeight = 64f;
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 24f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            TMP_Text text = Text(row.transform, "Label", label, 30f, 64f, FontStyles.Normal);
            text.alignment = TextAlignmentOptions.Left;
            text.GetComponent<LayoutElement>().preferredWidth = 200f;

            GameObject go = DefaultControls.CreateSlider(new DefaultControls.Resources());
            go.name = $"{label} Slider";
            go.transform.SetParent(row.transform, false);
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = panel.layer;
            var element = go.AddComponent<LayoutElement>();
            element.flexibleWidth = 1f;
            element.preferredHeight = 28f;

            var slider = go.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;
            slider.transform.Find("Background").GetComponent<Image>().color = k_ButtonColor;
            slider.fillRect.GetComponent<Image>().color = new Color(0.35f, 0.75f, 0.85f);
            slider.handleRect.GetComponent<Image>().color = Color.white;
            slider.handleRect.sizeDelta = new Vector2(32f, 16f);
            ColorBlock colors = slider.colors;
            colors.highlightedColor = new Color(0.8f, 0.9f, 1f);
            colors.pressedColor = new Color(0.8f, 0.7f, 0.3f);
            slider.colors = colors;
            return slider;
        }

        // Never wraps: each line shrinks to fit the width instead.
        static void OneLine(TMP_Text text)
        {
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.enableAutoSizing = true;
            text.fontSizeMax = text.fontSize;
            text.fontSizeMin = text.fontSize * 0.4f;
        }

        static void Frame(Image image, string spritePath)
        {
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            image.type = Image.Type.Tiled;
            image.pixelsPerUnitMultiplier = k_FrameScale;
        }

        // Rounds the size up so the tiled edges end between two runes instead of cutting one in half.
        static Vector2 FrameSize(Vector2 size)
        {
            float corners = 2f * k_FrameBorder / k_FrameScale;
            float spacing = k_RuneSpacing / k_FrameScale;
            return new Vector2(
                corners + Mathf.Ceil((size.x - corners) / spacing) * spacing,
                corners + Mathf.Ceil((size.y - corners) / spacing) * spacing);
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
