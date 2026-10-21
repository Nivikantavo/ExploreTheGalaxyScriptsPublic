using UnityEngine;
using Zenject;

public class AdsInstaller : MonoInstaller
{
    public override void InstallBindings()
    {
        Container.Bind<AdsRemovalState>().AsSingle();

        Container.Bind<RewardedAdsController>()
            .FromNewComponentOnNewGameObject()
            .WithGameObjectName("RewardedAdsController")
            .AsSingle()
            .NonLazy();

        Container.BindInterfacesAndSelfTo<InterstitialAdsController>()
            .FromNewComponentOnNewGameObject()
            .WithGameObjectName("InterstitialAdsController")
            .AsSingle()
            .NonLazy();

        Container.Bind<BannerAdsController>()
            .FromNewComponentOnNewGameObject()
            .WithGameObjectName("BannerAdsController")
            .AsSingle()
            .NonLazy();
    }
}
