using UnityEngine;

public class ResourceCell
{
    public ResourceType ResourseType { get; private set; }
    public int Amount { get; private set; }

    public ResourceCell(ResourceType resourseType, int amount)
    {
        ResourseType = resourseType;
        Amount = amount;
    }

    public void AddAmount(int amount)
    {
        if(amount <= 0) return;
        Amount += amount;
    }

    public void RemoveAmount(int amount)
    {
        if(amount <= 0) return;
        Amount -= amount;
        if(Amount <= 0)
        {
            Amount = 0;
        }
    }
}
