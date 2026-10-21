using UnityEngine;
using UnityEngine.UI;
using Zenject;

/// <summary>
/// Opens the IAP shop when the HUD gem panel (BackgroundInapp) is clicked,
/// but only while payments are supported on the current platform and the tutorial is finished.
/// </summary>
[RequireComponent(typeof(Button))]
public class ShopHudOpenButton : MonoBehaviour
{
    [SerializeField] private ShopPanelView _shopPanel;
    [SerializeField] private bool _disableChildRaycasts = true;

    private Button _button;
    private IapService _iapService;
    private ISaveLoader _saveLoader;
    private TutorialHandler _tutorialHandler;
    private bool _registeredHudLock;

    [Inject]
    private void Construct(IapService iapService, ISaveLoader saveLoader)
    {
        _iapService = iapService;
        _saveLoader = saveLoader;
        _iapService.StateChanged += RefreshVisibility;
        _iapService.CatalogUpdated += RefreshVisibility;
    }

    private void Awake()
    {
        _button = GetComponent<Button>();
        _tutorialHandler = TutorialHudAccess.FindTutorialHandler();

        if (_shopPanel == null)
            _shopPanel = FindFirstObjectByType<ShopPanelView>();

        if (_disableChildRaycasts)
            DisableChildRaycasts();

        RegisterTutorialHudLock();
    }

    private void OnEnable()
    {
        if (_tutorialHandler == null)
            _tutorialHandler = TutorialHudAccess.FindTutorialHandler();

        if (_tutorialHandler != null)
            _tutorialHandler.OnTutorialResolved += RefreshVisibility;

        if (_button != null)
            _button.onClick.AddListener(OnClicked);

        RefreshVisibility();
    }

    private void OnDisable()
    {
        if (_tutorialHandler != null)
            _tutorialHandler.OnTutorialResolved -= RefreshVisibility;

        if (_button != null)
            _button.onClick.RemoveListener(OnClicked);
    }

    private void OnDestroy()
    {
        if (_iapService == null)
            return;

        _iapService.StateChanged -= RefreshVisibility;
        _iapService.CatalogUpdated -= RefreshVisibility;
    }

    private void OnClicked()
    {
        if (!TutorialHudAccess.IsMetaHudUnlocked(_saveLoader, _tutorialHandler))
            return;

        if (_iapService == null || !_iapService.IsSupported)
            return;

        _shopPanel?.Show();
    }

    private void RegisterTutorialHudLock()
    {
        if (_registeredHudLock)
            return;

        if (_tutorialHandler == null)
            _tutorialHandler = TutorialHudAccess.FindTutorialHandler();

        if (_tutorialHandler == null)
            return;

        _tutorialHandler.RegisterHudLock(gameObject);
        _registeredHudLock = true;
    }

    private void RefreshVisibility()
    {
        // Tutorial lock is owned by TutorialHandler via RegisterHudLock (SetActive).
        // After unlock, still hide when payments are unsupported.
        if (!TutorialHudAccess.IsMetaHudUnlocked(_saveLoader, _tutorialHandler))
            return;

        bool supported = _iapService != null && _iapService.IsSupported;
        gameObject.SetActive(supported);
    }

    private void DisableChildRaycasts()
    {
        Graphic[] graphics = GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            Graphic graphic = graphics[i];
            if (graphic == null || graphic.gameObject == gameObject)
                continue;

            graphic.raycastTarget = false;
        }
    }
}
