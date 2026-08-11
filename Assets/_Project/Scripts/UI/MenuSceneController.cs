using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DevouringBeast
{
    public sealed class MenuSceneController : MonoBehaviour
    {
        [SerializeField] private GameObject borderButtonPrefab;
        [SerializeField] private Transform saveListContent;
        [SerializeField] private GameObject saveListPanel;
        [SerializeField] private GameObject actionPanel;
        [SerializeField] private GameObject confirmPanel;
        [SerializeField] private GameObject optionsPanel;
        [SerializeField] private Text saveListTitle;
        [SerializeField] private Text selectedSaveText;
        [SerializeField] private Text confirmText;
        [SerializeField] private Button continueSelectedButton;
        [SerializeField] private Slider bgmSlider;
        [SerializeField] private Slider sfxSlider;

        private int _selectedSlot = -1;
        private bool _newGameMode;
        private bool _confirmDelete;
        private readonly List<Selectable> _blockedMenuSelectables = new();
        private float _ignoreInputUntil;

        private void Start()
        {
            _ignoreInputUntil = Time.unscaledTime + 0.25f;
            AudioManager.EnsureInitialized();
            SaveGameService.Initialize();
            AudioManager.Instance.PlayBgm(BgmTrack.Normal);
            HideAllPanels();
            if (bgmSlider != null)
            {
                ConfigureSliderRaycasts(bgmSlider);
                bgmSlider.SetValueWithoutNotify(AudioManager.Instance.BgmVolume);
                bgmSlider.onValueChanged.AddListener(AudioManager.Instance.SetBgmVolume);
            }
            if (sfxSlider != null)
            {
                ConfigureSliderRaycasts(sfxSlider);
                sfxSlider.SetValueWithoutNotify(AudioManager.Instance.SfxVolume);
                sfxSlider.onValueChanged.AddListener(AudioManager.Instance.SetSfxVolume);
            }
            ConfigureOptionsPanel();
            ConfigureSaveListScrolling();
            BuildTestButton();
        }

        private void BuildTestButton()
        {
            if (GameObject.Find("TestRoomButton") != null) return;
            Canvas canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null) return;
            GameObject buttonObject = new("TestRoomButton", typeof(RectTransform), typeof(Image),
                typeof(Button), typeof(UIButtonAudio));
            Transform menu = canvas.transform.Find("Menu");
            buttonObject.transform.SetParent(menu != null ? menu : canvas.transform, false);
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, -250f);
            rect.sizeDelta = new Vector2(300f, 64f);
            Image image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.14f, 0.16f, 0.2f, 0.96f);
            Text label = new GameObject("Label", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            label.transform.SetParent(buttonObject.transform, false);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 22;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.text = "\u6d4b\u8bd5\u623f\u95f4";
            buttonObject.GetComponent<Button>().onClick.AddListener(OnTestRoom);
        }

        public void OnTestRoom()
        {
            if (Time.unscaledTime < _ignoreInputUntil) return;
            GameManager.Instance.StartTestGame();
            SceneManager.LoadScene(SceneNames.Game);
        }

        public void OnNewGame()
        {
            if (Time.unscaledTime < _ignoreInputUntil) return;
            _newGameMode = true;
            ShowSaveList("选择新游戏存档位");
        }

        public void OnContinueGame()
        {
            if (Time.unscaledTime < _ignoreInputUntil) return;
            _newGameMode = false;
            ShowSaveList("选择要继续的存档");
        }

        public void OnOptions()
        {
            if (Time.unscaledTime < _ignoreInputUntil) return;
            HideAllPanels();
            if (optionsPanel == null) return;
            optionsPanel.transform.SetAsLastSibling();
            optionsPanel.SetActive(true);
            BlockUnderlyingMenuInput();
        }

        public void OnClosePanels() { HideAllPanels(); }

        public void OnContinueSelected()
        {
            if (Time.unscaledTime < _ignoreInputUntil) return;
            SaveSlotData data = SaveGameService.GetSlot(_selectedSlot);
            if (data == null) return;
            SaveGameService.SetActiveSlot(_selectedSlot);
            StartGameScene();
        }

        public void OnDeleteSelected()
        {
            if (Time.unscaledTime < _ignoreInputUntil) return;
            SaveSlotData data = SaveGameService.GetSlot(_selectedSlot);
            if (data == null) return;
            _confirmDelete = true;
            if (confirmText != null) confirmText.text = "确认删除 " + data.displayName + "？";
            if (confirmPanel != null) confirmPanel.SetActive(true);
        }

        public void OnConfirmYes()
        {
            if (Time.unscaledTime < _ignoreInputUntil) return;
            if (_selectedSlot < 0) return;
            if (_confirmDelete)
            {
                SaveGameService.DeleteSlot(_selectedSlot);
                _confirmDelete = false;
                if (confirmPanel != null) confirmPanel.SetActive(false);
                if (actionPanel != null) actionPanel.SetActive(false);
                RefreshSaveSlots();
            }
            else
            {
                SaveGameService.CreateNewGame(_selectedSlot);
                StartGameScene();
            }
        }

        public void OnConfirmNo()
        {
            if (confirmPanel != null) confirmPanel.SetActive(false);
        }

        private void ShowSaveList(string title)
        {
            HideAllPanels();
            if (saveListTitle != null) saveListTitle.text = title;
            if (saveListPanel != null) saveListPanel.SetActive(true);
            RefreshSaveSlots();
        }

        private void RefreshSaveSlots()
        {
            if (saveListContent == null || borderButtonPrefab == null) return;
            for (int i = saveListContent.childCount - 1; i >= 0; i--)
                Destroy(saveListContent.GetChild(i).gameObject);

            SaveSlotData[] slots = SaveGameService.GetAllSlots();
            for (int i = 0; i < slots.Length; i++)
            {
                int slotIndex = i;
                GameObject buttonObject = Instantiate(borderButtonPrefab, saveListContent);
                buttonObject.name = "SaveSlot_" + (i + 1);
                Text label = buttonObject.GetComponentInChildren<Text>(true);
                SaveSlotData data = slots[i];
                if (label != null)
                {
                    label.text = data == null
                        ? "存档 " + (i + 1) + "　空"
                        : data.displayName + "　波次 " + data.completedWave + "\n" +
                          new DateTime(data.updatedTicks, DateTimeKind.Utc).ToLocalTime().ToString("yyyy-MM-dd HH:mm");
                }
                buttonObject.GetComponent<Button>().onClick.AddListener(() => OnSlotSelected(slotIndex));
            }
        }

        private void OnSlotSelected(int slotIndex)
        {
            _selectedSlot = slotIndex;
            SaveSlotData data = SaveGameService.GetSlot(slotIndex);
            if (_newGameMode)
            {
                if (data == null)
                {
                    SaveGameService.CreateNewGame(slotIndex);
                    StartGameScene();
                }
                else
                {
                    _confirmDelete = false;
                    if (confirmText != null) confirmText.text = "覆盖 " + data.displayName + " 并开始新游戏？";
                    if (confirmPanel != null) confirmPanel.SetActive(true);
                }
                return;
            }

            if (data == null) return;
            if (selectedSaveText != null)
                selectedSaveText.text = data.displayName + "\n已完成波次：" + data.completedWave;
            if (continueSelectedButton != null) continueSelectedButton.interactable = true;
            if (actionPanel != null) actionPanel.SetActive(true);
        }

        private void StartGameScene()
        {
            GameManager.Instance.StartGame();
            SceneManager.LoadScene(SceneNames.Game);
        }

        private void HideAllPanels()
        {
            RestoreUnderlyingMenuInput();
            if (saveListPanel != null) saveListPanel.SetActive(false);
            if (actionPanel != null) actionPanel.SetActive(false);
            if (confirmPanel != null) confirmPanel.SetActive(false);
            if (optionsPanel != null) optionsPanel.SetActive(false);
        }

        private static void ConfigureSliderRaycasts(Slider slider)
        {
            slider.interactable = true;
            Transform background = slider.transform.Find("Background");
            Transform handle = slider.transform.Find("Handle");
            if (background != null && background.TryGetComponent(out Image backgroundImage))
                backgroundImage.raycastTarget = true;
            if (handle != null && handle.TryGetComponent(out Image handleImage))
                handleImage.raycastTarget = true;
        }

        private void ConfigureOptionsPanel()
        {
            if (optionsPanel == null) return;
            if (optionsPanel.TryGetComponent(out Image panelImage))
                panelImage.raycastTarget = true;
            CanvasGroup group = optionsPanel.GetComponent<CanvasGroup>();
            if (group == null) group = optionsPanel.AddComponent<CanvasGroup>();
            group.interactable = true;
            group.blocksRaycasts = true;
        }

        private void ConfigureSaveListScrolling()
        {
            if (saveListContent == null || saveListContent.parent == null ||
                saveListContent.GetComponentInParent<ScrollRect>() != null) return;
            RectTransform contentRect = saveListContent as RectTransform;
            if (contentRect == null) return;
            Transform parent = contentRect.parent;
            GameObject viewportObject = new("SaveListViewport", typeof(RectTransform), typeof(RectMask2D), typeof(ScrollRect));
            viewportObject.transform.SetParent(parent, false);
            RectTransform viewport = viewportObject.GetComponent<RectTransform>();
            viewport.anchorMin = contentRect.anchorMin;
            viewport.anchorMax = contentRect.anchorMax;
            viewport.pivot = contentRect.pivot;
            viewport.anchoredPosition = contentRect.anchoredPosition;
            viewport.sizeDelta = contentRect.sizeDelta;

            contentRect.SetParent(viewport, false);
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = Vector2.zero;
            ContentSizeFitter fitter = contentRect.GetComponent<ContentSizeFitter>();
            if (fitter == null) fitter = contentRect.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect scroll = viewportObject.GetComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = contentRect;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 36f;
        }

        private void BlockUnderlyingMenuInput()
        {
            _blockedMenuSelectables.Clear();
            Selectable[] selectables = FindObjectsOfType<Selectable>(true);
            foreach (Selectable selectable in selectables)
            {
                if (selectable == null || !selectable.gameObject.activeInHierarchy || !selectable.interactable)
                    continue;
                if (optionsPanel != null && selectable.transform.IsChildOf(optionsPanel.transform))
                    continue;

                selectable.interactable = false;
                _blockedMenuSelectables.Add(selectable);
            }
        }

        private void RestoreUnderlyingMenuInput()
        {
            foreach (Selectable selectable in _blockedMenuSelectables)
                if (selectable != null) selectable.interactable = true;
            _blockedMenuSelectables.Clear();
        }
    }
}
