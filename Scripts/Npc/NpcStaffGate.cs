using UnityEngine;

public class NpcStaffGate : MonoBehaviour, INpcStaffable
{
    [SerializeField] private ApproachUIShower _approachUIShower;
    [SerializeField] private Collider _interactionCollider;
    [SerializeField] private OpenHexPriceView _requirementViewPrefab;
    [SerializeField] private float _requirementHeight = 2f;

    private bool _isStaffed;
    private bool _requirementBound;
    private Sprite _requiredWorkerIcon;
    private OpenHexPriceView _requirementView;

    public bool IsStaffed => _isStaffed;

    private void Reset()
    {
        ResolveReferences();
    }

    private void Awake()
    {
        ResolveReferences();
        ApplyInteractionEnabled();
    }

    public void BindRequiredWorker(Sprite icon)
    {
        _requiredWorkerIcon = icon;
        _requirementBound = true;
        RefreshRequirementView();
    }

    public void SetStaffed(bool staffed)
    {
        bool changed = _isStaffed != staffed;
        _isStaffed = staffed;

        if (changed)
            ApplyInteractionEnabled();

        if (_isStaffed)
            _approachUIShower?.TryShowIfPlayerInside();

        RefreshRequirementView();
    }

    private void ResolveReferences()
    {
        if (_approachUIShower == null)
            _approachUIShower = GetComponentInChildren<ApproachUIShower>(true);

        if (_interactionCollider == null && _approachUIShower != null)
            _interactionCollider = _approachUIShower.GetComponent<Collider>();
    }

    private void ApplyInteractionEnabled()
    {
        if (_approachUIShower != null)
            _approachUIShower.enabled = _isStaffed;

        if (_interactionCollider != null)
            _interactionCollider.enabled = _isStaffed;
    }

    private void RefreshRequirementView()
    {
        if (!_requirementBound)
            return;

        if (_isStaffed || _requiredWorkerIcon == null)
        {
            if (_requirementView != null)
                _requirementView.Hide();
            return;
        }

        EnsureRequirementView();
        _requirementView?.ShowRequirement(_requiredWorkerIcon);
    }

    private void EnsureRequirementView()
    {
        if (_requirementView != null || _requirementViewPrefab == null)
            return;

        Transform holder = new GameObject("WorkerRequirement").transform;
        holder.SetParent(transform, false);
        holder.localPosition = Vector3.up * _requirementHeight;
        holder.localRotation = Quaternion.identity;

        Vector3 lossy = transform.lossyScale;
        holder.localScale = new Vector3(InverseScale(lossy.x), InverseScale(lossy.y), InverseScale(lossy.z));

        _requirementView = Instantiate(_requirementViewPrefab, holder);
        _requirementView.transform.localPosition = Vector3.zero;
        _requirementView.transform.localRotation = Quaternion.identity;
    }

    private static float InverseScale(float scale)
    {
        return Mathf.Abs(scale) < 0.0001f ? 1f : 1f / scale;
    }
}
