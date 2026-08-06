using System;
using UnityEngine.SceneManagement;
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
            Application.targetFrameRate = 60;
            int inhaleableLayer = LayerMask.NameToLayer("inhaleableLayer");
            if (inhaleableLayer >= 0)
                Physics2D.IgnoreLayerCollision(inhaleableLayer, inhaleableLayer, true);
#if UNITY_EDITOR
            // Keep editor play mode responsive while profiling or using another window.
            Application.runInBackground = true;
#endif
#if UNITY_ANDROID
            QualitySettings.vSyncCount = 0;
#endif
            SceneManager.sceneLoaded += OnSceneLoaded;
            ApplySceneState(SceneManager.GetActiveScene());
        }

private void OnDestroy()
        {
            if (_instance != this) return;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            _instance = null;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Time.timeScale = 1f;
            AudioManager.Instance.SetSfxSuppressed(false);
            ApplySceneState(scene);
            FloorMapManager.EnsureForScene(scene);
        }

        private void ApplySceneState(Scene scene)
        {
            GameState target = scene.name == SceneNames.Game ? GameState.Playing : GameState.Menu;
            if (CurrentState == target) return;

            GameState previous = CurrentState;
            CurrentState = target;
            OnStateChanged?.Invoke(previous, target);
            Debug.Log($"[GameManager] Scene state: {previous} -> {target}");
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
                (GameState.Playing, GameState.RogueChoosing) => true,
                (GameState.RogueChoosing, GameState.Playing) => true,
                (GameState.Paused, GameState.Playing) => true,
                (GameState.Playing, GameState.GameOver) => true,
                (GameState.RogueChoosing, GameState.GameOver) => true,
                (GameState.GameOver, GameState.Menu) => true,
                (GameState.Paused, GameState.Menu) => true,
                _ => false
            };
        }

        #region 便捷方法

        public void StartGame() => ChangeState(GameState.Playing);
        public void PauseGame()
        {
            if (!IsPlaying) return;
            ChangeState(GameState.Paused);
            Time.timeScale = 0f;
        }

        public void ResumeGame()
        {
            if (!IsPaused) return;
            Time.timeScale = WaveManager.Instance != null ? WaveManager.Instance.GameplayTimeScale : 1f;
            ChangeState(GameState.Playing);
        }
        public void GameOver() => ChangeState(GameState.GameOver);
        public void ReturnToMenu() => ChangeState(GameState.Menu);

        public bool IsPlaying => CurrentState == GameState.Playing;
        public bool IsPaused => CurrentState == GameState.Paused;
        public bool IsGameOver => CurrentState == GameState.GameOver;

        public void EnterRogueSelection()
        {
            if (!IsPlaying) return;
            ChangeState(GameState.RogueChoosing);
            Time.timeScale = 0f;
            AudioManager.Instance.SetSfxSuppressed(true);
        }

        public void ExitRogueSelection()
        {
            if (CurrentState != GameState.RogueChoosing) return;
            Time.timeScale = WaveManager.Instance != null ? WaveManager.Instance.GameplayTimeScale : 1f;
            AudioManager.Instance.SetSfxSuppressed(false);
            ChangeState(GameState.Playing);
        }

        public void HandlePlayerDeath()
        {
            if (IsGameOver) return;
            ChangeState(GameState.GameOver);
            Time.timeScale = 0f;
            AudioManager.Instance.EnterGameOverAudio();
            GameOverUI.Show();
        }

        public void RestartGame()
        {
            Time.timeScale = 1f;
            AudioManager.Instance.SetSfxSuppressed(false);
            SaveGameService.ResetActiveRun();
            AudioManager.Instance.RestartCurrentBgm();
            SceneManager.LoadScene(SceneNames.Game);
        }

        public void ReturnToMainMenu()
        {
            Time.timeScale = 1f;
            AudioManager.Instance.SetSfxSuppressed(false);
            SceneManager.LoadScene(SceneNames.Menu);
        }

        #endregion
    }
}
