using UnityEngine;

public readonly struct AchievementListEntry
{
    public AchievementListEntry(
        string id,
        string title,
        string body,
        Sprite icon,
        bool isAchieved)
    {
        Id = id;
        Title = title;
        Body = body;
        Icon = icon;
        IsAchieved = isAchieved;
    }

    public string Id { get; }
    public string Title { get; }
    public string Body { get; }
    public Sprite Icon { get; }
    public bool IsAchieved { get; }
}
