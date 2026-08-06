using UnityEngine;
using UnityEngine.UI;

namespace DevouringBeast
{
    [DisallowMultipleComponent]
    public sealed class GameplayHudController : MonoBehaviour
    {
        private SwallowContainer _container;
        private InputManager _input;
        private PlayerInhale _inhale;
        private PlayerSpit _spit;
        private RogueSkillManager _skills;
        private RogueSkillCatalog _catalog;
        private Image _primaryImage, _swallowImage, _progressFill;
        private Button _primaryButton, _swallowButton;
        private Text _levelText, _progressText;
        private bool _lastHasItems, _lastAngel;
        private bool _displayHasItems;
        private bool _primaryHoldVisualActive;
        private bool _swallowHoldVisualActive;
        private bool _lastSwallowInteractable, _lastPrimaryInteractable, _lastWitchActive;
        private int _lastLevel = int.MinValue, _lastMass = int.MinValue, _lastRequiredMass = int.MinValue;
        private float _lastProgress = -1f, _lastWitchProgress = -1f;
        private GameObject _witchPanel;
        private Image _witchFill;
        private Text _witchText;

        public static GameplayHudController EnsureFor(GameObject player)
        {
            GameplayHudController existing = player.GetComponent<GameplayHudController>();
            return existing != null ? existing : player.AddComponent<GameplayHudController>();
        }

        private void Awake()
        {
            _container = GetComponent<SwallowContainer>();
            _input = GetComponent<InputManager>();
            _inhale = GetComponent<PlayerInhale>();
            _spit = GetComponent<PlayerSpit>();
            _skills = GetComponent<RogueSkillManager>();
            _catalog = Resources.Load<RogueSkillCatalog>("Rogue/RogueSkillCatalog");
        }

        private void Start()
        {
            BindButtons();
            BuildLevelProgress();
            BuildWitchProgress();
            Refresh(true);
        }

        private void BuildWitchProgress()
        {
            Canvas canvas=FindFirstObjectByType<Canvas>();
            if(canvas==null||GameObject.Find("WitchProgressPanel")!=null)return;
            _witchPanel=new GameObject("WitchProgressPanel",typeof(RectTransform));
            _witchPanel.transform.SetParent(canvas.transform,false);
            RectTransform panelRect=_witchPanel.GetComponent<RectTransform>();
            panelRect.anchorMin=panelRect.anchorMax=new Vector2(0.5f,0f); panelRect.pivot=new Vector2(0.5f,0f);
            panelRect.anchoredPosition=new Vector2(0f,24f); panelRect.sizeDelta=new Vector2(260f,42f);
            GameObject bg=CreateImage("ProgressBar",_witchPanel.transform,_catalog?.progressBar); Stretch(bg.GetComponent<RectTransform>());
            GameObject fill=CreateImage("ProgressFill",bg.transform,_catalog?.progressFill);
            SetRect(fill.GetComponent<RectTransform>(),new Vector2(0.035f,0.25f),new Vector2(0.965f,0.75f));
            _witchFill=fill.GetComponent<Image>(); _witchFill.type=Image.Type.Filled;
            _witchFill.fillMethod=Image.FillMethod.Horizontal; _witchFill.fillOrigin=0;
            _witchText=CreateText("Label",_witchPanel.transform,16,TextAnchor.MiddleCenter);
            Stretch(_witchText.rectTransform); _witchText.text="野兽吞吞";
            _witchPanel.SetActive(false);
        }

        private void Update() => Refresh(false);

        private void BindButtons()
        {
            InputManager input=GetComponent<InputManager>();
            GameObject primary = GameObject.Find("Btn_InhaleSpit");
            GameObject swallow = GameObject.Find("Btn_Swallow");
            if (primary != null)
            {
                _primaryImage=primary.GetComponent<Image>(); _primaryButton=primary.GetComponent<Button>(); HideLegacyLabels(primary);
                ConfigureButtonVisual(_primaryImage, _primaryButton);
                GameplayActionButton action=primary.GetComponent<GameplayActionButton>();
                if(action==null) action=primary.AddComponent<GameplayActionButton>(); action.Configure(input,true);
            }
            if (swallow != null)
            {
                _swallowImage=swallow.GetComponent<Image>(); _swallowButton=swallow.GetComponent<Button>(); HideLegacyLabels(swallow);
                ConfigureButtonVisual(_swallowImage, _swallowButton);
                GameplayActionButton action=swallow.GetComponent<GameplayActionButton>();
                if(action==null) action=swallow.AddComponent<GameplayActionButton>(); action.Configure(input,false);
            }
        }

