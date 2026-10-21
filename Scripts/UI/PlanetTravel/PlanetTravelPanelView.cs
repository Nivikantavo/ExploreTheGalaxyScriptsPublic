using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Zenject;

public class PlanetTravelPanelView : MonoBehaviour
{
    [SerializeField] private GameObject _panelRoot;
    [SerializeField] private Button _closeButton;
    [SerializeField] private Transform _contentRoot;
    [SerializeField] private PlanetCardView _cardPrefab;
    [SerializeField] private PlanetCardView _comingSoonCardPrefab;
    [SerializeField] private string _comingSoonWorldId = SaveWorldIds.World2;
    [SerializeField] private int _comingSoonIslandIndex = 7;
    [SerializeField] private string _comingSoonChainSaveId = "Portal";
    [SerializeField] private int _comingSoonRequiredSteps = 7;
    [SerializeField] private WorldCatalogSO _worldCatalog;
    [SerializeField] private LoadingScreen _loadingScreen;
    [SerializeField] private TMP_Text _titleText;
    [SerializeField] private LocalizedString _title;
    [SerializeField] private LocalizedString _goLabel;

    public WorldCatalogSO WorldCatalog => _worldCatalog;

    private readonly List<PlanetCardView> _spawnedCards = new();
    private ISaveLoader _saveLoader;
    private bool _isVisible;
    private bool _isTraveling;

    [Inject]
    private void Construct(ISaveLoader saveLoader)
    {
        _saveLoader = saveLoader;
    }

    private void Awake()
    {
        if (_panelRoot != null)
            _panelRoot.SetActive(false);

        if (_loadingScreen == null)
            _loadingScreen = FindFirstObjectByType<LoadingScreen>(FindObjectsInactive.Include);
    }

    private void OnEnable()
    {
        if (_closeButton != null)
            _closeButton.onClick.AddListener(Hide);
    }

    private void OnDisable()
    {
        if (_closeButton != null)
            _closeButton.onClick.RemoveListener(Hide);
    }

    public void Show()
    {
        if (_isTraveling)
            return;

        GrantPortalUnlock();
        RefreshCards();
        ApplyTitle();

        if (_panelRoot != null)
            _panelRoot.SetActive(true);
        else
            gameObject.SetActive(true);

        if (!_isVisible)
            GameplayPause.Push();

        _isVisible = true;
    }

    public void Hide()
    {
        if (_panelRoot != null)
            _panelRoot.SetActive(false);
        else
            gameObject.SetActive(false);

        if (_isVisible)
            GameplayPause.Pop();

        _isVisible = false;
    }

    private void GrantPortalUnlock()
    {
        if (_saveLoader == null)
            return;

        string currentWorldId = _saveLoader.GetHostWorldId();
        string[] unlocked = PlanetTravelAccess.UnlockNext(
            currentWorldId,
            _saveLoader.GetUnlockedWorldIds(),
            _worldCatalog != null ? _worldCatalog.Worlds : null);
        _saveLoader.GrantWorldUnlocks(unlocked);
    }

    private void RefreshCards()
    {
        ClearCards();

        if (_contentRoot == null || _cardPrefab == null)
            return;

        WorldCatalogEntry[] worlds = _worldCatalog != null
            ? _worldCatalog.GetOrderedWorlds()
            : PlanetTravelAccess.GetOrdered(null);

        string currentWorldId = _saveLoader != null ? _saveLoader.GetHostWorldId() : SaveWorldIds.World1;
        string[] unlocked = _saveLoader != null
            ? _saveLoader.GetUnlockedWorldIds()
            : PlanetTravelAccess.EnsureDefaultUnlocks(null, currentWorldId, worlds);
        string goLabel = LocalizationSafe.Get(_goLabel, "Go");

        for (int i = 0; i < worlds.Length; i++)
        {
            WorldCatalogEntry entry = worlds[i];
            bool isUnlocked = PlanetTravelAccess.IsUnlocked(entry.WorldId, unlocked, worlds, currentWorldId);
            bool canTravel = PlanetTravelAccess.CanTravel(entry.WorldId, currentWorldId, unlocked, worlds);

            PlanetCardView view = Instantiate(_cardPrefab, _contentRoot);
            view.gameObject.SetActive(true);
            view.Bind(
                entry.WorldId,
                entry.GetDisplayName(),
                entry.Icon,
                goLabel,
                isUnlocked,
                canTravel,
                OnGoClicked);
            _spawnedCards.Add(view);
        }

        if (ShouldShowComingSoon() && _comingSoonCardPrefab != null)
        {
            PlanetCardView comingSoon = Instantiate(_comingSoonCardPrefab, _contentRoot);
            comingSoon.gameObject.SetActive(true);
            _spawnedCards.Add(comingSoon);
        }
    }

