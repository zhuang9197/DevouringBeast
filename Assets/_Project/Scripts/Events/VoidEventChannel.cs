using UnityEngine;
using UnityEngine.Events;

namespace DevouringBeast
{
    /// <summary>
    /// 通用事件通道 (ScriptableObject) — 松耦合事件系统
    /// 使用方法：Create Asset → 在发送方 RaiseEvent()，在接收方 AddListener()
    /// </summary>
    [CreateAssetMenu(menuName = "DevouringBeast/Events/Void Event Channel", fileName = "EventChannel")]
    public class VoidEventChannel : ScriptableObject
    {
        private UnityAction _onEvent;

        public void RaiseEvent()
        {
            _onEvent?.Invoke();
        }

        public void AddListener(UnityAction action)
        {
            _onEvent += action;
        }

        public void RemoveListener(UnityAction action)
        {
            _onEvent -= action;
        }
    }
}
