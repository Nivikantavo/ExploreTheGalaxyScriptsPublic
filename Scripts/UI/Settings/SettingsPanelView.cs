using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;
using Zenject;

public class SettingsPanelView : MonoBehaviour
{
    [SerializeField] private GameObject _panelRoot;
    [SerializeField] private Slider _musicVolumeSlider;
    [SerializeField] private Slider _sfxVolumeSlider;
    [SerializeField] private TMP_Dropdown _languageDropdown;
    [SerializeField] private Button _openButton;
    [SerializeField] private Button _closeButton;
    [Tooltip("Used on menu scenes without Zenject AudioController.")]
    [SerializeField] private AudioClipsConfig _menuAudioClipsConfig;

    private IAudioService _audioService;
    private readonly List<Locale> _locales = new();
    private bool _isLanguageDropdownReady;
    private Coroutine _languageDropdownSetupRoutine;
    private bool _holdsPause;

    [Inject]
    private void Construct(IAudioService audioService)
    {
        _audioService = audioService;
    }

    private void Awake()
    {
        EnsureAudioService();
    }

    private void Start()
    {
        EnsureAudioService();
        Hide();
        SyncSlidersWithAudio();
        EnsureLanguageDropdownSetup();
    }

    private void OnEnable()
    {
        EnsureAudioService();

        if (_audioService != null)
        {
            _audioService.MusicVolumeChanged += OnMusicVolumeChanged;
            _audioService.SfxVolumeChanged += OnSfxVolumeChanged;
        }

        _openButton.onClick.AddListener(Show);
        _closeButton.onClick.AddListener(Hide);
        _musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeSliderChanged);
        _sfxVolumeSlider.onValueChanged.AddListener(OnSfxVolumeSliderChanged);
        if (_languageDropdown != null)
            _languageDropdown.onValueChanged.AddListener(OnLanguageChanged);
    }

    private void OnDisable()
    {
        if (_audioService != null)
        {
            _audioService.MusicVolumeChanged -= OnMusicVolumeChanged;
            _audioService.SfxVolumeChanged -= OnSfxVolumeChanged;
        }

        _openButton.onClick.RemoveListener(Show);
        _closeButton.onClick.RemoveListener(Hide);
        _musicVolumeSlider.onValueChanged.RemoveListener(OnMusicVolumeSliderChanged);
        _sfxVolumeSlider.onValueChanged.RemoveListener(OnSfxVolumeSliderChanged);
        if (_languageDropdown != null)
            _languageDropdown.onValueChanged.RemoveListener(OnLanguageChanged);
    }

    private void OnDestroy()
    {
        EndPause();

        if (_languageDropdownSetupRoutine != null)
        {
            StopCoroutine(_languageDropdownSetupRoutine);
            _languageDropdownSetupRoutine = null;
        }
    }

    public void Show()
    {
        EnsureAudioService();
        SyncSlidersWithAudio();
        EnsureLanguageDropdownSetup();
        RefreshLanguageDropdownSelection();

        if (_panelRoot != null)
            _panelRoot.SetActive(true);
        else
            gameObject.SetActive(true);

        BeginPause();
    }

    public void Hide()
    {
        if (_panelRoot != null)
            _panelRoot.SetActive(false);
        else
            gameObject.SetActive(false);

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

    private void EnsureAudioService()
    {
        if (_audioService != null)
            return;

        if (_menuAudioClipsConfig != null)
            _audioService = new MenuAudioVolumeService(_menuAudioClipsConfig);
    }

    private void EnsureLanguageDropdownSetup()
    {
        if (_isLanguageDropdownReady || _languageDropdown == null)
            return;

        if (_languageDropdownSetupRoutine != null)
            return;

        _languageDropdownSetupRoutine = StartCoroutine(SetupLanguageDropdownWhenReady());
    }

    private IEnumerator SetupLanguageDropdownWhenReady()
    {
        yield return LocalizationSettings.InitializationOperation;
        SetupLanguageDropdown();
        _languageDropdownSetupRoutine = null;
    }

    private void SetupLanguageDropdown()
    {
        if (_languageDropdown == null)
            return;

        if (!LocalizationSettings.InitializationOperation.IsDone)
            return;

        _locales.Clear();
        _languageDropdown.ClearOptions();

        var options = new List<TMP_Dropdown.OptionData>();
        IReadOnlyList<Locale> availableLocales = LocalizationSettings.AvailableLocales.Locales;
        for (int i = 0; i < availableLocales.Count; i++)
        {
            Locale locale = availableLocales[i];
            if (locale == null)
                continue;

            _locales.Add(locale);
            options.Add(new TMP_Dropdown.OptionData(LocaleApplicator.GetNativeDisplayName(locale)));
        }

        _languageDropdown.AddOptions(options);
        _isLanguageDropdownReady = true;
        RefreshLanguageDropdownSelection();
    }

    private void RefreshLanguageDropdownSelection()
    {
        if (!_isLanguageDropdownReady || _languageDropdown == null || _locales.Count == 0)
        {
            EnsureLanguageDropdownSetup();
            return;
        }

        Locale selected = LocalizationSettings.SelectedLocale;
        int index = 0;
        if (selected != null)
        {
            for (int i = 0; i < _locales.Count; i++)
            {
                if (_locales[i] == selected || _locales[i].Identifier == selected.Identifier)
                {
                    index = i;
                    break;
                }
            }
        }

        _languageDropdown.SetValueWithoutNotify(index);
    }

    private void OnLanguageChanged(int index)
    {
        if (index < 0 || index >= _locales.Count)
            return;

        Locale locale = _locales[index];
        LocalizationSettings.SelectedLocale = locale;
        LocaleSettingsStorage.SaveLocaleCode(locale.Identifier.Code);
    }

    private void OnMusicVolumeSliderChanged(float value)
    {
        _audioService.MusicVolume = value;
    }

    private void OnSfxVolumeSliderChanged(float value)
    {
        _audioService.SfxVolume = value;
    }

    private void OnMusicVolumeChanged(float value)
    {
        if (_musicVolumeSlider != null && !Mathf.Approximately(_musicVolumeSlider.value, value))
            _musicVolumeSlider.SetValueWithoutNotify(value);
    }

    private void OnSfxVolumeChanged(float value)
    {
        if (_sfxVolumeSlider != null && !Mathf.Approximately(_sfxVolumeSlider.value, value))
            _sfxVolumeSlider.SetValueWithoutNotify(value);
    }

    private void SyncSlidersWithAudio()
    {
        if (_audioService == null)
            return;

        _musicVolumeSlider.SetValueWithoutNotify(_audioService.MusicVolume);
        _sfxVolumeSlider.SetValueWithoutNotify(_audioService.SfxVolume);
    }
}
