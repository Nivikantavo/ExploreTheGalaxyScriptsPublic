using UnityEngine;
using UnityEngine.Localization;

public abstract class AchievementDefinitionSO : ScriptableObject
{
    [SerializeField] private string _id;
    [SerializeField] protected LocalizedString _title;
    [SerializeField] protected LocalizedString _body;
    [SerializeField] private Sprite _icon;

    public string Id => string.IsNullOrEmpty(_id) ? name : _id;

    public virtual string Title
    {
        get
        {
            string localized = LocalizationSafe.Get(_title);
            return string.IsNullOrEmpty(localized) ? name : localized;
        }
    }

    public virtual string Body => LocalizationSafe.Get(_body);
    public Sprite Icon => _icon;

    public abstract bool IsSatisfied(AchievementEvaluationContext context);

    public virtual AchievementListEntry CreateListEntry(bool isAchieved)
    {
        return new AchievementListEntry(Id, Title, Body, Icon, isAchieved);
    }
}
