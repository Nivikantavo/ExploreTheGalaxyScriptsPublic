using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
#if UNITY_WEBGL
using Playgama;
#endif

public static class LocaleApplicator
{
    public const string NativeNameTable = "LocalizationTable";
    public const string NativeNameKey = "LanguageNativeName";

    private const float BridgeReadyTimeoutSeconds = 5f;

    private static readonly Dictionary<string, string> FallbackNativeNames = new(StringComparer.OrdinalIgnoreCase)
    {
        { "en", "English" },
        { "ru", "Русский" },
        { "tr", "Türk" },
        { "de", "Deutsch" },
        { "fr", "Français" },
        { "es", "Español" },
        { "it", "Italiano" },
        { "ja", "日本語" },
        { "ko", "한국어" },
        { "zh-Hans", "简体中文" },
    };

    /// <summary>
    /// Applies saved locale if present; otherwise platform language from Playgama Bridge.
    /// Must complete before hiding the start-scene loading screen.
    /// </summary>
    public static IEnumerator ApplyStartupLocale()
    {
        yield return LocalizationSettings.InitializationOperation;

        string savedCode = LocaleSettingsStorage.LoadLocaleCode();
        if (TryApplyLocaleCode(savedCode))
            yield break;

        yield return WaitForBridgePlatform();

        string platformCode = ReadPlatformLanguageCode();
        TryApplyLocaleCode(platformCode);
    }

    public static bool TryApplyLocaleCode(string localeCode)
    {
        Locale locale = FindAvailableLocale(localeCode);
        if (locale == null)
            return false;

        LocalizationSettings.SelectedLocale = locale;
        return true;
    }

    public static Locale FindAvailableLocale(string localeCode)
    {
        if (string.IsNullOrWhiteSpace(localeCode))
            return null;

        if (LocalizationSettings.AvailableLocales == null)
            return null;

        ILocalesProvider available = LocalizationSettings.AvailableLocales;
        Locale locale = available.GetLocale(localeCode);
        if (locale != null)
            return locale;

        locale = available.GetLocale(new LocaleIdentifier(localeCode));
        if (locale != null)
            return locale;

        string language = ExtractLanguagePart(localeCode);
        if (!string.Equals(language, localeCode, StringComparison.OrdinalIgnoreCase))
        {
            locale = available.GetLocale(language);
            if (locale != null)
                return locale;
        }

        if (language.Equals("zh", StringComparison.OrdinalIgnoreCase))
        {
            locale = available.GetLocale("zh-Hans");
            if (locale != null)
                return locale;
        }

        IReadOnlyList<Locale> locales = available.Locales;
        for (int i = 0; i < locales.Count; i++)
        {
            Locale candidate = locales[i];
            if (candidate == null)
                continue;

            string code = candidate.Identifier.Code;
            if (code.Equals(language, StringComparison.OrdinalIgnoreCase)
                || code.StartsWith(language + "-", StringComparison.OrdinalIgnoreCase)
                || code.StartsWith(language + "_", StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        return null;
    }

    public static string GetNativeDisplayName(Locale locale)
    {
        if (locale == null)
            return string.Empty;

        string fromTable = TryGetNativeNameFromTable(locale);
        if (!string.IsNullOrEmpty(fromTable))
            return fromTable;

        if (FallbackNativeNames.TryGetValue(locale.Identifier.Code, out string fallback))
            return fallback;

        string language = ExtractLanguagePart(locale.Identifier.Code);
        if (FallbackNativeNames.TryGetValue(language, out fallback))
            return fallback;

        return locale.LocaleName;
    }

    private static string TryGetNativeNameFromTable(Locale locale)
    {
        try
        {
            StringTable table = LocalizationSettings.StringDatabase.GetTable(NativeNameTable, locale);
            if (table == null)
                return null;

            StringTableEntry entry = table.GetEntry(NativeNameKey);
            if (entry == null)
                return null;

            string value = entry.LocalizedValue;
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static IEnumerator WaitForBridgePlatform()
    {
#if UNITY_WEBGL
        float elapsed = 0f;
        while ((Bridge.instance == null || Bridge.platform == null) && elapsed < BridgeReadyTimeoutSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
#else
        yield break;
#endif
    }

    private static string ReadPlatformLanguageCode()
    {
#if UNITY_WEBGL
        if (Bridge.instance == null || Bridge.platform == null)
            return null;

        return Bridge.platform.language;
#else
        return null;
#endif
    }

    private static string ExtractLanguagePart(string localeCode)
    {
        int separator = localeCode.IndexOfAny(new[] { '-', '_' });
        if (separator <= 0)
            return localeCode;

        return localeCode.Substring(0, separator);
    }
}
