using System;
using UnityEngine;
using UnityEngine.Audio;
using Zenject;
#if UNITY_WEBGL && !UNITY_EDITOR
using Playgama;
#endif

[RequireComponent(typeof(AudioSource))]
public class AudioController : MonoBehaviour, IAudioService, IInitializable
{
    private const int ResourceHitSourceCount = 3;
    private const float MinDb = -80f;

    public event Action<float> MusicVolumeChanged;
    public event Action<float> SfxVolumeChanged;

    public float MusicVolume
    {
        get => _musicVolume;
        set => SetMusicVolume(value, save: true, notify: true);
    }

    public float SfxVolume
    {
        get => _sfxVolume;
        set => SetSfxVolume(value, save: true, notify: true);
    }

    private AudioClipsConfig _clipsConfig;
    private AudioSource _musicSource;
    private AudioSource _sfxSource;
    private AudioSource[] _resourceHitSources;
    private int _nextResourceHitSourceIndex;
    private float _musicVolume = 1f;
    private float _sfxVolume = 1f;
    private bool _pausedByFocus;
    private bool _pausedByAd;
    private bool _pausedByPlatform;
    private bool _subscribedToPlatformEvents;
    private float _timeScaleBeforePlatformPause = 1f;

    [Inject]
    private void Construct(AudioClipsConfig clipsConfig)
    {
        _clipsConfig = clipsConfig;
    }

    public void Initialize()
    {
        SetupAudioSources();
        SetMusicVolume(AudioSettingsStorage.LoadMusicVolume(), save: false, notify: false);
        SetSfxVolume(AudioSettingsStorage.LoadSfxVolume(), save: false, notify: false);
        PlayMusic();
    }

    private void Start()
    {
        TrySubscribePlatformEvents();
    }

    private void Update()
    {
        if (!_subscribedToPlatformEvents)
            TrySubscribePlatformEvents();
    }

    private void OnDestroy()
    {
        UnsubscribePlatformEvents();
    }

    public void PlayMusic()
    {
        if (_clipsConfig == null || _clipsConfig.BackgroundMusic == null || _musicSource == null)
            return;

        if (_musicSource.clip == _clipsConfig.BackgroundMusic && _musicSource.isPlaying)
            return;

        _musicSource.clip = _clipsConfig.BackgroundMusic;
        _musicSource.loop = true;
        _musicSource.Play();
    }

    public void PlaySfx(AudioClipId clipId)
    {
        if (_clipsConfig == null || _sfxSource == null)
            return;

        AudioClip clip = _clipsConfig.GetClip(clipId);
        if (clip == null)
            return;

        _sfxSource.pitch = 1f;
        _sfxSource.PlayOneShot(clip);
    }

    public void PlayResourceHit(ResourceType resourceType)
    {
        if (_clipsConfig == null || _resourceHitSources == null)
            return;

        AudioClip clip = _clipsConfig.GetResourceHitClip(resourceType);
        if (clip == null)
            return;

        AudioSource source = _resourceHitSources[_nextResourceHitSourceIndex];
        _nextResourceHitSourceIndex = (_nextResourceHitSourceIndex + 1) % _resourceHitSources.Length;

        float minPitch = _clipsConfig.ResourceHitPitchMin;
        float maxPitch = _clipsConfig.ResourceHitPitchMax;
        source.pitch = UnityEngine.Random.Range(minPitch, maxPitch);
        source.PlayOneShot(clip);
    }

    public void SetPausedByAd(bool paused)
    {
        if (_pausedByAd == paused)
            return;

        _pausedByAd = paused;
        UpdatePauseState();
    }

