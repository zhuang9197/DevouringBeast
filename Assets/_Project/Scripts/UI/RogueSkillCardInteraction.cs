using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DevouringBeast
{
    public sealed class RogueSkillCardInteraction : MonoBehaviour,
        IPointerDownHandler, IPointerUpHandler, IPointerExitHandler, IPointerClickHandler
    {
        [SerializeField, Min(0.1f)] private float longPressDuration = 0.45f;

        private Action _select;
        private Action _showDescription;
        private Action _hideDescription;
        private Coroutine _longPressRoutine;
        private bool _longPressed;

        public void Initialize(Action select, Action showDescription, Action hideDescription)
        {
            _select = select;
            _showDescription = showDescription;
            _hideDescription = hideDescription;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _longPressed = false;
            CancelLongPress();
            _longPressRoutine = StartCoroutine(LongPressRoutine());
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            CancelLongPress();
            _hideDescription?.Invoke();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            CancelLongPress();
            _hideDescription?.Invoke();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_longPressed) return;
            AudioManager.Instance.PlaySfx(AudioCue.RogueSelect);
            _select?.Invoke();
        }

        private IEnumerator LongPressRoutine()
        {
            yield return new WaitForSecondsRealtime(longPressDuration);
            _longPressRoutine = null;
            _longPressed = true;
            _showDescription?.Invoke();
        }

        private void CancelLongPress()
        {
            if (_longPressRoutine == null) return;
            StopCoroutine(_longPressRoutine);
            _longPressRoutine = null;
        }

        private void OnDisable()
        {
            CancelLongPress();
            _hideDescription?.Invoke();
        }
    }
}
