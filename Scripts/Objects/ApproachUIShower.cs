using System;
using UnityEngine;

public class ApproachUIShower : MonoBehaviour
{
    public event Action OnPlayerEnter;
    [SerializeField] private WorldSpaceUI _uiToShow;

    private Collider _trigger;
    private int _playerBodyOverlaps;

    private void Awake()
    {
        _trigger = GetComponent<Collider>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayerBody(other))
            return;

        _playerBodyOverlaps++;
        if (_playerBodyOverlaps == 1)
            ShowUi();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsPlayerBody(other))
            return;

        _playerBodyOverlaps = Mathf.Max(0, _playerBodyOverlaps - 1);
        if (_playerBodyOverlaps == 0 && _uiToShow != null)
            _uiToShow.Hide();
    }

    public void TryShowIfPlayerInside()
    {
        if (!isActiveAndEnabled || _uiToShow == null)
            return;

        if (_trigger == null)
            _trigger = GetComponent<Collider>();

        if (_trigger == null || !_trigger.enabled)
            return;

        Vector3 center = GetTriggerWorldCenter();
        float radius = GetTriggerWorldRadius();
        Collider[] hits = Physics.OverlapSphere(center, radius, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < hits.Length; i++)
        {
            if (!IsPlayerBody(hits[i]))
                continue;

            if (_playerBodyOverlaps == 0)
                _playerBodyOverlaps = 1;

            ShowUi();
            return;
        }
    }

    private void ShowUi()
    {
        _uiToShow.Show();
        OnPlayerEnter?.Invoke();
    }

    private static bool IsPlayerBody(Collider other)
    {
        // Weapon hitboxes are triggers on the animated hand. During an attack they
        // sweep through this volume and must not open or close the UI.
        if (other == null || other.isTrigger)
            return false;

        return TryGetPlayer(other) != null;
    }

    private static Player TryGetPlayer(Collider other)
    {
        if (other.TryGetComponent(out Player player))
            return player;

        return other.GetComponentInParent<Player>();
    }

    private Vector3 GetTriggerWorldCenter()
    {
        if (_trigger is SphereCollider sphere)
            return transform.TransformPoint(sphere.center);

        return _trigger.bounds.center;
    }

    private float GetTriggerWorldRadius()
    {
        if (_trigger is SphereCollider sphere)
        {
            Vector3 scale = transform.lossyScale;
            float maxScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            return sphere.radius * maxScale;
        }

        Vector3 extents = _trigger.bounds.extents;
        return Mathf.Max(extents.x, extents.y, extents.z);
    }
}
