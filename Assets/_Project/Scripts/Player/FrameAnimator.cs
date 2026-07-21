using UnityEngine;

namespace DevouringBeast
{
    /// <summary>
    /// 代码驱动帧动画播放器 — 替代 Animator，精确控制逐帧切换
    /// 支持：单段播放、单段循环、先播一段再循环另一段
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class FrameAnimator : MonoBehaviour
    {
        [SerializeField] private float frameRate = 12f;

        private SpriteRenderer _renderer;
        private Sprite[] _frames;

        // 当前播放范围
        private int _startFrame;
        private int _endFrame;
        private int _currentFrame;
        private float _timer;
        private bool _isPlaying;

        // 循环范围（当 _hasLoopRange=true 时，主段播完后跳到 loopStart~loopEnd 循环）
        private bool _hasLoopRange;
        private int _loopStart;
        private int _loopEnd;

        // 去重：记录当前播放的动画标识，避免每帧重复调用
        private int _lastAnimHash;

        public bool IsPlaying => _isPlaying;
        public int CurrentFrame => _currentFrame;
        public int StartFrame => _startFrame;
        public int EndFrame => _endFrame;
        public bool HasPendingLoopRange => _hasLoopRange;

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
        }

        /// <summary>
        /// 播放指定范围内的帧
        /// </summary>
        /// <param name="loop">true=范围内循环, false=播完停住</param>
        public void Play(Sprite[] frames, int startFrame, int endFrame, bool loop)
        {
            if (frames == null || frames.Length == 0) return;

            // 去重检查
            int hash = frames.GetHashCode() ^ (startFrame << 8) ^ (endFrame << 16) ^ (loop ? 1 : 0) ^ 0x40000000;
            if (hash == _lastAnimHash && _isPlaying) return;
            _lastAnimHash = hash;

            _frames = frames;
            _startFrame = Mathf.Clamp(startFrame, 0, frames.Length - 1);
            _endFrame = Mathf.Clamp(endFrame, _startFrame, frames.Length - 1);
            _hasLoopRange = false; // 纯 loop 或纯单次
            _loop = loop;          // 存到下面定义的字段
            _currentFrame = _startFrame;
            _timer = 0f;
            _isPlaying = true;
            ApplyFrame();
        }

        private bool _loop;

        /// <summary>
        /// 先播放 introStart~introEnd（不循环），播完后跳到 loopStart~loopEnd 循环
        /// 用于"张嘴→张大后微动循环"场景
        /// </summary>
        public void PlayThenLoop(Sprite[] frames, int introStart, int introEnd, int loopStart, int loopEnd)
        {
            if (frames == null || frames.Length == 0) return;

            // 去重检查
            int hash = frames.GetHashCode() ^ (introStart << 8) ^ (introEnd << 16) ^ (loopStart << 24) ^ loopEnd;
            if (hash == _lastAnimHash && _isPlaying) return;
            _lastAnimHash = hash;

            _frames = frames;
            _startFrame = Mathf.Clamp(introStart, 0, frames.Length - 1);
            _endFrame = Mathf.Clamp(introEnd, _startFrame, frames.Length - 1);
            _loopStart = Mathf.Clamp(loopStart, 0, frames.Length - 1);
            _loopEnd = Mathf.Clamp(loopEnd, _loopStart, frames.Length - 1);
            _hasLoopRange = true;
            _currentFrame = _startFrame;
            _timer = 0f;
            _isPlaying = true;
            ApplyFrame();
        }

        /// <summary>
        /// 停止播放，显示静态精灵
        /// </summary>
        public void Stop(Sprite staticSprite)
        {
            _isPlaying = false;
            _frames = null;
            _hasLoopRange = false;
            _lastAnimHash = 0;
            if (staticSprite != null)
                _renderer.sprite = staticSprite;
        }

        private void Update()
        {
            if (!_isPlaying || _frames == null) return;

            _timer += Time.deltaTime;
            float interval = 1f / frameRate;

            while (_timer >= interval)
            {
                _timer -= interval;
                _currentFrame++;

                if (_currentFrame > _endFrame)
                {
                    if (_hasLoopRange)
                    {
                        // intro 段播完 → 跳到 loop 段循环
                        _startFrame = _loopStart;
                        _endFrame = _loopEnd;
                        _currentFrame = _loopStart;
                        _hasLoopRange = false; // 进入纯循环模式
                        _loop = true;
                    }
                    else if (_loop)
                    {
                        _currentFrame = _startFrame;
                    }
                    else
                    {
                        // 停在最后一帧
                        _currentFrame = _endFrame;
                        _isPlaying = false;
                        ApplyFrame();
                        return;
                    }
                }

                ApplyFrame();
            }
        }

        private void ApplyFrame()
        {
            if (_frames != null && _currentFrame >= 0 && _currentFrame < _frames.Length)
                _renderer.sprite = _frames[_currentFrame];
        }
    }
}
