using UnityEngine;
using UnityEngine.Events;

namespace DevouringBeast
{
    /// <summary>
    /// 泛型事件通道 (ScriptableObject)
    /// </summary>
    public abstract class BaseEventChannel<T> : ScriptableObject
    {
        private UnityAction<T> _onEvent;

        public void RaiseEvent(T value)
        {
            _onEvent?.Invoke(value);
        }

        public void AddListener(UnityAction<T> action)
        {
            _onEvent += action;
        }

        public void RemoveListener(UnityAction<T> action)
        {
            _onEvent -= action;
        }
    }

    /// <summary>Int 事件通道</summary>
    [CreateAssetMenu(menuName = "DevouringBeast/Events/Int Event Channel", fileName = "IntEventChannel")]
    public class IntEventChannel : BaseEventChannel<int> { }

    /// <summary>Float 事件通道</summary>
    [CreateAssetMenu(menuName = "DevouringBeast/Events/Float Event Channel", fileName = "FloatEventChannel")]
    public class FloatEventChannel : BaseEventChannel<float> { }

    /// <summary>String 事件通道</summary>
    [CreateAssetMenu(menuName = "DevouringBeast/Events/String Event Channel", fileName = "StringEventChannel")]
    public class StringEventChannel : BaseEventChannel<string> { }

    /// <summary>Bool 事件通道</summary>
    [CreateAssetMenu(menuName = "DevouringBeast/Events/Bool Event Channel", fileName = "BoolEventChannel")]
    public class BoolEventChannel : BaseEventChannel<bool> { }

    /// <summary>GameObject 事件通道</summary>
    [CreateAssetMenu(menuName = "DevouringBeast/Events/GameObject Event Channel", fileName = "GOEventChannel")]
    public class GameObjectEventChannel : BaseEventChannel<GameObject> { }
}
