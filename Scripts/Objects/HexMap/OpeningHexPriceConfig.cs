using UnityEngine;
using AYellowpaper.SerializedCollections;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "OpeningHexPriceConfig", menuName = "Scriptable Objects/OpeningHexPriceConfig")]
public class OpeningHexPriceConfig : ScriptableObject
{
    public IReadOnlyDictionary<ResourceType, int> Price => _price;
    [SerializeField] private SerializedDictionary<ResourceType, int> _price;
}
