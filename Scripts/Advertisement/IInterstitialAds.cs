using System.Collections;

public interface IInterstitialAds
{
    /// <summary>
    /// Requests an interstitial and waits until it closes, fails, or is skipped.
    /// Completes immediately when ads cannot be shown.
    /// </summary>
    IEnumerator WaitForInterstitial(string placement);
}
