using UnityEngine;

public interface IResourceKeeper
{
    public ResourceType ResourseType {get;}
    public int KnockOutResources(Instrument instrument, Transform pickupTarget);
}
