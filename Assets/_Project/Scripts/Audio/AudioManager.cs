using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DevouringBeast
{
    public enum BgmTrack { Normal, Battle, Boss }
    public enum AudioCue
    {
        Split, BigSplit, Charged, Hurt, Die, Idle, Run, Walk, Suck, Swallow, Roll, BeastHit,
        BossDie, EnemyDie, Rebound, MeatMountainLand, BabyCry, SatanLaugh, Dash,
        Hit, Bomb, LevelUp, UiClick, RogueSelect
    }

    [DisallowMultipleComponent]
    public sealed class AudioManager : MonoBehaviour
    {
        private const string BgmVolumeKey = "settings.bgmVolume";
        private const string SfxVolumeKey = "settings.sfxVolume";
        private static AudioManager _instance;
        private static bool _quitting;

        [Header("BGM")]
        [SerializeField] private AudioClip normal;
        [SerializeField] private AudioClip battle;
        [SerializeField] private AudioClip boss;
        [Header("Player SFX")]
        [SerializeField] private AudioClip split;
        [SerializeField] private AudioClip bigSplit;
        [SerializeField] private AudioClip charged;
        [SerializeField] private AudioClip hurt;
        [SerializeField] private AudioClip die;
        [SerializeField] private AudioClip idle;
        [SerializeField] private AudioClip run;
        [SerializeField] private AudioClip walk;
        [SerializeField] private AudioClip suck;
        [SerializeField] private AudioClip swallow;
        [SerializeField] private AudioClip roll;
        [SerializeField] private AudioClip beastHit;
        [Header("Enemy SFX")]
        [SerializeField] private AudioClip bossDie;
        [SerializeField] private AudioClip enemyDie;
        [SerializeField] private AudioClip rebound;
        [SerializeField] private AudioClip meatMountainLand;
        [SerializeField] private AudioClip babyCry;
        [SerializeField] private AudioClip satanLaugh;
        [SerializeField] private AudioClip dash;
        [Header("Environment SFX")]
        [SerializeField] private AudioClip hit;
        [SerializeField] private AudioClip bomb;
        [SerializeField] private AudioClip levelUp;
        [Header("UI SFX")]
        [SerializeField] private AudioClip uiClick;
        [SerializeField] private AudioClip rogueSelect;
        [Header("Mix")]
        [SerializeField, Range(0f, 1f)] private float defaultBgmVolume = 0.75f;
        [SerializeField, Range(0f, 1f)] private float defaultSfxVolume = 1f;
        [SerializeField, Min(0f)] private float bgmFadeDuration = 0.35f;

        private AudioSource _bgmSource;
        private AudioSource _sfxSource;
        private AudioSource _idleSource;
        private AudioSource _loopSource;
        private AudioSource _criticalSource;
        private AudioSource _intervalSource;
        private AudioCue? _loopCue;
        private AudioCue? _intervalCue;
        private Coroutine _intervalRoutine;
        private Coroutine _bgmRoutine;
        private BgmTrack? _currentTrack;
        private bool _sfxSuppressed;

        public static AudioManager Instance
        {
            get { EnsureInitialized(); return _instance; }
        }
        public static AudioManager Existing => _instance;

        public float BgmVolume { get; private set; }
        public float SfxVolume { get; private set; }
        public BgmTrack? CurrentTrack => _currentTrack;
        public bool IsSfxSuppressed => _sfxSuppressed;
        public bool IsCriticalSfxPlaying => _criticalSource != null && _criticalSource.isPlaying;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RuntimeBootstrap()
        {
            _quitting = false;
            EnsureInitialized();
        }

        public static void EnsureInitialized()
        {
            if (_instance != null || _quitting) return;
            AudioManager prefab = Resources.Load<AudioManager>("System/AudioManager");
            if (prefab != null) Instantiate(prefab);
            else new GameObject("[AudioManager]").AddComponent<AudioManager>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
            BgmVolume = PlayerPrefs.GetFloat(BgmVolumeKey, defaultBgmVolume);
            SfxVolume = PlayerPrefs.GetFloat(SfxVolumeKey, defaultSfxVolume);
            CreateSources();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            if (_instance != this) return;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            _instance = null;
        }

        private void OnApplicationQuit() { _quitting = true; }

        private void CreateSources()
        {
            _bgmSource = gameObject.AddComponent<AudioSource>();
            _bgmSource.playOnAwake = false;
            _bgmSource.loop = true;
            _bgmSource.volume = BgmVolume;
            _sfxSource = gameObject.AddComponent<AudioSource>();
            _sfxSource.playOnAwake = false;
            _sfxSource.volume = SfxVolume;
            _idleSource = gameObject.AddComponent<AudioSource>();
            _idleSource.playOnAwake = false;
            _idleSource.volume = SfxVolume;
            _loopSource = gameObject.AddComponent<AudioSource>();
            _loopSource.playOnAwake = false;
            _loopSource.loop = true;
            _loopSource.volume = SfxVolume;
            _criticalSource = gameObject.AddComponent<AudioSource>();
            _criticalSource.playOnAwake = false;
            _criticalSource.volume = SfxVolume;
            _intervalSource = gameObject.AddComponent<AudioSource>();
            _intervalSource.playOnAwake = false;
            _intervalSource.volume = SfxVolume;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == SceneNames.Game) PlayBgm(BgmTrack.Battle);
            else if (scene.name == SceneNames.Load || scene.name == SceneNames.Menu) PlayBgm(BgmTrack.Normal);
        }

        public void SetBattleWave(int wave)
        {
            PlayBgm(wave > 0 && wave % 10 == 0 ? BgmTrack.Boss : BgmTrack.Battle);
        }

        public void PlayBgm(BgmTrack track)
        {
            AudioClip clip = GetBgmClip(track);
            if (clip == null) return;
            if (_currentTrack == track && _bgmSource.clip == clip && _bgmSource.isPlaying) return;
            _currentTrack = track;
            if (_bgmRoutine != null) StopCoroutine(_bgmRoutine);
            _bgmRoutine = StartCoroutine(SwitchBgmRoutine(clip));
        }

        private IEnumerator SwitchBgmRoutine(AudioClip clip)
        {
            float startVolume = _bgmSource.volume;
            float elapsed = 0f;
            while (_bgmSource.isPlaying && elapsed < bgmFadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                _bgmSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / Mathf.Max(0.01f, bgmFadeDuration));
                yield return null;
            }
            _bgmSource.clip = clip;
            _bgmSource.volume = BgmVolume;
            _bgmSource.Play();
            _bgmRoutine = null;
        }

        public void PlaySfx(AudioCue cue, float volumeScale = 1f)
        {
            if (_sfxSuppressed && cue != AudioCue.RogueSelect) return;
            AudioClip clip = GetSfxClip(cue);
            if (clip == null) return;

            if (cue == AudioCue.Idle)
            {
                _idleSource.Stop();
                _idleSource.clip = clip;
                _idleSource.volume = SfxVolume * Mathf.Clamp01(volumeScale);
                _idleSource.Play();
                return;
            }

            _sfxSource.PlayOneShot(clip, Mathf.Clamp01(volumeScale));
        }

        public void StopSfx(AudioCue cue)
        {
            if (cue != AudioCue.Idle || _idleSource == null) return;
            _idleSource.Stop();
            _idleSource.clip = null;
            _idleSource.volume = SfxVolume;
        }

        public void PlayLoop(AudioCue cue)
        {
            if (_sfxSuppressed) return;
            AudioClip clip = GetSfxClip(cue);
            if (clip == null) return;
            if (_loopCue == cue && _loopSource.isPlaying) return;
            _loopCue = cue;
            _loopSource.clip = clip;
            _loopSource.loop = true;
            _loopSource.Play();
        }

        /// <summary>Plays on the controllable loop source once, so release can stop it without repetition.</summary>
        public void PlayOnceUntilStopped(AudioCue cue)
        {
            if (_sfxSuppressed) return;
            AudioClip clip = GetSfxClip(cue);
            if (clip == null) return;
            if (_loopCue == cue && _loopSource.isPlaying) return;
            _loopCue = cue;
            _loopSource.clip = clip;
            _loopSource.loop = false;
            _loopSource.Play();
        }

        public void PlayIntervalLoop(AudioCue cue, float gapSeconds)
        {
            if (_sfxSuppressed) return;
            AudioClip clip = GetSfxClip(cue);
            if (clip == null) return;
            if (_intervalCue == cue && _intervalRoutine != null) return;
            StopIntervalLoop();
            _intervalCue = cue;
            _intervalRoutine = StartCoroutine(IntervalLoopRoutine(clip, Mathf.Max(0f, gapSeconds)));
        }

        public void StopIntervalLoop(AudioCue cue)
        {
            if (_intervalCue == cue) StopIntervalLoop();
        }

        private void StopIntervalLoop()
        {
            if (_intervalRoutine != null) StopCoroutine(_intervalRoutine);
            _intervalRoutine = null;
            _intervalCue = null;
            if (_intervalSource == null) return;
            _intervalSource.Stop();
            _intervalSource.clip = null;
        }

        private IEnumerator IntervalLoopRoutine(AudioClip clip, float gapSeconds)
        {
            while (true)
            {
                _intervalSource.clip = clip;
                _intervalSource.Play();
                yield return new WaitForSecondsRealtime(clip.length + gapSeconds);
            }
        }

        public void SetSfxSuppressed(bool suppressed)
        {
            _sfxSuppressed = suppressed;
            if (!suppressed) return;
            // Do not cut action one-shots already in progress (swallow, teaching shot, level-up).
            // New gameplay sounds remain blocked by PlaySfx while the selection is open.
            StopSfx(AudioCue.Idle);
            _loopSource.Stop();
            _loopSource.clip = null;
            _loopCue = null;
            StopIntervalLoop();
        }

        public void EnterGameOverAudio()
        {
            SetSfxSuppressed(true);
            _sfxSource.Stop();
            AudioClip clip = GetSfxClip(AudioCue.Die);
            if (clip != null)
            {
                _criticalSource.Stop();
                _criticalSource.PlayOneShot(clip);
            }
        }

        public void RestartCurrentBgm()
        {
            if (!_currentTrack.HasValue) return;
            AudioClip clip = GetBgmClip(_currentTrack.Value);
            if (clip == null) return;
            if (_bgmRoutine != null) StopCoroutine(_bgmRoutine);
            _bgmSource.Stop();
            _bgmSource.clip = clip;
            _bgmSource.volume = BgmVolume;
            _bgmSource.Play();
        }

        public void StopLoop(AudioCue cue)
        {
            if (_loopCue != cue) return;
            _loopSource.Stop();
            _loopSource.clip = null;
            _loopCue = null;
        }

        public void SetBgmVolume(float value)
        {
            BgmVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(BgmVolumeKey, BgmVolume);
            PlayerPrefs.Save();
            _bgmSource.volume = BgmVolume;
        }

        public void SetSfxVolume(float value)
        {
            SfxVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(SfxVolumeKey, SfxVolume);
            PlayerPrefs.Save();
            _sfxSource.volume = SfxVolume;
            _idleSource.volume = SfxVolume;
            _loopSource.volume = SfxVolume;
            _criticalSource.volume = SfxVolume;
            _intervalSource.volume = SfxVolume;
        }

        private AudioClip GetBgmClip(BgmTrack track)
        {
            switch (track)
            {
                case BgmTrack.Battle: return battle;
                case BgmTrack.Boss: return boss;
                default: return normal;
            }
        }

        private AudioClip GetSfxClip(AudioCue cue)
        {
            switch (cue)
            {
                case AudioCue.Split: return split;
                case AudioCue.BigSplit: return bigSplit;
                case AudioCue.Charged: return charged;
                case AudioCue.Hurt: return hurt;
                case AudioCue.Die: return die;
                case AudioCue.Idle: return idle;
                case AudioCue.Run: return run;
                case AudioCue.Walk: return walk;
                case AudioCue.Suck: return suck;
                case AudioCue.Swallow: return swallow;
                case AudioCue.Roll: return roll;
                case AudioCue.BeastHit: return beastHit;
                case AudioCue.BossDie: return bossDie;
                case AudioCue.EnemyDie: return enemyDie;
                case AudioCue.Rebound: return rebound;
                case AudioCue.MeatMountainLand: return meatMountainLand;
                case AudioCue.BabyCry: return babyCry;
                case AudioCue.SatanLaugh: return satanLaugh;
                case AudioCue.Dash: return dash;
                case AudioCue.Hit: return hit;
                case AudioCue.Bomb: return bomb;
                case AudioCue.LevelUp: return levelUp;
                case AudioCue.UiClick: return uiClick;
                case AudioCue.RogueSelect: return rogueSelect;
                default: return null;
            }
        }
    }
}
