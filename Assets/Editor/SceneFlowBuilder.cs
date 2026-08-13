#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DevouringBeast.EditorTools
{
    public static class SceneFlowBuilder
    {
        private const string SceneFolder = "Assets/Scenes";
        private const string LoadScenePath = "Assets/Scenes/LoadScene.unity";
        private const string MenuScenePath = "Assets/Scenes/MenuScene.unity";
        private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";
        private const string GameScenePath = "Assets/Scenes/GameScene.unity";
        private const string ButtonPrefabPath = "Assets/_Project/Prefabs/UI/BorderButton.prefab";
        private const string LoadClipPath = "Assets/_Project/Animations/UI/LoadLoop.anim";
        private const string LoadControllerPath = "Assets/_Project/Animations/UI/LoadLoop.controller";
        private const string AudioPrefabPath = "Assets/Resources/System/AudioManager.prefab";

        private static Font _font;

        [MenuItem("DevouringBeast/Build Scene Flow")]
        public static void Build()
        {
            EnsureFolder("Assets/_Project/Prefabs/UI");
            EnsureFolder("Assets/_Project/Animations/UI");
            EnsureFolder("Assets/Resources/System");
            EnsureFolder(SceneFolder);

            BuildAudioManagerPrefab();
            RuntimeAnimatorController loadController = BuildLoadAnimation();
            GameObject buttonPrefab = BuildButtonPrefab();

            MoveGameScene();
            BuildLoadScene(loadController);
            BuildMenuScene(buttonPrefab);
            ConfigureGameScene();
            ConfigureBuildSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.OpenScene(LoadScenePath);
            Debug.Log("[SceneFlowBuilder] LoadScene, MenuScene and GameScene are ready.");
        }

        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static void MoveGameScene()
        {
            SceneAsset game = AssetDatabase.LoadAssetAtPath<SceneAsset>(GameScenePath);
            SceneAsset sample = AssetDatabase.LoadAssetAtPath<SceneAsset>(SampleScenePath);
            if (game == null && sample != null)
            {
                string error = AssetDatabase.MoveAsset(SampleScenePath, GameScenePath);
                if (!string.IsNullOrEmpty(error))
                    throw new InvalidOperationException(error);
            }
        }

        private static void BuildAudioManagerPrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(AudioPrefabPath) != null)
                AssetDatabase.DeleteAsset(AudioPrefabPath);

            GameObject root = new GameObject("AudioManager");
            AudioManager manager = root.AddComponent<AudioManager>();
            SerializedObject serialized = new SerializedObject(manager);

            SetClip(serialized, "normal", "Assets/Audio/BGM/normal.ogg");
            SetClip(serialized, "battle", "Assets/Audio/BGM/battle.ogg");
            SetClip(serialized, "boss", "Assets/Audio/BGM/boss.ogg");
            SetClip(serialized, "split", "Assets/Audio/SFX/Player/split.mp3");
            SetClip(serialized, "bigSplit", "Assets/Audio/SFX/Player/big_split.wav");
            SetClip(serialized, "charged", "Assets/Audio/SFX/Player/charged.wav");
            SetClip(serialized, "hurt", "Assets/Audio/SFX/Player/hurt.wav");
            SetClip(serialized, "die", "Assets/Audio/SFX/Player/die.wav");
            SetClip(serialized, "idle", "Assets/Audio/SFX/Player/idle.wav");
            SetClip(serialized, "run", "Assets/Audio/SFX/Player/run.wav");
            SetClip(serialized, "walk", "Assets/Audio/SFX/Player/walk.wav");
            SetClip(serialized, "suck", "Assets/Audio/SFX/Player/suck.wav");
            SetClip(serialized, "swallow", "Assets/Audio/SFX/Player/swallow.wav");
            SetClip(serialized, "roll", "Assets/Audio/SFX/Player/roll.wav");
            SetClip(serialized, "beastHit", "Assets/Audio/SFX/Env/hit.wav");
            SetClip(serialized, "bossDie", "Assets/Audio/SFX/Enemy/boss_die.wav");
            SetClip(serialized, "enemyDie", "Assets/Audio/SFX/Enemy/enemy_die.wav");
            SetClip(serialized, "rebound", "Assets/Audio/SFX/Enemy/rebound.wav");
            SetClip(serialized, "meatMountainLand", "Assets/Audio/SFX/Enemy/bong.wav");
            SetClip(serialized, "babyCry", "Assets/Audio/SFX/Enemy/baby_cry.wav");
            SetClip(serialized, "satanLaugh", "Assets/Audio/SFX/Enemy/satan_laugh.wav");
            SetClip(serialized, "dash", "Assets/Audio/SFX/Enemy/sou.wav");
            SetClip(serialized, "hit", "Assets/Audio/SFX/Env/hit.wav");
            SetClip(serialized, "bomb", "Assets/Audio/SFX/Env/bomb.wav");
            SetClip(serialized, "levelUp", "Assets/Audio/SFX/Env/level_up.wav");
            SetClip(serialized, "uiClick", "Assets/Audio/SFX/UI/ui_click.wav");
            SetClip(serialized, "rogueSelect", "Assets/Audio/SFX/UI/rogue_select.wav");
            serialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, AudioPrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
        }

        private static void SetClip(SerializedObject serialized, string property, string path)
        {
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null) Debug.LogWarning("[SceneFlowBuilder] Missing audio: " + path);
            serialized.FindProperty(property).objectReferenceValue = clip;
        }

        private static RuntimeAnimatorController BuildLoadAnimation()
        {
            if (AssetDatabase.LoadAssetAtPath<AnimationClip>(LoadClipPath) != null)
                AssetDatabase.DeleteAsset(LoadClipPath);
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(LoadControllerPath) != null)
                AssetDatabase.DeleteAsset(LoadControllerPath);

            Sprite[] frames = AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Sprites/UI/load_anim.png")
                .OfType<Sprite>()
                .OrderBy(sprite => ExtractNumber(sprite.name))
                .ToArray();
            if (frames.Length == 0)
                throw new InvalidOperationException("load_anim contains no Sprite frames.");

            const float frameRate = 12f;
            AnimationClip clip = new AnimationClip { name = "LoadLoop", frameRate = frameRate };
            ObjectReferenceKeyframe[] keys = new ObjectReferenceKeyframe[frames.Length];
            for (int i = 0; i < frames.Length; i++)
                keys[i] = new ObjectReferenceKeyframe { time = i / frameRate, value = frames[i] };

            EditorCurveBinding binding = new EditorCurveBinding
            {
                path = string.Empty,
                type = typeof(Image),
                propertyName = "m_Sprite"
            };
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            AssetDatabase.CreateAsset(clip, LoadClipPath);

            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(LoadControllerPath);
            AnimatorState state = controller.layers[0].stateMachine.AddState("LoadLoop");
            state.motion = clip;
            controller.layers[0].stateMachine.defaultState = state;
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static int ExtractNumber(string value)
        {
            int separator = value.LastIndexOf('_');
            int number;
            return separator >= 0 && int.TryParse(value.Substring(separator + 1), out number)
                ? number
                : int.MaxValue;
        }

        private static GameObject BuildButtonPrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ButtonPrefabPath) != null)
                AssetDatabase.DeleteAsset(ButtonPrefabPath);

            Sprite border = LoadSprite("Assets/Art/Sprites/UI/border.png");
            GameObject root = new GameObject(
                "BorderButton",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button),
                typeof(UIButtonAudio));
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(520f, 100f);
            Image image = root.GetComponent<Image>();
            image.sprite = border;
            image.type = Image.Type.Simple;
            Button button = root.GetComponent<Button>();
            button.targetGraphic = image;

            Text label = CreateText("Label", root.transform, "按钮", 34, TextAnchor.MiddleCenter);
            Stretch(label.rectTransform);
            label.color = new Color(0.12f, 0.09f, 0.16f, 1f);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, ButtonPrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static void BuildLoadScene(RuntimeAnimatorController loadController)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = SceneNames.Load;
            AddCameraAndLight();
            Canvas canvas = CreateCanvas();

            Image background = CreateImage("LoadBackground", canvas.transform, LoadSprite("Assets/Art/Sprites/UI/load_bg.png"));
            Stretch(background.rectTransform);

            Image animationImage = CreateImage(
                "LoadingAnimation",
                canvas.transform,
                AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Sprites/UI/load_anim.png").OfType<Sprite>().First());
            animationImage.rectTransform.anchorMin = animationImage.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            animationImage.rectTransform.sizeDelta = new Vector2(300f, 300f);
            animationImage.rectTransform.anchoredPosition = new Vector2(0f, -220f);
            animationImage.preserveAspect = true;
            Animator animator = animationImage.gameObject.AddComponent<Animator>();
            animator.runtimeAnimatorController = loadController;

            Image done = CreateImage("LoadDone", canvas.transform, LoadSprite("Assets/Art/Sprites/UI/load_done.png"));
            done.rectTransform.anchorMin = done.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            done.rectTransform.sizeDelta = new Vector2(520f, 220f);
            done.rectTransform.anchoredPosition = new Vector2(0f, -220f);
            done.preserveAspect = true;
            done.gameObject.SetActive(false);

            Image interaction = CreateImage("Interaction", canvas.transform, null);
            Stretch(interaction.rectTransform);
            interaction.color = new Color(1f, 1f, 1f, 0f);
            interaction.raycastTarget = true;
            LoadSceneController controller = interaction.gameObject.AddComponent<LoadSceneController>();
            SerializedObject serialized = new SerializedObject(controller);
            serialized.FindProperty("loadingAnimator").objectReferenceValue = animator;
            serialized.FindProperty("loadingImage").objectReferenceValue = animationImage;
            serialized.FindProperty("doneObject").objectReferenceValue = done.gameObject;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            CreateEventSystem();
            EditorSceneManager.SaveScene(scene, LoadScenePath);
        }

        private static void BuildMenuScene(GameObject buttonPrefab)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = SceneNames.Menu;
            AddCameraAndLight();
            Canvas canvas = CreateCanvas();

            Image background = CreateImage("MainBackground", canvas.transform, LoadSprite("Assets/Art/Sprites/UI/main_bg.png"));
            Stretch(background.rectTransform);

            Image menuPanel = CreateImage("Menu", canvas.transform, LoadSprite("Assets/Art/Sprites/UI/menu.png"));
            Center(menuPanel.rectTransform, new Vector2(820f, 820f), Vector2.zero);
            menuPanel.preserveAspect = true;

            GameObject controllerObject = new GameObject("MenuController");
            controllerObject.transform.SetParent(canvas.transform, false);
            MenuSceneController controller = controllerObject.AddComponent<MenuSceneController>();

            Button newButton = CreateButton(buttonPrefab, menuPanel.transform, "新游戏", new Vector2(0f, 150f));
            Button continueButton = CreateButton(buttonPrefab, menuPanel.transform, "继续游戏", new Vector2(0f, 10f));
            Button optionsButton = CreateButton(buttonPrefab, menuPanel.transform, "选项", new Vector2(0f, -130f));
            UnityEventTools.AddPersistentListener(newButton.onClick, controller.OnNewGame);
            UnityEventTools.AddPersistentListener(continueButton.onClick, controller.OnContinueGame);
            UnityEventTools.AddPersistentListener(optionsButton.onClick, controller.OnOptions);

            Sprite listSprite = LoadSprite("Assets/Art/Sprites/UI/list.png");
            Image savePanel = CreateImage("SaveListPanel", canvas.transform, listSprite);
            Center(savePanel.rectTransform, new Vector2(980f, 760f), Vector2.zero);
            savePanel.preserveAspect = true;
            Text saveTitle = CreateText("Title", savePanel.transform, "存档列表", 40, TextAnchor.MiddleCenter);
            Center(saveTitle.rectTransform, new Vector2(700f, 80f), new Vector2(0f, 270f));

            GameObject contentObject = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup));
            contentObject.transform.SetParent(savePanel.transform, false);
            RectTransform content = contentObject.GetComponent<RectTransform>();
            Center(content, new Vector2(560f, 390f), new Vector2(0f, 20f));
            VerticalLayoutGroup layout = contentObject.GetComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 20f;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            Button saveClose = CreateButton(buttonPrefab, savePanel.transform, "返回", new Vector2(0f, -285f), new Vector2(300f, 72f));
            UnityEventTools.AddPersistentListener(saveClose.onClick, controller.OnClosePanels);

            Image actionPanel = CreateImage("SaveActionPanel", canvas.transform, listSprite);
            Center(actionPanel.rectTransform, new Vector2(700f, 480f), Vector2.zero);
            Text selectedText = CreateText("SelectedSave", actionPanel.transform, "存档", 34, TextAnchor.MiddleCenter);
            Center(selectedText.rectTransform, new Vector2(560f, 120f), new Vector2(0f, 120f));
            Button continueSelected = CreateButton(buttonPrefab, actionPanel.transform, "继续", new Vector2(0f, 15f), new Vector2(360f, 80f));
            Button deleteSelected = CreateButton(buttonPrefab, actionPanel.transform, "删除", new Vector2(0f, -85f), new Vector2(360f, 80f));
            Button actionBack = CreateButton(buttonPrefab, actionPanel.transform, "返回", new Vector2(0f, -185f), new Vector2(260f, 65f));
            UnityEventTools.AddPersistentListener(continueSelected.onClick, controller.OnContinueSelected);
            UnityEventTools.AddPersistentListener(deleteSelected.onClick, controller.OnDeleteSelected);
            UnityEventTools.AddPersistentListener(actionBack.onClick, controller.OnClosePanels);

            Image confirmPanel = CreateImage("ConfirmPanel", canvas.transform, listSprite);
            Center(confirmPanel.rectTransform, new Vector2(680f, 400f), Vector2.zero);
            Text confirmText = CreateText("ConfirmText", confirmPanel.transform, "确认？", 34, TextAnchor.MiddleCenter);
            Center(confirmText.rectTransform, new Vector2(560f, 130f), new Vector2(0f, 90f));
            Button yes = CreateButton(buttonPrefab, confirmPanel.transform, "确认", new Vector2(-150f, -90f), new Vector2(260f, 75f));
            Button no = CreateButton(buttonPrefab, confirmPanel.transform, "取消", new Vector2(150f, -90f), new Vector2(260f, 75f));
            UnityEventTools.AddPersistentListener(yes.onClick, controller.OnConfirmYes);
            UnityEventTools.AddPersistentListener(no.onClick, controller.OnConfirmNo);

            Image optionsPanel = CreateImage("OptionsPanel", canvas.transform, listSprite);
            Center(optionsPanel.rectTransform, new Vector2(760f, 560f), Vector2.zero);
            Text optionsTitle = CreateText("Title", optionsPanel.transform, "选项", 42, TextAnchor.MiddleCenter);
            Center(optionsTitle.rectTransform, new Vector2(500f, 80f), new Vector2(0f, 190f));
            CreateTextAt(optionsPanel.transform, "BGM 音量", new Vector2(-210f, 70f));
            Slider bgmSlider = CreateSlider("BgmSlider", optionsPanel.transform, new Vector2(120f, 70f));
            CreateTextAt(optionsPanel.transform, "音效音量", new Vector2(-210f, -30f));
            Slider sfxSlider = CreateSlider("SfxSlider", optionsPanel.transform, new Vector2(120f, -30f));
            Button optionsClose = CreateButton(buttonPrefab, optionsPanel.transform, "返回", new Vector2(0f, -180f), new Vector2(300f, 72f));
            UnityEventTools.AddPersistentListener(optionsClose.onClick, controller.OnClosePanels);

            savePanel.gameObject.SetActive(false);
            actionPanel.gameObject.SetActive(false);
            confirmPanel.gameObject.SetActive(false);
            optionsPanel.gameObject.SetActive(false);

            SerializedObject serialized = new SerializedObject(controller);
            serialized.FindProperty("borderButtonPrefab").objectReferenceValue = buttonPrefab;
            serialized.FindProperty("saveListContent").objectReferenceValue = content;
            serialized.FindProperty("saveListPanel").objectReferenceValue = savePanel.gameObject;
            serialized.FindProperty("actionPanel").objectReferenceValue = actionPanel.gameObject;
            serialized.FindProperty("confirmPanel").objectReferenceValue = confirmPanel.gameObject;
            serialized.FindProperty("optionsPanel").objectReferenceValue = optionsPanel.gameObject;
            serialized.FindProperty("saveListTitle").objectReferenceValue = saveTitle;
            serialized.FindProperty("selectedSaveText").objectReferenceValue = selectedText;
            serialized.FindProperty("confirmText").objectReferenceValue = confirmText;
            serialized.FindProperty("continueSelectedButton").objectReferenceValue = continueSelected;
            serialized.FindProperty("bgmSlider").objectReferenceValue = bgmSlider;
            serialized.FindProperty("sfxSlider").objectReferenceValue = sfxSlider;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            CreateEventSystem();
            EditorSceneManager.SaveScene(scene, MenuScenePath);
        }

        private static void ConfigureGameScene()
        {
            Scene scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            EditorSceneManager.SaveScene(scene, GameScenePath);
        }

        private static void ConfigureBuildSettings()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(LoadScenePath, true),
                new EditorBuildSettingsScene(MenuScenePath, true),
                new EditorBuildSettingsScene(GameScenePath, true)
            };
        }

        private static void AddCameraAndLight()
        {
            GameObject cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;

            GameObject lightObject = new GameObject("Directional Light", typeof(Light));
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            lightObject.GetComponent<Light>().type = LightType.Directional;
        }

        private static Canvas CreateCanvas()
        {
            GameObject canvasObject = new GameObject(
                "Canvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

private static void CreateEventSystem()
        {
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        private static Image CreateImage(string name, Transform parent, Sprite sprite)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = Color.white;
            image.raycastTarget = false;
            return image;
        }

        private static Text CreateText(string name, Transform parent, string value, int size, TextAnchor alignment)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            Text text = go.GetComponent<Text>();
            text.text = value;
            text.font = GetFont();
            text.fontSize = size;
            text.alignment = alignment;
            text.color = new Color(0.16f, 0.12f, 0.2f, 1f);
            text.raycastTarget = false;
            return text;
        }

        private static Text CreateTextAt(Transform parent, string value, Vector2 position)
        {
            Text text = CreateText(value, parent, value, 30, TextAnchor.MiddleCenter);
            Center(text.rectTransform, new Vector2(260f, 60f), position);
            return text;
        }

        private static Font GetFont()
        {
            if (_font == null)
            {
                _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (_font == null) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
            return _font;
        }

        private static Button CreateButton(GameObject prefab, Transform parent, string label, Vector2 position)
        {
            return CreateButton(prefab, parent, label, position, new Vector2(520f, 100f));
        }

        private static Button CreateButton(GameObject prefab, Transform parent, string label, Vector2 position, Vector2 size)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.SetParent(parent, false);
            RectTransform rect = instance.GetComponent<RectTransform>();
            Center(rect, size, position);
            Text text = instance.GetComponentInChildren<Text>(true);
            if (text != null) text.text = label;
            return instance.GetComponent<Button>();
        }

        private static Slider CreateSlider(string name, Transform parent, Vector2 position)
        {
            GameObject root = new GameObject(name, typeof(RectTransform), typeof(Slider));
            root.transform.SetParent(parent, false);
            RectTransform rootRect = root.GetComponent<RectTransform>();
            Center(rootRect, new Vector2(360f, 42f), position);

            Image background = CreateImage("Background", root.transform, null);
            Stretch(background.rectTransform);
            background.color = new Color(0.1f, 0.1f, 0.15f, 0.9f);

            Image fill = CreateImage("Fill", root.transform, null);
            Stretch(fill.rectTransform);
            fill.color = new Color(0.3f, 0.85f, 0.65f, 1f);

            Image handle = CreateImage("Handle", root.transform, null);
            handle.rectTransform.sizeDelta = new Vector2(36f, 54f);
            handle.color = Color.white;

            Slider slider = root.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            return slider;
        }

        private static Sprite LoadSprite(string path)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) throw new InvalidOperationException("Missing sprite: " + path);
            return sprite;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void Center(RectTransform rect, Vector2 size, Vector2 position)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }
    }
}
#endif
