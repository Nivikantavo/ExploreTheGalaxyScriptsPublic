using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zenject;

public class InstrumentPickup : MonoBehaviour
{
    [SerializeField] private InstrumentType _instrumentType = InstrumentType.Axe;
    [SerializeField, Min(1)] private int _level = 1;
    [SerializeField] private Transform _visual;
    [SerializeField] private float _bobAmplitude = 0.25f;
    [SerializeField] private float _bobSpeed = 2f;
    [SerializeField] private float _rotateSpeed = 45f;

    private static bool _saveApplied;
    private static readonly List<InstrumentPickup> _instances = new();

    private Inventory _inventory;
    private AllInstruments _allInstruments;
    private Collider _trigger;
    private Vector3 _visualBaseLocalPosition;
    private bool _resolved;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _saveApplied = false;
        _instances.Clear();
    }

    [Inject]
    private void Construct(Inventory inventory, AllInstruments allInstruments)
    {
        _inventory = inventory;
        _allInstruments = allInstruments;
    }

    private void Awake()
    {
        _trigger = GetComponent<Collider>();
        if (_visual != null)
            _visualBaseLocalPosition = _visual.localPosition;
    }

    private void OnEnable()
    {
        if (!_instances.Contains(this))
            _instances.Add(this);
    }

    private void Start()
    {
        if (_saveApplied && IsAlreadyOwned())
        {
            Hide();
            return;
        }

        SpawnVisual();

        if (_saveApplied)
            RefreshAfterSave();
    }

    private void OnDisable()
    {
        _instances.Remove(this);
    }

    private void Update()
    {
        if (_resolved || _visual == null)
            return;

        float bob = Mathf.Sin(Time.time * _bobSpeed) * _bobAmplitude;
        _visual.localPosition = _visualBaseLocalPosition + Vector3.up * bob;
        _visual.Rotate(Vector3.up, _rotateSpeed * Time.deltaTime, Space.Self);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!_saveApplied || _resolved)
            return;

        if (TryGetPlayer(other) == null)
            return;

        TryResolve();
    }

    public static void NotifySaveApplied()
    {
        _saveApplied = true;

        InstrumentPickup[] snapshot = _instances.ToArray();
        for (int i = 0; i < snapshot.Length; i++)
        {
            if (snapshot[i] != null)
                snapshot[i].RefreshAfterSave();
        }
    }

    private void RefreshAfterSave()
    {
        if (_resolved)
            return;

        if (IsAlreadyOwned())
        {
            Hide();
            return;
        }

        if (IsPlayerInside())
            TryResolve();
    }

    private void TryResolve()
    {
        if (_resolved || _inventory == null)
            return;

        if (IsAlreadyOwned())
        {
            Hide();
            RefreshRemaining();
            return;
        }

        InstrumentConfig config = FindConfig();
        if (config == null)
        {
            Debug.LogError($"InstrumentPickup: no config for {_instrumentType} level {_level}.", this);
            return;
        }

        _inventory.UpgradeInstrument(config);
        RefreshUpgraders();
        Hide();
        RefreshRemaining();
    }

    private static void RefreshRemaining()
    {
        InstrumentPickup[] snapshot = _instances.ToArray();
        for (int i = 0; i < snapshot.Length; i++)
        {
            InstrumentPickup pickup = snapshot[i];
            if (pickup != null && !pickup._resolved && pickup.IsAlreadyOwned())
                pickup.Hide();
        }
    }

    private bool IsAlreadyOwned()
    {
        if (_inventory == null)
            return false;

        Instrument instrument = _inventory.Instruments.FirstOrDefault(item => item.Type == _instrumentType);
        return instrument != null && instrument.Level >= _level;
    }

    private InstrumentConfig FindConfig()
    {
        if (_allInstruments == null)
            return null;

        return _allInstruments.Instruments.FirstOrDefault(
            item => item != null && item.Type == _instrumentType && item.Level == _level);
    }

    private void SpawnVisual()
    {
        if (_visual == null)
            return;

        InstrumentConfig config = FindConfig();
        if (config == null || config.InstrumentObj == null)
        {
            Debug.LogError($"InstrumentPickup: no config for {_instrumentType} level {_level}.", this);
            return;
        }

        GameObject instance = Instantiate(config.InstrumentObj, _visual);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;

        Collider[] colliders = instance.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
            colliders[i].enabled = false;
    }

    private void Hide()
    {
        _resolved = true;
        gameObject.SetActive(false);
    }

    private void RefreshUpgraders()
    {
        InstrumentUpgrader[] upgraders = FindObjectsByType<InstrumentUpgrader>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < upgraders.Length; i++)
        {
            InstrumentUpgrader upgrader = upgraders[i];
            if (upgrader != null && upgrader.InstrumentType == _instrumentType)
                upgrader.RefreshFromInventory();
        }
    }

    private bool IsPlayerInside()
    {
        if (!isActiveAndEnabled)
            return false;

        if (_trigger == null)
            _trigger = GetComponent<Collider>();

        if (_trigger == null || !_trigger.enabled)
            return false;

        Vector3 center = GetTriggerWorldCenter();
        float radius = GetTriggerWorldRadius();
        Collider[] hits = Physics.OverlapSphere(center, radius, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < hits.Length; i++)
        {
            if (TryGetPlayer(hits[i]) != null)
                return true;
        }

        return false;
    }

    private static Player TryGetPlayer(Collider other)
    {
        if (other == null)
            return null;

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

    private void OnValidate()
    {
        if (_level < 1)
            _level = 1;
    }
}
