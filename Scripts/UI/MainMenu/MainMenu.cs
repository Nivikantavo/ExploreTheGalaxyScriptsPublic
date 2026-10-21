using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_WEBGL && !UNITY_EDITOR
using Playgama;
using Playgama.Modules.Platform;
#endif

public class MainMenu : MonoBehaviour
{
    private const float StorageInitTimeoutSeconds = 20f;

    [Header("Loading")]
    [SerializeField] private LoadingScreen _loadingScreen;
    [SerializeField] private GameObject _menuRoot;

    [Header("Buttons")]
    [SerializeField] private Button _playButton;
    [SerializeField] private Button _settingsButton;

    [Header("Settings")]
    [SerializeField] private SettingsPanelView _settingsPanel;

    [Header("Scene")]
    [SerializeField] private string _gameplaySceneName = "GameplayScene";
    [SerializeField] private WorldCatalogSO _worldCatalog;

    private bool _isReady;
    private bool _isLoadingGameplay;
    private bool _gameReadySent;
    private ISaveStorage _saveStorage;
    private IInitializableSaveLoader _initializableStorage;

    private void Awake()
    {
        SetMenuVisible(false);

        if (_loadingScreen != null)
            _loadingScreen.Show();

        if (_settingsPanel != null)
            _settingsPanel.Hide();
    }

    private void OnEnable()
    {
        if (_playButton != null)
            _playButton.onClick.AddListener(OnPlayClicked);

        if (_settingsButton != null)
            _settingsButton.onClick.AddListener(OnSettingsClicked);
    }

    private void OnDisable()
    {
        if (_playButton != null)
            _playButton.onClick.RemoveListener(OnPlayClicked);

        if (_settingsButton != null)
            _settingsButton.onClick.RemoveListener(OnSettingsClicked);

        if (_initializableStorage != null)
            _initializableStorage.LateDataArrived -= OnLateStorageDataArrived;
    }

    private IEnumerator Start()
    {
        // Saved settings language wins; otherwise Bridge.platform.language.
        // Loading screen stays until locale is applied.
        yield return LocaleApplicator.ApplyStartupLocale();
        yield return InitializeStorageAndResolveScene();

        if (_loadingScreen != null)
            _loadingScreen.Hide();

        SetMenuVisible(true);
        _isReady = true;

        yield return WaitForBridgePlatform();
        NotifyGameReady();
    }

    private static IEnumerator WaitForBridgePlatform()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        const float timeoutSeconds = 5f;
        float elapsed = 0f;
        while ((Bridge.instance == null || Bridge.platform == null) && elapsed < timeoutSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
#else
        yield break;
#endif
    }

    private void NotifyGameReady()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (_gameReadySent)
            return;

        if (Bridge.instance == null || Bridge.platform == null)
        {
            Debug.LogError("MainMenu: GameReady skipped — Bridge platform unavailable.");
            return;
        }

        try
        {
            Bridge.platform.SendMessage(PlatformMessage.GameReady);
            _gameReadySent = true;
        }
        catch (System.Exception exception)
        {
            Debug.LogError($"MainMenu: GameReady failed: {exception}");
        }
#endif
    }

    private void OnPlayClicked()
    {
        if (!_isReady || _isLoadingGameplay)
            return;

        ResolveGameplaySceneFromStorage();

        if (string.IsNullOrEmpty(_gameplaySceneName))
        {
            Debug.LogError("MainMenu: gameplay scene name is empty.");
            return;
        }

        _isLoadingGameplay = true;
        SetMenuVisible(false);

        if (_loadingScreen != null)
            _loadingScreen.Show();

        SceneManager.LoadScene(_gameplaySceneName);
    }

    private void OnSettingsClicked()
    {
        if (!_isReady || _settingsPanel == null)
            return;

        _settingsPanel.Show();
    }

    private void SetMenuVisible(bool visible)
    {
        if (_menuRoot != null)
            _menuRoot.SetActive(visible);

        if (_playButton != null)
            _playButton.interactable = visible;

        if (_settingsButton != null)
            _settingsButton.interactable = visible;
    }

    private IEnumerator InitializeStorageAndResolveScene()
    {
        _saveStorage = CreateStorage();
        if (_saveStorage is IInitializableSaveLoader initializableSaveLoader)
        {
            _initializableStorage = initializableSaveLoader;
            _initializableStorage.LateDataArrived += OnLateStorageDataArrived;

            if (!initializableSaveLoader.IsInitialized)
            {
                bool isCompleted = false;
                initializableSaveLoader.Initialize(() => isCompleted = true);

                float elapsed = 0f;
                while (!isCompleted && elapsed < StorageInitTimeoutSeconds)
                {
                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }

                if (!isCompleted)
                    initializableSaveLoader.ForceCompleteInitialization();
            }
        }

        ResolveGameplaySceneFromStorage();
    }

    private void OnLateStorageDataArrived()
    {
        if (_isLoadingGameplay)
            return;

        ResolveGameplaySceneFromStorage();
    }

    private void ResolveGameplaySceneFromStorage()
    {
        if (_saveStorage == null)
            return;

        if (!SavePersistPolicy.CanWriteToStorage(_saveStorage.InitState) &&
            _saveStorage.InitState != SaveStorageInitState.Loaded)
        {
            return;
        }

        string pickedJson = SaveSlotSelector.PickBestJson(_saveStorage.Load(), _saveStorage.LoadPrevious());
        if (string.IsNullOrEmpty(pickedJson))
            return;

        if (!SaveSlotSelector.TryParse(pickedJson, out GameSaveData save) || save == null)
            return;

        string sceneName = _worldCatalog != null
            ? _worldCatalog.ResolveSceneName(save.CurrentWorldId)
            : WorldCatalog.ResolveSceneName(save.CurrentWorldId);

        if (!string.IsNullOrEmpty(sceneName))
            _gameplaySceneName = sceneName;
    }

    private static ISaveStorage CreateStorage()
    {
#if UNITY_EDITOR
        return new PlayerPrefsSaveLoader();
#elif UNITY_WEBGL
        return new BridgeSaveLoader();
#else
        return new PlayerPrefsSaveLoader();
#endif
    }
}
