using System.Collections.Generic;
using UnityEngine;
using Zenject;

[DefaultExecutionOrder(10000)] // after Cinemachine LateUpdate camera settle
public class CameraOccluderFade : MonoBehaviour
{
    private static readonly HashSet<OccluderFadeTarget> HitTargets = new();
    private static readonly List<OccluderFadeTarget> ActiveFadeTargets = new();

    [SerializeField] private float _targetHeightOffset = -0.5f;
    [SerializeField] private float _playerClearance = 0.3f;
    [SerializeField] private float _castRadius = 0.75f;
    [SerializeField] private int _maxHits = 32;
    [SerializeField] private Shader _fadeShader;
    [SerializeField] private bool _drawGizmos = true;

    private Transform _target;
    private Camera _camera;

    private Material _fadeMaterial;
    private Collider[] _overlapBuffer = new Collider[32];

    private readonly List<Vector3> _debugHitPoints = new();
    private Vector3 _debugCameraPosition;
    private Vector3 _debugAimPosition;
    private Vector3 _debugCapsuleEnd;
    private float _debugRadius;
    private bool _hasDebugRay;

    [Inject]
    private void Construct(Player player)
    {
        _target = player.CameraTarget.transform;
        _camera = Camera.main;
    }

    private void Awake()
    {
        Shader shader = _fadeShader != null ? _fadeShader : Shader.Find("MyLittleUniverse/OccluderFadeDither");
        if (shader == null)
        {
            Debug.LogError($"{nameof(CameraOccluderFade)}: fade shader not found.", this);
            enabled = false;
            return;
        }

        _fadeMaterial = new Material(shader);
        EnsureOverlapBufferCapacity(_maxHits);
    }

    private void OnDestroy()
    {
        if (_fadeMaterial != null)
            Destroy(_fadeMaterial);
    }

    private void LateUpdate()
    {
        if (_target == null || _camera == null || _fadeMaterial == null)
            return;

        DetectOccluders();
        UpdateFades();
    }

    private void DetectOccluders()
    {
        HitTargets.Clear();
        _debugHitPoints.Clear();
        _hasDebugRay = false;

        Vector3 cameraPosition = _camera.transform.position;
        Vector3 targetPosition = _target.position + Vector3.up * _targetHeightOffset;
        Vector3 toTarget = targetPosition - cameraPosition;
        float distanceToTarget = toTarget.magnitude;
        if (distanceToTarget <= 0.01f)
            return;

        Vector3 direction = toTarget / distanceToTarget;
        float capsuleLength = Mathf.Max(0f, distanceToTarget - _playerClearance);
        Vector3 capsuleEnd = cameraPosition + direction * capsuleLength;
        float radius = Mathf.Max(0.01f, _castRadius);

        _debugCameraPosition = cameraPosition;
        _debugAimPosition = targetPosition;
        _debugCapsuleEnd = capsuleEnd;
        _debugRadius = radius;
        _hasDebugRay = true;

        // OverlapCapsule covers a thick volume and includes colliders already around the camera
        // (unlike Raycast/SphereCast, which miss objects overlapping the cast origin).
        int hitCount = Physics.OverlapCapsuleNonAlloc(
            cameraPosition,
            capsuleEnd,
            radius,
            _overlapBuffer,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Collide);

        if (hitCount == _overlapBuffer.Length)
        {
            EnsureOverlapBufferCapacity(_overlapBuffer.Length * 2);
            hitCount = Physics.OverlapCapsuleNonAlloc(
                cameraPosition,
                capsuleEnd,
                radius,
                _overlapBuffer,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Collide);
        }

        int limit = Mathf.Min(hitCount, _maxHits);
        for (int i = 0; i < limit; i++)
        {
            Collider hitCollider = _overlapBuffer[i];
            if (hitCollider == null)
                continue;

            OccluderFadeTarget fadeTarget = hitCollider.GetComponentInParent<OccluderFadeTarget>();
            if (fadeTarget == null)
                continue;

            if (HitTargets.Add(fadeTarget))
                _debugHitPoints.Add(hitCollider.bounds.ClosestPoint(cameraPosition));
        }

        for (int i = ActiveFadeTargets.Count - 1; i >= 0; i--)
        {
            OccluderFadeTarget fadeTarget = ActiveFadeTargets[i];
            if (fadeTarget == null)
            {
                ActiveFadeTargets.RemoveAt(i);
                continue;
            }

            fadeTarget.SetOccluding(HitTargets.Contains(fadeTarget));
        }

        foreach (OccluderFadeTarget hitTarget in HitTargets)
        {
            if (!ActiveFadeTargets.Contains(hitTarget))
                ActiveFadeTargets.Add(hitTarget);

            hitTarget.SetOccluding(true);
        }
    }

