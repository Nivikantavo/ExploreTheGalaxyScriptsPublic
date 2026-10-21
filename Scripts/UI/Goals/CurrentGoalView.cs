using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UI;

public class CurrentGoalView : MonoBehaviour
{
    private const string LocalizationTable = "LocalizationTable";
    private const string ExitDungeonKey = "Exit the dungeon";
    private const string GoToDungeonKey = "Go to the dungeon";
    private const string GoToPlanetKey = "Go to planet {0}";

    [SerializeField] private GoalSystem _goalSystem;
    [SerializeField] private GoalCameraFocusController _cameraFocusController;
    [SerializeField] private Button _focusButton;
    [SerializeField] private TMP_Text _goalText;
    [SerializeField] private GameObject _completedGoalObject;
    [SerializeField] private Image _completedGoalIcon;
    [SerializeField] private float _completedGoalTextDuration = 1.75f;
    [SerializeField] private LocalizedString _allGoalsCompletedText;
    [SerializeField] private WorldCatalogSO _worldCatalog;
    [SerializeField] private LocalizedString _exitDungeonText;
    [SerializeField] private LocalizedString _goToDungeonText;
    [SerializeField] private LocalizedString _goToPlanetText;

    private Coroutine _completedMessageCoroutine;

    private void Awake()
    {
        if (_goalSystem == null)
            _goalSystem = FindFirstObjectByType<GoalSystem>(FindObjectsInactive.Include);

        if (_cameraFocusController == null)
            _cameraFocusController = FindFirstObjectByType<GoalCameraFocusController>(FindObjectsInactive.Include);

        if (_worldCatalog == null)
            _worldCatalog = FindLoadedWorldCatalog();
    }

    private void OnEnable()
    {
        if (_goalSystem == null)
            _goalSystem = FindFirstObjectByType<GoalSystem>(FindObjectsInactive.Include);

        if (_goalSystem != null)
        {
            _goalSystem.CurrentGoalChanged += OnCurrentGoalChanged;
            _goalSystem.GoalCompleted += OnGoalCompleted;
        }

        if (_focusButton != null)
            _focusButton.onClick.AddListener(OnFocusButtonClicked);
    }

    private void Start()
    {
        UpdateCurrentGoalView();
    }

    private void OnDisable()
    {
        if (_goalSystem != null)
        {
            _goalSystem.CurrentGoalChanged -= OnCurrentGoalChanged;
            _goalSystem.GoalCompleted -= OnGoalCompleted;
        }

        if (_focusButton != null)
            _focusButton.onClick.RemoveListener(OnFocusButtonClicked);
    }

    private void OnCurrentGoalChanged(GoalDefinitionSO _)
    {
        UpdateCurrentGoalView();
    }

    private void OnGoalCompleted(GoalDefinitionSO completedGoal)
    {
        if (_completedGoalObject == null || completedGoal == null)
            return;

        if (_completedMessageCoroutine != null)
            StopCoroutine(_completedMessageCoroutine);

        _completedMessageCoroutine = StartCoroutine(ShowCompletedMessageRoutine());
    }

    private IEnumerator ShowCompletedMessageRoutine()
    {
        _completedGoalObject.gameObject.SetActive(true);
        yield return new WaitForSeconds(Mathf.Max(0.1f, _completedGoalTextDuration));
        _completedGoalObject.gameObject.SetActive(false);
        _completedMessageCoroutine = null;
    }

    private void UpdateCurrentGoalView()
    {
        try
        {
            if (_goalSystem == null)
            {
                if (_goalText != null)
                    _goalText.text = LocalizationSafe.Get(_allGoalsCompletedText, "All goals completed");
                return;
            }

            if (_goalText != null)
                _goalText.text = ResolveGoalText();

            Sprite sprite = _goalSystem.GetCurrentGoalIcon();
            if (_completedGoalIcon != null)
            {
                if (sprite != null)
                {
                    _completedGoalIcon.sprite = sprite;
                    _completedGoalIcon.gameObject.SetActive(true);
                }
                else
                {
                    _completedGoalIcon.gameObject.SetActive(false);
                }
            }
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning($"{nameof(CurrentGoalView)} localization deferred: {exception.Message}");
            if (_goalText != null)
                _goalText.text = string.Empty;
        }
    }

    private string ResolveGoalText()
    {
        GoalTravelHint hint = _goalSystem.GetCurrentTravelHint();
        if (hint.Kind != GoalTravelHintKind.None)
            return FormatTravelHint(hint);

        return _goalSystem.GetCurrentGoalText();
    }

    private string FormatTravelHint(GoalTravelHint hint)
    {
        switch (hint.Kind)
        {
            case GoalTravelHintKind.ExitDungeon:
                return GetLocalized(_exitDungeonText, ExitDungeonKey, "Exit the dungeon");
            case GoalTravelHintKind.EnterDungeon:
                return GetLocalized(_goToDungeonText, GoToDungeonKey, "Go to the dungeon");
            case GoalTravelHintKind.GoToPlanet:
                string template = GetLocalized(_goToPlanetText, GoToPlanetKey, "Go to {0}");
                return string.Format(template, ResolvePlanetName(hint.PlanetWorldId));
            default:
                return _goalSystem.GetCurrentGoalText();
        }
    }

    private static string GetLocalized(LocalizedString serialized, string key, string fallback)
    {
        string fromInspector = LocalizationSafe.Get(serialized, string.Empty);
        if (!string.IsNullOrEmpty(fromInspector))
            return fromInspector;

        return LocalizationSafe.Get(new LocalizedString(LocalizationTable, key), fallback);
    }

    private string ResolvePlanetName(string worldId)
    {
        WorldCatalogSO catalog = _worldCatalog != null ? _worldCatalog : FindLoadedWorldCatalog();
        WorldCatalogEntry[] worlds = catalog != null ? catalog.Worlds : WorldCatalog.DefaultEntries;
        if (worlds != null)
        {
            for (int i = 0; i < worlds.Length; i++)
            {
                WorldCatalogEntry entry = worlds[i];
                if (entry == null || !string.Equals(entry.WorldId, worldId, System.StringComparison.Ordinal))
                    continue;

                string displayName = entry.GetDisplayName();
                if (!string.IsNullOrEmpty(displayName))
                    return displayName;
            }
        }

        return string.IsNullOrEmpty(worldId) ? SaveWorldIds.World1 : worldId;
    }

    private static WorldCatalogSO FindLoadedWorldCatalog()
    {
        PlanetTravelPanelView panel = FindFirstObjectByType<PlanetTravelPanelView>(FindObjectsInactive.Include);
        if (panel != null && panel.WorldCatalog != null)
            return panel.WorldCatalog;

        WorldCatalogSO[] catalogs = Resources.FindObjectsOfTypeAll<WorldCatalogSO>();
        for (int i = 0; i < catalogs.Length; i++)
        {
            WorldCatalogSO catalog = catalogs[i];
            if (catalog != null && catalog.Worlds != null && catalog.Worlds.Length > 0)
                return catalog;
        }

        return null;
    }

    private void OnFocusButtonClicked()
    {
        if (_goalSystem == null || _cameraFocusController == null)
            return;

        GoalTravelHint hint = _goalSystem.GetCurrentTravelHint();
        if (hint.Kind != GoalTravelHintKind.None)
        {
            _cameraFocusController.FocusTravelHint(hint);
            return;
        }

        _cameraFocusController.FocusTarget(_goalSystem.GetCurrentGoalTargetId());
    }
}
