using UnityEngine;

public class IslandMainPathTrigger : MonoBehaviour
{
    [SerializeField] private IslandProgressHandler _island;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.TryGetComponent<Player>(out _))
            return;

        _island?.MarkMainPathReached();
    }
}
