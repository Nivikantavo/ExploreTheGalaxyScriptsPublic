using UnityEngine;

public class ResourcesObjectDropEffect : MonoBehaviour
{
    private const int MaxDropsPerCall = 5;

    private IResourcesTransition _resourcesTransition;

    private void Awake()
    {
        _resourcesTransition = GetComponentInParent<IResourcesTransition>();
    }

    private void OnEnable()
    {
        if (_resourcesTransition == null)
            return;

        _resourcesTransition.OnResourcesTransition += HandleResourcesTransition;
    }

    private void OnDisable()
    {
        if (_resourcesTransition == null)
            return;

        _resourcesTransition.OnResourcesTransition -= HandleResourcesTransition;
    }

    private void HandleResourcesTransition(ResourcesTransitionViewData data)
    {
        if (data.Count <= 0 || data.Config == null)
            return;

        if (data.Config.ResourseObj == null || data.PickupTarget == null)
            return;

        SpawnResourceDrops(data);
    }

    private void SpawnResourceDrops(ResourcesTransitionViewData data)
    {
        int dropCount = Mathf.Min(data.Count, MaxDropsPerCall);
        for (int i = 0; i < dropCount; i++)
        {
            ResourseDropPool.SpawnAndPlay(
                prefab: data.Config.ResourseObj,
                spawnPosition: data.StartPosition,
                pickupTarget: data.PickupTarget
            );
        }
    }
}
