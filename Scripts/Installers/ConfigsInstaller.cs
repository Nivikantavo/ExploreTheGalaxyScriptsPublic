using UnityEngine;
using Zenject;

public class ConfigsInstaller : MonoInstaller
{
    [SerializeField] private AllInstruments _allInstruments;
    [SerializeField] private AllEquipment _allEquipment;
    [SerializeField] private AllResoursesConfig _allResoursesConfig;
    [SerializeField] private ResourceTypeViews _resourceTypeViews;
    [SerializeField] private AudioClipsConfig _audioClipsConfig;

    public override void InstallBindings()
    {
        Container.Bind<AllInstruments>().FromInstance(_allInstruments).AsSingle();
        Container.Bind<AllEquipment>().FromInstance(_allEquipment).AsSingle();
        Container.Bind<AllResoursesConfig>().FromInstance(_allResoursesConfig).AsSingle();
        Container.Bind<ResourceTypeViews>().FromInstance(_resourceTypeViews).AsSingle();
        InstallAudioBindings();
    }

    private void InstallAudioBindings()
    {
        if (_audioClipsConfig == null)
        {
            Debug.LogWarning("ConfigsInstaller: AudioClipsConfig is not assigned. Audio system will not be initialized.");
            return;
        }

        Container.Bind<AudioClipsConfig>().FromInstance(_audioClipsConfig).AsSingle();

        Container.BindInterfacesAndSelfTo<AudioController>()
            .FromNewComponentOnNewGameObject()
            .WithGameObjectName("AudioController")
            .AsSingle()
            .NonLazy();

        Container.BindInterfacesAndSelfTo<GameplayAudioHandler>()
            .FromNewComponentOnNewGameObject()
            .WithGameObjectName("GameplayAudioHandler")
            .AsSingle()
            .NonLazy();
    }
}
