using PrimeTween;
using UnityEngine;

public class RepairAnimation : MonoBehaviour
{
    [SerializeField] private Transform _target;
    [SerializeField] private float _repairDuration = 0.5f;
    private IReparable _reparable;
    private Vector3 _startScale;

    private void Awake()
    {
        _reparable = GetComponentInParent<IReparable>();
        if(_target == null)
        {
            _target = transform;
        }
        _startScale = _target.localScale;
    }

    private void OnEnable() 
    {
        _reparable.OnRepairCompleted += PlayRepairAnimation;
    }

    private void OnDisable() 
    {
        _reparable.OnRepairCompleted -= PlayRepairAnimation;
    }

    private void PlayRepairAnimation()
    {
        Tween.StopAll(_target);
        _target.localScale = Vector3.zero;
        _target.localEulerAngles = Vector3.zero;
        //Tween.LocalEulerAngles(_target, Vector3.zero, new Vector3(0, 360, 0), _repairDuration, Ease.InOutBack);

        float step = _repairDuration * 0.25f;
        Sequence.Create()
            .Chain(Tween.LocalEulerAngles(_target, Vector3.zero, new Vector3(0f, -8f, 0f), step * 0.4f, Ease.OutQuad))
            .Chain(Tween.LocalEulerAngles(_target, new Vector3(0f, -30f, -20f), new Vector3(0f, 6f, 0f), step, Ease.InOutSine))
            .Chain(Tween.LocalEulerAngles(_target, new Vector3(0f, 16f, 15f), new Vector3(0f, -2.5f, 0f), step * 0.9f, Ease.InOutSine))
            .Chain(Tween.LocalEulerAngles(_target, new Vector3(0f, -12.5f, -10f), Vector3.zero, step * 1.1f, Ease.OutSine));

        Tween.Scale(_target, _startScale, _repairDuration, Ease.InOutSine);
    }
}
