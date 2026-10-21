using System;
using System.Collections;
using System.Collections.Generic;
using ECM2;
using UnityEngine;
using Zenject;

public class Player : MonoBehaviour, IHealthObserver
{
    public event Action<float> HealthValueChange;
    public event Action<float> OnHealthInitialized;
    public event Action Hit;
    public event Action Died;

    public InstrumentType CurrentInstument {get; private set;}
    public CameraTarget CameraTarget => _cameraTarget;
    public Inventory Inventory => _inventory;
    public float MaxHealth => _health.MaxHealth;
    public bool IsDead { get; private set; }

    [SerializeField] private CameraTarget _cameraTarget;
    [SerializeField] private Transform _resourseHolderDetector;
    [SerializeField] private float _resourseHolderDetectorRadius = 0.7f;
    [SerializeField] private InstrumentViewer _instrumentViewer;

    [Header("Attack"), Space]
    [SerializeField] private float _attackCooldown = 0.5f;

    private Health _health;
    private Inventory _inventory;
    private LifecycleEventBus _lifecycleEventBus;
    private PlayerInitConfig _config;
    private PlayerController _playerController;
    private Character _character;
    private readonly int _resourseHolderLayer = 1 << 6;
    private float _attackTimer = 0f;
    private Coroutine _healthRegenIdleCoroutine;
    private Coroutine _healthRegenCoroutine;

    public bool TrySpendResource(ResourceType resourseType, int amount)
    {
        if(_inventory.GetResourceAmount(resourseType) < amount)
        {
            return false;
        }
        _inventory.RemoveResource(resourseType, amount);
        return true;
    }

    public int GetResourceAmount(ResourceType resourseType)
    {
        return _inventory.GetResourceAmount(resourseType);
    }

    public void TakeDamage(int damage)
    {
        if (IsDead || damage <= 0)
            return;

        _health.TakeDamage(damage);
        HealthValueChange?.Invoke(_health.CurrentHealth);
        RestartHealthRegenIdleWatch();
    }

    public void SetGameplayControlEnabled(bool enabled)
    {
        if (_playerController == null)
            return;

        if (!enabled)
        {
            _playerController.Input?.Disable();
            _playerController.enabled = false;
            return;
        }

        _playerController.enabled = true;
        _playerController.Input?.Enable();
    }

    public void RespawnAt(Vector3 position)
    {
        _health.RestoreHealth();
        gameObject.SetActive(true);

        if (_character != null)
            _character.SetPosition(position);
        else
            transform.position = position;

        SetGameplayControlEnabled(true);

        IsDead = false;
        HealthValueChange?.Invoke(_health.CurrentHealth);
        OnHealthInitialized?.Invoke(_health.MaxHealth);
        _lifecycleEventBus.Publish(new LifecycleEventData(LifecycleEventType.PlayerSpawned, position));
        RestartHealthRegenIdleWatch();
    }

    [Inject]
    private void Construct(Inventory inventory, PlayerInitConfig config, LifecycleEventBus lifecycleEventBus)
    {
        _inventory = inventory;
        _config = config;
        _health = new Health(config.MaxHealth);
        _lifecycleEventBus = lifecycleEventBus;
        _health.OnZeroHealth += OnPlayerDied;
        _inventory.EquipmentChanged += OnEquipmentChanged;
    }

    private void Awake()
    {
        _playerController = GetComponent<PlayerController>();
        _character = GetComponent<Character>();
    }

    private void OnDestroy()
    {
        if (_health != null)
            _health.OnZeroHealth -= OnPlayerDied;

        if (_inventory != null)
            _inventory.EquipmentChanged -= OnEquipmentChanged;

        StopHealthRegen();
    }

    private void Start()
    {
        RefreshEquipmentHealth();
        _lifecycleEventBus.Publish(new LifecycleEventData(LifecycleEventType.PlayerSpawned, transform.position));
        OnHealthInitialized?.Invoke(_health.MaxHealth);
        RestartHealthRegenIdleWatch();
    }

    public void RefreshEquipmentHealth()
    {
        if (_health == null || _config == null || _inventory == null)
            return;

        float newMaxHealth = _config.MaxHealth + _inventory.GetEquippedHealthBonus();
        if (Mathf.Approximately(newMaxHealth, _health.MaxHealth))
            return;

        _health.SetMaxHealth(newMaxHealth);
        HealthValueChange?.Invoke(_health.CurrentHealth);
        OnHealthInitialized?.Invoke(_health.MaxHealth);
    }

    private void OnEquipmentChanged(Equipment _)
    {
        RefreshEquipmentHealth();
    }

    public void DieFromDrowning()
    {
        if (IsDead)
            return;

        _health.OnZeroHealth -= OnPlayerDied;
        float remainingHealth = _health.CurrentHealth;
        _health.TakeDamage(remainingHealth > 0f ? remainingHealth : 1f);
        _health.OnZeroHealth += OnPlayerDied;
        HealthValueChange?.Invoke(_health.CurrentHealth);

        FinishDeath(LifecycleEventType.PlayerDrowned);
    }

    private void OnPlayerDied()
    {
        FinishDeath(LifecycleEventType.PlayerDied);
    }

