using UnityEngine;
using UnityEngine.InputSystem;

namespace DevouringBeast
{
    /// <summary>
    /// InputManager — 双重输入源
    /// 键盘/手柄：New Input System (WASD + JK)
    /// 触屏：虚拟摇杆 + UI 按钮（代码驱动）
    /// </summary>
    public class InputManager : MonoBehaviour
    {
        [Header("输入配置")]
        [SerializeField] private InputActionAsset inputActionsAsset;

        [Header("触屏控件")]
        [SerializeField] private VirtualJoystick virtualJoystick;

        [Header("目标")]
        [SerializeField] private PlayerController playerController;
        [SerializeField] private PlayerInhale playerInhale;
        [SerializeField] private PlayerSpit playerSpit;
        [SerializeField] private SwallowContainer swallowContainer;
        private RogueSkillManager _rogueSkills;

        private InputAction _moveAction;
        private InputAction _inhaleSpitAction;
        private InputAction _swallowAction;
        private InputAction _pauseAction;

        // 键盘当前移动值
        private Vector2 _keyboardMove;
        private bool _primaryActionHeld;
        private bool _swallowActionHeld;

        private void Awake()
        {
            if (playerController == null)
                playerController = GetComponent<PlayerController>();
            if (playerInhale == null)
                playerInhale = GetComponent<PlayerInhale>();
            if (playerSpit == null)
                playerSpit = GetComponent<PlayerSpit>();
            if (swallowContainer == null)
                swallowContainer = GetComponent<SwallowContainer>();
            _rogueSkills = GetComponent<RogueSkillManager>();
            GameplayHudController.EnsureFor(gameObject);

            var map = inputActionsAsset.FindActionMap("Player");
            _moveAction = map.FindAction("Move");
            _inhaleSpitAction = map.FindAction("InhaleSpit");
            _swallowAction = map.FindAction("Swallow");
            _pauseAction = map.FindAction("Pause");
        }

        private void OnEnable()
        {
            _moveAction.performed += OnMovePerformed;
            _moveAction.canceled += OnMoveCanceled;
            _inhaleSpitAction.started += OnInhaleSpitStarted;
            _inhaleSpitAction.canceled += OnInhaleSpitCanceled;
            _swallowAction.started += OnSwallowStarted;
            _swallowAction.canceled += OnSwallowCanceled;
            _pauseAction.started += OnPauseStarted;

            _moveAction.Enable();
            _inhaleSpitAction.Enable();
            _swallowAction.Enable();
            _pauseAction.Enable();
        }

        private void OnDisable()
        {
            _moveAction.performed -= OnMovePerformed;
            _moveAction.canceled -= OnMoveCanceled;
            _inhaleSpitAction.started -= OnInhaleSpitStarted;
            _inhaleSpitAction.canceled -= OnInhaleSpitCanceled;
            _swallowAction.started -= OnSwallowStarted;
            _swallowAction.canceled -= OnSwallowCanceled;
            _pauseAction.started -= OnPauseStarted;

            _moveAction.Disable();
            _inhaleSpitAction.Disable();
            _swallowAction.Disable();
            _pauseAction.Disable();
            _primaryActionHeld = false;
            _swallowActionHeld = false;
        }

        private void Update()
        {
            if (!GameManager.Instance.IsPlaying)
            {
                _keyboardMove = Vector2.zero;
                playerController.SetMoveInput(Vector2.zero);
                return;
            }
            // 触屏摇杆输入（每帧轮询，覆盖键盘输入）
            if (virtualJoystick != null && virtualJoystick.Input != Vector2.zero)
            {
                playerController.SetMoveInput(virtualJoystick.Input);
            }
            else if (_keyboardMove != Vector2.zero)
            {
                playerController.SetMoveInput(_keyboardMove);
            }
            else
            {
                // 确保松开时归零（仅当两个输入源都为零）
                if (virtualJoystick == null || virtualJoystick.Input == Vector2.zero)
                    playerController.SetMoveInput(Vector2.zero);
            }
        }

        #region 键盘/手柄回调

