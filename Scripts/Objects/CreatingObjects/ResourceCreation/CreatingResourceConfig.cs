using UnityEngine;

[CreateAssetMenu(fileName = "CreatingResourceConfig", menuName = "Scriptable Objects/CreatingResourceConfig")]
public class CreatingResourceConfig : ScriptableObject
{
    public ResourceType ResultResource => _resourceType;
    public ResourceType RequestedResource => _requestedResource;
    public int RequestedForResource => _requestedForResource;
    public int TimeToCreatingOne => _timeToCreatingOne;
    public Sprite ResultResourceIcon => _resultResourceIcon;

    [SerializeField] private ResourceType _resourceType;
    [SerializeField] private ResourceType _requestedResource;
    [SerializeField] private int _requestedForResource;
    [SerializeField] private int _timeToCreatingOne;
    [SerializeField] private Sprite _resultResourceIcon;

}
