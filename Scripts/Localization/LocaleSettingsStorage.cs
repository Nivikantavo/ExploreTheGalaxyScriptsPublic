using UnityEngine;

public static class LocaleSettingsStorage
{
    private const string LocaleCodeKey = "Localization.LocaleCode";

    public static string LoadLocaleCode()
    {
        return PlayerPrefs.GetString(LocaleCodeKey, string.Empty);
    }

    public static void SaveLocaleCode(string localeCode)
    {
        if (string.IsNullOrEmpty(localeCode))
            PlayerPrefs.DeleteKey(LocaleCodeKey);
        else
            PlayerPrefs.SetString(LocaleCodeKey, localeCode);

        PlayerPrefs.Save();
    }
}
