using System;
using UnityEngine;

namespace DevouringBeast
{
    /// <summary>
    /// GameManager — 单例 + 状态机，管理游戏全局状态
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        private static GameManager _instance;
        public static GameManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<GameManager>();
                    if (_instance == null)
                    {
                        var go = new GameObject("[GameManager]");
                        _instance = go.AddComponent<GameManager>();
                    }
                }
                return _instance;
            }
        }

        [field: SerializeField] public GameState CurrentState { get; private set; } = GameState.Menu;

        /// <summary>状态变更事件</summary>
        public event Action<GameState, GameState> OnStateChanged; // (from, to)

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);

            // 场景启动时自动进入 Playing（当前无主菜单 UI）
            CurrentState = GameState.Playing;
        }

        /// <summary>
        /// 切换游戏状态
        /// </summary>
        public void ChangeState(GameState newState)
        {
            if (CurrentState == newState) return;

            // 验证状态转换合法性
            if (!IsValidTransition(CurrentState, newState))
            {
                Debug.LogWarning($"[GameManager] Invalid transition: {CurrentState} -> {newState}");
                return;
            }

            var oldState = CurrentState;
            CurrentState = newState;
            OnStateChanged?.Invoke(oldState, newState);
            Debug.Log($"[GameManager] State: {oldState} -> {newState}");
        }

        private bool IsValidTransition(GameState from, GameState to)
        {
            return (from, to) switch
            {
                (GameState.Menu, GameState.Playing) => true,
                (GameState.Playing, GameState.Paused) => true,
                (GameState.Paused, GameState.Playing) => true,
                (GameState.Playing, GameState.GameOver) => true,
                (GameState.GameOver, GameState.Menu) => true,
                (GameState.Paused, GameState.Menu) => true,
                _ => false
            };
        }

        #region 便捷方法

        public void StartGame() => ChangeState(GameState.Playing);
        public void PauseGame() => ChangeState(GameState.Paused);
        public void ResumeGame() => ChangeState(GameState.Playing);
        public void GameOver() => ChangeState(GameState.GameOver);
        public void ReturnToMenu() => ChangeState(GameState.Menu);

        public bool IsPlaying => CurrentState == GameState.Playing;
        public bool IsPaused => CurrentState == GameState.Paused;
        public bool IsGameOver => CurrentState == GameState.GameOver;

        #endregion
    }
}
