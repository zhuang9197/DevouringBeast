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
        private int _activeTouchId = -1;
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
            Touchscreen touchscreen = Touchscreen.current;
            if (touchscreen != null)
            {
                if (!_tracking)
                {
                    foreach (var touch in touchscreen.touches)
                    {
                        if (!touch.press.wasPressedThisFrame) continue;
                        Begin(touch.position.ReadValue(), touch.touchId.ReadValue());
                        if (_tracking) break;
                    }
                }
                else
                {
                    bool foundTrackedTouch = false;
                    foreach (var touch in touchscreen.touches)
                    {
                        if (touch.touchId.ReadValue() != _activeTouchId) continue;
                        foundTrackedTouch = true;
                        if (touch.press.isPressed) Move(touch.position.ReadValue());
                        else Cancel();
                        break;
                    }

                    // Android can cancel a touch when it leaves the game view or loses focus.
                    // In that case no PointerUp/wasReleasedThisFrame event is guaranteed.
                    if (!foundTrackedTouch) Cancel();
                }
                return;
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

        private void Begin(Vector2 screenPosition, int touchId = -1)
        {
            if (screenPosition.x > Screen.width * 0.5f) return;
            _tracking=true; _activeTouchId=touchId; _visible=false; Input=Vector2.zero;
            _startScreenPosition=screenPosition;
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
            _tracking=false; _activeTouchId=-1; Input=Vector2.zero; SetVisible(false);
            if (_rect != null) _rect.localRotation=Quaternion.identity;
        }

        private void SetVisible(bool value)
        {
            _visible=value;
            if (_image != null) _image.enabled=value;
        }

        private void OnDisable() => Cancel();
        private void OnApplicationFocus(bool hasFocus) { if (!hasFocus) Cancel(); }
        private void OnApplicationPause(bool paused) { if (paused) Cancel(); }

        public void OnPointerDown(PointerEventData eventData) => Begin(eventData.position, eventData.pointerId);
        public void OnDrag(PointerEventData eventData) { if (_tracking) Move(eventData.position); }
        public void OnPointerUp(PointerEventData eventData) => Cancel();
    }
}
