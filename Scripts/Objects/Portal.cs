using UnityEngine;

public class Portal : MonoBehaviour
{
    [SerializeField] private PlanetTravelPanelView _planetTravelPanel;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.TryGetComponent(out Player _))
            return;

        if (_planetTravelPanel == null)
        {
            Debug.LogError("Portal: PlanetTravelPanelView is not assigned.", this);
            return;
        }

        _planetTravelPanel.Show();
    }
}
