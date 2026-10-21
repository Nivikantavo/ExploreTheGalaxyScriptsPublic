using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class AchievementsPanelView : MonoBehaviour
{
    [SerializeField] private GameObject _panelRoot;
    [SerializeField] private Button _openButton;
    [SerializeField] private Button _closeButton;
    [SerializeField] private Transform _contentRoot;
    [SerializeField] private AchievementEntryView _entryPrefab;
    [SerializeField] private AchievementSystem _achievementSystem;

    private readonly List<AchievementEntryView> _spawnedEntries = new();
    private readonly List<AchievementListEntry> _entriesBuffer = new();
    private TutorialHandler _tutorialHandler;
    private bool _holdsPause;

    private void Awake()
    {
        if (_achievementSystem == null)
            _achievementSystem = FindFirstObjectByType<AchievementSystem>(FindObjectsInactive.Include);

        if (_panelRoot != null)
            _panelRoot.SetActive(false);

        _tutorialHandler = TutorialHudAccess.FindTutorialHandler();
        RefreshTutorialLock();
    }

    private void OnEnable()
    {
        if (_tutorialHandler == null)
            _tutorialHandler = TutorialHudAccess.FindTutorialHandler();

        if (_tutorialHandler != null)
            _tutorialHandler.OnTutorialResolved += RefreshTutorialLock;

        if (_openButton != null)
            _openButton.onClick.AddListener(Show);

        if (_closeButton != null)
            _closeButton.onClick.AddListener(Hide);

        if (_achievementSystem != null)
            _achievementSystem.AchievementUnlocked += OnAchievementUnlocked;

        RefreshTutorialLock();
    }

    private void OnDisable()
    {
        if (_tutorialHandler != null)
            _tutorialHandler.OnTutorialResolved -= RefreshTutorialLock;

        if (_openButton != null)
            _openButton.onClick.RemoveListener(Show);

        if (_closeButton != null)
            _closeButton.onClick.RemoveListener(Hide);

        if (_achievementSystem != null)
            _achievementSystem.AchievementUnlocked -= OnAchievementUnlocked;

        EndPause();
    }

    public void Show()
    {
        if (!TutorialHudAccess.IsMetaHudUnlocked(null, _tutorialHandler))
            return;

        RefreshEntries();

        if (_panelRoot != null)
            _panelRoot.SetActive(true);

        BeginPause();
    }

    private void RefreshTutorialLock()
    {
        if (_openButton == null)
            return;

        _openButton.gameObject.SetActive(TutorialHudAccess.IsMetaHudUnlocked(null, _tutorialHandler));
    }

    public void Hide()
    {
        if (_panelRoot != null)
            _panelRoot.SetActive(false);

        EndPause();
    }

    private void BeginPause()
    {
        if (_holdsPause)
            return;

        GameplayPause.Push();
        _holdsPause = true;
    }

    private void EndPause()
    {
        if (!_holdsPause)
            return;

        GameplayPause.Pop();
        _holdsPause = false;
    }

    private void OnAchievementUnlocked()
    {
        if (_panelRoot != null && _panelRoot.activeSelf)
            RefreshEntries();
    }

    private void RefreshEntries()
    {
        ClearEntries();

        if (_achievementSystem == null || _contentRoot == null || _entryPrefab == null)
            return;

        _entriesBuffer.Clear();
        _achievementSystem.CollectListEntries(_entriesBuffer);

        foreach (AchievementListEntry entry in _entriesBuffer)
        {
            AchievementEntryView view = Instantiate(_entryPrefab, _contentRoot);
            view.gameObject.SetActive(true);
            view.Bind(entry);
            _spawnedEntries.Add(view);
        }
    }

    private void ClearEntries()
    {
        for (int i = 0; i < _spawnedEntries.Count; i++)
        {
            if (_spawnedEntries[i] != null)
                Destroy(_spawnedEntries[i].gameObject);
        }

        _spawnedEntries.Clear();
    }
}
