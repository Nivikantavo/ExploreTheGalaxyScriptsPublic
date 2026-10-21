using System;
using System.Collections;
using System.Collections.Generic;
using ECM2;
using Unity.Cinemachine;
using UnityEngine;
using Zenject;

public class TutorialHandler : MonoBehaviour
{
    public event Action OnTutorialEnded;

    /// <summary>
    /// Fires when the tutorial no longer blocks meta HUD — either completed in-play,
    /// skipped, or already finished in the loaded save.
    /// </summary>
    public event Action OnTutorialResolved;

    public bool IsResolved => _tutorialResolved;

    [SerializeField] private HexTile _tutorialEndHexTile;
    [SerializeField] private HexTile _gameStartHexTile;
    [SerializeField] private TutorialRocket _tutorialRocket;
    [SerializeField] private GameObject _brokenRocketGameObject;
    [SerializeField] private GameObject _explosionGameObject;
    [SerializeField] private CinemachineCamera _cinemachineCamera;
    [SerializeField] private LoadingScreen _loadingScreen;
    [SerializeField] private float _delayBeforePlayerHide = 1f;
    [Tooltip("Optional HUD roots/buttons to hide while the tutorial is active (in addition to RegisterHudLock).")]
    [SerializeField] private GameObject[] _hudLockedDuringTutorial;

    private readonly List<GameObject> _registeredHudLocks = new();
    private Player _player;
    private PlayerController _playerController;
    private LifecycleEventBus _lifecycleEventBus;
    private ISaveLoader _saveLoader;
    private PlayerSpawnPoints _spawnPoints;
    private bool _isPlayingCutscene;
    private bool _saveGateResolved;
    private bool _tutorialResolved;
    private bool _hudLocked = true;

    [Inject]
    private void Construct(
        Player player,
        LifecycleEventBus lifecycleEventBus,
        ISaveLoader saveLoader,
        PlayerSpawnPoints spawnPoints)
    {
        _player = player;
        _playerController = player.GetComponent<PlayerController>();
        _lifecycleEventBus = lifecycleEventBus;
        _saveLoader = saveLoader;
        _spawnPoints = spawnPoints;
    }

    private void Awake()
    {
        if (_tutorialRocket == null)
            _tutorialRocket = FindFirstObjectByType<TutorialRocket>(FindObjectsInactive.Include);

        if (_cinemachineCamera == null)
            _cinemachineCamera = FindFirstObjectByType<CinemachineCamera>(FindObjectsInactive.Include);

        if (_loadingScreen == null)
            _loadingScreen = FindFirstObjectByType<LoadingScreen>(FindObjectsInactive.Include);

        // Lock immediately so meta HUD does not flash before save/tutorial resolution.
        SetHudLocked(true);
    }

