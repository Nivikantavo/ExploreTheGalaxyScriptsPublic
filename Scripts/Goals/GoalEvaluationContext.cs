public class GoalEvaluationContext
{
    public GoalEvaluationContext(
        Inventory inventory,
        IslandProgressRegistry islandRegistry,
        GoalOpenerRegistry goalOpenerRegistry,
        NpcRescueRegistry npcRescueRegistry = null)
    {
        Inventory = inventory;
        IslandRegistry = islandRegistry;
        GoalOpenerRegistry = goalOpenerRegistry;
        NpcRescueRegistry = npcRescueRegistry;
    }

    public Inventory Inventory { get; }
    public IslandProgressRegistry IslandRegistry { get; }
    public GoalOpenerRegistry GoalOpenerRegistry { get; }
    public NpcRescueRegistry NpcRescueRegistry { get; }
}