    private void SetupAudioSources()
    {
        _musicSource = GetComponent<AudioSource>();
        _musicSource.playOnAwake = false;
        _musicSource.loop = true;
        _musicSource.outputAudioMixerGroup = _clipsConfig != null ? _clipsConfig.MusicGroup : null;

        _sfxSource = gameObject.AddComponent<AudioSource>();
        _sfxSource.playOnAwake = false;
        _sfxSource.loop = false;
        _sfxSource.outputAudioMixerGroup = _clipsConfig != null ? _clipsConfig.SfxGroup : null;

        _resourceHitSources = new AudioSource[ResourceHitSourceCount];
        for (int i = 0; i < ResourceHitSourceCount; i++)
        {
            AudioSource hitSource = gameObject.AddComponent<AudioSource>();
            hitSource.playOnAwake = false;
            hitSource.loop = false;
            hitSource.outputAudioMixerGroup = _clipsConfig != null ? _clipsConfig.SfxGroup : null;
            _resourceHitSources[i] = hitSource;
        }
    }

    private void SetMusicVolume(float volume, bool save, bool notify)
    {
        float clamped = Mathf.Clamp01(volume);
        bool changed = !Mathf.Approximately(_musicVolume, clamped);
        _musicVolume = clamped;
        ApplyMixerVolume(_clipsConfig != null ? _clipsConfig.MusicVolumeParameter : null, _musicVolume);

        if (save)
            AudioSettingsStorage.SaveMusicVolume(_musicVolume);

        if (notify && changed)
            MusicVolumeChanged?.Invoke(_musicVolume);
    }

    private void SetSfxVolume(float volume, bool save, bool notify)
    {
        float clamped = Mathf.Clamp01(volume);
        bool changed = !Mathf.Approximately(_sfxVolume, clamped);
        _sfxVolume = clamped;
        ApplyMixerVolume(_clipsConfig != null ? _clipsConfig.SfxVolumeParameter : null, _sfxVolume);

        if (save)
            AudioSettingsStorage.SaveSfxVolume(_sfxVolume);

        if (notify && changed)
            SfxVolumeChanged?.Invoke(_sfxVolume);
    }

    private void ApplyMixerVolume(string parameterName, float linearVolume)
    {
        AudioMixer mixer = _clipsConfig != null ? _clipsConfig.AudioMixer : null;
        if (mixer == null || string.IsNullOrEmpty(parameterName))
            return;

        float db = linearVolume > 0.0001f ? Mathf.Log10(linearVolume) * 20f : MinDb;
        mixer.SetFloat(parameterName, db);
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        _pausedByFocus = !hasFocus;
        UpdatePauseState();
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        _pausedByFocus = pauseStatus;
        UpdatePauseState();
    }

    private void UpdatePauseState()
    {
        AudioListener.pause = _pausedByFocus || _pausedByAd || _pausedByPlatform;
    }

    private void TrySubscribePlatformEvents()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (_subscribedToPlatformEvents || Bridge.instance == null || Bridge.platform == null)
            return;

        Bridge.platform.audioStateChanged += OnAudioStateChanged;
        Bridge.platform.pauseStateChanged += OnPauseStateChanged;
        _subscribedToPlatformEvents = true;
        OnAudioStateChanged(Bridge.platform.isAudioEnabled);
#endif
    }

    private void UnsubscribePlatformEvents()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (!_subscribedToPlatformEvents || Bridge.instance == null || Bridge.platform == null)
            return;

        Bridge.platform.audioStateChanged -= OnAudioStateChanged;
        Bridge.platform.pauseStateChanged -= OnPauseStateChanged;
        _subscribedToPlatformEvents = false;
#endif
    }

    private void OnAudioStateChanged(bool isEnabled)
    {
        AudioListener.volume = isEnabled ? 1f : 0f;
    }

    private void OnPauseStateChanged(bool isPaused)
    {
        if (isPaused)
        {
            if (_pausedByPlatform)
                return;

            _timeScaleBeforePlatformPause = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
            _pausedByPlatform = true;
        }
        else
        {
            if (!_pausedByPlatform)
                return;

            Time.timeScale = _timeScaleBeforePlatformPause > 0f ? _timeScaleBeforePlatformPause : 1f;
            _pausedByPlatform = false;
        }

        UpdatePauseState();
    }
}
