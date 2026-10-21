using System;
using ECM2;
using UnityEngine;

public class PlayerAnimation : MonoBehaviour
{
    private const string Velocity = "Velocity";
    private const string Attack = "Attack";
    private const string Swim = "Swim";

    [SerializeField] private Animator _animator;
    [SerializeField] private Character _character;
    [SerializeField] private Player _player;
    [SerializeField] private InstrumentViewer _instrumentViewer;

    private void OnEnable() 
    {
        _player.Hit += OnHit;
    }

    private void OnDisable() 
    {
        _player.Hit -= OnHit;
    }

    private void OnHit()
    {
        _animator.SetTrigger(Attack);
    }

    private void Update() 
    {
        _animator.SetFloat(Velocity, _character.speed);
        _animator.SetBool(Swim, _character.IsSwimming());
    }

    public void StartHit()
    {
        _instrumentViewer.ResetInstrument(_player.CurrentInstument);
    }

    public void EndHit()
    {
        //_instrumentViewer.HideInstrument(_player.CurrentInstument);
    }
}
