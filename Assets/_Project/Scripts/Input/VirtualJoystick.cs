using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DevouringBeast
{
    public sealed class VirtualJoystick : MonoBehaviour, IDragHandler, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField, Min(20f)] private float maxRadius = 120f;
        [SerializeField, Min(0f)] private float deadZone = 20f;
        [SerializeField, Min(0f)] private float activationDragDistance = 12f;

        private RectTransform _rect;
        private Image _image;
        private Vector2 _startScreenPosition;
        private bool _tracking;
        private bool _visible;

        public Vector2 Input { get; private set; }
        public bool IsVisible => _visible;
        public Vector2 ScreenPosition => _rect != null ? (Vector2)_rect.position : Vector2.zero;

        private void Awake()
        {
            _rect = GetComponent<RectTransform>();
            _image = GetComponent<Image>();
            RogueSkillCatalog catalog = Resources.Load<RogueSkillCatalog>("Rogue/RogueSkillCatalog");
            if (_image != null)
            {
                if (catalog != null) _image.sprite=catalog.joystick;
                _image.preserveAspect=true;
                _image.color=Color.white;
                _image.raycastTarget=false;
            }
            if (_rect.sizeDelta.sqrMagnitude < 100f) _rect.sizeDelta = new Vector2(150f,150f);
            SetVisible(false);
        }

        private void Update()
        {
            if (!GameManager.Instance.IsPlaying) { Cancel(); return; }
            bool handledTouch = false;
            if (Touchscreen.current != null)
            {
                var touch=Touchscreen.current.primaryTouch;
                if (touch.press.wasPressedThisFrame) { Begin(touch.position.ReadValue()); handledTouch=true; }
                if (_tracking && touch.press.isPressed) { Move(touch.position.ReadValue()); handledTouch=true; }
                if (_tracking && touch.press.wasReleasedThisFrame) { Cancel(); handledTouch=true; }
                if (handledTouch) return;
            }
#if UNITY_EDITOR || UNITY_STANDALONE
            if (Mouse.current != null)
            {
                if (Mouse.current.leftButton.wasPressedThisFrame) Begin(Mouse.current.position.ReadValue());
                if (_tracking && Mouse.current.leftButton.isPressed) Move(Mouse.current.position.ReadValue());
                if (_tracking && Mouse.current.leftButton.wasReleasedThisFrame) Cancel();
            }
#endif
        }

        private void Begin(Vector2 screenPosition)
        {
            if (screenPosition.x > Screen.width * 0.5f) return;
            _tracking=true; _visible=false; Input=Vector2.zero; _startScreenPosition=screenPosition;
        }

        private void Move(Vector2 screenPosition)
        {
            Vector2 delta=screenPosition-_startScreenPosition;
            if (!_visible && delta.magnitude < activationDragDistance) return;
            if (!_visible)
            {
                _rect.position=_startScreenPosition;
                SetVisible(true);
            }
            float magnitude=Mathf.Min(delta.magnitude,maxRadius);
            Input=magnitude<deadZone ? Vector2.zero : delta.normalized*Mathf.InverseLerp(deadZone,maxRadius,magnitude);
            if (delta.sqrMagnitude>0.01f)
            {
                float angle=Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg-90f;
                _rect.localRotation=Quaternion.Euler(0f,0f,angle);
            }
        }

        private void Cancel()
        {
            _tracking=false; Input=Vector2.zero; SetVisible(false); _rect.localRotation=Quaternion.identity;
        }

        private void SetVisible(bool value)
        {
            _visible=value;
            if (_image != null) _image.enabled=value;
        }

        public void OnPointerDown(PointerEventData eventData) => Begin(eventData.position);
        public void OnDrag(PointerEventData eventData) { if (_tracking) Move(eventData.position); }
        public void OnPointerUp(PointerEventData eventData) => Cancel();
    }
}
