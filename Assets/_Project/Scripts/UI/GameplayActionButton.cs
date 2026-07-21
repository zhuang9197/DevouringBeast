using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DevouringBeast
{
    [DisallowMultipleComponent]
    public sealed class GameplayActionButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        private InputManager _input;
        private Button _button;
        private bool _primary;
        private bool _pressed;

        public void Configure(InputManager input, bool primary)
        {
            _input=input;
            _primary=primary;
            _button=GetComponent<Button>();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_input == null || (_button != null && !_button.interactable)) return;
            _pressed=true;
            if (_primary) _input.HandleInhaleSpitPress();
            else _input.HandleSwallowPress();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!_pressed) return;
            _pressed=false;
            if (_primary) _input.HandleInhaleSpitRelease();
            else _input.HandleSwallowRelease();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_pressed)
            {
                if (_primary) _input.HandleInhaleSpitRelease();
                else _input.HandleSwallowRelease();
            }
            _pressed=false;
        }
    }
}
