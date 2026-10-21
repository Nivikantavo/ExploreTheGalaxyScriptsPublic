using UnityEngine;

public static class AudioSettingsStorage
{
    private const string MusicVolumeKey = "Audio.MusicVolume";
    private const string SfxVolumeKey = "Audio.SfxVolume";
    private const string LegacyMasterVolumeKey = "Audio.MasterVolume";
    private const float DefaultVolume = 1f;

    public static float LoadMusicVolume()
    {
        if (PlayerPrefs.HasKey(MusicVolumeKey))
            return PlayerPrefs.GetFloat(MusicVolumeKey, DefaultVolume);

        if (PlayerPrefs.HasKey(LegacyMasterVolumeKey))
            return PlayerPrefs.GetFloat(LegacyMasterVolumeKey, DefaultVolume);

        return DefaultVolume;
    }

    public static float LoadSfxVolume()
    {
        if (PlayerPrefs.HasKey(SfxVolumeKey))
            return PlayerPrefs.GetFloat(SfxVolumeKey, DefaultVolume);

        if (PlayerPrefs.HasKey(LegacyMasterVolumeKey))
            return PlayerPrefs.GetFloat(LegacyMasterVolumeKey, DefaultVolume);

        return DefaultVolume;
    }

    public static void SaveMusicVolume(float volume)
    {
        PlayerPrefs.SetFloat(MusicVolumeKey, Mathf.Clamp01(volume));
        PlayerPrefs.Save();
    }

    public static void SaveSfxVolume(float volume)
    {
        PlayerPrefs.SetFloat(SfxVolumeKey, Mathf.Clamp01(volume));
        PlayerPrefs.Save();
    }
}
