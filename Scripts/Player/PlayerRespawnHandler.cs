using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using Zenject;

public class PlayerRespawnHandler : MonoBehaviour
{
    private const float RespawnDelaySeconds = 1f;

    private Player _player;
    private PlayerSpawnPoints _spawnPoints;
    private ISaveLoader _saveLoader;
    private CinemachineCamera _cinemachineCamera;
    private IInterstitialAds _interstitialAds;
    private Coroutine _respawnCoroutine;

    [Inject]
    private void Construct(
        Player player,
        PlayerSpawnPoints spawnPoints,
        ISaveLoader saveLoader,
        CinemachineCamera cinemachineCamera,
        [InjectOptional] IInterstitialAds interstitialAds)
    {
        _player = player;
        _spawnPoints = spawnPoints;
        _saveLoader = saveLoader;
        _cinemachineCamera = cinemachineCamera;
        _interstitialAds = interstitialAds;
        _player.Died += OnPlayerDied;
    }

    private void OnDestroy()
    {
        if (_player != null)
            _player.Died -= OnPlayerDied;

        if (_respawnCoroutine != null)
        {
            StopCoroutine(_respawnCoroutine);
            _respawnCoroutine = null;
        }
    }

    private void OnPlayerDied()
    {
        if (_respawnCoroutine != null)
            return;

        _respawnCoroutine = StartCoroutine(RespawnRoutine());
    }

    private IEnumerator RespawnRoutine()
    {
        _player.gameObject.SetActive(false);

        yield return new WaitForSecondsRealtime(RespawnDelaySeconds);

        if (_interstitialAds != null)
            yield return _interstitialAds.WaitForInterstitial(InterstitialPlacements.PlayerDeath);

        Transform spawnPoint = GetCurrentSpawnPoint();
        if (spawnPoint != null)
        {
            _cinemachineCamera.Follow = spawnPoint;
            _cinemachineCamera.LookAt = spawnPoint;
        }

        Vector3 spawnPosition = spawnPoint != null
            ? spawnPoint.position
            : _player.transform.position;

        _player.RespawnAt(spawnPosition);

        Transform cameraTarget = _player.CameraTarget.transform;
        _cinemachineCamera.Follow = cameraTarget;
        _cinemachineCamera.LookAt = cameraTarget;

        _respawnCoroutine = null;
    }

    private Transform GetCurrentSpawnPoint()
    {
        SceneTravelService travel = SceneTravelService.Instance;
        if (travel != null && travel.TryGetDungeonSpawnPoint(out Transform dungeonSpawn))
            return dungeonSpawn;

        if (_spawnPoints == null)
            return null;

        return _saveLoader != null && _saveLoader.IsTutorialCompleted
            ? _spawnPoints.DefaultSpawn
            : _spawnPoints.TutorialSpawn;
    }
}
