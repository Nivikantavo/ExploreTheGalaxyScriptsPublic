using System;
using System.Collections.Generic;
using System.Globalization;

/// <summary>
/// Yandex remote-config sale percents. A flag is active when its value parses as an integer above 0.
/// </summary>
public readonly struct IapSalePercents
{
    public const string RemoveAdsFlag = "remove_ads_sale";
    public const string CurrencySale50Flag = "currency_sale_50";
    public const string CurrencySale25Flag = "currency_sale_25";
    public const string StarterPackSale50Flag = "starter_pack_sale_50";
    public const string StarterPackSale25Flag = "starter_pack_sale_25";

    public int RemoveAdsPercent { get; }
    public int CurrencyPercent { get; }
    public int StarterPackPercent { get; }

    public IapSalePercents(int removeAdsPercent, int currencyPercent, int starterPackPercent)
    {
        RemoveAdsPercent = removeAdsPercent;
        CurrencyPercent = currencyPercent;
        StarterPackPercent = starterPackPercent;
    }

    public static IapSalePercents FromFlags(IReadOnlyDictionary<string, string> flags)
    {
        if (flags == null)
            return default;

        int currency = Math.Max(
            ReadPercent(flags, CurrencySale50Flag),
            ReadPercent(flags, CurrencySale25Flag));
        int starter = Math.Max(
            ReadPercent(flags, StarterPackSale50Flag),
            ReadPercent(flags, StarterPackSale25Flag));

        return new IapSalePercents(ReadPercent(flags, RemoveAdsFlag), currency, starter);
    }

    private static int ReadPercent(IReadOnlyDictionary<string, string> flags, string key)
    {
        if (!flags.TryGetValue(key, out string raw) || string.IsNullOrWhiteSpace(raw))
            return 0;

        if (!int.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int percent))
            return 0;

        if (percent <= 0)
            return 0;

        return Math.Min(percent, 100);
    }
}
