using System;
using System.Collections.Generic;
using System.Linq;

public class IslandProgressRegistry
{
    public event Action<IslandProgressHandler> IslandRegistered;
    public event Action<IslandProgressHandler> IslandUnregistered;

    private readonly List<IslandProgressHandler> _islands = new();

    public IReadOnlyList<IslandProgressHandler> Islands =>
        _islands.OrderBy(island => island.IslandIndex).ToList();

    public void Register(IslandProgressHandler island)
    {
        if (island == null || _islands.Contains(island))
            return;

        _islands.Add(island);
        IslandRegistered?.Invoke(island);
    }

    public void Unregister(IslandProgressHandler island)
    {
        if (!_islands.Remove(island))
            return;

        IslandUnregistered?.Invoke(island);
    }
}
