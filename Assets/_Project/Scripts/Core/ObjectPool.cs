using System.Collections.Generic;
using UnityEngine;

namespace DevouringBeast
{
    /// <summary>
    /// 通用对象池 — 减少 GC，适用于子弹、敌人、VFX
    /// </summary>
    public class ObjectPool<T> where T : Component
    {
        private readonly Queue<T> _pool = new();
        private readonly T _prefab;
        private readonly Transform _parent;
        private readonly int _maxSize;

        public int ActiveCount { get; private set; }
        public int InactiveCount => _pool.Count;

        public ObjectPool(T prefab, int initialSize = 10, int maxSize = 100, Transform parent = null)
        {
            _prefab = prefab;
            _parent = parent;
            _maxSize = maxSize;

            for (int i = 0; i < initialSize; i++)
            {
                var obj = CreateInstance();
                _pool.Enqueue(obj);
            }
        }

        public T Get()
        {
            T obj = TakeInactive();
            obj.gameObject.SetActive(true);
            ActiveCount++;
            return obj;
        }

        public T Get(Vector3 position, Quaternion rotation)
        {
            // 池对象必须先定位、后激活。否则 Rigidbody/粒子会在旧位置先运行一帧。
            T obj = TakeInactive();
            obj.transform.SetPositionAndRotation(position, rotation);
            obj.gameObject.SetActive(true);
            ActiveCount++;
            return obj;
        }

        public void Release(T obj)
        {
            if (obj == null) return;

            obj.gameObject.SetActive(false);
            if (_pool.Count < _maxSize)
            {
                _pool.Enqueue(obj);
            }
            else
            {
                Object.Destroy(obj.gameObject);
            }
            ActiveCount--;
        }

        public void Clear()
        {
            while (_pool.Count > 0)
            {
                var obj = _pool.Dequeue();
                if (obj != null) Object.Destroy(obj.gameObject);
            }
            ActiveCount = 0;
        }

        public void Prewarm(int count)
        {
            for (int i = 0; i < count; i++)
            {
                var obj = CreateInstance();
                _pool.Enqueue(obj);
            }
        }

        private T TakeInactive()
        {
            return _pool.Count > 0 ? _pool.Dequeue() : CreateInstance();
        }

        private T CreateInstance()
        {
            var obj = Object.Instantiate(_prefab, _parent);
            obj.name = _prefab.name;
            obj.gameObject.SetActive(false);
            return obj;
        }
    }
}