    private void Start()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        // WebGL loads cloud save asynchronously. SaveLoadBootstrap calls
        // BeginAfterSaveLoad() after storage is ready so UnlockFromSave cannot
        // accidentally trigger the tutorial end subscription.
        return;
#else
        BeginAfterSaveLoad();
#endif
    }

    /// <summary>
    /// Registers a HUD button/root that must stay inactive until the tutorial resolves.
    /// Safe to call from the target's Awake — this handler keeps the reference and re-enables it later.
    /// </summary>
    public void RegisterHudLock(GameObject target)
    {
        if (target == null)
            return;

        if (!_registeredHudLocks.Contains(target))
            _registeredHudLocks.Add(target);

        target.SetActive(!_hudLocked);
    }

    public void BeginAfterSaveLoad()
    {
        if (_saveLoader.HasLoadedSave && _saveLoader.LoadedSave.IsTutorialCompleted)
        {
            SkipBecauseSaveLoaded();
            return;
        }

        if (_saveGateResolved)
            return;

        _saveGateResolved = true;

        if (_tutorialEndHexTile != null)
            _tutorialEndHexTile.OnUnlockAnimationEnded += TutorialEnded;

        SetHudLocked(true);
    }

    public void SkipBecauseSaveLoaded()
    {
        _saveGateResolved = true;

        if (_tutorialEndHexTile != null)
            _tutorialEndHexTile.OnUnlockAnimationEnded -= TutorialEnded;

        if (_tutorialRocket != null)
        {
            _tutorialRocket.Landed -= OnRocketLanded;
            _tutorialRocket.gameObject.SetActive(false);
        }

        StopAllCoroutines();
        _isPlayingCutscene = false;
        _loadingScreen?.Hide();

        if (_brokenRocketGameObject != null)
            _brokenRocketGameObject.SetActive(true);

        ResolveTutorialHud();
        gameObject.SetActive(false);
    }

    private void TutorialEnded()
    {
        OnTutorialEnded?.Invoke();
        StartCoroutine(PlayEndCutscene());
    }

    public void SkipTutorial()
    {
        if (!isActiveAndEnabled)
            return;

        if (_tutorialEndHexTile != null)
            _tutorialEndHexTile.OnUnlockAnimationEnded -= TutorialEnded;

        if (_tutorialRocket != null)
            _tutorialRocket.Landed -= OnRocketLanded;

        StopAllCoroutines();
        _isPlayingCutscene = false;
        _loadingScreen?.Hide();

        OnTutorialEnded?.Invoke();

        if (_brokenRocketGameObject != null)
            _brokenRocketGameObject.SetActive(true);

        if (_tutorialRocket != null)
            _tutorialRocket.gameObject.SetActive(false);

        RespawnPlayerAtDefaultSpawn();
        ResolveTutorialHud();
        _saveLoader.Save();
        gameObject.SetActive(false);
    }

    private IEnumerator PlayEndCutscene()
    {
        if (_isPlayingCutscene)
            yield break;

        _isPlayingCutscene = true;

        if (_playerController != null)
        {
            _playerController.Input?.Disable();
            _playerController.enabled = false;
        }

        yield return new WaitForSeconds(_delayBeforePlayerHide);

        if (_player == null)
            yield break;

        Vector3 playerPosition = _player.transform.position;
        _player.gameObject.SetActive(false);
        _lifecycleEventBus?.Publish(new LifecycleEventData(LifecycleEventType.PlayerDied, playerPosition));

        FocusCameraOnRocket();
        yield return new WaitForSeconds(_delayBeforePlayerHide);

        if (_tutorialRocket != null)
            yield return _tutorialRocket.Launch();

        _loadingScreen?.Show();
        yield return null;

        _saveLoader?.Save();

        yield return null;
        yield return new WaitForSeconds(_delayBeforePlayerHide);
        yield return PlayStartCutscene();
    }

    private IEnumerator PlayStartCutscene()
    {
        if (_tutorialRocket == null || _gameStartHexTile == null)
            yield break;

        _tutorialRocket.Landed += OnRocketLanded;
        _loadingScreen?.Hide();
        yield return _tutorialRocket.Landing(_gameStartHexTile.transform);
        if (_explosionGameObject != null)
            _explosionGameObject.gameObject.SetActive(true);
    }

    private void OnRocketLanded()
    {
        StartCoroutine(SpawnPlayerCoroutine());
    }

    private IEnumerator SpawnPlayerCoroutine()
    {
        if (_brokenRocketGameObject != null)
            _brokenRocketGameObject.SetActive(true);

        if (_tutorialRocket != null)
            _tutorialRocket.gameObject.SetActive(false);

        yield return new WaitForSeconds(_delayBeforePlayerHide);
        RespawnPlayerAtDefaultSpawn();

        if (_tutorialRocket != null)
            _tutorialRocket.Landed -= OnRocketLanded;

        ResolveTutorialHud();
        gameObject.SetActive(false);
    }

    private void RespawnPlayerAtDefaultSpawn()
    {
        if (_player == null || _spawnPoints?.DefaultSpawn == null)
            return;

        Vector3 spawnPosition = _spawnPoints.DefaultSpawn.position;

        _player.gameObject.SetActive(true);

        Character character = _player.GetComponent<Character>();
        if (character != null)
            character.SetPosition(spawnPosition);
        else
            _player.transform.position = spawnPosition;

        if (_playerController != null)
        {
            _playerController.enabled = true;
            _playerController.Input?.Enable();
        }

        if (_cinemachineCamera != null && _player.CameraTarget != null)
        {
            Transform cameraTarget = _player.CameraTarget.transform;
            _cinemachineCamera.Follow = cameraTarget;
            _cinemachineCamera.LookAt = cameraTarget;
        }

        _lifecycleEventBus?.Publish(new LifecycleEventData(LifecycleEventType.PlayerSpawned, spawnPosition));
    }

    private void FocusCameraOnRocket()
    {
        if (_cinemachineCamera == null || _tutorialRocket == null)
            return;

        Transform rocketTransform = _tutorialRocket.transform;
        _cinemachineCamera.Follow = rocketTransform;
        _cinemachineCamera.LookAt = rocketTransform;
    }

    private void ResolveTutorialHud()
    {
        SetHudLocked(false);
        TutorialHudAccess.NotifyTutorialResolved();

        if (_tutorialResolved)
            return;

        _tutorialResolved = true;
        OnTutorialResolved?.Invoke();
    }

    private void SetHudLocked(bool locked)
    {
        _hudLocked = locked;

        if (_hudLockedDuringTutorial != null)
        {
            for (int i = 0; i < _hudLockedDuringTutorial.Length; i++)
                ApplyLock(_hudLockedDuringTutorial[i], locked);
        }

        for (int i = 0; i < _registeredHudLocks.Count; i++)
            ApplyLock(_registeredHudLocks[i], locked);
    }

    private static void ApplyLock(GameObject target, bool locked)
    {
        if (target != null)
            target.SetActive(!locked);
    }
}
