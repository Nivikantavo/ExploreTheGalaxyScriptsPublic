using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;

public abstract class CelebrationDefinitionSO : ScriptableObject
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

    public abstract bool IsSatisfied(CelebrationEvaluationContext context);

    public virtual void CollectNewlySatisfied(
        CelebrationEvaluationContext context,
        HashSet<string> triggeredIds,
        List<PendingCelebration> results)
    {
        string id = Id;
        if (string.IsNullOrEmpty(id) || triggeredIds.Contains(id))
            return;

        if (!IsSatisfied(context))
            return;

        results.Add(new PendingCelebration(this, id, Title, Body, Icon));
    }
}
