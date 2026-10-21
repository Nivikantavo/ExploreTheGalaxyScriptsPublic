using UnityEngine.Localization;

public static class LocalizationSafe
{
    public static string Get(LocalizedString localized, string fallback = "")
    {
        if (localized == null || localized.IsEmpty)
            return fallback ?? string.Empty;

        try
        {
            return localized.GetLocalizedString();
        }
        catch
        {
            return fallback ?? string.Empty;
        }
    }
}
