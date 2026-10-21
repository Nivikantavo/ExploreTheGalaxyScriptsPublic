public class AchievementEvaluationContext
{
    public AchievementEvaluationContext(
        Inventory inventory,
        IslandProgressRegistry islandRegistry,
        GoalOpenerRegistry goalOpenerRegistry)
    {
        Inventory = inventory;
        IslandRegistry = islandRegistry;
        GoalOpenerRegistry = goalOpenerRegistry;
    }

    public Inventory Inventory { get; }
    public IslandProgressRegistry IslandRegistry { get; }
    public GoalOpenerRegistry GoalOpenerRegistry { get; }

    public int EnemiesKilled => Inventory != null ? Inventory.EnemiesKilled : 0;

    public int GetLifetimeAmount(ResourceType resourceType)
    {
        return Inventory != null ? Inventory.GetLifetimeAmount(resourceType) : 0;
    }
}
