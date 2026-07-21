using UnityEngine.EventSystems;
using UnityEngine;
using UnityEngine.UI;

namespace DevouringBeast
{
    [RequireComponent(typeof(Button))]
    public sealed class UIButtonAudio : MonoBehaviour, IPointerDownHandler
    {public void OnPointerDown(PointerEventData eventData)
        {
            AudioManager.Instance.PlaySfx(AudioCue.UiClick);
        }
    }
}
