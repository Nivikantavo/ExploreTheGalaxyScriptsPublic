using System;
using Playgama;
using Playgama.Modules.Platform;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CelebrationPopupView : MonoBehaviour
{
    /// <summary>
    /// Fired after the player closes a celebration and there are no more pending ones.
    /// Intended for interstitial ads (non-game action → ad).
    /// </summary>
    public event Action CelebrationClosed;

    [SerializeField] private CelebrationSystem _celebrationSystem;
    [SerializeField] private GameObject _panelRoot;
    [SerializeField] private Image _icon;
    [SerializeField] private TMP_Text _titleText;
    [SerializeField] private TMP_Text _bodyText;
    [SerializeField] private Button _closeButton;

    private bool _isShowing;
    private bool _pausedByPopup;

    private void Awake()
    {
        if (_celebrationSystem == null)
            _celebrationSystem = FindFirstObjectByType<CelebrationSystem>(FindObjectsInactive.Include);

        EnsurePanelBuilt();

        if (_panelRoot != null)
            _panelRoot.SetActive(false);
    }

    private void OnEnable()
    {
        if (_celebrationSystem != null)
            _celebrationSystem.CelebrationTriggered += OnCelebrationTriggered;

        if (_closeButton != null)
            _closeButton.onClick.AddListener(OnCloseClicked);

        TryShowNext();
    }

    private void OnDisable()
    {
        if (_celebrationSystem != null)
            _celebrationSystem.CelebrationTriggered -= OnCelebrationTriggered;

        if (_closeButton != null)
            _closeButton.onClick.RemoveListener(OnCloseClicked);

        if (_isShowing)
            Hide();
    }

    private void OnCelebrationTriggered(PendingCelebration _)
    {
        TryShowNext();
    }

    private void OnCloseClicked()
    {
        Hide();

        bool hasMorePending = _celebrationSystem != null && _celebrationSystem.HasPending;
        if (hasMorePending)
        {
            TryShowNext();
            return;
        }

        CelebrationClosed?.Invoke();
    }

    private void TryShowNext()
    {
        if (_isShowing || _celebrationSystem == null)
            return;

        if (!_celebrationSystem.TryDequeue(out PendingCelebration celebration) || celebration.Definition == null)
            return;

        Show(celebration);
    }

    private void Show(PendingCelebration celebration)
    {
        _isShowing = true;

        if (_titleText != null)
            _titleText.text = string.IsNullOrEmpty(celebration.Title)
                ? (celebration.Definition != null ? celebration.Definition.name : string.Empty)
                : celebration.Title;

        if (_bodyText != null)
            _bodyText.text = celebration.Body;

        if (_icon != null)
        {
            if (celebration.Icon != null)
            {
                _icon.sprite = celebration.Icon;
                _icon.enabled = true;
                _icon.gameObject.SetActive(true);
                _icon.SetNativeSize();
            }
            else
            {
                _icon.enabled = false;
            }
        }

        if (_panelRoot != null)
            _panelRoot.SetActive(true);

        PauseGameplay();
        NotifyPlayerGotAchievement();
    }

    private static void NotifyPlayerGotAchievement()
    {
        if (Bridge.instance == null || Bridge.platform == null)
            return;

        Bridge.platform.SendMessage(PlatformMessage.PlayerGotAchievement);
    }

    private void Hide()
    {
        _isShowing = false;

        if (_panelRoot != null)
            _panelRoot.SetActive(false);

        ResumeGameplay();
    }

    private void PauseGameplay()
    {
        if (_pausedByPopup)
            return;

        GameplayPause.Push();
        _pausedByPopup = true;
    }

    private void ResumeGameplay()
    {
        if (!_pausedByPopup)
            return;

        GameplayPause.Pop();
        _pausedByPopup = false;
    }

    private void EnsurePanelBuilt()
    {
        if (_panelRoot != null && _closeButton != null && _titleText != null && _bodyText != null)
            return;

        RectTransform root = GetComponent<RectTransform>();
        if (root == null)
            root = gameObject.AddComponent<RectTransform>();

        StretchFull(root);

        GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.transform.SetParent(transform, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        StretchFull(panelRect);
        Image dimmer = panel.GetComponent<Image>();
        dimmer.color = new Color(0f, 0f, 0f, 0.65f);
        dimmer.raycastTarget = true;

        GameObject card = new GameObject("Card", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        card.transform.SetParent(panel.transform, false);
        RectTransform cardRect = card.GetComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.sizeDelta = new Vector2(700f, 520f);
        card.GetComponent<Image>().color = new Color(0.16f, 0.18f, 0.22f, 1f);

        GameObject iconGo = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        iconGo.transform.SetParent(card.transform, false);
        RectTransform iconRect = iconGo.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 1f);
        iconRect.anchorMax = new Vector2(0.5f, 1f);
        iconRect.pivot = new Vector2(0.5f, 1f);
        iconRect.anchoredPosition = new Vector2(0f, -40f);
        iconRect.sizeDelta = new Vector2(160f, 160f);
        Image iconImage = iconGo.GetComponent<Image>();
        iconImage.preserveAspect = true;

        GameObject titleGo = new GameObject("Title", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        titleGo.transform.SetParent(card.transform, false);
        RectTransform titleRect = titleGo.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.1f, 0.45f);
        titleRect.anchorMax = new Vector2(0.9f, 0.62f);
        titleRect.offsetMin = Vector2.zero;
        titleRect.offsetMax = Vector2.zero;
        TextMeshProUGUI titleText = titleGo.GetComponent<TextMeshProUGUI>();
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.fontSize = 48f;
        titleText.color = Color.white;

        GameObject bodyGo = new GameObject("Body", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        bodyGo.transform.SetParent(card.transform, false);
        RectTransform bodyRect = bodyGo.GetComponent<RectTransform>();
        bodyRect.anchorMin = new Vector2(0.1f, 0.25f);
        bodyRect.anchorMax = new Vector2(0.9f, 0.45f);
        bodyRect.offsetMin = Vector2.zero;
        bodyRect.offsetMax = Vector2.zero;
        TextMeshProUGUI bodyText = bodyGo.GetComponent<TextMeshProUGUI>();
        bodyText.alignment = TextAlignmentOptions.Center;
        bodyText.fontSize = 32f;
        bodyText.color = new Color(0.9f, 0.9f, 0.9f, 1f);

        GameObject closeGo = new GameObject("CloseButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        closeGo.transform.SetParent(card.transform, false);
        RectTransform closeRect = closeGo.GetComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(0.5f, 0f);
        closeRect.anchorMax = new Vector2(0.5f, 0f);
        closeRect.pivot = new Vector2(0.5f, 0f);
        closeRect.anchoredPosition = new Vector2(0f, 36f);
        closeRect.sizeDelta = new Vector2(260f, 80f);
        closeGo.GetComponent<Image>().color = new Color(0.3f, 0.65f, 0.4f, 1f);
        Button closeButton = closeGo.GetComponent<Button>();

        GameObject closeLabelGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        closeLabelGo.transform.SetParent(closeGo.transform, false);
        RectTransform closeLabelRect = closeLabelGo.GetComponent<RectTransform>();
        StretchFull(closeLabelRect);
        TextMeshProUGUI closeLabel = closeLabelGo.GetComponent<TextMeshProUGUI>();
        closeLabel.text = "OK";
        closeLabel.alignment = TextAlignmentOptions.Center;
        closeLabel.fontSize = 36f;
        closeLabel.color = Color.white;

        _panelRoot = panel;
        _icon = iconImage;
        _titleText = titleText;
        _bodyText = bodyText;
        _closeButton = closeButton;
    }

    private static void StretchFull(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }
}
