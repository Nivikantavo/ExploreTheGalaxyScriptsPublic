using UnityEngine;
using Zenject;

public class SaveInstaller : MonoInstaller
{
    public override void InstallBindings()
    {
#if UNITY_EDITOR
        Container.Bind<ISaveStorage>().To<PlayerPrefsSaveLoader>().AsSingle();
#elif UNITY_WEBGL
        Container.Bind<ISaveStorage>().To<BridgeSaveLoader>().AsSingle();
#else
        Container.Bind<ISaveStorage>().To<PlayerPrefsSaveLoader>().AsSingle();
#endif
        Container.Bind<IslandProgressRegistry>().AsSingle();
        Container.Bind<GoalOpenerRegistry>().AsSingle();
        Container.Bind<NpcRescueRegistry>().AsSingle();
        Container.Bind<IapSaveBridge>().AsSingle();
        Container.BindInterfacesAndSelfTo<SaveLoadSystem>().AsSingle();
        Container.Bind<DynamicNavMeshRebuilder>()
            .FromNewComponentOnNewGameObject()
            .WithGameObjectName(nameof(DynamicNavMeshRebuilder))
            .AsSingle()
            .NonLazy();
    }
}
