using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Celebrations/Celebration Catalog", fileName = "CelebrationCatalog")]
public class CelebrationCatalogSO : ScriptableObject
{
    [SerializeField] private List<CelebrationDefinitionSO> _celebrations = new();

    public IReadOnlyList<CelebrationDefinitionSO> Celebrations => _celebrations;
}
