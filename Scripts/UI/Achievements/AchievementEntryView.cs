using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AchievementEntryView : MonoBehaviour
{
    [SerializeField] private Image _icon;
    [SerializeField] private TMP_Text _titleText;
    [SerializeField] private TMP_Text _bodyText;
    [SerializeField] private GameObject _checkmark;

    public void Bind(AchievementListEntry entry)
    {
        if (_titleText != null)
            _titleText.text = entry.Title ?? string.Empty;

        if (_bodyText != null)
            _bodyText.text = entry.Body ?? string.Empty;

        if (_icon != null)
        {
            if (entry.Icon != null)
            {
                _icon.sprite = entry.Icon;
                _icon.enabled = true;
                _icon.gameObject.SetActive(true);
            }
            else
            {
                _icon.enabled = false;
            }
        }

        if (_checkmark != null)
            _checkmark.SetActive(entry.IsAchieved);
    }
}