        private static void HideLegacyLabels(GameObject button)
        {
            foreach (Text text in button.GetComponentsInChildren<Text>(true)) text.enabled=false;
        }

        private static void ConfigureButtonVisual(Image image, Button button)
        {
            if (image != null) image.color = Color.white;
            if (button == null) return;

            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.96f, 0.72f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(0.82f, 0.9f, 1f, 1f);
            colors.disabledColor = new Color(0.62f, 0.62f, 0.68f, 0.75f);
            colors.colorMultiplier = 1.2f;
            colors.fadeDuration = 0.06f;
            button.colors = colors;
        }

        private void BuildLevelProgress()
        {
            Canvas canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null || GameObject.Find("LevelProgressPanel") != null) return;
            GameObject panel = new("LevelProgressPanel", typeof(RectTransform)); panel.transform.SetParent(canvas.transform,false);
            RectTransform panelRect=panel.GetComponent<RectTransform>();
            panelRect.anchorMin=panelRect.anchorMax=new Vector2(0f,1f); panelRect.pivot=new Vector2(0f,1f);
            panelRect.anchoredPosition=new Vector2(18f,-72f); panelRect.sizeDelta=new Vector2(220f,42f);

            GameObject bg=CreateImage("ProgressBar",panel.transform,_catalog?.progressBar);
            RectTransform bgRect=bg.GetComponent<RectTransform>(); Stretch(bgRect);
            GameObject fill=CreateImage("ProgressFill",bg.transform,_catalog?.progressFill);
            RectTransform fillRect=fill.GetComponent<RectTransform>();
            fillRect.anchorMin=new Vector2(0.035f,0.25f); fillRect.anchorMax=new Vector2(0.965f,0.75f);
            fillRect.offsetMin=fillRect.offsetMax=Vector2.zero;
            _progressFill=fill.GetComponent<Image>(); _progressFill.type=Image.Type.Filled;
            _progressFill.fillMethod=Image.FillMethod.Horizontal; _progressFill.fillOrigin=0;

            _levelText=CreateText("Level",panel.transform,14,TextAnchor.MiddleLeft);
            SetRect(_levelText.rectTransform,new Vector2(0.04f,0.52f),new Vector2(0.42f,0.98f));
            _progressText=CreateText("Progress",panel.transform,11,TextAnchor.MiddleRight);
            SetRect(_progressText.rectTransform,new Vector2(0.45f,0.52f),new Vector2(0.95f,0.98f));
        }

