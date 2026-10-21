using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Achievements/Achievement Catalog", fileName = "AchievementCatalog")]
public class AchievementCatalogSO : ScriptableObject
{
    [SerializeField] private List<AchievementDefinitionSO> _achievements = new();

    public IReadOnlyList<AchievementDefinitionSO> Achievements => _achievements;
}
