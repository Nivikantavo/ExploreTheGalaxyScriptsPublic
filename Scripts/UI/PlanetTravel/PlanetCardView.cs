using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlanetCardView : MonoBehaviour
{
    [SerializeField] private Image _icon;
    [SerializeField] private TMP_Text _nameText;
    [SerializeField] private Button _goButton;
    [SerializeField] private TMP_Text _goButtonText;
    [SerializeField] private GameObject _lockOverlay;

    private string _worldId;
    private Action<string> _onGoClicked;

    private void OnEnable()
    {
        if (_goButton != null)
            _goButton.onClick.AddListener(OnGoClicked);
    }

    private void OnDisable()
    {
        if (_goButton != null)
            _goButton.onClick.RemoveListener(OnGoClicked);
    }

    public void Bind(
        string worldId,
        string displayName,
        Sprite icon,
        string goLabel,
        bool unlocked,
        bool canTravel,
        Action<string> onGoClicked)
    {
        _worldId = worldId;
        _onGoClicked = onGoClicked;

        if (_nameText != null)
            _nameText.text = displayName ?? string.Empty;

        if (_goButtonText != null)
            _goButtonText.text = goLabel ?? string.Empty;

        if (_icon != null)
        {
            _icon.sprite = icon;
            _icon.enabled = icon != null;
        }

        if (_lockOverlay != null)
            _lockOverlay.SetActive(!unlocked);

        if (_goButton != null)
            _goButton.interactable = canTravel;
    }

    private void OnGoClicked()
    {
        if (string.IsNullOrEmpty(_worldId))
            return;

        _onGoClicked?.Invoke(_worldId);
    }
}
