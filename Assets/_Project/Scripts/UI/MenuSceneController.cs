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

        private void Start()
        {
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
        }

        public void OnNewGame()
        {
            _newGameMode = true;
            ShowSaveList("选择新游戏存档位");
        }

        public void OnContinueGame()
        {
            _newGameMode = false;
            ShowSaveList("选择要继续的存档");
        }

        public void OnOptions()
        {
            HideAllPanels();
            if (optionsPanel == null) return;
            optionsPanel.transform.SetAsLastSibling();
            optionsPanel.SetActive(true);
            BlockUnderlyingMenuInput();
        }

        public void OnClosePanels() { HideAllPanels(); }

        public void OnContinueSelected()
        {
            SaveSlotData data = SaveGameService.GetSlot(_selectedSlot);
            if (data == null) return;
            SaveGameService.SetActiveSlot(_selectedSlot);
            StartGameScene();
        }

        public void OnDeleteSelected()
        {
            SaveSlotData data = SaveGameService.GetSlot(_selectedSlot);
            if (data == null) return;
            _confirmDelete = true;
            if (confirmText != null) confirmText.text = "确认删除 " + data.displayName + "？";
            if (confirmPanel != null) confirmPanel.SetActive(true);
        }

        public void OnConfirmYes()
        {
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
            for (int i = 0; i < SaveGameService.SlotCount; i++)
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
