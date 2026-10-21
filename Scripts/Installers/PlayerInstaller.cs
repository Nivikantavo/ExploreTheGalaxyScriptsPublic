using Unity.Cinemachine;
using UnityEngine;
using Zenject;

public class PlayerInstaller : MonoInstaller
{
    [SerializeField] private Player _playerPrefab;
    [SerializeField] private PlayerInitConfig _playerConfig;
    [SerializeField] private Transform _playerSpawnPoint;
    [SerializeField] private Transform _playerTutorialSpawnPoint;
    [SerializeField] private DefaultInstruments _defaultInstruments;
    [SerializeField] private CinemachineCamera _cinemachineCamera;

    public override void InstallBindings()
    {
        Container.Bind<PlayerInitConfig>().FromInstance(_playerConfig).AsSingle();
        Container.Bind<LifecycleEventBus>().AsSingle();
        Container.Bind<PlayerSpawnPoints>()
            .FromInstance(new PlayerSpawnPoints(_playerSpawnPoint, _playerTutorialSpawnPoint))
            .AsSingle();
        Container.Bind<CinemachineCamera>().FromInstance(_cinemachineCamera).AsSingle();
        BindInventory();
        BindInstance();
        BindRespawnHandler();
    }

    private void BindInstance()
    {
        ISaveStorage saveStorage = Container.Resolve<ISaveStorage>();
        PlayerSpawnPoints spawnPoints = Container.Resolve<PlayerSpawnPoints>();
        Transform spawnPoint = saveStorage.HasSave() ? spawnPoints.DefaultSpawn : spawnPoints.TutorialSpawn;

        Player player = Container.InstantiatePrefabForComponent<Player>(_playerPrefab, spawnPoint.position, Quaternion.identity, null);
        Container.Bind<Player>().FromInstance(player).AsSingle();
        _cinemachineCamera.Follow = player.CameraTarget.transform;
    }

    private void BindRespawnHandler()
    {
        Container.Bind<PlayerRespawnHandler>()
            .FromNewComponentOnNewGameObject()
            .WithGameObjectName(nameof(PlayerRespawnHandler))
            .AsSingle()
            .NonLazy();
    }

    private void BindInventory()
    {
        Container.Bind<Inventory>().FromNew().AsSingle().WithArguments(_defaultInstruments).NonLazy();
    }
}
