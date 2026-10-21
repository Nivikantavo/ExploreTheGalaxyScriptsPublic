using UnityEngine;
using Zenject;

public class IapInstaller : MonoInstaller
{
    [SerializeField] private IapCatalogSO _catalog;
    [SerializeField] private ResourceCellView _islandOfferResourceCellPrefab;

    public override void InstallBindings()
    {
        if (_catalog == null)
        {
            Debug.LogWarning("IapInstaller: IapCatalogSO is not assigned. IAP will be unavailable.");
            return;
        }

        Container.Bind<IapCatalogSO>().FromInstance(_catalog).AsSingle();

        if (_catalog.StarterPack != null)
            Container.Bind<StarterPackSO>().FromInstance(_catalog.StarterPack).AsSingle();

        if (_catalog.IslandOffers != null)
            Container.Bind<IslandOfferCatalogSO>().FromInstance(_catalog.IslandOffers).AsSingle();

        // Prefer AdsInstaller binding; create one if ads installer is absent.
        // Do not use AsSingle().IfNotBound() — Zenject marks AsSingle before IfNotBound runs.
        if (!Container.HasBinding<AdsRemovalState>())
            Container.Bind<AdsRemovalState>().AsSingle();

        Container.Bind<IapService>().AsSingle();

        Container.Bind<IslandOfferController>()
            .FromNewComponentOnNewGameObject()
            .WithGameObjectName(nameof(IslandOfferController))
            .AsSingle()
            .NonLazy();

        Container.BindInterfacesAndSelfTo<IslandOfferUiBootstrap>()
            .AsSingle()
            .WithArguments(_islandOfferResourceCellPrefab)
            .NonLazy();
    }
}