    private void UpdateFades()
    {
        float deltaTime = Time.deltaTime;

        for (int i = ActiveFadeTargets.Count - 1; i >= 0; i--)
        {
            OccluderFadeTarget fadeTarget = ActiveFadeTargets[i];
            if (fadeTarget == null)
            {
                ActiveFadeTargets.RemoveAt(i);
                continue;
            }

            fadeTarget.TickFade(deltaTime, _fadeMaterial);

            if (!fadeTarget.NeedsUpdate && !HitTargets.Contains(fadeTarget))
                ActiveFadeTargets.RemoveAt(i);
        }
    }

    public static void Unregister(OccluderFadeTarget target)
    {
        ActiveFadeTargets.Remove(target);
        HitTargets.Remove(target);
    }

    private void EnsureOverlapBufferCapacity(int capacity)
    {
        if (_overlapBuffer != null && _overlapBuffer.Length >= capacity)
            return;

        _overlapBuffer = new Collider[Mathf.Max(capacity, 32)];
    }

    private void OnDrawGizmos()
    {
        if (!_drawGizmos || !_hasDebugRay)
            return;

        // Aim point
        Gizmos.color = new Color(0.3f, 0.85f, 1f, 0.9f);
        Gizmos.DrawWireSphere(_debugCameraPosition, 0.08f);
        Gizmos.DrawWireSphere(_debugAimPosition, 0.12f);
        Gizmos.DrawLine(_debugCameraPosition, _debugAimPosition);

        // Detection capsule (camera → clearance end)
        Gizmos.color = new Color(1f, 0.92f, 0.2f, 0.85f);
        DrawWireCapsule(_debugCameraPosition, _debugCapsuleEnd, _debugRadius);

        // Clearance zone near player
        Gizmos.color = new Color(1f, 0.45f, 0.1f, 0.9f);
        Gizmos.DrawLine(_debugCapsuleEnd, _debugAimPosition);
        Gizmos.DrawWireSphere(_debugCapsuleEnd, 0.1f);

        // Hit markers
        Gizmos.color = Color.magenta;
        for (int i = 0; i < _debugHitPoints.Count; i++)
            Gizmos.DrawSphere(_debugHitPoints[i], 0.1f);

        // Currently occluding targets
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.85f);
        foreach (OccluderFadeTarget hitTarget in HitTargets)
        {
            if (hitTarget == null)
                continue;

            Gizmos.DrawWireSphere(hitTarget.transform.position, 0.35f);
        }

        // Restoring opacity
        Gizmos.color = new Color(0.2f, 1f, 0.35f, 0.7f);
        for (int i = 0; i < ActiveFadeTargets.Count; i++)
        {
            OccluderFadeTarget fadeTarget = ActiveFadeTargets[i];
            if (fadeTarget == null || HitTargets.Contains(fadeTarget))
                continue;

            Gizmos.DrawWireSphere(fadeTarget.transform.position, 0.28f);
        }
    }

    private static void DrawWireCapsule(Vector3 start, Vector3 end, float radius)
    {
        Gizmos.DrawWireSphere(start, radius);
        Gizmos.DrawWireSphere(end, radius);

        Vector3 axis = end - start;
        if (axis.sqrMagnitude < 0.0001f)
            return;

        Vector3 up = axis.normalized;
        Vector3 right = Vector3.Cross(up, Vector3.up);
        if (right.sqrMagnitude < 0.001f)
            right = Vector3.Cross(up, Vector3.right);
        right.Normalize();
        Vector3 forward = Vector3.Cross(right, up);

        Gizmos.DrawLine(start + right * radius, end + right * radius);
        Gizmos.DrawLine(start - right * radius, end - right * radius);
        Gizmos.DrawLine(start + forward * radius, end + forward * radius);
        Gizmos.DrawLine(start - forward * radius, end - forward * radius);
    }
}