    private void FinishDeath(LifecycleEventType deathEventType)
    {
        if (IsDead)
            return;

        IsDead = true;
        StopHealthRegen();

        SetGameplayControlEnabled(false);

        _lifecycleEventBus.Publish(new LifecycleEventData(deathEventType, transform.position));
        Died?.Invoke();
    }

    private void RestartHealthRegenIdleWatch()
    {
        if (IsDead || !isActiveAndEnabled)
            return;

        StopHealthRegen();
        _healthRegenIdleCoroutine = StartCoroutine(HealthRegenIdleWatch());
    }

    private void StopHealthRegen()
    {
        if (_healthRegenIdleCoroutine != null)
        {
            StopCoroutine(_healthRegenIdleCoroutine);
            _healthRegenIdleCoroutine = null;
        }

        if (_healthRegenCoroutine != null)
        {
            StopCoroutine(_healthRegenCoroutine);
            _healthRegenCoroutine = null;
        }
    }

    private IEnumerator HealthRegenIdleWatch()
    {
        yield return new WaitForSeconds(_config.HealthRegenDelaySeconds);

        _healthRegenIdleCoroutine = null;

        if (IsDead || _health.CurrentHealth >= _health.MaxHealth)
            yield break;

        _healthRegenCoroutine = StartCoroutine(RegenerateHealth());
    }

    private IEnumerator RegenerateHealth()
    {
        float regenPercentPerSecond = _config.HealthRegenPercentPerSecond;
        if (regenPercentPerSecond <= 0f)
        {
            _healthRegenCoroutine = null;
            yield break;
        }

        while (!IsDead && _health.CurrentHealth < _health.MaxHealth)
        {
            float regenPerSecond = _health.MaxHealth * regenPercentPerSecond * 0.01f;
            float restoreAmount = regenPerSecond * Time.deltaTime;
            _health.RestoreHealth(restoreAmount);
            HealthValueChange?.Invoke(_health.CurrentHealth);
            yield return null;
        }

        _healthRegenCoroutine = null;
    }

    private void Update() 
    {
        if (IsDead)
            return;

        _attackTimer += Time.deltaTime;
        if(_attackTimer >= _attackCooldown)
        {
            _attackTimer = 0f;
            HitArea();
        }
    }

    private void HitArea()
    {
        if(HasTargets())
        {
            Hit?.Invoke();
        }
        else
        {
            if(CurrentInstument != InstrumentType.None)
            {
                _instrumentViewer.HideInstrument(CurrentInstument);
                CurrentInstument = InstrumentType.None;
            }
            
        }
    }

    private bool HasTargets()
    {
        if(HasEnemyNear())
        {
            if(InstrumentType.Sword != CurrentInstument)
            {
                ChangeInstrument(InstrumentType.Sword);
            }
            return true;
        }
        else
        {
            List<ResourseHolderView> resourseHolderViews = FindResourseHolders();
            if(resourseHolderViews.Count <= 0)
            {
                return false;
            }
            
            InstrumentType instrumentType = GetInstrumentType(resourseHolderViews);
            if(instrumentType != CurrentInstument)
            {
                ChangeInstrument(instrumentType);
            }
            return true;
        }
    }

    private List<ResourseHolderView> FindResourseHolders()
    {
        Collider[] resourseHolders = Physics.OverlapSphere(_resourseHolderDetector.position, _resourseHolderDetectorRadius, _resourseHolderLayer);
        List<ResourseHolderView> resourseHolderViews = new List<ResourseHolderView>();
        foreach(Collider collider in resourseHolders)
        {
            if(collider.TryGetComponent(out ResourseHolderView resourseHolderView))
            {
                resourseHolderViews.Add(resourseHolderView);
            }
        }

        return resourseHolderViews;
    }

    private bool HasEnemyNear()
    {
        Collider[] enemies = Physics.OverlapSphere(_resourseHolderDetector.position, _resourseHolderDetectorRadius, _resourseHolderLayer);
        
        foreach(Collider collider in enemies)
        {
            if(collider.TryGetComponent(out Enemy enemy))
            {
                return true;
            }
        }

        return false;
    }

    private InstrumentType GetInstrumentType(List<ResourseHolderView> resourseHolderViews)
    {
        ResourseHolderView closestResourseHolder = null;
        float closestDistance = float.MaxValue;
        foreach(ResourseHolderView resourseHolderView in resourseHolderViews)
        {
            Vector3 distance = resourseHolderView.transform.position - transform.position;
            if(distance.magnitude < closestDistance)
            {
                closestDistance = distance.magnitude;
                closestResourseHolder = resourseHolderView;
            }
        }
        return closestResourseHolder != null ? closestResourseHolder.InstrumentType : InstrumentType.Axe;
    }

    private void ChangeInstrument(InstrumentType newInstrument)
    {
        if(CurrentInstument != InstrumentType.None)
            _instrumentViewer.HideInstrument(CurrentInstument);
        CurrentInstument = newInstrument;
        _instrumentViewer.ShowInstrument(CurrentInstument);
    }

    private void OnDrawGizmos() 
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(_resourseHolderDetector.position, _resourseHolderDetectorRadius);
    }
}
