using AYellowpaper.SerializedCollections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

[CreateAssetMenu(fileName = "AudioClipsConfig", menuName = "Scriptable Objects/AudioClipsConfig")]
public class AudioClipsConfig : ScriptableObject
{
    public AudioMixer AudioMixer => _audioMixer;
    public AudioMixerGroup MusicGroup => _musicGroup;
    public AudioMixerGroup SfxGroup => _sfxGroup;
    public string MusicVolumeParameter => _musicVolumeParameter;
    public string SfxVolumeParameter => _sfxVolumeParameter;
    public float ResourceHitPitchMin => _resourceHitPitchMin;
    public float ResourceHitPitchMax => _resourceHitPitchMax;
    public AudioClip BackgroundMusic => _backgroundMusic;
    public IReadOnlyDictionary<ResourceType, AudioClip> ResourceHitSounds => _resourceHitSounds;
    public AudioClip EnemyDeath => _enemyDeath;
    public AudioClip PlayerDeath => _playerDeath;

    [Header("Mixer")]
    [SerializeField] private AudioMixer _audioMixer;
    [SerializeField] private AudioMixerGroup _musicGroup;
    [SerializeField] private AudioMixerGroup _sfxGroup;
    [SerializeField] private string _musicVolumeParameter = "MusicVolume";
    [SerializeField] private string _sfxVolumeParameter = "SfxVolume";

    [Header("Resource Hit Pitch")]
    [SerializeField] private float _resourceHitPitchMin = 0.9f;
    [SerializeField] private float _resourceHitPitchMax = 1.1f;

    [Header("Clips")]
    [SerializeField] private AudioClip _backgroundMusic;
    [SerializeField] private SerializedDictionary<ResourceType, AudioClip> _resourceHitSounds;
    [SerializeField] private AudioClip _enemyDeath;
    [SerializeField] private AudioClip _playerDeath;

    public AudioClip GetClip(AudioClipId clipId)
    {
        return clipId switch
        {
            AudioClipId.EnemyDeath => _enemyDeath,
            AudioClipId.PlayerDeath => _playerDeath,
            _ => null,
        };
    }

    public AudioClip GetResourceHitClip(ResourceType resourceType)
    {
        if (_resourceHitSounds != null && _resourceHitSounds.TryGetValue(resourceType, out AudioClip clip))
            return clip;

        return null;
    }
}
