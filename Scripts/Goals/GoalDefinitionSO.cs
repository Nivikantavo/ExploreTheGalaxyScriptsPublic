using UnityEngine;
using UnityEngine.Localization;

public abstract class GoalDefinitionSO : ScriptableObject
{
    [SerializeField] private LocalizedString _title;
    [SerializeField] private string _targetId;
    [SerializeField] private Sprite _icon;

    public string Title => LocalizationSafe.Get(_title);
    public string TargetId => _targetId;
    public Sprite Icon => _icon;
    public virtual string RequiredSceneName => string.Empty;

    public abstract bool IsCompleted(GoalEvaluationContext context);
    public abstract string BuildDescription(GoalEvaluationContext context);
}
