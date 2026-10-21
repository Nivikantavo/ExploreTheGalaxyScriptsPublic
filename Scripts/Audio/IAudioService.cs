using System;

public interface IAudioService
{
    float MusicVolume { get; set; }
    float SfxVolume { get; set; }

    event Action<float> MusicVolumeChanged;
    event Action<float> SfxVolumeChanged;

    void PlayMusic();
    void PlaySfx(AudioClipId clipId);
    void PlayResourceHit(ResourceType resourceType);
    void SetPausedByAd(bool paused);
}

public enum AudioClipId
{
    EnemyDeath,
    PlayerDeath,
}
