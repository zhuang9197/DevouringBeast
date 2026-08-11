using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DevouringBeast
{
    public sealed class LoadSceneController : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private Animator loadingAnimator;
        [SerializeField] private Image loadingImage;
        [SerializeField] private GameObject doneObject;
        [SerializeField, Min(0f)] private float minimumLoadingTime = 0.8f;
        private bool _ready;
        private bool _transitioning;

        private IEnumerator Start()
        {
            AudioManager.EnsureInitialized();
            SaveGameService.Initialize();
            AudioManager.Instance.PlayBgm(BgmTrack.Normal);
            if (doneObject != null) doneObject.SetActive(false);
            if (loadingAnimator != null) loadingAnimator.enabled = true;
            //if (loadingImage != null) loadingImage.enabled = true;

            float endTime = Time.unscaledTime + minimumLoadingTime;
            yield return null;
            while (Time.unscaledTime < endTime) yield return null;

            if (loadingAnimator != null) loadingAnimator.enabled = false;
            //if (loadingImage != null) loadingImage.enabled = false;
            //if (doneObject != null) doneObject.SetActive(true);
            _ready = true;
        }

        private void Update()
        {
            if (!_ready || _transitioning) return;
            bool pressed = (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) ||
                           (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) ||
                           (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame);
            if (pressed) ContinueToMenu();
        }

        public void OnPointerClick(PointerEventData eventData) { ContinueToMenu(); }

        public void ContinueToMenu()
        {
            if (!_ready || _transitioning) return;
            _transitioning = true;
            SceneManager.LoadScene(SceneNames.Menu);
        }
    }
}
