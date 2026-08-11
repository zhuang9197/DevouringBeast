using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using EnhancedTouch = UnityEngine.InputSystem.EnhancedTouch;

namespace DevouringBeast
{
    public sealed class VirtualJoystick : MonoBehaviour, IDragHandler, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField, Min(20f)] private float maxRadius = 120f;
        [SerializeField, Min(0f)] private float deadZone = 20f;
        [SerializeField, Min(0f)] private float activationDragDistance = 12f;

        private RectTransform _rect;
        private Image _image;
        private readonly Vector3[] _worldCorners = new Vector3[4];
        private Vector2 _startScreenPosition;
        private EnhancedTouch.Finger _activeFinger;
        private bool _ownsEnhancedTouch;
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
            if (Touchscreen.current != null)
            {
                var touches = EnhancedTouch.Touch.activeTouches;
                if (!_tracking)
                {
                    foreach (EnhancedTouch.Touch touch in touches)
                    {
                        if (touch.phase != UnityEngine.InputSystem.TouchPhase.Began) continue;
                        Begin(touch.screenPosition, touch.finger);
                        if (_tracking) break;
                    }
                }
                else
                {
                    bool foundTrackedTouch = false;
                    foreach (EnhancedTouch.Touch touch in touches)
                    {
                        if (touch.finger != _activeFinger) continue;
                        foundTrackedTouch = true;
                        if (touch.phase == UnityEngine.InputSystem.TouchPhase.Ended ||
                            touch.phase == UnityEngine.InputSystem.TouchPhase.Canceled)
                            Cancel();
                        else
                            Move(touch.screenPosition);
                        break;
                    }

                    if (_activeFinger == null || !_activeFinger.isActive || !foundTrackedTouch) Cancel();
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

        private void Begin(Vector2 screenPosition, EnhancedTouch.Finger finger = null)
        {
            if (screenPosition.x > Screen.width * 0.5f) return;
            _tracking=true; _activeFinger=finger; _visible=false; Input=Vector2.zero;
            _startScreenPosition=screenPosition;
        }

        private void Move(Vector2 screenPosition)
        {
            Vector2 delta=screenPosition-_startScreenPosition;
            if (!_visible && delta.magnitude < activationDragDistance) return;
            if (!_visible)
            {
                // Keep the visual on-screen without changing the real touch origin used for movement.
                _rect.position=ClampVisualPosition(_startScreenPosition);
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

        private Vector2 ClampVisualPosition(Vector2 screenPosition)
        {
            _rect.GetWorldCorners(_worldCorners);
            float visualRadius = Vector2.Distance(_worldCorners[0], _worldCorners[2]) * 0.5f;
            Rect safeArea = Screen.safeArea;

            // The gameplay camera is letterboxed to the fixed 16:9 room. Screen-space
            // overlay pixels outside its pixelRect are not cleared by the camera, so a
            // joystick rendered there can leave a stale image after it is hidden. Keep
            // the visual inside the area that is actually redrawn while still accepting
            // touch input from the whole left half of the physical screen.
            Camera gameplayCamera = Camera.main;
            if (gameplayCamera != null && gameplayCamera.rect != new Rect(0f, 0f, 1f, 1f))
                safeArea = Intersect(safeArea, gameplayCamera.pixelRect);

            return new Vector2(
                ClampAxis(screenPosition.x, safeArea.xMin + visualRadius, safeArea.xMax - visualRadius),
                ClampAxis(screenPosition.y, safeArea.yMin + visualRadius, safeArea.yMax - visualRadius));
        }

        private static Rect Intersect(Rect first, Rect second)
        {
            float xMin = Mathf.Max(first.xMin, second.xMin);
            float yMin = Mathf.Max(first.yMin, second.yMin);
            float xMax = Mathf.Min(first.xMax, second.xMax);
            float yMax = Mathf.Min(first.yMax, second.yMax);
            return Rect.MinMaxRect(xMin, yMin, Mathf.Max(xMin, xMax), Mathf.Max(yMin, yMax));
        }

        private static float ClampAxis(float value, float min, float max)
        {
            return min <= max ? Mathf.Clamp(value, min, max) : (min + max) * 0.5f;
        }

        private void Cancel()
        {
            _tracking=false; _activeFinger=null; Input=Vector2.zero; SetVisible(false);
            if (_rect != null) _rect.localRotation=Quaternion.identity;
        }

        private void SetVisible(bool value)
        {
            _visible=value;
            if (_image != null) _image.enabled=value;
        }

        private void OnEnable()
        {
            if (!EnhancedTouch.EnhancedTouchSupport.enabled)
            {
                EnhancedTouch.EnhancedTouchSupport.Enable();
                _ownsEnhancedTouch = true;
            }
            EnhancedTouch.Touch.onFingerUp += HandleFingerUp;
        }

        private void OnDisable()
        {
            EnhancedTouch.Touch.onFingerUp -= HandleFingerUp;
            Cancel();
            if (!_ownsEnhancedTouch) return;
            EnhancedTouch.EnhancedTouchSupport.Disable();
            _ownsEnhancedTouch = false;
        }

        private void HandleFingerUp(EnhancedTouch.Finger finger)
        {
            if (finger == _activeFinger) Cancel();
        }

        private void OnApplicationFocus(bool hasFocus) { if (!hasFocus) Cancel(); }
        private void OnApplicationPause(bool paused) { if (paused) Cancel(); }

        public void OnPointerDown(PointerEventData eventData) => Begin(eventData.position);
        public void OnDrag(PointerEventData eventData) { if (_tracking) Move(eventData.position); }
        public void OnPointerUp(PointerEventData eventData) => Cancel();
    }
}
