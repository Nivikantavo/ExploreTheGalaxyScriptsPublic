using System;
using System.Collections.Generic;

public class GoalOpenerRegistry
{
    public event Action<ResourcePaymentOpener> PaymentOpenerChanged;
    public event Action<ResourcePaymentOpener> OpenerRegistered;

    private readonly Dictionary<string, ResourcePaymentOpener> _openersById = new();

    public void Register(ResourcePaymentOpener opener)
    {
        if (opener == null || string.IsNullOrEmpty(opener.GoalOpenerId))
            return;

        _openersById[opener.GoalOpenerId] = opener;
        OpenerRegistered?.Invoke(opener);
    }

    public void Unregister(ResourcePaymentOpener opener)
    {
        if (opener == null || string.IsNullOrEmpty(opener.GoalOpenerId))
            return;

        if (_openersById.TryGetValue(opener.GoalOpenerId, out var registered) && registered == opener)
            _openersById.Remove(opener.GoalOpenerId);
    }

    public ResourcePaymentOpener Find(string goalOpenerId)
    {
        if (string.IsNullOrEmpty(goalOpenerId))
            return null;

        return _openersById.GetValueOrDefault(goalOpenerId);
    }

    public bool IsFilled(string goalOpenerId)
    {
        ResourcePaymentOpener opener = Find(goalOpenerId);
        return opener != null && opener.IsGoalComplete();
    }

    public void NotifyChanged(ResourcePaymentOpener opener)
    {
        if (opener == null)
            return;

        PaymentOpenerChanged?.Invoke(opener);
    }
}