        private void OnMovePerformed(InputAction.CallbackContext ctx)
        {
            if (!GameManager.Instance.IsPlaying) return;
            _keyboardMove = ctx.ReadValue<Vector2>();
        }

        private void OnMoveCanceled(InputAction.CallbackContext ctx)
        {
            _keyboardMove = Vector2.zero;
        }

        private void OnInhaleSpitStarted(InputAction.CallbackContext ctx)
        {
            HandleInhaleSpitPress();
        }

private void OnInhaleSpitCanceled(InputAction.CallbackContext ctx)
        {
            HandleInhaleSpitRelease();
        }

        private void OnSwallowStarted(InputAction.CallbackContext ctx)
        {
            HandleSwallowPress();
        }

        private void OnSwallowCanceled(InputAction.CallbackContext ctx)
        {
            HandleSwallowRelease();
        }

        private void OnPauseStarted(InputAction.CallbackContext ctx)
        {
            if (GameManager.Instance.CurrentState == GameState.RogueChoosing || GameManager.Instance.IsGameOver) return;
            if (GameManager.Instance.IsPlaying)
                GameManager.Instance.PauseGame();
            else if (GameManager.Instance.IsPaused)
                GameManager.Instance.ResumeGame();
        }

        #endregion

        #region 触屏按钮公开方法（供 UI Button.onClick 调用）

        /// <summary>吸入/吐出按钮按下（PointerDown 或 Click）</summary>
public void HandleInhaleSpitPress()
        {
            if (!GameManager.Instance.IsPlaying) return;
            if (_primaryActionHeld || _swallowActionHeld) return;
            playerController?.NotifyPlayerActivity();
            if (playerController != null && playerController.IsBeastForm) return;
            _primaryActionHeld = true;
            if (playerInhale != null && playerInhale.IsInhaling) return;

            if (_rogueSkills != null && _rogueSkills.Has(RogueSkillId.FaithAngel))
            {
                playerSpit.StartAngelFire();
                return;
            }

            if (_rogueSkills != null && _rogueSkills.Has(RogueSkillId.FaithPope))
            {
                if (swallowContainer != null && swallowContainer.HasItems)
                {
                    float taughtMass = swallowContainer.Consume();
                    playerSpit.Spit(taughtMass);
                    AudioManager.Instance.PlaySfx(AudioCue.Swallow);
                }
                return;
            }

            if (swallowContainer != null && swallowContainer.HasItems)
            {
                if (playerSpit.CanCharge) playerSpit.StartCharge();
                else playerSpit.Spit();
            }
            else
            {
                playerInhale.StartInhale();
            }
        }

        /// <summary>吸入/吐出按钮松开（PointerUp）</summary>
public void HandleInhaleSpitRelease()
        {
            if (!_primaryActionHeld) return;
            _primaryActionHeld = false;
            playerSpit?.StopAngelFire();
            if (playerSpit != null && playerSpit.IsCharging)
            {
                playerSpit.Spit();
                playerSpit.StopCharge();
            }
            else if (playerInhale != null)
            {
                playerInhale.StopInhale();
            }
        }

        /// <summary>吞噬按钮按下</summary>
public void HandleSwallowPress()
        {
            if (!GameManager.Instance.IsPlaying) return;
            if (_primaryActionHeld || _swallowActionHeld) return;
            playerController?.NotifyPlayerActivity();
            if (playerController != null && playerController.IsBeastForm) return;
            if (playerInhale != null && playerInhale.IsInhaling) return;
            _swallowActionHeld = true;

            if (_rogueSkills != null && _rogueSkills.Has(RogueSkillId.FaithAngel))
            {
                playerSpit.Spit();
                return;
            }

            if (swallowContainer != null && swallowContainer.CanConsume)
            {
                float taughtMass = swallowContainer.Consume();
                AudioManager.Instance.PlaySfx(AudioCue.Swallow);
                if (_rogueSkills != null && _rogueSkills.Has(RogueSkillId.FaithPope)) playerSpit.Spit(taughtMass);
                _rogueSkills?.NotifySwallow();
            }
        }

        public void HandleSwallowRelease()
        {
            _swallowActionHeld = false;
        }

        #endregion
    }
}
