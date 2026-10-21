using System;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Lightweight volume-only audio service for menu scenes without Zenject AudioController.
/// Persists the same PlayerPrefs keys that gameplay AudioController reads.
/// </summary>
public sealed class MenuAudioVolumeService : IAudioService
{
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

    private readonly AudioClipsConfig _clipsConfig;
    private float _musicVolume = 1f;
    private float _sfxVolume = 1f;

    public MenuAudioVolumeService(AudioClipsConfig clipsConfig)
    {
        _clipsConfig = clipsConfig;
        SetMusicVolume(AudioSettingsStorage.LoadMusicVolume(), save: false, notify: false);
        SetSfxVolume(AudioSettingsStorage.LoadSfxVolume(), save: false, notify: false);
    }

    public void PlayMusic()
    {
    }

    public void PlaySfx(AudioClipId clipId)
    {
    }

    public void PlayResourceHit(ResourceType resourceType)
    {
    }

    public void SetPausedByAd(bool paused)
    {
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
}
