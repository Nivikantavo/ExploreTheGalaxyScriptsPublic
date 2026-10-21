using ECM2;
using UnityEngine;

public class EnemyAnimation : MonoBehaviour
{
    private const string IsRunning = "IsRunning";
    private const string Attack = "Attack";

    [SerializeField] private Enemy _enemy;
    private Character _character;
    private Animator _animator;

    private void Awake()
    {
        _character = GetComponentInParent<Character>();
        _animator = _character.GetAnimator();
    }

    private void OnEnable() 
    {
        _enemy.Hit += OnHit;
    }

    private void OnDisable() 
    {
        _enemy.Hit -= OnHit;
    }

    private void OnHit()
    {
        _animator.SetTrigger(Attack);
    }

    private void Update() 
    {
        _animator.SetBool(IsRunning, _character.speed > 0.01f);
    }

    public void StartHit()
    {
        _enemy.EnableSword();
    }

    public void EndHit()
    {
        _enemy.DisableSword();
    }
}
