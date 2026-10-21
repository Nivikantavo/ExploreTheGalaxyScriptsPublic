using UnityEngine;

public class ResourcePopapEffects : MonoBehaviour
{
    [SerializeField] private GameObject _pickupPopupPrefab;
    [SerializeField] private Vector3 _pickupPopupOffset = new Vector3(0f, 1.5f, 0f);

    private IResourcesTransition _resourcesTransition;

    private void Awake()
    {
        _resourcesTransition = GetComponentInParent<IResourcesTransition>();
    }

    private void OnEnable()
    {
        _resourcesTransition.OnResourcesTransition += HandleResourcesTransition;
    }

    private void OnDisable()
    {
        _resourcesTransition.OnResourcesTransition -= HandleResourcesTransition;
    }

    private void HandleResourcesTransition(ResourcesTransitionViewData data)
    {
        if (data.Count <= 0 || data.Config == null)
            throw new System.ArgumentOutOfRangeException("Count or config is less than 0");

        SpawnPopup(data);
    }

    private void SpawnPopup(ResourcesTransitionViewData data)
    {
        ResoursePickupPopupPool.SpawnAndPlay(
            prefab: _pickupPopupPrefab,
            spawnPosition: data.StartPosition + _pickupPopupOffset,
            icon: data.Config.Icon,
            info: CompactNumberFormatter.Format(data.Count)
        );
    }
}
