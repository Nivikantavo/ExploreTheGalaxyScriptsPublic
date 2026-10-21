using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;

[CreateAssetMenu(fileName = "StarterPackSO", menuName = "IAP/Starter Pack")]
public class StarterPackSO : ScriptableObject
{
    public string ProductId => _productId;
    public LocalizedString Title => _title;
    public int BaseInstrumentLevel => _baseInstrumentLevel;
    public int BonusLevelsWhenAtOrAboveBase => _bonusLevelsWhenAtOrAboveBase;
    public IReadOnlyList<IapResourceReward> ResourceRewards => _resourceRewards;

    [SerializeField] private string _productId = "starter_pack";
    [SerializeField] private LocalizedString _title;
    [SerializeField] private int _baseInstrumentLevel = 4;
    [SerializeField] private int _bonusLevelsWhenAtOrAboveBase = 2;
    [SerializeField] private List<IapResourceReward> _resourceRewards = new();

    /// <summary>
    /// Offered level for one instrument: base if below base, otherwise current + bonus (clamped to max).
    /// Example: base 4, bonus 2 — level 1 → 4; level 5 → 7.
    /// </summary>
    public int GetOfferedInstrumentLevel(int currentLevel, int maxLevel = 0)
    {
        int baseLevel = Mathf.Max(1, _baseInstrumentLevel);
        int bonus = Mathf.Max(0, _bonusLevelsWhenAtOrAboveBase);
        int targetLevel = currentLevel >= baseLevel
            ? currentLevel + bonus
            : baseLevel;

        if (maxLevel > 0)
            targetLevel = Mathf.Min(targetLevel, maxLevel);

        return Mathf.Max(1, targetLevel);
    }
}
