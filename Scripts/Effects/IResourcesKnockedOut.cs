using System;

public interface IResourcesTransition
{
    event Action<ResourcesTransitionViewData> OnResourcesTransition;
}
