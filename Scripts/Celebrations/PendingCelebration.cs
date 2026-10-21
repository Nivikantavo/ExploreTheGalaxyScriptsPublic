using UnityEngine;

public readonly struct PendingCelebration
{
    public PendingCelebration(
        CelebrationDefinitionSO definition,
        string triggerId,
        string title,
        string body,
        Sprite icon)
    {
        Definition = definition;
        TriggerId = triggerId;
        Title = title;
        Body = body;
        Icon = icon;
    }

    public CelebrationDefinitionSO Definition { get; }
    public string TriggerId { get; }
    public string Title { get; }
    public string Body { get; }
    public Sprite Icon { get; }
}
