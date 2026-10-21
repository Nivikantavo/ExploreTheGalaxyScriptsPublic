using System.Collections.Generic;
using AYellowpaper.SerializedCollections;
using UnityEngine;

[CreateAssetMenu(fileName = "ResourceTypeViews", menuName = "Scriptable Objects/ResourceTypeViews")]
public class ResourceTypeViews : ScriptableObject
{
    public IReadOnlyDictionary<ResourceType, Sprite> Icons => _icons;
    public IReadOnlyDictionary<ResourceType, GameObject> ResourceObjects => _resourceObjects;
    [SerializeField] private SerializedDictionary<ResourceType, Sprite> _icons;
    [SerializeField] private SerializedDictionary<ResourceType, GameObject> _resourceObjects;
}
