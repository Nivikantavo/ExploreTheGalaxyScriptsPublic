using UnityEngine;

public class SceneTravelGate : MonoBehaviour
{
    [SerializeField] private SceneTravelDestination _destination;
    [SerializeField] private Transform _enterPoint;
    [SerializeField] private Transform _exitPoint;
    [SerializeField] private float _exitWorldOffset = 2f;

    private float _suppressUntilUnscaled;

    public SceneTravelDestination Destination => _destination;

    public Vector3 GetReturnPosition()
    {
        if (_exitPoint != null)
            return _exitPoint.position;

        return transform.position + Vector3.forward * _exitWorldOffset;
    }

    public Quaternion GetReturnRotation()
    {
        if (_exitPoint != null)
            return _exitPoint.rotation;

        return Quaternion.identity;
    }

    public Vector3 GetRunInTarget()
    {
        return _enterPoint.position;
    }

    public void SuppressFor(float seconds)
    {
        _suppressUntilUnscaled = Time.unscaledTime + Mathf.Max(0f, seconds);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_destination == null || Time.unscaledTime < _suppressUntilUnscaled)
            return;

        if (other.GetComponentInParent<Player>() == null)
            return;

        SceneTravelService service = SceneTravelService.Instance;
        if (service == null)
            return;

        service.Travel(_destination, GetReturnPosition(), GetReturnRotation(), GetRunInTarget());
    }
}