        private void Refresh(bool force)
        {
            if (_container == null || _catalog == null) return;
            bool hasItems=_container.HasItems;
            bool inhaling=_inhale != null && _inhale.IsInhaling;
            bool primaryHeld = _input != null && _input.IsPrimaryActionHeld;
            if (force || !primaryHeld) _displayHasItems=hasItems;
            bool angel=_skills != null && _skills.Has(RogueSkillId.FaithAngel);
            bool witch=_skills != null && _skills.Has(RogueSkillId.FaithWitch);
            if (force || _displayHasItems!=_lastHasItems || angel!=_lastAngel)
            {
                if (_primaryImage != null)
                    _primaryImage.sprite = angel ? _catalog.spitButton : _displayHasItems ? _catalog.spitButton : _catalog.suckButton;
                if (_swallowImage != null) _swallowImage.sprite = angel ? _catalog.spitButton : _catalog.swallowButton;
                _lastHasItems=_displayHasItems; _lastAngel=angel;
            }
            bool playing=GameManager.Instance.IsPlaying;
            bool swallowInteractable=playing && !inhaling && (angel || hasItems);
            if (_swallowButton != null && (force || swallowInteractable!=_lastSwallowInteractable))
                _swallowButton.interactable=swallowInteractable;
            if (_primaryButton != null && (force || playing!=_lastPrimaryInteractable))
                _primaryButton.interactable=playing;
            _lastSwallowInteractable=swallowInteractable;
            _lastPrimaryInteractable=playing;

            float pct=_container.RequiredMass>0f ? Mathf.Clamp01(_container.CurrentMass/_container.RequiredMass) : 0f;
            if (_progressFill != null && (force || !Mathf.Approximately(pct,_lastProgress)))
                _progressFill.fillAmount=pct;
            _lastProgress=pct;

            int level=_container.CurrentLevel;
            int mass=Mathf.FloorToInt(_container.CurrentMass);
            int requiredMass=Mathf.CeilToInt(_container.RequiredMass);
            if (_levelText != null && (force || level!=_lastLevel))
                _levelText.text="等级 " + level;
            if (_progressText != null && (force || mass!=_lastMass || requiredMass!=_lastRequiredMass))
                _progressText.text=mass+" / "+requiredMass;
            _lastLevel=level;
            _lastMass=mass;
            _lastRequiredMass=requiredMass;

            if (_witchPanel != null && (force || witch!=_lastWitchActive))
                _witchPanel.SetActive(witch);
            _lastWitchActive=witch;
            if (_witchFill != null && _skills != null)
            {
                float witchProgress=_skills.WitchProgressNormalized;
                if (force || !Mathf.Approximately(witchProgress,_lastWitchProgress))
                    _witchFill.fillAmount=witchProgress;
                _lastWitchProgress=witchProgress;
            }
            RefreshHoldVisuals(inhaling);
        }

        private void RefreshHoldVisuals(bool inhaling)
        {
            bool charging = _spit != null && _spit.IsCharging;
            bool swallowCharge = charging && _input != null && _input.IsSwallowActionHeld;
            bool maxed = (inhaling && _inhale.IsSuctionMaxed) || (charging && _spit.IsChargeMaxed);
            RefreshHoldImage(_primaryImage, inhaling || (charging && !swallowCharge), maxed,
                ref _primaryHoldVisualActive);
            RefreshHoldImage(_swallowImage, swallowCharge, maxed, ref _swallowHoldVisualActive);
        }

        private static void RefreshHoldImage(Image image, bool active, bool maxed, ref bool wasActive)
        {
            if (image == null) return;
            if (!active)
            {
                if (wasActive) image.color = Color.white;
                wasActive = false;
                return;
            }

            wasActive = true;
            if (maxed)
            {
                image.color = new Color(1f, 0.16f, 0.16f, 1f);
                return;
            }

            float pulse = (Mathf.Sin(Time.unscaledTime * Mathf.PI * 6f) + 1f) * 0.5f;
            float redStrength = Mathf.Lerp(0.25f, 0.82f, pulse);
            image.color = Color.Lerp(Color.white, new Color(1f, 0.08f, 0.08f, 1f), redStrength);
        }

        private static GameObject CreateImage(string name,Transform parent,Sprite sprite)
        { GameObject go=new(name,typeof(RectTransform),typeof(Image)); go.transform.SetParent(parent,false); Image image=go.GetComponent<Image>(); image.sprite=sprite; image.type=Image.Type.Sliced; image.raycastTarget=false; return go; }
        private static Text CreateText(string name,Transform parent,int size,TextAnchor anchor)
        { GameObject go=new(name,typeof(RectTransform),typeof(Text)); go.transform.SetParent(parent,false); Text text=go.GetComponent<Text>(); text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize=size; text.fontStyle=FontStyle.Bold; text.alignment=anchor; text.color=Color.white; text.raycastTarget=false; return text; }
        private static void Stretch(RectTransform rect) => SetRect(rect,Vector2.zero,Vector2.one);
        private static void SetRect(RectTransform rect,Vector2 min,Vector2 max) { rect.anchorMin=min; rect.anchorMax=max; rect.offsetMin=rect.offsetMax=Vector2.zero; }
    }
}