    private bool ShouldShowComingSoon()
    {
        if (string.IsNullOrEmpty(_comingSoonChainSaveId) || _comingSoonRequiredSteps <= 0)
            return false;

        if (_saveLoader != null &&
            string.Equals(_saveLoader.GetHostWorldId(), _comingSoonWorldId, StringComparison.Ordinal) &&
            TryGetIslandPortalChain(out OpeningObjectChain chain))
        {
            return chain.AreAllStepsFilled();
        }

        if (_saveLoader?.LoadedSave == null)
            return false;

        WorldSaveData world = SaveWorldAccess.FindWorld(_saveLoader.LoadedSave, _comingSoonWorldId);
        return PlanetTravelAccess.IsChainComplete(
            world?.OpeningObjectChains,
            _comingSoonChainSaveId,
            _comingSoonRequiredSteps);
    }

    private bool TryGetIslandPortalChain(out OpeningObjectChain chain)
    {
        chain = null;
        IslandProgressHandler[] islands = FindObjectsByType<IslandProgressHandler>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < islands.Length; i++)
        {
            IslandProgressHandler island = islands[i];
            if (island == null || island.IslandIndex != _comingSoonIslandIndex)
                continue;

            OpeningObjectChain[] chains = island.GetComponentsInChildren<OpeningObjectChain>(true);
            for (int j = 0; j < chains.Length; j++)
            {
                OpeningObjectChain candidate = chains[j];
                if (candidate == null || candidate.Steps == null || candidate.Steps.Count == 0)
                    continue;

                if (!string.Equals(candidate.ChainSaveId, _comingSoonChainSaveId, StringComparison.Ordinal))
                    continue;

                chain = candidate;
                return true;
            }
        }

        return false;
    }

    private void ApplyTitle()
    {
        if (_titleText == null)
            return;

        _titleText.text = LocalizationSafe.Get(_title, "Choose a planet");
    }

    private void OnGoClicked(string worldId)
    {
        if (_isTraveling || _saveLoader == null)
            return;

        WorldCatalogEntry[] worlds = _worldCatalog != null ? _worldCatalog.Worlds : null;
        string currentWorldId = _saveLoader.GetHostWorldId();
        if (!PlanetTravelAccess.CanTravel(worldId, currentWorldId, _saveLoader.GetUnlockedWorldIds(), worlds))
            return;

        string sceneName = _worldCatalog != null
            ? _worldCatalog.ResolveSceneName(worldId)
            : WorldCatalog.ResolveSceneName(worldId);

        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError($"PlanetTravelPanelView: scene for '{worldId}' is empty.");
            return;
        }

        if (!_saveLoader.SaveThenTravelTo(worldId))
        {
            Debug.LogError($"PlanetTravelPanelView: failed to persist travel to '{worldId}'.");
            return;
        }

        _isTraveling = true;
        Hide();

        if (_loadingScreen == null)
            _loadingScreen = FindFirstObjectByType<LoadingScreen>(FindObjectsInactive.Include);

        if (_loadingScreen != null)
            _loadingScreen.Show();

        SceneManager.LoadScene(sceneName);
    }

    private void ClearCards()
    {
        for (int i = 0; i < _spawnedCards.Count; i++)
        {
            if (_spawnedCards[i] != null)
                Destroy(_spawnedCards[i].gameObject);
        }

        _spawnedCards.Clear();
    }
}
