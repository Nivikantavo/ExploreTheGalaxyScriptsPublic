using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ShopInstrumentUpgradeCatalog", menuName = "Shop/Instrument Upgrade Catalog")]
public class ShopInstrumentUpgradeCatalogSO : ScriptableObject
{
    [Serializable]
    public struct LevelCost
    {
        [Min(2)] public int TargetLevel;
        [Min(1)] public int GemCost;
    }

    [SerializeField, Min(1)] private int _fallbackBaseCost = 5;
    [SerializeField] private List<LevelCost> _costsByTargetLevel = new()
    {
        new LevelCost { TargetLevel = 2, GemCost = 10 },
        new LevelCost { TargetLevel = 3, GemCost = 15 },
        new LevelCost { TargetLevel = 4, GemCost = 20 },
        new LevelCost { TargetLevel = 5, GemCost = 25 },
        new LevelCost { TargetLevel = 6, GemCost = 30 },
        new LevelCost { TargetLevel = 7, GemCost = 40 },
        new LevelCost { TargetLevel = 8, GemCost = 50 },
        new LevelCost { TargetLevel = 9, GemCost = 60 },
        new LevelCost { TargetLevel = 10, GemCost = 75 },
    };

    public int FallbackBaseCost => Mathf.Max(1, _fallbackBaseCost);

    public int GetGemCost(int targetLevel)
    {
        targetLevel = Mathf.Max(2, targetLevel);

        if (_costsByTargetLevel != null)
        {
            for (int i = 0; i < _costsByTargetLevel.Count; i++)
            {
                LevelCost entry = _costsByTargetLevel[i];
                if (entry.TargetLevel == targetLevel && entry.GemCost > 0)
                    return entry.GemCost;
            }
        }

        return FallbackBaseCost * targetLevel;
    }
}
