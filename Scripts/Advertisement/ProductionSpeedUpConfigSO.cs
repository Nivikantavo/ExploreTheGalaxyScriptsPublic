using UnityEngine;
using UnityEngine.Localization;

[CreateAssetMenu(menuName = "Ads/Production Speed Up Config", fileName = "ProductionSpeedUpConfig")]
public class ProductionSpeedUpConfigSO : ScriptableObject
{
    [SerializeField] private int _minRemainingSecondsToShow = 60;
    [SerializeField] private int _rewardedTimeReductionSeconds = 180;
    [SerializeField] private string _rewardedPlacement = "production_speed_up";

    [Header("Popup Copy")]
    [SerializeField] private LocalizedString _instantSkipLabel;
    [SerializeField] private LocalizedString _skipPartLabel;

    [Header("Soft Currency Skip")]
    [SerializeField] private bool _softCurrencySkipEnabled;
    [SerializeField] private int _secondsPerGem = 600;

    public int MinRemainingSecondsToShow => Mathf.Max(0, _minRemainingSecondsToShow);
    public int RewardedTimeReductionSeconds => Mathf.Max(1, _rewardedTimeReductionSeconds);
    public string RewardedPlacement =>
        string.IsNullOrWhiteSpace(_rewardedPlacement) ? "production_speed_up" : _rewardedPlacement;

    public bool SoftCurrencySkipEnabled => _softCurrencySkipEnabled;
    public int SecondsPerGem => Mathf.Max(1, _secondsPerGem);

    public string InstantSkipLabel => SafeLocalized(_instantSkipLabel);

    public string SkipPartLabel => SafeLocalized(_skipPartLabel);

    public int GetSoftCurrencySkipCost(int remainingSeconds)
    {
        remainingSeconds = Mathf.Max(0, remainingSeconds);
        if (remainingSeconds <= 0)
            return 0;

        return Mathf.Max(1, Mathf.CeilToInt(remainingSeconds / (float)SecondsPerGem));
    }

    public string GetSkipTitle(int remainingSeconds)
    {
        remainingSeconds = Mathf.Max(0, remainingSeconds);

        if (remainingSeconds <= RewardedTimeReductionSeconds)
        {
            string instant = InstantSkipLabel;
            return string.IsNullOrEmpty(instant) ? "Instant skip!" : instant;
        }

        string template = SkipPartLabel;
        if (string.IsNullOrEmpty(template))
            template = "{0}";

        return string.Format(template, FormatRemainingTime(_rewardedTimeReductionSeconds));
    }

    public static string FormatRemainingTime(int remainingSeconds)
    {
        remainingSeconds = Mathf.Max(0, remainingSeconds);
        return $"{remainingSeconds / 60}:{remainingSeconds % 60:00}";
    }

    private static string SafeLocalized(LocalizedString localizedString)
    {
        if (localizedString == null || localizedString.IsEmpty)
            return string.Empty;

        try
        {
            return localizedString.GetLocalizedString();
        }
        catch
        {
            return string.Empty;
        }
    }
}
